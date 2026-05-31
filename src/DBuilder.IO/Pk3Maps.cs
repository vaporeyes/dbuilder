// ABOUTME: Discovers and loads map WAD entries embedded inside PK3/ZIP archives.
// ABOUTME: Keeps PK3 map handling thin by delegating actual Doom, Hexen and UDMF loading to WadMaps.

using System.IO.Compression;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed record Pk3MapEntry(string ArchivePath, MapEntry Map);

public static class Pk3Maps
{
    private const char NestedArchiveSeparator = '!';

    public static List<Pk3MapEntry> Find(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        return Find(zip);
    }

    public static List<Pk3MapEntry> Find(ZipArchive zip)
    {
        var result = new List<Pk3MapEntry>();
        Find(zip, archivePrefix: "", result);
        return result;
    }

    public static MapSet? Load(string path, Pk3MapEntry entry)
    {
        using var zip = ZipFile.OpenRead(path);
        return Load(zip, entry);
    }

    public static MapSet? Load(ZipArchive zip, Pk3MapEntry entry)
    {
        byte[]? bytes = ReadArchiveBytes(zip, entry.ArchivePath);
        if (bytes == null) return null;
        using var wad = OpenWad(bytes, entry.ArchivePath);
        return WadMaps.Load(wad, entry.Map);
    }

    public static byte[]? ReadMapLump(string path, Pk3MapEntry entry, string lumpName, GameConfiguration? config = null)
    {
        using var zip = ZipFile.OpenRead(path);
        return ReadMapLump(zip, entry, lumpName, config);
    }

    public static byte[]? ReadMapLump(ZipArchive zip, Pk3MapEntry entry, string lumpName, GameConfiguration? config = null)
    {
        byte[]? bytes = ReadArchiveBytes(zip, entry.ArchivePath);
        if (bytes == null) return null;
        using var wad = OpenWad(bytes, entry.ArchivePath);
        return WadMaps.ReadMapLump(wad, entry.Map.Name, lumpName, config);
    }

    private static void Find(ZipArchive zip, string archivePrefix, List<Pk3MapEntry> result)
    {
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal)) continue;

            string archivePath = archivePrefix.Length == 0
                ? entry.FullName
                : archivePrefix + NestedArchiveSeparator + entry.FullName;

            if (IsWadPath(entry.FullName))
            {
                foreach (var map in FindMaps(entry))
                    result.Add(new Pk3MapEntry(archivePath, map));
            }
            else if (ArchivePath.IsPk3FamilyPath(entry.FullName))
            {
                using var nested = OpenNestedZip(entry);
                Find(nested, archivePath, result);
            }
        }
    }

    private static IEnumerable<MapEntry> FindMaps(ZipArchiveEntry entry)
    {
        using var wad = OpenWad(entry);
        return WadMaps.Find(wad);
    }

    private static WAD OpenWad(ZipArchiveEntry entry)
    {
        return OpenWad(ReadEntryBytes(entry), entry.FullName);
    }

    private static WAD OpenWad(byte[] bytes, string virtualFilename)
        => new(new MemoryStream(bytes), openreadonly: true, virtualFilename: virtualFilename);

    private static ZipArchive OpenNestedZip(ZipArchiveEntry entry)
        => new(new MemoryStream(ReadEntryBytes(entry)), ZipArchiveMode.Read);

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static byte[]? ReadArchiveBytes(ZipArchive zip, string archivePath)
    {
        int separator = archivePath.IndexOf(NestedArchiveSeparator);
        if (separator < 0)
        {
            var entry = zip.GetEntry(archivePath);
            return entry == null ? null : ReadEntryBytes(entry);
        }

        string outerPath = archivePath.Substring(0, separator);
        string innerPath = archivePath.Substring(separator + 1);
        var outerEntry = zip.GetEntry(outerPath);
        if (outerEntry == null || !ArchivePath.IsPk3FamilyPath(outerEntry.FullName)) return null;

        using var nested = OpenNestedZip(outerEntry);
        return ReadArchiveBytes(nested, innerPath);
    }

    private static bool IsWadPath(string path)
        => Path.GetExtension(path).Equals(".wad", StringComparison.OrdinalIgnoreCase);

}
