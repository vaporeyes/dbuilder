// ABOUTME: Converts a map between binary (Doom/Hexen) and UDMF formats by translating flags and action encodings.
// ABOUTME: Binary writers read int Flags; the UDMF writer reads named UdmfFlags and normalized line ids.

using DBuilder.Map;

namespace DBuilder.IO;

/// <summary>
/// Translates a loaded map so a different writer can emit it. Flag encodings are translated via the active game
/// config's flag-translation tables, and format-specific action/tag encodings are normalized when needed.
/// The fill is additive where possible so the in-memory map stays valid for the source format too.
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
            if (from == MapFormat.Hexen) HexenLinedefActionsToUdmf(map);
        }
        else if (!fromBinary && toBinary)
        {
            if (targetConfig != null) UdmfToBinary(map, targetConfig);
            if (to == MapFormat.Hexen) UdmfLinedefActionsToHexen(map);
            ClearUdmfOnlyElementData(map);
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

    private static void HexenLinedefActionsToUdmf(MapSet map)
    {
        foreach (var l in map.Linedefs)
        {
            if (l.Action == 121)
            {
                l.Tag = l.Args[0] + l.Args[4] * 256;
                AddLineIdFlags(l, l.Args[1]);
                l.Action = 0;
                Array.Clear(l.Args, 0, l.Args.Length);
                continue;
            }

            switch (l.Action)
            {
                case 208:
                    l.Tag = l.Args[0];
                    AddLineIdFlags(l, l.Args[3]);
                    l.Args[3] = 0;
                    break;
                case 1:
                    ConvertArgToTag(l, 3, clearArg: true);
                    break;
                case 5:
                    ConvertArgToTag(l, 4, clearArg: true);
                    break;
                case 181:
                    ConvertArgToTag(l, 2, clearArg: true);
                    break;
                case 215:
                    ConvertArgToTag(l, 0, clearArg: true);
                    break;
                case 222:
                    ConvertArgToTag(l, 0, clearArg: false);
                    break;
                case 160:
                    ConvertSector3DFloorToUdmf(l);
                    break;
            }
        }
    }

    private static void ConvertArgToTag(Linedef linedef, int argIndex, bool clearArg)
    {
        linedef.Tag = linedef.Args[argIndex];
        if (clearArg) linedef.Args[argIndex] = 0;
    }

    private static void ConvertSector3DFloorToUdmf(Linedef linedef)
    {
        if ((linedef.Args[1] & 8) == 8)
        {
            linedef.Tag = linedef.Args[4];
            linedef.Args[1] &= ~8;
        }
        else
        {
            linedef.Args[0] += linedef.Args[4] * 256;
        }

        linedef.Args[4] = 0;
    }

    private static void UdmfLinedefActionsToHexen(MapSet map)
    {
        foreach (var l in map.Linedefs)
        {
            switch (l.Action)
            {
                case 208:
                    SetArgFromTag(l, 0);
                    l.Args[3] = LineIdFlagsToArg(l.UdmfFlags);
                    break;
                case 1:
                    SetArgFromTag(l, 3);
                    break;
                case 5:
                    SetArgFromTag(l, 4);
                    break;
                case 181:
                    SetArgFromTag(l, 2);
                    break;
                case 215:
                case 222:
                    SetArgFromTag(l, 0);
                    break;
                case 160:
                    ConvertSector3DFloorToHexen(l);
                    break;
                default:
                    ConvertUdmfLineIdToHexenIdentification(l);
                    break;
            }

            l.Tag = 0;
        }
    }

    private static void SetArgFromTag(Linedef linedef, int argIndex)
    {
        if (linedef.Tag is >= 0 and <= 255)
            linedef.Args[argIndex] = linedef.Tag;
    }

    private static void ConvertSector3DFloorToHexen(Linedef linedef)
    {
        if (linedef.Args[0] > 255)
        {
            linedef.Args[4] = linedef.Args[0] / 256;
            linedef.Args[0] %= 256;
        }
        else if (linedef.Tag is > 0 and <= 255)
        {
            linedef.Args[4] = linedef.Tag;
            linedef.Args[1] |= 8;
        }
    }

    private static void ConvertUdmfLineIdToHexenIdentification(Linedef linedef)
    {
        if (linedef.Action != 0 || linedef.Tag <= 255) return;

        linedef.Action = 121;
        linedef.Args[0] = linedef.Tag % 256;
        linedef.Args[4] = linedef.Tag / 256;
        linedef.Args[1] = LineIdFlagsToArg(linedef.UdmfFlags);
    }

    private static void AddLineIdFlags(Linedef linedef, int bits)
    {
        if ((bits & 1) == 1) linedef.UdmfFlags.Add("zoneboundary");
        if ((bits & 2) == 2) linedef.UdmfFlags.Add("jumpover");
        if ((bits & 4) == 4) linedef.UdmfFlags.Add("blockfloaters");
        if ((bits & 8) == 8) linedef.UdmfFlags.Add("clipmidtex");
        if ((bits & 16) == 16) linedef.UdmfFlags.Add("wrapmidtex");
        if ((bits & 32) == 32) linedef.UdmfFlags.Add("midtex3d");
        if ((bits & 64) == 64) linedef.UdmfFlags.Add("checkswitchrange");
        if ((bits & 128) == 128) linedef.UdmfFlags.Add("firstsideonly");
    }

    private static int LineIdFlagsToArg(HashSet<string> flags)
    {
        int bits = 0;
        if (flags.Contains("zoneboundary")) bits |= 1;
        if (flags.Contains("jumpover")) bits |= 2;
        if (flags.Contains("blockfloaters")) bits |= 4;
        if (flags.Contains("clipmidtex")) bits |= 8;
        if (flags.Contains("wrapmidtex")) bits |= 16;
        if (flags.Contains("midtex3d")) bits |= 32;
        if (flags.Contains("checkswitchrange")) bits |= 64;
        if (flags.Contains("firstsideonly")) bits |= 128;
        return bits;
    }

    private static void ClearUdmfOnlyElementData(MapSet map)
    {
        foreach (var v in map.Vertices)
        {
            v.Fields.Clear();
            v.ZCeiling = double.NaN;
            v.ZFloor = double.NaN;
        }

        foreach (var l in map.Linedefs)
        {
            l.UdmfFlags.Clear();
            l.Fields.Clear();
        }

        foreach (var sd in map.Sidedefs)
        {
            ApplySidedefTextureOffsets(sd);
            sd.UdmfFlags.Clear();
            sd.Fields.Clear();
        }

        foreach (var s in map.Sectors)
        {
            s.UdmfFlags.Clear();
            s.Fields.Clear();
            s.FloorSlope = default;
            s.FloorSlopeOffset = double.NaN;
            s.CeilSlope = default;
            s.CeilSlopeOffset = double.NaN;
        }

        foreach (var t in map.Things)
        {
            t.UdmfFlags.Clear();
            t.Fields.Clear();
            t.Pitch = 0;
            t.Roll = 0;
            t.ScaleX = 1.0;
            t.ScaleY = 1.0;
        }
    }

    private static void ApplySidedefTextureOffsets(Sidedef side)
    {
        if (side.MidTexture != "-" && side.MiddleRequired())
        {
            side.OffsetX += FieldAsInt(side, "offsetx_mid");
            side.OffsetY += FieldAsInt(side, "offsety_mid");
        }
        else if (side.HighTexture != "-" && side.HighRequired())
        {
            side.OffsetX += FieldAsInt(side, "offsetx_top");
            side.OffsetY += FieldAsInt(side, "offsety_top");
        }
        else if (side.LowTexture != "-" && side.LowRequired())
        {
            side.OffsetX += FieldAsInt(side, "offsetx_bottom");
            side.OffsetY += FieldAsInt(side, "offsety_bottom");
        }
    }

    private static int FieldAsInt(IFielded element, string key)
    {
        if (!element.Fields.TryGetValue(key, out var value)) return 0;
        return value switch
        {
            int i => i,
            long l => (int)l,
            float f => (int)f,
            double d => (int)d,
            _ => 0,
        };
    }

    private static void ClearDoomUnsupportedArgs(MapSet map)
    {
        foreach (var l in map.Linedefs)
            Array.Clear(l.Args, 0, l.Args.Length);
        foreach (var t in map.Things)
            Array.Clear(t.Args, 0, t.Args.Length);
    }
}
