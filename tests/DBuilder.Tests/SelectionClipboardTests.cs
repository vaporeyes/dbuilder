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
        Assert.Equal(1, dst.Sectors.Count);
        Assert.Equal(4, dst.Sectors[0].Sidedefs.Count);
        Assert.Equal(res.LinedefCount, dst.Linedefs.Count);
    }
}
