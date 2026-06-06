// ABOUTME: Tests ResourceManager discovery and byte lookup for MODELDEF model resources.
// ABOUTME: Uses synthetic PK3 data to verify path handling and newest-resource priority.

using System.IO;
using System.Linq;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class ModelResourceTests
{
    [Fact]
    public void DiscoversModeldefsAndResolvesModelAndSkinBytes()
    {
        byte[] model = { 1, 2, 3 };
        byte[] skin = { 4, 5, 6 };
        byte[] surfaceSkin = { 7, 8, 9 };
        string pk3 = TestArtifacts.BuildPk3(
            ("MODELDEF.txt", Encoding.ASCII.GetBytes("""
model Zombie
{
    Path "models/monsters"
    Model 0 "zombie.md3"
    Skin 0 "zombie.png"
    SurfaceSkin 0 2 "zombie_alt.png"
    FrameIndex POSS A 0 12
}
""")),
            ("models/monsters/zombie.md3", model),
            ("models/monsters/zombie.png", skin),
            ("models/monsters/zombie_alt.png", surfaceSkin));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            var def = Assert.Single(resources.GetModelDefs());
            Assert.Equal("Zombie", def.ActorName);
            Assert.Equal("models/monsters/zombie.md3", ResourceManager.CombineModelPath(def.Path, def.Models[0].File));
            Assert.Equal(model, resources.GetModelResourceBytes(def, def.Models[0].File));
            Assert.Equal(skin, resources.GetModelResourceBytes(def, def.Skins[0].File));
            Assert.Equal(surfaceSkin, resources.GetModelResourceBytes(def, def.SurfaceSkins[0].File));
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void LaterResourcesOverrideModelBytes()
    {
        byte[] oldModel = { 1 };
        byte[] newModel = { 2 };
        string oldPk3 = TestArtifacts.BuildPk3(("models/shared.md3", oldModel));
        string newPk3 = TestArtifacts.BuildPk3(("models/shared.md3", newModel));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(oldPk3);
            resources.AddResource(newPk3);

            Assert.Equal(newModel, resources.GetModelResourceBytes("models/shared.md3"));
        }
        finally
        {
            File.Delete(oldPk3);
            File.Delete(newPk3);
        }
    }

    [Fact]
    public void ModelTextureLookupProbesSupportedExtensionsLikeUdb()
    {
        byte[] pngSkin = { 10 };
        byte[] jpgSkin = { 11 };
        string pk3 = TestArtifacts.BuildPk3(
            ("models/zombie.png", pngSkin),
            ("models/soldier.jpg", jpgSkin));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            Assert.Equal(pngSkin, resources.GetModelTextureResourceBytes("models/zombie"));
            Assert.Equal(jpgSkin, resources.GetModelTextureResourceBytes("models/soldier.tga"));
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void ModelTextureLookupPrefersExactPathAndNewestResource()
    {
        byte[] exactSkin = { 20 };
        byte[] oldSkin = { 21 };
        byte[] newSkin = { 22 };
        string exactPk3 = TestArtifacts.BuildPk3(
            ("models/skin.dds", exactSkin),
            ("models/skin.png", oldSkin));
        string overridePk3 = TestArtifacts.BuildPk3(("models/other.png", newSkin));
        string oldOverridePk3 = TestArtifacts.BuildPk3(("models/other.png", oldSkin));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(exactPk3);
            resources.AddResource(oldOverridePk3);
            resources.AddResource(overridePk3);

            Assert.Equal(exactSkin, resources.GetModelTextureResourceBytes("models/skin.dds"));
            Assert.Equal(newSkin, resources.GetModelTextureResourceBytes("models/other"));
        }
        finally
        {
            File.Delete(exactPk3);
            File.Delete(overridePk3);
            File.Delete(oldOverridePk3);
        }
    }

    [Fact]
    public void ModelTextureImageLookupUsesDefinedTextureExtensionsLikeUdb()
    {
        byte[] texture = TestArtifacts.Png(2, 3, TestArtifacts.SolidRgba(2, 3, 60, 70, 80, 255));
        string pk3 = TestArtifacts.BuildPk3(("textures/skin.png", texture));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            ImageData? image = resources.GetModelTextureImage("skin.tga");

            Assert.NotNull(image);
            Assert.Equal(2, image!.Width);
            Assert.Equal(3, image.Height);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void ModelTextureImageLookupFallsBackToBasenameTextureLikeUdb()
    {
        byte[] texture = TestArtifacts.Png(4, 5, TestArtifacts.SolidRgba(4, 5, 90, 100, 110, 255));
        string pk3 = TestArtifacts.BuildPk3(("textures/skin.png", texture));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            ImageData? image = resources.GetModelTextureImage("models/skin.missing");

            Assert.NotNull(image);
            Assert.Equal(4, image!.Width);
            Assert.Equal(5, image.Height);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void ModelTextureImageLookupFallsBackToSpriteLikeUdb()
    {
        byte[] texture = TestArtifacts.Png(6, 7, TestArtifacts.SolidRgba(6, 7, 120, 130, 140, 255));
        string pk3 = TestArtifacts.BuildPk3(("sprites/POSSA0.png", texture));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            ImageData? image = resources.GetModelTextureImage("POSSA0.png");

            Assert.NotNull(image);
            Assert.Equal(6, image!.Width);
            Assert.Equal(7, image.Height);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void ModelTextureImageLookupFallsBackToBasenameSpriteLikeUdb()
    {
        byte[] texture = TestArtifacts.Png(8, 9, TestArtifacts.SolidRgba(8, 9, 140, 150, 160, 255));
        string pk3 = TestArtifacts.BuildPk3(("sprites/POSSA0.png", texture));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            ImageData? image = resources.GetModelTextureImage("models/monsters/POSSA0.png");

            Assert.NotNull(image);
            Assert.Equal(8, image!.Width);
            Assert.Equal(9, image.Height);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void DiscoversMultipleRootModeldefFiles()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("MODELDEF.txt", Encoding.ASCII.GetBytes("model Root { Model 0 \"root.md3\" }")),
            ("MODELDEF.extra", Encoding.ASCII.GetBytes("model Extra { Model 0 \"extra.md3\" }")));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            Assert.Equal(new[] { "Extra", "Root" }, resources.GetModelDefs().Select(d => d.ActorName).OrderBy(n => n).ToArray());
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void ResolvesModeldefIncludesFromSamePk3()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("MODELDEF.txt", Encoding.ASCII.GetBytes("""
#include "models/defs.txt"
model Root { Model 0 "root.md3" }
""")),
            ("models/defs.txt", Encoding.ASCII.GetBytes("model Included { Model 0 \"included.md3\" }")));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            Assert.Equal(new[] { "Included", "Root" }, resources.GetModelDefs().Select(d => d.ActorName).OrderBy(n => n).ToArray());
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void WadModeldefIncludesUseLastMatchingLumpLikeUdb()
    {
        string wad = TestArtifacts.BuildPwadFile(
            ("MODELDEF", Encoding.ASCII.GetBytes("""
#include "MODELINC"
model Root { Model 0 "root.md3" }
""")),
            ("MODELINC", Encoding.ASCII.GetBytes("model OldIncluded { Model 0 \"old.md3\" }")),
            ("MODELINC", Encoding.ASCII.GetBytes("model NewIncluded { Model 0 \"new.md3\" }")));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(wad);

            Assert.Equal(new[] { "NewIncluded", "Root" }, resources.GetModelDefs().Select(d => d.ActorName).OrderBy(n => n).ToArray());
        }
        finally
        {
            File.Delete(wad);
        }
    }

    [Fact]
    public void WadModelResourcesResolveByModeldefBasename()
    {
        byte[] oldModel = { 1, 2, 3 };
        byte[] model = { 4, 5, 6 };
        byte[] skin = { 7, 8, 9 };
        string wad = TestArtifacts.BuildPwadFile(
            ("MODELDEF", Encoding.ASCII.GetBytes("""
model Zombie
{
    Path "models/monsters"
    Model 0 "zombie.md3"
    Skin 0 "zombie.png"
}
""")),
            ("ZOMBIE", oldModel),
            ("ZOMBIE", model),
            ("ZOMBSKIN", skin));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(wad);

            var def = Assert.Single(resources.GetModelDefs());
            Assert.Equal(model, resources.GetModelResourceBytes(def, def.Models[0].File));
            Assert.Equal(skin, resources.GetModelResourceBytes("models/monsters/zombskin.png"));
        }
        finally
        {
            File.Delete(wad);
        }
    }

    [Fact]
    public void WadModelTextureLookupProbesBasenameExtensionsLikeUdb()
    {
        byte[] skin = { 11, 12, 13 };
        string wad = TestArtifacts.BuildPwadFile(("SOLDIER", skin));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(wad);

            Assert.Equal(skin, resources.GetModelTextureResourceBytes("models/soldier.tga"));
        }
        finally
        {
            File.Delete(wad);
        }
    }
}
