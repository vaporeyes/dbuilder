// ABOUTME: Tests shape-draw loop generation (rectangle, ellipse/N-gon) and degenerate-box handling.

using System;
using System.Linq;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ShapeGeneratorTests
{
    [Fact]
    public void RectangleSpansBoundingBoxRegardlessOfCornerOrder()
    {
        var loop = ShapeGenerator.Rectangle(new Vector2D(100, 80), new Vector2D(0, 0)); // reversed corners
        Assert.Equal(4, loop.Count);
        Assert.Equal(0, loop.Min(p => p.x), 3);
        Assert.Equal(100, loop.Max(p => p.x), 3);
        Assert.Equal(0, loop.Min(p => p.y), 3);
        Assert.Equal(80, loop.Max(p => p.y), 3);
    }

    [Fact]
    public void EllipseHasRequestedSidesAndStaysWithinBox()
    {
        var loop = ShapeGenerator.Ellipse(new Vector2D(0, 0), new Vector2D(100, 60), 12);
        Assert.Equal(12, loop.Count);
        Assert.All(loop, p =>
        {
            Assert.InRange(p.x, -0.001, 100.001);
            Assert.InRange(p.y, -0.001, 60.001);
        });
    }

    [Fact]
    public void EllipseClampsSidesToAtLeastThree()
    {
        Assert.Equal(3, ShapeGenerator.Ellipse(new Vector2D(0, 0), new Vector2D(50, 50), 1).Count);
    }

    [Fact]
    public void DegenerateBoxReturnsEmpty()
    {
        Assert.Empty(ShapeGenerator.Rectangle(new Vector2D(10, 10), new Vector2D(10, 50)));
        Assert.Empty(ShapeGenerator.Ellipse(new Vector2D(10, 10), new Vector2D(10, 50), 8));
    }

    [Fact]
    public void EllipseIsCenteredOnTheBox()
    {
        var loop = ShapeGenerator.Ellipse(new Vector2D(0, 0), new Vector2D(100, 100), 4);
        double cx = loop.Average(p => p.x), cy = loop.Average(p => p.y);
        Assert.Equal(50, cx, 3);
        Assert.Equal(50, cy, 3);
    }
}
