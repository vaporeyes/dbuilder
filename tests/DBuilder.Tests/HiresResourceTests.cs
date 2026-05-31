// ABOUTME: Tests high-resolution replacement lookup from folder-structured resources.
// ABOUTME: Builds synthetic PK3 entries under hires/ and regular namespaces to verify replacement priority.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class HiresResourceTests
{
    private static byte[] DoomPatch(byte index)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        w.Write((short)1);
        w.Write((short)1);
        w.Write((short)0);
        w.Write((short)0);
        w.Write((int)12);
        w.Write((byte)0);
        w.Write((byte)1);
        w.Write((byte)0);
        w.Write(index);
        w.Write((byte)0);
        w.Write((byte)0xFF);
        return ms.ToArray();
    }

    [Fact]
    public void HiresEntryOverridesRegularTextureFlatAndSprite()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("textures/ROCK.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 10, 11, 12, 255))),
            ("flats/ROCK.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 20, 21, 22, 255))),
            ("sprites/ROCK.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 30, 31, 32, 255))),
            ("hires/ROCK.png", TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 40, 41, 42, 255))));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            Assert.Equal(new byte[] { 40, 41, 42, 255 }, rm.GetWallTexture("ROCK")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 40, 41, 42, 255 }, rm.GetFlat("ROCK")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 40, 41, 42, 255 }, rm.GetSprite("ROCK")!.Rgba[0..4]);
            Assert.Equal(2, rm.GetWallTexture("ROCK")!.Width);
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void HiresEntryOverridesLowerPriorityResources()
    {
        string basePk3 = TestArtifacts.BuildPk3(
            ("textures/SHARED.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 10, 11, 12, 255))),
            ("flats/SHARED.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 20, 21, 22, 255))),
            ("sprites/SHARED.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 30, 31, 32, 255))));
        string hiPk3 = TestArtifacts.BuildPk3(
            ("hires/SHARED.png", TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 50, 51, 52, 255))));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(basePk3);
            rm.AddResource(hiPk3);

            Assert.Equal(new byte[] { 50, 51, 52, 255 }, rm.GetWallTexture("SHARED")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 50, 51, 52, 255 }, rm.GetFlat("SHARED")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 50, 51, 52, 255 }, rm.GetSprite("SHARED")!.Rgba[0..4]);
            Assert.Equal(2, rm.GetFlat("SHARED")!.Width);
        }
        finally
        {
            File.Delete(basePk3);
            File.Delete(hiPk3);
        }
    }

    [Fact]
    public void HiresEntryDoesNotResolveWithoutRegularEntry()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("hires/ONLYHI.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 5, 6, 7, 255))));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            Assert.Null(rm.GetWallTexture("ONLYHI"));
            Assert.Null(rm.GetFlat("ONLYHI"));
            Assert.Null(rm.GetSprite("ONLYHI"));
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void ConfiguredWadHiresRangeOverridesResolvableBaseImage()
    {
        var config = GameConfiguration.FromText("""
            hires
            {
                detail { start = "HI_START"; end = "HI_END"; }
            }
            """);
        string wad = TestArtifacts.BuildPwadFile(
            ("PLAYPAL", TestArtifacts.GrayscalePlaypal()),
            ("ROCK", TestArtifacts.SolidFlat(9)),
            ("HI_START", []),
            ("ROCK", DoomPatch(70)),
            ("HI_END", []));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(wad);

            Assert.Equal(9, rm.GetFlat("ROCK")!.Rgba[0]);

            rm.Configuration = config;

            Assert.Equal(new byte[] { 70, 70, 70, 255 }, rm.GetFlat("ROCK")!.Rgba[0..4]);
            Assert.Equal(1, rm.GetFlat("ROCK")!.Width);
        }
        finally
        {
            File.Delete(wad);
        }
    }

    [Fact]
    public void DirectoryNestedWadHiresOverridesDirectoryHiresFolder()
    {
        var config = GameConfiguration.FromText("""
            hires
            {
                detail { start = "HI_START"; end = "HI_END"; }
            }
            """);
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_hires_dir_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "textures"));
            Directory.CreateDirectory(Path.Combine(dir, "hires"));
            File.WriteAllBytes(Path.Combine(dir, "textures", "ROCK.png"), TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 9, 9, 9, 255)));
            File.WriteAllBytes(Path.Combine(dir, "hires", "ROCK.png"), TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 40, 41, 42, 255)));

            using (var wad = new WAD(Path.Combine(dir, "resources.wad")))
            {
                Insert(wad, "PLAYPAL", TestArtifacts.GrayscalePlaypal());
                Insert(wad, "HI_START", []);
                Insert(wad, "ROCK", DoomPatch(70));
                Insert(wad, "HI_END", []);
                wad.WriteHeaders();
            }

            using var rm = new ResourceManager(config);
            rm.AddResource(dir);

            Assert.Equal(new byte[] { 70, 70, 70, 255 }, rm.GetWallTexture("ROCK")!.Rgba[0..4]);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    private static void Insert(WAD wad, string name, byte[] bytes)
    {
        var lump = wad.Insert(name, wad.Lumps.Count, bytes.Length)!;
        lump.Stream.Write(bytes, 0, bytes.Length);
    }
}
