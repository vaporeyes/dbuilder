// ABOUTME: Line2D port verification tests.
// ABOUTME: Intersection, distance, side, clipping behavior preserved from UDB Line2D.cs.

using System.Drawing;
using DBuilder.Geometry;

namespace DBuilder.Tests;

public class Line2DTests
{
    private const double Epsilon = 1e-9;

    [Fact]
    public void DeltaAndLength()
    {
        var l = new Line2D(0, 0, 3, 4);
        Assert.Equal(new Vector2D(3, 4), l.GetDelta());
        Assert.Equal(5, l.GetLength(), Epsilon);
        Assert.Equal(25, l.GetLengthSq(), Epsilon);
    }

    [Fact]
    public void PerpendicularRotates90DegLeft()
    {
        // (1,0) -> perpendicular -> (0,1) by UDB's convention (-dy, dx).
        var p = new Line2D(0, 0, 1, 0).GetPerpendicular();
        Assert.Equal(new Vector2D(0, 1), p);
    }

    [Fact]
    public void CrossingLinesIntersect()
    {
        // Horizontal line y=0 and vertical x=0 cross at origin.
        var a = new Line2D(-1, 0, 1, 0);
        var b = new Line2D(0, -1, 0, 1);
        Assert.True(Line2D.GetIntersection(a, b));
    }

    [Fact]
    public void ParallelLinesDoNotIntersect()
    {
        var a = new Line2D(0, 0, 1, 0);
        var b = new Line2D(0, 1, 1, 1);
        Assert.False(Line2D.GetIntersection(a, b));
    }

    [Fact]
    public void IntersectionPointMatchesGeometry()
    {
        var a = new Line2D(-1, 0, 1, 0);
        var b = new Line2D(0, -1, 0, 1);
        var p = Line2D.GetIntersectionPoint(a, b, bounded: true);
        Assert.Equal(0, p.x, Epsilon);
        Assert.Equal(0, p.y, Epsilon);
    }

    [Fact]
    public void SideOfLineSignSplitsLeftRight()
    {
        // Line from (0,0) to (1,0): point above (y>0) is back, below is front per UDB sign convention.
        var v1 = new Vector2D(0, 0);
        var v2 = new Vector2D(1, 0);
        double above = Line2D.GetSideOfLine(v1, v2, new Vector2D(0.5, 1));
        double below = Line2D.GetSideOfLine(v1, v2, new Vector2D(0.5, -1));
        Assert.True(above > 0 && below < 0);
    }

    [Fact]
    public void DistanceToLineWithBoundedClamp()
    {
        var v1 = new Vector2D(0, 0);
        var v2 = new Vector2D(10, 0);
        // Point at (5, 3) projects onto the line at (5, 0); perpendicular distance is 3.
        Assert.Equal(3, Line2D.GetDistanceToLine(v1, v2, new Vector2D(5, 3), bounded: true), Epsilon);
        // Point at (15, 0) is beyond the line endpoint; bounded distance is 5 (to (10,0)).
        Assert.Equal(5, Line2D.GetDistanceToLine(v1, v2, new Vector2D(15, 0), bounded: true), Epsilon);
        // Unbounded: that same point is on the infinite line extension, distance 0.
        Assert.Equal(0, Line2D.GetDistanceToLine(v1, v2, new Vector2D(15, 0), bounded: false), Epsilon);
    }

    [Fact]
    public void NearestOnLineGives01ForEndpoints()
    {
        var v1 = new Vector2D(0, 0);
        var v2 = new Vector2D(10, 0);
        Assert.Equal(0, Line2D.GetNearestOnLine(v1, v2, new Vector2D(0, 5)), Epsilon);
        Assert.Equal(1, Line2D.GetNearestOnLine(v1, v2, new Vector2D(10, 5)), Epsilon);
        Assert.Equal(0.5, Line2D.GetNearestOnLine(v1, v2, new Vector2D(5, 5)), Epsilon);
    }

    [Fact]
    public void CoordinatesAtInterpolates()
    {
        var v1 = new Vector2D(0, 0);
        var v2 = new Vector2D(10, 0);
        Assert.Equal(new Vector2D(5, 0), Line2D.GetCoordinatesAt(v1, v2, 0.5));
    }

    [Fact]
    public void NearestPointOnLineClampsWhenBounded()
    {
        var v1 = new Vector2D(0, 0);
        var v2 = new Vector2D(10, 0);

        Assert.Equal(new Vector2D(5, 0), Line2D.GetNearestPointOnLine(v1, v2, new Vector2D(5, 3), bounded: true));
        Assert.Equal(new Vector2D(10, 0), Line2D.GetNearestPointOnLine(v1, v2, new Vector2D(15, 3), bounded: true));
        Assert.Equal(new Vector2D(15, 0), Line2D.GetNearestPointOnLine(v1, v2, new Vector2D(15, 3), bounded: false));
    }

    [Fact]
    public void NearestPointOnDegenerateLineReturnsStart()
    {
        var line = new Line2D(3, 4, 3, 4);

        Assert.Equal(new Vector2D(3, 4), line.GetNearestPointOnLine(new Vector2D(100, 200), bounded: true));
    }

    [Fact]
    public void TransformRoundTrip()
    {
        // Forward: (x+off)*scale. Inverse: x*invScale + invOff. To round-trip we need invOff = -off and invScale = 1/scale.
        var l = new Line2D(2, 3, 4, 5);
        var t = l.GetTransformed(1, 1, 2, 2);
        var back = t.GetInvTransformed(-1, -1, 0.5, 0.5);
        Assert.Equal(l.v1.x, back.v1.x, Epsilon);
        Assert.Equal(l.v1.y, back.v1.y, Epsilon);
        Assert.Equal(l.v2.x, back.v2.x, Epsilon);
        Assert.Equal(l.v2.y, back.v2.y, Epsilon);
    }

    [Fact]
    public void ClipToRectangleKeepsLineInside()
    {
        var rect = new RectangleF(0, 0, 10, 10);
        var l = new Line2D(1, 1, 9, 9);
        var clipped = Line2D.ClipToRectangle(l, rect, out bool intersects);
        Assert.True(intersects);
        Assert.Equal(l.v1, clipped.v1);
        Assert.Equal(l.v2, clipped.v2);
    }
}
