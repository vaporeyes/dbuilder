// ABOUTME: Tests for the snapshot-based UndoManager - create/undo/redo restore full map state, redo invalidation, level cap.
// ABOUTME: Exercises geometry, custom fields, tags and namespace to confirm the ClipboardStream snapshot captures everything.

using System.Linq;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class UndoManagerTests
{
    private static MapSet BuildMap()
    {
        var map = new MapSet { Namespace = "Doom" };
        var s = map.AddSector();
        s.FloorTexture = "FLOOR1"; s.CeilTexture = "CEIL1"; s.FloorHeight = 0; s.CeilHeight = 128; s.Tag = 7;
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(100, 0));
        var l = map.AddLinedef(v0, v1);
        map.AddSidedef(l, true, s);
        map.AddThing(new Vector2D(50, 50), 3001);
        map.BuildIndexes();
        return map;
    }

    [Fact]
    public void UndoRestoresVertexMove()
    {
        var map = BuildMap();
        var undo = new UndoManager(map);

        undo.CreateUndo("Move vertex");
        map.Vertices[0].Position = new Vector2D(999, 888);

        Assert.True(undo.CanUndo);
        Assert.True(undo.Undo());
        Assert.Equal(new Vector2D(0, 0), map.Vertices[0].Position);
    }

    [Fact]
    public void RedoReappliesEdit()
    {
        var map = BuildMap();
        var undo = new UndoManager(map);

        undo.CreateUndo("Move vertex");
        map.Vertices[0].Position = new Vector2D(999, 888);
        undo.Undo();
        Assert.Equal(new Vector2D(0, 0), map.Vertices[0].Position);

        Assert.True(undo.CanRedo);
        Assert.True(undo.Redo());
        Assert.Equal(new Vector2D(999, 888), map.Vertices[0].Position);
    }

    [Fact]
    public void UndoRestoresDeletedGeometry()
    {
        var map = BuildMap();
        var undo = new UndoManager(map);
        int vertsBefore = map.Vertices.Count;
        int linesBefore = map.Linedefs.Count;
        int sidesBefore = map.Sidedefs.Count;

        undo.CreateUndo("Delete linedef");
        map.RemoveLinedef(map.Linedefs[0]);
        map.BuildIndexes();
        Assert.Empty(map.Linedefs);

        undo.Undo();
        Assert.Equal(vertsBefore, map.Vertices.Count);
        Assert.Equal(linesBefore, map.Linedefs.Count);
        Assert.Equal(sidesBefore, map.Sidedefs.Count);
        // Restored linedef references restored vertices and sidedef.
        var l = map.Linedefs[0];
        Assert.Same(map.Vertices[0], l.Start);
        Assert.Same(map.Vertices[1], l.End);
        Assert.NotNull(l.Front);
        Assert.Same(map.Sectors[0], l.Front!.Sector);
    }

    [Fact]
    public void SnapshotPreservesFieldsTagsAndNamespace()
    {
        var map = BuildMap();
        map.Fields["author"] = "tester";
        map.Sectors[0].Fields["lightcolor"] = 16711680;
        map.Sectors[0].Tags.Add(9); // now [7, 9]
        var undo = new UndoManager(map);

        undo.CreateUndo("edit");
        map.Namespace = "ZDoom";
        map.Fields.Clear();
        map.Sectors[0].Fields.Clear();
        map.Sectors[0].Tags.Clear();
        undo.Undo();

        Assert.Equal("Doom", map.Namespace);
        Assert.Equal("tester", map.Fields["author"]);
        Assert.Equal(16711680, (int)map.Sectors[0].Fields["lightcolor"]);
        Assert.Equal(new[] { 7, 9 }, map.Sectors[0].Tags);
    }

    [Fact]
    public void NewEditClearsRedoStack()
    {
        var map = BuildMap();
        var undo = new UndoManager(map);

        undo.CreateUndo("a");
        map.Vertices[0].Position = new Vector2D(1, 1);
        undo.Undo();
        Assert.True(undo.CanRedo);

        // A fresh edit should discard the redo history.
        undo.CreateUndo("b");
        map.Vertices[0].Position = new Vector2D(2, 2);
        Assert.False(undo.CanRedo);
    }

    [Fact]
    public void MultiLevelUndoRedo()
    {
        var map = BuildMap();
        var undo = new UndoManager(map);

        undo.CreateUndo("move1");
        map.Vertices[0].Position = new Vector2D(10, 0);
        undo.CreateUndo("move2");
        map.Vertices[0].Position = new Vector2D(20, 0);
        undo.CreateUndo("move3");
        map.Vertices[0].Position = new Vector2D(30, 0);

        undo.Undo(); Assert.Equal(new Vector2D(20, 0), map.Vertices[0].Position);
        undo.Undo(); Assert.Equal(new Vector2D(10, 0), map.Vertices[0].Position);
        undo.Undo(); Assert.Equal(new Vector2D(0, 0),  map.Vertices[0].Position);
        Assert.False(undo.CanUndo);

        undo.Redo(); Assert.Equal(new Vector2D(10, 0), map.Vertices[0].Position);
        undo.Redo(); Assert.Equal(new Vector2D(20, 0), map.Vertices[0].Position);
    }

    [Fact]
    public void LevelCapDiscardsOldestSnapshots()
    {
        var map = BuildMap();
        var undo = new UndoManager(map, maxLevels: 2);

        undo.CreateUndo("1");
        map.Vertices[0].Position = new Vector2D(1, 0);
        undo.CreateUndo("2");
        map.Vertices[0].Position = new Vector2D(2, 0);
        undo.CreateUndo("3");
        map.Vertices[0].Position = new Vector2D(3, 0);

        Assert.Equal(2, undo.UndoCount);
        // Only the two most recent edits can be undone; the oldest snapshot was dropped.
        undo.Undo(); Assert.Equal(new Vector2D(2, 0), map.Vertices[0].Position);
        undo.Undo(); Assert.Equal(new Vector2D(1, 0), map.Vertices[0].Position);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void UndoWithEmptyStackReturnsFalse()
    {
        var map = BuildMap();
        var undo = new UndoManager(map);
        Assert.False(undo.Undo());
        Assert.False(undo.Redo());
    }

    [Fact]
    public void DescriptionsTrackTheNextOperation()
    {
        var map = BuildMap();
        var undo = new UndoManager(map);
        undo.CreateUndo("Move vertex");
        map.Vertices[0].Position = new Vector2D(1, 1);

        Assert.Equal("Move vertex", undo.NextUndoDescription);
        undo.Undo();
        Assert.Equal("Move vertex", undo.NextRedoDescription);
    }
}
