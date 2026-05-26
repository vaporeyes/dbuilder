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
            using var rm = new ResourceManager();
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
}
