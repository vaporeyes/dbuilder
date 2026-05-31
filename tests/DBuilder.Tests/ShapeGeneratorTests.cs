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

    [Fact]
    public void UdbRectangleReturnsClosedShapeAndHintText()
    {
        DrawShapePlan plan = ShapeGenerator.UdbRectangle(
            new Vector2D(0, 0),
            new Vector2D(64, 32),
            new DrawRectangleModeSettings(Subdivisions: 0, BevelWidth: 0));

        Assert.Equal(
            [
                new Vector2D(0, 0),
                new Vector2D(0, 32),
                new Vector2D(64, 32),
                new Vector2D(64, 0),
                new Vector2D(0, 0)
            ],
            plan.Points);
        Assert.Equal(0, plan.EffectiveBevelWidth);
        Assert.Equal(string.Empty, plan.HintText);

        DrawShapePlan hinted = ShapeGenerator.UdbRectangle(
            new Vector2D(0, 0),
            new Vector2D(64, 32),
            new DrawRectangleModeSettings(Subdivisions: 2, BevelWidth: 8));

        Assert.Equal("BVL: 8; SUB: 2", hinted.HintText);
    }

    [Fact]
    public void UdbRectangleBevelCapsToHalfSmallestSideAndClosesShape()
    {
        DrawShapePlan plan = ShapeGenerator.UdbRectangle(
            new Vector2D(0, 0),
            new Vector2D(64, 32),
            new DrawRectangleModeSettings(Subdivisions: 1, BevelWidth: 99));

        Assert.Equal(16, plan.EffectiveBevelWidth);
        Assert.Equal(13, plan.Points.Count);
        Assert.Equal(plan.Points[0], plan.Points[^1]);
        Assert.Contains(plan.Points, point => Math.Abs(point.x - 0) < 0.001 && Math.Abs(point.y - 16) < 0.001);
        Assert.Contains(plan.Points, point => Math.Abs(point.x - 64) < 0.001 && Math.Abs(point.y - 16) < 0.001);
    }

    [Fact]
    public void UdbRectangleNegativeBevelKeepsNegativeEffectiveWidth()
    {
        DrawShapePlan plan = ShapeGenerator.UdbRectangle(
            new Vector2D(0, 0),
            new Vector2D(64, 32),
            new DrawRectangleModeSettings(Subdivisions: 0, BevelWidth: -99));

        Assert.Equal(-16, plan.EffectiveBevelWidth);
        Assert.Equal("BVL: -99", plan.HintText);
        Assert.Equal(plan.Points[0], plan.Points[^1]);
    }

    [Fact]
    public void UdbEllipseReturnsClosedShapeFittedToBounds()
    {
        DrawShapePlan plan = ShapeGenerator.UdbEllipse(
            new Vector2D(0, 0),
            new Vector2D(100, 60),
            new DrawEllipseModeSettings(Subdivisions: 8, Angle: 0));

        Assert.Equal(9, plan.Points.Count);
        Assert.Equal(plan.Points[0], plan.Points[^1]);
        Assert.Equal(0, plan.Points.Min(point => point.x), 3);
        Assert.Equal(100, plan.Points.Max(point => point.x), 3);
        Assert.Equal(0, plan.Points.Min(point => point.y), 3);
        Assert.Equal(60, plan.Points.Max(point => point.y), 3);
        Assert.Equal("VERTS: 8", plan.HintText);
    }

    [Fact]
    public void UdbEllipseUsesSpikinessAndAngleHint()
    {
        DrawShapePlan plan = ShapeGenerator.UdbEllipse(
            new Vector2D(0, 0),
            new Vector2D(100, 100),
            new DrawEllipseModeSettings(Subdivisions: 8, BevelWidth: 12, Angle: 45));

        Assert.Equal(12, plan.EffectiveBevelWidth);
        Assert.Equal("BVL: 12; VERTS: 8; ANGLE: 45", plan.HintText);
        Assert.Equal(0, plan.Points.Min(point => point.x), 3);
        Assert.Equal(100, plan.Points.Max(point => point.x), 3);
        Assert.Equal(0, plan.Points.Min(point => point.y), 3);
        Assert.Equal(100, plan.Points.Max(point => point.y), 3);
    }

    [Fact]
    public void UdbEllipseDisablesSpikinessForLowSubdivisionShapes()
    {
        DrawShapePlan plan = ShapeGenerator.UdbEllipse(
            new Vector2D(0, 0),
            new Vector2D(100, 100),
            new DrawEllipseModeSettings(Subdivisions: 5, BevelWidth: 12));

        Assert.Equal(0, plan.EffectiveBevelWidth);
        Assert.Equal(6, plan.Points.Count);
    }
}
