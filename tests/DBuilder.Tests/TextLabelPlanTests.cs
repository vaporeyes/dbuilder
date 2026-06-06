// ABOUTME: Verifies UDB-style text label layout planning without creating GL textures.
// ABOUTME: Covers padding, texture sizing, alignment, transform coordinates, and viewport culling.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class TextLabelPlanTests
{
    [Fact]
    public void BuildPadsMeasuredTextAndUsesPowerOfTwoTextureSize()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "Sector 7",
            new TextLabelSize(33.4, 12.2),
            new TextLabelPoint(10, 20),
            alignX: TextLabelAlignmentX.Left,
            alignY: TextLabelAlignmentY.Top,
            viewportWidth: 320,
            viewportHeight: 200);

        Assert.Equal(new TextLabelSize(41, 18), layout.TextSize);
        Assert.Equal(new TextLabelSize(64, 32), layout.TextureSize);
        Assert.Equal(new TextLabelRectangle(4, 3, 33, 12), layout.TextRectangle);
        Assert.Equal(new TextLabelRectangle(0, 0, 41, 18), layout.BackgroundRectangle);
        Assert.Equal(new TextLabelRectangle(10, 20, 64, 32), layout.ScreenRectangle);
        Assert.False(layout.SkipRendering);
        Assert.Equal(4, layout.Vertices.Length);
    }

    [Fact]
    public void BuildAlignsTextureAroundLocationLikeUdb()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "Thing",
            new TextLabelSize(48, 14),
            new TextLabelPoint(100, 80),
            alignX: TextLabelAlignmentX.Center,
            alignY: TextLabelAlignmentY.Middle,
            viewportWidth: 320,
            viewportHeight: 200);

        Assert.Equal(new TextLabelSize(56, 20), layout.TextSize);
        Assert.Equal(new TextLabelSize(64, 32), layout.TextureSize);
        Assert.Equal(new TextLabelRectangle(4, 6, 56, 20), layout.BackgroundRectangle);
        Assert.Equal(new TextLabelRectangle(8, 9, 48, 14), layout.TextRectangle);
        Assert.Equal(new TextLabelRectangle(68, 64, 64, 32), layout.ScreenRectangle);
    }

    [Fact]
    public void BuildTransformsWorldCoordinatesWhenRequested()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "Vertex",
            new TextLabelSize(16, 10),
            new TextLabelPoint(20, -12),
            alignX: TextLabelAlignmentX.Right,
            alignY: TextLabelAlignmentY.Bottom,
            transformCoordinates: true,
            translateX: 4,
            translateY: 2,
            scaleX: 3,
            scaleY: -3,
            viewportWidth: 320,
            viewportHeight: 200);

        Assert.Equal(new TextLabelPoint(72, 30), new TextLabelPoint(layout.ScreenRectangle.Right, layout.ScreenRectangle.Bottom));
        Assert.Equal(new TextLabelRectangle(8, 0, 24, 16), layout.BackgroundRectangle);
        Assert.Equal(new TextLabelRectangle(12, 3, 16, 10), layout.TextRectangle);
    }

    [Fact]
    public void BuildSkipsLabelsOutsideViewport()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "Offscreen",
            new TextLabelSize(24, 10),
            new TextLabelPoint(401, 20),
            alignX: TextLabelAlignmentX.Left,
            alignY: TextLabelAlignmentY.Top,
            viewportWidth: 320,
            viewportHeight: 200);

        Assert.True(layout.SkipRendering);
        Assert.Empty(layout.Vertices);
    }

    [Fact]
    public void BuildSkipsEmptyText()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "",
            new TextLabelSize(24, 10),
            new TextLabelPoint(10, 20),
            viewportWidth: 320,
            viewportHeight: 200);

        Assert.True(layout.SkipRendering);
        Assert.Equal(new TextLabelSize(0, 0), layout.TextureSize);
        Assert.Empty(layout.Vertices);
    }
}
