// ABOUTME: Verifies UDB-style thing display source resolution across MODELDEF, VOXELDEF, sprite, and marker fallbacks.
// ABOUTME: Uses synthetic resources so model and voxel precedence is tested through ResourceManager.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class ThingDisplayResolverTests
{
    [Fact]
    public void ModeldefActorClassResolvesToModelDisplay()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("MODELDEF.txt", Encoding.ASCII.GetBytes("""
                model LampActor
                {
                    Path "models"
                    Model 0 "lamp.md3"
                    Skin 0 "lamp.png"
                    FrameIndex LAMP A 0 0
                }
                """)),
            ("models/lamp.md3", [1, 2, 3]),
            ("models/lamp.png", [4, 5, 6]));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);
            var info = new ThingTypeInfo { ClassName = "LampActor", Sprite = "LAMPA0" };

            ThingDisplaySource source = ThingDisplayResolver.Resolve(info, resources);

            Assert.Equal(ThingDisplayKind.Model, source.Kind);
            Assert.NotNull(source.Model);
            Assert.Equal("LampActor", source.Model!.ActorName);
            Assert.Equal("LAMPA0", source.SpriteName);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void MissingModelBytesFallBackToSpriteDisplay()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("MODELDEF.txt", Encoding.ASCII.GetBytes("""
                model LampActor
                {
                    Path "models"
                    Model 0 "missing.md3"
                    FrameIndex LAMP A 0 0
                }
                """)),
            ("sprites/LAMPA0.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 20, 30, 40, 255))));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);
            var info = new ThingTypeInfo { ClassName = "LampActor", Sprite = "LAMPA0" };

            ThingDisplaySource source = ThingDisplayResolver.Resolve(info, resources);

            Assert.Equal(ThingDisplayKind.Sprite, source.Kind);
            Assert.Equal("LAMPA0", source.SpriteName);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void VoxeldefSpriteMappingResolvesToVoxelDisplay()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("VOXELDEF.txt", Encoding.ASCII.GetBytes("BAR1 = models/barrel.kvx { scale = 1.5 }\n")),
            ("models/BARREL.kvx", [9, 10, 11, 12]));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);
            var info = new ThingTypeInfo { ClassName = "BarrelActor", Sprite = "BAR1A0" };

            ThingDisplaySource source = ThingDisplayResolver.Resolve(info, resources);

            Assert.Equal(ThingDisplayKind.Voxel, source.Kind);
            Assert.Equal("BAR1A0", source.SpriteName);
            Assert.Equal("MODELS/BARREL.KVX", source.VoxelModelName);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void DirectVoxelNameResolvesToVoxelDisplay()
    {
        string pk3 = TestArtifacts.BuildPk3(("voxels/BAR1.kvx", [13, 14, 15, 16]));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);
            var info = new ThingTypeInfo { Sprite = "BAR1A0" };

            ThingDisplaySource source = ThingDisplayResolver.Resolve(info, resources);

            Assert.Equal(ThingDisplayKind.Voxel, source.Kind);
            Assert.Equal("BAR1", source.VoxelModelName);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void MissingResourcesFallBackToMarkerDisplay()
    {
        using var resources = new ResourceManager();
        var info = new ThingTypeInfo { ClassName = "UnknownActor", Sprite = "NOPEA0" };

        ThingDisplaySource source = ThingDisplayResolver.Resolve(info, resources);

        Assert.Equal(ThingDisplayKind.Marker, source.Kind);
    }
}
