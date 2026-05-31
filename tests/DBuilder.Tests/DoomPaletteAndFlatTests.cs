// ABOUTME: Palette + flat reader verification tests.
// ABOUTME: Synthetic 768-byte palette + 4096-byte flat; verifies index->RGBA mapping order and dimensions.

using DBuilder.IO;

namespace DBuilder.Tests;

public class DoomPaletteAndFlatTests
{
    // Builds a palette where entry N has (R=N, G=N+1, B=N+2) so order errors are obvious.
    private static byte[] BuildSyntheticPalette()
    {
        var bytes = new byte[768];
        for (int i = 0; i < 256; i++)
        {
            bytes[i * 3 + 0] = (byte)i;
            bytes[i * 3 + 1] = (byte)(i + 1);
            bytes[i * 3 + 2] = (byte)(i + 2);
        }
        return bytes;
    }

    [Fact]
    public void PaletteReadsAllEntries()
    {
        var palette = DoomPalette.FromBytes(BuildSyntheticPalette());
        Assert.Equal(256, palette.Colors.Length);

        // Entry 0 = (R=0, G=1, B=2) with alpha 0xFF
        uint expected0 = 0xFF000102u;
        Assert.Equal(expected0, palette.Colors[0]);

        // Entry 100 = (R=100, G=101, B=102)
        uint expected100 = 0xFF000000u | (100u << 16) | (101u << 8) | 102u;
        Assert.Equal(expected100, palette.Colors[100]);
    }

    [Fact]
    public void PaletteThrowsOnShortInput()
    {
        Assert.Throws<System.IO.IOException>(() => DoomPalette.FromBytes(new byte[700]));
    }

    [Fact]
    public void IndexedToRgba8MapsByteToTuple()
    {
        var palette = DoomPalette.FromBytes(BuildSyntheticPalette());
        var rgba = palette.IndexedToRgba8(new byte[] { 0, 100, 255 });
        Assert.Equal(12, rgba.Length); // 3 pixels * 4 bytes
        Assert.Equal(0, rgba[0]);   Assert.Equal(1, rgba[1]);   Assert.Equal(2, rgba[2]);   Assert.Equal(0xFF, rgba[3]);
        Assert.Equal(100, rgba[4]); Assert.Equal(101, rgba[5]); Assert.Equal(102, rgba[6]); Assert.Equal(0xFF, rgba[7]);
        Assert.Equal(255, rgba[8]); Assert.Equal(0, rgba[9]);   Assert.Equal(1, rgba[10]);  Assert.Equal(0xFF, rgba[11]);
    }

    [Fact]
    public void FindClosestColorUsesUdbSquaredRgbDistanceAndFirstTie()
    {
        var palette = DoomPalette.FromBytes(BuildSyntheticPalette());
        var tieBytes = new byte[768];
        for (int i = 0; i < 256; i++)
        {
            tieBytes[i * 3 + 0] = 200;
            tieBytes[i * 3 + 1] = 200;
            tieBytes[i * 3 + 2] = 200;
        }
        tieBytes[0] = 0;
        tieBytes[1] = 0;
        tieBytes[2] = 0;
        tieBytes[3] = 10;
        tieBytes[4] = 0;
        tieBytes[5] = 0;
        var tiePalette = DoomPalette.FromBytes(tieBytes);

        Assert.Equal(10, palette.FindClosestColor(0xFF0A0B0Cu));
        Assert.Equal(0, tiePalette.FindClosestColor(0xFF050000u));
        Assert.Equal(0, palette.FindClosestColor(0x00000102u));
    }

    [Fact]
    public void QuantizeArgbToIndicesMapsPixelsToNearestPaletteEntries()
    {
        var palette = DoomPalette.FromBytes(BuildSyntheticPalette());
        uint[] pixels =
        [
            0xFF000102u,
            0xFF646566u,
            0xFFFF0001u,
        ];

        byte[] indices = palette.QuantizeArgbToIndices(pixels);

        Assert.Equal(new byte[] { 0, 100, 255 }, indices);
    }

    [Fact]
    public void FlatDecodesTo16384RgbaBytes()
    {
        var palette = DoomPalette.FromBytes(BuildSyntheticPalette());
        // A flat full of index 50 should map to (R=50, G=51, B=52, A=FF) for every pixel.
        var flatBytes = new byte[DoomFlatReader.RawSize];
        System.Array.Fill(flatBytes, (byte)50);

        var rgba = DoomFlatReader.DecodeRgba8(flatBytes, palette);
        Assert.Equal(DoomFlatReader.RawSize * 4, rgba.Length);
        Assert.Equal(50, rgba[0]); Assert.Equal(51, rgba[1]); Assert.Equal(52, rgba[2]); Assert.Equal(0xFF, rgba[3]);
        int last = rgba.Length - 4;
        Assert.Equal(50, rgba[last]); Assert.Equal(51, rgba[last + 1]); Assert.Equal(52, rgba[last + 2]); Assert.Equal(0xFF, rgba[last + 3]);
    }

    [Fact]
    public void FlatLooksLikeFlatRecognizesCanonicalSize()
    {
        Assert.True(DoomFlatReader.LooksLikeFlat(4096));
        Assert.False(DoomFlatReader.LooksLikeFlat(4095));
        Assert.False(DoomFlatReader.LooksLikeFlat(4097));
    }

    [Fact]
    public void FlatThrowsOnShortInput()
    {
        var palette = DoomPalette.FromBytes(BuildSyntheticPalette());
        Assert.Throws<System.IO.IOException>(() => DoomFlatReader.DecodeRgba8(new byte[100], palette));
    }
}
