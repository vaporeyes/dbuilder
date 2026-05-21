// ABOUTME: COLORMAP lump parsing + lookup + visualization render tests.
// ABOUTME: Synthetic colormaps with predictable per-level remaps so position->color assertions are simple.

using System;
using DBuilder.IO;

namespace DBuilder.Tests;

public class DoomColormapTests
{
    /// <summary>Palette where entry N maps to RGB (N, N, N) - lets us match pixels to expected palette indices by inspecting any channel.</summary>
    private static DoomPalette GrayPalette()
    {
        var bytes = new byte[768];
        for (int i = 0; i < 256; i++) { bytes[i * 3] = (byte)i; bytes[i * 3 + 1] = (byte)i; bytes[i * 3 + 2] = (byte)i; }
        return DoomPalette.FromBytes(bytes);
    }

    /// <summary>Builds a 34-level colormap where level L remaps every index to (idx + L) mod 256.  Easy to verify by lookup.</summary>
    private static byte[] BuildSyntheticColormap(int levels = DoomColormap.StandardLevelCount)
    {
        var data = new byte[levels * DoomColormap.LevelSize];
        for (int level = 0; level < levels; level++)
        {
            for (int idx = 0; idx < DoomColormap.LevelSize; idx++)
            {
                data[level * DoomColormap.LevelSize + idx] = (byte)((idx + level) & 0xFF);
            }
        }
        return data;
    }

    [Fact]
    public void ParsesStandard34LevelLump()
    {
        var data = BuildSyntheticColormap();
        Assert.Equal(8704, data.Length); // 34 * 256
        var cm = DoomColormapReader.FromBytes(data);
        Assert.Equal(34, cm.LevelCount);
        Assert.Same(data, cm.Data);
    }

    [Fact]
    public void ParsesNonStandardLevelCounts()
    {
        // Some PWADs ship custom COLORMAP lumps with extra subtables (BOOM extended colormaps).
        var cm = DoomColormapReader.FromBytes(new byte[DoomColormap.LevelSize * 64]);
        Assert.Equal(64, cm.LevelCount);
    }

    [Fact]
    public void RejectsNonMultipleOf256()
    {
        Assert.Throws<System.IO.IOException>(() => DoomColormapReader.FromBytes(new byte[100]));
        Assert.Throws<System.IO.IOException>(() => DoomColormapReader.FromBytes(new byte[0]));
    }

    [Fact]
    public void LookupAtLevelZeroIsIdentityForOurSynthetic()
    {
        var cm = DoomColormapReader.FromBytes(BuildSyntheticColormap());
        // Level 0: (idx + 0) % 256 = idx
        for (int i = 0; i < 256; i++)
            Assert.Equal((byte)i, cm.Lookup(0, (byte)i));
    }

    [Fact]
    public void LookupShiftsByLevel()
    {
        var cm = DoomColormapReader.FromBytes(BuildSyntheticColormap());
        // Level 5: (idx + 5) % 256
        Assert.Equal((byte)5,   cm.Lookup(5, 0));
        Assert.Equal((byte)10,  cm.Lookup(5, 5));
        Assert.Equal((byte)4,   cm.Lookup(5, 255)); // (255 + 5) mod 256 = 4
    }

    [Fact]
    public void LookupThrowsForOutOfRangeLevel()
    {
        var cm = DoomColormapReader.FromBytes(BuildSyntheticColormap());
        Assert.Throws<ArgumentOutOfRangeException>(() => cm.Lookup(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => cm.Lookup(34, 0));
    }

    [Fact]
    public void LevelTableReturnsCopy()
    {
        var cm = DoomColormapReader.FromBytes(BuildSyntheticColormap());
        var table = cm.LevelTable(3);
        Assert.Equal(256, table.Length);
        Assert.Equal((byte)3, table[0]); // level 3, idx 0 -> 3

        // Mutating the copy must not affect the colormap's internal data
        table[0] = 99;
        Assert.Equal((byte)3, cm.Lookup(3, 0));
    }

    [Fact]
    public void RenderLevelSwatchIs128x128Rgba()
    {
        var cm = DoomColormapReader.FromBytes(BuildSyntheticColormap());
        var palette = GrayPalette();
        byte[] rgba = DoomColormapReader.RenderLevelSwatch(cm, level: 0, palette);
        Assert.Equal(128 * 128 * 4, rgba.Length);

        // Level 0, idx 0 maps to itself = 0 = palette[0] = (0,0,0,255).
        // Idx 0 lives in the top-left 8x8 swatch; check the center of that swatch.
        int dst = (4 * 128 + 4) * 4;
        Assert.Equal(0, rgba[dst + 0]);
        Assert.Equal(0xFF, rgba[dst + 3]);

        // Idx 255 lives in the bottom-right 8x8 swatch; check the center.
        // For level 0, idx 255 -> palette[255] = (255,255,255,255).
        int last = (124 * 128 + 124) * 4;
        Assert.Equal(255, rgba[last + 0]);
        Assert.Equal(0xFF, rgba[last + 3]);
    }

    [Fact]
    public void RenderAllLevelsStripHas256xLevelCountPixels()
    {
        var cm = DoomColormapReader.FromBytes(BuildSyntheticColormap());
        var palette = GrayPalette();
        byte[] rgba = DoomColormapReader.RenderAllLevelsStrip(cm, palette, out int w, out int h);
        Assert.Equal(256, w);
        Assert.Equal(34, h);
        Assert.Equal(256 * 34 * 4, rgba.Length);

        // Pixel at (col=0, row=0): level 0 maps idx 0 to 0 -> palette[0] = (0,0,0)
        int dst = (0 * 256 + 0) * 4;
        Assert.Equal(0, rgba[dst + 0]);
        // Pixel at (col=10, row=5): level 5 maps idx 10 to 15 -> palette[15] = (15,15,15)
        dst = (5 * 256 + 10) * 4;
        Assert.Equal(15, rgba[dst + 0]);
        // Pixel at (col=255, row=33): level 33 maps idx 255 to (255+33) mod 256 = 32
        dst = (33 * 256 + 255) * 4;
        Assert.Equal(32, rgba[dst + 0]);
    }
}
