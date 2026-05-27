// ABOUTME: Shared test helpers for building synthetic WAD, PNG and PK3 resource fixtures.
// ABOUTME: Produces copyright-free IWAD/PWAD/PK3 assets for resource and map pipeline tests.

using System;
using System.IO;
using System.IO.Compression;
using DBuilder.IO;

namespace DBuilder.Tests;

internal static class TestArtifacts
{
    private static readonly byte[] Sig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static byte[] GrayscalePlaypal()
    {
        var palette = new byte[768];
        for (int i = 0; i < 256; i++)
        {
            palette[i * 3] = (byte)i;
            palette[i * 3 + 1] = (byte)i;
            palette[i * 3 + 2] = (byte)i;
        }
        return palette;
    }

    public static byte[] SolidFlat(byte index)
    {
        var flat = new byte[DoomFlatReader.RawSize];
        for (int i = 0; i < flat.Length; i++) flat[i] = index;
        return flat;
    }

    public static byte[] SolidRgba(int w, int h, byte r, byte g, byte b, byte a)
    {
        var px = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++) { px[i * 4] = r; px[i * 4 + 1] = g; px[i * 4 + 2] = b; px[i * 4 + 3] = a; }
        return px;
    }

    public static byte[] Png(int w, int h, byte[] rgba)
    {
        var scan = new byte[h * (1 + w * 4)];
        for (int y = 0; y < h; y++)
        {
            scan[y * (1 + w * 4)] = 0; // filter None
            Array.Copy(rgba, y * w * 4, scan, y * (1 + w * 4) + 1, w * 4);
        }
        using var comp = new MemoryStream();
        using (var z = new ZLibStream(comp, CompressionMode.Compress, leaveOpen: true)) z.Write(scan, 0, scan.Length);

        var ihdr = new byte[13];
        WriteBE(ihdr, 0, w); WriteBE(ihdr, 4, h); ihdr[8] = 8; ihdr[9] = 6;
        using var ms = new MemoryStream();
        ms.Write(Sig, 0, 8);
        Chunk(ms, "IHDR", ihdr); Chunk(ms, "IDAT", comp.ToArray()); Chunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    public static string BuildPk3(params (string name, byte[] bytes)[] entries)
    {
        string path = Path.Combine(Path.GetTempPath(), "dbuilder_test_" + Guid.NewGuid().ToString("N") + ".pk3");
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        foreach (var (name, bytes) in entries)
        {
            using var s = zip.CreateEntry(name).Open();
            s.Write(bytes, 0, bytes.Length);
        }
        return path;
    }

    public static string BuildIwadFile(params (string name, byte[] bytes)[] lumps)
        => BuildWadFile(isIwad: true, lumps);

    public static string BuildPwadFile(params (string name, byte[] bytes)[] lumps)
        => BuildWadFile(isIwad: false, lumps);

    public static string BuildWadFile(bool isIwad, params (string name, byte[] bytes)[] lumps)
    {
        string path = Path.Combine(Path.GetTempPath(), "dbuilder_test_" + Guid.NewGuid().ToString("N") + ".wad");
        using (var wad = new WAD(path))
        {
            wad.IsIWAD = isIwad;
            foreach (var (name, bytes) in lumps)
            {
                var lump = wad.Insert(name, wad.Lumps.Count, bytes.Length)!;
                lump.Stream.Write(bytes, 0, bytes.Length);
            }
            wad.WriteHeaders();
        }
        return path;
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; WriteBE(len, 0, data.Length); s.Write(len, 0, 4);
        var t = System.Text.Encoding.ASCII.GetBytes(type); s.Write(t, 0, 4); s.Write(data, 0, data.Length);
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
}
