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
        string pk3 = TestArtifacts.BuildPk3(
            ("MODELDEF.txt", Encoding.ASCII.GetBytes("""
model Zombie
{
    Path "models/monsters"
    Model 0 "zombie.md3"
    Skin 0 "zombie.png"
    FrameIndex POSS A 0 12
}
""")),
            ("models/monsters/zombie.md3", model),
            ("models/monsters/zombie.png", skin));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            var def = Assert.Single(resources.GetModelDefs());
            Assert.Equal("Zombie", def.ActorName);
            Assert.Equal("models/monsters/zombie.md3", ResourceManager.CombineModelPath(def.Path, def.Models[0].File));
            Assert.Equal(model, resources.GetModelResourceBytes(def, def.Models[0].File));
            Assert.Equal(skin, resources.GetModelResourceBytes(def, def.Skins[0].File));
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
}
