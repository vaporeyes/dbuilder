// ABOUTME: Tests for PngDecoder - decodes PNGs built in-test (real zlib + CRC) and checks pixels/dimensions.
// ABOUTME: Covers RGBA and grayscale round-trips, the Up filter path, and graceful failure on garbage.

using System.IO;
using System.IO.Compression;
using DBuilder.IO;

namespace DBuilder.Tests;

public class PngDecoderTests
{
    private static readonly byte[] Sig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // Builds a valid PNG from pre-filtered scanlines (each row already prefixed with its filter byte).
    private static byte[] BuildPng(int w, int h, byte bitDepth, byte colorType, byte[] filteredScanlines)
    {
        var ihdr = new byte[13];
        WriteBE32(ihdr, 0, w); WriteBE32(ihdr, 4, h);
        ihdr[8] = bitDepth; ihdr[9] = colorType; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;

        using var comp = new MemoryStream();
        using (var z = new ZLibStream(comp, CompressionMode.Compress, leaveOpen: true))
            z.Write(filteredScanlines, 0, filteredScanlines.Length);
        byte[] idat = comp.ToArray();

        using var ms = new MemoryStream();
        ms.Write(Sig, 0, 8);
        WriteChunk(ms, "IHDR", ihdr);
        WriteChunk(ms, "IDAT", idat);
        WriteChunk(ms, "IEND", System.Array.Empty<byte>());
        return ms.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; WriteBE32(len, 0, data.Length); s.Write(len, 0, 4);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes, 0, 4);
        s.Write(data, 0, data.Length);
        var crcInput = new byte[4 + data.Length];
        System.Array.Copy(typeBytes, 0, crcInput, 0, 4);
        System.Array.Copy(data, 0, crcInput, 4, data.Length);
        var crc = new byte[4]; WriteBE32(crc, 0, (int)Crc32(crcInput)); s.Write(crc, 0, 4);
    }

    private static void WriteBE32(byte[] d, int p, int v)
    { d[p] = (byte)(v >> 24); d[p + 1] = (byte)(v >> 16); d[p + 2] = (byte)(v >> 8); d[p + 3] = (byte)v; }

    private static uint Crc32(byte[] d)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte b in d)
        {
            c ^= b;
            for (int i = 0; i < 8; i++) c = (c & 1) != 0 ? (c >> 1) ^ 0xEDB88320 : c >> 1;
        }
        return c ^ 0xFFFFFFFF;
    }

    [Fact]
    public void IsPngDetectsSignature()
    {
        Assert.True(PngDecoder.IsPng(Sig));
        Assert.False(PngDecoder.IsPng(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
    }

    [Fact]
    public void DecodesRgba2x2()
    {
        // Two rows of two RGBA pixels, each row filter 0 (None).
        byte[] rows =
        {
            0, /*row0*/ 255, 0, 0, 255,   0, 255, 0, 128,
            0, /*row1*/ 0, 0, 255, 255,   255, 255, 255, 0,
        };
        var img = PngDecoder.Decode(BuildPng(2, 2, 8, 6, rows));
        Assert.NotNull(img);
        Assert.Equal(2, img!.Width);
        Assert.Equal(2, img.Height);
        // pixel (0,0) red opaque
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, img.Rgba[0..4]);
        // pixel (1,0) green half-alpha
        Assert.Equal(new byte[] { 0, 255, 0, 128 }, img.Rgba[4..8]);
        // pixel (1,1) white fully transparent
        Assert.Equal(new byte[] { 255, 255, 255, 0 }, img.Rgba[12..16]);
    }

    [Fact]
    public void DecodesGrayscale8()
    {
        byte[] rows = { 0, 0, 128, 255 }; // one row, three gray pixels
        var img = PngDecoder.Decode(BuildPng(3, 1, 8, 0, rows));
        Assert.NotNull(img);
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, img!.Rgba[0..4]);
        Assert.Equal(new byte[] { 128, 128, 128, 255 }, img.Rgba[4..8]);
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, img.Rgba[8..12]);
    }

    [Fact]
    public void ReconstructsUpFilter()
    {
        // Row0 raw (filter 0); row1 uses Up (filter 2) with zero residuals, so it must equal row0.
        byte[] rows =
        {
            0, 10, 20, 30, 255,
            2, 0, 0, 0, 0,
        };
        var img = PngDecoder.Decode(BuildPng(1, 2, 8, 6, rows));
        Assert.NotNull(img);
        Assert.Equal(img!.Rgba[0..4], img.Rgba[4..8]); // row1 == row0
        Assert.Equal(new byte[] { 10, 20, 30, 255 }, img.Rgba[0..4]);
    }

    [Fact]
    public void GarbageReturnsNull()
    {
        Assert.Null(PngDecoder.Decode(new byte[] { 1, 2, 3, 4 }));
    }
}
