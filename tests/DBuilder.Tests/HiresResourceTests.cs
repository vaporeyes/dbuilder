// ABOUTME: Tests high-resolution replacement lookup from folder-structured resources.
// ABOUTME: Builds synthetic PK3 entries under hires/ and regular namespaces to verify replacement priority.

using System.IO;
using DBuilder.IO;

namespace DBuilder.Tests;

public class HiresResourceTests
{
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
}
