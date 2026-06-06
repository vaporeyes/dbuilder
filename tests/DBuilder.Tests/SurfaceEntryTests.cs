// ABOUTME: Verifies UDB-style sector surface entry data used by the renderer surface manager.
// ABOUTME: Covers chunk metadata, update allocation, copy semantics, and floor-vertex bounds.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class SurfaceEntryTests
{
    [Fact]
    public void ConstructorStoresBufferChunkMetadata()
    {
        var entry = new SurfaceEntry(numVertices: 6, bufferIndex: 2, vertexOffset: 18);

        Assert.Equal(6, entry.NumVertices);
        Assert.Equal(2, entry.BufferIndex);
        Assert.Equal(18, entry.VertexOffset);
    }

    [Fact]
    public void CopyConstructorCopiesChunkLocationWithoutVertexData()
    {
        var source = new SurfaceEntry(numVertices: 3, bufferIndex: 1, vertexOffset: 9)
        {
            FloorVertices = new[] { Vertex(1, 2) },
            CeilingVertices = new[] { Vertex(3, 4) },
            FloorTexture = 100,
            CeilingTexture = 200,
            Hidden = true,
            Desaturation = 0.5,
        };

        var copy = new SurfaceEntry(source);

        Assert.Equal(source.NumVertices, copy.NumVertices);
        Assert.Equal(source.BufferIndex, copy.BufferIndex);
        Assert.Equal(source.VertexOffset, copy.VertexOffset);
        Assert.Empty(copy.FloorVertices);
        Assert.Empty(copy.CeilingVertices);
        Assert.Equal(0, copy.FloorTexture);
        Assert.Equal(0, copy.CeilingTexture);
        Assert.False(copy.Hidden);
        Assert.Equal(0, copy.Desaturation);
    }

    [Fact]
    public void UpdateAllocatesRequestedFloorAndCeilingArrays()
    {
        var both = new SurfaceUpdate(numVertices: 6, updateFloor: true, updateCeiling: true);
        var floorOnly = new SurfaceUpdate(numVertices: 6, updateFloor: true, updateCeiling: false);
        var ceilingOnly = new SurfaceUpdate(numVertices: 6, updateFloor: false, updateCeiling: true);

        Assert.Equal(6, both.NumVertices);
        Assert.Equal(6, both.FloorVertices?.Length);
        Assert.Equal(6, both.CeilingVertices?.Length);
        Assert.Equal(6, floorOnly.FloorVertices?.Length);
        Assert.Null(floorOnly.CeilingVertices);
        Assert.Null(ceilingOnly.FloorVertices);
        Assert.Equal(6, ceilingOnly.CeilingVertices?.Length);
    }

    [Fact]
    public void UpdateBoundsUsesFloorVerticesLikeUdb()
    {
        var entry = new SurfaceEntry(numVertices: 3, bufferIndex: 0, vertexOffset: 0)
        {
            FloorVertices = new[] { Vertex(12, 3), Vertex(-4, 9), Vertex(6, -2) },
            CeilingVertices = new[] { Vertex(-100, -100), Vertex(100, 100) },
        };

        entry.UpdateBounds();

        Assert.Equal(-4, entry.Bounds.Left);
        Assert.Equal(-2, entry.Bounds.Top);
        Assert.Equal(16, entry.Bounds.Width);
        Assert.Equal(11, entry.Bounds.Height);
    }

    [Fact]
    public void BoundsIntersectionMatchesViewportCullingShape()
    {
        var bounds = new SurfaceBounds(0, 0, 10, 10);

        Assert.True(bounds.Intersects(new SurfaceBounds(5, 5, 10, 10)));
        Assert.False(bounds.Intersects(new SurfaceBounds(10, 10, 5, 5)));
        Assert.False(bounds.Intersects(new SurfaceBounds(11, 0, 5, 5)));
    }

    private static FlatVertex Vertex(float x, float y)
        => new() { x = x, y = y };
}
