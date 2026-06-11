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
        AssertIndexes(map.Vertices);
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
        AssertIndexes(map.Linedefs);
    }

    [Fact]
    public void ChangeIndexOverloadsMoveElementsByOldAndNewIndex()
    {
        var map = new MapSet();
        Vertex firstVertex = map.AddVertex(new Vector2D(0, 0));
        Vertex secondVertex = map.AddVertex(new Vector2D(1, 0));
        Vertex thirdVertex = map.AddVertex(new Vector2D(2, 0));
        Sector firstSector = map.AddSector();
        Sector secondSector = map.AddSector();
        Thing firstThing = map.AddThing(new Vector2D(0, 0), 1);
        Thing secondThing = map.AddThing(new Vector2D(1, 0), 2);

        Assert.True(map.ChangeVertexIndex(0, 2));
        Assert.True(map.ChangeSectorIndex(1, 0));
        Assert.True(map.ChangeThingIndex(0, 1));

        Assert.Equal([secondVertex, thirdVertex, firstVertex], map.Vertices);
        Assert.Equal([secondSector, firstSector], map.Sectors);
        Assert.Equal([secondThing, firstThing], map.Things);
        AssertIndexes(map.Vertices);
        AssertIndexes(map.Sectors);
        AssertIndexes(map.Things);
    }

    [Fact]
    public void ChangeLindefIndexAliasMatchesUdbSpelling()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(1, 0));
        Linedef first = map.AddLinedef(a, b);
        Linedef second = map.AddLinedef(a, b);

        Assert.True(map.ChangeLindefIndex(1, 0));

        Assert.Equal([second, first], map.Linedefs);
        AssertIndexes(map.Linedefs);
    }

    [Fact]
    public void GetLindefByIndexAliasMatchesUdbSpelling()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(1, 0));
        Linedef first = map.AddLinedef(a, b);
        Linedef second = map.AddLinedef(a, b);

        Assert.Same(first, map.GetLindefByIndex(0));
        Assert.Same(second, map.GetLindefByIndex(1));
        Assert.Null(map.GetLindefByIndex(-1));
        Assert.Null(map.GetLindefByIndex(2));
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
        AssertIndexes(map.Sectors);
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
        AssertIndexes(map.Things);
    }

    [Fact]
    public void ChangeIndexOverloadsRejectOutOfRangeIndexes()
    {
        var map = new MapSet();
        Vertex first = map.AddVertex(new Vector2D(0, 0));
        Vertex second = map.AddVertex(new Vector2D(1, 0));

        Assert.False(map.ChangeVertexIndex(-1, 0));
        Assert.False(map.ChangeVertexIndex(0, 2));

        Assert.Equal([first, second], map.Vertices);
        AssertIndexes(map.Vertices);
    }

    [Fact]
    public void AddedElementsReceiveListIndexes()
    {
        var map = new MapSet();
        var firstVertex = map.AddVertex(new Vector2D(0, 0));
        var secondVertex = map.AddVertex(new Vector2D(1, 0));
        var line = map.AddLinedef(firstVertex, secondVertex);
        var sector = map.AddSector();
        var side = map.AddSidedef(line, true, sector);
        var thing = map.AddThing(new Vector2D(2, 0), 3001);

        Assert.Equal(0, firstVertex.Index);
        Assert.Equal(1, secondVertex.Index);
        Assert.Equal(0, line.Index);
        Assert.Equal(0, sector.Index);
        Assert.Equal(0, side.Index);
        Assert.Equal(0, thing.Index);
    }

    [Fact]
    public void RemovingElementsReindexesRemainingElements()
    {
        var map = new MapSet();
        Vertex firstVertex = map.AddVertex(new Vector2D(0, 0));
        Vertex secondVertex = map.AddVertex(new Vector2D(1, 0));
        Vertex thirdVertex = map.AddVertex(new Vector2D(2, 0));
        Thing firstThing = map.AddThing(new Vector2D(0, 0), 1);
        Thing secondThing = map.AddThing(new Vector2D(1, 0), 2);

        map.RemoveVertex(firstVertex);
        map.RemoveThing(firstThing);

        Assert.Equal([secondVertex, thirdVertex], map.Vertices);
        Assert.Equal([secondThing], map.Things);
        AssertIndexes(map.Vertices);
        AssertIndexes(map.Things);
    }

    [Fact]
    public void BuildIndexesRefreshesManuallyMutatedListIndexes()
    {
        var map = new MapSet();
        Vertex first = map.AddVertex(new Vector2D(0, 0));
        Vertex second = map.AddVertex(new Vector2D(1, 0));

        map.Vertices.Reverse();
        map.BuildIndexes();

        Assert.Equal([second, first], map.Vertices);
        AssertIndexes(map.Vertices);
    }

    private static void AssertIndexes<T>(IReadOnlyList<T> elements) where T : IMapElement
    {
        for (int i = 0; i < elements.Count; i++) Assert.Equal(i, elements[i].Index);
    }
}
