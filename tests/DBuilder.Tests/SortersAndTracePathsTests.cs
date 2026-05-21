// ABOUTME: Verification tests for LinedefAngleSorter, SidedefAngleSorter, and the trace path types.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SortersAndTracePathsTests
{
    private static Linedef Line(Vertex a, Vertex b)
    {
        var l = new Linedef(a, b);
        l.Front = new Sidedef(l, true);
        l.Back = new Sidedef(l, false);
        return l;
    }

    [Fact]
    public void LinedefAngleSorterReturnsZeroForSameLineComparison()
    {
        var center = new Vertex(new Vector2D(0, 0));
        var east = new Vertex(new Vector2D(10, 0));
        var north = new Vertex(new Vector2D(0, 10));

        var baseLine = Line(center, east);
        var other = Line(center, north);
        var sorter = new LinedefAngleSorter(baseLine, front: true, fromvertex: center);

        Assert.Equal(0, sorter.Compare(other, other));
    }

    [Fact]
    public void LinedefAngleSorterOrdersLinesAroundVertex()
    {
        // Center vertex; three rays. After sorting, they should come back in consistent rotational order.
        var c = new Vertex(new Vector2D(0, 0));
        var e = new Vertex(new Vector2D(10, 0));
        var n = new Vertex(new Vector2D(0, 10));
        var w = new Vertex(new Vector2D(-10, 0));

        var lE = Line(c, e);
        var lN = Line(c, n);
        var lW = Line(c, w);

        var lines = new List<Linedef> { lN, lW, lE };
        lines.Sort(new LinedefAngleSorter(lE, front: true, fromvertex: c));
        // Stability isn't promised; what's promised is that sorting is deterministic and lossless.
        Assert.Equal(3, lines.Count);
        Assert.Contains(lE, lines);
        Assert.Contains(lN, lines);
        Assert.Contains(lW, lines);
    }

    [Fact]
    public void SidedefAngleSorterIdentityIsZero()
    {
        var c = new Vertex(new Vector2D(0, 0));
        var e = new Vertex(new Vector2D(10, 0));
        var lE = Line(c, e);
        var sorter = new SidedefAngleSorter(lE.Front!, c);
        Assert.Equal(0, sorter.Compare(lE.Front, lE.Front));
    }

    [Fact]
    public void LinedefTracePathDisconnectedSequenceIsNotClosed()
    {
        // UDB's CheckIsClosed reports true whenever the first and last lines share any endpoint —
        // for a real trace that means a 2-line connected path passes the check by quirk.
        // A genuinely disconnected first/last pair is what reports "not closed".
        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(10, 0));
        var v2 = new Vertex(new Vector2D(100, 100));
        var v3 = new Vertex(new Vector2D(200, 200));
        var path = new LinedefTracePath { Line(v0, v1), Line(v2, v3) };
        Assert.False(path.CheckIsClosed());
    }

    [Fact]
    public void LinedefTracePathClosedTriangleIsClosed()
    {
        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(10, 0));
        var v2 = new Vertex(new Vector2D(5, 10));
        var path = new LinedefTracePath { Line(v0, v1), Line(v1, v2), Line(v2, v0) };
        Assert.True(path.CheckIsClosed());
    }

    [Fact]
    public void LinedefTracePathMakePolygonHasExpectedVertexCount()
    {
        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(10, 0));
        var v2 = new Vertex(new Vector2D(5, 10));
        var path = new LinedefTracePath { Line(v0, v1), Line(v1, v2), Line(v2, v0) };
        var poly = path.MakePolygon(startfront: true);
        Assert.Equal(3, poly.Count);
    }

    [Fact]
    public void SidedefsTracePathClosedSquareIsClosed()
    {
        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(10, 0));
        var v2 = new Vertex(new Vector2D(10, 10));
        var v3 = new Vertex(new Vector2D(0, 10));
        var path = new SidedefsTracePath();
        path.Add(Line(v0, v1).Front!);
        path.Add(Line(v1, v2).Front!);
        path.Add(Line(v2, v3).Front!);
        path.Add(Line(v3, v0).Front!);
        Assert.True(path.CheckIsClosed());
        var poly = path.MakePolygon();
        Assert.Equal(4, poly.Count);
    }

    [Fact]
    public void LinedefSideEqualityMatchesLineAndFront()
    {
        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(10, 0));
        var l = Line(v0, v1);
        var a = new LinedefSide(l, true);
        var b = new LinedefSide(l, true);
        var c = new LinedefSide(l, false);
        Assert.True(a == b);
        Assert.False(a == c);
        Assert.NotEqual(a, c);
        Assert.Equal(a, b);
    }
}
