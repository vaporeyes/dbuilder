// ABOUTME: Tests ResourceManager behavior across mixed IWAD, PWAD, PK3, nested WAD, and directory resources.
// ABOUTME: Builds synthetic assets so priority and fallback behavior are covered without copyrighted game data.

using System;
using System.IO;
using DBuilder.IO;

namespace DBuilder.Tests;

public class ResourceStackTests
{
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

    private static byte[] Marker() => new byte[] { 0 };

    private static string BuildWadFile(params (string name, byte[] bytes)[] lumps)
    {
        string path = Path.Combine(Path.GetTempPath(), "dbuilder_stack_" + Guid.NewGuid().ToString("N") + ".wad");
        using (var wad = new WAD(path))
        {
            int position = 0;
            foreach (var (name, bytes) in lumps) Insert(wad, name, bytes, position++);
            wad.WriteHeaders();
        }
        return path;
    }

    private static byte[] BuildNestedWadBytes()
    {
        using var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            Insert(wad, "F_START", Array.Empty<byte>());
            Insert(wad, "PK3NEST", SolidFlat(70));
            Insert(wad, "F_END", Array.Empty<byte>());
            wad.WriteHeaders();
        }
        return ms.ToArray();
    }

    private static void Insert(WAD wad, string name, byte[] bytes)
        => Insert(wad, name, bytes, wad.Lumps.Count);

    private static void Insert(WAD wad, string name, byte[] bytes, int position)
    {
        var lump = wad.Insert(name, position, bytes.Length)!;
        lump.Stream.Write(bytes, 0, bytes.Length);
    }

    private static string BuildResourceDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "dbuilder_stack_dir_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "flats"));
        File.WriteAllBytes(
            Path.Combine(root, "flats", "STACKFLAT.png"),
            TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 90, 91, 92, 255)));
        File.WriteAllBytes(
            Path.Combine(root, "flats", "DIRONLY.png"),
            TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 100, 101, 102, 255)));
        Directory.CreateDirectory(Path.Combine(root, "textures"));
        File.WriteAllBytes(
            Path.Combine(root, "textures", "DIRWALL.png"),
            TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 110, 111, 112, 255)));
        return root;
    }

    [Fact]
    public void MixedResourceStackResolvesPriorityAndFallbacks()
    {
        using var iwad = TestArtifacts.BuildWad(
            ("PLAYPAL", GrayscalePlaypal()),
            ("F_START", Array.Empty<byte>()),
            ("BASEFL", SolidFlat(10)),
            ("STACKFLAT", SolidFlat(20)),
            ("F_END", Array.Empty<byte>()));
        using var pwad = TestArtifacts.BuildWad(
            ("F_START", Array.Empty<byte>()),
            ("PWADFL", SolidFlat(40)),
            ("STACKFLAT", SolidFlat(50)),
            ("F_END", Array.Empty<byte>()));
        string pk3 = TestArtifacts.BuildPk3(
            ("flats/PK3ONLY.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 60, 61, 62, 255))),
            ("nested.wad", BuildNestedWadBytes()));
        string dir = BuildResourceDirectory();

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(iwad);
            rm.AddResource(pwad);
            rm.AddResource(pk3);
            rm.AddResource(dir);

            Assert.Equal(new byte[] { 90, 91, 92, 255 }, rm.GetFlat("STACKFLAT")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 60, 61, 62, 255 }, rm.GetFlat("PK3ONLY")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 70, 70, 70, 255 }, rm.GetFlat("PK3NEST")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 100, 101, 102, 255 }, rm.GetFlat("DIRONLY")!.Rgba[0..4]);

            Assert.Contains("PK3NEST", rm.GetFlatNames());
            Assert.Contains("DIRONLY", rm.GetFlatNames());
        }
        finally
        {
            File.Delete(pk3);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ResourceTextureSetsExposePerResourceNames()
    {
        string iwad = BuildWadFile(
            ("PLAYPAL", GrayscalePlaypal()),
            ("F_START", Marker()),
            ("BASEONLY", SolidFlat(10)),
            ("F_END", Marker()));
        string pk3 = TestArtifacts.BuildPk3(
            ("flats/PK3ONLY.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 60, 61, 62, 255))),
            ("textures/PK3WALL.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 63, 64, 65, 255))));
        string dir = BuildResourceDirectory();

        try
        {
            using var rm = new ResourceManager();
            rm.AddBaseResource(iwad);
            rm.AddResource(pk3);
            rm.AddResource(dir);

            var sets = rm.GetResourceTextureSets();
            var pk3Set = Assert.Single(sets, s => s.Name == Path.GetFileName(pk3));
            Assert.True(pk3Set.TextureExists("PK3WALL"));
            Assert.True(pk3Set.FlatExists("PK3ONLY"));

            var dirSet = Assert.Single(sets, s => s.Name == Path.GetFileName(dir));
            Assert.True(dirSet.TextureExists("DIRWALL"));
            Assert.True(dirSet.FlatExists("DIRONLY"));
            dirSet.MixTexturesAndFlats();
            Assert.True(dirSet.FlatExists("DIRWALL"));
            Assert.True(dirSet.TextureExists("DIRONLY"));
        }
        finally
        {
            File.Delete(iwad);
            File.Delete(pk3);
            Directory.Delete(dir, recursive: true);
        }
    }
}
