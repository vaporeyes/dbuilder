// ABOUTME: Tests ResourceManager priority and namespace behavior for textures, flats, and sprites.
// ABOUTME: Uses synthetic PK3 resources so override order and same-name conflicts stay deterministic.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class ResourcePriorityTests
{
    [Fact]
    public void LaterPk3OverridesEarlierTextureAndSprite()
    {
        string lower = TestArtifacts.BuildPk3(
            ("textures/OVERRIDE.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 10, 11, 12, 255))),
            ("sprites/POSSA0.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 20, 21, 22, 255))));
        string higher = TestArtifacts.BuildPk3(
            ("textures/OVERRIDE.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 30, 31, 32, 255))),
            ("sprites/POSSA0.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 40, 41, 42, 255))));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(lower);
            rm.AddResource(higher);

            Assert.Equal(new byte[] { 30, 31, 32, 255 }, rm.GetWallTexture("OVERRIDE")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 40, 41, 42, 255 }, rm.GetSprite("POSSA0")!.Rgba[0..4]);
        }
        finally
        {
            File.Delete(lower);
            File.Delete(higher);
        }
    }

    [Fact]
    public void SameNameEntriesResolveByRequestedNamespace()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("flats/SHARED.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 1, 2, 3, 255))),
            ("textures/SHARED.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 4, 5, 6, 255))),
            ("sprites/SHARED.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 7, 8, 9, 255))));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            Assert.Equal(new byte[] { 1, 2, 3, 255 }, rm.GetFlat("SHARED")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 4, 5, 6, 255 }, rm.GetWallTexture("SHARED")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 7, 8, 9, 255 }, rm.GetSprite("SHARED")!.Rgba[0..4]);
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void LaterSingleImageOverridesEarlierTexturesDefinition()
    {
        string lowerTextures =
            "WallTexture STACKED, 1, 1\n" +
            "{\n" +
            "    Patch \"LOWER\", 0, 0\n" +
            "}\n";
        string lower = TestArtifacts.BuildPk3(
            ("TEXTURES.txt", Encoding.ASCII.GetBytes(lowerTextures)),
            ("patches/LOWER.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 10, 11, 12, 255))));
        string higher = TestArtifacts.BuildPk3(
            ("textures/STACKED.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 40, 41, 42, 255))));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(lower);
            rm.AddResource(higher);

            Assert.Equal(new byte[] { 40, 41, 42, 255 }, rm.GetWallTexture("STACKED")!.Rgba[0..4]);
        }
        finally
        {
            File.Delete(lower);
            File.Delete(higher);
        }
    }
}
