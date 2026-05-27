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
    /// <summary>The text of a named lump (e.g. TEXTURES, DECORATE) if this resource has one, else null.</summary>
    string? GetTextLump(string name);
    /// <summary>The text of every matching lump or root text file in this resource, oldest first.</summary>
    IEnumerable<string> GetTextLumps(string name, bool partialTitleMatch);
    /// <summary>The text of a named lump or exact PK3/directory path if this resource has one, else null.</summary>
    string? GetTextResource(string name);
    /// <summary>The raw bytes of a named lump (e.g. ANIMATED, PLAYPAL) if this resource has one, else null.</summary>
    byte[]? GetLumpBytes(string name);
    /// <summary>Names of the wall textures this resource provides (for the texture browser).</summary>
    IEnumerable<string> TextureNames();
    /// <summary>Names of the flats this resource provides (for the texture browser).</summary>
    IEnumerable<string> FlatNames();
    /// <summary>Names of voxel model lumps or files this resource provides.</summary>
    IEnumerable<string> VoxelNames();
    /// <summary>Raw voxel model bytes, or null when this resource does not provide the model.</summary>
    byte[]? GetVoxelBytes(string name);
    /// <summary>Raw model or skin bytes by MODELDEF path, or null when this resource does not provide the file.</summary>
    byte[]? GetModelResourceBytes(string path);
}

internal sealed class WadResourceReader : IResourceReader
{
    private static readonly Regex VoxelName = new(@"^\S{4}(([A-Za-z][0-9])?|[A-Za-z]?)$", RegexOptions.Compiled);
    private readonly WAD wad;
    private readonly bool owns;
    private Dictionary<string, DoomTextureDef>? texDefs;
    private DoomPatchNames? patchNames;

    public WadResourceReader(WAD wad, bool owns) { this.wad = wad; this.owns = owns; }

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

    protected FolderResourceReader(string displayName)
    {
        DisplayName = displayName;
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
        var b = Find("PLAYPAL", "");
        var palette = b != null ? DoomPalette.FromBytes(b) : null;
        if (palette != null) return palette;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            palette = nestedReaders[i].GetPalette();
            if (palette != null) return palette;
        }

        return null;
    }

    public virtual ImageData? GetFlat(string name, DoomPalette? palette)
    {
        var image = Decode(Find(name, "hires", "flats", ""), palette, preferFlat: true);
        if (image != null) return image;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            image = nestedReaders[i].GetFlat(name, palette);
            if (image != null) return image;
        }

        return null;
    }

    public virtual ImageData? GetWallTexture(string name, DoomPalette? palette)
    {
        var image = Decode(Find(name, "hires", "textures", "patches"), palette, preferFlat: false);
        if (image != null) return image;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            image = nestedReaders[i].GetWallTexture(name, palette);
            if (image != null) return image;
        }

        return null;
    }

    public virtual ImageData? GetSprite(string name, DoomPalette? palette)
    {
        var image = Decode(Find(name, "hires", "sprites", "graphics", "patches", ""), palette, preferFlat: false);
        if (image != null) return image;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            image = nestedReaders[i].GetSprite(name, palette);
            if (image != null) return image;
        }

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
        var bytes = Find(name, "");
        if (bytes != null) return bytes;

        for (int i = nestedReaders.Count - 1; i >= 0; i--)
        {
            bytes = nestedReaders[i].GetLumpBytes(name);
            if (bytes != null) return bytes;
        }

        return null;
    }

    public virtual IEnumerable<string> TextureNames()
    {
        foreach (var name in NamesInFolder("textures/")) yield return name;
        foreach (var reader in nestedReaders)
            foreach (var name in reader.TextureNames())
                yield return name;
    }

    public virtual IEnumerable<string> FlatNames()
    {
        foreach (var name in NamesInFolder("flats/")) yield return name;
        foreach (var reader in nestedReaders)
            foreach (var name in reader.FlatNames())
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
        string key = name.ToUpperInvariant();
        foreach (var f in folders)
            if (entries.TryGetValue(f.ToLowerInvariant() + "/" + key, out var read)) return read();
        return null;
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

    public Pk3ResourceReader(Stream zipStream, bool ownsStream, string displayName = "PK3 resource") : base(displayName)
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
                nestedReaders.Add(new Pk3ResourceReader(ms, ownsStream: false, displayName: e.FullName));
            }
        }
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
