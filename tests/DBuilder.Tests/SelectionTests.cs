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
    public void GetSelectedCanReturnEitherSelectionState()
    {
        var map = BuildMap();
        map.Vertices[0].Selected = true;
        map.Linedefs[1].Selected = true;

        Assert.Equal(new[] { map.Vertices[0] }, map.GetSelectedVertices(selected: true));
        Assert.Equal(new[] { map.Vertices[1], map.Vertices[2] }, map.GetSelectedVertices(selected: false));
        Assert.Equal(new[] { map.Linedefs[1] }, map.GetSelectedLinedefs(selected: true));
        Assert.Equal(new[] { map.Linedefs[0] }, map.GetSelectedLinedefs(selected: false));
    }

    [Fact]
    public void GetSidedefsFromSelectedLinedefsUsesLinedefSelection()
    {
        var map = BuildTwoSidedMap();
        map.Linedefs[0].Selected = true;
        map.Sidedefs[2].Selected = true;

        Assert.Equal(new[] { map.Sidedefs[0], map.Sidedefs[1] }, map.GetSidedefsFromSelectedLinedefs(selected: true));
        Assert.Equal(new[] { map.Sidedefs[2] }, map.GetSidedefsFromSelectedLinedefs(selected: false));
    }

    [Fact]
    public void KeepSelectedLinedefsBySidednessKeepsOnlySingleSidedLines()
    {
        var map = BuildTwoSidedMap();
        map.Linedefs[0].Selected = true;
        map.Linedefs[1].Selected = true;

        int kept = map.KeepSelectedLinedefsBySidedness(doubleSided: false);

        Assert.Equal(1, kept);
        Assert.Equal(new[] { map.Linedefs[1] }, map.GetSelectedLinedefs());
    }

    [Fact]
    public void KeepSelectedLinedefsBySidednessKeepsOnlyDoubleSidedLines()
    {
        var map = BuildTwoSidedMap();
        map.Linedefs[0].Selected = true;
        map.Linedefs[1].Selected = true;

        int kept = map.KeepSelectedLinedefsBySidedness(doubleSided: true);

        Assert.Equal(1, kept);
        Assert.Equal(new[] { map.Linedefs[0] }, map.GetSelectedLinedefs());
    }

    [Fact]
    public void CountsMatchSelection()
    {
        var map = BuildMap();
        map.Vertices[0].Selected = true;
        map.Things[0].Selected = true;
        Assert.Equal(1, map.SelectedVerticesCount);
        Assert.Equal(1, map.SelectedVerticessCount);
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
    public void SelectAllPerTypeOnlySelectsRequestedElementType()
    {
        var map = BuildMap();

        map.SelectAllVertices();
        Assert.Equal(map.Vertices.Count, map.SelectedVerticesCount);
        Assert.Equal(0, map.SelectedLinedefsCount);
        Assert.Equal(0, map.SelectedSectorsCount);
        Assert.Equal(0, map.SelectedThingsCount);

        map.SelectAllThings();
        Assert.Equal(map.Vertices.Count, map.SelectedVerticesCount);
        Assert.Equal(map.Things.Count, map.SelectedThingsCount);
        Assert.Equal(0, map.SelectedLinedefsCount);
    }

    [Fact]
    public void InvertSelectedPerTypeOnlyFlipsRequestedElementType()
    {
        var map = BuildMap();
        map.Vertices[0].Selected = true;
        map.Linedefs[0].Selected = true;

        map.InvertSelectedVertices();

        Assert.Equal(new[] { map.Vertices[1], map.Vertices[2] }, map.GetSelectedVertices());
        Assert.Equal(new[] { map.Linedefs[0] }, map.GetSelectedLinedefs());
    }

    [Fact]
    public void SelectMarkedGeometrySetsSelectionForMatchingMarks()
    {
        var map = BuildMap();
        map.Vertices[0].Marked = true;
        map.Linedefs[0].Marked = true;
        map.Sidedefs[0].Marked = true;
        map.Sectors[0].Marked = true;
        map.Things[0].Marked = true;
        map.Vertices[1].Selected = true;
        map.AddSidedef(map.Linedefs[1], true, map.Sectors[0]).Selected = true;

        map.SelectMarkedGeometry(mark: true, select: true);

        Assert.True(map.Vertices[0].Selected);
        Assert.True(map.Vertices[1].Selected);
        Assert.True(map.Linedefs[0].Selected);
        Assert.True(map.Sidedefs[0].Selected);
        Assert.True(map.Sidedefs[1].Selected);
        Assert.True(map.Sectors[0].Selected);
        Assert.True(map.Things[0].Selected);

        map.SelectMarkedGeometry(mark: true, select: false);

        Assert.False(map.Vertices[0].Selected);
        Assert.True(map.Vertices[1].Selected);
        Assert.False(map.Linedefs[0].Selected);
        Assert.False(map.Sidedefs[0].Selected);
        Assert.True(map.Sidedefs[1].Selected);
        Assert.False(map.Sectors[0].Selected);
        Assert.False(map.Things[0].Selected);
    }

    [Fact]
    public void GetMarkedCanReturnEitherMarkState()
    {
        var map = BuildMap();
        map.Vertices[0].Marked = true;
        map.Sidedefs[0].Marked = true;

        Assert.Equal(new[] { map.Vertices[0] }, map.GetMarkedVertices(marked: true));
        Assert.Equal(new[] { map.Vertices[1], map.Vertices[2] }, map.GetMarkedVertices(marked: false));
        Assert.Equal(new[] { map.Sidedefs[0] }, map.GetMarkedSidedefs(marked: true));
        Assert.Empty(map.GetMarkedSidedefs(marked: false));
    }

    [Fact]
    public void MarkSelectedHelpersSetMarksBySelectionState()
    {
        var map = BuildMap();
        map.Vertices[0].Selected = true;
        map.Linedefs[0].Selected = true;
        map.Sidedefs[0].Selected = true;
        map.Sectors[0].Selected = true;
        map.Things[0].Selected = true;

        map.MarkSelectedVertices(selected: true, mark: true);
        map.MarkSelectedLinedefs(selected: true, mark: true);
        map.MarkSelectedSidedefs(selected: true, mark: true);
        map.MarkSelectedSectors(selected: true, mark: true);
        map.MarkSelectedThings(selected: true, mark: true);

        Assert.Equal(new[] { map.Vertices[0] }, map.GetMarkedVertices());
        Assert.Equal(new[] { map.Linedefs[0] }, map.GetMarkedLinedefs());
        Assert.Equal(new[] { map.Sidedefs[0] }, map.GetMarkedSidedefs());
        Assert.Equal(new[] { map.Sectors[0] }, map.GetMarkedSectors());
        Assert.Equal(new[] { map.Things[0] }, map.GetMarkedThings());

        map.MarkSelectedVertices(selected: false, mark: true);

        Assert.Equal(map.Vertices, map.GetMarkedVertices());
    }

    [Fact]
    public void MarkSidedefsFromLinedefsAndSectorsPropagatesMarks()
    {
        var map = BuildTwoSidedMap();
        map.Linedefs[0].Marked = true;
        map.Sectors[0].Marked = true;

        map.MarkSidedefsFromLinedefs(matchMark: true, setMark: true);

        Assert.True(map.Sidedefs[0].Marked);
        Assert.True(map.Sidedefs[1].Marked);
        Assert.False(map.Sidedefs[2].Marked);

        map.ClearMarkedSidedefs();
        map.MarkSidedefsFromSectors(matchMark: true, setMark: true);

        Assert.True(map.Sidedefs[0].Marked);
        Assert.False(map.Sidedefs[1].Marked);
        Assert.True(map.Sidedefs[2].Marked);
    }

    [Fact]
    public void GetVerticesFromLinesMarksUsesAnyOrAllTouchingLines()
    {
        var map = BuildTwoSidedMap();
        map.Linedefs[0].Marked = true;

        Assert.Equal(new[] { map.Vertices[0], map.Vertices[1] }, map.GetVerticesFromLinesMarks(mark: true));
        Assert.Equal(new[] { map.Vertices[0] }, map.GetVerticesFromAllLinesMarks(mark: true));
        Assert.Equal(new[] { map.Vertices[2] }, map.GetVerticesFromAllLinesMarks(mark: false));
    }

    [Fact]
    public void GetVerticesFromSectorsMarksUsesTouchingLineSectors()
    {
        var map = BuildTwoSidedMap();
        map.Sectors[1].Marked = true;

        Assert.Equal(new[] { map.Vertices[0], map.Vertices[1] }, map.GetVerticesFromSectorsMarks(mark: true));
        Assert.Equal(map.Vertices, map.GetVerticesFromSectorsMarks(mark: false));
    }

    [Fact]
    public void LinedefsFromMarkedVerticesClassifiesStableUnstableAndUnmarkedLines()
    {
        var map = BuildTwoSidedMap();
        map.Vertices[0].Marked = true;
        map.Vertices[1].Marked = true;

        Assert.Equal(new[] { map.Linedefs[0] }, map.LinedefsFromMarkedVertices(
            includeUnmarked: false,
            includeStable: true,
            includeUnstable: false));
        Assert.Equal(new[] { map.Linedefs[1] }, map.LinedefsFromMarkedVertices(
            includeUnmarked: false,
            includeStable: false,
            includeUnstable: true));
        Assert.Empty(map.LinedefsFromMarkedVertices(
            includeUnmarked: true,
            includeStable: false,
            includeUnstable: false));
        Assert.Equal(map.Linedefs, map.LinedefsFromMarkedVertices(
            includeUnmarked: true,
            includeStable: true,
            includeUnstable: true));
    }

    [Fact]
    public void MarkAllSelectedGeometryMarksDirectGeometryAndLineVertexExpansion()
    {
        var map = BuildTwoSidedMap();
        map.Vertices[0].Selected = true;
        map.Vertices[1].Selected = true;
        map.Linedefs[1].Selected = true;
        map.AddThing(new Vector2D(32, 16), 3001).Selected = true;

        map.MarkAllSelectedGeometry(
            mark: true,
            linedefsFromVertices: true,
            verticesFromLinedefs: true,
            sectorsFromLinedefs: false,
            sidedefsFromSectors: false);

        Assert.Equal(map.Vertices, map.GetMarkedVertices());
        Assert.Equal(map.Linedefs, map.GetMarkedLinedefs());
        Assert.Equal(new[] { map.Sidedefs[0], map.Sidedefs[1], map.Sidedefs[2] }, map.GetMarkedSidedefs());
        Assert.Empty(map.GetMarkedSectors());
        Assert.Equal(map.Things, map.GetMarkedThings());
    }

    [Fact]
    public void MarkAllSelectedGeometryCanMarkSectorsAndSidedefsFromClosedSelectedLines()
    {
        var map = BuildClosedSectorMap();
        foreach (var line in map.Linedefs) line.Selected = true;

        map.MarkAllSelectedGeometry(
            mark: true,
            linedefsFromVertices: false,
            verticesFromLinedefs: false,
            sectorsFromLinedefs: true,
            sidedefsFromSectors: true);

        Assert.Equal(new[] { map.Sectors[0] }, map.GetMarkedSectors());
        Assert.Equal(map.Sidedefs, map.GetMarkedSidedefs());
    }

    [Fact]
    public void ConvertSelectionToVerticesSelectsLineAndSectorVertices()
    {
        var map = BuildClosedSectorMap();
        map.Linedefs[0].Selected = true;
        map.Sectors[0].Selected = true;

        map.ConvertSelection(SelectionType.Vertices);

        Assert.Equal(map.Vertices, map.GetSelectedVertices());
        Assert.Empty(map.GetSelectedLinedefs());
        Assert.Empty(map.GetSelectedSectors());
    }

    [Fact]
    public void ConvertSelectionToLinedefsUsesStableVertexPairsAndSelectedSectors()
    {
        var map = BuildClosedSectorMap();
        map.Vertices[0].Selected = true;
        map.Vertices[1].Selected = true;
        map.Sectors[0].Selected = true;

        map.ConvertSelection(SelectionType.Linedefs);

        Assert.Equal(map.Linedefs, map.GetSelectedLinedefs());
        Assert.Empty(map.GetSelectedVertices());
        Assert.Empty(map.GetSelectedSectors());
    }

    [Fact]
    public void ConvertSelectionToSectorsSelectsClosedSelectedLineSectors()
    {
        var map = BuildClosedSectorMap();
        foreach (var line in map.Linedefs) line.Selected = true;

        map.ConvertSelection(SelectionType.Sectors);

        Assert.Equal(new[] { map.Sectors[0] }, map.GetSelectedSectors());
        Assert.Equal(map.Linedefs, map.GetSelectedLinedefs());
        Assert.Empty(map.GetSelectedVertices());
    }

    [Fact]
    public void ConvertSelectionRejectsUnsupportedTargets()
    {
        var map = BuildMap();

        Assert.Throws<ArgumentException>(() => map.ConvertSelection(SelectionType.Things));
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

    private static MapSet BuildTwoSidedMap()
    {
        var map = new MapSet();
        var front = map.AddSector();
        var back = map.AddSector();
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(64, 0));
        var v2 = map.AddVertex(new Vector2D(128, 0));
        var twoSided = map.AddLinedef(v0, v1);
        var oneSided = map.AddLinedef(v1, v2);
        map.AddSidedef(twoSided, true, front);
        map.AddSidedef(twoSided, false, back);
        map.AddSidedef(oneSided, true, front);
        map.BuildIndexes();
        return map;
    }

    private static MapSet BuildClosedSectorMap()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(64, 0));
        var v2 = map.AddVertex(new Vector2D(64, 64));
        var v3 = map.AddVertex(new Vector2D(0, 64));
        var l0 = map.AddLinedef(v0, v1);
        var l1 = map.AddLinedef(v1, v2);
        var l2 = map.AddLinedef(v2, v3);
        var l3 = map.AddLinedef(v3, v0);
        map.AddSidedef(l0, true, sector);
        map.AddSidedef(l1, true, sector);
        map.AddSidedef(l2, true, sector);
        map.AddSidedef(l3, true, sector);
        map.BuildIndexes();
        return map;
    }
}
