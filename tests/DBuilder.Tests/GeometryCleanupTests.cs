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
        Assert.True(c.IsDisposed);
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
        Assert.True(ab.IsDisposed);
    }

    [Fact]
    public void UnstableLinedefsFromVerticesReturnsLinesWithOneEndpointInSet()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(64, 0));
        var c = map.AddVertex(new Vector2D(128, 0));
        var ab = map.AddLinedef(a, b);
        var bc = map.AddLinedef(b, c);
        map.BuildIndexes();

        var unstable = MapSet.UnstableLinedefsFromVertices(new[] { a, b });

        Assert.Equal(new[] { bc }, unstable);
        Assert.DoesNotContain(ab, unstable);
    }

    [Fact]
    public void UnstableLinedefsFromVerticesKeepsLinesTouchingOneSelectedVertex()
    {
        var map = new MapSet();
        var center = map.AddVertex(new Vector2D(0, 0));
        var left = map.AddVertex(new Vector2D(-64, 0));
        var right = map.AddVertex(new Vector2D(64, 0));
        var up = map.AddVertex(new Vector2D(0, 64));
        var leftLine = map.AddLinedef(center, left);
        var rightLine = map.AddLinedef(center, right);
        var upLine = map.AddLinedef(center, up);
        map.BuildIndexes();

        var unstable = MapSet.UnstableLinedefsFromVertices(new[] { center });

        Assert.Equal(new[] { leftLine, rightLine, upLine }, unstable);
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
        var orphan = map.AddVertex(new Vector2D(50, 50));
        map.AddLinedef(a, b);
        map.BuildIndexes();

        int removed = map.RemoveUnusedVertices();
        Assert.Equal(1, removed);
        Assert.Equal(2, map.Vertices.Count);
        Assert.True(map.ContainsVertex(a));
        Assert.True(orphan.IsDisposed);
    }

    [Fact]
    public void RemoveUnusedSectorsDropsUnreferenced()
    {
        var map = new MapSet();
        var used = map.AddSector();
        var unused = map.AddSector();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(10, 0));
        var l = map.AddLinedef(a, b);
        map.AddSidedef(l, true, used);
        map.BuildIndexes();

        int removed = map.RemoveUnusedSectors();
        Assert.Equal(1, removed);
        Assert.Single(map.Sectors);
        Assert.Same(used, map.Sectors[0]);
        Assert.True(unused.IsDisposed);
    }

    [Fact]
    public void RepairReferencesRemovesLinedefsWithMissingVertices()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(10, 0));
        var outside = new Vertex(new Vector2D(20, 0));
        var valid = map.AddLinedef(a, b);
        var invalid = map.AddLinedef(a, outside);
        var invalidSide = map.AddSidedef(invalid, true, null);

        int repairs = map.RepairReferences();

        Assert.Equal(1, repairs);
        Assert.Contains(valid, map.Linedefs);
        Assert.DoesNotContain(invalid, map.Linedefs);
        Assert.DoesNotContain(invalidSide, map.Sidedefs);
        Assert.True(invalid.IsDisposed);
        Assert.True(invalidSide.IsDisposed);
    }

    [Fact]
    public void RepairReferencesRemovesDetachedSidedefsAndClearsInvalidLineSides()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(10, 0));
        var line = map.AddLinedef(a, b);
        var owned = map.AddSidedef(line, true, null);
        var detached = new Sidedef(line, false);
        map.Sidedefs.Add(detached);
        line.Back = new Sidedef(line, false);

        int repairs = map.RepairReferences();

        Assert.Equal(2, repairs);
        Assert.Contains(owned, map.Sidedefs);
        Assert.DoesNotContain(detached, map.Sidedefs);
        Assert.Null(line.Back);
        Assert.True(detached.IsDisposed);
    }

    [Fact]
    public void RepairReferencesNullsSidedefSectorRemovedFromMap()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(10, 0));
        var line = map.AddLinedef(a, b);
        var side = map.AddSidedef(line, true, sector);
        map.Sectors.Clear();

        int repairs = map.RepairReferences();

        Assert.Equal(1, repairs);
        Assert.Null(side.Sector);
        Assert.Contains(side, map.Sidedefs);
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
