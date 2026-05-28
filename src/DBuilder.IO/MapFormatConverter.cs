// ABOUTME: Converts a map between binary (Doom/Hexen) and UDMF formats by translating the flag representation.
// ABOUTME: Binary writers read int Flags; the UDMF writer reads named UdmfFlags - this fills the target side from the source.

using DBuilder.Map;

namespace DBuilder.IO;

/// <summary>
/// Translates a loaded map's flag representation so a different writer can emit it. Action/args/tags are already
/// shared across formats; only the flag encoding differs (binary bitfield vs named UDMF flags), so that is what
/// this converts, using the active game config's flag-translation tables. The fill is additive (it populates the
/// target representation without discarding the source), so the in-memory map stays valid for the source format too.
/// </summary>
public static class MapFormatConverter
{
    private static bool IsBinary(MapFormat f) => f == MapFormat.Doom || f == MapFormat.Hexen;

    /// <summary>
    /// Prepares <paramref name="map"/> for saving as <paramref name="to"/> when it was loaded as <paramref name="from"/>.
    /// Binary&lt;-&gt;UDMF conversions translate flags via <paramref name="config"/>; binary&lt;-&gt;binary is a pass-through
    /// (flags share the same int field). A null config skips flag translation.
    /// </summary>
    public static void Convert(MapSet map, MapFormat from, MapFormat to, GameConfiguration? config)
        => Convert(map, from, to, config, config);

    /// <summary>
    /// Prepares <paramref name="map"/> for saving as <paramref name="to"/> when source and target formats use
    /// different game configurations. Doom-to-Hexen conversion routes thing flags through UDMF so Doom's inverted
    /// single-player bit becomes Hexen's explicit single-player bit.
    /// </summary>
    public static void Convert(MapSet map, MapFormat from, MapFormat to, GameConfiguration? sourceConfig, GameConfiguration? targetConfig)
    {
        if (from == to) return;
        bool fromBinary = IsBinary(from), toBinary = IsBinary(to);

        if (fromBinary && !toBinary)
        {
            if (sourceConfig != null) BinaryToUdmf(map, sourceConfig);
        }
        else if (!fromBinary && toBinary)
        {
            if (targetConfig != null) UdmfToBinary(map, targetConfig);
        }
        else if (from == MapFormat.Doom && to == MapFormat.Hexen && sourceConfig != null && targetConfig != null)
        {
            DoomThingsToHexen(map, sourceConfig, targetConfig);
        }
        // Other binary -> binary conversions leave shared int flags as-is.

        if (to == MapFormat.Doom)
            ClearDoomUnsupportedArgs(map);
    }

    private static void BinaryToUdmf(MapSet map, GameConfiguration config)
    {
        foreach (var l in map.Linedefs)
            foreach (var name in config.LinedefFlagsToUdmf(l.Flags))
                l.UdmfFlags.Add(name);
        foreach (var t in map.Things)
            foreach (var name in config.ThingFlagsToUdmf(t.Flags))
                t.UdmfFlags.Add(name);
    }

    private static void UdmfToBinary(MapSet map, GameConfiguration config)
    {
        foreach (var l in map.Linedefs)
            l.Flags = config.LinedefFlagsFromUdmf(l.UdmfFlags);
        foreach (var t in map.Things)
            t.Flags = config.ThingFlagsFromUdmf(t.UdmfFlags);
    }

    private static void DoomThingsToHexen(MapSet map, GameConfiguration sourceConfig, GameConfiguration targetConfig)
    {
        foreach (var t in map.Things)
        {
            foreach (var name in sourceConfig.ThingFlagsToUdmf(t.Flags))
                t.UdmfFlags.Add(name);
            t.Flags = targetConfig.ThingFlagsFromUdmf(t.UdmfFlags);
        }
    }

    private static void ClearDoomUnsupportedArgs(MapSet map)
    {
        foreach (var l in map.Linedefs)
            Array.Clear(l.Args, 0, l.Args.Length);
        foreach (var t in map.Things)
            Array.Clear(t.Args, 0, t.Args.Length);
    }
}
