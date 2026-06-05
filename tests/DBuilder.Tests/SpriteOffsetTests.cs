// ABOUTME: Tests sprite render-offset propagation: PNG grAb chunk and TEXTURES Offset surface as ImageData offsets.
// ABOUTME: Builds a PK3 with a grAb'd PNG sprite and a TEXTURES sprite def, checking the resolved offsets.

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class SpriteOffsetTests
{
    private static readonly byte[] Sig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // A 2x2 RGBA PNG with a grAb chunk carrying the given offsets.
    private static byte[] PngWithGrab(int ox, int oy)
    {
        var rgba = TestArtifacts.SolidRgba(2, 2, 5, 6, 7, 255);
        var scan = new byte[2 * (1 + 2 * 4)];
        for (int y = 0; y < 2; y++) Array.Copy(rgba, y * 8, scan, y * 9 + 1, 8);
        using var comp = new MemoryStream();
        using (var z = new ZLibStream(comp, CompressionMode.Compress, leaveOpen: true)) z.Write(scan, 0, scan.Length);

        var ihdr = new byte[13];
        WriteBE(ihdr, 0, 2); WriteBE(ihdr, 4, 2); ihdr[8] = 8; ihdr[9] = 6;
        var grab = new byte[8]; WriteBE(grab, 0, ox); WriteBE(grab, 4, oy);

        using var ms = new MemoryStream();
        ms.Write(Sig, 0, 8);
        Chunk(ms, "IHDR", ihdr);
        Chunk(ms, "grAb", grab);
        Chunk(ms, "IDAT", comp.ToArray());
        Chunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; WriteBE(len, 0, data.Length); s.Write(len, 0, 4);
        var t = Encoding.ASCII.GetBytes(type); s.Write(t, 0, 4); s.Write(data, 0, data.Length);
        var crcIn = new byte[4 + data.Length];
        Array.Copy(t, crcIn, 4); Array.Copy(data, 0, crcIn, 4, data.Length);
        var crc = new byte[4]; WriteBE(crc, 0, (int)Crc(crcIn)); s.Write(crc, 0, 4);
    }

    private static void WriteBE(byte[] d, int p, int v) { d[p] = (byte)(v >> 24); d[p + 1] = (byte)(v >> 16); d[p + 2] = (byte)(v >> 8); d[p + 3] = (byte)v; }

    private static uint Crc(byte[] d)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte b in d) { c ^= b; for (int i = 0; i < 8; i++) c = (c & 1) != 0 ? (c >> 1) ^ 0xEDB88320 : c >> 1; }
        return c ^ 0xFFFFFFFF;
    }

    [Fact]
    public void PngDecoderReadsGrabOffsets()
    {
        var img = PngDecoder.Decode(PngWithGrab(1, 5));
        Assert.NotNull(img);
        Assert.Equal(1, img!.OffsetX);
        Assert.Equal(5, img.OffsetY);
    }

    [Fact]
    public void ResourceManagerSurfacesSpriteOffsets()
    {
        string pk3 = TestArtifacts.BuildPk3(("sprites/TROOA0.png", PngWithGrab(13, 60)));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);
            var s = rm.GetSprite("TROOA0");
            Assert.NotNull(s);
            Assert.Equal(13, s!.OffsetX);
            Assert.Equal(60, s.OffsetY);
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void ResourceManagerSurfacesTexturesSpriteOffsets()
    {
        string textures =
            "Sprite TROOA0, 2, 2\n" +
            "{\n" +
            "    Offset 7, 11\n" +
            "    Patch PAT, 0, 0\n" +
            "}\n";
        string pk3 = TestArtifacts.BuildPk3(
            ("TEXTURES.txt", Encoding.ASCII.GetBytes(textures)),
            ("patches/PAT.png", TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 5, 6, 7, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var sprite = rm.GetSprite("TROOA0");

            Assert.NotNull(sprite);
            Assert.Equal(7, sprite!.OffsetX);
            Assert.Equal(11, sprite.OffsetY);
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void NoGrabMeansZeroOffsets()
    {
        var img = PngDecoder.Decode(TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 1, 1, 1, 255)));
        Assert.Equal(0, img!.OffsetX);
        Assert.Equal(0, img.OffsetY);
    }
}
