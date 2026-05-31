// ABOUTME: Tests PK3 resource loading via ResourceManager: PNG entries under flats/textures/sprites resolve by name.
// ABOUTME: Builds a real temp .pk3 with generated PNGs and checks lookups, folder routing, and overrides.

using System;
using System.IO;
using System.IO.Compression;
using DBuilder.IO;

namespace DBuilder.Tests;

public class Pk3ResourceTests
{
    // --- minimal PNG builder (RGBA, filter 0) ---
    private static readonly byte[] Sig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static byte[] Png(int w, int h, byte[] rgba)
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

    private static byte[] SolidRgba(int w, int h, byte r, byte g, byte b, byte a)
    {
        var px = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++) { px[i * 4] = r; px[i * 4 + 1] = g; px[i * 4 + 2] = b; px[i * 4 + 3] = a; }
        return px;
    }

    private static byte[] GrayscalePlaypal()
    {
        var p = new byte[768];
        for (int i = 0; i < 256; i++) { p[i * 3] = (byte)i; p[i * 3 + 1] = (byte)i; p[i * 3 + 2] = (byte)i; }
        return p;
    }

    private static byte[] SolidFlat(byte index)
    {
        var f = new byte[DoomFlatReader.RawSize];
        for (int i = 0; i < f.Length; i++) f[i] = index;
        return f;
    }

    private static byte[] BuildNestedResourceWad()
    {
        using var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            Insert(wad, "PLAYPAL", GrayscalePlaypal());
            Insert(wad, "F_START", Array.Empty<byte>());
            Insert(wad, "NESTFLAT", SolidFlat(42));
            Insert(wad, "F_END", Array.Empty<byte>());
            wad.WriteHeaders();
        }
        return ms.ToArray();
    }

    private static byte[] BuildNestedResourcePk3()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("flats/NESTPK3.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 33, 44, 55, 255))));
        try { return File.ReadAllBytes(pk3); }
        finally { File.Delete(pk3); }
    }

    private static void Insert(WAD wad, string name, byte[] bytes)
    {
        var lump = wad.Insert(name, wad.Lumps.Count, bytes.Length)!;
        lump.Stream.Write(bytes, 0, bytes.Length);
    }

    private static string BuildPk3()
    {
        string path = Path.Combine(Path.GetTempPath(), "dbuilder_test_" + Guid.NewGuid().ToString("N") + ".pk3");
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        void Add(string name, byte[] bytes)
        {
            var e = zip.CreateEntry(name);
            using var s = e.Open();
            s.Write(bytes, 0, bytes.Length);
        }
        Add("flats/CFLOOR.png", Png(2, 2, SolidRgba(2, 2, 10, 20, 30, 255)));
        Add("textures/CWALL.png", Png(2, 2, SolidRgba(2, 2, 40, 50, 60, 255)));
        Add("sprites/CSPRITE.png", Png(2, 2, SolidRgba(2, 2, 70, 80, 90, 128)));
        return path;
    }

    [Fact]
    public void ResolvesPngFlatTextureSpriteFromPk3()
    {
        string pk3 = BuildPk3();
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var flat = rm.GetFlat("CFLOOR");
            Assert.NotNull(flat);
            Assert.Equal(2, flat!.Width);
            Assert.Equal(new byte[] { 10, 20, 30, 255 }, flat.Rgba[0..4]);

            var wall = rm.GetWallTexture("CWALL");
            Assert.NotNull(wall);
            Assert.Equal(new byte[] { 40, 50, 60, 255 }, wall!.Rgba[0..4]);

            var sprite = rm.GetSprite("CSPRITE");
            Assert.NotNull(sprite);
            Assert.Equal(new byte[] { 70, 80, 90, 128 }, sprite!.Rgba[0..4]);

            Assert.Null(rm.GetFlat("DOESNOTEXIST"));
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void FolderRoutingKeepsNamesDistinct()
    {
        // A flat and a texture sharing a name resolve independently from their own folders.
        string path = Path.Combine(Path.GetTempPath(), "dbuilder_test_" + Guid.NewGuid().ToString("N") + ".pk3");
        try
        {
            using (var fs = File.Create(path))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                void Add(string name, byte[] bytes) { using var s = zip.CreateEntry(name).Open(); s.Write(bytes, 0, bytes.Length); }
                Add("flats/SHARED.png", Png(1, 1, SolidRgba(1, 1, 1, 1, 1, 255)));
                Add("textures/SHARED.png", Png(1, 1, SolidRgba(1, 1, 2, 2, 2, 255)));
            }
            using var rm = new ResourceManager();
            rm.AddResource(path);
            Assert.Equal(new byte[] { 1, 1, 1, 255 }, rm.GetFlat("SHARED")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 2, 2, 2, 255 }, rm.GetWallTexture("SHARED")!.Rgba[0..4]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Pk3RootImagesRequireUdbResourceOptions()
    {
        string path = TestArtifacts.BuildPk3(
            ("ROOTFLAT.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 10, 11, 12, 255))),
            ("ROOTWALL.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 20, 21, 22, 255))),
            ("flats/ROOTFLAT.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 30, 31, 32, 255))),
            ("textures/ROOTWALL.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 40, 41, 42, 255))));
        try
        {
            using (var rm = new ResourceManager())
            {
                rm.AddResource(path);
                Assert.Equal(new byte[] { 30, 31, 32, 255 }, rm.GetFlat("ROOTFLAT")!.Rgba[0..4]);
                Assert.Equal(new byte[] { 40, 41, 42, 255 }, rm.GetWallTexture("ROOTWALL")!.Rgba[0..4]);
            }

            using (var rm = new ResourceManager())
            {
                rm.AddResource(new DataLocation(DataLocationType.Pk3, path, option1: true, option2: true));
                Assert.Equal(new byte[] { 10, 11, 12, 255 }, rm.GetFlat("ROOTFLAT")!.Rgba[0..4]);
                Assert.Equal(new byte[] { 20, 21, 22, 255 }, rm.GetWallTexture("ROOTWALL")!.Rgba[0..4]);
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Pk3ResourceHonorsConfiguredIgnoredPaths()
    {
        string path = TestArtifacts.BuildPk3(
            ("textures/KEEP.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 10, 11, 12, 255))),
            (".git/textures/HIDDEN.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 20, 21, 22, 255))),
            ("textures/SKIP.ignore", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 30, 31, 32, 255))));
        try
        {
            var config = GameConfiguration.FromText("""
                ignoreddirectories = ".git";
                ignoredextensions = "ignore";
                """);

            using var rm = new ResourceManager(config);
            rm.AddResource(path);

            Assert.NotNull(rm.GetWallTexture("KEEP"));
            Assert.Null(rm.GetWallTexture("HIDDEN"));
            Assert.Null(rm.GetWallTexture("SKIP"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void NestedWadInsidePk3ProvidesPaletteAndFlats()
    {
        string path = Path.Combine(Path.GetTempPath(), "dbuilder_test_" + Guid.NewGuid().ToString("N") + ".pk3");
        try
        {
            using (var fs = File.Create(path))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                using var s = zip.CreateEntry("resources/nested.wad").Open();
                var bytes = BuildNestedResourceWad();
                s.Write(bytes, 0, bytes.Length);
            }

            using var rm = new ResourceManager();
            rm.AddResource(path);

            Assert.NotNull(rm.Palette);
            var flat = rm.GetFlat("NESTFLAT");
            Assert.NotNull(flat);
            Assert.Equal(64, flat!.Width);
            Assert.Equal(new byte[] { 42, 42, 42, 255 }, flat.Rgba[0..4]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void NestedPk3InsidePk3ProvidesFolderResources()
    {
        string path = TestArtifacts.BuildPk3(("archives/nested.pk3", BuildNestedResourcePk3()));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(path);

            var flat = rm.GetFlat("NESTPK3");
            Assert.NotNull(flat);
            Assert.Equal(new byte[] { 33, 44, 55, 255 }, flat!.Rgba[0..4]);
            Assert.Contains("NESTPK3", rm.GetFlatNames());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void NestedIpk3InsidePk3ProvidesFolderResources()
    {
        string path = TestArtifacts.BuildPk3(("archives/nested.ipk3", BuildNestedResourcePk3()));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(path);

            var flat = rm.GetFlat("NESTPK3");
            Assert.NotNull(flat);
            Assert.Equal(new byte[] { 33, 44, 55, 255 }, flat!.Rgba[0..4]);
            Assert.Contains("NESTPK3", rm.GetFlatNames());
        }
        finally { File.Delete(path); }
    }
}
