// ABOUTME: Tests for MapSet geometry cleanup - JoinVertices, MergeOverlappingVertices, RemoveUnusedVertices/Sectors.
// ABOUTME: Verifies linedef repointing, degenerate-line removal, distance-based merging, and unused-element pruning.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class GeometryCleanupTests
{
    [Fact]
    public void JoinVerticesRepointsLinedefs()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var c = map.AddVertex(new Vector2D(100, 1)); // near b
        var l1 = map.AddLinedef(a, b);
        var l2 = map.AddLinedef(c, a); // uses c
        map.BuildIndexes();

        map.JoinVertices(b, c); // merge c into b
        map.BuildIndexes();

        Assert.DoesNotContain(c, map.Vertices);
        Assert.Equal(2, map.Vertices.Count);
        Assert.Same(b, l2.Start); // l2 was c->a, now b->a
        Assert.Equal(2, map.Linedefs.Count);
    }

    [Fact]
    public void JoinVerticesDropsDegenerateLine()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(10, 0));
        var ab = map.AddLinedef(a, b);
        map.BuildIndexes();

        map.JoinVertices(a, b); // both ends become 'a' -> line collapses
        map.BuildIndexes();

        Assert.DoesNotContain(ab, map.Linedefs);
        Assert.Empty(map.Linedefs);
        Assert.Single(map.Vertices);
    }

    [Fact]
    public void MergeOverlappingVerticesCollapsesNearbyPoints()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(0.5, 0.5)); // within 2 units of a
        var c = map.AddVertex(new Vector2D(100, 0));
        map.AddLinedef(a, c);
        map.AddLinedef(b, c);
        map.BuildIndexes();

        int merges = map.MergeOverlappingVertices(2.0);
        map.BuildIndexes();

        Assert.Equal(1, merges);
        Assert.Equal(2, map.Vertices.Count); // a/b merged, c remains
        // Both lines now share the merged vertex.
        Assert.Same(map.Linedefs[0].Start, map.Linedefs[1].Start);
    }

    [Fact]
    public void MergeRespectsDistanceThreshold()
    {
        var map = new MapSet();
        map.AddVertex(new Vector2D(0, 0));
        map.AddVertex(new Vector2D(5, 0)); // 5 units apart
        int merges = map.MergeOverlappingVertices(2.0);
        Assert.Equal(0, merges);
        Assert.Equal(2, map.Vertices.Count);
    }

    [Fact]
    public void RemoveUnusedVerticesDropsOrphans()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(10, 0));
        map.AddVertex(new Vector2D(50, 50)); // orphan
        map.AddLinedef(a, b);
        map.BuildIndexes();

        int removed = map.RemoveUnusedVertices();
        Assert.Equal(1, removed);
        Assert.Equal(2, map.Vertices.Count);
    }

    [Fact]
    public void RemoveUnusedSectorsDropsUnreferenced()
    {
        var map = new MapSet();
        var used = map.AddSector();
        map.AddSector(); // unreferenced
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(10, 0));
        var l = map.AddLinedef(a, b);
        map.AddSidedef(l, true, used);
        map.BuildIndexes();

        int removed = map.RemoveUnusedSectors();
        Assert.Equal(1, removed);
        Assert.Single(map.Sectors);
        Assert.Same(used, map.Sectors[0]);
    }

    [Fact]
    public void MergeIsUndoable()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(1, 0));
        var c = map.AddVertex(new Vector2D(100, 0));
        map.AddLinedef(a, c);
        map.AddLinedef(b, c);
        map.BuildIndexes();
        var undo = new UndoManager(map);

        undo.CreateUndo("Merge vertices");
        map.MergeOverlappingVertices(2.0);
        map.BuildIndexes();
        Assert.Equal(2, map.Vertices.Count);

        undo.Undo();
        Assert.Equal(3, map.Vertices.Count);
    }
}
