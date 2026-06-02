// ABOUTME: Tests UDB-style map element index reassignment helpers.
// ABOUTME: Verifies list reordering, sector reindexing, and invalid index rejection.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class MapElementIndexTests
{
    [Fact]
    public void ChangeVertexIndexMovesVertexWithinList()
    {
        var map = new MapSet();
        Vertex first = map.AddVertex(new Vector2D(0, 0));
        Vertex second = map.AddVertex(new Vector2D(1, 0));
        Vertex third = map.AddVertex(new Vector2D(2, 0));

        bool changed = map.ChangeVertexIndex(first, 2);

        Assert.True(changed);
        Assert.Equal([second, third, first], map.Vertices);
    }

    [Fact]
    public void ChangeLinedefIndexMovesLinedefWithinList()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(1, 0));
        Linedef first = map.AddLinedef(a, b);
        Linedef second = map.AddLinedef(a, b);
        Linedef third = map.AddLinedef(a, b);

        bool changed = map.ChangeLinedefIndex(third, 0);

        Assert.True(changed);
        Assert.Equal([third, first, second], map.Linedefs);
    }

    [Fact]
    public void ChangeSectorIndexReindexesSectors()
    {
        var map = new MapSet();
        Sector first = map.AddSector();
        Sector second = map.AddSector();
        Sector third = map.AddSector();

        bool changed = map.ChangeSectorIndex(third, 0);

        Assert.True(changed);
        Assert.Equal([third, first, second], map.Sectors);
        Assert.Equal(0, third.Index);
        Assert.Equal(1, first.Index);
        Assert.Equal(2, second.Index);
    }

    [Fact]
    public void ChangeThingIndexRejectsOutOfRangeIndex()
    {
        var map = new MapSet();
        Thing first = map.AddThing(new Vector2D(0, 0), 1);
        Thing second = map.AddThing(new Vector2D(1, 0), 2);

        bool changed = map.ChangeThingIndex(first, 2);

        Assert.False(changed);
        Assert.Equal([first, second], map.Things);
    }
}
