// ABOUTME: Boom ANIMATED binary lump parser plus the hardcoded vanilla Doom animation table.
// ABOUTME: Each entry is a flat/texture animation that ranges from First to Last in the directory order.

using System;
using System.Collections.Generic;
using System.Text;

namespace DBuilder.IO;

public static class BoomAnimated
{
    /// <summary>An animation range: cycles from First to Last (inclusive, in directory order) at Tics per frame.</summary>
    public readonly record struct Entry(bool IsTexture, string First, string Last, int Tics);

    /// <summary>Parses a Boom ANIMATED lump (23-byte records, terminated by a leading 0xFF byte).</summary>
    public static List<Entry> Parse(byte[] data)
    {
        var list = new List<Entry>();
        int p = 0;
        while (p + 23 <= data.Length)
        {
            sbyte type = unchecked((sbyte)data[p]);
            if (type < 0) break; // 0xFF terminator
            bool isTexture = (type & 1) != 0; // bit 0: 1 = texture, 0 = flat
            string last = ReadName(data, p + 1);
            string first = ReadName(data, p + 10);
            int speed = BitConverter.ToInt32(data, p + 19);
            list.Add(new Entry(isTexture, first, last, speed <= 0 ? 8 : speed));
            p += 23;
        }
        return list;
    }

    private static string ReadName(byte[] d, int off)
    {
        int end = off;
        int limit = Math.Min(off + 9, d.Length);
        while (end < limit && d[end] != 0) end++;
        return Encoding.ASCII.GetString(d, off, end - off).ToUpperInvariant();
    }

    /// <summary>
    /// The hardcoded vanilla Doom/Doom2 animations (engine built-ins, since IWADs ship no ANIMATED/ANIMDEFS lump).
    /// Ranges whose names are absent in the loaded resources are simply ignored.
    /// </summary>
    public static readonly Entry[] DoomDefaults =
    {
        new(false, "NUKAGE1", "NUKAGE3", 8),
        new(false, "FWATER1", "FWATER4", 8),
        new(false, "SWATER1", "SWATER4", 8),
        new(false, "LAVA1", "LAVA4", 8),
        new(false, "BLOOD1", "BLOOD3", 8),
        new(false, "RROCK05", "RROCK08", 8),
        new(false, "SLIME01", "SLIME04", 8),
        new(false, "SLIME05", "SLIME08", 8),
        new(false, "SLIME09", "SLIME12", 8),
        new(true, "BLODGR1", "BLODGR4", 8),
        new(true, "SLADRIP1", "SLADRIP3", 8),
        new(true, "BLODRIP1", "BLODRIP4", 8),
        new(true, "FIREWALA", "FIREWALL", 8),
        new(true, "GSTFONT1", "GSTFONT3", 8),
        new(true, "FIRELAV3", "FIRELAVA", 8),
        new(true, "FIREMAG1", "FIREMAG3", 8),
        new(true, "FIREBLU1", "FIREBLU2", 8),
        new(true, "ROCKRED1", "ROCKRED3", 8),
        new(true, "BFALL1", "BFALL4", 8),
        new(true, "SFALL1", "SFALL4", 8),
        new(true, "WFALL1", "WFALL4", 8),
        new(true, "DBRAIN1", "DBRAIN4", 8),
    };
}
