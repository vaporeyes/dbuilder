// ABOUTME: Tests UDB draw-mode accept lifecycle decisions for one-shot and continuous drawing.
// ABOUTME: Covers polyline and box-drag tools without requiring the Avalonia editor control.

using DBuilder.Map;

namespace DBuilder.Tests;

public class DrawModeLifecycleTests
{
    [Theory]
    [InlineData(DrawModeTool.Sector)]
    [InlineData(DrawModeTool.Lines)]
    [InlineData(DrawModeTool.Curve)]
    [InlineData(DrawModeTool.Rectangle)]
    [InlineData(DrawModeTool.Ellipse)]
    [InlineData(DrawModeTool.Grid)]
    public void AfterAcceptExitsToolWhenContinuousDrawingIsDisabled(DrawModeTool tool)
    {
        DrawModeLifecycleState state = DrawModeLifecycle.AfterAccept(tool, continuousDrawing: false);

        Assert.False(state.DrawMode);
        Assert.False(state.LinesOnly);
        Assert.False(state.Curve);
        Assert.Null(state.Shape);
    }

    [Theory]
    [InlineData(DrawModeTool.Sector, false, false)]
    [InlineData(DrawModeTool.Lines, true, false)]
    [InlineData(DrawModeTool.Curve, true, true)]
    public void AfterAcceptKeepsPolylineToolWhenContinuousDrawingIsEnabled(
        DrawModeTool tool,
        bool linesOnly,
        bool curve)
    {
        DrawModeLifecycleState state = DrawModeLifecycle.AfterAccept(tool, continuousDrawing: true);

        Assert.True(state.DrawMode);
        Assert.Equal(linesOnly, state.LinesOnly);
        Assert.Equal(curve, state.Curve);
        Assert.Null(state.Shape);
    }

    [Theory]
    [InlineData(DrawModeTool.Rectangle)]
    [InlineData(DrawModeTool.Ellipse)]
    [InlineData(DrawModeTool.Grid)]
    public void AfterAcceptKeepsShapeToolWhenContinuousDrawingIsEnabled(DrawModeTool tool)
    {
        DrawModeLifecycleState state = DrawModeLifecycle.AfterAccept(tool, continuousDrawing: true);

        Assert.False(state.DrawMode);
        Assert.False(state.LinesOnly);
        Assert.False(state.Curve);
        Assert.Equal(tool, state.Shape);
    }
}
