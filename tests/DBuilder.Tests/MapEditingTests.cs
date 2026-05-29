// ABOUTME: Tests for MapSet mutation operations (add/remove vertex/linedef/sidedef/sector/thing) and referential integrity.
// ABOUTME: Verifies that removals cascade correctly and BuildIndexes produces a consistent derived state afterward.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class MapEditingTests
{
    // Builds a simple two-sector setup sharing a two-sided divider, returning the map.
    private static MapSet BuildTwoRooms()
    {
        var map = new MapSet();
        var sA = map.AddSector();
        var sB = map.AddSector();
        sA.FloorTexture = sB.FloorTexture = "F";
        sA.CeilTexture = sB.CeilTexture = "C";

        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(100, 0));
        var v2 = map.AddVertex(new Vector2D(100, 100));
        var v3 = map.AddVertex(new Vector2D(0, 100));

        // Outer ring around sA, plus a divider with two sides.
        var lA = map.AddLinedef(v0, v1);
        map.AddSidedef(lA, true, sA);
        var lDiv = map.AddLinedef(v1, v2);
        map.AddSidedef(lDiv, true, sA);
        map.AddSidedef(lDiv, false, sB);

        map.BuildIndexes();
        return map;
    }

    [Fact]
    public void AddOperationsAppendToLists()
    {
        var map = new MapSet();
        var v = map.AddVertex(new Vector2D(5, 5));
        var s = map.AddSector();
        var l = map.AddLinedef(v, map.AddVertex(new Vector2D(10, 10)));
        var sd = map.AddSidedef(l, true, s);
        var t = map.AddThing(new Vector2D(1, 1), 3001);

        Assert.Contains(v, map.Vertices);
        Assert.Contains(s, map.Sectors);
        Assert.Contains(l, map.Linedefs);
        Assert.Contains(sd, map.Sidedefs);
        Assert.Contains(t, map.Things);
        Assert.Same(sd, l.Front);
        Assert.Same(s, sd.Sector);
        Assert.Equal(0, s.Index);
    }

    [Fact]
    public void RemoveLinedefAlsoRemovesItsSidedefs()
    {
        var map = BuildTwoRooms();
        var lDiv = map.Linedefs[1];
        var front = lDiv.Front!;
        var back = lDiv.Back!;

        map.RemoveLinedef(lDiv);

        Assert.DoesNotContain(lDiv, map.Linedefs);
        Assert.DoesNotContain(front, map.Sidedefs);
        Assert.DoesNotContain(back, map.Sidedefs);
        Assert.Null(lDiv.Front);
        Assert.Null(lDiv.Back);
        Assert.True(lDiv.IsDisposed);
        Assert.True(front.IsDisposed);
        Assert.True(back.IsDisposed);
    }

    [Fact]
    public void RemoveVertexCascadesToTouchingLinedefs()
    {
        var map = BuildTwoRooms();
        int sidedefsBefore = map.Sidedefs.Count;
        var v1 = map.Vertices[1]; // shared by lA (end) and lDiv (start)

        map.RemoveVertex(v1);

        Assert.DoesNotContain(v1, map.Vertices);
        // Both linedefs touching v1 are gone (lA and lDiv).
        Assert.Empty(map.Linedefs);
        // All three sidedefs (lA front, lDiv front+back) are gone.
        Assert.Equal(sidedefsBefore - 3, map.Sidedefs.Count);
        Assert.Empty(map.Sidedefs);
        Assert.True(v1.IsDisposed);
    }

    [Fact]
    public void RemoveSectorNullsReferencingSidedefs()
    {
        var map = BuildTwoRooms();
        var sB = map.Sectors[1];
        var backSide = map.Linedefs[1].Back!;
        Assert.Same(sB, backSide.Sector);

        map.RemoveSector(sB);

        Assert.DoesNotContain(sB, map.Sectors);
        Assert.Null(backSide.Sector);
        // The sidedef itself survives (orphaned), only its sector reference is cleared.
        Assert.Contains(backSide, map.Sidedefs);
        Assert.True(sB.IsDisposed);
        Assert.False(backSide.IsDisposed);
    }

    [Fact]
    public void BuildIndexesIsConsistentAfterRemoval()
    {
        var map = BuildTwoRooms();
        map.RemoveLinedef(map.Linedefs[1]); // remove the divider
        map.BuildIndexes();

        // Remaining: lA with front in sA. sB now has no sidedefs.
        Assert.Single(map.Linedefs);
        Assert.Single(map.Sectors[0].Sidedefs);
        Assert.Empty(map.Sectors[1].Sidedefs);
        // v1 is still referenced by lA only now.
        Assert.Single(map.Vertices[1].Linedefs);
    }

    [Fact]
    public void RemoveSidedefDetachesFromLinedef()
    {
        var map = BuildTwoRooms();
        var lDiv = map.Linedefs[1];
        var back = lDiv.Back!;

        map.RemoveSidedef(back);

        Assert.Null(lDiv.Back);
        Assert.NotNull(lDiv.Front); // front untouched
        Assert.DoesNotContain(back, map.Sidedefs);
        Assert.True(back.IsDisposed);
        Assert.False(lDiv.IsDisposed);
    }

    [Fact]
    public void RemoveThingRemovesIt()
    {
        var map = new MapSet();
        var t = map.AddThing(new Vector2D(0, 0), 1);
        map.RemoveThing(t);
        Assert.Empty(map.Things);
        Assert.True(t.IsDisposed);
    }

    [Fact]
    public void RemoveIgnoresForeignThingWithoutDisposingIt()
    {
        var map = new MapSet();
        var foreign = new Thing(new Vector2D(0, 0), 1);

        map.RemoveThing(foreign);

        Assert.False(foreign.IsDisposed);
    }

    [Fact]
    public void DisposeMarksAllElementsAndClearsCollections()
    {
        var map = BuildTwoRooms();
        var vertex = map.Vertices[0];
        var line = map.Linedefs[0];
        var side = map.Sidedefs[0];
        var sector = map.Sectors[0];
        var thing = map.AddThing(new Vector2D(16, 16), 3004);
        map.Namespace = "ZDoom";
        map.Fields["author"] = "tester";

        map.Dispose();
        map.Dispose();

        Assert.True(map.IsDisposed);
        Assert.True(vertex.IsDisposed);
        Assert.True(line.IsDisposed);
        Assert.True(side.IsDisposed);
        Assert.True(sector.IsDisposed);
        Assert.True(thing.IsDisposed);
        Assert.Empty(map.Vertices);
        Assert.Empty(map.Linedefs);
        Assert.Empty(map.Sidedefs);
        Assert.Empty(map.Sectors);
        Assert.Empty(map.Things);
        Assert.Empty(map.Fields);
        Assert.Equal("", map.Namespace);
    }

    [Fact]
    public void MarkHelpersTrackAndClearAllElementTypes()
    {
        var map = BuildTwoRooms();
        var thing = map.AddThing(new Vector2D(32, 32), 3001);

        map.Vertices[0].Marked = true;
        map.Linedefs[0].Marked = true;
        map.Sidedefs[0].Marked = true;
        map.Sectors[0].Marked = true;
        thing.Marked = true;
        map.Vertices[1].Selected = true;

        Assert.Equal(new[] { map.Vertices[0] }, map.GetMarkedVertices());
        Assert.Equal(new[] { map.Linedefs[0] }, map.GetMarkedLinedefs());
        Assert.Equal(new[] { map.Sidedefs[0] }, map.GetMarkedSidedefs());
        Assert.Equal(new[] { map.Sectors[0] }, map.GetMarkedSectors());
        Assert.Equal(new[] { thing }, map.GetMarkedThings());
        Assert.Equal(1, map.MarkedVerticesCount);
        Assert.Equal(1, map.MarkedLinedefsCount);
        Assert.Equal(1, map.MarkedSidedefsCount);
        Assert.Equal(1, map.MarkedSectorsCount);
        Assert.Equal(1, map.MarkedThingsCount);

        map.ClearAllMarked();

        Assert.Equal(0, map.MarkedVerticesCount);
        Assert.Equal(0, map.MarkedLinedefsCount);
        Assert.Equal(0, map.MarkedSidedefsCount);
        Assert.Equal(0, map.MarkedSectorsCount);
        Assert.Equal(0, map.MarkedThingsCount);
        Assert.True(map.Vertices[1].Selected);
    }

    [Fact]
    public void LookupHelpersReturnReferenceIndexesAndMembership()
    {
        var map = BuildTwoRooms();
        var thing = map.AddThing(new Vector2D(16, 16), 3004);

        Assert.Equal(1, map.IndexOfVertex(map.Vertices[1]));
        Assert.Equal(1, map.IndexOfLinedef(map.Linedefs[1]));
        Assert.Equal(2, map.IndexOfSidedef(map.Sidedefs[2]));
        Assert.Equal(1, map.IndexOfSector(map.Sectors[1]));
        Assert.Equal(0, map.IndexOfThing(thing));

        Assert.True(map.ContainsVertex(map.Vertices[0]));
        Assert.True(map.ContainsLinedef(map.Linedefs[0]));
        Assert.True(map.ContainsSidedef(map.Sidedefs[0]));
        Assert.True(map.ContainsSector(map.Sectors[0]));
        Assert.True(map.ContainsThing(thing));

        Assert.Equal(-1, map.IndexOfVertex(new Vertex(new Vector2D(0, 0))));
        Assert.False(map.ContainsThing(new Thing(new Vector2D(16, 16), 3004)));
    }

    [Fact]
    public void GetByIndexHelpersReturnElementsOrNull()
    {
        var map = BuildTwoRooms();
        var thing = map.AddThing(new Vector2D(16, 16), 3004);

        Assert.Same(map.Vertices[1], map.GetVertexByIndex(1));
        Assert.Same(map.Linedefs[1], map.GetLinedefByIndex(1));
        Assert.Same(map.Sidedefs[2], map.GetSidedefByIndex(2));
        Assert.Same(map.Sectors[1], map.GetSectorByIndex(1));
        Assert.Same(thing, map.GetThingByIndex(0));

        Assert.Null(map.GetVertexByIndex(-1));
        Assert.Null(map.GetLinedefByIndex(map.Linedefs.Count));
        Assert.Null(map.GetSidedefByIndex(map.Sidedefs.Count));
        Assert.Null(map.GetSectorByIndex(map.Sectors.Count));
        Assert.Null(map.GetThingByIndex(map.Things.Count));
    }

    [Fact]
    public void ChangeIndexHelpersReorderOwnedElements()
    {
        var map = BuildTwoRooms();
        var thing0 = map.AddThing(new Vector2D(16, 16), 3004);
        var thing1 = map.AddThing(new Vector2D(32, 16), 3005);
        var vertex = map.Vertices[0];
        var line = map.Linedefs[0];
        var side = map.Sidedefs[0];
        var sector = map.Sectors[0];

        Assert.True(map.ChangeVertexIndex(vertex, 2));
        Assert.True(map.ChangeLinedefIndex(line, 1));
        Assert.True(map.ChangeSidedefIndex(side, 2));
        Assert.True(map.ChangeSectorIndex(sector, 1));
        Assert.True(map.ChangeThingIndex(thing0, 1));

        Assert.Same(vertex, map.Vertices[2]);
        Assert.Same(line, map.Linedefs[1]);
        Assert.Same(side, map.Sidedefs[2]);
        Assert.Same(sector, map.Sectors[1]);
        Assert.Same(thing0, map.Things[1]);
        Assert.Same(thing1, map.Things[0]);
        Assert.Equal(0, map.Sectors[0].Index);
        Assert.Equal(1, map.Sectors[1].Index);
    }

    [Fact]
    public void ChangeIndexHelpersRejectForeignAndOutOfRangeElements()
    {
        var map = BuildTwoRooms();
        var vertex = map.Vertices[0];

        Assert.False(map.ChangeVertexIndex(new Vertex(new Vector2D(0, 0)), 0));
        Assert.False(map.ChangeVertexIndex(vertex, -1));
        Assert.False(map.ChangeVertexIndex(vertex, map.Vertices.Count));
        Assert.True(map.ChangeVertexIndex(vertex, 0));
        Assert.Same(vertex, map.Vertices[0]);
    }

    [Fact]
    public void SelectedSidedefsCountMatchesSelectedSidedefs()
    {
        var map = BuildTwoRooms();
        map.Sidedefs[0].Selected = true;
        map.Sidedefs[2].Selected = true;

        Assert.Equal(2, map.SelectedSidedefsCount);
        Assert.Equal(new[] { map.Sidedefs[0], map.Sidedefs[2] }, map.GetSelectedSidedefs());
    }

    [Fact]
    public void MarkHelpersCanSetAndInvertAllElementTypes()
    {
        var map = BuildTwoRooms();
        var thing = map.AddThing(new Vector2D(16, 16), 3004);

        map.ClearAllMarked(true);

        Assert.True(map.Vertices[0].Marked);
        Assert.True(map.Linedefs[0].Marked);
        Assert.True(map.Sidedefs[0].Marked);
        Assert.True(map.Sectors[0].Marked);
        Assert.True(thing.Marked);

        map.Vertices[0].Marked = false;
        map.Linedefs[0].Marked = false;
        map.Sidedefs[0].Marked = false;
        map.Sectors[0].Marked = false;
        thing.Marked = false;

        map.InvertAllMarked();

        Assert.True(map.Vertices[0].Marked);
        Assert.True(map.Linedefs[0].Marked);
        Assert.True(map.Sidedefs[0].Marked);
        Assert.True(map.Sectors[0].Marked);
        Assert.True(thing.Marked);
        Assert.False(map.Vertices[1].Marked);
    }
}
