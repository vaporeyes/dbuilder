// ABOUTME: Tests ResourceManager texture/flat name enumeration (PK3 folders + TEXTURES defs) for the browser.
// ABOUTME: Builds a temp PK3 and checks the collected, de-duplicated, sorted name lists.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class ResourceNameEnumerationTests
{
    private static WAD BuildWad(params (string name, byte[] data)[] lumps)
    {
        var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            int pos = 0;
            foreach (var (name, data) in lumps)
            {
                var lump = wad.Insert(name, pos++, data.Length)!;
                lump.Stream.Write(data, 0, data.Length);
            }
            wad.WriteHeaders();
        }
        ms.Position = 0;
        return new WAD(ms, openreadonly: true);
    }

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
    public void MixTexturesFlatsMergesNamesSetsAndLookups()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("textures/TEXONLY.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 10, 11, 12, 255))),
            ("flats/FLATONLY.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 20, 21, 22, 255))));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            Assert.DoesNotContain("FLATONLY", rm.GetTextureNames());
            Assert.DoesNotContain("TEXONLY", rm.GetFlatNames());
            Assert.Null(rm.GetWallTexture("FLATONLY"));
            Assert.Null(rm.GetFlat("TEXONLY"));

            rm.MixTexturesFlats = true;

            Assert.Contains("FLATONLY", rm.GetTextureNames());
            Assert.Contains("TEXONLY", rm.GetFlatNames());
            Assert.Equal(new byte[] { 20, 21, 22, 255 }, rm.GetWallTexture("FLATONLY")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 10, 11, 12, 255 }, rm.GetFlat("TEXONLY")!.Rgba[0..4]);

            var set = Assert.Single(rm.GetResourceTextureSets());
            Assert.True(set.TextureExists("FLATONLY"));
            Assert.True(set.FlatExists("TEXONLY"));
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

    [Fact]
    public void EnumeratesSpriteNamesFromWadAndPk3Resources()
    {
        using var wad = BuildWad(
            ("S_START", Array.Empty<byte>()),
            ("TROOA0", Array.Empty<byte>()),
            ("NOTSPR", Array.Empty<byte>()),
            ("BOSSA1B2", Array.Empty<byte>()),
            ("S_END", Array.Empty<byte>()));
        string pk3 = TestArtifacts.BuildPk3(
            ("sprites/POSSA0.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 1, 2, 3, 255))),
            ("sprites/notasprite.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 4, 5, 6, 255))));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(wad);
            rm.AddResource(pk3);

            var sprites = rm.GetSpriteNames();

            Assert.Contains("TROOA0", sprites);
            Assert.Contains("BOSSA1B2", sprites);
            Assert.Contains("POSSA0", sprites);
            Assert.DoesNotContain("NOTSPR", sprites);
            Assert.DoesNotContain("NOTASPRITE", sprites);
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void WadMarkerNameEnumerationResolvesEachRangeFamilyIndependently()
    {
        byte[] flat = new byte[64 * 64];
        using var wad = BuildWad(
            ("F_START", Array.Empty<byte>()),
            ("FLAT1", flat),
            ("FF_END", Array.Empty<byte>()),
            ("FLAT2", flat),
            ("F_END", Array.Empty<byte>()),
            ("S_START", Array.Empty<byte>()),
            ("TROOA0", Array.Empty<byte>()),
            ("SS_END", Array.Empty<byte>()),
            ("BOSSA1B2", Array.Empty<byte>()),
            ("S_END", Array.Empty<byte>()),
            ("V_START", Array.Empty<byte>()),
            ("BAR1", new byte[] { 1 }),
            ("VX_END", Array.Empty<byte>()),
            ("CYBR", new byte[] { 2 }),
            ("V_END", Array.Empty<byte>()));
        using var rm = new ResourceManager();
        rm.AddResource(wad);

        Assert.Contains("FLAT2", rm.GetFlatNames());
        Assert.Contains("BOSSA1B2", rm.GetSpriteNames());
        Assert.Contains("CYBR", rm.GetVoxelNames());
        Assert.Equal(new byte[] { 2 }, rm.GetVoxelBytes("CYBR"));
    }

    [Fact]
    public void EnumeratesSpriteNamesFromTexturesDefinitionsAndSkipsGraphics()
    {
        string textures = "Sprite SPOSA0, 4, 4 { NullTexture }\nGraphic GFXA0, 4, 4 { NullTexture }\n";
        string pk3 = TestArtifacts.BuildPk3(("TEXTURES.txt", Encoding.ASCII.GetBytes(textures)));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var sprites = rm.GetSpriteNames();

            Assert.Contains("SPOSA0", sprites);
            Assert.DoesNotContain("GFXA0", sprites);
        }
        finally { File.Delete(pk3); }
    }
}
