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
    /// <summary>The raw bytes of a named lump (e.g. ANIMATED, PLAYPAL) if this resource has one, else null.</summary>
    byte[]? GetLumpBytes(string name);
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
        return pic != null ? new ImageData(pic.Width, pic.Height, pic.Rgba8, pic.OffsetX, pic.OffsetY) : null;
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

    public byte[]? GetLumpBytes(string name) => wad.FindLump(name)?.Stream.ReadAllBytes();

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

    public virtual DoomPalette? GetPalette()
    {
        var b = Find("PLAYPAL", "");
        return b != null ? DoomPalette.FromBytes(b) : null;
    }

    public virtual ImageData? GetFlat(string name, DoomPalette? palette)
        => Decode(Find(name, "flats", ""), palette, preferFlat: true);

    public virtual ImageData? GetWallTexture(string name, DoomPalette? palette)
        => Decode(Find(name, "textures", "patches"), palette, preferFlat: false);

    public virtual ImageData? GetSprite(string name, DoomPalette? palette)
        => Decode(Find(name, "sprites", "graphics", "patches", ""), palette, preferFlat: false);

    public virtual string? GetTextLump(string name)
    {
        var b = Find(name, "", name.ToLowerInvariant());
        return b != null ? System.Text.Encoding.ASCII.GetString(b) : null;
    }

    public virtual byte[]? GetLumpBytes(string name) => Find(name, "");

    public virtual IEnumerable<string> TextureNames() => NamesInFolder("textures/");
    public virtual IEnumerable<string> FlatNames() => NamesInFolder("flats/");

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
            if (pic != null) return new ImageData(pic.Width, pic.Height, pic.Rgba8, pic.OffsetX, pic.OffsetY);
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
    private readonly List<IResourceReader> nestedReaders = new();
    private readonly List<MemoryStream> nestedStreams = new();

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

            if (Path.GetExtension(e.FullName).Equals(".wad", StringComparison.OrdinalIgnoreCase))
            {
                using var s = e.Open();
                var ms = new MemoryStream();
                s.CopyTo(ms);
                ms.Position = 0;
                nestedStreams.Add(ms);
                nestedReaders.Add(new WadResourceReader(new WAD(ms, openreadonly: true, virtualFilename: e.FullName), owns: true));
            }
            else if (LooksLikeNestedZip(e.FullName))
            {
                using var s = e.Open();
                var ms = new MemoryStream();
                s.CopyTo(ms);
                ms.Position = 0;
                nestedStreams.Add(ms);
                nestedReaders.Add(new Pk3ResourceReader(ms, ownsStream: false));
            }
        }
    }

    public override DoomPalette? GetPalette()
    {
        var palette = base.GetPalette();
        if (palette != null) return palette;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            palette = nestedReaders[i].GetPalette();
            if (palette != null) return palette;
        }

        return null;
    }

    public override ImageData? GetFlat(string name, DoomPalette? palette)
    {
        var image = base.GetFlat(name, palette);
        if (image != null) return image;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            image = nestedReaders[i].GetFlat(name, palette);
            if (image != null) return image;
        }

        return null;
    }

    public override ImageData? GetWallTexture(string name, DoomPalette? palette)
    {
        var image = base.GetWallTexture(name, palette);
        if (image != null) return image;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            image = nestedReaders[i].GetWallTexture(name, palette);
            if (image != null) return image;
        }

        return null;
    }

    public override ImageData? GetSprite(string name, DoomPalette? palette)
    {
        var image = base.GetSprite(name, palette);
        if (image != null) return image;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            image = nestedReaders[i].GetSprite(name, palette);
            if (image != null) return image;
        }

        return null;
    }

    public override string? GetTextLump(string name)
    {
        var text = base.GetTextLump(name);
        if (text != null) return text;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            text = nestedReaders[i].GetTextLump(name);
            if (text != null) return text;
        }

        return null;
    }

    public override byte[]? GetLumpBytes(string name)
    {
        var bytes = base.GetLumpBytes(name);
        if (bytes != null) return bytes;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            bytes = nestedReaders[i].GetLumpBytes(name);
            if (bytes != null) return bytes;
        }

        return null;
    }

    public override IEnumerable<string> TextureNames()
    {
        foreach (var name in base.TextureNames()) yield return name;
        foreach (var reader in nestedReaders)
            foreach (var name in reader.TextureNames())
                yield return name;
    }

    public override IEnumerable<string> FlatNames()
    {
        foreach (var name in base.FlatNames()) yield return name;
        foreach (var reader in nestedReaders)
            foreach (var name in reader.FlatNames())
                yield return name;
    }

    private static bool LooksLikeNestedZip(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".pk3", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".pk7", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".pkz", StringComparison.OrdinalIgnoreCase);
    }

    public override void Dispose()
    {
        foreach (var reader in nestedReaders) reader.Dispose();
        foreach (var stream in nestedStreams) stream.Dispose();
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
