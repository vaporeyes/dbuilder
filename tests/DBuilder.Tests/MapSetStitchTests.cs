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
}
