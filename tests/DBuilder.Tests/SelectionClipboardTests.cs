// ABOUTME: Tests for SelectionClipboard - copy/paste of a map selection with dependency closure and offset.
// ABOUTME: Covers things, sector closure, linedef closure, cross-map paste, and pasted-selection state.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SelectionClipboardTests
{
    private static (MapSet map, Sector sector) SquareSector()
    {
        var map = new MapSet();
        var s = map.AddSector();
        var v = new[]
        {
            map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(0, 64)),
            map.AddVertex(new Vector2D(64, 64)), map.AddVertex(new Vector2D(64, 0)),
        };
        for (int i = 0; i < 4; i++)
        {
            var l = map.AddLinedef(v[i], v[(i + 1) % 4]);
            map.AddSidedef(l, true, s);
        }
        map.BuildIndexes();
        return (map, s);
    }

    [Fact]
    public void CopyWithNoSelectionReturnsNull()
    {
        var (map, _) = SquareSector();
        Assert.Null(SelectionClipboard.CopySelection(map));
    }

    [Fact]
    public void CopyPasteThingOffsetsAndSelects()
    {
        var map = new MapSet();
        var t = map.AddThing(new Vector2D(10, 20), 3001);
        t.Angle = 90;
        t.Selected = true;
        map.BuildIndexes();

        var buf = SelectionClipboard.CopySelection(map)!;
        var res = SelectionClipboard.Paste(map, buf, new Vector2D(100, 0));

        Assert.Equal(2, map.Things.Count);
        Assert.Equal(1, res.ThingCount);
        var pasted = map.Things[res.FirstThing];
        Assert.Equal(110, pasted.Position.x, 6);
        Assert.Equal(20, pasted.Position.y, 6);
        Assert.Equal(3001, pasted.Type);
        Assert.True(pasted.Selected);
        Assert.False(t.Selected); // prior selection cleared
    }

    [Fact]
    public void PasteAtAnchorPlacesLowerLeftCornerAtAnchor()
    {
        // A square sector at (0,0)-(64,64); copy then insert it anchored at (200, 300) in a fresh map.
        var (src, s) = SquareSector();
        s.Selected = true;
        var buf = SelectionClipboard.CopySelection(src)!;

        var dst = new MapSet();
        var res = SelectionClipboard.PasteAtAnchor(dst, buf, new Vector2D(200, 300));

        double minX = double.MaxValue, minY = double.MaxValue;
        for (int i = res.FirstVertex; i < res.FirstVertex + res.VertexCount; i++)
        {
            minX = System.Math.Min(minX, dst.Vertices[i].Position.x);
            minY = System.Math.Min(minY, dst.Vertices[i].Position.y);
        }
        Assert.Equal(200, minX, 6);
        Assert.Equal(300, minY, 6);
        Assert.Equal(4, res.VertexCount);
    }

    [Fact]
    public void PrefabRoundTripsThroughBytes()
    {
        // Simulate save/insert: copy bytes from one map, paste into another (origin-independent anchor).
        var (src, s) = SquareSector();
        s.Selected = true;
        byte[] bytes = SelectionClipboard.CopySelection(src)!;

        var dst = new MapSet();
        var res = SelectionClipboard.PasteAtAnchor(dst, bytes, new Vector2D(0, 0));
        Assert.Equal(1, res.SectorCount);
        Assert.Equal(4, res.LinedefCount);
        Assert.Equal(4, dst.Vertices.Count);
    }

    [Fact]
    public void CopySectorPullsInClosure()
    {
        var (map, s) = SquareSector();
        s.Selected = true;

        var buf = SelectionClipboard.CopySelection(map)!;
        var res = SelectionClipboard.Paste(map, buf, new Vector2D(200, 0));

        Assert.Equal(4, res.VertexCount);
        Assert.Equal(4, res.LinedefCount);
        Assert.Equal(4, res.SidedefCount);
        Assert.Equal(1, res.SectorCount);

        // Pasted geometry sits 200 units to the right of the originals.
        for (int i = res.FirstVertex; i < res.FirstVertex + res.VertexCount; i++)
            Assert.True(map.Vertices[i].Position.x >= 200);

        // The pasted sector keeps its sidedef wiring after BuildIndexes.
        Assert.Equal(4, map.Sectors[res.FirstSector].Sidedefs.Count);
    }

    [Fact]
    public void CopyLinedefPullsInVerticesAndFrontSide()
    {
        var (map, _) = SquareSector();
        map.Linedefs[0].Selected = true;

        var buf = SelectionClipboard.CopySelection(map)!;
        var res = SelectionClipboard.Paste(map, buf, new Vector2D(0, 200));

        Assert.Equal(1, res.LinedefCount);
        Assert.Equal(2, res.VertexCount);
        Assert.Equal(1, res.SidedefCount);
        Assert.Equal(1, res.SectorCount);
        Assert.NotNull(map.Linedefs[res.FirstLinedef].Front);
    }

    [Fact]
    public void CopySelectedSidedefPullsInLineVerticesAndSector()
    {
        var (map, _) = SquareSector();
        map.Sidedefs[0].Selected = true;

        var buf = SelectionClipboard.CopySelection(map)!;
        var res = SelectionClipboard.Paste(map, buf, new Vector2D(0, 200));

        Assert.Equal(1, res.LinedefCount);
        Assert.Equal(2, res.VertexCount);
        Assert.Equal(1, res.SidedefCount);
        Assert.Equal(1, res.SectorCount);
        Assert.NotNull(map.Linedefs[res.FirstLinedef].Front);
        Assert.Same(map.Sidedefs[res.FirstSidedef], map.Linedefs[res.FirstLinedef].Front);
        Assert.True(map.Sidedefs[res.FirstSidedef].Selected);
    }

    [Fact]
    public void PasteIntoDifferentMap()
    {
        var (src, s) = SquareSector();
        s.Selected = true;
        var buf = SelectionClipboard.CopySelection(src)!;

        var dst = new MapSet();
        var res = SelectionClipboard.Paste(dst, buf, new Vector2D(0, 0));

        Assert.Equal(4, dst.Vertices.Count);
        Assert.Equal(4, dst.Linedefs.Count);
        Assert.Equal(4, dst.Sidedefs.Count);
        Assert.Single(dst.Sectors);
        Assert.Equal(4, dst.Sectors[0].Sidedefs.Count);
        Assert.Equal(res.LinedefCount, dst.Linedefs.Count);
    }

    [Fact]
    public void DuplicateSelectionCopiesSelectionAndRunsPrePasteHook()
    {
        var map = new MapSet();
        var thing = map.AddThing(new Vector2D(10, 20), 3001);
        thing.Selected = true;
        bool beforePaste = false;

        var res = SelectionClipboard.DuplicateSelection(map, new Vector2D(32, 16), () =>
        {
            beforePaste = true;
            Assert.Same(thing, Assert.Single(map.Things));
            Assert.True(thing.Selected);
        });

        Assert.NotNull(res);
        Assert.True(beforePaste);
        Assert.Equal(2, map.Things.Count);
        Assert.Equal(1, res.Value.ThingCount);
        Assert.False(thing.Selected);
        Assert.True(map.Things[res.Value.FirstThing].Selected);
        Assert.Equal(new Vector2D(42, 36), map.Things[res.Value.FirstThing].Position);
    }

    [Fact]
    public void DuplicateSelectionWithNoSelectionReturnsNullAndSkipsHook()
    {
        var (map, _) = SquareSector();
        bool beforePaste = false;

        var res = SelectionClipboard.DuplicateSelection(map, new Vector2D(32, 16), () => beforePaste = true);

        Assert.Null(res);
        Assert.False(beforePaste);
    }

    [Fact]
    public void PasteOptionsRemovePastedTagsAndActions()
    {
        var (map, sector) = SquareSector();
        var line = map.Linedefs[0];
        line.Tag = 5;
        line.Action = 80;
        line.Args[0] = 7;
        sector.Tag = 6;
        var thing = map.AddThing(new Vector2D(8, 8), 3001);
        thing.Tag = 7;
        thing.Action = 226;
        thing.Args[0] = 3;
        sector.Selected = true;
        thing.Selected = true;

        var buffer = SelectionClipboard.CopySelection(map)!;
        var result = SelectionClipboard.Paste(map, buffer, new Vector2D(128, 0), new PasteOptions
        {
            ChangeTags = PasteTagMode.Remove,
            RemoveActions = true,
        });

        Assert.Equal(5, line.Tag);
        Assert.Equal(80, line.Action);
        Assert.Equal(6, sector.Tag);
        Assert.Equal(7, thing.Tag);
        Assert.Equal(226, thing.Action);

        var pastedLine = map.Linedefs[result.FirstLinedef];
        var pastedSector = map.Sectors[result.FirstSector];
        var pastedThing = map.Things[result.FirstThing];
        Assert.Empty(pastedLine.Tags);
        Assert.Empty(pastedSector.Tags);
        Assert.Equal(0, pastedThing.Tag);
        Assert.Equal(0, pastedLine.Action);
        Assert.All(pastedLine.Args, arg => Assert.Equal(0, arg));
        Assert.Equal(0, pastedThing.Action);
        Assert.All(pastedThing.Args, arg => Assert.Equal(0, arg));
    }

    [Fact]
    public void PasteOptionsRenumberPastedTagsAwayFromExistingGeometry()
    {
        var (map, sector) = SquareSector();
        var line = map.Linedefs[0];
        line.Tag = 1;
        line.Tags.Add(7);
        sector.Tag = 2;
        var thing = map.AddThing(new Vector2D(8, 8), 3001);
        thing.Tag = 3;
        sector.Selected = true;
        thing.Selected = true;

        var buffer = SelectionClipboard.CopySelection(map)!;
        var result = SelectionClipboard.Paste(map, buffer, new Vector2D(128, 0), new PasteOptions
        {
            ChangeTags = PasteTagMode.Renumber,
        });

        Assert.Equal(new[] { 1, 7 }, line.Tags);
        Assert.Equal(2, sector.Tag);
        Assert.Equal(3, thing.Tag);
        Assert.Equal(new[] { 4, 5 }, map.Linedefs[result.FirstLinedef].Tags);
        Assert.Equal(6, map.Sectors[result.FirstSector].Tag);
        Assert.Equal(8, map.Things[result.FirstThing].Tag);
    }
}
