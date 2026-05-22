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
        int idx = wad.FindLumpIndex(marker);
        if (idx >= 0 && idx + 1 < wad.Lumps.Count && wad.Lumps[idx + 1].Name == "TEXTMAP")
            return wad.Lumps[idx + 1];
        return null;
    }

    /// <summary>
    /// Writes <paramref name="map"/> into <paramref name="wad"/> under <paramref name="marker"/> in the given
    /// format, replacing the marker and its existing map sub-lumps in place (other lumps are untouched). If the
    /// marker is absent the block is appended. An existing Hexen BEHAVIOR lump is preserved across the rewrite.
    /// </summary>
    public static void SaveMap(WAD wad, string marker, MapSet map, MapFormat format)
    {
        int insertPos;
        byte[]? behavior = null;

        int idx = wad.FindLumpIndex(marker);
        if (idx >= 0)
        {
            insertPos = idx;
            // Capture the existing compiled ACS so a Hexen save keeps its scripts.
            for (int j = idx + 1; j < wad.Lumps.Count && IsMapSubLump(wad.Lumps[j].Name); j++)
                if (wad.Lumps[j].Name == "BEHAVIOR") { behavior = wad.Lumps[j].Stream.ReadAllBytes(); break; }

            wad.RemoveAt(idx, false);                                   // the marker
            while (idx < wad.Lumps.Count && IsMapSubLump(wad.Lumps[idx].Name))
                wad.RemoveAt(idx, false);                               // its sub-lumps
        }
        else
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

    private static bool IsMapSubLump(string name) => name switch
    {
        "THINGS" or "LINEDEFS" or "SIDEDEFS" or "VERTEXES" or "SECTORS"
            or "SEGS" or "SSECTORS" or "NODES" or "REJECT" or "BLOCKMAP"
            or "BEHAVIOR" or "SCRIPTS" or "TEXTMAP" or "ENDMAP" or "DIALOGUE" or "ZNODES" or "GL_VERT" => true,
        _ => false,
    };
}
