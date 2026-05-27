// ABOUTME: Tests ResourceManager aggregation of GLDEFS data from resource files.
// ABOUTME: Uses synthetic PK3 resources to verify same-resource include resolution and override order.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class GldefsResourceTests
{
    [Fact]
    public void ResourceManagerParsesGldefsIncludesFromPk3()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("GLDEFS.txt", Encoding.ASCII.GetBytes("""
#include "lights/defs.txt"
object LampActor { frame LAMP { light LAMP_LIGHT } }
""")),
            ("lights/defs.txt", Encoding.ASCII.GetBytes("pointlight LAMP_LIGHT { color 0.2 0.4 0.6 size 48 }")));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            var gldefs = resources.GetGldefs();
            Assert.True(gldefs.Lights.ContainsKey("LAMP_LIGHT"));
            Assert.Equal(0.4f, gldefs.ActorLightColor("LampActor")!.Value.G, 4);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void LaterGldefsResourcesOverrideLights()
    {
        string oldPk3 = TestArtifacts.BuildPk3(("GLDEFS.txt", Encoding.ASCII.GetBytes("pointlight SHARED { color 0.1 0.1 0.1 size 16 }")));
        string newPk3 = TestArtifacts.BuildPk3(("GLDEFS.txt", Encoding.ASCII.GetBytes("pointlight SHARED { color 0.8 0.7 0.6 size 64 }")));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(oldPk3);
            resources.AddResource(newPk3);

            Assert.Equal(0.8f, resources.GetGldefs().Lights["SHARED"].R, 4);
            Assert.Equal(128f, resources.GetGldefs().Lights["SHARED"].Size, 4);
        }
        finally
        {
            File.Delete(oldPk3);
            File.Delete(newPk3);
        }
    }

    [Fact]
    public void ResourceManagerParsesMultipleGldefsFilesFromPk3()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("GLDEFS.txt", Encoding.ASCII.GetBytes("pointlight ROOT_LIGHT { color 0.1 0.2 0.3 size 16 }")),
            ("GLDEFS.extra", Encoding.ASCII.GetBytes("pointlight EXTRA_LIGHT { color 0.4 0.5 0.6 size 32 }")));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            var gldefs = resources.GetGldefs();
            Assert.True(gldefs.Lights.ContainsKey("ROOT_LIGHT"));
            Assert.True(gldefs.Lights.ContainsKey("EXTRA_LIGHT"));
        }
        finally
        {
            File.Delete(pk3);
        }
    }
}
