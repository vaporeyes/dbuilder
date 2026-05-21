// ABOUTME: EarClipPolygon port verification tests.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class EarClipPolygonTests
{
    private static EarClipPolygon Square(double x, double y, double size)
    {
        var p = new EarClipPolygon();
        p.AddLast(new EarClipVertex(new Vector2D(x, y), null));
        p.AddLast(new EarClipVertex(new Vector2D(x + size, y), null));
        p.AddLast(new EarClipVertex(new Vector2D(x + size, y + size), null));
        p.AddLast(new EarClipVertex(new Vector2D(x, y + size), null));
        return p;
    }

    [Fact]
    public void AreaOfUnitSquareIsOne()
    {
        Assert.Equal(1.0, Square(0, 0, 1).CalculateArea(), 1e-12);
    }

    [Fact]
    public void BoundingBoxMatchesExtents()
    {
        var bbox = Square(2, 3, 4).CreateBBox();
        Assert.Equal(2, bbox.X);
        Assert.Equal(3, bbox.Y);
        Assert.Equal(4, bbox.Width);
        Assert.Equal(4, bbox.Height);
    }

    [Fact]
    public void PointInsideIntersects()
    {
        Assert.True(Square(0, 0, 10).Intersect(new Vector2D(5, 5)));
    }

    [Fact]
    public void PointOutsideDoesNotIntersect()
    {
        Assert.False(Square(0, 0, 10).Intersect(new Vector2D(20, 5)));
    }

    [Fact]
    public void HoleIsExcludedFromIntersection()
    {
        // Outer 10x10 square with a hole 4x4 centered inside.
        var outer = Square(0, 0, 10);
        var hole = Square(3, 3, 4);
        Assert.True(outer.InsertChild(hole));

        // Point in the outer ring but outside the hole.
        Assert.True(outer.Intersect(new Vector2D(1, 1)));
        // Point inside the hole — should not count as inside the polygon.
        Assert.False(outer.Intersect(new Vector2D(5, 5)));
    }

    [Fact]
    public void InnerPolygonGetsOppositeInnerFlag()
    {
        var outer = Square(0, 0, 10);
        outer.Inner = false;
        var hole = Square(3, 3, 4);
        outer.InsertChild(hole);
        Assert.True(hole.Inner);
    }
}
