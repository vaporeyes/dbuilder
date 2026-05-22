// ABOUTME: Tests for the per-element Selected flag and MapSet selection query/clear helpers.
// ABOUTME: Also confirms selection is transient - it does not survive an undo (snapshots restore fresh instances).

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SelectionTests
{
    private static MapSet BuildMap()
    {
        var map = new MapSet();
        var s = map.AddSector();
        s.FloorTexture = "F"; s.CeilTexture = "C";
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(10, 0));
        var v2 = map.AddVertex(new Vector2D(10, 10));
        var l0 = map.AddLinedef(v0, v1);
        var l1 = map.AddLinedef(v1, v2);
        map.AddSidedef(l0, true, s);
        map.AddThing(new Vector2D(5, 5), 3001);
        map.BuildIndexes();
        return map;
    }

    [Fact]
    public void GetSelectedReturnsOnlyFlaggedElements()
    {
        var map = BuildMap();
        map.Vertices[0].Selected = true;
        map.Vertices[2].Selected = true;
        map.Linedefs[1].Selected = true;
        map.Sectors[0].Selected = true;

        var selVerts = map.GetSelectedVertices();
        Assert.Equal(2, selVerts.Count);
        Assert.Contains(map.Vertices[0], selVerts);
        Assert.Contains(map.Vertices[2], selVerts);

        Assert.Single(map.GetSelectedLinedefs());
        Assert.Same(map.Linedefs[1], map.GetSelectedLinedefs()[0]);
        Assert.Single(map.GetSelectedSectors());
        Assert.Empty(map.GetSelectedThings());
    }

    [Fact]
    public void CountsMatchSelection()
    {
        var map = BuildMap();
        map.Vertices[0].Selected = true;
        map.Things[0].Selected = true;
        Assert.Equal(1, map.SelectedVerticesCount);
        Assert.Equal(0, map.SelectedLinedefsCount);
        Assert.Equal(1, map.SelectedThingsCount);
    }

    [Fact]
    public void ClearSelectedPerType()
    {
        var map = BuildMap();
        map.Vertices[0].Selected = true;
        map.Linedefs[0].Selected = true;

        map.ClearSelectedVertices();
        Assert.Empty(map.GetSelectedVertices());
        // Linedef selection untouched by the vertex-only clear.
        Assert.Single(map.GetSelectedLinedefs());
    }

    [Fact]
    public void ClearAllSelectedClearsEverything()
    {
        var map = BuildMap();
        map.Vertices[0].Selected = true;
        map.Linedefs[0].Selected = true;
        map.Sidedefs[0].Selected = true;
        map.Sectors[0].Selected = true;
        map.Things[0].Selected = true;

        map.ClearAllSelected();

        Assert.Empty(map.GetSelectedVertices());
        Assert.Empty(map.GetSelectedLinedefs());
        Assert.Empty(map.GetSelectedSidedefs());
        Assert.Empty(map.GetSelectedSectors());
        Assert.Empty(map.GetSelectedThings());
    }

    [Fact]
    public void SelectionPicksUpHitTestResults()
    {
        // The intended editor flow: hit-test then flag the result selected.
        var map = BuildMap();
        var v = map.NearestVertex(new Vector2D(1, 1));
        Assert.NotNull(v);
        v!.Selected = true;
        Assert.Single(map.GetSelectedVertices());
        Assert.Same(v, map.GetSelectedVertices()[0]);
    }

    [Fact]
    public void SelectionDoesNotSurviveUndo()
    {
        var map = BuildMap();
        var undo = new UndoManager(map);

        undo.CreateUndo("edit");
        map.Vertices[0].Position = new Vector2D(99, 99);
        map.Vertices[0].Selected = true;
        Assert.Equal(1, map.SelectedVerticesCount);

        undo.Undo();
        // Snapshot restore rebuilds fresh element instances with default (unselected) state.
        Assert.Equal(0, map.SelectedVerticesCount);
        Assert.Equal(new Vector2D(0, 0), map.Vertices[0].Position);
    }
}
