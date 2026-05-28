// ABOUTME: Tests ResourceManager texture/flat name enumeration (PK3 folders + TEXTURES defs) for the browser.
// ABOUTME: Builds a temp PK3 and checks the collected, de-duplicated, sorted name lists.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class ResourceNameEnumerationTests
{
    [Fact]
    public void EnumeratesPk3TextureAndFlatNamesPlusTexturesDefs()
    {
        string textures = "WallTexture CCC, 4, 4 { Patch \"AAA\", 0, 0 }\nFlat DDD, 64, 64 { Patch \"FLT1\", 0, 0 }\n";
        string pk3 = TestArtifacts.BuildPk3(
            ("TEXTURES.txt", Encoding.ASCII.GetBytes(textures)),
            ("textures/AAA.png", TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 1, 1, 1, 255))),
            ("textures/BBB.png", TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 2, 2, 2, 255))),
            ("flats/FLT1.png", TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 3, 3, 3, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var textureNames = rm.GetTextureNames();
            Assert.Contains("AAA", textureNames);
            Assert.Contains("BBB", textureNames);
            Assert.Contains("CCC", textureNames); // from TEXTURES WallTexture def

            var flatNames = rm.GetFlatNames();
            Assert.Contains("FLT1", flatNames);
            Assert.Contains("DDD", flatNames); // from TEXTURES Flat def

            // Sorted ascending.
            for (int i = 1; i < textureNames.Count; i++)
                Assert.True(string.CompareOrdinal(textureNames[i - 1].ToUpperInvariant(), textureNames[i].ToUpperInvariant()) <= 0);
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void DeduplicatesAcrossResources()
    {
        string a = TestArtifacts.BuildPk3(("textures/SHARED.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 0, 0, 0, 255))));
        string b = TestArtifacts.BuildPk3(("textures/SHARED.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 9, 9, 9, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(a);
            rm.AddResource(b);
            int count = 0;
            foreach (var n in rm.GetTextureNames()) if (n.Equals("SHARED", System.StringComparison.OrdinalIgnoreCase)) count++;
            Assert.Equal(1, count);
        }
        finally { File.Delete(a); File.Delete(b); }
    }

    [Fact]
    public void ResourceTextureSetsIncludeTexturesDefinitions()
    {
        string textures =
            "WallTexture SETWALL, 4, 4 { Patch \"P\", 0, 0 }\n" +
            "Flat SETFLAT, 64, 64 { Patch \"P\", 0, 0 }\n" +
            "Texture SETBOTH, 8, 8 { Patch \"P\", 0, 0 }\n";
        string pk3 = TestArtifacts.BuildPk3(("TEXTURES.txt", Encoding.ASCII.GetBytes(textures)));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var set = Assert.Single(rm.GetResourceTextureSets());
            Assert.True(set.TextureExists("SETWALL"));
            Assert.True(set.FlatExists("SETFLAT"));
            Assert.True(set.TextureExists("SETBOTH"));
            Assert.True(set.FlatExists("SETBOTH"));
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void ColormapFolderNamesAreTextureNames()
    {
        string pk3 = TestArtifacts.BuildPk3(("colormaps/FOGMAP.lmp", new byte[DoomColormap.LevelSize]));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            Assert.Contains("FOGMAP", rm.GetTextureNames());
            var set = Assert.Single(rm.GetResourceTextureSets());
            Assert.True(set.TextureExists("FOGMAP"));
        }
        finally { File.Delete(pk3); }
    }
}
