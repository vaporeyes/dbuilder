// ABOUTME: Locates map markers in a WAD and loads them with the correct loader (Doom/Hexen/UDMF).
// ABOUTME: A marker is identified by the lump that immediately follows it (THINGS or TEXTMAP), not by its own length.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DBuilder.Map;

namespace DBuilder.IO;

public enum MapFormat { Doom, Hexen, Udmf }

/// <summary>A discovered map: its marker lump name and the binary/text format to load it with.</summary>
public sealed record MapEntry(string Name, MapFormat Format);

public static class WadMaps
{
    private static readonly Regex EpisodeMapName = new("^E[1-9]M[1-9]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex NoEpisodeMapName = new("^MAP[0-9][0-9]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
            if (MapNameFormatMismatch(config.MapNameFormat, wad.Lumps[i].Name)) continue;
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

    private static bool MapNameFormatMismatch(string mapNameFormat, string lumpName)
    {
        return (mapNameFormat == "MAPxy" && EpisodeMapName.IsMatch(lumpName))
            || (mapNameFormat == "ExMy" && NoEpisodeMapName.IsMatch(lumpName));
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
        var preservedLumps = config != null ? CapturePreservedMapLumps(wad, marker, format, config) : Array.Empty<LumpSnapshot>();
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
        if (config != null)
        {
            RestorePreservedMapLumps(wad, marker, config, preservedLumps);
            OrderConfiguredMapLumps(wad, marker, config);
            CreateRequiredMapLumps(wad, marker, config);
            OrderConfiguredMapLumps(wad, marker, config);
        }
        wad.WriteHeaders();
    }

    private sealed record LumpSnapshot(string Name, byte[] Bytes, int OriginalIndex);

    private static LumpSnapshot[] CapturePreservedMapLumps(WAD wad, string marker, MapFormat format, GameConfiguration config)
    {
        int headerIndex = FindMapHeaderIndex(wad, marker);
        if (headerIndex < 0) return Array.Empty<LumpSnapshot>();

        var snapshots = new List<LumpSnapshot>();
        int endIndex = headerIndex;
        while (endIndex + 1 < wad.Lumps.Count && IsMapSubLump(wad.Lumps[endIndex + 1].Name, config))
            endIndex++;

        for (int i = headerIndex; i <= endIndex; i++)
        {
            var lump = wad.Lumps[i];
            string configuredName = NormalizeMapHeaderPlaceholder(lump.Name, marker);
            if (!config.MapLumpNames.TryGetValue(configuredName, out var info)) continue;
            if (!ShouldPreserveExistingMapLump(info, lump.Name, marker, format)) continue;

            snapshots.Add(new LumpSnapshot(lump.Name, lump.Stream.ReadAllBytes(), i - headerIndex));
        }

        return snapshots.ToArray();
    }

    private static bool ShouldPreserveExistingMapLump(MapLumpInfo info, string lumpName, string marker, MapFormat format)
    {
        if (info.IsMarker) return false;
        if (IsWriterGeneratedLump(lumpName, marker, format)) return false;
        return info.BlindCopy || info.Script != null || info.ScriptBuild;
    }

    private static bool IsWriterGeneratedLump(string lumpName, string marker, MapFormat format)
        => lumpName == marker || format switch
        {
            MapFormat.Udmf => lumpName is "TEXTMAP" or "ENDMAP",
            MapFormat.Hexen => lumpName is "THINGS" or "LINEDEFS" or "SIDEDEFS" or "VERTEXES" or "SECTORS" or "BEHAVIOR",
            _ => lumpName is "THINGS" or "LINEDEFS" or "SIDEDEFS" or "VERTEXES" or "SECTORS",
        };

    private static void RestorePreservedMapLumps(WAD wad, string marker, GameConfiguration config, IReadOnlyList<LumpSnapshot> snapshots)
    {
        if (snapshots.Count == 0) return;

        int headerIndex = FindMapHeaderIndex(wad, marker);
        if (headerIndex < 0) return;

        foreach (var snapshot in snapshots)
        {
            if (FindSpecificMapLump(wad, snapshot.Name, headerIndex, marker, config.MapLumpNames) != -1) continue;

            int insertIndex = Math.Min(wad.Lumps.Count, headerIndex + snapshot.OriginalIndex);
            var lump = wad.Insert(snapshot.Name, insertIndex, snapshot.Bytes.Length, false)!;
            if (snapshot.Bytes.Length > 0) lump.Stream.Write(snapshot.Bytes, 0, snapshot.Bytes.Length);
        }
    }

    private static void OrderConfiguredMapLumps(WAD wad, string marker, GameConfiguration config)
    {
        if (config.MapLumpNames.Count == 0) return;

        int headerIndex = FindMapHeaderIndex(wad, marker);
        if (headerIndex < 0) return;

        int endIndex = headerIndex;
        while (endIndex + 1 < wad.Lumps.Count && IsMapSubLump(wad.Lumps[endIndex + 1].Name, config))
            endIndex++;

        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int configuredIndex = 0;
        foreach (var key in config.MapLumpNames.Keys)
            order[key] = configuredIndex++;

        var snapshots = new List<LumpSnapshot>();
        for (int i = headerIndex; i <= endIndex; i++)
            snapshots.Add(new LumpSnapshot(wad.Lumps[i].Name, wad.Lumps[i].Stream.ReadAllBytes(), i - headerIndex));

        var ordered = snapshots
            .OrderBy(s => ConfiguredLumpOrder(s.Name, marker, order))
            .ThenBy(s => s.OriginalIndex)
            .ToArray();

        for (int i = endIndex; i >= headerIndex; i--)
            wad.RemoveAt(i, false);

        for (int i = 0; i < ordered.Length; i++)
        {
            var snapshot = ordered[i];
            var lump = wad.Insert(snapshot.Name, headerIndex + i, snapshot.Bytes.Length, false)!;
            if (snapshot.Bytes.Length > 0) lump.Stream.Write(snapshot.Bytes, 0, snapshot.Bytes.Length);
        }
    }

    private static int ConfiguredLumpOrder(string lumpName, string marker, Dictionary<string, int> order)
    {
        string configuredName = NormalizeMapHeaderPlaceholder(lumpName, marker);
        if (order.TryGetValue(configuredName, out int index)) return index;
        if (lumpName == marker) return -1;
        return int.MaxValue;
    }

    private static void CreateRequiredMapLumps(WAD wad, string marker, GameConfiguration config)
    {
        int headerIndex = FindMapHeaderIndex(wad, marker);
        if (headerIndex < 0) return;

        int insertIndex = headerIndex;
        foreach (var group in config.MapLumpNames)
        {
            if (!group.Value.Required) continue;

            string lumpName = group.Key.Contains("~MAP") ? group.Key.Replace("~MAP", marker) : group.Key;
            int existingIndex = FindSpecificMapLump(wad, lumpName, headerIndex, marker, config.MapLumpNames);
            if (existingIndex == -1)
            {
                insertIndex++;
                if (insertIndex > wad.Lumps.Count) insertIndex = wad.Lumps.Count;
                wad.Insert(lumpName, insertIndex, 0, false);
            }
            else
            {
                insertIndex = existingIndex;
            }
        }
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
    /// Renames a validated map marker without touching non-map lumps that happen to share the same name.
    /// The rename is refused when the target marker already exists as a map block.
    /// </summary>
    public static bool RenameMap(WAD wad, string oldMarker, string newMarker)
    {
        int oldHeaderIndex = FindMapHeaderIndex(wad, oldMarker);
        if (oldHeaderIndex < 0) return false;
        if (oldMarker == newMarker) return true;
        if (FindMapHeaderIndex(wad, newMarker) >= 0) return false;

        wad.Lumps[oldHeaderIndex].Rename(newMarker);
        return true;
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

    /// <summary>
    /// Copies configured map lumps from one map block to another using UDB's required, blind-copy,
    /// nodebuilder, and script category switches.
    /// </summary>
    public static bool CopyMapLumpsByType(
        WAD source,
        string sourceMarker,
        WAD target,
        string targetMarker,
        GameConfiguration config,
        bool copyRequired,
        bool copyBlindCopy,
        bool copyNodeBuild,
        bool copyScript,
        bool replaceTargetLumps = false)
    {
        int sourceHeaderIndex = FindMapHeaderIndex(source, sourceMarker);
        if (sourceHeaderIndex < 0) return false;

        int targetHeaderIndex = FindMapHeaderIndex(target, targetMarker);
        bool replaceExistingTarget = replaceTargetLumps && targetHeaderIndex > -1;
        if (targetHeaderIndex < 0) targetHeaderIndex = target.Lumps.Count;

        int targetIndex = targetHeaderIndex;
        foreach (var group in config.MapLumpNames)
        {
            if (!ShouldCopyMapLump(group.Value, copyRequired, copyBlindCopy, copyNodeBuild, copyScript)) continue;

            string sourceLumpName = group.Key.Contains("~MAP") ? group.Key.Replace("~MAP", sourceMarker) : group.Key;
            string targetLumpName = group.Key.Contains("~MAP") ? group.Key.Replace("~MAP", targetMarker) : group.Key;
            int sourceIndex = FindSpecificMapLump(source, sourceLumpName, sourceHeaderIndex, sourceMarker, config.MapLumpNames);
            if (sourceIndex < 0) continue;

            if (replaceExistingTarget)
            {
                int removedIndex = RemoveSpecificMapLump(target, targetLumpName, targetHeaderIndex, targetMarker, config.MapLumpNames);
                if (removedIndex > -1) targetIndex = removedIndex;
                else targetIndex++;
            }
            else
            {
                targetIndex++;
            }

            if (targetIndex > target.Lumps.Count) targetIndex = target.Lumps.Count;
            Lump lump = source.Lumps[sourceIndex];
            Lump copied = target.Insert(targetLumpName, targetIndex, lump.Length, false)!;
            lump.CopyTo(copied);

            if (!replaceExistingTarget) targetIndex++;
        }

        target.WriteHeaders();
        target.Compress();
        return true;
    }

    private static bool ShouldCopyMapLump(MapLumpInfo info, bool required, bool blindCopy, bool nodeBuild, bool script)
        => (info.Required && required)
            || (info.BlindCopy && blindCopy)
            || (info.NodeBuild && nodeBuild)
            || ((info.Script != null || info.ScriptBuild) && script);

    /// <summary>
    /// Verifies that required nodebuilder output lumps are present for one map block.
    /// Optional and allow-empty nodebuilder lumps do not make the block incomplete, matching UDB.
    /// </summary>
    public static bool RequiredNodeBuildLumpsPresent(WAD wad, string marker, GameConfiguration config)
    {
        int headerIndex = FindMapHeaderIndex(wad, marker);
        if (headerIndex < 0) return false;

        foreach (var group in config.MapLumpNames)
        {
            var info = group.Value;
            if (!info.NodeBuild || info.AllowEmpty || !info.Required) continue;

            string lumpName = group.Key.Contains("~MAP") ? group.Key.Replace("~MAP", marker) : group.Key;
            int endIndex = Math.Min(wad.Lumps.Count - 1, headerIndex + config.MapLumpNames.Count + 2);
            if (wad.FindLump(lumpName, headerIndex, endIndex) == null)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Removes lumps not required by the configured map format from a WAD that only contains one map block.
    /// Nodebuilder lumps are removed because they are recreated during save, matching UDB temporary-map cleanup.
    /// </summary>
    public static void RemoveUnneededMapLumps(WAD wad, string marker, GameConfiguration config, bool glNodesOnly)
    {
        var requiredLumps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in config.MapLumpNames)
        {
            if (group.Value.NodeBuild && (!glNodesOnly || group.Key.ToUpperInvariant().StartsWith("GL_", StringComparison.Ordinal)))
                continue;

            string lumpName = group.Key.Contains("~MAP") ? group.Key.Replace("~MAP", marker) : group.Key;
            requiredLumps.Add(lumpName);
        }

        var toRemove = new List<Lump>();
        foreach (var lump in wad.Lumps)
            if (!requiredLumps.Contains(lump.Name)) toRemove.Add(lump);

        foreach (var lump in toRemove) wad.Remove(lump);
    }

    private static string NormalizeMapHeaderPlaceholder(string lumpName, string mapHeaderName)
        => lumpName.Contains(mapHeaderName) ? lumpName.Replace(mapHeaderName, "~MAP") : lumpName;

    // A lump is a map sub-lump if the game config lists it (port-specific lumps) or it is in the curated
    // built-in set. The config only extends this set, so save-back never fails to clean a standard lump.
    /// <summary>Reads a named sub-lump (e.g. REJECT, BLOCKMAP) belonging to a map marker, or null if absent.</summary>
    public static byte[]? ReadMapLump(WAD wad, string marker, string lumpName, GameConfiguration? config = null)
    {
        int idx = FindMapHeaderIndex(wad, marker);
        if (idx < 0) return null;
        for (int j = idx + 1; j < wad.Lumps.Count && IsMapSubLump(wad.Lumps[j].Name, config); j++)
            if (wad.Lumps[j].Name == lumpName) return wad.Lumps[j].Stream.ReadAllBytes();
        return null;
    }

    public static byte[]? ReadMapLumpOrGlobalLump(WAD wad, string marker, string lumpName, GameConfiguration? config = null)
        => ReadMapLump(wad, marker, lumpName, config) ?? wad.FindLump(lumpName)?.Stream.ReadAllBytes();

    private static bool IsMapSubLump(string name, GameConfiguration? config = null)
        => (config != null && config.IsMapLump(name)) || name switch
    {
        "THINGS" or "LINEDEFS" or "SIDEDEFS" or "VERTEXES" or "SECTORS"
            or "SEGS" or "SSECTORS" or "NODES" or "REJECT" or "BLOCKMAP"
            or "BEHAVIOR" or "SCRIPTS" or "TEXTMAP" or "ENDMAP" or "DIALOGUE" or "ZNODES" or "GL_VERT" => true,
        _ => false,
    };
}
