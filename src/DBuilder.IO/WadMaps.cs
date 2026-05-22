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
}
