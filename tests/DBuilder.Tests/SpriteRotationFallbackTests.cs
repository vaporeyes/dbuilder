// ABOUTME: Tests ResourceManager.GetSprite rotation fallback so TROOA0 resolves a lump stored as TROOA1, etc.
// ABOUTME: Uses a temp PK3 with PNG sprites under sprites/.

using System.IO;
using DBuilder.IO;

namespace DBuilder.Tests;

public class SpriteRotationFallbackTests
{
    [Fact]
    public void FindsRotation1WhenAskedForRotation0()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("sprites/TROOA1.png", TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 9, 8, 7, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);
            var sprite = rm.GetSprite("TROOA0");          // asked for rotation 0
            Assert.NotNull(sprite);
            Assert.Equal(new byte[] { 9, 8, 7, 255 }, sprite!.Rgba[0..4]);
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void AppendsRotationToFiveCharName()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("sprites/POSSA0.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 1, 2, 3, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);
            var sprite = rm.GetSprite("POSSA");           // 5-char name, no rotation digit
            Assert.NotNull(sprite);
            Assert.Equal(new byte[] { 1, 2, 3, 255 }, sprite!.Rgba[0..4]);
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void ExactMatchStillWins()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("sprites/BOSSA0.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 100, 0, 0, 255))),
            ("sprites/BOSSA1.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 0, 100, 0, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);
            Assert.Equal(new byte[] { 100, 0, 0, 255 }, rm.GetSprite("BOSSA0")!.Rgba[0..4]); // exact A0
            Assert.Equal(new byte[] { 0, 100, 0, 255 }, rm.GetSprite("BOSSA1")!.Rgba[0..4]); // exact A1
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void FindsSecondFrameInPairedSpriteName()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("sprites/POSSA1B1.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 40, 50, 60, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var sprite = rm.GetSprite("POSSB1");

            Assert.NotNull(sprite);
            Assert.Equal(new byte[] { 40, 50, 60, 255 }, sprite!.Rgba[0..4]);
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void FiveCharNameFindsSecondFrameInPairedSpriteName()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("sprites/POSSA1B1.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 70, 80, 90, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var sprite = rm.GetSprite("POSSB");

            Assert.NotNull(sprite);
            Assert.Equal(new byte[] { 70, 80, 90, 255 }, sprite!.Rgba[0..4]);
        }
        finally { File.Delete(pk3); }
    }
}
