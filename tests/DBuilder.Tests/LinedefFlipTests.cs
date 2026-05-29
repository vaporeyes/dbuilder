// ABOUTME: Tests for Linedef.FlipVertices/FlipSidedefs and the MapSet selection-flip helpers.
// ABOUTME: Verifies direction reversal, angle update, side swapping, IsFront flags, and geometry preservation.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class LinedefFlipTests
{
    [Fact]
    public void FlipVerticesReversesDirectionAndAngle()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var l = map.AddLinedef(a, b);
        map.BuildIndexes();
        double before = l.Angle;

        l.FlipVertices();
        map.BuildIndexes();

        Assert.Same(b, l.Start);
        Assert.Same(a, l.End);
        Assert.NotEqual(before, l.Angle);
        // The reversed direction differs from the original by half a turn.
        double diff = Angle2D.Difference(before, l.Angle);
        Assert.Equal(Angle2D.PI, diff, 6);
    }

    [Fact]
    public void FlipSidedefsSwapsFrontAndBack()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var l = map.AddLinedef(a, b);
        var s1 = map.AddSector();
        var s2 = map.AddSector();
        var front = map.AddSidedef(l, true, s1);
        var back = map.AddSidedef(l, false, s2);
        map.BuildIndexes();

        l.FlipSidedefs();
        map.BuildIndexes();

        Assert.Same(back, l.Front);
        Assert.Same(front, l.Back);
        Assert.True(l.Front!.IsFront);
        Assert.False(l.Back!.IsFront);
        Assert.Same(s2, l.Front.Sector);
        Assert.Same(s1, l.Back.Sector);
    }

    [Fact]
    public void FlipSidedefsHandlesMissingBack()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var l = map.AddLinedef(a, b);
        var s1 = map.AddSector();
        var front = map.AddSidedef(l, true, s1);
        map.BuildIndexes();

        l.FlipSidedefs();

        Assert.Null(l.Front);
        Assert.Same(front, l.Back);
        Assert.False(l.Back!.IsFront);
    }

    [Fact]
    public void GetCenterPointReturnsLineMidpoint()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(-10, 8));
        var b = map.AddVertex(new Vector2D(30, 24));
        var line = map.AddLinedef(a, b);

        Assert.Equal(new Vector2D(10, 16), line.GetCenterPoint());
    }

    [Fact]
    public void GetSidePointOffsetsFromLineCenter()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var line = map.AddLinedef(a, b);

        Assert.Equal(new Vector2D(50, -0.01), line.GetSidePoint(front: true));
        Assert.Equal(new Vector2D(50, 0.01), line.GetSidePoint(front: false));
    }

    [Fact]
    public void GetSidePointHandlesDegenerateLinesLikeUdb()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(16, 32));
        var line = map.AddLinedef(a, a);

        Assert.Equal(a.Position, line.GetSidePoint(front: true));
        Assert.Equal(a.Position, line.GetSidePoint(front: false));
    }

    [Fact]
    public void GeometryPropertiesMatchUdbLineSurface()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(-4, 2));
        var b = map.AddVertex(new Vector2D(8, 7));
        var line = map.AddLinedef(a, b);

        Assert.Equal(new Line2D(a.Position, b.Position).v1, line.Line.v1);
        Assert.Equal(new Line2D(a.Position, b.Position).v2, line.Line.v2);
        Assert.Equal(169, line.LengthSq, 1e-9);
        Assert.Equal(13, line.Length, 1e-9);
        Assert.Equal(1.0 / 13.0, line.LengthInv, 1e-9);
        Assert.Equal((int)(line.Angle * Angle2D.PIDEG), line.AngleDeg);
        Assert.Equal(-4, line.Rect.Left);
        Assert.Equal(2, line.Rect.Top);
        Assert.Equal(12, line.Rect.Width);
        Assert.Equal(5, line.Rect.Height);
    }

    [Fact]
    public void GeometryMethodsMatchLine2DBehavior()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(10, 0));
        var line = map.AddLinedef(a, b);

        Assert.Equal(new Vector2D(10, 0), line.NearestOnLine(new Vector2D(15, 4)));
        Assert.Equal(25, line.DistanceToSq(new Vector2D(15, 0), bounded: true), 1e-9);
        Assert.Equal(5, line.DistanceTo(new Vector2D(15, 0), bounded: true), 1e-9);
        Assert.Equal(0, line.DistanceTo(new Vector2D(15, 0), bounded: false), 1e-9);
        Assert.True(line.SideOfLine(new Vector2D(5, 1)) > 0);
        Assert.True(line.SideOfLine(new Vector2D(5, -1)) < 0);
    }

    [Fact]
    public void SafeDistanceAvoidsEqualEndpointDistanceForLongLines()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(10, 0));
        var line = map.AddLinedef(a, b);

        Assert.True(line.SafeDistanceToSq(new Vector2D(-5, 0), bounded: true) > line.DistanceToSq(new Vector2D(-5, 0), bounded: true));
    }

    [Fact]
    public void FlipSelectedLinedefsOnlyTouchesSelection()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var c = map.AddVertex(new Vector2D(100, 100));
        var l1 = map.AddLinedef(a, b);
        var l2 = map.AddLinedef(b, c);
        map.BuildIndexes();
        l1.Selected = true;

        int n = map.FlipSelectedLinedefs();
        map.BuildIndexes();

        Assert.Equal(1, n);
        Assert.Same(b, l1.Start); // flipped
        Assert.Same(b, l2.Start); // untouched
        Assert.Same(c, l2.End);
    }

    [Fact]
    public void FlipBackwardLinedefsFlipsLinesWithOnlyBackSide()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var sector = map.AddSector();
        var line = map.AddLinedef(a, b);
        var back = map.AddSidedef(line, false, sector);

        int flips = MapSet.FlipBackwardLinedefs(new[] { line });

        Assert.Equal(1, flips);
        Assert.Same(b, line.Start);
        Assert.Same(a, line.End);
        Assert.Same(back, line.Front);
        Assert.Null(line.Back);
        Assert.True(back.IsFront);
    }

    [Fact]
    public void FlipBackwardLinedefsIgnoresLinesWithFrontSides()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var sector = map.AddSector();
        var line = map.AddLinedef(a, b);
        var front = map.AddSidedef(line, true, sector);

        int flips = MapSet.FlipBackwardLinedefs(new[] { line });

        Assert.Equal(0, flips);
        Assert.Same(a, line.Start);
        Assert.Same(b, line.End);
        Assert.Same(front, line.Front);
        Assert.Null(line.Back);
    }
}
