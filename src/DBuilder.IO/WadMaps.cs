// ABOUTME: Locates map markers in a WAD and loads them with the correct loader (Doom/Hexen/UDMF).
// ABOUTME: A marker is identified by the lump that immediately follows it (THINGS or TEXTMAP), not by its own length.

using System.Collections.Generic;
using System.Text;
using DBuilder.Map;

namespace DBuilder.IO;

public enum MapFormat { Doom, Hexen, Udmf }

/// <summary>A discovered map: its marker lump name and the binary/text format to load it with.</summary>
public sealed record MapEntry(string Name, MapFormat Format);

public static class WadMaps
{
    /// <summary>
    /// Returns every map in the WAD in directory order. A map marker is any lump immediately followed by
    /// THINGS (binary) or TEXTMAP (UDMF). The marker's own length is NOT tested: Doom markers are zero-length
    /// but Hexen IWAD markers (MAP01, ...) carry 12 bytes, so length is an unreliable signal.
    /// </summary>
    public static List<MapEntry> Find(WAD wad)
    {
        var result = new List<MapEntry>();
        for (int i = 0; i < wad.Lumps.Count - 1; i++)
        {
            string name = wad.Lumps[i].Name;
            string next = wad.Lumps[i + 1].Name;
            if (next == "TEXTMAP")
                result.Add(new MapEntry(name, MapFormat.Udmf));
            else if (next == "THINGS")
                result.Add(new MapEntry(name, HexenMapLoader.IsHexenFormat(wad, name) ? MapFormat.Hexen : MapFormat.Doom));
        }
        return result;
    }

    /// <summary>
    /// Returns maps whose lump blocks match the supplied game configuration's maplumpnames rules.
    /// Forbidden configured lumps reject a candidate block, matching UDB's open-map detection.
    /// </summary>
    public static List<MapEntry> Find(WAD wad, GameConfiguration config)
    {
        if (config.MapLumpNames.Count == 0) return Find(wad);

        var result = new List<MapEntry>();
        int required = CountRequiredMapLumps(config.MapLumpNames);
        var seen = new HashSet<string>();

        for (int i = 0; i < wad.Lumps.Count - 1; i++)
        {
            if (config.MapLumpNames.ContainsKey(wad.Lumps[i].Name)) continue;

            int found = 0;
            bool rejected = false;
            bool hasTextmap = false;
            int offset = 1;

            while (i + offset < wad.Lumps.Count && config.MapLumpNames.TryGetValue(wad.Lumps[i + offset].Name, out var info))
            {
                if (info.Forbidden)
                {
                    rejected = true;
                    break;
                }

                if (info.Required) found++;
                if (wad.Lumps[i + offset].Name == "TEXTMAP") hasTextmap = true;
                offset++;
            }

            if (!rejected && found >= required && seen.Add(wad.Lumps[i].Name))
                result.Add(new MapEntry(wad.Lumps[i].Name, InferFormat(wad, i, hasTextmap)));
        }

        return result;
    }

    private static int CountRequiredMapLumps(IReadOnlyDictionary<string, MapLumpInfo> mapLumps)
    {
        int count = 0;
        foreach (var lump in mapLumps.Values)
            if (!lump.IsMarker && lump.Required) count++;
        return count;
    }

    private static MapFormat InferFormat(WAD wad, int markerIndex, bool hasTextmap)
    {
        if (hasTextmap || (markerIndex + 1 < wad.Lumps.Count && wad.Lumps[markerIndex + 1].Name == "TEXTMAP"))
            return MapFormat.Udmf;

        string marker = wad.Lumps[markerIndex].Name;
        return HexenMapLoader.IsHexenFormat(wad, marker) ? MapFormat.Hexen : MapFormat.Doom;
    }

    /// <summary>Loads a discovered map with the loader matching its format, or null on failure.</summary>
    public static MapSet? Load(WAD wad, MapEntry entry)
    {
        switch (entry.Format)
        {
            case MapFormat.Udmf:
                var textmap = TextmapAfter(wad, entry.Name) ?? wad.FindLump("TEXTMAP");
                return textmap == null ? null
                    : UdmfMapLoader.Load(Encoding.ASCII.GetString(textmap.Stream.ReadAllBytes()), out _);
            case MapFormat.Hexen:
                return HexenMapLoader.Load(wad, entry.Name);
            default:
                return DoomMapLoader.Load(wad, entry.Name);
        }
    }

    // The TEXTMAP lump immediately following the marker (so multi-map UDMF wads resolve the right one).
    private static Lump? TextmapAfter(WAD wad, string marker)
    {
        int idx = FindMapHeaderIndex(wad, marker);
        if (idx >= 0 && idx + 1 < wad.Lumps.Count && wad.Lumps[idx + 1].Name == "TEXTMAP")
            return wad.Lumps[idx + 1];
        return null;
    }

    /// <summary>
    /// Writes <paramref name="map"/> into <paramref name="wad"/> under <paramref name="marker"/> in the given
    /// format, replacing the marker and its existing map sub-lumps in place (other lumps are untouched). If the
    /// marker is absent the block is appended. An existing Hexen BEHAVIOR lump is preserved across the rewrite.
    /// </summary>
    public static void SaveMap(WAD wad, string marker, MapSet map, MapFormat format, GameConfiguration? config = null)
    {
        MapFormatConstraints.ThrowIfInvalid(map, format);

        byte[]? behavior = ReadFirstMapLump(wad, marker, "BEHAVIOR", config);
        int insertPos = RemoveMapBlocks(wad, marker, config);
        if (insertPos < 0)
        {
            insertPos = wad.Lumps.Count;
        }

        switch (format)
        {
            case MapFormat.Udmf: UdmfMapWriter.WriteMap(map, wad, marker, insertPos); break;
            case MapFormat.Hexen: HexenMapWriter.WriteMap(map, wad, marker, insertPos, behavior); break;
            default: DoomMapWriter.WriteMap(map, wad, marker, insertPos); break;
        }
        wad.WriteHeaders();
    }

    private static byte[]? ReadFirstMapLump(WAD wad, string marker, string lumpName, GameConfiguration? config)
    {
        int idx = FindMapHeaderIndex(wad, marker);
        if (idx < 0) return null;

        for (int j = idx + 1; j < wad.Lumps.Count && IsMapSubLump(wad.Lumps[j].Name, config); j++)
            if (wad.Lumps[j].Name == lumpName) return wad.Lumps[j].Stream.ReadAllBytes();
        return null;
    }

    private static int RemoveMapBlocks(WAD wad, string marker, GameConfiguration? config)
    {
        int firstRemoved = -1;
        int scan = 0;

        while (scan < wad.Lumps.Count)
        {
            int idx = FindMapHeaderIndex(wad, marker, scan);
            if (idx < 0) break;
            if (firstRemoved < 0) firstRemoved = idx;

            wad.RemoveAt(idx, false);
            while (idx < wad.Lumps.Count && IsMapSubLump(wad.Lumps[idx].Name, config))
                wad.RemoveAt(idx, false);

            scan = idx;
        }

        return firstRemoved;
    }

    private static int FindMapHeaderIndex(WAD wad, string marker)
        => FindMapHeaderIndex(wad, marker, 0);

    private static int FindMapHeaderIndex(WAD wad, string marker, int start)
    {
        for (int i = start; i < wad.Lumps.Count - 1; i++)
        {
            if (wad.Lumps[i].Name != marker) continue;
            string next = wad.Lumps[i + 1].Name;
            if (next is "THINGS" or "TEXTMAP") return i;
        }

        return -1;
    }

    /// <summary>Copies every lump from <paramref name="src"/> into <paramref name="dst"/> (append order preserved).</summary>
    public static void CopyAllLumps(WAD src, WAD dst)
    {
        foreach (var s in src.Lumps)
        {
            var bytes = s.Stream.ReadAllBytes();
            var d = dst.Insert(s.Name, dst.Lumps.Count, bytes.Length, false)!;
            if (bytes.Length > 0) d.Stream.Write(bytes, 0, bytes.Length);
        }
        dst.WriteHeaders();
    }

    /// <summary>
    /// Finds a configured map lump inside one map block. The scan stops at the first lump that is not
    /// known by the supplied map-lump table, matching UDB's save/copy boundary behavior.
    /// </summary>
    public static int FindSpecificMapLump(WAD wad, string lumpName, int mapHeaderIndex, string mapHeaderName, IReadOnlyDictionary<string, MapLumpInfo> mapLumps)
    {
        for (int i = 0; i < mapLumps.Count + 1; i++)
        {
            int index = mapHeaderIndex + i;
            if (index >= wad.Lumps.Count) break;

            string configuredName = NormalizeMapHeaderPlaceholder(wad.Lumps[index].Name, mapHeaderName);
            if (!mapLumps.ContainsKey(configuredName)) break;

            if (wad.Lumps[index].Name == lumpName) return index;
        }

        return -1;
    }

    /// <summary>Removes one configured map lump and returns its former index, or -1 when absent.</summary>
    public static int RemoveSpecificMapLump(WAD wad, string lumpName, int mapHeaderIndex, string mapHeaderName, IReadOnlyDictionary<string, MapLumpInfo> mapLumps)
    {
        int index = FindSpecificMapLump(wad, lumpName, mapHeaderIndex, mapHeaderName, mapLumps);
        if (index > -1) wad.RemoveAt(index);
        return index;
    }

    private static string NormalizeMapHeaderPlaceholder(string lumpName, string mapHeaderName)
        => lumpName.Contains(mapHeaderName) ? lumpName.Replace(mapHeaderName, "~MAP") : lumpName;

    // A lump is a map sub-lump if the game config lists it (port-specific lumps) or it is in the curated
    // built-in set. The config only extends this set, so save-back never fails to clean a standard lump.
    /// <summary>Reads a named sub-lump (e.g. REJECT, BLOCKMAP) belonging to a map marker, or null if absent.</summary>
    public static byte[]? ReadMapLump(WAD wad, string marker, string lumpName)
    {
        int idx = FindMapHeaderIndex(wad, marker);
        if (idx < 0) return null;
        for (int j = idx + 1; j < wad.Lumps.Count && IsMapSubLump(wad.Lumps[j].Name); j++)
            if (wad.Lumps[j].Name == lumpName) return wad.Lumps[j].Stream.ReadAllBytes();
        return null;
    }

    private static bool IsMapSubLump(string name, GameConfiguration? config = null)
        => (config != null && config.IsMapLump(name)) || name switch
    {
        "THINGS" or "LINEDEFS" or "SIDEDEFS" or "VERTEXES" or "SECTORS"
            or "SEGS" or "SSECTORS" or "NODES" or "REJECT" or "BLOCKMAP"
            or "BEHAVIOR" or "SCRIPTS" or "TEXTMAP" or "ENDMAP" or "DIALOGUE" or "ZNODES" or "GL_VERT" => true,
        _ => false,
    };
}
