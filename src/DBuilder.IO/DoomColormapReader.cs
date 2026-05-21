// ABOUTME: COLORMAP lump parser and visualization helpers.
// ABOUTME: Parses raw bytes -> DoomColormap; provides two visualization renderers used by diagnostic UIs: a single-level swatch grid and an all-levels horizontal strip.

using System.IO;

namespace DBuilder.IO;

public static class DoomColormapReader
{
    /// <summary>Parses raw COLORMAP bytes. Length must be a multiple of 256; subtable count is inferred.</summary>
    public static DoomColormap FromBytes(byte[] data)
    {
        if (data.Length == 0 || data.Length % DoomColormap.LevelSize != 0)
            throw new IOException($"COLORMAP data must be a non-zero multiple of {DoomColormap.LevelSize} bytes (got {data.Length})");

        int levels = data.Length / DoomColormap.LevelSize;
        return new DoomColormap(data, levels);
    }

    /// <summary>Loads COLORMAP from a WAD. Returns null when the lump is absent.</summary>
    public static DoomColormap? FromWad(WAD wad)
    {
        var lump = wad.FindLump("COLORMAP");
        if (lump == null) return null;
        return FromBytes(lump.Stream.ReadAllBytes());
    }

    /// <summary>
    /// UDB-compatible visualization: renders one subtable as a 128x128 RGBA8 image - a 16x16 grid of 8x8 swatches, one per palette index.
    /// Useful for inspecting how a specific light level remaps the palette.
    /// </summary>
    public static byte[] RenderLevelSwatch(DoomColormap colormap, int level, DoomPalette palette)
    {
        const int width = 128, height = 128;
        var rgba = new byte[width * height * 4];

        for (int by = 0; by < 16; by++)
        {
            for (int bx = 0; bx < 16; bx++)
            {
                int idx = by * 16 + bx;
                byte remapped = colormap.Lookup(level, (byte)idx);
                uint color = palette.Colors[remapped];
                byte r = (byte)((color >> 16) & 0xFF);
                byte g = (byte)((color >>  8) & 0xFF);
                byte b = (byte)( color        & 0xFF);

                for (int py = 0; py < 8; py++)
                {
                    for (int px = 0; px < 8; px++)
                    {
                        int x = bx * 8 + px;
                        int y = by * 8 + py;
                        int dst = (y * width + x) * 4;
                        rgba[dst + 0] = r;
                        rgba[dst + 1] = g;
                        rgba[dst + 2] = b;
                        rgba[dst + 3] = 0xFF;
                    }
                }
            }
        }

        return rgba;
    }

    /// <summary>
    /// Renders all light levels stacked as a (256 x LevelCount) RGBA8 strip - each row is one subtable's full 256-color band.
    /// Reading top-to-bottom shows the gradient from level 0 (brightest) through level 31 (darkest) plus levels 32 (invulnerability) and 33 (reserved).
    /// Each pixel is colored by palette[colormap[row][col]].
    /// </summary>
    public static byte[] RenderAllLevelsStrip(DoomColormap colormap, DoomPalette palette, out int width, out int height)
    {
        width = DoomColormap.LevelSize;
        height = colormap.LevelCount;
        var rgba = new byte[width * height * 4];

        for (int level = 0; level < colormap.LevelCount; level++)
        {
            for (int idx = 0; idx < DoomColormap.LevelSize; idx++)
            {
                byte remapped = colormap.Lookup(level, (byte)idx);
                uint color = palette.Colors[remapped];
                int dst = (level * width + idx) * 4;
                rgba[dst + 0] = (byte)((color >> 16) & 0xFF);
                rgba[dst + 1] = (byte)((color >>  8) & 0xFF);
                rgba[dst + 2] = (byte)( color        & 0xFF);
                rgba[dst + 3] = 0xFF;
            }
        }

        return rgba;
    }
}
