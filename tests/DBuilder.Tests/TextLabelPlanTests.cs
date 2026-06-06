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

    [Fact]
    public void BuildImagePlanMatchesUdbPlainTextDrawingRectangles()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "Sector",
            new TextLabelSize(32, 12),
            new TextLabelPoint(0, 0),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);

        var color = new PixelColor(255, 255, 255, 255);
        var backColor = new PixelColor(128, 0, 0, 0);
        TextLabelImagePlan image = TextLabelPlan.BuildImagePlan("Sector", layout, drawBackground: false, color, backColor);

        Assert.Equal(TextLabelImageStyle.Plain, image.Style);
        Assert.Equal(layout.TextureSize, image.TextureSize);
        Assert.Equal(new TextLabelRectangle(-2, 1, 44, 16), image.BackgroundFillRectangle);
        Assert.Equal(new TextLabelRectangle(-2, -1, 44, 20), image.TextDrawRectangle);
        Assert.Equal(backColor, image.BackgroundFillColor);
        Assert.Equal(color, image.TextColor);
        Assert.Null(image.BorderColor);
        Assert.Equal(0, image.CornerRadius);
    }

    [Fact]
    public void BuildImagePlanKeepsSingleCharacterPlainBackgroundTightLikeUdb()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "A",
            new TextLabelSize(8, 12),
            new TextLabelPoint(0, 0),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);

        TextLabelImagePlan image = TextLabelPlan.BuildImagePlan(
            "A",
            layout,
            drawBackground: false,
            new PixelColor(255, 1, 2, 3),
            new PixelColor(128, 4, 5, 6));

        Assert.Equal(layout.TextRectangle, image.BackgroundFillRectangle);
        Assert.Equal(new TextLabelRectangle(-2, -1, 20, 20), image.TextDrawRectangle);
    }

    [Fact]
    public void BuildImagePlanMatchesUdbBackgroundLabelDrawingRectangles()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "Line",
            new TextLabelSize(24, 10),
            new TextLabelPoint(0, 0),
            alignX: TextLabelAlignmentX.Left,
            alignY: TextLabelAlignmentY.Top,
            viewportWidth: 320,
            viewportHeight: 200);

        var color = new PixelColor(255, 240, 230, 220);
        var backColor = new PixelColor(192, 10, 20, 30);
        TextLabelImagePlan image = TextLabelPlan.BuildImagePlan("Line", layout, drawBackground: true, color, backColor);

        Assert.Equal(TextLabelImageStyle.Background, image.Style);
        Assert.Equal(new TextLabelRectangle(0, 0, 31, 15), image.BackgroundFillRectangle);
        Assert.Equal(new TextLabelRectangle(0, 1, 32, 14), image.TextDrawRectangle);
        Assert.Equal(color, image.BackgroundFillColor);
        Assert.Equal(backColor, image.TextColor);
        Assert.Equal(backColor, image.BorderColor);
        Assert.Equal(TextLabelPlan.TextOriginX, image.CornerRadius);
    }

    [Fact]
    public void BuildRenderPlanSkipsOffscreenLabelsAndDrawsVisibleLabelsAsTriangleStrips()
    {
        TextLabelLayout visible = TextLabelPlan.Build(
            "Visible",
            new TextLabelSize(24, 10),
            new TextLabelPoint(8, 8),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);
        TextLabelLayout skipped = TextLabelPlan.Build(
            "Skipped",
            new TextLabelSize(24, 10),
            new TextLabelPoint(400, 8),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);
        TextLabelLayout secondVisible = TextLabelPlan.Build(
            "Again",
            new TextLabelSize(16, 10),
            new TextLabelPoint(40, 24),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);

        TextLabelRenderPlan plan = TextLabelPlan.BuildRenderPlan([visible, skipped, secondVisible]);

        Assert.True(plan.ShouldRender);
        Assert.Equal(1, plan.SkippedLabels);
        Assert.Equal(2, plan.Commands.Count);
        Assert.Equal(new TextLabelRenderCommand(0, visible.TextureSize, visible.ScreenRectangle, PrimitiveCount: 2), plan.Commands[0]);
        Assert.Equal(new TextLabelRenderCommand(2, secondVisible.TextureSize, secondVisible.ScreenRectangle, PrimitiveCount: 2), plan.Commands[1]);
    }

    [Fact]
    public void BuildRenderPlanDoesNotRenderWhenEveryLabelIsSkipped()
    {
        TextLabelLayout skipped = TextLabelPlan.Build(
            "Skipped",
            new TextLabelSize(24, 10),
            new TextLabelPoint(400, 8),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);

        TextLabelRenderPlan plan = TextLabelPlan.BuildRenderPlan([skipped]);

        Assert.False(plan.ShouldRender);
        Assert.Equal(1, plan.SkippedLabels);
        Assert.Empty(plan.Commands);
    }

    [Fact]
    public void BuildRenderStatePlanMatchesUdbTextRenderState()
    {
        TextLabelLayout visible = TextLabelPlan.Build(
            "Visible",
            new TextLabelSize(24, 10),
            new TextLabelPoint(8, 8),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);
        TextLabelRenderPlan renderPlan = TextLabelPlan.BuildRenderPlan([visible]);

        TextLabelRenderStatePlan state = TextLabelPlan.BuildRenderStatePlan(renderPlan);

        Assert.Equal(Cull.None, state.CullMode);
        Assert.False(state.DepthEnabled);
        Assert.True(state.AlphaBlendEnabled);
        Assert.False(state.AlphaTestEnabled);
        Assert.Equal(TextLabelPlan.Display2DNormalShaderName, state.ShaderName);
        Assert.False(state.WorldTransformation);
        Assert.Equal(1.0f, state.Alpha);
        Assert.Equal(1.0f, state.Brightness);
        Assert.Equal(0.0f, state.TextureOffset);
        Assert.Equal(1.0f, state.TextureScale);
        Assert.False(state.TextureTransformEnabled);
    }

    [Fact]
    public void BuildRenderStatePlanKeepsBlendDisabledWhenRenderPlanIsEmpty()
    {
        TextLabelLayout skipped = TextLabelPlan.Build(
            "Skipped",
            new TextLabelSize(24, 10),
            new TextLabelPoint(400, 8),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);
        TextLabelRenderPlan renderPlan = TextLabelPlan.BuildRenderPlan([skipped]);

        TextLabelRenderStatePlan state = TextLabelPlan.BuildRenderStatePlan(renderPlan);

        Assert.False(renderPlan.ShouldRender);
        Assert.False(state.AlphaBlendEnabled);
    }
}
