// ABOUTME: Resource reader abstraction over a WAD or a PK3 (zip), resolving flat/texture/sprite names to RGBA.
// ABOUTME: WadResourceReader composes Doom textures; Pk3ResourceReader reads folder-structured PNG/Doom entries.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace DBuilder.IO;

/// <summary>A single resource (WAD or PK3) able to resolve images by name against the active palette.</summary>
internal interface IResourceReader : IDisposable
{
    DoomPalette? GetPalette();
    ImageData? GetFlat(string name, DoomPalette? palette);
    ImageData? GetWallTexture(string name, DoomPalette? palette);
    ImageData? GetSprite(string name, DoomPalette? palette);
    /// <summary>The text of a named lump (e.g. TEXTURES, DECORATE) if this resource has one, else null.</summary>
    string? GetTextLump(string name);
    /// <summary>Names of the wall textures this resource provides (for the texture browser).</summary>
    IEnumerable<string> TextureNames();
    /// <summary>Names of the flats this resource provides (for the texture browser).</summary>
    IEnumerable<string> FlatNames();
}

internal sealed class WadResourceReader : IResourceReader
{
    private readonly WAD wad;
    private readonly bool owns;
    private Dictionary<string, DoomTextureDef>? texDefs;
    private DoomPatchNames? patchNames;

    public WadResourceReader(WAD wad, bool owns) { this.wad = wad; this.owns = owns; }

    public DoomPalette? GetPalette() => DoomPalette.FromWad(wad);

    public ImageData? GetFlat(string name, DoomPalette? palette)
    {
        if (palette == null) return null;
        byte[]? rgba = DoomFlatReader.DecodeRgba8(wad, name, palette);
        return rgba != null ? new ImageData(DoomFlatReader.Width, DoomFlatReader.Height, rgba) : null;
    }

    public ImageData? GetWallTexture(string name, DoomPalette? palette)
    {
        if (palette == null) return null;
        var defs = TexDefs();
        if (!defs.TryGetValue(name, out var def)) return null;
        byte[]? rgba = DoomWallTextureCompositor.Compose(def, PatchNames(), wad, palette);
        return rgba != null ? new ImageData(def.Width, def.Height, rgba) : null;
    }

    public ImageData? GetSprite(string name, DoomPalette? palette)
    {
        if (palette == null) return null;
        var pic = DoomPictureReader.Decode(wad, name, palette);
        return pic != null ? new ImageData(pic.Width, pic.Height, pic.Rgba8) : null;
    }

    private Dictionary<string, DoomTextureDef> TexDefs()
    {
        if (texDefs != null) return texDefs;
        texDefs = new Dictionary<string, DoomTextureDef>(StringComparer.OrdinalIgnoreCase);
        foreach (var lumpName in new[] { "TEXTURE1", "TEXTURE2" })
        {
            var list = DoomTextureListReader.FromWad(wad, lumpName);
            if (list == null) continue;
            foreach (var def in list) texDefs[def.Name] = def;
        }
        return texDefs;
    }

    private DoomPatchNames PatchNames() => patchNames ??= DoomPatchNames.FromWad(wad) ?? DoomPatchNames.Empty;

    public string? GetTextLump(string name)
    {
        var lump = wad.FindLump(name);
        return lump != null ? System.Text.Encoding.ASCII.GetString(lump.Stream.ReadAllBytes()) : null;
    }

    public IEnumerable<string> TextureNames() => TexDefs().Keys;

    public IEnumerable<string> FlatNames()
    {
        // Flats live between F_START/F_END (and FF_/F1_/F2_/F3_) namespace markers.
        var result = new List<string>();
        bool inFlats = false;
        foreach (var l in wad.Lumps)
        {
            string n = l.Name;
            if (n is "F_START" or "FF_START" or "F1_START" or "F2_START" or "F3_START") { inFlats = true; continue; }
            if (n is "F_END" or "FF_END" or "F1_END" or "F2_END" or "F3_END") { inFlats = false; continue; }
            if (inFlats && l.Length > 0) result.Add(n);
        }
        return result;
    }

    public void Dispose() { if (owns) wad.Dispose(); }
}

// Shared base for folder-structured resources (PK3 zip or a filesystem directory): entries keyed by
// "<folder>/<BASENAME>" (folder lowercased, basename uppercased), each mapping to a byte-provider.
internal abstract class FolderResourceReader : IResourceReader
{
    protected readonly Dictionary<string, Func<byte[]>> entries = new(StringComparer.Ordinal);

    // Registers an entry from a relative path like "flats/floor1.png".
    protected void AddEntry(string relativePath, Func<byte[]> read)
    {
        string norm = relativePath.Replace('\\', '/');
        int slash = norm.LastIndexOf('/');
        string folder = slash >= 0 ? norm.Substring(0, slash).ToLowerInvariant() : "";
        string file = slash >= 0 ? norm.Substring(slash + 1) : norm;
        int dot = file.LastIndexOf('.');
        string baseName = (dot >= 0 ? file.Substring(0, dot) : file).ToUpperInvariant();
        entries[folder + "/" + baseName] = read; // last entry of a name wins
    }

    public DoomPalette? GetPalette()
    {
        var b = Find("PLAYPAL", "");
        return b != null ? DoomPalette.FromBytes(b) : null;
    }

    public ImageData? GetFlat(string name, DoomPalette? palette)
        => Decode(Find(name, "flats", ""), palette, preferFlat: true);

    public ImageData? GetWallTexture(string name, DoomPalette? palette)
        => Decode(Find(name, "textures", "patches"), palette, preferFlat: false);

    public ImageData? GetSprite(string name, DoomPalette? palette)
        => Decode(Find(name, "sprites", "graphics", "patches", ""), palette, preferFlat: false);

    public string? GetTextLump(string name)
    {
        var b = Find(name, "", name.ToLowerInvariant());
        return b != null ? System.Text.Encoding.ASCII.GetString(b) : null;
    }

    public IEnumerable<string> TextureNames() => NamesInFolder("textures/");
    public IEnumerable<string> FlatNames() => NamesInFolder("flats/");

    private byte[]? Find(string name, params string[] folders)
    {
        string key = name.ToUpperInvariant();
        foreach (var f in folders)
            if (entries.TryGetValue(f.ToLowerInvariant() + "/" + key, out var read)) return read();
        return null;
    }

    private static ImageData? Decode(byte[]? bytes, DoomPalette? palette, bool preferFlat)
    {
        if (bytes == null) return null;
        if (PngDecoder.IsPng(bytes)) return PngDecoder.Decode(bytes);
        if (palette == null) return null;

        if (preferFlat && DoomFlatReader.LooksLikeFlat(bytes.Length))
            return new ImageData(DoomFlatReader.Width, DoomFlatReader.Height, DoomFlatReader.DecodeRgba8(bytes, palette));
        if (DoomPictureReader.Validate(bytes))
        {
            var pic = DoomPictureReader.Decode(bytes, palette);
            if (pic != null) return new ImageData(pic.Width, pic.Height, pic.Rgba8);
        }
        if (DoomFlatReader.LooksLikeFlat(bytes.Length))
            return new ImageData(DoomFlatReader.Width, DoomFlatReader.Height, DoomFlatReader.DecodeRgba8(bytes, palette));
        return null;
    }

    private IEnumerable<string> NamesInFolder(string prefix)
    {
        foreach (var key in entries.Keys)
            if (key.StartsWith(prefix, StringComparison.Ordinal)) yield return key.Substring(prefix.Length);
    }

    public abstract void Dispose();
}

internal sealed class Pk3ResourceReader : FolderResourceReader
{
    private readonly ZipArchive zip;
    private readonly Stream? ownedStream;

    public Pk3ResourceReader(Stream zipStream, bool ownsStream)
    {
        ownedStream = ownsStream ? zipStream : null;
        zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: !ownsStream);
        foreach (var e in zip.Entries)
        {
            if (e.FullName.EndsWith("/")) continue; // directory entry
            var entry = e;
            AddEntry(e.FullName, () =>
            {
                using var s = entry.Open();
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                return ms.ToArray();
            });
        }
    }

    public override void Dispose()
    {
        zip.Dispose();
        ownedStream?.Dispose();
    }
}

internal sealed class DirectoryResourceReader : FolderResourceReader
{
    public DirectoryResourceReader(string root)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(root, path);
            string p = path;
            AddEntry(rel, () => File.ReadAllBytes(p));
        }
    }

    public override void Dispose() { /* nothing to release */ }
}
