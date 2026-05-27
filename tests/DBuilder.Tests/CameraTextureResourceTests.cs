// ABOUTME: Tests ResourceManager exposure of ANIMDEFS camera textures as virtual images.
// ABOUTME: Uses synthetic PK3 fixtures to verify names, dimensions and override behavior.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class CameraTextureResourceTests
{
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
