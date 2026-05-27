// ABOUTME: Tests ResourceManager discovery and byte lookup for MODELDEF model resources.
// ABOUTME: Uses synthetic PK3 data to verify path handling and newest-resource priority.

using System.IO;
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
}
