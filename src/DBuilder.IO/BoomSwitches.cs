// ABOUTME: Boom SWITCHES binary lump parser - maps a switch's off (SW1) texture to its on (SW2) texture.
// ABOUTME: 20-byte records (off name[9], on name[9], int16 game), terminated by a zero-game record.

using System;
using System.Collections.Generic;
using System.Text;

namespace DBuilder.IO;

public static class BoomSwitches
{
    /// <summary>Parses a Boom SWITCHES lump into (off, on) texture pairs.</summary>
    public static List<SwitchDef> Parse(byte[] data)
    {
        var list = new List<SwitchDef>();
        int p = 0;
        while (p + 20 <= data.Length)
        {
            short game = BitConverter.ToInt16(data, p + 18);
            if (game == 0) break; // terminator record
            string off = ReadName(data, p, 9);
            string on = ReadName(data, p + 9, 9);
            if (off.Length > 0 && on.Length > 0) list.Add(new SwitchDef(off, on));
            p += 20;
        }
        return list;
    }

    private static string ReadName(byte[] d, int off, int max)
    {
        int end = off, limit = Math.Min(off + max, d.Length);
        while (end < limit && d[end] != 0) end++;
        return Encoding.ASCII.GetString(d, off, end - off).ToUpperInvariant();
    }
}
