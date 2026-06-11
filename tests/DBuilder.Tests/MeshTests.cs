// ABOUTME: Verifies UDB-style mesh construction, draw, and disposal operation planning.
// ABOUTME: Keeps indexed-triangle mesh behavior covered without requiring a live GL context.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class MeshTests
{
    [Fact]
    public void ConstructionPlanUploadsWorldVerticesAndIndices()
    {
        var vertices = new[]
        {
            new WorldVertex(),
            new WorldVertex(),
            new WorldVertex(),
            new WorldVertex(),
        };
        int[] indices = { 0, 1, 2, 0, 2, 3 };

        MeshConstructionPlan plan = Mesh.BuildConstructionPlan(vertices, indices);

        Assert.Equal(4, plan.VertexCount);
        Assert.Equal(6, plan.IndexCount);
        Assert.Equal(2, plan.PrimitiveCount);
        Assert.Equal(new[] { MeshBufferKind.Vertex, MeshBufferKind.Index }, plan.Uploads);
    }

    [Fact]
    public void DrawPlanMatchesUdbIndexedTriangleSequence()
    {
        IReadOnlyList<MeshDrawStep> plan = Mesh.BuildDrawPlan(primitiveCount: 2);

        Assert.Equal(
            new[]
            {
                new MeshDrawStep(MeshDrawStepKind.BindVertexBuffer),
                new MeshDrawStep(MeshDrawStepKind.BindIndexBuffer),
                new MeshDrawStep(MeshDrawStepKind.DrawIndexed, PrimitiveType.TriangleList, StartIndex: 0, PrimitiveCount: 2),
                new MeshDrawStep(MeshDrawStepKind.UnbindIndexBuffer),
                new MeshDrawStep(MeshDrawStepKind.UnbindVertexBuffer),
            },
            plan);
    }

    [Fact]
    public void DisposePlanReleasesOwnedBuffersInUdbOrder()
    {
        MeshDisposePlan plan = Mesh.BuildDisposePlan();

        Assert.Equal(
            new[] { MeshDisposeStepKind.DisposeVertexBuffer, MeshDisposeStepKind.DisposeIndexBuffer },
            plan.Steps);
    }

    [Fact]
    public void MeshExposesDisposedState()
    {
        Assert.NotNull(typeof(Mesh).GetProperty(nameof(Mesh.Disposed)));
        Assert.Equal(typeof(bool), typeof(Mesh).GetProperty(nameof(Mesh.Disposed))!.PropertyType);
        Assert.Null(typeof(Mesh).GetProperty(nameof(Mesh.Disposed))!.SetMethod);
    }

    [Fact]
    public void PrimitiveCountUsesCompleteIndexedTrianglesOnly()
    {
        Assert.Equal(0, Mesh.PrimitiveCountFor(0));
        Assert.Equal(1, Mesh.PrimitiveCountFor(3));
        Assert.Equal(1, Mesh.PrimitiveCountFor(5));
        Assert.Equal(2, Mesh.PrimitiveCountFor(6));
    }

    [Fact]
    public void MeshPlanningRejectsNegativeCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Mesh.PrimitiveCountFor(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Mesh.BuildDrawPlan(primitiveCount: -1));
    }
}
