// ABOUTME: Verifies loaded GZDoom model mesh render-batch planning.
// ABOUTME: Composes loaded model meshes with MODELDEF/thing world transforms before live GL drawing exists.

using System.Numerics;
using DBuilder.IO;
using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class GzModelRenderPlannerTests
{
    [Fact]
    public void PlansOneBatchPerDrawableMeshWithTextureAndTint()
    {
        GzModelMesh first = Mesh(new WorldVertex { x = 0, y = 0, z = 0 }, new WorldVertex { x = 1, y = 0, z = 0 }, new WorldVertex { x = 0, y = 1, z = 0 });
        GzModelMesh second = Mesh(new WorldVertex { x = 0, y = 0, z = 1 }, new WorldVertex { x = 1, y = 0, z = 1 }, new WorldVertex { x = 0, y = 1, z = 1 });
        var loaded = new GzLoadedModel(
            new[] { first, second },
            new string?[] { "skins/first.png", "skins/second.png" },
            Array.Empty<string>(),
            new GzModelBounds(0, 0, 0, 1, 1, 1),
            Radius: 32);
        Matrix4x4 world = Matrix4x4.CreateTranslation(8, 16, 24);

        IReadOnlyList<GzModelRenderBatch> batches = GzModelRenderPlanner.Plan(loaded, world, unchecked((int)0xfffff080));

        Assert.Equal(2, batches.Count);
        Assert.Same(first, batches[0].Mesh);
        Assert.Equal("skins/first.png", batches[0].TexturePath);
        Assert.Equal(world, batches[0].World);
        Assert.Equal(unchecked((int)0xfffff080), batches[0].TintArgb);
        Assert.Equal(1, batches[0].TriangleCount);
        Assert.Same(second, batches[1].Mesh);
        Assert.Equal("skins/second.png", batches[1].TexturePath);
    }

    [Fact]
    public void MissingTextureEntriesRemainNullPerMesh()
    {
        GzModelMesh first = Mesh(new WorldVertex(), new WorldVertex(), new WorldVertex());
        GzModelMesh second = Mesh(new WorldVertex(), new WorldVertex(), new WorldVertex());
        var loaded = new GzLoadedModel(
            new[] { first, second },
            new string?[] { "skins/first.png" },
            Array.Empty<string>(),
            GzModelBounds.Empty,
            Radius: 0);

        IReadOnlyList<GzModelRenderBatch> batches = GzModelRenderPlanner.Plan(loaded, Matrix4x4.Identity, unchecked((int)0xffffffff));

        Assert.Equal("skins/first.png", batches[0].TexturePath);
        Assert.Null(batches[1].TexturePath);
    }

    [Fact]
    public void SkipsMeshesWithoutDrawableTriangles()
    {
        var empty = new GzModelMesh(Array.Empty<WorldVertex>(), Array.Empty<int>());
        GzModelMesh drawable = Mesh(new WorldVertex(), new WorldVertex(), new WorldVertex());
        var loaded = new GzLoadedModel(
            new[] { empty, drawable },
            new string?[] { "empty.png", "drawable.png" },
            Array.Empty<string>(),
            GzModelBounds.Empty,
            Radius: 0);

        IReadOnlyList<GzModelRenderBatch> batches = GzModelRenderPlanner.Plan(loaded, Matrix4x4.Identity, unchecked((int)0xffffffff));

        GzModelRenderBatch batch = Assert.Single(batches);
        Assert.Same(drawable, batch.Mesh);
        Assert.Equal("drawable.png", batch.TexturePath);
    }

    [Fact]
    public void UsesThingModelWorldTransformForBatches()
    {
        ThingModelDisplay display = new(
            new Modeldef(),
            "",
            new ModeldefVector(2.0f, 2.0f, 2.0f),
            new ModeldefVector(0.0f, 0.0f, 0.0f),
            new ModeldefVector(0.0f, 0.0f, 0.0f),
            AngleOffset: 0.0f,
            PitchOffset: 0.0f,
            RollOffset: 0.0f,
            InheritActorPitch: false,
            UseActorPitch: false,
            UseActorRoll: false,
            UseRotationCenter: false,
            Array.Empty<ThingModelDisplayPart>());
        ThingModelRenderPlan thingPlan = ThingModelRenderPlanner.Plan3D(display, new ThingModelRenderInput(PositionX: 10.0, PositionY: 20.0, PositionZ: 30.0));
        GzModelMesh mesh = Mesh(new WorldVertex { x = 1, y = 0, z = 0 }, new WorldVertex { x = 0, y = 1, z = 0 }, new WorldVertex { x = 0, y = 0, z = 1 });
        var loaded = new GzLoadedModel(new[] { mesh }, new string?[] { null }, Array.Empty<string>(), GzModelBounds.Empty, Radius: 0);

        GzModelRenderBatch batch = Assert.Single(GzModelRenderPlanner.Plan(loaded, thingPlan.World3D, unchecked((int)0xffffffff)));
        Vector3 transformed = Vector3.Transform(new Vector3(1, 0, 0), batch.World);

        Assert.Equal(12.0f, transformed.X, precision: 5);
        Assert.Equal(20.0f, transformed.Y, precision: 5);
        Assert.Equal(30.0f, transformed.Z, precision: 5);
    }

    private static GzModelMesh Mesh(params WorldVertex[] vertices)
        => new(vertices, new[] { 0, 1, 2 });
}
