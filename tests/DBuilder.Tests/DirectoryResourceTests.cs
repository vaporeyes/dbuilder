// ABOUTME: Tests directory (folder) resource loading via ResourceManager - the dev-folder equivalent of a PK3.
// ABOUTME: Builds a temp folder tree with PNG entries and a TEXTURES lump and checks name/image resolution.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class DirectoryResourceTests
{
    private static string BuildResourceDir()
    {
        string root = Path.Combine(Path.GetTempPath(), "dbuilder_dir_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "flats"));
        Directory.CreateDirectory(Path.Combine(root, "textures"));
        Directory.CreateDirectory(Path.Combine(root, "sprites"));
        File.WriteAllBytes(Path.Combine(root, "flats", "DFLOOR.png"), TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 11, 22, 33, 255)));
        File.WriteAllBytes(Path.Combine(root, "textures", "DWALL.png"), TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 44, 55, 66, 255)));
        File.WriteAllBytes(Path.Combine(root, "sprites", "DSPRA0.png"), TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 77, 88, 99, 255)));
        File.WriteAllText(Path.Combine(root, "TEXTURES.txt"), "WallTexture DCOMP, 2, 2 { Patch \"DSPRA0\", 0, 0 }\n");
        return root;
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

    private static void WriteRootWad(string path)
    {
        using var wad = new WAD(path);
        Insert(wad, "PLAYPAL", GrayscalePlaypal());
        Insert(wad, "F_START", System.Array.Empty<byte>());
        Insert(wad, "ROOTFLAT", SolidFlat(77));
        Insert(wad, "F_END", System.Array.Empty<byte>());
        wad.WriteHeaders();
    }

    private static void Insert(WAD wad, string name, byte[] bytes)
    {
        var lump = wad.Insert(name, wad.Lumps.Count, bytes.Length)!;
        lump.Stream.Write(bytes, 0, bytes.Length);
    }

    [Fact]
    public void ResolvesImagesFromAFolder()
    {
        string dir = BuildResourceDir();
        try
        {
            using var rm = new ResourceManager { MixTexturesFlats = true };
            rm.AddResource(dir);

            Assert.Equal(new byte[] { 11, 22, 33, 255 }, rm.GetFlat("DFLOOR")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 44, 55, 66, 255 }, rm.GetWallTexture("DWALL")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 77, 88, 99, 255 }, rm.GetSprite("DSPRA0")!.Rgba[0..4]);

            Assert.Contains("DWALL", rm.GetTextureNames());
            Assert.Contains("DFLOOR", rm.GetFlatNames());
            // The TEXTURES file is read from the folder too (composes a wall from the sprite patch).
            Assert.NotNull(rm.GetWallTexture("DCOMP"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void PathQualifiedDirectoryLookupsRequireExactFileExtensionLikeUdb()
    {
        string dir = BuildResourceDir();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "textures", "ROCK.png"), TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 10, 11, 12, 255)));

            using var rm = new ResourceManager();
            rm.AddResource(dir);

            Assert.NotNull(rm.GetWallTexture("textures/ROCK.png"));
            Assert.Null(rm.GetWallTexture("textures/ROCK.lmp"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void DirectoryNamespaceLookupsSearchSubfoldersAndLongTitlesLikeUdb()
    {
        string dir = BuildResourceDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "textures", "detail"));
            Directory.CreateDirectory(Path.Combine(dir, "flats", "detail"));
            File.WriteAllBytes(Path.Combine(dir, "textures", "detail", "SUBWALL.png"), TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 10, 11, 12, 255)));
            File.WriteAllBytes(Path.Combine(dir, "flats", "detail", "SUBFLAT.png"), TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 20, 21, 22, 255)));
            File.WriteAllBytes(Path.Combine(dir, "textures", "LONGNAMEEXTRA.png"), TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 30, 31, 32, 255)));

            using var rm = new ResourceManager();
            rm.AddResource(dir);

            Assert.Equal(new byte[] { 10, 11, 12, 255 }, rm.GetWallTexture("SUBWALL")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 20, 21, 22, 255 }, rm.GetFlat("SUBFLAT")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 30, 31, 32, 255 }, rm.GetWallTexture("LONGNAME")!.Rgba[0..4]);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void DirectoryOverlaidUnderAWadResolvesBoth()
    {
        // A base WAD-less manager: directory provides everything; sprite rotation fallback still applies.
        string dir = BuildResourceDir();
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(dir);
            Assert.NotNull(rm.GetSprite("DSPRA")); // 5-char name resolves DSPRA0 via rotation fallback
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void RootWadInsideDirectoryProvidesNestedResources()
    {
        string dir = BuildResourceDir();
        try
        {
            WriteRootWad(Path.Combine(dir, "resources.wad"));

            using var rm = new ResourceManager();
            rm.AddResource(dir);

            Assert.NotNull(rm.Palette);
            var flat = rm.GetFlat("ROOTFLAT");
            Assert.NotNull(flat);
            Assert.Equal(new byte[] { 77, 77, 77, 255 }, flat!.Rgba[0..4]);
            Assert.Contains("ROOTFLAT", rm.GetFlatNames());
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void NestedPk3FamilyArchiveInsideDirectoryProvidesResourcesLikeUdb()
    {
        string dir = BuildResourceDir();
        string pk3 = TestArtifacts.BuildPk3(
            ("flats/NESTDIR.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 33, 44, 55, 255))));
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "archives"));
            File.Copy(pk3, Path.Combine(dir, "archives", "nested.ipk3"));

            using var rm = new ResourceManager();
            rm.AddResource(dir);

            var flat = rm.GetFlat("NESTDIR");
            Assert.NotNull(flat);
            Assert.Equal(new byte[] { 33, 44, 55, 255 }, flat!.Rgba[0..4]);
            Assert.Contains("NESTDIR", rm.GetFlatNames());
        }
        finally
        {
            File.Delete(pk3);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void DirectoryDataLocationRootOptionsExposeRootImagesLikeUdb()
    {
        string dir = BuildResourceDir();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "ROOTWALL.png"), TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 12, 34, 56, 255)));
            File.WriteAllBytes(Path.Combine(dir, "ROOTFLAT.png"), TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 65, 43, 21, 255)));

            using (var defaultOptions = new ResourceManager())
            {
                defaultOptions.AddResource(new DataLocation(DataLocationType.Directory, dir));
                Assert.Null(defaultOptions.GetWallTexture("ROOTWALL"));
                Assert.Null(defaultOptions.GetFlat("ROOTFLAT"));
            }

            using (var rootOptions = new ResourceManager())
            {
                rootOptions.AddResource(new DataLocation(DataLocationType.Directory, dir, option1: true, option2: true));
                Assert.Equal(new byte[] { 12, 34, 56, 255 }, rootOptions.GetWallTexture("ROOTWALL")!.Rgba[0..4]);
                Assert.Equal(new byte[] { 65, 43, 21, 255 }, rootOptions.GetFlat("ROOTFLAT")!.Rgba[0..4]);
                Assert.Contains("ROOTWALL", rootOptions.GetTextureNames());
                Assert.Contains("ROOTFLAT", rootOptions.GetFlatNames());
            }
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void DirectoryResourceHonorsConfiguredIgnoredPaths()
    {
        string dir = BuildResourceDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, ".git", "textures"));
            File.WriteAllText(Path.Combine(dir, ".git", "HIDDEN.txt"), "hidden");
            File.WriteAllBytes(Path.Combine(dir, ".git", "textures", "HIDDEN.png"), TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 1, 2, 3, 255)));
            File.WriteAllBytes(Path.Combine(dir, "textures", "SKIP.ignore"), TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 4, 5, 6, 255)));
            var config = GameConfiguration.FromText("""
                ignoreddirectories = ".git";
                ignoredextensions = "ignore";
                """);

            using var rm = new ResourceManager(config);
            rm.AddResource(dir);

            Assert.Null(rm.GetWallTexture("HIDDEN"));
            Assert.Null(rm.GetTextResource(".git/HIDDEN.txt"));
            Assert.Null(rm.GetWallTexture("SKIP"));
            Assert.NotNull(rm.GetWallTexture("DWALL"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void DirectoryResourceSkipsInvalidPathCharactersLikeUdb()
    {
        string dir = BuildResourceDir();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "textures", "BAD<.png"), TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 4, 5, 6, 255)));

            using var rm = new ResourceManager();
            rm.AddResource(dir);

            Assert.Null(rm.GetWallTexture("BAD<"));
            Assert.NotNull(rm.GetWallTexture("DWALL"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
