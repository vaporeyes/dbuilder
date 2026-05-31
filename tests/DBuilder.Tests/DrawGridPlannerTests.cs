// ABOUTME: Tests UDB-style draw-grid geometry planning from two picked points.
// ABOUTME: Covers rectangle grids, subdivisions, triangulation, line segmentation, and interpolation modes.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class DrawGridPlannerTests
{
    [Fact]
    public void CreateBuildsRectangleAndSubdivisionLines()
    {
        DrawGridPlan plan = DrawGridPlanner.Create(
            new Vector2D(0, 0),
            new Vector2D(90, 60),
            new DrawGridPlanOptions { HorizontalSlices = 3, VerticalSlices = 2 });

        Assert.Equal(3, plan.HorizontalSlices);
        Assert.Equal(2, plan.VerticalSlices);
        Assert.Equal(4, plan.Shapes.Count);
        Assert.Equal(new[] { new Vector2D(0, 0), new Vector2D(0, 60), new Vector2D(90, 60), new Vector2D(90, 0), new Vector2D(0, 0) }, plan.Shapes[0]);
        Assert.Equal(new[] { new Vector2D(30, 0), new Vector2D(30, 60) }, plan.Shapes[1]);
        Assert.Equal(new[] { new Vector2D(60, 0), new Vector2D(60, 60) }, plan.Shapes[2]);
        Assert.Equal(new[] { new Vector2D(0, 30), new Vector2D(90, 30) }, plan.Shapes[3]);
    }

    [Fact]
    public void CreateTriangulatesSingleCellLikeUdb()
    {
        DrawGridPlan plan = DrawGridPlanner.Create(
            new Vector2D(0, 0),
            new Vector2D(32, 32),
            new DrawGridPlanOptions { HorizontalSlices = 1, VerticalSlices = 1, Triangulate = true });

        Assert.Single(plan.Shapes);
        Assert.Equal(
            new[] { new Vector2D(0, 0), new Vector2D(0, 32), new Vector2D(32, 32), new Vector2D(32, 0), new Vector2D(0, 0), new Vector2D(0, 0), new Vector2D(32, 32) },
            plan.Shapes[0]);
    }

    [Fact]
    public void CreateAddsCheckerboardTriangulationForMultiCellGrid()
    {
        DrawGridPlan plan = DrawGridPlanner.Create(
            new Vector2D(0, 0),
            new Vector2D(64, 64),
            new DrawGridPlanOptions { HorizontalSlices = 2, VerticalSlices = 2, Triangulate = true, GridSizeF = 32 });

        Assert.Equal(7, plan.Shapes.Count);
        Assert.Equal(new[] { new Vector2D(0, 32), new Vector2D(32, 64) }, plan.Shapes[3]);
        Assert.Equal(new[] { new Vector2D(32, 0), new Vector2D(0, 32) }, plan.Shapes[4]);
        Assert.Equal(new[] { new Vector2D(64, 32), new Vector2D(32, 64) }, plan.Shapes[5]);
        Assert.Equal(new[] { new Vector2D(32, 0), new Vector2D(64, 32) }, plan.Shapes[6]);
    }

    [Fact]
    public void CreateSegmentsAxisAlignedLinesUsingSlices()
    {
        DrawGridPlan plan = DrawGridPlanner.Create(
            new Vector2D(0, 0),
            new Vector2D(90, 0),
            new DrawGridPlanOptions { HorizontalSlices = 3, VerticalSlices = 2 });

        Assert.Equal(3, plan.Shapes.Count);
        Assert.Equal(new[] { new Vector2D(0, 0), new Vector2D(30, 0) }, plan.Shapes[0]);
        Assert.Equal(new[] { new Vector2D(30, 0), new Vector2D(60, 0) }, plan.Shapes[1]);
        Assert.Equal(new[] { new Vector2D(60, 0), new Vector2D(90, 0) }, plan.Shapes[2]);
    }

    [Fact]
    public void CreateCanSortReferencePointsWhenRelativeInterpolationIsDisabled()
    {
        DrawGridPlan plan = DrawGridPlanner.Create(
            new Vector2D(90, 60),
            new Vector2D(0, 0),
            new DrawGridPlanOptions { HorizontalSlices = 3, VerticalSlices = 2, RelativeInterpolation = false });

        Assert.Equal(new[] { new Vector2D(0, 0), new Vector2D(0, 60), new Vector2D(90, 60), new Vector2D(90, 0), new Vector2D(0, 0) }, plan.Shapes[0]);
    }

    [Fact]
    public void CreateUsesInterpolationModesForSubdivisionPositions()
    {
        DrawGridPlan plan = DrawGridPlanner.Create(
            new Vector2D(0, 0),
            new Vector2D(100, 100),
            new DrawGridPlanOptions
            {
                HorizontalSlices = 2,
                VerticalSlices = 2,
                HorizontalInterpolation = InterpolationTools.Mode.EASE_IN_SINE,
                VerticalInterpolation = InterpolationTools.Mode.EASE_OUT_SINE,
            });

        Assert.Equal(29.289, plan.Shapes[1][0].x, 3);
        Assert.Equal(70.711, plan.Shapes[2][0].y, 3);
    }

    [Fact]
    public void CreateDerivesSliceCountsFromGridLock()
    {
        DrawGridPlan plan = DrawGridPlanner.Create(
            new Vector2D(0, 0),
            new Vector2D(96, 48),
            new DrawGridPlanOptions
            {
                GridLockMode = DrawGridLockMode.Both,
                GridSize = 32,
                HorizontalSlices = 1,
                VerticalSlices = 1,
            });

        Assert.Equal(3, plan.HorizontalSlices);
        Assert.Equal(2, plan.VerticalSlices);
        Assert.Equal(4, plan.Shapes.Count);
    }
}
