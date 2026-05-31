// ABOUTME: Doom PLAYPAL palette reader - 256 RGB triplets (one full palette table).
// ABOUTME: The PLAYPAL lump contains 14 stacked tables (one per pain/bonus state); we only need the first 768 bytes.

using System.IO;

namespace DBuilder.IO;

public sealed class DoomPalette
{
    public const int ColorCount = 256;

    /// <summary>256 ARGB entries (alpha always 0xFF).  Index by the Doom 0-255 palette byte.</summary>
    public uint[] Colors { get; }

    private DoomPalette(uint[] colors) { Colors = colors; }

    public static DoomPalette CreateDefaultGray()
    {
        var colors = new uint[ColorCount];
        for (int i = 0; i < colors.Length; i++) colors[i] = 0xFF7F7F7Fu;
        return new DoomPalette(colors);
    }

    /// <summary>Builds a palette from a 768-byte (or larger - extra tables are ignored) RGB buffer.</summary>
    public static DoomPalette FromBytes(byte[] data)
    {
        if (data.Length < 768) throw new IOException($"PLAYPAL data too short: got {data.Length} bytes, need at least 768.");

        var colors = new uint[ColorCount];
        for (int i = 0; i < ColorCount; i++)
        {
            byte r = data[i * 3 + 0];
            byte g = data[i * 3 + 1];
            byte b = data[i * 3 + 2];
            colors[i] = 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
        }
        return new DoomPalette(colors);
    }

    /// <summary>Loads a palette from the PLAYPAL lump in a WAD. Returns null when the lump is absent.</summary>
    public static DoomPalette? FromWad(WAD wad)
    {
        var lump = wad.FindLump("PLAYPAL");
        if (lump == null) return null;
        return FromBytes(lump.Stream.ReadAllBytes());
    }

    /// <summary>Returns RGBA8 bytes (4 per pixel) by mapping each indexed byte through the palette.</summary>
    public byte[] IndexedToRgba8(byte[] indexed)
    {
        var rgba = new byte[indexed.Length * 4];
        for (int i = 0; i < indexed.Length; i++)
        {
            uint c = Colors[indexed[i]];
            rgba[i * 4 + 0] = (byte)((c >> 16) & 0xFF); // R
            rgba[i * 4 + 1] = (byte)((c >>  8) & 0xFF); // G
            rgba[i * 4 + 2] = (byte)( c        & 0xFF); // B
            rgba[i * 4 + 3] = (byte)((c >> 24) & 0xFF); // A
        }
        return rgba;
    }

    public int FindClosestColor(uint argb)
    {
        byte matchR = (byte)((argb >> 16) & 0xFF);
        byte matchG = (byte)((argb >> 8) & 0xFF);
        byte matchB = (byte)(argb & 0xFF);
        int minDistance = int.MaxValue;
        int minIndex = 0;

        for (int i = 0; i < Colors.Length; i++)
        {
            uint color = Colors[i];
            int dr = matchR - (byte)((color >> 16) & 0xFF);
            int dg = matchG - (byte)((color >> 8) & 0xFF);
            int db = matchB - (byte)(color & 0xFF);
            int distance = dr * dr + dg * dg + db * db;

            if (distance < minDistance)
            {
                minIndex = i;
                minDistance = distance;
            }
        }

        return minIndex;
    }

    public byte[] QuantizeArgbToIndices(ReadOnlySpan<uint> argbPixels)
    {
        var indices = new byte[argbPixels.Length];
        for (int i = 0; i < argbPixels.Length; i++) indices[i] = (byte)FindClosestColor(argbPixels[i]);
        return indices;
    }
}
