// ABOUTME: Resource reader abstraction over a WAD or a PK3 (zip), resolving flat/texture/sprite names to RGBA.
// ABOUTME: WadResourceReader composes Doom textures; Pk3ResourceReader reads folder-structured PNG/Doom entries.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace DBuilder.IO;

/// <summary>A single resource (WAD or PK3) able to resolve images by name against the active palette.</summary>
internal interface IResourceReader : IDisposable
{
    string DisplayName { get; }
    DoomPalette? GetPalette();
    ImageData? GetFlat(string name, DoomPalette? palette);
    ImageData? GetWallTexture(string name, DoomPalette? palette);
    ImageData? GetSprite(string name, DoomPalette? palette);
    ImageData? GetFlatBase(string name, DoomPalette? palette);
    ImageData? GetWallTextureBase(string name, DoomPalette? palette);
    ImageData? GetSpriteBase(string name, DoomPalette? palette);
    ImageData? GetHiRes(string name, DoomPalette? palette);
    ImageData? GetPatch(string name, DoomPalette? palette, bool includeMixedNamespaces);
    /// <summary>The text of a named lump (e.g. TEXTURES, DECORATE) if this resource has one, else null.</summary>
    string? GetTextLump(string name);
    /// <summary>The text of every matching lump or root text file in this resource, oldest first.</summary>
    IEnumerable<string> GetTextLumps(string name, bool partialTitleMatch);
    /// <summary>The text of a named lump or exact PK3/directory path if this resource has one, else null.</summary>
    string? GetTextResource(string name);
    /// <summary>The raw bytes of a named lump (e.g. ANIMATED, PLAYPAL) if this resource has one, else null.</summary>
    byte[]? GetLumpBytes(string name);
    /// <summary>Patch-name table for classic TEXTURE1/TEXTURE2 composition, or null when absent.</summary>
    DoomPatchNames? GetPatchNames();
    /// <summary>Raw colormap bytes by name, or null when this resource does not provide the colormap.</summary>
    byte[]? GetColormapBytes(string name);
    /// <summary>Names of colormap images this resource provides.</summary>
    IEnumerable<string> ColormapNames();
    /// <summary>Names of the wall textures this resource provides (for the texture browser).</summary>
    IEnumerable<string> TextureNames();
    /// <summary>Names of the flats this resource provides (for the texture browser).</summary>
    IEnumerable<string> FlatNames();
    /// <summary>Names of sprite frames this resource provides.</summary>
    IEnumerable<string> SpriteNames();
    /// <summary>Names of voxel model lumps or files this resource provides.</summary>
    IEnumerable<string> VoxelNames();
    /// <summary>Raw voxel model bytes, or null when this resource does not provide the model.</summary>
    byte[]? GetVoxelBytes(string name);
    /// <summary>Raw model or skin bytes by MODELDEF path, or null when this resource does not provide the file.</summary>
    byte[]? GetModelResourceBytes(string path);
}

internal sealed class WadResourceReader : IResourceReader
{
    private static readonly Regex Sprite6 = new(@"^\S{4}[A-Za-z\[\]\\][0-8]$", RegexOptions.Compiled);
    private static readonly Regex Sprite8 = new(@"^\S{4}[A-Za-z\[\]\\][0-8][A-Za-z\[\]\\][0-8]$", RegexOptions.Compiled);
    private static readonly Regex VoxelName = new(@"^\S{4}(([A-Za-z][0-9])?|[A-Za-z]?)$", RegexOptions.Compiled);
    private static readonly (string Start, string End)[] PatchRanges =
    {
        ("P_START", "P_END"),
        ("PP_START", "PP_END"),
        ("P1_START", "P1_END"),
        ("P2_START", "P2_END"),
        ("P3_START", "P3_END"),
    };
    private readonly WAD wad;
    private readonly bool owns;
    private readonly bool strictPatches;
    private Dictionary<string, DoomTextureDef>? texDefs;
    private DoomPatchNames? patchNames;

    public WadResourceReader(WAD wad, bool owns, bool strictPatches = false) { this.wad = wad; this.owns = owns; this.strictPatches = strictPatches; }

    public string DisplayName => string.IsNullOrEmpty(wad.Filename) ? "WAD resource" : Path.GetFileName(wad.Filename);

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
        byte[]? rgba = DoomWallTextureCompositor.Compose(def, PatchNames(), wad, palette, FindPatchLump);
        return rgba != null ? new ImageData(def.Width, def.Height, rgba) : null;
    }

    public ImageData? GetSprite(string name, DoomPalette? palette)
    {
        if (palette == null) return null;
        var pic = DoomPictureReader.Decode(wad, name, palette);
        return pic != null ? new ImageData(pic.Width, pic.Height, pic.Rgba8, pic.OffsetX, pic.OffsetY) : null;
    }

    public ImageData? GetFlatBase(string name, DoomPalette? palette) => GetFlat(name, palette);

    public ImageData? GetWallTextureBase(string name, DoomPalette? palette) => GetWallTexture(name, palette);

    public ImageData? GetSpriteBase(string name, DoomPalette? palette) => GetSprite(name, palette);

    public ImageData? GetHiRes(string name, DoomPalette? palette) => null;

    public ImageData? GetPatch(string name, DoomPalette? palette, bool includeMixedNamespaces)
    {
        if (palette == null || FindPatchLump(name) is not { } lump) return null;
        var pic = DoomPictureReader.Decode(lump.Stream.ReadAllBytes(), palette);
        return pic != null ? new ImageData(pic.Width, pic.Height, pic.Rgba8, pic.OffsetX, pic.OffsetY) : null;
    }

    private Lump? FindPatchLump(string name)
    {
        foreach (var (startName, endName) in PatchRanges)
        {
            int start = wad.FindLumpIndex(startName);
            while (start >= 0)
            {
                int end = wad.FindLumpIndex(endName, start + 1);
                if (end < 0) break;
                var lump = wad.FindLump(name, start, end);
                if (lump != null) return lump;
                start = wad.FindLumpIndex(startName, end + 1);
            }
        }

        return strictPatches ? null : wad.FindLump(name);
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
        foreach (string text in GetTextLumps(name, partialTitleMatch: false)) return text;
        return null;
    }

    public IEnumerable<string> GetTextLumps(string name, bool partialTitleMatch)
    {
        int index = wad.FindLumpIndex(name);
        while (index != -1)
        {
            yield return System.Text.Encoding.ASCII.GetString(wad.Lumps[index].Stream.ReadAllBytes());
            index = wad.FindLumpIndex(name, index + 1);
        }
    }

    public string? GetTextResource(string name) => GetTextLump(name);

    public byte[]? GetLumpBytes(string name) => wad.FindLump(name)?.Stream.ReadAllBytes();

    public DoomPatchNames? GetPatchNames() => DoomPatchNames.FromWad(wad);

    public byte[]? GetColormapBytes(string name) => GetLumpBytes(name);

    public IEnumerable<string> ColormapNames() => Array.Empty<string>();

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

    public IEnumerable<string> SpriteNames()
    {
        bool inSprites = false;
        foreach (var l in wad.Lumps)
        {
            string n = l.Name;
            if (n is "S_START" or "SS_START") { inSprites = true; continue; }
            if (n is "S_END" or "SS_END") { inSprites = false; continue; }
            if (inSprites && IsValidSpriteName(n)) yield return n;
        }
    }

    public IEnumerable<string> VoxelNames()
    {
        bool inVoxels = false;
        foreach (var l in wad.Lumps)
        {
            string n = l.Name;
            if (n is "VX_START" or "V_START") { inVoxels = true; continue; }
            if (n is "VX_END" or "V_END") { inVoxels = false; continue; }
            if (inVoxels && IsValidVoxelName(n)) yield return n;
        }
    }

    public byte[]? GetVoxelBytes(string name)
    {
        string shortName = VoxelLookupName(name);
        bool inVoxels = false;
        foreach (var l in wad.Lumps)
        {
            string n = l.Name;
            if (n is "VX_START" or "V_START") { inVoxels = true; continue; }
            if (n is "VX_END" or "V_END") { inVoxels = false; continue; }
            if (inVoxels && string.Equals(n, shortName, StringComparison.OrdinalIgnoreCase))
                return l.Stream.ReadAllBytes();
        }
        return null;
    }

    public byte[]? GetModelResourceBytes(string path) => null;

    internal static bool IsValidSpriteName(string name)
        => (name.Length == 6 && Sprite6.IsMatch(name)) || (name.Length == 8 && Sprite8.IsMatch(name));

    internal static bool IsValidVoxelName(string name) => name.Length > 3 && name.Length < 7 && VoxelName.IsMatch(name);

    internal static string VoxelLookupName(string name)
    {
        string file = Path.GetFileNameWithoutExtension(name.Replace('\\', '/'));
        return file.ToUpperInvariant();
    }

    public void Dispose() { if (owns) wad.Dispose(); }
}

// Shared base for folder-structured resources (PK3 zip or a filesystem directory): entries keyed by
// "<folder>/<BASENAME>" (folder lowercased, basename uppercased), each mapping to a byte-provider.
internal abstract class FolderResourceReader : IResourceReader
{
    protected readonly Dictionary<string, Func<byte[]>> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<byte[]>> files = new(StringComparer.OrdinalIgnoreCase);
    protected readonly List<IResourceReader> nestedReaders = new();
    private Dictionary<string, DoomTextureDef>? classicTextureDefs;
    private DoomPatchNames? classicPatchNames;
    private readonly bool rootTextures;
    private readonly bool rootFlats;

    protected FolderResourceReader(string displayName, bool rootTextures = false, bool rootFlats = false)
    {
        DisplayName = displayName;
        this.rootTextures = rootTextures;
        this.rootFlats = rootFlats;
    }

    public string DisplayName { get; }

    // Registers an entry from a relative path like "flats/floor1.png".
    protected void AddEntry(string relativePath, Func<byte[]> read)
    {
        string norm = relativePath.Replace('\\', '/');
        files[norm.TrimStart('/')] = read;
        int slash = norm.LastIndexOf('/');
        string folder = slash >= 0 ? norm.Substring(0, slash).ToLowerInvariant() : "";
        string file = slash >= 0 ? norm.Substring(slash + 1) : norm;
        int dot = file.LastIndexOf('.');
        string baseName = (dot >= 0 ? file.Substring(0, dot) : file).ToUpperInvariant();
        entries[folder + "/" + baseName] = read; // last entry of a name wins
    }

    public virtual DoomPalette? GetPalette()
    {
        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            var palette = nestedReaders[i].GetPalette();
            if (palette != null) return palette;
        }

        var b = Find("PLAYPAL", "");
        return b != null ? DoomPalette.FromBytes(b) : null;
    }

    public virtual ImageData? GetFlat(string name, DoomPalette? palette)
    {
        var image = GetFlatBase(name, palette);
        if (image == null) return null;

        return GetHiRes(name, palette) ?? image;
    }

    public virtual ImageData? GetWallTexture(string name, DoomPalette? palette)
    {
        var image = GetWallTextureBase(name, palette);
        if (image == null) return null;

        return GetHiRes(name, palette) ?? image;
    }

    public virtual ImageData? GetSprite(string name, DoomPalette? palette)
    {
        var image = GetSpriteBase(name, palette);
        if (image == null) return null;

        return GetHiRes(name, palette) ?? image;
    }

    public virtual ImageData? GetFlatBase(string name, DoomPalette? palette)
    {
        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            var nestedImage = nestedReaders[i].GetFlatBase(name, palette);
            if (nestedImage != null) return nestedImage;
        }

        var image = rootFlats
            ? Decode(Find(name, "", "flats"), palette, preferFlat: true)
            : Decode(Find(name, "flats"), palette, preferFlat: true);
        return image;
    }

    public virtual ImageData? GetWallTextureBase(string name, DoomPalette? palette)
    {
        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            var nestedImage = nestedReaders[i].GetWallTextureBase(name, palette);
            if (nestedImage != null) return nestedImage;
        }

        var image = rootTextures
            ? Decode(Find(name, "", "textures", "patches"), palette, preferFlat: false)
            : Decode(Find(name, "textures", "patches"), palette, preferFlat: false);
        if (image != null) return image;

        image = ComposeClassicTexture(name, palette);
        return image;
    }

    public virtual ImageData? GetSpriteBase(string name, DoomPalette? palette)
    {
        var image = Decode(Find(name, "sprites", "graphics", "patches", ""), palette, preferFlat: false);
        if (image != null) return image;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            image = nestedReaders[i].GetSpriteBase(name, palette);
            if (image != null) return image;
        }

        return null;
    }

    public virtual ImageData? GetHiRes(string name, DoomPalette? palette) => Decode(Find(name, "hires"), palette, preferFlat: false);

    public virtual ImageData? GetPatch(string name, DoomPalette? palette, bool includeMixedNamespaces)
    {
        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            var nestedImage = nestedReaders[i].GetPatch(name, palette, includeMixedNamespaces);
            if (nestedImage != null) return nestedImage;
        }

        string[] folders = includeMixedNamespaces
            ? new[] { "patches", "textures", "flats", "sprites", "graphics" }
            : new[] { "patches" };
        var image = Decode(Find(name, folders), palette, preferFlat: false);
        if (image != null) return image;

        return null;
    }

    public virtual string? GetTextLump(string name)
    {
        foreach (string text in GetTextLumps(name, partialTitleMatch: false)) return text;
        return null;
    }

    public virtual IEnumerable<string> GetTextLumps(string name, bool partialTitleMatch)
    {
        foreach (string text in LocalTextLumps(name, partialTitleMatch)) yield return text;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
            foreach (string text in nestedReaders[i].GetTextLumps(name, partialTitleMatch))
                yield return text;
    }

    public virtual string? GetTextResource(string name)
    {
        string normalized = name.Replace('\\', '/').TrimStart('/');
        if (files.TryGetValue(normalized, out var read)) return System.Text.Encoding.ASCII.GetString(read());

        var text = GetTextLump(name);
        if (text != null) return text;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            text = nestedReaders[i].GetTextResource(name);
            if (text != null) return text;
        }

        return null;
    }

    public virtual byte[]? GetLumpBytes(string name)
    {
        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            var bytes = nestedReaders[i].GetLumpBytes(name);
            if (bytes != null) return bytes;
        }

        return Find(name, "");
    }

    public virtual DoomPatchNames? GetPatchNames()
    {
        for (int i = nestedReaders.Count - 1; i >= 0; i--)
            if (nestedReaders[i].GetPatchNames() is { } pnames)
                return pnames;

        var bytes = Find("PNAMES", "");
        return bytes != null ? DoomPatchNames.FromBytes(bytes) : null;
    }

    public virtual byte[]? GetColormapBytes(string name)
    {
        string normalized = name.Replace('\\', '/').TrimStart('/');
        string? folder = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
        string file = Path.GetFileName(normalized);
        byte[]? bytes = string.IsNullOrWhiteSpace(folder)
            ? Find(file, "colormaps")
            : Find(file, "colormaps/" + folder);
        if (bytes != null) return bytes;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            bytes = nestedReaders[i].GetColormapBytes(name);
            if (bytes != null) return bytes;
        }

        return null;
    }

    public virtual IEnumerable<string> TextureNames()
    {
        foreach (var name in NamesInFolder("textures/")) yield return name;
        foreach (var name in ClassicTextureDefs().Keys) yield return name;
        foreach (var reader in nestedReaders)
            foreach (var name in reader.TextureNames())
                yield return name;
    }

    public virtual IEnumerable<string> ColormapNames()
    {
        foreach (var name in NamesInFolder("colormaps/")) yield return name;
        foreach (var reader in nestedReaders)
            foreach (var name in reader.ColormapNames())
                yield return name;
    }

    public virtual IEnumerable<string> FlatNames()
    {
        foreach (var name in NamesInFolder("flats/")) yield return name;
        foreach (var reader in nestedReaders)
            foreach (var name in reader.FlatNames())
                yield return name;
    }

    public virtual IEnumerable<string> SpriteNames()
    {
        foreach (var name in NamesInFolder("sprites/"))
            if (WadResourceReader.IsValidSpriteName(name))
                yield return name;
        foreach (var reader in nestedReaders)
            foreach (var name in reader.SpriteNames())
                yield return name;
    }

    public virtual IEnumerable<string> VoxelNames()
    {
        foreach (var name in NamesInFolder("voxels/"))
            if (WadResourceReader.IsValidVoxelName(name))
                yield return name;
        foreach (var reader in nestedReaders)
            foreach (var name in reader.VoxelNames())
                yield return name;
    }

    public virtual byte[]? GetVoxelBytes(string name)
    {
        string normalized = name.Replace('\\', '/');
        if (files.TryGetValue(normalized.TrimStart('/'), out var read)) return read();

        string lookup = WadResourceReader.VoxelLookupName(normalized);
        string? folder = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
        byte[]? bytes = string.IsNullOrWhiteSpace(folder)
            ? Find(lookup, "voxels", "")
            : Find(lookup, folder);
        if (bytes != null) return bytes;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            bytes = nestedReaders[i].GetVoxelBytes(name);
            if (bytes != null) return bytes;
        }

        return null;
    }

    public virtual byte[]? GetModelResourceBytes(string path)
    {
        string normalized = path.Replace('\\', '/').TrimStart('/');
        if (files.TryGetValue(normalized, out var read)) return read();

        string lookup = WadResourceReader.VoxelLookupName(normalized);
        string? folder = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
        byte[]? bytes = string.IsNullOrWhiteSpace(folder)
            ? Find(lookup, "models", "")
            : Find(lookup, folder);
        if (bytes != null) return bytes;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            bytes = nestedReaders[i].GetModelResourceBytes(path);
            if (bytes != null) return bytes;
        }

        return null;
    }

    private byte[]? Find(string name, params string[] folders)
    {
        if (IsPathQualified(name) && TryFindPath(name, out var bytes)) return bytes;

        string key = name.ToUpperInvariant();
        foreach (var f in folders)
            if (entries.TryGetValue(f.ToLowerInvariant() + "/" + key, out var read)) return read();
        return null;
    }

    private bool TryFindPath(string path, out byte[] bytes)
    {
        string normalized = path.Replace('\\', '/').TrimStart('/');
        int slash = normalized.LastIndexOf('/');
        string folder = slash >= 0 ? normalized.Substring(0, slash).ToLowerInvariant() : "";
        string file = slash >= 0 ? normalized.Substring(slash + 1) : normalized;
        int dot = file.LastIndexOf('.');
        string baseName = (dot >= 0 ? file.Substring(0, dot) : file).ToUpperInvariant();
        if (entries.TryGetValue(folder + "/" + baseName, out var read))
        {
            bytes = read();
            return true;
        }

        bytes = Array.Empty<byte>();
        return false;
    }

    private static bool IsPathQualified(string name) => name.Contains('/') || name.Contains('\\');

    private ImageData? ComposeClassicTexture(string name, DoomPalette? palette)
    {
        if (!ClassicTextureDefs().TryGetValue(name, out var def)) return null;
        if (def.Width <= 0 || def.Height <= 0 || def.Patches.Count == 0) return null;

        var canvas = new byte[def.Width * def.Height * 4];
        bool anyPatched = false;
        var pnames = ClassicPatchNames();

        foreach (var patch in def.Patches)
        {
            if (patch.PatchIndex < 0 || patch.PatchIndex >= pnames.Length) continue;
            string patchName = pnames[patch.PatchIndex];
            if (string.IsNullOrEmpty(patchName)) continue;

            var image = GetPatch(patchName, palette, includeMixedNamespaces: false);
            if (image == null) continue;

            BlitOpaque(canvas, def.Width, def.Height, image, patch.OriginX, patch.OriginY);
            anyPatched = true;
        }

        return anyPatched ? new ImageData(def.Width, def.Height, canvas) : null;
    }

    private Dictionary<string, DoomTextureDef> ClassicTextureDefs()
    {
        if (classicTextureDefs != null) return classicTextureDefs;

        classicTextureDefs = new Dictionary<string, DoomTextureDef>(StringComparer.OrdinalIgnoreCase);
        foreach (string lumpName in new[] { "TEXTURE1", "TEXTURE2" })
        {
            var bytes = Find(lumpName, "");
            if (bytes == null) continue;
            foreach (var def in DoomTextureListReader.Parse(bytes))
                classicTextureDefs[def.Name] = def;
        }
        return classicTextureDefs;
    }

    private DoomPatchNames ClassicPatchNames()
    {
        if (classicPatchNames != null) return classicPatchNames;
        classicPatchNames = GetPatchNames() ?? DoomPatchNames.Empty;
        return classicPatchNames;
    }

    private static void BlitOpaque(byte[] dst, int dstWidth, int dstHeight, ImageData src, int dstX, int dstY)
    {
        for (int sy = 0; sy < src.Height; sy++)
        {
            int y = dstY + sy;
            if (y < 0 || y >= dstHeight) continue;

            for (int sx = 0; sx < src.Width; sx++)
            {
                int x = dstX + sx;
                if (x < 0 || x >= dstWidth) continue;

                int si = (sy * src.Width + sx) * 4;
                if (src.Rgba[si + 3] == 0) continue;

                int di = (y * dstWidth + x) * 4;
                dst[di] = src.Rgba[si];
                dst[di + 1] = src.Rgba[si + 1];
                dst[di + 2] = src.Rgba[si + 2];
                dst[di + 3] = src.Rgba[si + 3];
            }
        }
    }

    private IEnumerable<string> LocalTextLumps(string name, bool partialTitleMatch)
    {
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string normalizedName = name.ToLowerInvariant();
        var paths = new List<string>(files.Keys);
        paths.Sort(StringComparer.OrdinalIgnoreCase);

        foreach (string path in paths)
        {
            if (!IsRootTextLumpMatch(path, normalizedName, partialTitleMatch)) continue;
            matched.Add(path);
            yield return System.Text.Encoding.ASCII.GetString(files[path]());
        }

        if (!partialTitleMatch)
        {
            string folderKey = normalizedName + "/" + name.ToUpperInvariant();
            if (entries.TryGetValue(folderKey, out var read) && matched.Add(folderKey))
                yield return System.Text.Encoding.ASCII.GetString(read());
        }
    }

    private static bool IsRootTextLumpMatch(string path, string name, bool partialTitleMatch)
    {
        string normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains('/')) return false;

        string title = Path.GetFileNameWithoutExtension(normalized).ToLowerInvariant();
        return partialTitleMatch ? title.StartsWith(name, StringComparison.Ordinal) : title == name;
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
    private readonly List<MemoryStream> nestedStreams = new();

    public Pk3ResourceReader(Stream zipStream, bool ownsStream, string displayName = "PK3 resource", bool rootTextures = false, bool rootFlats = false)
        : base(displayName, rootTextures, rootFlats)
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
            else if (ArchivePath.IsPk3FamilyPath(e.FullName))
            {
                using var s = e.Open();
                var ms = new MemoryStream();
                s.CopyTo(ms);
                ms.Position = 0;
                nestedStreams.Add(ms);
                nestedReaders.Add(new Pk3ResourceReader(ms, ownsStream: false, displayName: e.FullName));
            }
        }
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
    public DirectoryResourceReader(string root) : base(Path.GetFileName(Path.TrimEndingDirectorySeparator(root)))
    {
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(root, path);
            string p = path;
            if (IsRootWad(rel))
                nestedReaders.Add(new WadResourceReader(new WAD(path, openreadonly: true), owns: true));
            AddEntry(rel, () => File.ReadAllBytes(p));
        }
    }

    private static bool IsRootWad(string relativePath)
        => Path.GetDirectoryName(relativePath) is null or ""
            && Path.GetExtension(relativePath).Equals(".wad", StringComparison.OrdinalIgnoreCase);

    public override void Dispose()
    {
        foreach (var reader in nestedReaders) reader.Dispose();
    }
}
