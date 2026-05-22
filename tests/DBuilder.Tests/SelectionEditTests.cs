// ABOUTME: Tests for selection-driven edit operations - MoveSelectedVerticesBy/ThingsBy and DeleteSelection.
// ABOUTME: Includes undo bracketing so move and delete are demonstrably reversible end to end.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SelectionEditTests
{
    private static MapSet BuildTwoRooms()
    {
        var map = new MapSet();
        var sA = map.AddSector(); sA.FloorTexture = "F"; sA.CeilTexture = "C";
        var sB = map.AddSector(); sB.FloorTexture = "F"; sB.CeilTexture = "C";

        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(100, 0));
        var v2 = map.AddVertex(new Vector2D(100, 100));

        var lA = map.AddLinedef(v0, v1);
        map.AddSidedef(lA, true, sA);
        var lDiv = map.AddLinedef(v1, v2);
        map.AddSidedef(lDiv, true, sA);
        map.AddSidedef(lDiv, false, sB);

        map.AddThing(new Vector2D(50, 50), 3001);
        map.BuildIndexes();
        return map;
    }

    [Fact]
    public void MoveSelectedVerticesOffsetsOnlyFlagged()
    {
        var map = BuildTwoRooms();
        map.Vertices[0].Selected = true;
        map.Vertices[2].Selected = true;

        int moved = map.MoveSelectedVerticesBy(new Vector2D(10, -5));

        Assert.Equal(2, moved);
        Assert.Equal(new Vector2D(10, -5), map.Vertices[0].Position);
        Assert.Equal(new Vector2D(100, 0), map.Vertices[1].Position); // untouched
        Assert.Equal(new Vector2D(110, 95), map.Vertices[2].Position);
    }

    [Fact]
    public void MoveSelectedThingsOffsetsOnlyFlagged()
    {
        var map = BuildTwoRooms();
        map.Things[0].Selected = true;
        int moved = map.MoveSelectedThingsBy(new Vector2D(0, 16));
        Assert.Equal(1, moved);
        Assert.Equal(new Vector2D(50, 66), map.Things[0].Position);
    }

    [Fact]
    public void DeleteSelectionRemovesThings()
    {
        var map = BuildTwoRooms();
        map.Things[0].Selected = true;
        int removed = map.DeleteSelection();
        Assert.Equal(1, removed);
        Assert.Empty(map.Things);
    }

    [Fact]
    public void DeleteSelectionRemovesLinedefAndItsSidedefs()
    {
        var map = BuildTwoRooms();
        var lDiv = map.Linedefs[1];
        lDiv.Selected = true;
        int sidesBefore = map.Sidedefs.Count;

        map.DeleteSelection();
        map.BuildIndexes();

        Assert.DoesNotContain(lDiv, map.Linedefs);
        Assert.Equal(sidesBefore - 2, map.Sidedefs.Count); // both sides of the divider gone
    }

    [Fact]
    public void DeleteSelectionVertexCascadesToLines()
    {
        var map = BuildTwoRooms();
        var v1 = map.Vertices[1]; // shared by lA and lDiv
        v1.Selected = true;
        map.DeleteSelection();
        map.BuildIndexes();

        Assert.DoesNotContain(v1, map.Vertices);
        Assert.Equal(2, map.Vertices.Count);
        Assert.Empty(map.Linedefs); // both lines touched v1
        Assert.Empty(map.Sidedefs);
    }

    [Fact]
    public void DeleteSelectionMixedTypesInDependencyOrder()
    {
        var map = BuildTwoRooms();
        map.Sectors[1].Selected = true;   // sB
        map.Things[0].Selected = true;
        map.DeleteSelection();
        map.BuildIndexes();

        Assert.Single(map.Sectors);
        Assert.Empty(map.Things);
        // The divider's back side referenced sB; after sector removal its sector ref is null.
        var back = map.Linedefs[1].Back;
        Assert.NotNull(back);
        Assert.Null(back!.Sector);
    }

    [Fact]
    public void MoveIsUndoable()
    {
        var map = BuildTwoRooms();
        var undo = new UndoManager(map);
        map.Vertices[0].Selected = true;

        undo.CreateUndo("Move selection");
        map.MoveSelectedVerticesBy(new Vector2D(25, 25));
        Assert.Equal(new Vector2D(25, 25), map.Vertices[0].Position);

        undo.Undo();
        Assert.Equal(new Vector2D(0, 0), map.Vertices[0].Position);
    }

    [Fact]
    public void DeleteIsUndoable()
    {
        var map = BuildTwoRooms();
        var undo = new UndoManager(map);
        int vertsBefore = map.Vertices.Count;
        int linesBefore = map.Linedefs.Count;
        int sidesBefore = map.Sidedefs.Count;

        map.Linedefs[1].Selected = true;
        undo.CreateUndo("Delete selection");
        map.DeleteSelection();
        map.BuildIndexes();
        Assert.Single(map.Linedefs);

        undo.Undo();
        Assert.Equal(vertsBefore, map.Vertices.Count);
        Assert.Equal(linesBefore, map.Linedefs.Count);
        Assert.Equal(sidesBefore, map.Sidedefs.Count);
        // Restored two-sided divider has both sectors wired again.
        var div = map.Linedefs[1];
        Assert.NotNull(div.Front);
        Assert.NotNull(div.Back);
        Assert.Same(map.Sectors[0], div.Front!.Sector);
        Assert.Same(map.Sectors[1], div.Back!.Sector);
    }
}
