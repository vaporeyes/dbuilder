// ABOUTME: Tests ResourceManager exposure of ANIMDEFS camera textures as virtual images.
// ABOUTME: Uses synthetic PK3 fixtures to verify names, dimensions and override behavior.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class CameraTextureResourceTests
{
    private static byte[] CameraTextureBytes(string name, int width, int height)
        => Encoding.ASCII.GetBytes($"cameratexture {name} {width.ToString(System.Globalization.CultureInfo.InvariantCulture)} {height.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n");

    [Fact]
    public void CameraTexturesResolveAsWallTexturesAndFlats()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("ANIMDEFS.txt", Encoding.ASCII.GetBytes("cameratexture CAMVIEW 96 48 worldpanning\n")));
        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            Assert.Contains("CAMVIEW", resources.GetTextureNames());
            Assert.Contains("CAMVIEW", resources.GetFlatNames());
            var set = Assert.Single(resources.GetResourceTextureSets());
            Assert.True(set.TextureExists("CAMVIEW"));
            Assert.True(set.FlatExists("CAMVIEW"));

            var wall = resources.GetWallTexture("CAMVIEW");
            var flat = resources.GetFlat("CAMVIEW");
            Assert.NotNull(wall);
            Assert.NotNull(flat);
            Assert.Equal(96, wall!.Width);
            Assert.Equal(48, wall.Height);
            Assert.Equal(96, flat!.Width);
            Assert.Equal(48, flat.Height);
            Assert.Equal(255, wall.Rgba[3]);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void ResolvesAnimdefsCameraTexturesFromWadLumps()
    {
        string wad = TestArtifacts.BuildPwadFile(
            ("ANIMDEFS", CameraTextureBytes("CAMONE", 64, 32)),
            ("ANIMDEFS", CameraTextureBytes("CAMTWO", 128, 48)));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(wad);

            Assert.Equal(64, resources.GetWallTexture("CAMONE")!.Width);
            Assert.Equal(32, resources.GetWallTexture("CAMONE")!.Height);
            Assert.Equal(128, resources.GetWallTexture("CAMTWO")!.Width);
            Assert.Equal(48, resources.GetWallTexture("CAMTWO")!.Height);
        }
        finally
        {
            File.Delete(wad);
        }
    }

    [Fact]
    public void FolderResourcesResolveRootAnimdefsTitleFilesThenNestedWadsLikeUdb()
    {
        string nestedWad = TestArtifacts.BuildPwadFile(("ANIMDEFS", CameraTextureBytes("CAMVIEW", 96, 32)));
        string pk3 = TestArtifacts.BuildPk3(
            ("ANIMDEFS.txt", CameraTextureBytes("CAMVIEW", 64, 32)),
            ("ANIMDEFS.extra", CameraTextureBytes("CAMEXTRA", 40, 20)),
            ("nested.wad", File.ReadAllBytes(nestedWad)));
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_animdefs_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "ANIMDEFS.txt"), CameraTextureBytes("CAMVIEW", 128, 64));

            using var resources = new ResourceManager();
            resources.AddResource(pk3);
            resources.AddResource(dir);

            Assert.Equal(128, resources.GetWallTexture("CAMVIEW")!.Width);
            Assert.Equal(64, resources.GetWallTexture("CAMVIEW")!.Height);
            Assert.Equal(40, resources.GetWallTexture("CAMEXTRA")!.Width);
            Assert.Equal(20, resources.GetWallTexture("CAMEXTRA")!.Height);
        }
        finally
        {
            File.Delete(nestedWad);
            File.Delete(pk3);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CameraTextureOverridesSameNamedResourceImages()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("ANIMDEFS.txt", Encoding.ASCII.GetBytes("cameratexture CAMVIEW 80 40\n")),
            ("textures/CAMVIEW.png", TestArtifacts.Png(8, 8, TestArtifacts.SolidRgba(8, 8, 1, 2, 3, 255))),
            ("flats/CAMVIEW.png", TestArtifacts.Png(16, 16, TestArtifacts.SolidRgba(16, 16, 4, 5, 6, 255))));
        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            var wall = resources.GetWallTexture("CAMVIEW");
            var flat = resources.GetFlat("CAMVIEW");
            Assert.NotNull(wall);
            Assert.NotNull(flat);
            Assert.Equal(80, wall!.Width);
            Assert.Equal(40, wall.Height);
            Assert.Equal(80, flat!.Width);
            Assert.Equal(40, flat.Height);
        }
        finally
        {
            File.Delete(pk3);
        }
    }
}
