// ABOUTME: Verifies UDB-style text label layout planning without creating GL textures.
// ABOUTME: Covers padding, texture sizing, alignment, transform coordinates, and viewport culling.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class TextLabelPlanTests
{
    [Fact]
    public void BuildInitializationPlanMatchesUdbConstructorDefaults()
    {
        TextLabelInitializationPlan plan = TextLabelPlan.BuildInitializationPlan();

        Assert.Equal("", plan.Text);
        Assert.Equal(new TextLabelPoint(0, 0), plan.Location);
        Assert.Equal(new PixelColor(255, 255, 255, 255), plan.Color);
        Assert.Equal(new PixelColor(128, 0, 0, 0), plan.BackColor);
        Assert.Equal(TextLabelAlignmentX.Center, plan.AlignX);
        Assert.Equal(TextLabelAlignmentY.Top, plan.AlignY);
        Assert.False(plan.DrawBackground);
        Assert.False(plan.TransformCoordinates);
        Assert.Equal(TextLabelInvalidation.Initial, plan.Invalidation);
        Assert.True(plan.RegisterRenderResource);
        Assert.True(plan.SuppressFinalizer);
    }

    [Fact]
    public void BuildDisposePlanMatchesUdbDisposeLifecycle()
    {
        TextLabelDisposePlan active = TextLabelPlan.BuildDisposePlan(isDisposed: false);
        TextLabelDisposePlan disposed = TextLabelPlan.BuildDisposePlan(isDisposed: true);

        Assert.True(active.UnloadResource);
        Assert.True(active.DisposeFont);
        Assert.True(active.UnregisterRenderResource);
        Assert.True(active.MarkDisposed);
        Assert.False(disposed.UnloadResource);
        Assert.False(disposed.DisposeFont);
        Assert.False(disposed.UnregisterRenderResource);
        Assert.False(disposed.MarkDisposed);
    }

    [Fact]
    public void BuildDefaultFontPlanMatchesUdbConfigurationDefaults()
    {
        TextLabelFontPlan plan = TextLabelPlan.BuildDefaultFontPlan(
            availableFamilies: [TextLabelPlan.DefaultTextLabelFontName]);

        Assert.Equal("Microsoft Sans Serif", plan.RequestedFamily);
        Assert.Equal("Microsoft Sans Serif", plan.ResolvedFamily);
        Assert.Equal(10, plan.Size);
        Assert.False(plan.Bold);
        Assert.False(plan.UsedFallback);
    }

    [Fact]
    public void BuildFontPlanUsesConfiguredFamilyWhenAvailable()
    {
        TextLabelFontPlan plan = TextLabelPlan.BuildFontPlan(
            requestedFamily: "Consolas",
            size: 12,
            bold: true,
            availableFamilies: ["Arial", "consolas"],
            fallbackFamily: "Avalonia Default");

        Assert.Equal("Consolas", plan.RequestedFamily);
        Assert.Equal("Consolas", plan.ResolvedFamily);
        Assert.Equal(12, plan.Size);
        Assert.True(plan.Bold);
        Assert.False(plan.UsedFallback);
    }

    [Fact]
    public void BuildFontPlanFallsBackWhenConfiguredFamilyIsUnavailable()
    {
        TextLabelFontPlan plan = TextLabelPlan.BuildFontPlan(
            requestedFamily: "Missing Family",
            size: 14,
            bold: false,
            availableFamilies: ["Arial", "Consolas"],
            fallbackFamily: "Avalonia Default");

        Assert.Equal("Missing Family", plan.RequestedFamily);
        Assert.Equal("Avalonia Default", plan.ResolvedFamily);
        Assert.Equal(14, plan.Size);
        Assert.False(plan.Bold);
        Assert.True(plan.UsedFallback);
    }

    [Fact]
    public void BuildLegacyScaleFontPlanRoundsScaleLikeUdb()
    {
        TextLabelFontPlan plan = TextLabelPlan.BuildLegacyScaleFontPlan(
            scale: 13.6,
            requestedFamily: "Consolas",
            bold: true,
            availableFamilies: ["Consolas"]);

        Assert.Equal("Consolas", plan.ResolvedFamily);
        Assert.Equal(10, plan.Size);
        Assert.True(plan.Bold);
        Assert.False(plan.UsedFallback);
    }

    [Fact]
    public void BackcolorCompatibilityForcesHalfAlphaLikeUdb()
    {
        PixelColor value = TextLabelPlan.BuildBackcolorCompatibilityValue(new PixelColor(255, 1, 2, 3));

        Assert.Equal(new PixelColor(128, 1, 2, 3), value);
    }

    [Fact]
    public void TextChangeInvalidatesTextureOnlyWhenValueChanges()
    {
        TextLabelPropertyChangePlan unchanged = TextLabelPlan.BuildTextChangePlan(
            currentText: "Thing",
            nextText: "Thing",
            TextLabelInvalidation.Clean);
        TextLabelPropertyChangePlan changed = TextLabelPlan.BuildTextChangePlan(
            currentText: "Thing",
            nextText: "Line",
            TextLabelInvalidation.Clean);

        Assert.Equal(TextLabelPropertyChangeKind.None, unchanged.ChangeKind);
        Assert.Equal(TextLabelInvalidation.Clean, unchanged.Invalidation);
        Assert.Equal(TextLabelPropertyChangeKind.Texture, changed.ChangeKind);
        Assert.Equal(new TextLabelInvalidation(LayoutUpdateNeeded: false, TextureUpdateNeeded: true), changed.Invalidation);
    }

    [Fact]
    public void LocationAndRectangleCompatibilityInvalidateLayoutOnly()
    {
        var current = new TextLabelPoint(10, 20);

        TextLabelPropertyChangePlan location = TextLabelPlan.BuildLocationChangePlan(
            current,
            new TextLabelPoint(12, 20),
            TextLabelInvalidation.Clean);
        TextLabelPropertyChangePlan rectangle = TextLabelPlan.BuildRectangleCompatibilityChangePlan(
            current,
            new TextLabelRectangle(12, 24, 100, 50),
            TextLabelInvalidation.Clean);

        Assert.Equal(TextLabelPropertyChangeKind.Layout, location.ChangeKind);
        Assert.Equal(new TextLabelInvalidation(LayoutUpdateNeeded: true, TextureUpdateNeeded: false), location.Invalidation);
        Assert.Equal(TextLabelPropertyChangeKind.Layout, rectangle.ChangeKind);
        Assert.Equal(new TextLabelInvalidation(LayoutUpdateNeeded: true, TextureUpdateNeeded: false), rectangle.Invalidation);
    }

    [Fact]
    public void LocationAndRectangleCompatibilityInvalidateLayoutOnAssignmentLikeUdb()
    {
        var current = new TextLabelPoint(10, 20);

        TextLabelPropertyChangePlan location = TextLabelPlan.BuildLocationChangePlan(
            current,
            current,
            TextLabelInvalidation.Clean);
        TextLabelPropertyChangePlan rectangle = TextLabelPlan.BuildRectangleCompatibilityChangePlan(
            current,
            new TextLabelRectangle(10, 20, 100, 50),
            TextLabelInvalidation.Clean);

        Assert.Equal(TextLabelPropertyChangeKind.Layout, location.ChangeKind);
        Assert.Equal(new TextLabelInvalidation(LayoutUpdateNeeded: true, TextureUpdateNeeded: false), location.Invalidation);
        Assert.Equal(TextLabelPropertyChangeKind.Layout, rectangle.ChangeKind);
        Assert.Equal(new TextLabelInvalidation(LayoutUpdateNeeded: true, TextureUpdateNeeded: false), rectangle.Invalidation);
    }

    [Fact]
    public void ColorBackColorAndBackgroundTogglesInvalidateTextureOnlyWhenChanged()
    {
        var color = new PixelColor(255, 1, 2, 3);
        var backColor = new PixelColor(128, 4, 5, 6);

        TextLabelPropertyChangePlan sameColor = TextLabelPlan.BuildColorChangePlan(
            color,
            color,
            TextLabelInvalidation.Clean);
        TextLabelPropertyChangePlan newColor = TextLabelPlan.BuildColorChangePlan(
            color,
            new PixelColor(255, 3, 2, 1),
            TextLabelInvalidation.Clean);
        TextLabelPropertyChangePlan sameBackColor = TextLabelPlan.BuildBackColorChangePlan(
            backColor,
            backColor,
            TextLabelInvalidation.Clean);
        TextLabelPropertyChangePlan newBackColor = TextLabelPlan.BuildBackColorChangePlan(
            backColor,
            new PixelColor(128, 6, 5, 4),
            TextLabelInvalidation.Clean);
        TextLabelPropertyChangePlan sameBackground = TextLabelPlan.BuildDrawBackgroundChangePlan(
            currentDrawBackground: true,
            nextDrawBackground: true,
            TextLabelInvalidation.Clean);
        TextLabelPropertyChangePlan newBackground = TextLabelPlan.BuildDrawBackgroundChangePlan(
            currentDrawBackground: true,
            nextDrawBackground: false,
            TextLabelInvalidation.Clean);

        Assert.Equal(TextLabelPropertyChangeKind.None, sameColor.ChangeKind);
        Assert.Equal(TextLabelPropertyChangeKind.Texture, newColor.ChangeKind);
        Assert.Equal(new TextLabelInvalidation(LayoutUpdateNeeded: false, TextureUpdateNeeded: true), newColor.Invalidation);
        Assert.Equal(TextLabelPropertyChangeKind.None, sameBackColor.ChangeKind);
        Assert.Equal(TextLabelPropertyChangeKind.Texture, newBackColor.ChangeKind);
        Assert.Equal(new TextLabelInvalidation(LayoutUpdateNeeded: false, TextureUpdateNeeded: true), newBackColor.Invalidation);
        Assert.Equal(TextLabelPropertyChangeKind.None, sameBackground.ChangeKind);
        Assert.Equal(TextLabelPropertyChangeKind.Texture, newBackground.ChangeKind);
        Assert.Equal(new TextLabelInvalidation(LayoutUpdateNeeded: false, TextureUpdateNeeded: true), newBackground.Invalidation);
    }

    [Fact]
    public void FontTransformAndAlignmentAssignmentsMatchUdbInvalidation()
    {
        TextLabelPropertyChangePlan font = TextLabelPlan.BuildFontChangePlan(TextLabelInvalidation.Clean);
        TextLabelPropertyChangePlan scale = TextLabelPlan.BuildScaleChangePlan(TextLabelInvalidation.Clean);
        TextLabelPropertyChangePlan transform = TextLabelPlan.BuildTransformCoordinatesChangePlan(TextLabelInvalidation.Clean);
        TextLabelPropertyChangePlan alignment = TextLabelPlan.BuildAlignmentChangePlan(TextLabelInvalidation.Clean);

        Assert.Equal(TextLabelPropertyChangeKind.Font, font.ChangeKind);
        Assert.True(font.DisposeCurrentFont);
        Assert.Equal(new TextLabelInvalidation(LayoutUpdateNeeded: false, TextureUpdateNeeded: true), font.Invalidation);
        Assert.Equal(TextLabelPropertyChangeKind.Font, scale.ChangeKind);
        Assert.True(scale.DisposeCurrentFont);
        Assert.Equal(new TextLabelInvalidation(LayoutUpdateNeeded: false, TextureUpdateNeeded: true), scale.Invalidation);
        Assert.Equal(TextLabelPropertyChangeKind.Layout, transform.ChangeKind);
        Assert.Equal(new TextLabelInvalidation(LayoutUpdateNeeded: true, TextureUpdateNeeded: false), transform.Invalidation);
        Assert.Equal(TextLabelPropertyChangeKind.Layout, alignment.ChangeKind);
        Assert.Equal(new TextLabelInvalidation(LayoutUpdateNeeded: true, TextureUpdateNeeded: false), alignment.Invalidation);
    }

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
        Assert.Equal(TextLabelSkipReason.None, layout.SkipReason);
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
        Assert.Equal(TextLabelSkipReason.Offscreen, layout.SkipReason);
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
        Assert.Equal(TextLabelSkipReason.Empty, layout.SkipReason);
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

    [Fact]
    public void TextLabelInvalidationStartsWithLayoutAndTextureUpdatesNeeded()
    {
        TextLabelInvalidation state = TextLabelInvalidation.Initial;

        Assert.True(state.LayoutUpdateNeeded);
        Assert.True(state.TextureUpdateNeeded);
    }

    [Fact]
    public void InvalidateLayoutMarksOnlyLayoutWorkWhenTextureIsClean()
    {
        TextLabelInvalidation state = TextLabelPlan.InvalidateLayout(TextLabelInvalidation.Clean);

        Assert.True(state.LayoutUpdateNeeded);
        Assert.False(state.TextureUpdateNeeded);
    }

    [Fact]
    public void InvalidateTextureMarksOnlyTextureWorkWhenLayoutIsClean()
    {
        TextLabelInvalidation state = TextLabelPlan.InvalidateTexture(TextLabelInvalidation.Clean);

        Assert.False(state.LayoutUpdateNeeded);
        Assert.True(state.TextureUpdateNeeded);
    }

    [Fact]
    public void InvalidateResourcesMarksLayoutAndTextureLikeUdbUnloadResource()
    {
        TextLabelInvalidation state = TextLabelPlan.InvalidateResources();

        Assert.True(state.LayoutUpdateNeeded);
        Assert.True(state.TextureUpdateNeeded);
    }

    [Fact]
    public void BuildUnloadResourcePlanDisposesExistingResourcesAndInvalidatesLikeUdb()
    {
        TextLabelResourceUnloadPlan plan = TextLabelPlan.BuildUnloadResourcePlan(
            hasTexture: true,
            hasVertexBuffer: true);

        Assert.True(plan.DisposeTexture);
        Assert.True(plan.DisposeVertexBuffer);
        Assert.Equal(TextLabelInvalidation.Initial, plan.ResultInvalidation);
    }

    [Fact]
    public void BuildUnloadResourcePlanSkipsMissingResourcesButStillInvalidates()
    {
        TextLabelResourceUnloadPlan plan = TextLabelPlan.BuildUnloadResourcePlan(
            hasTexture: false,
            hasVertexBuffer: false);

        Assert.False(plan.DisposeTexture);
        Assert.False(plan.DisposeVertexBuffer);
        Assert.Equal(TextLabelInvalidation.Initial, plan.ResultInvalidation);
    }

    [Fact]
    public void MarkUpdatedClearsLayoutAndTextureFlagsAfterRendering()
    {
        TextLabelInvalidation state = TextLabelPlan.MarkUpdated();

        Assert.False(state.LayoutUpdateNeeded);
        Assert.False(state.TextureUpdateNeeded);
    }

    [Fact]
    public void UpdateTransformCacheIgnoresTransformValuesWhenCoordinatesAreNotTransformed()
    {
        var cache = new TextLabelTransformCache(false, 0, 0, 0, 0);
        TextLabelInvalidation invalidation = TextLabelInvalidation.Clean;

        TextLabelTransformUpdatePlan plan = TextLabelPlan.UpdateTransformCache(
            transformCoordinates: false,
            cache,
            invalidation,
            translateX: 1,
            translateY: 2,
            scaleX: 3,
            scaleY: 4);

        Assert.Equal(cache, plan.Cache);
        Assert.Equal(invalidation, plan.Invalidation);
    }

    [Fact]
    public void UpdateTransformCacheMarksLayoutDirtyOnFirstTransformedUpdateLikeUdb()
    {
        TextLabelTransformUpdatePlan plan = TextLabelPlan.UpdateTransformCache(
            transformCoordinates: true,
            new TextLabelTransformCache(false, 0, 0, 0, 0),
            TextLabelInvalidation.Clean,
            translateX: 8,
            translateY: -4,
            scaleX: 2,
            scaleY: -2);

        Assert.Equal(new TextLabelTransformCache(true, 8, -4, 2, -2), plan.Cache);
        Assert.True(plan.Invalidation.LayoutUpdateNeeded);
        Assert.False(plan.Invalidation.TextureUpdateNeeded);
    }

    [Fact]
    public void UpdateTransformCacheKeepsCleanStateWhenTransformedValuesMatch()
    {
        var cache = new TextLabelTransformCache(true, 8, -4, 2, -2);

        TextLabelTransformUpdatePlan plan = TextLabelPlan.UpdateTransformCache(
            transformCoordinates: true,
            cache,
            TextLabelInvalidation.Clean,
            translateX: 8,
            translateY: -4,
            scaleX: 2,
            scaleY: -2);

        Assert.Equal(cache, plan.Cache);
        Assert.Equal(TextLabelInvalidation.Clean, plan.Invalidation);
    }

    [Fact]
    public void UpdateTransformCacheMarksLayoutDirtyWhenTransformedValuesChangeLikeUdb()
    {
        TextLabelTransformUpdatePlan plan = TextLabelPlan.UpdateTransformCache(
            transformCoordinates: true,
            new TextLabelTransformCache(true, 8, -4, 2, -2),
            TextLabelInvalidation.Clean,
            translateX: 8,
            translateY: -4,
            scaleX: 3,
            scaleY: -2);

        Assert.Equal(new TextLabelTransformCache(true, 8, -4, 3, -2), plan.Cache);
        Assert.True(plan.Invalidation.LayoutUpdateNeeded);
        Assert.False(plan.Invalidation.TextureUpdateNeeded);
    }

    [Fact]
    public void ResourceUpdatePlanRefreshesLabelImageAndUploadsQuadForVisibleDirtyLabel()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "Visible",
            new TextLabelSize(24, 10),
            new TextLabelPoint(8, 8),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);

        TextLabelResourceUpdatePlan plan = TextLabelPlan.BuildResourceUpdatePlan(
            TextLabelInvalidation.Initial,
            layout,
            hasTexture: true,
            hasVertexBuffer: true,
            vertexBufferDisposed: false);

        Assert.False(plan.DisposeTexture);
        Assert.True(plan.CreateLabelImage);
        Assert.False(plan.CreateTexture);
        Assert.True(plan.UploadTexture);
        Assert.False(plan.CreateVertexBuffer);
        Assert.True(plan.UploadQuadBuffer);
        Assert.Equal(TextLabelInvalidation.Clean, plan.ResultInvalidation);
    }

    [Fact]
    public void ResourceUpdatePlanCreatesTextureOnlyWhenMissing()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "Visible",
            new TextLabelSize(24, 10),
            new TextLabelPoint(8, 8),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);

        TextLabelResourceUpdatePlan plan = TextLabelPlan.BuildResourceUpdatePlan(
            new TextLabelInvalidation(LayoutUpdateNeeded: false, TextureUpdateNeeded: true),
            layout,
            hasTexture: false,
            hasVertexBuffer: true,
            vertexBufferDisposed: false);

        Assert.False(plan.DisposeTexture);
        Assert.True(plan.CreateLabelImage);
        Assert.True(plan.CreateTexture);
        Assert.True(plan.UploadTexture);
        Assert.False(plan.CreateVertexBuffer);
        Assert.False(plan.UploadQuadBuffer);
        Assert.Equal(TextLabelInvalidation.Clean, plan.ResultInvalidation);
    }

    [Fact]
    public void ResourceUpdatePlanRecreatesMissingTextureForVisibleCleanLabel()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "Visible",
            new TextLabelSize(24, 10),
            new TextLabelPoint(8, 8),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);

        TextLabelResourceUpdatePlan plan = TextLabelPlan.BuildResourceUpdatePlan(
            TextLabelInvalidation.Clean,
            layout,
            hasTexture: false,
            hasVertexBuffer: true,
            vertexBufferDisposed: false);

        Assert.False(plan.DisposeTexture);
        Assert.True(plan.CreateLabelImage);
        Assert.True(plan.CreateTexture);
        Assert.True(plan.UploadTexture);
        Assert.False(plan.CreateVertexBuffer);
        Assert.False(plan.UploadQuadBuffer);
        Assert.Equal(TextLabelInvalidation.Clean, plan.ResultInvalidation);
    }

    [Fact]
    public void ResourceUpdatePlanReusesCleanTextureAndBuffer()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "Visible",
            new TextLabelSize(24, 10),
            new TextLabelPoint(8, 8),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);

        TextLabelResourceUpdatePlan plan = TextLabelPlan.BuildResourceUpdatePlan(
            TextLabelInvalidation.Clean,
            layout,
            hasTexture: true,
            hasVertexBuffer: true,
            vertexBufferDisposed: false);

        Assert.False(plan.DisposeTexture);
        Assert.False(plan.CreateLabelImage);
        Assert.False(plan.CreateTexture);
        Assert.False(plan.UploadTexture);
        Assert.False(plan.CreateVertexBuffer);
        Assert.False(plan.UploadQuadBuffer);
        Assert.Equal(TextLabelInvalidation.Clean, plan.ResultInvalidation);
    }

    [Fact]
    public void ResourceUpdatePlanCreatesMissingOrDisposedVertexBuffer()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "Visible",
            new TextLabelSize(24, 10),
            new TextLabelPoint(8, 8),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);

        TextLabelResourceUpdatePlan missing = TextLabelPlan.BuildResourceUpdatePlan(
            TextLabelInvalidation.Clean,
            layout,
            hasTexture: true,
            hasVertexBuffer: false,
            vertexBufferDisposed: false);
        TextLabelResourceUpdatePlan disposed = TextLabelPlan.BuildResourceUpdatePlan(
            TextLabelInvalidation.Clean,
            layout,
            hasTexture: true,
            hasVertexBuffer: true,
            vertexBufferDisposed: true);

        Assert.True(missing.CreateVertexBuffer);
        Assert.True(missing.UploadQuadBuffer);
        Assert.True(disposed.CreateVertexBuffer);
        Assert.True(disposed.UploadQuadBuffer);
    }

    [Fact]
    public void ResourceUpdatePlanSkipsOffscreenLabelAndKeepsInvalidation()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "Skipped",
            new TextLabelSize(24, 10),
            new TextLabelPoint(400, 8),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);

        TextLabelResourceUpdatePlan plan = TextLabelPlan.BuildResourceUpdatePlan(
            TextLabelInvalidation.Initial,
            layout,
            hasTexture: false,
            hasVertexBuffer: true,
            vertexBufferDisposed: false);

        Assert.False(plan.DisposeTexture);
        Assert.False(plan.CreateLabelImage);
        Assert.False(plan.CreateTexture);
        Assert.False(plan.UploadTexture);
        Assert.False(plan.CreateVertexBuffer);
        Assert.False(plan.UploadQuadBuffer);
        Assert.Equal(TextLabelInvalidation.Initial, plan.ResultInvalidation);
    }

    [Fact]
    public void ResourceUpdatePlanDisposesOffscreenTextureAndKeepsInvalidation()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "Skipped",
            new TextLabelSize(24, 10),
            new TextLabelPoint(400, 8),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);

        TextLabelResourceUpdatePlan plan = TextLabelPlan.BuildResourceUpdatePlan(
            TextLabelInvalidation.Initial,
            layout,
            hasTexture: true,
            hasVertexBuffer: true,
            vertexBufferDisposed: false);

        Assert.True(layout.SkipRendering);
        Assert.Equal(TextLabelSkipReason.Offscreen, layout.SkipReason);
        Assert.True(plan.DisposeTexture);
        Assert.False(plan.CreateLabelImage);
        Assert.False(plan.CreateTexture);
        Assert.False(plan.UploadTexture);
        Assert.False(plan.CreateVertexBuffer);
        Assert.False(plan.UploadQuadBuffer);
        Assert.Equal(TextLabelInvalidation.Initial, plan.ResultInvalidation);
    }

    [Fact]
    public void ResourceUpdatePlanClearsInvalidationForEmptyTextLikeUdb()
    {
        TextLabelLayout layout = TextLabelPlan.Build(
            "",
            new TextLabelSize(24, 10),
            new TextLabelPoint(8, 8),
            alignX: TextLabelAlignmentX.Left,
            viewportWidth: 320,
            viewportHeight: 200);

        TextLabelResourceUpdatePlan plan = TextLabelPlan.BuildResourceUpdatePlan(
            TextLabelInvalidation.Initial,
            layout,
            hasTexture: true,
            hasVertexBuffer: true,
            vertexBufferDisposed: false);

        Assert.True(layout.SkipRendering);
        Assert.Equal(TextLabelSkipReason.Empty, layout.SkipReason);
        Assert.False(plan.DisposeTexture);
        Assert.False(plan.CreateLabelImage);
        Assert.False(plan.CreateTexture);
        Assert.False(plan.UploadTexture);
        Assert.False(plan.CreateVertexBuffer);
        Assert.False(plan.UploadQuadBuffer);
        Assert.Equal(TextLabelInvalidation.Clean, plan.ResultInvalidation);
    }

    [Fact]
    public void IsInViewportAllowsTextureSizePaddingOnXAxisLikeUdb()
    {
        var viewport = new TextLabelRectangle(100, 100, 80, -60);
        var textureSize = new TextLabelSize(16, 8);

        Assert.True(TextLabelPlan.IsInViewport(new TextLabelPoint(84, 80), textureSize, viewport));
        Assert.True(TextLabelPlan.IsInViewport(new TextLabelPoint(195, 80), textureSize, viewport));
        Assert.False(TextLabelPlan.IsInViewport(new TextLabelPoint(83.9, 80), textureSize, viewport));
        Assert.False(TextLabelPlan.IsInViewport(new TextLabelPoint(196, 80), textureSize, viewport));
    }

    [Fact]
    public void IsInViewportUsesUdbInvertedYAxisBounds()
    {
        var viewport = new TextLabelRectangle(100, 100, 80, -60);
        var textureSize = new TextLabelSize(16, 8);

        Assert.True(TextLabelPlan.IsInViewport(new TextLabelPoint(120, 92), textureSize, viewport));
        Assert.True(TextLabelPlan.IsInViewport(new TextLabelPoint(120, 49), textureSize, viewport));
        Assert.False(TextLabelPlan.IsInViewport(new TextLabelPoint(120, 93), textureSize, viewport));
        Assert.False(TextLabelPlan.IsInViewport(new TextLabelPoint(120, 48), textureSize, viewport));
    }

    [Fact]
    public void IsInViewportUsesZeroPaddingForEmptyTextureSize()
    {
        var viewport = new TextLabelRectangle(100, 100, 80, -60);

        Assert.True(TextLabelPlan.IsInViewport(new TextLabelPoint(100, 99), new TextLabelSize(0, 0), viewport));
        Assert.False(TextLabelPlan.IsInViewport(new TextLabelPoint(99.9, 99), new TextLabelSize(0, 0), viewport));
    }
}
