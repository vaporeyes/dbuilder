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
    public void UsesX11ColorsForTexturePatchBlend()
    {
        string textures =
            "WallTexture WX11, 1, 1\n" +
            "{\n" +
            "    Patch \"WHITE\", 0, 0\n" +
            "    {\n" +
            "        Blend \"ghostwhite\"\n" +
            "    }\n" +
            "}\n";

        string pk3 = TestArtifacts.BuildPk3(
            ("X11R6RGB", Encoding.ASCII.GetBytes("248 248 255 ghost white\n")),
            ("TEXTURES.txt", Encoding.ASCII.GetBytes(textures)),
            ("patches/WHITE.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 255, 255, 255, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var tex = rm.GetWallTexture("WX11");
            Assert.NotNull(tex);
            Assert.Equal(new byte[] { 248, 248, 255, 255 }, tex!.Rgba[0..4]);
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

    [Fact]
    public void AppliesPatchRenderStylesDuringComposition()
    {
        string textures =
            "WallTexture WSTYLE, 3, 1\n" +
            "{\n" +
            "    Patch \"BASE\", 0, 0\n" +
            "    Patch \"ADD\", 0, 0 { Style Add }\n" +
            "    Patch \"BASE\", 1, 0\n" +
            "    Patch \"SUB\", 1, 0 { Style Subtract }\n" +
            "    Patch \"BASE\", 2, 0\n" +
            "    Patch \"MOD\", 2, 0 { Style Modulate }\n" +
            "}\n";

        string pk3 = TestArtifacts.BuildPk3(
            ("TEXTURES.txt", Encoding.ASCII.GetBytes(textures)),
            ("patches/BASE.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 100, 100, 100, 255))),
            ("patches/ADD.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 80, 60, 40, 255))),
            ("patches/SUB.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 30, 40, 50, 255))),
            ("patches/MOD.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 128, 64, 255, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var tex = rm.GetWallTexture("WSTYLE");
            Assert.NotNull(tex);
            Assert.Equal(new byte[] { 180, 160, 140, 255 }, tex!.Rgba[0..4]);
            Assert.Equal(new byte[] { 70, 60, 50, 255 }, tex.Rgba[4..8]);
            Assert.Equal(new byte[] { 50, 25, 100, 255 }, tex.Rgba[8..12]);
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void LoadsMultipleRootTexturesFilesFromPk3()
    {
        string first =
            "WallTexture WFIRST, 1, 1\n" +
            "{\n" +
            "    Patch \"RED\", 0, 0\n" +
            "}\n";
        string second =
            "WallTexture WSECOND, 1, 1\n" +
            "{\n" +
            "    Patch \"BLUE\", 0, 0\n" +
            "}\n";

        string pk3 = TestArtifacts.BuildPk3(
            ("TEXTURES.txt", Encoding.ASCII.GetBytes(first)),
            ("TEXTURES.extra", Encoding.ASCII.GetBytes(second)),
            ("patches/RED.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 200, 0, 0, 255))),
            ("patches/BLUE.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 0, 0, 200, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            Assert.Equal(new byte[] { 200, 0, 0, 255 }, rm.GetWallTexture("WFIRST")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 0, 0, 200, 255 }, rm.GetWallTexture("WSECOND")!.Rgba[0..4]);
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void ResolvesTexturePatchesFromTextureAndFlatFolders()
    {
        string textures =
            "WallTexture WFOLDERS, 2, 1\n" +
            "{\n" +
            "    Patch \"TEXPAT\", 0, 0\n" +
            "    Patch \"FLTPAT\", 1, 0\n" +
            "}\n";

        string pk3 = TestArtifacts.BuildPk3(
            ("TEXTURES.txt", Encoding.ASCII.GetBytes(textures)),
            ("textures/TEXPAT.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 20, 30, 40, 255))),
            ("flats/FLTPAT.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 50, 60, 70, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var tex = rm.GetWallTexture("WFOLDERS");
            Assert.NotNull(tex);
            Assert.Equal(new byte[] { 20, 30, 40, 255 }, tex!.Rgba[0..4]);
            Assert.Equal(new byte[] { 50, 60, 70, 255 }, tex.Rgba[4..8]);
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void MissingRequiredTexturesPatchesDoNotCreateTransparentTexture()
    {
        string pk3 = TestArtifacts.BuildPk3(("TEXTURES.txt", Encoding.ASCII.GetBytes("WallTexture WMISS, 1, 1 { Patch \"MISSING\", 0, 0 }\n")));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            Assert.Null(rm.GetWallTexture("WMISS"));
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void NullTextureWithoutPatchesCreatesTransparentTexture()
    {
        string textures =
            "WallTexture WNULL, 1, 1\n" +
            "{\n" +
            "    NullTexture\n" +
            "}\n";
        string pk3 = TestArtifacts.BuildPk3(("TEXTURES.txt", Encoding.ASCII.GetBytes(textures)));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var tex = rm.GetWallTexture("WNULL");
            Assert.NotNull(tex);
            Assert.Equal(new byte[] { 0, 0, 0, 0 }, tex!.Rgba[0..4]);
        }
        finally { File.Delete(pk3); }
    }
}
