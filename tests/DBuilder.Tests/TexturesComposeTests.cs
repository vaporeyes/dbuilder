// ABOUTME: End-to-end test: a PK3 TEXTURES lump composes patches into a wall texture via ResourceManager.
// ABOUTME: Verifies a 4x2 texture built from two 2x2 patches resolves with the patches blitted at their offsets.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class TexturesComposeTests
{
    [Fact]
    public void ComposesWallTextureFromPatches()
    {
        string textures =
            "WallTexture WTEST, 4, 2\n" +
            "{\n" +
            "    Patch \"REDPAT\", 0, 0\n" +
            "    Patch \"BLUPAT\", 2, 0\n" +
            "}\n";

        string pk3 = TestArtifacts.BuildPk3(
            ("TEXTURES.txt", Encoding.ASCII.GetBytes(textures)),
            ("patches/REDPAT.png", TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 200, 0, 0, 255))),
            ("patches/BLUPAT.png", TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 0, 0, 200, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var tex = rm.GetWallTexture("WTEST");
            Assert.NotNull(tex);
            Assert.Equal(4, tex!.Width);
            Assert.Equal(2, tex.Height);

            // Left half (x=0,1) is the red patch; right half (x=2,3) is the blue patch.
            Assert.Equal(new byte[] { 200, 0, 0, 255 }, tex.Rgba[0..4]);          // (0,0)
            Assert.Equal(new byte[] { 200, 0, 0, 255 }, tex.Rgba[4..8]);          // (1,0)
            Assert.Equal(new byte[] { 0, 0, 200, 255 }, tex.Rgba[8..12]);         // (2,0)
            Assert.Equal(new byte[] { 0, 0, 200, 255 }, tex.Rgba[12..16]);        // (3,0)
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void TextureDefinitionOverridesSingleImage()
    {
        // A texture both as a single image (textures/WSOLID.png, green) and a TEXTURES def (red patch).
        // The TEXTURES definition must win.
        string textures = "WallTexture WSOLID, 2, 2 { Patch \"REDPAT\", 0, 0 }\n";
        string pk3 = TestArtifacts.BuildPk3(
            ("TEXTURES.txt", Encoding.ASCII.GetBytes(textures)),
            ("textures/WSOLID.png", TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 0, 255, 0, 255))),
            ("patches/REDPAT.png", TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 200, 0, 0, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);
            var tex = rm.GetWallTexture("WSOLID");
            Assert.NotNull(tex);
            Assert.Equal(new byte[] { 200, 0, 0, 255 }, tex!.Rgba[0..4]); // red (from TEXTURES), not green
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void AppliesPatchAlphaBlendAndSkipMetadata()
    {
        string textures =
            "WallTexture WMETA, 1, 1\n" +
            "{\n" +
            "    Patch \"BASE\", 0, 0\n" +
            "    Patch \"TNT1A0\", 0, 0\n" +
            "    Patch \"WHITE\", 0, 0\n" +
            "    {\n" +
            "        Alpha 0.5\n" +
            "        Blend 255, 0, 0\n" +
            "    }\n" +
            "}\n";

        string pk3 = TestArtifacts.BuildPk3(
            ("TEXTURES.txt", Encoding.ASCII.GetBytes(textures)),
            ("patches/BASE.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 100, 100, 100, 255))),
            ("patches/TNT1A0.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 0, 255, 0, 255))),
            ("patches/WHITE.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 255, 255, 255, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var tex = rm.GetWallTexture("WMETA");
            Assert.NotNull(tex);
            Assert.Equal(new byte[] { 177, 49, 49, 255 }, tex!.Rgba[0..4]);
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void AppliesPatchRotationDuringComposition()
    {
        string textures =
            "WallTexture WROT, 1, 2\n" +
            "{\n" +
            "    Patch \"STRIP\", 0, 0\n" +
            "    {\n" +
            "        Rotate 90\n" +
            "    }\n" +
            "}\n";

        byte[] pixels =
        {
            10, 0, 0, 255,
            20, 0, 0, 255,
        };
        string pk3 = TestArtifacts.BuildPk3(
            ("TEXTURES.txt", Encoding.ASCII.GetBytes(textures)),
            ("patches/STRIP.png", TestArtifacts.Png(2, 1, pixels)));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var tex = rm.GetWallTexture("WROT");
            Assert.NotNull(tex);
            Assert.Equal(new byte[] { 10, 0, 0, 255 }, tex!.Rgba[0..4]);
            Assert.Equal(new byte[] { 20, 0, 0, 255 }, tex.Rgba[4..8]);
        }
        finally { File.Delete(pk3); }
    }
}
