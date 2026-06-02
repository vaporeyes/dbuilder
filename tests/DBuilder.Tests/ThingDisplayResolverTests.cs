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
                    SurfaceSkin 0 2 "lamp_alt.png"
                    Scale 1.5 2.5 3.5
                    Offset 4 5 6
                    AngleOffset 45
                    PitchOffset 10
                    RollOffset 15
                    Rotation-Center 7 8 9
                    UseActorPitch
                    UseActorRoll
                    Userotationcenter
                    FrameIndex LAMP A 0 0
                }
                """)),
            ("models/lamp.md3", [1, 2, 3]),
            ("models/lamp.png", [4, 5, 6]),
            ("models/lamp_alt.png", [7, 8, 9]));

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
            Assert.NotNull(source.ModelDisplay);
            ThingModelDisplay display = source.ModelDisplay!;
            Assert.Equal("models", display.Path);
            Assert.Equal(new ModeldefVector(2.5f, 1.5f, 3.5f), display.Scale);
            Assert.Equal(new ModeldefVector(4.0f, 5.0f, 6.0f), display.Offset);
            Assert.Equal(new ModeldefVector(7.0f, 8.0f, 9.0f), display.RotationCenter);
            Assert.Equal(45.0f, display.AngleOffset);
            Assert.Equal(10.0f, display.PitchOffset);
            Assert.Equal(15.0f, display.RollOffset);
            Assert.False(display.InheritActorPitch);
            Assert.True(display.UseActorPitch);
            Assert.True(display.UseActorRoll);
            Assert.True(display.UseRotationCenter);
            ThingModelDisplayPart part = Assert.Single(display.Parts);
            Assert.Equal("models/lamp.md3", part.ModelName);
            Assert.Equal("models/lamp.png", part.SkinName);
            Assert.Equal("", part.FrameName);
            Assert.Equal(0, part.FrameIndex);
            Assert.Equal("models/lamp_alt.png", Assert.Single(part.SurfaceSkinNames).Value);
            Assert.Empty(part.EffectiveSurfaceSkinNames);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void SurfaceSkinsRemainEffectiveWhenSkinIsNotDefined()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("MODELDEF.txt", Encoding.ASCII.GetBytes("""
                model LampActor
                {
                    Path "models"
                    Model 0 "lamp.md3"
                    SurfaceSkin 0 2 "lamp_alt.png"
                    FrameIndex LAMP A 0 0
                }
                """)),
            ("models/lamp.md3", [1, 2, 3]),
            ("models/lamp_alt.png", [7, 8, 9]));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);
            var info = new ThingTypeInfo { ClassName = "LampActor", Sprite = "LAMPA0" };

            ThingDisplaySource source = ThingDisplayResolver.Resolve(info, resources);

            ThingModelDisplayPart part = Assert.Single(source.ModelDisplay!.Parts);
            Assert.Equal("", part.SkinName);
            Assert.Equal("models/lamp_alt.png", Assert.Single(part.SurfaceSkinNames).Value);
            Assert.Equal("models/lamp_alt.png", Assert.Single(part.EffectiveSurfaceSkinNames).Value);
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
    public void ModeldefWithoutMatchingSpriteFrameFallsBackToSpriteDisplay()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("MODELDEF.txt", Encoding.ASCII.GetBytes("""
                model LampActor
                {
                    Path "models"
                    Model 0 "lamp.md3"
                    FrameIndex OTHR A 0 0
                }
                """)),
            ("models/lamp.md3", [1, 2, 3]),
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
    public void DisabledModelFrameFallsBackToSpriteDisplay()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("MODELDEF.txt", Encoding.ASCII.GetBytes("""
                model LampActor
                {
                    Path "models"
                    Model 0 "lamp.md3"
                    FrameIndex LAMP A 0 -1
                }
                """)),
            ("models/lamp.md3", [1, 2, 3]),
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
    public void NamedModelFrameResolvesToModelDisplay()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("MODELDEF.txt", Encoding.ASCII.GetBytes("""
                model LampActor
                {
                    Path "models"
                    Model 0 "lamp.md3"
                    Frame LAMP A 0 "Idle"
                }
                """)),
            ("models/lamp.md3", [1, 2, 3]));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);
            var info = new ThingTypeInfo { ClassName = "LampActor", Sprite = "LAMPA0" };

            ThingDisplaySource source = ThingDisplayResolver.Resolve(info, resources);

            Assert.Equal(ThingDisplayKind.Model, source.Kind);
            Assert.NotNull(source.Model);
            ModeldefFrame frame = Assert.Single(source.Model!.Frames);
            Assert.Equal("Idle", frame.ModelFrame);
            ThingModelDisplayPart part = Assert.Single(source.ModelDisplay!.Parts);
            Assert.Equal("Idle", part.FrameName);
            Assert.Equal(0, part.FrameIndex);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void MissingReferencedModelIndexFallsBackToSpriteDisplay()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("MODELDEF.txt", Encoding.ASCII.GetBytes("""
                model LampActor
                {
                    Path "models"
                    Model 0 "lamp.md3"
                    FrameIndex LAMP A 1 0
                }
                """)),
            ("models/lamp.md3", [1, 2, 3]),
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
