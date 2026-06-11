// ABOUTME: Verifies UDB Renderer2D thing batch upload and draw count planning.
// ABOUTME: Pins 2D thing buffer chunk sizes against upstream Renderer2D expressions.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class ThingBatchRenderPlannerTests
{
    private static string? FindUdbRoot()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "."));
        string sibling = Path.GetFullPath(Path.Combine(repositoryRoot, "..", "UltimateDoomBuilder"));
        if (File.Exists(Path.Combine(sibling, "Source", "Core", "Rendering", "Renderer2D.cs"))) return sibling;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string root = Path.Combine(home, "dev", "repos", "UltimateDoomBuilder");
        return File.Exists(Path.Combine(root, "Source", "Core", "Rendering", "Renderer2D.cs")) ? root : null;
    }

    [Fact]
    public void EmptyThingBatchProducesNoDraws()
    {
        Assert.Empty(ThingBatchRenderPlanner.BuildDraws(0));
    }

    [Fact]
    public void ThingBatchAtCapacityUsesOneDraw()
    {
        ThingBatchDraw draw = Assert.Single(ThingBatchRenderPlanner.BuildDraws(
            PresentationRenderTargetPlan.ThingBufferSize));

        Assert.Equal(0, draw.StartIndex);
        Assert.Equal(100, draw.ItemCount);
        Assert.Equal(600, draw.VertexCount);
        Assert.Equal(200, draw.TriangleCount);
    }

    [Fact]
    public void ThingBatchOverCapacityUsesUdbChunks()
    {
        IReadOnlyList<ThingBatchDraw> draws = ThingBatchRenderPlanner.BuildDraws(250);

        Assert.Equal(new[]
        {
            new ThingBatchDraw(0, 100, 600, 200),
            new ThingBatchDraw(100, 100, 600, 200),
            new ThingBatchDraw(200, 50, 300, 100),
        }, draws);
    }

    [Fact]
    public void ThingBatchDrawsRejectInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildDraws(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildDraws(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildDraws(1, -1));
    }

    [Fact]
    public void SetupPlanMatchesUdbThingBatchRenderState()
    {
        ThingBatchSetupPlan plan = ThingBatchRenderPlanner.BuildSetupPlan(0.66f);

        Assert.Equal(Cull.None, plan.CullMode);
        Assert.False(plan.DepthEnabled);
        Assert.True(plan.AlphaBlendEnabled);
        Assert.Equal(Blend.SourceAlpha, plan.SourceBlend);
        Assert.Equal(Blend.InverseSourceAlpha, plan.DestinationBlend);
        Assert.False(plan.AlphaTestEnabled);
        Assert.True(plan.BindThingTexture);
        Assert.True(plan.ResetWorldTransformation);
        Assert.Equal(ShaderName.things2d_thing, plan.Shader);
        Assert.Equal(0.66f, plan.Alpha);
    }

    [Fact]
    public void SetupPlanRejectsInvalidAlpha()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSetupPlan(float.NaN));
    }

    [Theory]
    [InlineData(0, 6)]
    [InlineData(45, 5)]
    [InlineData(90, 4)]
    [InlineData(180, 2)]
    [InlineData(270, 0)]
    [InlineData(315, 7)]
    [InlineData(360, 6)]
    [InlineData(-45, 7)]
    public void SpriteFrameAngleIndexMatchesUdbEightAngleFormula(int angleDoom, int expected)
    {
        Assert.Equal(expected, ThingBatchRenderPlanner.SpriteFrameAngleIndex(
            angleDoom,
            ThingBatchRenderPlanner.SpriteAngleFrameCount));
    }

    [Fact]
    public void SpriteFrameAngleIndexUsesZeroForNonEightFrameSprites()
    {
        Assert.Equal(0, ThingBatchRenderPlanner.SpriteFrameAngleIndex(90, spriteFrameCount: 0));
        Assert.Equal(0, ThingBatchRenderPlanner.SpriteFrameAngleIndex(90, spriteFrameCount: 1));
        Assert.Equal(0, ThingBatchRenderPlanner.SpriteFrameAngleIndex(90, spriteFrameCount: 4));
    }

    [Fact]
    public void SpriteFrameAngleIndexRejectsInvalidFrameCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.SpriteFrameAngleIndex(0, -1));
    }

    [Fact]
    public void SpriteRenderDecisionSkipsModelSpritesWhenModelRenderWillDrawSelection()
    {
        ThingSpriteRenderDecision decision = ThingBatchRenderPlanner.BuildSpriteRenderDecision(
            ThingRenderMode.MODEL,
            ModelRenderMode.SELECTION,
            selected: true,
            alpha: 0.5f,
            forceSpriteRendering: false,
            spriteSize: 32);

        Assert.True(decision.SkipForModelRender);
        Assert.False(decision.RenderSprite);
        Assert.False(decision.MarkArrowLarge);
    }

    [Fact]
    public void SpriteRenderDecisionSkipsVoxelSpritesForActiveFilterAlpha()
    {
        ThingSpriteRenderDecision decision = ThingBatchRenderPlanner.BuildSpriteRenderDecision(
            ThingRenderMode.VOXEL,
            ModelRenderMode.ACTIVE_THINGS_FILTER,
            selected: false,
            alpha: 1.0f,
            forceSpriteRendering: false,
            spriteSize: 32);

        Assert.True(decision.SkipForModelRender);
        Assert.False(decision.RenderSprite);
        Assert.False(decision.MarkArrowLarge);
    }

    [Fact]
    public void SpriteRenderDecisionKeepsModelSpriteWhenModeDoesNotDrawModel()
    {
        ThingSpriteRenderDecision decision = ThingBatchRenderPlanner.BuildSpriteRenderDecision(
            ThingRenderMode.MODEL,
            ModelRenderMode.SELECTION,
            selected: false,
            alpha: 1.0f,
            forceSpriteRendering: false,
            spriteSize: 32);

        Assert.False(decision.SkipForModelRender);
        Assert.True(decision.RenderSprite);
        Assert.False(decision.MarkArrowLarge);
    }

    [Fact]
    public void SpriteRenderDecisionMarksArrowLargeWhenSpriteIsTooSmall()
    {
        ThingSpriteRenderDecision decision = ThingBatchRenderPlanner.BuildSpriteRenderDecision(
            ThingRenderMode.NORMAL,
            ModelRenderMode.NONE,
            selected: false,
            alpha: 1.0f,
            forceSpriteRendering: false,
            spriteSize: ThingBatchRenderPlanner.MinimumSpriteRadius - 0.01);

        Assert.False(decision.SkipForModelRender);
        Assert.False(decision.RenderSprite);
        Assert.True(decision.MarkArrowLarge);
    }

    [Fact]
    public void SpriteRenderDecisionForceRendersSmallSpritesLikeUdb()
    {
        ThingSpriteRenderDecision decision = ThingBatchRenderPlanner.BuildSpriteRenderDecision(
            ThingRenderMode.NORMAL,
            ModelRenderMode.NONE,
            selected: false,
            alpha: 1.0f,
            forceSpriteRendering: true,
            spriteSize: 1);

        Assert.False(decision.SkipForModelRender);
        Assert.True(decision.RenderSprite);
        Assert.False(decision.MarkArrowLarge);
    }

    [Fact]
    public void SpriteRenderDecisionRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSpriteRenderDecision(
            ThingRenderMode.NORMAL,
            ModelRenderMode.NONE,
            selected: false,
            alpha: float.NaN,
            forceSpriteRendering: false,
            spriteSize: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSpriteRenderDecision(
            ThingRenderMode.NORMAL,
            ModelRenderMode.NONE,
            selected: false,
            alpha: 1,
            forceSpriteRendering: false,
            spriteSize: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSpriteRenderDecision(
            ThingRenderMode.NORMAL,
            ModelRenderMode.NONE,
            selected: false,
            alpha: 1,
            forceSpriteRendering: false,
            spriteSize: double.NaN));
    }

    [Fact]
    public void BatchItemDecisionSkipsHighlightedThingsOutsideFixedColorRendering()
    {
        ThingBatchItemDecision decision = ThingBatchRenderPlanner.BuildItemDecision(
            ThingRenderMode.MODEL,
            fixedColor: false,
            highlighted: true,
            selected: false,
            thingsMode: true,
            alpha: 1.0f);

        Assert.True(decision.SkipHighlighted);
        Assert.False(decision.CollectModel);
        Assert.Equal(128, decision.BoundingBoxAlpha);
    }

    [Fact]
    public void BatchItemDecisionKeepsHighlightedThingsInFixedColorRendering()
    {
        ThingBatchItemDecision decision = ThingBatchRenderPlanner.BuildItemDecision(
            ThingRenderMode.MODEL,
            fixedColor: true,
            highlighted: true,
            selected: false,
            thingsMode: true,
            alpha: 1.0f);

        Assert.False(decision.SkipHighlighted);
        Assert.True(decision.CollectModel);
        Assert.Equal(255, decision.BoundingBoxAlpha);
    }

    [Theory]
    [InlineData(ThingRenderMode.MODEL, true)]
    [InlineData(ThingRenderMode.VOXEL, true)]
    [InlineData(ThingRenderMode.NORMAL, false)]
    [InlineData(ThingRenderMode.WALLSPRITE, false)]
    public void BatchItemDecisionCollectsModelsAndVoxelsOnly(ThingRenderMode renderMode, bool expected)
    {
        ThingBatchItemDecision decision = ThingBatchRenderPlanner.BuildItemDecision(
            renderMode,
            fixedColor: false,
            highlighted: false,
            selected: false,
            thingsMode: false,
            alpha: 1.0f);

        Assert.Equal(expected, decision.CollectModel);
    }

    [Fact]
    public void BatchItemDecisionDimsUnselectedThingModeBoundingBoxAlphaLikeUdb()
    {
        ThingBatchItemDecision unselected = ThingBatchRenderPlanner.BuildItemDecision(
            ThingRenderMode.NORMAL,
            fixedColor: false,
            highlighted: false,
            selected: false,
            thingsMode: true,
            alpha: 0.5f);
        ThingBatchItemDecision selected = ThingBatchRenderPlanner.BuildItemDecision(
            ThingRenderMode.NORMAL,
            fixedColor: false,
            highlighted: false,
            selected: true,
            thingsMode: true,
            alpha: 0.5f);

        Assert.Equal(64, unselected.BoundingBoxAlpha);
        Assert.Equal(127, selected.BoundingBoxAlpha);
    }

    [Fact]
    public void BatchItemDecisionRejectsInvalidAlpha()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildItemDecision(
            ThingRenderMode.NORMAL,
            fixedColor: false,
            highlighted: false,
            selected: false,
            thingsMode: false,
            alpha: float.NaN));
    }

    [Fact]
    public void Model2DPassPlanMatchesUdbWireframeStateAndAlpha()
    {
        ThingModel2DPassPlan plan = ThingBatchRenderPlanner.BuildModel2DPassPlan(
            ModelRenderMode.ALL,
            alpha: 0.5f,
            currentColor: new PixelColor(255, 1, 2, 3),
            highlightColor: new PixelColor(255, 4, 5, 6),
            selectionColor: new PixelColor(255, 20, 40, 60),
            modelWireColor: new PixelColor(255, 80, 100, 120));

        Assert.True(plan.RenderModels);
        Assert.False(plan.AlphaBlendEnabled);
        Assert.Equal(FillMode.Wireframe, plan.FillMode);
        Assert.Equal(FillMode.Solid, plan.RestoreFillMode);
        Assert.Equal(ShaderName.things2d_fill, plan.Shader);
        AssertColor(plan.SelectionColor, 20, 40, 60, 0.125f);
        AssertColor(plan.WireColor, 80, 100, 120, 0.125f);
    }

    [Fact]
    public void Model2DPassPlanUsesHighlightWireColorForHighlightedFixedColorPass()
    {
        PixelColor highlight = new(255, 4, 5, 6);

        ThingModel2DPassPlan plan = ThingBatchRenderPlanner.BuildModel2DPassPlan(
            ModelRenderMode.SELECTION,
            alpha: 1.0f,
            currentColor: highlight,
            highlightColor: highlight,
            selectionColor: new PixelColor(255, 20, 40, 60),
            modelWireColor: new PixelColor(255, 80, 100, 120));

        Assert.True(plan.RenderModels);
        AssertColor(plan.WireColor, 4, 5, 6, 0.6f);
        AssertColor(plan.SelectionColor, 20, 40, 60, 0.6f);
    }

    [Fact]
    public void Model2DPassPlanSkipsModelPassWhenModeIsNone()
    {
        ThingModel2DPassPlan plan = ThingBatchRenderPlanner.BuildModel2DPassPlan(
            ModelRenderMode.NONE,
            alpha: 1.0f,
            currentColor: new PixelColor(255, 1, 2, 3),
            highlightColor: new PixelColor(255, 4, 5, 6),
            selectionColor: new PixelColor(255, 20, 40, 60),
            modelWireColor: new PixelColor(255, 80, 100, 120));

        Assert.False(plan.RenderModels);
    }

    [Theory]
    [InlineData(ModelRenderMode.NONE, true, 1.0f, false)]
    [InlineData(ModelRenderMode.SELECTION, false, 1.0f, false)]
    [InlineData(ModelRenderMode.SELECTION, true, 0.5f, true)]
    [InlineData(ModelRenderMode.ACTIVE_THINGS_FILTER, false, 0.5f, false)]
    [InlineData(ModelRenderMode.ACTIVE_THINGS_FILTER, false, 1.0f, true)]
    [InlineData(ModelRenderMode.ALL, false, 0.5f, true)]
    public void ShouldRenderModel2DMatchesUdbModelModeGate(
        ModelRenderMode mode,
        bool selected,
        float alpha,
        bool expected)
    {
        Assert.Equal(expected, ThingBatchRenderPlanner.ShouldRenderModel2D(mode, selected, alpha));
    }

    [Fact]
    public void Model2DVisibilityMatchesUdbRadiusCulling()
    {
        Assert.True(ThingBatchRenderPlanner.IsModel2DVisible(
            screenX: 50,
            screenY: 50,
            modelRadius: 10,
            viewScale: 2,
            thingScaleX: 1.5,
            actorScaleWidth: 2,
            windowWidth: 100,
            windowHeight: 100));
        Assert.False(ThingBatchRenderPlanner.IsModel2DVisible(
            screenX: -61,
            screenY: 50,
            modelRadius: 10,
            viewScale: 2,
            thingScaleX: 1.5,
            actorScaleWidth: 2,
            windowWidth: 100,
            windowHeight: 100));
        Assert.False(ThingBatchRenderPlanner.IsModel2DVisible(
            screenX: 161,
            screenY: 50,
            modelRadius: 10,
            viewScale: 2,
            thingScaleX: 1.5,
            actorScaleWidth: 2,
            windowWidth: 100,
            windowHeight: 100));
    }

    [Fact]
    public void Model2DHelpersRejectInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildModel2DPassPlan(
            ModelRenderMode.ALL,
            alpha: float.NaN,
            currentColor: new PixelColor(),
            highlightColor: new PixelColor(),
            selectionColor: new PixelColor(),
            modelWireColor: new PixelColor()));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.ShouldRenderModel2D(
            ModelRenderMode.ALL,
            selected: false,
            alpha: float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.IsModel2DVisible(
            double.NaN,
            0,
            1,
            1,
            1,
            1,
            100,
            100));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.IsModel2DVisible(
            0,
            0,
            -1,
            1,
            1,
            1,
            100,
            100));
    }

    [Fact]
    public void ArrowTextureBoundsMatchUdbSpriteState()
    {
        Assert.Equal(new ThingArrowTextureBounds(0.501f, 0.999f, 0.001f, 0.999f),
            ThingBatchRenderPlanner.ArrowTextureBounds(spriteSkipped: false));
        Assert.Equal(new ThingArrowTextureBounds(0.625f, 0.874f, -0.039f, 0.46f),
            ThingBatchRenderPlanner.ArrowTextureBounds(spriteSkipped: true));
    }

    [Fact]
    public void ArrowVerticesMatchUdbRotatedQuadOrder()
    {
        FlatVertex[] vertices = ThingBatchRenderPlanner.BuildArrowVertices(
            screenX: 10,
            screenY: 20,
            angleRadians: 0,
            arrowSize: Math.Sqrt(2),
            spriteSkipped: false);

        Assert.Equal(6, vertices.Length);
        AssertVertex(vertices[0], 11, 21, -1, 0.501f, 0.001f);
        AssertVertex(vertices[1], 9, 21, -1, 0.999f, 0.001f);
        AssertVertex(vertices[2], 11, 19, -1, 0.501f, 0.999f);
        Assert.Equal(vertices[1], vertices[3]);
        Assert.Equal(vertices[2], vertices[4]);
        AssertVertex(vertices[5], 9, 19, -1, 0.999f, 0.999f);
    }

    [Fact]
    public void ArrowVerticesUseLargeArrowTextureWhenSpriteWasSkipped()
    {
        FlatVertex[] vertices = ThingBatchRenderPlanner.BuildArrowVertices(
            screenX: 0,
            screenY: 0,
            angleRadians: 0,
            arrowSize: 1,
            spriteSkipped: true);

        Assert.Equal(0.625f, vertices[0].u);
        Assert.Equal(-0.039f, vertices[0].v);
        Assert.Equal(0.874f, vertices[1].u);
        Assert.Equal(0.46f, vertices[2].v);
        Assert.Equal(0.874f, vertices[5].u);
        Assert.Equal(0.46f, vertices[5].v);
    }

    [Fact]
    public void ArrowVerticesRejectInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildArrowVertices(double.NaN, 0, 0, 1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildArrowVertices(0, double.NaN, 0, 1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildArrowVertices(0, 0, double.NaN, 1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildArrowVertices(0, 0, 0, -1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildArrowVertices(0, 0, 0, double.NaN, false));
    }

    [Fact]
    public void SpriteVerticesMatchUdbQuadOrder()
    {
        FlatVertex[] vertices = ThingBatchRenderPlanner.BuildSpriteVertices(
            screenX: 10,
            screenY: 20,
            width: 3,
            height: 4,
            color: 0x123456,
            mirror: false);

        Assert.Equal(6, vertices.Length);
        AssertVertex(vertices[0], 7, 16, 0x123456, 0.0f, 0.0f);
        AssertVertex(vertices[1], 13, 16, 0x123456, 1.0f, 0.0f);
        AssertVertex(vertices[2], 7, 24, 0x123456, 0.0f, 1.0f);
        Assert.Equal(vertices[1], vertices[3]);
        Assert.Equal(vertices[2], vertices[4]);
        AssertVertex(vertices[5], 13, 24, 0x123456, 1.0f, 1.0f);
    }

    [Fact]
    public void SpriteVerticesMirrorHorizontalTextureCoordinatesLikeUdb()
    {
        FlatVertex[] vertices = ThingBatchRenderPlanner.BuildSpriteVertices(
            screenX: 0,
            screenY: 0,
            width: 1,
            height: 1,
            color: -1,
            mirror: true);

        Assert.Equal(1.0f, vertices[0].u);
        Assert.Equal(0.0f, vertices[1].u);
        Assert.Equal(1.0f, vertices[2].u);
        Assert.Equal(0.0f, vertices[5].u);
    }

    [Fact]
    public void SpriteVerticesRejectInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSpriteVertices(double.NaN, 0, 1, 1, -1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSpriteVertices(0, double.NaN, 1, 1, -1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSpriteVertices(0, 0, -1, 1, -1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSpriteVertices(0, 0, double.NaN, 1, -1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSpriteVertices(0, 0, 1, -1, -1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSpriteVertices(0, 0, 1, double.NaN, -1, false));
    }

    [Fact]
    public void BoxPlanMatchesUdbThingMarkerQuadOrder()
    {
        ThingBoxRenderPlan plan = ThingBatchRenderPlanner.BuildBoxPlan(
            screenX: 10,
            screenY: 20,
            circleSize: 3,
            boundingBoxSize: -1,
            color: 0x123456,
            boundingBoxColor: 0x654321);

        Assert.Empty(plan.BoundingBoxLines);
        Assert.Equal(6, plan.Vertices.Length);
        AssertVertex(plan.Vertices[0], 7, 17, 0x123456, 0.0f, 0.0f);
        AssertVertex(plan.Vertices[1], 13, 17, 0x123456, 0.5f, 0.0f);
        AssertVertex(plan.Vertices[2], 7, 23, 0x123456, 0.0f, 1.0f);
        Assert.Equal(plan.Vertices[1], plan.Vertices[3]);
        Assert.Equal(plan.Vertices[2], plan.Vertices[4]);
        AssertVertex(plan.Vertices[5], 13, 23, 0x123456, 0.5f, 1.0f);
    }

    [Fact]
    public void BoxPlanAddsUdbFixedScaleBoundingBoxLines()
    {
        ThingBoxRenderPlan plan = ThingBatchRenderPlanner.BuildBoxPlan(
            screenX: 10,
            screenY: 20,
            circleSize: 3,
            boundingBoxSize: 5,
            color: 0x123456,
            boundingBoxColor: 0x654321);

        Assert.Equal(new[]
        {
            new ThingBoxLine(5, 15, 15, 15, 0x654321),
            new ThingBoxLine(15, 15, 15, 25, 0x654321),
            new ThingBoxLine(5, 25, 15, 25, 0x654321),
            new ThingBoxLine(5, 15, 5, 25, 0x654321),
        }, plan.BoundingBoxLines);
    }

    [Fact]
    public void BoxPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildBoxPlan(double.NaN, 0, 1, -1, -1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildBoxPlan(0, double.NaN, 1, -1, -1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildBoxPlan(0, 0, -1, -1, -1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildBoxPlan(0, 0, double.NaN, -1, -1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildBoxPlan(0, 0, 1, double.NaN, -1, -1));
    }

    [Fact]
    public void ThingBatchExpressionsMatchUdbRenderer2DWhenCloneIsAvailable()
    {
        string? udbRoot = FindUdbRoot();
        if (udbRoot == null) return;

        string source = File.ReadAllText(Path.Combine(udbRoot, "Source", "Core", "Rendering", "Renderer2D.cs"));

        Assert.Contains("FlatVertex[] verts = new FlatVertex[THING_BUFFER_SIZE * 6];", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetCullMode(Cull.None);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetZEnable(false);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetAlphaBlendEnable(true);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetSourceBlend(Blend.SourceAlpha);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetDestinationBlend(Blend.InverseSourceAlpha);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetAlphaTestEnable(false);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetTexture(General.Map.Data.ThingTexture.Texture);", source, StringComparison.Ordinal);
        Assert.Contains("SetWorldTransformation(false);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetShader(ShaderName.things2d_thing);", source, StringComparison.Ordinal);
        Assert.Contains("SetThings2DSettings(alpha);", source, StringComparison.Ordinal);
        Assert.Contains("if(info.SpriteFrame.Length == 8)", source, StringComparison.Ordinal);
        Assert.Contains("int spriteangle = General.ClampAngle(-t.AngleDoom + 270) / 45;", source, StringComparison.Ordinal);
        Assert.Contains("thingsbyangle[0] = group.Value;", source, StringComparison.Ordinal);
        Assert.Contains("if((t.RenderMode == ThingRenderMode.MODEL || t.RenderMode == ThingRenderMode.VOXEL)", source, StringComparison.Ordinal);
        Assert.Contains("&& ((General.Settings.GZDrawModelsMode == ModelRenderMode.SELECTION && t.Selected) || (General.Settings.GZDrawModelsMode == ModelRenderMode.ACTIVE_THINGS_FILTER && alpha == 1.0f)))", source, StringComparison.Ordinal);
        Assert.Contains("float spritesize = Math.Max(spritewidth, spriteheight);", source, StringComparison.Ordinal);
        Assert.Contains("if(!forcespriterendering && spritesize < MINIMUM_SPRITE_RADIUS)", source, StringComparison.Ordinal);
        Assert.Contains("v.z = -1;", source, StringComparison.Ordinal);
        Assert.Contains("if(!fixedcolor && t.Highlighted) continue;", source, StringComparison.Ordinal);
        Assert.Contains("if(t.RenderMode == ThingRenderMode.MODEL || t.RenderMode == ThingRenderMode.VOXEL)", source, StringComparison.Ordinal);
        Assert.Contains("byte bboxalpha = (byte)(alpha * ((!fixedcolor && !t.Selected && isthingsmode) ? 128 : 255));", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetBufferSubdata(thingsvertices, verts, buffercount * 6);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.Draw(PrimitiveType.TriangleList, 0, buffercount * 2);", source, StringComparison.Ordinal);
        Assert.Contains("locksize = ((things.Count - totalcount) > THING_BUFFER_SIZE) ? THING_BUFFER_SIZE : (things.Count - totalcount);", source, StringComparison.Ordinal);
        Assert.Contains("locksize = ((framegroup.Value.Count - totalcount) > THING_BUFFER_SIZE) ? THING_BUFFER_SIZE : (framegroup.Value.Count - totalcount);", source, StringComparison.Ordinal);
        Assert.Contains("locksize = ((thingsByPosition.Count - totalcount) > THING_BUFFER_SIZE) ? THING_BUFFER_SIZE : (thingsByPosition.Count - totalcount);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetAlphaBlendEnable(false);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetFillMode(FillMode.Wireframe);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetShader(ShaderName.things2d_fill);", source, StringComparison.Ordinal);
        Assert.Contains("Color4 cWire = ((c.ToInt() == General.Colors.Highlight.ToInt()) ? General.Colors.Highlight.ToColorValue() : General.Colors.ModelWireframe.ToColorValue());", source, StringComparison.Ordinal);
        Assert.Contains("cSelection.Alpha = ((alpha < 1.0f) ? alpha * 0.25f : 0.6f);", source, StringComparison.Ordinal);
        Assert.Contains("if((General.Settings.GZDrawModelsMode == ModelRenderMode.SELECTION && !t.Selected) || (General.Settings.GZDrawModelsMode == ModelRenderMode.ACTIVE_THINGS_FILTER && alpha < 1.0f)) continue;", source, StringComparison.Ordinal);
        Assert.Contains("double modelScale = scale * t.ActorScale.Width * t.ScaleX;", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetFillMode(FillMode.Solid);", source, StringComparison.Ordinal);
        Assert.Contains("float sinarrowsize = (float)Math.Sin(t.Angle + Angle2D.PI * 0.25f) * arrowsize;", source, StringComparison.Ordinal);
        Assert.Contains("float cosarrowsize = (float)Math.Cos(t.Angle + Angle2D.PI * 0.25f) * arrowsize;", source, StringComparison.Ordinal);
        Assert.Contains("ul = 0.625f;", source, StringComparison.Ordinal);
        Assert.Contains("ur = 0.874f;", source, StringComparison.Ordinal);
        Assert.Contains("ul = 0.501f;", source, StringComparison.Ordinal);
        Assert.Contains("ur = 0.999f;", source, StringComparison.Ordinal);
        Assert.Contains("float ul = (mirror ? 1f : 0f);", source, StringComparison.Ordinal);
        Assert.Contains("float ur = (mirror ? 0f : 1f);", source, StringComparison.Ordinal);
        Assert.Contains("verts[offset].x = (float)screenpos.x - width;", source, StringComparison.Ordinal);
        Assert.Contains("verts[offset].x = (float)screenpos.x + width;", source, StringComparison.Ordinal);
        Assert.Contains("verts[offset].y = (float)screenpos.y + height;", source, StringComparison.Ordinal);
        Assert.Contains("if(t.Size * scale < MINIMUM_THING_RADIUS) return false;", source, StringComparison.Ordinal);
        Assert.Contains("float screensize = (bboxsize > 0 ? bboxsize : circlesize);", source, StringComparison.Ordinal);
        Assert.Contains("verts[offset].u = 0.5f;", source, StringComparison.Ordinal);
        Assert.Contains("bboxes.Add(new Line3D(tl, tr, boxcolor, false));", source, StringComparison.Ordinal);
        Assert.Contains("bboxes.Add(new Line3D(tr, br, boxcolor, false));", source, StringComparison.Ordinal);
        Assert.Contains("bboxes.Add(new Line3D(bl, br, boxcolor, false));", source, StringComparison.Ordinal);
        Assert.Contains("bboxes.Add(new Line3D(tl, bl, boxcolor, false));", source, StringComparison.Ordinal);
    }

    private static void AssertVertex(FlatVertex vertex, float x, float y, int color, float u, float v)
    {
        Assert.Equal(x, vertex.x, precision: 5);
        Assert.Equal(y, vertex.y, precision: 5);
        Assert.Equal(color, vertex.c);
        Assert.Equal(u, vertex.u);
        Assert.Equal(v, vertex.v);
    }

    private static void AssertColor(Color4 color, byte red, byte green, byte blue, float alpha)
    {
        Assert.Equal(red * PixelColor.ByteToFloat, color.Red, precision: 5);
        Assert.Equal(green * PixelColor.ByteToFloat, color.Green, precision: 5);
        Assert.Equal(blue * PixelColor.ByteToFloat, color.Blue, precision: 5);
        Assert.Equal(alpha, color.Alpha, precision: 5);
    }
}
