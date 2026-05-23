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
    {
        if (from == to || config == null) return;
        bool fromBinary = IsBinary(from), toBinary = IsBinary(to);

        if (fromBinary && !toBinary) BinaryToUdmf(map, config);
        else if (!fromBinary && toBinary) UdmfToBinary(map, config);
        // binary -> binary: int Flags is shared; low flag bits are common across Doom/Hexen, so leave as-is.
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
}
