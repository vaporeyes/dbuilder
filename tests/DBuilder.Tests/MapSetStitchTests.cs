// ABOUTME: Tests for MapSet.SplitLinedefsAtVertices - welding T-junctions by splitting lines at vertices on them.
// ABOUTME: Covers an interior split, endpoint/no-op cases, sidedef preservation, and multi-line junctions.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class MapSetStitchTests
{
    [Fact]
    public void SplitsLineAtInteriorVertex()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        map.AddLinedef(a, b);
        var mid = map.AddVertex(new Vector2D(50, 0)); // sits on the line interior
        map.BuildIndexes();

        int n = map.SplitLinedefsAtVertices(0.5);
        map.BuildIndexes();

        Assert.Equal(1, n);
        Assert.Equal(2, map.Linedefs.Count);
        // The two halves meet at the mid vertex.
        Assert.Contains(map.Linedefs, l => ReferenceEquals(l.End, mid) || ReferenceEquals(l.Start, mid));
    }

    [Fact]
    public void DoesNotSplitAtEndpoints()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        map.AddLinedef(a, b);
        map.BuildIndexes();

        Assert.Equal(0, map.SplitLinedefsAtVertices(0.5));
        Assert.Single(map.Linedefs);
    }

    [Fact]
    public void DoesNotSplitOffLineVertex()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        map.AddLinedef(a, b);
        map.AddVertex(new Vector2D(50, 20)); // not on the line
        map.BuildIndexes();

        Assert.Equal(0, map.SplitLinedefsAtVertices(0.5));
        Assert.Single(map.Linedefs);
    }

    [Fact]
    public void SplitPreservesSidedefAndSector()
    {
        var map = new MapSet();
        var s = map.AddSector();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var l = map.AddLinedef(a, b);
        map.AddSidedef(l, true, s);
        l.Front!.MidTexture = "WALL";
        map.AddVertex(new Vector2D(50, 0));
        map.BuildIndexes();

        map.SplitLinedefsAtVertices(0.5);
        map.BuildIndexes();

        Assert.Equal(2, map.Linedefs.Count);
        foreach (var line in map.Linedefs)
        {
            Assert.NotNull(line.Front);
            Assert.Same(s, line.Front!.Sector);
            Assert.Equal("WALL", line.Front.MidTexture);
        }
    }

    [Fact]
    public void WeldsDrawnLineCrossingTwoExistingWalls()
    {
        // Two parallel horizontal walls; a vertex placed on each by a "drawn" connector should split both.
        var map = new MapSet();
        map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(100, 0)));
        map.AddLinedef(map.AddVertex(new Vector2D(0, 80)), map.AddVertex(new Vector2D(100, 80)));
        map.AddVertex(new Vector2D(50, 0));
        map.AddVertex(new Vector2D(50, 80));
        map.BuildIndexes();

        int n = map.SplitLinedefsAtVertices(0.5);
        Assert.Equal(2, n);
        Assert.Equal(4, map.Linedefs.Count);
    }

    [Fact]
    public void SplitLinesByVerticesSplitsOnlyProvidedLines()
    {
        var map = new MapSet();
        var first = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(100, 0)));
        var second = map.AddLinedef(map.AddVertex(new Vector2D(0, 80)), map.AddVertex(new Vector2D(100, 80)));
        var secondStart = second.Start;
        var secondEnd = second.End;
        var firstMid = map.AddVertex(new Vector2D(50, 0));
        map.AddVertex(new Vector2D(50, 80));
        var lines = new HashSet<Linedef> { first };
        var vertices = new[] { firstMid };
        var changed = new HashSet<Linedef>();

        int splits = map.SplitLinesByVertices(lines, vertices, 0.5, changed);

        Assert.Equal(1, splits);
        Assert.Equal(3, map.Linedefs.Count);
        Assert.Equal(2, lines.Count);
        Assert.Contains(first, changed);
        Assert.Contains(lines, line => !ReferenceEquals(line, first));
        Assert.Same(secondStart, second.Start);
        Assert.Same(secondEnd, second.End);
        Assert.DoesNotContain(second, changed);
    }

    [Fact]
    public void SplitLinesByVerticesReportsBothLineHalves()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(100, 0)));
        var mid = map.AddVertex(new Vector2D(50, 0));
        var changed = new List<Linedef>();

        int splits = map.SplitLinesByVertices(map.Linedefs, new[] { mid }, 0.5, changed);

        Assert.Equal(1, splits);
        Assert.Equal(2, changed.Count);
        Assert.Contains(line, changed);
        Assert.Contains(changed, changedLine => !ReferenceEquals(changedLine, line));
    }

    [Fact]
    public void SplitLinesByLinesSplitsIntersectingFixedAndChangedLines()
    {
        var map = new MapSet();
        var fixedLine = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(100, 0)));
        var changedLine = map.AddLinedef(map.AddVertex(new Vector2D(50, -50)), map.AddVertex(new Vector2D(50, 50)));
        var fixedLines = new HashSet<Linedef> { fixedLine };
        var changedLines = new HashSet<Linedef> { changedLine };

        int splits = map.SplitLinesByLines(fixedLines, changedLines);

        Assert.Equal(1, splits);
        Assert.Equal(4, map.Linedefs.Count);
        Assert.Equal(3, changedLines.Count);
        Assert.Contains(map.Vertices, vertex => vertex.Position == new Vector2D(50, 0));
        Assert.Contains(changedLines, line => !ReferenceEquals(line, changedLine));
        Assert.DoesNotContain(fixedLine, changedLines);
    }

    [Fact]
    public void SplitLinesByLinesReusesExistingIntersectionVertex()
    {
        var map = new MapSet();
        var fixedLine = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(100, 0)));
        var changedLine = map.AddLinedef(map.AddVertex(new Vector2D(50, -50)), map.AddVertex(new Vector2D(50, 50)));
        var existing = map.AddVertex(new Vector2D(50, 0));

        int splits = map.SplitLinesByLines(new[] { fixedLine }, new HashSet<Linedef> { changedLine });

        Assert.Equal(1, splits);
        Assert.Equal(5, map.Vertices.Count);
        Assert.Contains(map.Linedefs, line => ReferenceEquals(line.Start, existing) || ReferenceEquals(line.End, existing));
    }

    [Fact]
    public void SplitLinesByLinesSkipsSharedEndpoints()
    {
        var map = new MapSet();
        var shared = map.AddVertex(new Vector2D(50, 0));
        var fixedLine = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), shared);
        var changedLine = map.AddLinedef(shared, map.AddVertex(new Vector2D(50, 50)));

        int splits = map.SplitLinesByLines(new[] { fixedLine }, new HashSet<Linedef> { changedLine });

        Assert.Equal(0, splits);
        Assert.Equal(2, map.Linedefs.Count);
    }

    [Fact]
    public void StitchSelectedGeometryJoinsSelectedVerticesToFixedVertices()
    {
        var map = new MapSet();
        var fixedVertex = map.AddVertex(new Vector2D(0, 0));
        var movingVertex = map.AddVertex(new Vector2D(0.25, 0));
        movingVertex.Selected = true;

        GeometryStitchResult result = map.StitchSelectedGeometry(0.5);

        Assert.Equal(1, result.JoinedVertices);
        Assert.Equal(new Vector2D(0, 0), movingVertex.Position);
        Assert.Contains(movingVertex, map.Vertices);
        Assert.DoesNotContain(fixedVertex, map.Vertices);
    }

    [Fact]
    public void StitchSelectedGeometrySplitsSelectedLinesAgainstFixedLines()
    {
        var map = new MapSet();
        var fixedLine = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(100, 0)));
        var movingLine = map.AddLinedef(map.AddVertex(new Vector2D(50, -50)), map.AddVertex(new Vector2D(50, 50)));
        movingLine.Selected = true;

        GeometryStitchResult result = map.StitchSelectedGeometry(0.5);

        Assert.Equal(1, result.LineLineSplits);
        Assert.Equal(4, map.Linedefs.Count);
        Assert.Contains(map.Vertices, vertex => vertex.Position == new Vector2D(50, 0));
        Assert.Contains(map.Linedefs, line => ReferenceEquals(line.Start, fixedLine.Start) && line.End.Position == new Vector2D(50, 0));
    }

    [Fact]
    public void StitchSelectedGeometryFlipsBackwardSelectedLines()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        var sector = map.AddSector();
        map.AddSidedef(line, false, sector);
        line.Selected = true;

        GeometryStitchResult result = map.StitchSelectedGeometry(0.5);

        Assert.Equal(1, result.FlippedBackwardLinedefs);
        Assert.NotNull(line.Front);
        Assert.Null(line.Back);
    }

    [Fact]
    public void JoinOverlappingLinedefsRemovesDuplicateAndTransfersMissingSide()
    {
        var map = new MapSet();
        var frontSector = map.AddSector();
        var backSector = map.AddSector();
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(64, 0));
        var keep = map.AddLinedef(start, end);
        var duplicate = map.AddLinedef(start, end);
        var front = map.AddSidedef(keep, true, frontSector);
        var back = map.AddSidedef(duplicate, false, backSector);
        var changed = new HashSet<Linedef> { keep, duplicate };

        int removed = map.JoinOverlappingLinedefs(changed);

        Assert.Equal(1, removed);
        Assert.Single(map.Linedefs);
        Assert.Equal(new[] { keep }, changed);
        Assert.Same(front, keep.Front);
        Assert.Same(back, keep.Back);
        Assert.Same(keep, back.Line);
        Assert.True(duplicate.IsDisposed);
    }

    [Fact]
    public void JoinOverlappingLinedefsMapsOppositeDirectionSides()
    {
        var map = new MapSet();
        var frontSector = map.AddSector();
        var backSector = map.AddSector();
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(64, 0));
        var keep = map.AddLinedef(start, end);
        var duplicate = map.AddLinedef(end, start);
        var front = map.AddSidedef(duplicate, true, backSector);
        var back = map.AddSidedef(duplicate, false, frontSector);
        var changed = new HashSet<Linedef> { keep, duplicate };

        int removed = map.JoinOverlappingLinedefs(changed);

        Assert.Equal(1, removed);
        Assert.Same(back, keep.Front);
        Assert.Same(front, keep.Back);
        Assert.True(back.IsFront);
        Assert.False(front.IsFront);
    }

    [Fact]
    public void StitchSelectedGeometryReportsJoinedOverlappingLines()
    {
        var map = new MapSet();
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(64, 0));
        var first = map.AddLinedef(start, end);
        var second = map.AddLinedef(start, end);
        first.Selected = true;
        second.Selected = true;

        GeometryStitchResult result = map.StitchSelectedGeometry(0.5);

        Assert.Equal(1, result.JoinedOverlappingLinedefs);
        Assert.Single(map.Linedefs);
    }

    [Fact]
    public void CorrectOuterSidedefsAddsMissingBackSideInsideSector()
    {
        var (map, sector) = BuildSquare(128);
        var line = map.AddLinedef(map.AddVertex(new Vector2D(32, 32)), map.AddVertex(new Vector2D(96, 32)));
        var front = map.AddSidedef(line, true, sector);
        front.MidTexture = "STONE";
        front.OffsetX = 12;

        int created = map.CorrectOuterSidedefs(new[] { line });

        Assert.Equal(1, created);
        Assert.NotNull(line.Back);
        Assert.Same(sector, line.Back!.Sector);
        Assert.Equal("STONE", line.Back.MidTexture);
        Assert.Equal(12, line.Back.OffsetX);
    }

    [Fact]
    public void CorrectOuterSidedefsAddsMissingFrontSideInsideSector()
    {
        var (map, sector) = BuildSquare(128);
        var line = map.AddLinedef(map.AddVertex(new Vector2D(32, 32)), map.AddVertex(new Vector2D(96, 32)));
        var back = map.AddSidedef(line, false, sector);
        back.MidTexture = "BRICK";

        int created = map.CorrectOuterSidedefs(new[] { line });

        Assert.Equal(1, created);
        Assert.NotNull(line.Front);
        Assert.Same(sector, line.Front!.Sector);
        Assert.Equal("BRICK", line.Front.MidTexture);
    }

    [Fact]
    public void CorrectOuterSidedefsSkipsLineCrossingSectorBoundary()
    {
        var (map, sector) = BuildSquare(128);
        var line = map.AddLinedef(map.AddVertex(new Vector2D(32, 32)), map.AddVertex(new Vector2D(160, 32)));
        map.AddSidedef(line, true, sector);

        int created = map.CorrectOuterSidedefs(new[] { line });

        Assert.Equal(0, created);
        Assert.Null(line.Back);
    }

    [Fact]
    public void StitchSelectedGeometryClassicDoesNotCorrectOuterSidedefs()
    {
        var (map, sector) = BuildSquare(128);
        var line = map.AddLinedef(map.AddVertex(new Vector2D(32, 32)), map.AddVertex(new Vector2D(96, 32)));
        map.AddSidedef(line, true, sector);
        line.Selected = true;

        GeometryStitchResult result = map.StitchSelectedGeometry(MergeGeometryMode.Classic, 0.5);

        Assert.Equal(0, result.CorrectedOuterSidedefs);
        Assert.Null(line.Back);
    }

    [Fact]
    public void StitchSelectedGeometryMergeCorrectsOuterSidedefs()
    {
        var (map, sector) = BuildSquare(128);
        var line = map.AddLinedef(map.AddVertex(new Vector2D(32, 32)), map.AddVertex(new Vector2D(96, 32)));
        map.AddSidedef(line, true, sector);
        line.Selected = true;

        GeometryStitchResult result = map.StitchSelectedGeometry(MergeGeometryMode.Merge, 0.5);

        Assert.Equal(1, result.CorrectedOuterSidedefs);
        Assert.NotNull(line.Back);
        Assert.Same(sector, line.Back!.Sector);
    }

    [Fact]
    public void RemoveLinedefsInsideSectorsRemovesFullyContainedLine()
    {
        var (map, sector) = BuildSquare(128);
        var line = map.AddLinedef(map.AddVertex(new Vector2D(32, 32)), map.AddVertex(new Vector2D(96, 32)));
        var changed = new HashSet<Linedef> { line };

        int removed = map.RemoveLinedefsInsideSectors(new[] { line }, new[] { sector }, changed);

        Assert.Equal(1, removed);
        Assert.True(line.IsDisposed);
        Assert.DoesNotContain(line, map.Linedefs);
        Assert.Empty(changed);
    }

    [Fact]
    public void RemoveLinedefsInsideSectorsKeepsLineReferencingChangedSector()
    {
        var (map, sector) = BuildSquare(128);
        var line = map.AddLinedef(map.AddVertex(new Vector2D(32, 32)), map.AddVertex(new Vector2D(96, 32)));
        map.AddSidedef(line, true, sector);

        int removed = map.RemoveLinedefsInsideSectors(new[] { line }, new[] { sector });

        Assert.Equal(0, removed);
        Assert.Contains(line, map.Linedefs);
    }

    [Fact]
    public void StitchSelectedGeometryReplaceRemovesInteriorLinesFromReplacedSector()
    {
        var (map, sector) = BuildSquare(128);
        var interior = map.AddLinedef(map.AddVertex(new Vector2D(32, 32)), map.AddVertex(new Vector2D(96, 32)));
        SelectSectorBoundary(sector);

        GeometryStitchResult result = map.StitchSelectedGeometry(MergeGeometryMode.Replace, 0.5);

        Assert.Equal(1, result.RemovedInteriorLinedefs);
        Assert.True(interior.IsDisposed);
        Assert.DoesNotContain(interior, map.Linedefs);
    }

    [Fact]
    public void StitchSelectedGeometryMergeKeepsInteriorLinesInReplacedSector()
    {
        var (map, sector) = BuildSquare(128);
        var interior = map.AddLinedef(map.AddVertex(new Vector2D(32, 32)), map.AddVertex(new Vector2D(96, 32)));
        SelectSectorBoundary(sector);

        GeometryStitchResult result = map.StitchSelectedGeometry(MergeGeometryMode.Merge, 0.5);

        Assert.Equal(0, result.RemovedInteriorLinedefs);
        Assert.Contains(interior, map.Linedefs);
    }

    private static (MapSet map, Sector sector) BuildSquare(double size)
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(size, 0));
        var v2 = map.AddVertex(new Vector2D(size, size));
        var v3 = map.AddVertex(new Vector2D(0, size));

        map.AddSidedef(map.AddLinedef(v1, v0), true, sector);
        map.AddSidedef(map.AddLinedef(v0, v3), true, sector);
        map.AddSidedef(map.AddLinedef(v3, v2), true, sector);
        map.AddSidedef(map.AddLinedef(v2, v1), true, sector);
        map.BuildIndexes();
        return (map, sector);
    }

    private static void SelectSectorBoundary(Sector sector)
    {
        sector.Selected = true;
        foreach (var side in sector.Sidedefs)
        {
            side.Line.Selected = true;
            side.Line.Start.Selected = true;
            side.Line.End.Selected = true;
        }
    }
}
