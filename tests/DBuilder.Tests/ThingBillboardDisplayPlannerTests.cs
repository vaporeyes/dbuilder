// ABOUTME: Tests billboard fallback planning for sprite, voxel, and MODELDEF thing display sources.
// ABOUTME: Verifies the planner resolves the image data used by editor 2D and 3D thing billboards.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class ThingBillboardDisplayPlannerTests
{
    [Fact]
    public void PlansSpriteBillboardFromConfiguredSprite()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("sprites/POSSA0.png", TestArtifacts.Png(2, 3, TestArtifacts.SolidRgba(2, 3, 20, 30, 40, 255))));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);
            var info = new ThingTypeInfo { Sprite = "POSSA0" };

            ThingBillboardDisplay? display = ThingBillboardDisplayPlanner.Plan(info, resources);

            Assert.NotNull(display);
            Assert.Equal(ThingDisplayKind.Sprite, display!.Kind);
            Assert.Equal("POSSA0", display.SpriteName);
            Assert.Equal(2, display.Image.Width);
            Assert.Equal(3, display.Image.Height);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void PlansVoxelBillboardUsingVoxelPlaceholderImage()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("VOXELDEF.txt", Encoding.ASCII.GetBytes("BAR1 = models/barrel.kvx { scale = 1.5 }\n")),
            ("models/BARREL.kvx", [9, 10, 11, 12]));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);
            var info = new ThingTypeInfo { Sprite = "BAR1A0" };

            ThingBillboardDisplay? display = ThingBillboardDisplayPlanner.Plan(info, resources);

            Assert.NotNull(display);
            Assert.Equal(ThingDisplayKind.Voxel, display!.Kind);
            Assert.Equal("BAR1A0", display.SpriteName);
            Assert.Equal(64, display.Image.Width);
            Assert.Equal(64, display.Image.Height);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void PlansModelBillboardFromResolvedModelSpriteFallback()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("MODELDEF.txt", Encoding.ASCII.GetBytes("""
                model LampActor
                {
                    Path "models"
                    Model 0 "lamp.md3"
                    FrameIndex LAMP A 0 0
                }
                """)),
            ("models/lamp.md3", [1, 2, 3]),
            ("sprites/LAMPA0.png", TestArtifacts.Png(4, 5, TestArtifacts.SolidRgba(4, 5, 50, 60, 70, 255))));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);
            var info = new ThingTypeInfo { ClassName = "LampActor", Sprite = "LAMPA0" };

            ThingBillboardDisplay? display = ThingBillboardDisplayPlanner.Plan(info, resources);

            Assert.NotNull(display);
            Assert.Equal(ThingDisplayKind.Model, display!.Kind);
            Assert.Equal("LAMPA0", display.SpriteName);
            Assert.Equal(4, display.Image.Width);
            Assert.Equal(5, display.Image.Height);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void ModelRenderModeDemotesModelBillboardToSpriteFallback()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("MODELDEF.txt", Encoding.ASCII.GetBytes("""
                model LampActor
                {
                    Path "models"
                    Model 0 "lamp.md3"
                    FrameIndex LAMP A 0 0
                }
                """)),
            ("models/lamp.md3", [1, 2, 3]),
            ("sprites/LAMPA0.png", TestArtifacts.Png(4, 5, TestArtifacts.SolidRgba(4, 5, 50, 60, 70, 255))));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);
            var info = new ThingTypeInfo { ClassName = "LampActor", Sprite = "LAMPA0" };

            ThingBillboardDisplay? display = ThingBillboardDisplayPlanner.Plan(
                info,
                resources,
                ThingModelRenderMode.Selection,
                new ThingModelRenderInput(Selected: false),
                visual3D: true);

            Assert.NotNull(display);
            Assert.Equal(ThingDisplayKind.Sprite, display!.Kind);
            Assert.Equal("LAMPA0", display.SpriteName);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void MissingThingOrResourceReturnsNoBillboard()
    {
        using var resources = new ResourceManager();

        Assert.Null(ThingBillboardDisplayPlanner.Plan(null, resources));
        Assert.Null(ThingBillboardDisplayPlanner.Plan(new ThingTypeInfo { Sprite = "NOPEA0" }, resources));
        Assert.Null(ThingBillboardDisplayPlanner.Plan(new ThingTypeInfo { Sprite = "NOPEA0" }, null));
    }
}
