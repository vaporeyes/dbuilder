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

    public void Dispose() { if (owns) wad.Dispose(); }
}

internal sealed class Pk3ResourceReader : IResourceReader
{
    private readonly ZipArchive zip;
    private readonly Stream? ownedStream;
    // key = "<folder>/<BASENAME>" with folder lowercased and basename uppercased (Doom-style names).
    private readonly Dictionary<string, ZipArchiveEntry> entries = new(StringComparer.Ordinal);

    public Pk3ResourceReader(Stream zipStream, bool ownsStream)
    {
        ownedStream = ownsStream ? zipStream : null;
        zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: !ownsStream);
        foreach (var e in zip.Entries)
        {
            if (e.FullName.EndsWith("/")) continue; // directory entry
            string norm = e.FullName.Replace('\\', '/');
            int slash = norm.LastIndexOf('/');
            string folder = slash >= 0 ? norm.Substring(0, slash).ToLowerInvariant() : "";
            string file = slash >= 0 ? norm.Substring(slash + 1) : norm;
            int dot = file.LastIndexOf('.');
            string baseName = (dot >= 0 ? file.Substring(0, dot) : file).ToUpperInvariant();
            entries[folder + "/" + baseName] = e; // last entry of a name wins
        }
    }

    public DoomPalette? GetPalette()
    {
        var e = Find("PLAYPAL", "");
        return e != null ? DoomPalette.FromBytes(Read(e)) : null;
    }

    public ImageData? GetFlat(string name, DoomPalette? palette)
        => Decode(Find(name, "flats", ""), palette, preferFlat: true);

    public ImageData? GetWallTexture(string name, DoomPalette? palette)
        => Decode(Find(name, "textures", "patches"), palette, preferFlat: false);

    public ImageData? GetSprite(string name, DoomPalette? palette)
        => Decode(Find(name, "sprites", "graphics", "patches", ""), palette, preferFlat: false);

    private ZipArchiveEntry? Find(string name, params string[] folders)
    {
        string key = name.ToUpperInvariant();
        foreach (var f in folders)
            if (entries.TryGetValue(f.ToLowerInvariant() + "/" + key, out var e)) return e;
        return null;
    }

    private ImageData? Decode(ZipArchiveEntry? entry, DoomPalette? palette, bool preferFlat)
    {
        if (entry == null) return null;
        byte[] bytes = Read(entry);

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

    private static byte[] Read(ZipArchiveEntry e)
    {
        using var s = e.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public void Dispose()
    {
        zip.Dispose();
        ownedStream?.Dispose();
    }
}
