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

    [Fact]
    public void ApplyUpdateCreatesChunkedEntriesForNewSurface()
    {
        var entries = new SurfaceEntryCollection();
        SurfaceUpdate update = Update(
            vertices: 6001,
            floorStart: 10,
            ceilingStart: 1000,
            floorTexture: 11,
            ceilingTexture: 22,
            hidden: true,
            desaturation: 0.25);

        bool reallocated = entries.ApplyUpdate(update);

        Assert.False(reallocated);
        Assert.Equal(6001, entries.TotalVertices);
        Assert.Equal(new[] { 6000, 1 }, entries.Select(entry => entry.NumVertices).ToArray());
        Assert.Equal(11, entries[0].FloorTexture);
        Assert.Equal(22, entries[0].CeilingTexture);
        Assert.True(entries[0].Hidden);
        Assert.Equal(0.25, entries[0].Desaturation);
        Assert.Equal(10, entries[0].FloorVertices[0].x);
        Assert.Equal(6009, entries[0].FloorVertices[^1].x);
        Assert.Equal(6010, entries[1].FloorVertices[0].x);
        Assert.Equal(1000, entries[0].CeilingVertices[0].x);
    }

    [Fact]
    public void ApplyUpdateReusesEntriesWhenVertexCountMatches()
    {
        var entries = new SurfaceEntryCollection();
        entries.ApplyUpdate(Update(vertices: 3, floorStart: 0, ceilingStart: 100, floorTexture: 1, ceilingTexture: 2));
        SurfaceEntry existing = entries[0];
        existing.BufferIndex = 4;
        existing.VertexOffset = 12;

        bool reallocated = entries.ApplyUpdate(Update(vertices: 3, floorStart: 10, ceilingStart: 200, floorTexture: 7, ceilingTexture: 8));

        Assert.False(reallocated);
        Assert.Same(existing, entries[0]);
        Assert.Equal(4, entries[0].BufferIndex);
        Assert.Equal(12, entries[0].VertexOffset);
        Assert.Equal(new[] { 10f, 11f, 12f }, entries[0].FloorVertices.Select(v => v.x).ToArray());
        Assert.Equal(new[] { 200f, 201f, 202f }, entries[0].CeilingVertices.Select(v => v.x).ToArray());
        Assert.Equal(7, entries[0].FloorTexture);
        Assert.Equal(8, entries[0].CeilingTexture);
    }

    [Fact]
    public void ApplyUpdateCopiesOnlyRequestedSurfacesWhenReusingEntries()
    {
        var entries = new SurfaceEntryCollection();
        entries.ApplyUpdate(Update(vertices: 3, floorStart: 0, ceilingStart: 100, floorTexture: 1, ceilingTexture: 2));

        var update = new SurfaceUpdate(numVertices: 3, updateFloor: false, updateCeiling: true)
        {
            CeilingVertices = Vertices(3, 300),
            CeilingTexture = 9,
            Hidden = true,
            Desaturation = 0.75,
        };
        bool reallocated = entries.ApplyUpdate(update);

        Assert.False(reallocated);
        Assert.Equal(new[] { 0f, 1f, 2f }, entries[0].FloorVertices.Select(v => v.x).ToArray());
        Assert.Equal(new[] { 300f, 301f, 302f }, entries[0].CeilingVertices.Select(v => v.x).ToArray());
        Assert.Equal(1, entries[0].FloorTexture);
        Assert.Equal(9, entries[0].CeilingTexture);
        Assert.True(entries[0].Hidden);
        Assert.Equal(0.75, entries[0].Desaturation);
    }

    [Fact]
    public void ApplyUpdateReallocatesWhenVertexCountChanges()
    {
        var entries = new SurfaceEntryCollection();
        entries.ApplyUpdate(Update(vertices: 3, floorStart: 0, ceilingStart: 100, floorTexture: 1, ceilingTexture: 2));
        SurfaceEntry oldEntry = entries[0];

        bool reallocated = entries.ApplyUpdate(Update(vertices: 4, floorStart: 10, ceilingStart: 200, floorTexture: 3, ceilingTexture: 4));

        Assert.True(reallocated);
        Assert.NotSame(oldEntry, entries[0]);
        Assert.Equal(4, entries.TotalVertices);
        Assert.Single(entries);
        Assert.Equal(4, entries[0].NumVertices);
        Assert.Equal(new[] { 10f, 11f, 12f, 13f }, entries[0].FloorVertices.Select(v => v.x).ToArray());
    }

    [Fact]
    public void ApplyUpdateClearsEntriesWhenVertexCountBecomesZero()
    {
        var entries = new SurfaceEntryCollection();
        entries.ApplyUpdate(Update(vertices: 3, floorStart: 0, ceilingStart: 100, floorTexture: 1, ceilingTexture: 2));

        bool reallocated = entries.ApplyUpdate(new SurfaceUpdate(numVertices: 0, updateFloor: false, updateCeiling: false));

        Assert.True(reallocated);
        Assert.Empty(entries);
        Assert.Equal(0, entries.TotalVertices);
    }

    [Fact]
    public void ApplyUpdateRequiresBothSurfacesWhenCreatingEntries()
    {
        var entries = new SurfaceEntryCollection();
        var update = new SurfaceUpdate(numVertices: 3, updateFloor: true, updateCeiling: false);

        Assert.Throws<InvalidOperationException>(() => entries.ApplyUpdate(update));
    }

    private static FlatVertex Vertex(float x, float y)
        => new() { x = x, y = y };

    private static SurfaceUpdate Update(
        int vertices,
        float floorStart,
        float ceilingStart,
        long floorTexture,
        long ceilingTexture,
        bool hidden = false,
        double desaturation = 0)
        => new SurfaceUpdate(vertices, updateFloor: true, updateCeiling: true)
        {
            FloorVertices = Vertices(vertices, floorStart),
            CeilingVertices = Vertices(vertices, ceilingStart),
            FloorTexture = floorTexture,
            CeilingTexture = ceilingTexture,
            Hidden = hidden,
            Desaturation = desaturation,
        };

    private static FlatVertex[] Vertices(int count, float start)
        => Enumerable.Range(0, count)
            .Select(i => Vertex(start + i, start + i))
            .ToArray();
}
