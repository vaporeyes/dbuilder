// ABOUTME: Discovers and loads map WAD entries embedded inside PK3/ZIP archives.
// ABOUTME: Keeps PK3 map handling thin by delegating actual Doom, Hexen and UDMF loading to WadMaps.

using System.IO.Compression;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed record Pk3MapEntry(string ArchivePath, MapEntry Map);

public static class Pk3Maps
{
    public static List<Pk3MapEntry> Find(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        return Find(zip);
    }

    public static List<Pk3MapEntry> Find(ZipArchive zip)
    {
        var result = new List<Pk3MapEntry>();
        foreach (var entry in zip.Entries)
        {
            if (!IsWadEntry(entry)) continue;
            foreach (var map in FindMaps(entry))
                result.Add(new Pk3MapEntry(entry.FullName, map));
        }

        return result;
    }

    public static MapSet? Load(string path, Pk3MapEntry entry)
    {
        using var zip = ZipFile.OpenRead(path);
        return Load(zip, entry);
    }

    public static MapSet? Load(ZipArchive zip, Pk3MapEntry entry)
    {
        var wadEntry = zip.GetEntry(entry.ArchivePath);
        if (wadEntry == null) return null;
        using var wad = OpenWad(wadEntry);
        return WadMaps.Load(wad, entry.Map);
    }

    private static IEnumerable<MapEntry> FindMaps(ZipArchiveEntry entry)
    {
        using var wad = OpenWad(entry);
        return WadMaps.Find(wad);
    }

    private static WAD OpenWad(ZipArchiveEntry entry)
    {
        var ms = new MemoryStream();
        using (var stream = entry.Open()) stream.CopyTo(ms);
        ms.Position = 0;
        return new WAD(ms, openreadonly: true, virtualFilename: entry.FullName);
    }

    private static bool IsWadEntry(ZipArchiveEntry entry)
        => !entry.FullName.EndsWith("/", StringComparison.Ordinal)
            && Path.GetExtension(entry.FullName).Equals(".wad", StringComparison.OrdinalIgnoreCase);
}
