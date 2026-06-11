// ABOUTME: Verifies UDB-style Renderer3D.StartGeometry collection lifecycle planning.
// ABOUTME: Pins solid, masked, translucent, sky, model, light, and all-things buckets.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class Renderer3DGeometryLifecyclePlanTests
{
    private static string? FindUdbRoot()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "."));
        string sibling = Path.GetFullPath(Path.Combine(repositoryRoot, "..", "UltimateDoomBuilder"));
        if (File.Exists(Path.Combine(sibling, "Source", "Core", "Rendering", "Renderer3D.cs"))) return sibling;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string root = Path.Combine(home, "dev", "repos", "UltimateDoomBuilder");
        return File.Exists(Path.Combine(root, "Source", "Core", "Rendering", "Renderer3D.cs")) ? root : null;
    }

    [Fact]
    public void BuildStartGeometryPlanInitializesUdbBucketsInOrder()
    {
        Renderer3DStartGeometryPlan plan = Renderer3DGeometryLifecyclePlan.BuildStartGeometryPlan();

        Assert.True(plan.InitializesAllBuckets);
        Assert.Equal(
            [
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.SolidGeometry, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.MaskedGeometry, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.TranslucentGeometry, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.SkyGeometry, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.SolidThings, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.MaskedThings, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.TranslucentThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.MaskedModelThings, Renderer3DGeometryCollectionKind.ModelDictionary, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.TranslucentModelThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.LightThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.AllThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
            ],
            plan.Buckets);
    }

    [Fact]
    public void BuildFinishGeometryCleanupPlanMatchesUdbReferenceCleanup()
    {
        Renderer3DFinishGeometryCleanupPlan plan = Renderer3DGeometryLifecyclePlan.BuildFinishGeometryCleanupPlan();

        Assert.True(plan.UnbindTexture);
        Assert.Equal(
            [
                Renderer3DGeometryBucketKind.SolidGeometry,
                Renderer3DGeometryBucketKind.MaskedGeometry,
                Renderer3DGeometryBucketKind.TranslucentGeometry,
                Renderer3DGeometryBucketKind.SkyGeometry,
                Renderer3DGeometryBucketKind.SolidThings,
                Renderer3DGeometryBucketKind.MaskedThings,
                Renderer3DGeometryBucketKind.TranslucentThings,
                Renderer3DGeometryBucketKind.AllThings,
                Renderer3DGeometryBucketKind.LightThings,
                Renderer3DGeometryBucketKind.MaskedModelThings,
                Renderer3DGeometryBucketKind.TranslucentModelThings,
                Renderer3DGeometryBucketKind.VisualVertices,
            ],
            plan.ClearedBuckets);
    }

    [Fact]
    public void BuildFinishGeometryInitialStatePlanMatchesUdbRenderStates()
    {
        Renderer3DFinishGeometryInitialStatePlan plan = Renderer3DGeometryLifecyclePlan.BuildFinishGeometryInitialStatePlan();

        Assert.Equal(Cull.Clockwise, plan.CullMode);
        Assert.True(plan.DepthEnabled);
        Assert.True(plan.DepthWriteEnabled);
        Assert.False(plan.AlphaBlendEnabled);
        Assert.False(plan.AlphaTestEnabled);
    }

    [Fact]
    public void BuildSkySolidPassPlanSkipsSkyWhenNoSkyGeometryAndAlwaysRendersSolidPass()
    {
        Renderer3DSkySolidPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildSkySolidPassPlan(skyGeometryCount: 0);

        Assert.Equal(
            [
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetIdentityWorld),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetWorldUniform),
                new Renderer3DGeometryPassOperation(
                    Renderer3DGeometryPassOperationKind.RenderSinglePass,
                    GeometryBucket: Renderer3DGeometryBucketKind.SolidGeometry,
                    ThingBucket: Renderer3DGeometryBucketKind.SolidThings,
                    LightBucket: Renderer3DGeometryBucketKind.LightThings),
            ],
            plan.Operations);
    }

    [Fact]
    public void BuildSkySolidPassPlanIncludesSkyPassBeforeSolidPass()
    {
        Renderer3DSkySolidPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildSkySolidPassPlan(skyGeometryCount: 2);

        Assert.Equal(
            [
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetIdentityWorld),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetWorldUniform),
                new Renderer3DGeometryPassOperation(
                    Renderer3DGeometryPassOperationKind.RenderSky,
                    GeometryBucket: Renderer3DGeometryBucketKind.SkyGeometry),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetIdentityWorld),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetWorldUniform),
                new Renderer3DGeometryPassOperation(
                    Renderer3DGeometryPassOperationKind.RenderSinglePass,
                    GeometryBucket: Renderer3DGeometryBucketKind.SolidGeometry,
                    ThingBucket: Renderer3DGeometryBucketKind.SolidThings,
                    LightBucket: Renderer3DGeometryBucketKind.LightThings),
            ],
            plan.Operations);
    }

    [Fact]
    public void BuildSkySolidPassPlanRejectsNegativeSkyCounts()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            Renderer3DGeometryLifecyclePlan.BuildSkySolidPassPlan(skyGeometryCount: -1));

    [Fact]
    public void BuildMaskedModelPassPlanSkipsWhenNoMaskedModelsExist()
    {
        Renderer3DModelPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildMaskedModelPassPlan(maskedModelThingCount: 0);

        Assert.False(plan.ShouldRender);
        Assert.Empty(plan.Operations);
    }

    [Fact]
    public void BuildMaskedModelPassPlanMatchesUdbCullAndAlphaTestSequence()
    {
        Renderer3DModelPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildMaskedModelPassPlan(maskedModelThingCount: 3);

        Assert.True(plan.ShouldRender);
        Assert.Equal(
            [
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetAlphaTest, Enabled: true),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetCullMode, CullMode: Cull.None),
                new Renderer3DGeometryPassOperation(
                    Renderer3DGeometryPassOperationKind.RenderModels,
                    LightBucket: Renderer3DGeometryBucketKind.LightThings,
                    ModelBucket: Renderer3DGeometryBucketKind.MaskedModelThings,
                    TranslucentModels: false),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetCullMode, CullMode: Cull.Clockwise),
            ],
            plan.Operations);
    }

    [Fact]
    public void BuildMaskedModelPassPlanRejectsNegativeCounts()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            Renderer3DGeometryLifecyclePlan.BuildMaskedModelPassPlan(maskedModelThingCount: -1));

    [Fact]
    public void BuildMaskPassPlanSkipsWhenNoMaskedGeometryOrThingsExist()
    {
        Renderer3DMaskPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildMaskPassPlan(
            maskedGeometryCount: 0,
            maskedThingCount: 0);

        Assert.False(plan.ShouldRender);
        Assert.Empty(plan.Operations);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(0, 3)]
    public void BuildMaskPassPlanMatchesUdbMaskSequenceWhenMaskedBucketsExist(int maskedGeometryCount, int maskedThingCount)
    {
        Renderer3DMaskPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildMaskPassPlan(
            maskedGeometryCount,
            maskedThingCount);

        Assert.True(plan.ShouldRender);
        Assert.Equal(
            [
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetIdentityWorld),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetWorldUniform),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetAlphaTest, Enabled: true),
                new Renderer3DGeometryPassOperation(
                    Renderer3DGeometryPassOperationKind.RenderSinglePass,
                    GeometryBucket: Renderer3DGeometryBucketKind.MaskedGeometry,
                    ThingBucket: Renderer3DGeometryBucketKind.MaskedThings,
                    LightBucket: Renderer3DGeometryBucketKind.LightThings),
            ],
            plan.Operations);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void BuildMaskPassPlanRejectsNegativeCounts(int maskedGeometryCount, int maskedThingCount)
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            Renderer3DGeometryLifecyclePlan.BuildMaskPassPlan(maskedGeometryCount, maskedThingCount));

    [Fact]
    public void BuildTranslucentPassPlanSkipsWhenNoTranslucentGeometryOrThingsExist()
    {
        Renderer3DTranslucentPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildTranslucentPassPlan(
            translucentGeometryCount: 0,
            translucentThingCount: 0);

        Assert.False(plan.ShouldRender);
        Assert.Empty(plan.Operations);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(0, 3)]
    public void BuildTranslucentPassPlanMatchesUdbAlphaAndAdditiveSequenceWhenTranslucentBucketsExist(
        int translucentGeometryCount,
        int translucentThingCount)
    {
        Renderer3DTranslucentPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildTranslucentPassPlan(
            translucentGeometryCount,
            translucentThingCount);

        Assert.True(plan.ShouldRender);
        Assert.Equal(
            [
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetIdentityWorld),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetWorldUniform),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetAlphaBlend, Enabled: true),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetAlphaTest, Enabled: false),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetZWrite, Enabled: false),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetSourceBlend, SourceBlend: Blend.SourceAlpha),
                new Renderer3DGeometryPassOperation(
                    Renderer3DGeometryPassOperationKind.RenderTranslucentPass,
                    GeometryBucket: Renderer3DGeometryBucketKind.TranslucentGeometry,
                    ThingBucket: Renderer3DGeometryBucketKind.TranslucentThings,
                    LightBucket: Renderer3DGeometryBucketKind.LightThings),
            ],
            plan.Operations);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void BuildTranslucentPassPlanRejectsNegativeCounts(int translucentGeometryCount, int translucentThingCount)
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            Renderer3DGeometryLifecyclePlan.BuildTranslucentPassPlan(translucentGeometryCount, translucentThingCount));

    [Fact]
    public void BuildTranslucentModelPassPlanSkipsWhenNoTranslucentModelsExist()
    {
        Renderer3DModelPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildTranslucentModelPassPlan(translucentModelThingCount: 0);

        Assert.False(plan.ShouldRender);
        Assert.Empty(plan.Operations);
    }

    [Fact]
    public void BuildTranslucentModelPassPlanMatchesUdbAlphaBlendSequence()
    {
        Renderer3DModelPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildTranslucentModelPassPlan(translucentModelThingCount: 3);

        Assert.True(plan.ShouldRender);
        Assert.Equal(
            [
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetAlphaBlend, Enabled: true),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetAlphaTest, Enabled: false),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetZWrite, Enabled: false),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetSourceBlend, SourceBlend: Blend.SourceAlpha),
                new Renderer3DGeometryPassOperation(
                    Renderer3DGeometryPassOperationKind.RenderModels,
                    LightBucket: Renderer3DGeometryBucketKind.LightThings,
                    ModelBucket: Renderer3DGeometryBucketKind.TranslucentModelThings,
                    TranslucentModels: true),
            ],
            plan.Operations);
    }

    [Fact]
    public void BuildTranslucentModelPassPlanRejectsNegativeCounts()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            Renderer3DGeometryLifecyclePlan.BuildTranslucentModelPassPlan(translucentModelThingCount: -1));

    [Fact]
    public void BuildThingCagePassPlanSkipsWhenThingCagesAreDisabled()
    {
        Renderer3DThingCagePassPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingCagePassPlan(renderThingCages: false);

        Assert.False(plan.ShouldRender);
        Assert.Empty(plan.Operations);
    }

    [Fact]
    public void BuildThingCagePassPlanMatchesUdbThingCageSequenceWhenEnabled()
    {
        Renderer3DThingCagePassPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingCagePassPlan(renderThingCages: true);

        Assert.True(plan.ShouldRender);
        Assert.Equal(
            [
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetIdentityWorld),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetWorldUniform),
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.RenderThingCages),
            ],
            plan.Operations);
    }

    [Fact]
    public void BuildVisualVerticesPassPlanAlwaysRendersVertices()
    {
        Renderer3DVisualVerticesPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildVisualVerticesPassPlan();

        Assert.Equal(
            [
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.RenderVertices),
            ],
            plan.Operations);
    }

    [Fact]
    public void BuildSlopeHandlesPassPlanSkipsWhenMapIsNotUdmf()
    {
        Renderer3DSlopeHandlesPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildSlopeHandlesPassPlan(isUdmfMap: false);

        Assert.False(plan.ShouldRender);
        Assert.Empty(plan.Operations);
    }

    [Fact]
    public void BuildSlopeHandlesPassPlanMatchesUdbSlopeHandleSequenceForUdmfMaps()
    {
        Renderer3DSlopeHandlesPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildSlopeHandlesPassPlan(isUdmfMap: true);

        Assert.True(plan.ShouldRender);
        Assert.Equal(
            [
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.RenderSlopeHandles),
            ],
            plan.Operations);
    }

    [Fact]
    public void BuildEventLinesPassPlanSkipsWhenGzEventLinesAreDisabled()
    {
        Renderer3DEventLinesPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildEventLinesPassPlan(showGzEventLines: false);

        Assert.False(plan.ShouldRender);
        Assert.Empty(plan.Operations);
    }

    [Fact]
    public void BuildEventLinesPassPlanMatchesUdbEventLineSequenceWhenEnabled()
    {
        Renderer3DEventLinesPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildEventLinesPassPlan(showGzEventLines: true);

        Assert.True(plan.ShouldRender);
        Assert.Equal(
            [
                new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.RenderArrows),
            ],
            plan.Operations);
    }

    [Fact]
    public void BuildFpsUpdatePlanSkipsWhenFpsDisplayIsDisabled()
    {
        Renderer3DFpsUpdatePlan plan = Renderer3DGeometryLifecyclePlan.BuildFpsUpdatePlan(
            showFps: false,
            currentFps: 12,
            elapsedMilliseconds: 1500);

        Assert.False(plan.ShouldUpdate);
        Assert.Empty(plan.Operations);
    }

    [Theory]
    [InlineData(999)]
    [InlineData(1000)]
    public void BuildFpsUpdatePlanOnlyIncrementsBeforeUdbElapsedThreshold(long elapsedMilliseconds)
    {
        Renderer3DFpsUpdatePlan plan = Renderer3DGeometryLifecyclePlan.BuildFpsUpdatePlan(
            showFps: true,
            currentFps: 4,
            elapsedMilliseconds);

        Assert.True(plan.ShouldUpdate);
        Assert.Equal(
            [
                new Renderer3DFpsUpdateOperation(Renderer3DFpsUpdateOperationKind.IncrementFrameCounter, FrameCount: 5),
            ],
            plan.Operations);
    }

    [Fact]
    public void BuildFpsUpdatePlanMatchesUdbLabelAndResetSequenceAfterElapsedThreshold()
    {
        Renderer3DFpsUpdatePlan plan = Renderer3DGeometryLifecyclePlan.BuildFpsUpdatePlan(
            showFps: true,
            currentFps: 4,
            elapsedMilliseconds: 1001);

        Assert.True(plan.ShouldUpdate);
        Assert.Equal(
            [
                new Renderer3DFpsUpdateOperation(Renderer3DFpsUpdateOperationKind.IncrementFrameCounter, FrameCount: 5),
                new Renderer3DFpsUpdateOperation(Renderer3DFpsUpdateOperationKind.SetFpsLabelText, LabelText: "5 FPS"),
                new Renderer3DFpsUpdateOperation(Renderer3DFpsUpdateOperationKind.ResetFrameCounter, FrameCount: 0),
                new Renderer3DFpsUpdateOperation(Renderer3DFpsUpdateOperationKind.RestartStopwatch),
            ],
            plan.Operations);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void BuildFpsUpdatePlanRejectsInvalidCounts(int currentFps, long elapsedMilliseconds)
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            Renderer3DGeometryLifecyclePlan.BuildFpsUpdatePlan(
                showFps: true,
                currentFps,
                elapsedMilliseconds));

    [Fact]
    public void BuildDynamicLightUpdatePlanSelectsVisibleClosestLightsThenSortsByRenderStyle()
    {
        Renderer3DDynamicLightUpdatePlan plan = Renderer3DGeometryLifecyclePlan.BuildDynamicLightUpdatePlan(
            [
                new Renderer3DDynamicLightCandidate(1, CameraDistance: 40.0, Visible: true, Renderer3DDynamicLightRenderStyle.Normal),
                new Renderer3DDynamicLightCandidate(2, CameraDistance: 10.0, Visible: true, Renderer3DDynamicLightRenderStyle.Subtractive),
                new Renderer3DDynamicLightCandidate(3, CameraDistance: 20.0, Visible: false, Renderer3DDynamicLightRenderStyle.Additive),
                new Renderer3DDynamicLightCandidate(4, CameraDistance: 30.0, Visible: true, Renderer3DDynamicLightRenderStyle.Additive),
                new Renderer3DDynamicLightCandidate(5, CameraDistance: 50.0, Visible: true, Renderer3DDynamicLightRenderStyle.Vavoom),
            ],
            maxDynamicLights: 3);

        Assert.Equal([4, 1, 2], plan.SelectedLightIds);
        Assert.Equal([1, 0, 1, 1], plan.LightOffsets);
    }

    [Fact]
    public void BuildDynamicLightUpdatePlanBucketsVavoomWithNormalAndLightmapWithAttenuated()
    {
        Renderer3DDynamicLightUpdatePlan plan = Renderer3DGeometryLifecyclePlan.BuildDynamicLightUpdatePlan(
            [
                new Renderer3DDynamicLightCandidate(1, CameraDistance: 10.0, Visible: true, Renderer3DDynamicLightRenderStyle.Lightmap),
                new Renderer3DDynamicLightCandidate(2, CameraDistance: 20.0, Visible: true, Renderer3DDynamicLightRenderStyle.Vavoom),
                new Renderer3DDynamicLightCandidate(3, CameraDistance: 30.0, Visible: true, Renderer3DDynamicLightRenderStyle.Attenuated),
                new Renderer3DDynamicLightCandidate(4, CameraDistance: 40.0, Visible: true, Renderer3DDynamicLightRenderStyle.Normal),
            ],
            maxDynamicLights: 4);

        Assert.Equal([2, 1, 3, 4], plan.SelectedLightIds);
        Assert.Equal([2, 2, 0, 0], plan.LightOffsets);
    }

    [Fact]
    public void BuildDynamicLightUpdatePlanAllowsZeroMaxLights()
    {
        Renderer3DDynamicLightUpdatePlan plan = Renderer3DGeometryLifecyclePlan.BuildDynamicLightUpdatePlan(
            [
                new Renderer3DDynamicLightCandidate(1, CameraDistance: 10.0, Visible: true, Renderer3DDynamicLightRenderStyle.Normal),
            ],
            maxDynamicLights: 0);

        Assert.Empty(plan.SelectedLightIds);
        Assert.Equal([0, 0, 0, 0], plan.LightOffsets);
    }

    [Fact]
    public void BuildDynamicLightUpdatePlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildDynamicLightUpdatePlan(null!, maxDynamicLights: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildDynamicLightUpdatePlan([], maxDynamicLights: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildDynamicLightUpdatePlan(
            [
                new Renderer3DDynamicLightCandidate(1, double.NaN, Visible: true, Renderer3DDynamicLightRenderStyle.Normal),
            ],
            maxDynamicLights: 1));
    }

    [Fact]
    public void BuildThingCageRenderPlanMatchesUdbRenderStateSetup()
    {
        Renderer3DThingCageRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingCageRenderPlan(
            [],
            showSelection: true,
            selectionColor: unchecked((int)0xffff4000));

        Assert.Equal(
            [
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetAlphaBlend, Enabled: true),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetAlphaTest, Enabled: false),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetZWrite, Enabled: false),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetSourceBlend, Blend: Blend.SourceAlpha),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetDestinationBlend, Blend: Blend.SourceAlpha),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetShader, Shader: ShaderName.world3d_constant_color),
            ],
            plan.StateOperations);
        Assert.Empty(plan.Draws);
    }

    [Fact]
    public void BuildThingCageRenderPlanMatchesUdbColorAndDrawRules()
    {
        Renderer3DThingCageRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingCageRenderPlan(
            [
                new Renderer3DThingCageCandidate(1, unchecked((int)0xff010203), Selected: true, Highlighted: false, CageLength: 12),
                new Renderer3DThingCageCandidate(2, unchecked((int)0xff102030), Selected: false, Highlighted: true, CageLength: 8),
                new Renderer3DThingCageCandidate(3, unchecked((int)0xffa0b0c0), Selected: false, Highlighted: false, CageLength: 4),
            ],
            showSelection: true,
            selectionColor: unchecked((int)0xffff4000));

        Assert.Equal(
            [
                new Renderer3DThingCageDrawPlan(1, unchecked((int)0xffff4000), 1.0f, PrimitiveType.LineList, StartIndex: 0, PrimitiveCount: 12),
                new Renderer3DThingCageDrawPlan(2, unchecked((int)0xff102030), 1.0f, PrimitiveType.LineList, StartIndex: 0, PrimitiveCount: 8),
                new Renderer3DThingCageDrawPlan(3, unchecked((int)0xffa0b0c0), 0.6f, PrimitiveType.LineList, StartIndex: 0, PrimitiveCount: 4),
            ],
            plan.Draws);
    }

    [Fact]
    public void BuildThingCageRenderPlanUsesCageColorWhenSelectionDisplayIsDisabled()
    {
        Renderer3DThingCageRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingCageRenderPlan(
            [
                new Renderer3DThingCageCandidate(1, unchecked((int)0xff010203), Selected: true, Highlighted: false, CageLength: 12),
            ],
            showSelection: false,
            selectionColor: unchecked((int)0xffff4000));

        Assert.Equal(
            [
                new Renderer3DThingCageDrawPlan(1, unchecked((int)0xff010203), 0.6f, PrimitiveType.LineList, StartIndex: 0, PrimitiveCount: 12),
            ],
            plan.Draws);
    }

    [Fact]
    public void BuildThingCageRenderPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildThingCageRenderPlan(null!, showSelection: true, selectionColor: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildThingCageRenderPlan(
            [
                new Renderer3DThingCageCandidate(1, unchecked((int)0xff010203), Selected: false, Highlighted: false, CageLength: -1),
            ],
            showSelection: true,
            selectionColor: 0));
    }

    [Fact]
    public void BuildVisualVertexRenderPlanSkipsWhenVisualVerticesAreUnavailable()
    {
        Renderer3DVisualVertexRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildVisualVertexRenderPlan(
            visualVertices: null,
            showSelection: true,
            selectionColor: unchecked((int)0xffff4000),
            infoLineColor: unchecked((int)0xff00ff00),
            verticesColor: unchecked((int)0xff101010));

        Assert.Empty(plan.StateOperations);
        Assert.Empty(plan.Draws);
    }

    [Fact]
    public void BuildVisualVertexRenderPlanMatchesUdbRenderStateSetup()
    {
        Renderer3DVisualVertexRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildVisualVertexRenderPlan(
            [],
            showSelection: true,
            selectionColor: unchecked((int)0xffff4000),
            infoLineColor: unchecked((int)0xff00ff00),
            verticesColor: unchecked((int)0xff101010));

        Assert.Equal(
            [
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetAlphaBlend, Enabled: true),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetAlphaTest, Enabled: false),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetZWrite, Enabled: false),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetSourceBlend, Blend: Blend.SourceAlpha),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetDestinationBlend, Blend: Blend.SourceAlpha),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetShader, Shader: ShaderName.world3d_constant_color),
            ],
            plan.StateOperations);
        Assert.Empty(plan.Draws);
    }

    [Fact]
    public void BuildVisualVertexRenderPlanMatchesUdbColorHandleAndDrawRules()
    {
        Renderer3DVisualVertexRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildVisualVertexRenderPlan(
            [
                new Renderer3DVisualVertexCandidate(1, Selected: true, Highlighted: false, HaveHeightOffset: false, CeilingVertex: false),
                new Renderer3DVisualVertexCandidate(2, Selected: false, Highlighted: true, HaveHeightOffset: true, CeilingVertex: true),
                new Renderer3DVisualVertexCandidate(3, Selected: false, Highlighted: false, HaveHeightOffset: false, CeilingVertex: false),
            ],
            showSelection: true,
            selectionColor: unchecked((int)0xffff4000),
            infoLineColor: unchecked((int)0xff00ff00),
            verticesColor: unchecked((int)0xff101010));

        Assert.Equal(
            [
                new Renderer3DVisualVertexDrawPlan(1, unchecked((int)0xffff4000), 1.0f, Renderer3DVisualVertexHandleKind.Lower, PrimitiveType.LineList, StartIndex: 0, PrimitiveCount: 8),
                new Renderer3DVisualVertexDrawPlan(2, unchecked((int)0xff00ff00), 1.0f, Renderer3DVisualVertexHandleKind.Upper, PrimitiveType.LineList, StartIndex: 0, PrimitiveCount: 8),
                new Renderer3DVisualVertexDrawPlan(3, unchecked((int)0xff101010), 0.6f, Renderer3DVisualVertexHandleKind.Lower, PrimitiveType.LineList, StartIndex: 0, PrimitiveCount: 8),
            ],
            plan.Draws);
    }

    [Fact]
    public void BuildVisualVertexRenderPlanUsesNonSelectionColorWhenSelectionDisplayIsDisabled()
    {
        Renderer3DVisualVertexRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildVisualVertexRenderPlan(
            [
                new Renderer3DVisualVertexCandidate(1, Selected: true, Highlighted: false, HaveHeightOffset: true, CeilingVertex: false),
            ],
            showSelection: false,
            selectionColor: unchecked((int)0xffff4000),
            infoLineColor: unchecked((int)0xff00ff00),
            verticesColor: unchecked((int)0xff101010));

        Assert.Equal(
            [
                new Renderer3DVisualVertexDrawPlan(1, unchecked((int)0xff00ff00), 0.6f, Renderer3DVisualVertexHandleKind.Lower, PrimitiveType.LineList, StartIndex: 0, PrimitiveCount: 8),
            ],
            plan.Draws);
    }

    [Fact]
    public void BuildSlopeHandleRenderPlanSkipsWhenSlopeHandlesAreUnavailable()
    {
        Renderer3DSlopeHandleRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildSlopeHandleRenderPlan(
            slopeHandles: null,
            showSelection: true,
            verticesColor: unchecked((int)0xff101010),
            guidelineColor: unchecked((int)0xff202020),
            selectionColor: unchecked((int)0xffff4000),
            highlightColor: unchecked((int)0xff00ffff));

        Assert.Empty(plan.StateOperations);
        Assert.Empty(plan.Draws);
    }

    [Fact]
    public void BuildSlopeHandleRenderPlanSkipsWhenSelectionDisplayIsHidden()
    {
        Renderer3DSlopeHandleRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildSlopeHandleRenderPlan(
            [
                new Renderer3DSlopeHandleCandidate(1, Renderer3DSlopeHandleKind.Line, Pivot: false, Selected: true, Highlighted: false, SmartPivot: false, Length: 64.0),
            ],
            showSelection: false,
            verticesColor: unchecked((int)0xff101010),
            guidelineColor: unchecked((int)0xff202020),
            selectionColor: unchecked((int)0xffff4000),
            highlightColor: unchecked((int)0xff00ffff));

        Assert.Empty(plan.StateOperations);
        Assert.Empty(plan.Draws);
    }

    [Fact]
    public void BuildSlopeHandleRenderPlanMatchesUdbRenderStateSetup()
    {
        Renderer3DSlopeHandleRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildSlopeHandleRenderPlan(
            [],
            showSelection: true,
            verticesColor: unchecked((int)0xff101010),
            guidelineColor: unchecked((int)0xff202020),
            selectionColor: unchecked((int)0xffff4000),
            highlightColor: unchecked((int)0xff00ffff));

        Assert.Equal(
            [
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetAlphaBlend, Enabled: true),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetAlphaTest, Enabled: false),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetZWrite, Enabled: false),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetSourceBlend, Blend: Blend.SourceAlpha),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetDestinationBlend, Blend: Blend.InverseSourceAlpha),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetShader, Shader: ShaderName.world3d_slope_handle),
            ],
            plan.StateOperations);
        Assert.Empty(plan.Draws);
    }

    [Fact]
    public void BuildSlopeHandleRenderPlanMatchesUdbColorGeometryAndDrawRules()
    {
        Renderer3DSlopeHandleRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildSlopeHandleRenderPlan(
            [
                new Renderer3DSlopeHandleCandidate(1, Renderer3DSlopeHandleKind.Line, Pivot: true, Selected: true, Highlighted: true, SmartPivot: false, Length: 64.0),
                new Renderer3DSlopeHandleCandidate(2, Renderer3DSlopeHandleKind.Line, Pivot: false, Selected: true, Highlighted: true, SmartPivot: false, Length: 32.5),
                new Renderer3DSlopeHandleCandidate(3, Renderer3DSlopeHandleKind.Vertex, Pivot: false, Selected: false, Highlighted: true, SmartPivot: false, Length: 12.0),
                new Renderer3DSlopeHandleCandidate(4, Renderer3DSlopeHandleKind.Vertex, Pivot: false, Selected: false, Highlighted: false, SmartPivot: true, Length: 99.0),
            ],
            showSelection: true,
            verticesColor: unchecked((int)0xff101010),
            guidelineColor: unchecked((int)0xff202020),
            selectionColor: unchecked((int)0xffff4000),
            highlightColor: unchecked((int)0xff00ffff));

        Assert.Equal(
            [
                new Renderer3DSlopeHandleDrawPlan(1, unchecked((int)0xff202020), Renderer3DSlopeHandleKind.Line, 64.0f, PrimitiveType.TriangleList, StartIndex: 0, PrimitiveCount: 2),
                new Renderer3DSlopeHandleDrawPlan(2, unchecked((int)0xffff4000), Renderer3DSlopeHandleKind.Line, 32.5f, PrimitiveType.TriangleList, StartIndex: 0, PrimitiveCount: 2),
                new Renderer3DSlopeHandleDrawPlan(3, unchecked((int)0xff00ffff), Renderer3DSlopeHandleKind.Vertex, 1.0f, PrimitiveType.TriangleList, StartIndex: 0, PrimitiveCount: 1),
                new Renderer3DSlopeHandleDrawPlan(4, unchecked((int)0xff101010), Renderer3DSlopeHandleKind.Vertex, 1.0f, PrimitiveType.TriangleList, StartIndex: 0, PrimitiveCount: 1),
            ],
            plan.Draws);
    }

    [Fact]
    public void Renderer3DStartGeometryExpressionsMatchUdbWhenCloneIsAvailable()
    {
        string? udbRoot = FindUdbRoot();
        if (udbRoot == null) return;

        string source = File.ReadAllText(Path.Combine(udbRoot, "Source", "Core", "Rendering", "Renderer3D.cs"));

        Assert.Contains("public void StartGeometry()", source, StringComparison.Ordinal);
        Assert.Contains("solidgeo = new Dictionary<ImageData, List<VisualGeometry>>();", source, StringComparison.Ordinal);
        Assert.Contains("maskedgeo = new Dictionary<ImageData, List<VisualGeometry>>();", source, StringComparison.Ordinal);
        Assert.Contains("translucentgeo = new List<VisualGeometry>();", source, StringComparison.Ordinal);
        Assert.Contains("skygeo = new List<VisualGeometry>();", source, StringComparison.Ordinal);
        Assert.Contains("solidthings = new Dictionary<ImageData, List<VisualThing>>();", source, StringComparison.Ordinal);
        Assert.Contains("maskedthings = new Dictionary<ImageData, List<VisualThing>>();", source, StringComparison.Ordinal);
        Assert.Contains("translucentthings = new List<VisualThing>();", source, StringComparison.Ordinal);
        Assert.Contains("maskedmodelthings = new Dictionary<ModelData, List<VisualThing>>();", source, StringComparison.Ordinal);
        Assert.Contains("translucentmodelthings = new List<VisualThing>();", source, StringComparison.Ordinal);
        Assert.Contains("lightthings = new List<VisualThing>();", source, StringComparison.Ordinal);
        Assert.Contains("allthings = new List<VisualThing>();", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetCullMode(Cull.Clockwise);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetZEnable(true);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetZWriteEnable(true);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetAlphaBlendEnable(false);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetAlphaTestEnable(false);", source, StringComparison.Ordinal);
        Assert.Contains("if (skygeo.Count > 0)", source, StringComparison.Ordinal);
        Assert.Contains("RenderSky(skygeo);", source, StringComparison.Ordinal);
        Assert.Contains("RenderSinglePass(solidgeo, solidthings, lightthings);", source, StringComparison.Ordinal);
        Assert.Contains("if(maskedmodelthings.Count > 0)", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetAlphaTestEnable(true);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetCullMode(Cull.None);", source, StringComparison.Ordinal);
        Assert.Contains("RenderModels(false, lightthings);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetCullMode(Cull.Clockwise);", source, StringComparison.Ordinal);
        Assert.Contains("if(maskedgeo.Count > 0 || maskedthings.Count > 0)", source, StringComparison.Ordinal);
        Assert.Contains("RenderSinglePass(maskedgeo, maskedthings, lightthings);", source, StringComparison.Ordinal);
        Assert.Contains("if(translucentgeo.Count > 0 || translucentthings.Count > 0)", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetAlphaBlendEnable(true);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetZWriteEnable(false);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetSourceBlend(Blend.SourceAlpha);", source, StringComparison.Ordinal);
        Assert.Contains("RenderTranslucentPass(translucentgeo, translucentthings, lightthings);", source, StringComparison.Ordinal);
        Assert.Contains("if(translucentmodelthings.Count > 0)", source, StringComparison.Ordinal);
        Assert.Contains("RenderModels(true, lightthings);", source, StringComparison.Ordinal);
        Assert.Contains("if (renderthingcages)", source, StringComparison.Ordinal);
        Assert.Contains("RenderThingCages();", source, StringComparison.Ordinal);
        Assert.Contains("//mxd. Visual vertices", source, StringComparison.Ordinal);
        Assert.Contains("RenderVertices();", source, StringComparison.Ordinal);
        Assert.Contains("if (General.Map.UDMF /* && General.Settings.ShowVisualSlopeHandles */)", source, StringComparison.Ordinal);
        Assert.Contains("RenderSlopeHandles();", source, StringComparison.Ordinal);
        Assert.Contains("if (General.Settings.GZShowEventLines) RenderArrows(eventlines);", source, StringComparison.Ordinal);
        Assert.Contains("if (General.Settings.ShowFPS)", source, StringComparison.Ordinal);
        Assert.Contains("fps++;", source, StringComparison.Ordinal);
        Assert.Contains("if (fpsWatch.ElapsedMilliseconds > 1000)", source, StringComparison.Ordinal);
        Assert.Contains("fpsLabel.Text = string.Format(\"{0} FPS\", fps);", source, StringComparison.Ordinal);
        Assert.Contains("fps = 0;", source, StringComparison.Ordinal);
        Assert.Contains("fpsWatch.Restart();", source, StringComparison.Ordinal);
        Assert.Contains("lightthings.Sort((t1, t2) => Math.Sign(t1.CameraDistance - t2.CameraDistance));", source, StringComparison.Ordinal);
        Assert.Contains("tl.Count < General.Settings.GZMaxDynamicLights", source, StringComparison.Ordinal);
        Assert.Contains("if (!CullLight(lightthings[i]))", source, StringComparison.Ordinal);
        Assert.Contains("lightthings.Sort((t1, t2) => Math.Sign(t1.LightType.LightRenderStyle - t2.LightType.LightRenderStyle));", source, StringComparison.Ordinal);
        Assert.Contains("lightOffsets = new int[4];", source, StringComparison.Ordinal);
        Assert.Contains("case GZGeneral.LightRenderStyle.VAVOOM: lightOffsets[0]++; break;", source, StringComparison.Ordinal);
        Assert.Contains("case GZGeneral.LightRenderStyle.ADDITIVE: lightOffsets[2]++; break;", source, StringComparison.Ordinal);
        Assert.Contains("case GZGeneral.LightRenderStyle.SUBTRACTIVE: lightOffsets[3]++; break;", source, StringComparison.Ordinal);
        Assert.Contains("default: lightOffsets[1]++; break;", source, StringComparison.Ordinal);
        Assert.Contains("private void RenderThingCages()", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetDestinationBlend(Blend.SourceAlpha);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetShader(ShaderName.world3d_constant_color);", source, StringComparison.Ordinal);
        Assert.Contains("thingcolor = General.Colors.Selection3D.ToColorValue();", source, StringComparison.Ordinal);
        Assert.Contains("thingcolor = t.CageColor;", source, StringComparison.Ordinal);
        Assert.Contains("if(t != highlighted) thingcolor.Alpha = 0.6f;", source, StringComparison.Ordinal);
        Assert.Contains("graphics.Draw(PrimitiveType.LineList, 0, t.CageLength);", source, StringComparison.Ordinal);
        Assert.Contains("private void RenderVertices()", source, StringComparison.Ordinal);
        Assert.Contains("if(visualvertices == null) return;", source, StringComparison.Ordinal);
        Assert.Contains("color = v.HaveHeightOffset ? General.Colors.InfoLine.ToColorValue() : General.Colors.Vertices.ToColorValue();", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.world, ref world);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetVertexBuffer(v.CeilingVertex ? vertexhandle.Upper : vertexhandle.Lower);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.Draw(PrimitiveType.LineList, 0, 8);", source, StringComparison.Ordinal);
        Assert.Contains("private void RenderSlopeHandles()", source, StringComparison.Ordinal);
        Assert.Contains("if (visualslopehandles == null || !showselection) return;", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetDestinationBlend(Blend.InverseSourceAlpha);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetShader(ShaderName.world3d_slope_handle);", source, StringComparison.Ordinal);
        Assert.Contains("if (handle.Pivot)", source, StringComparison.Ordinal);
        Assert.Contains("color = General.Colors.Guideline;", source, StringComparison.Ordinal);
        Assert.Contains("else if (handle.Selected)", source, StringComparison.Ordinal);
        Assert.Contains("else if (handle == highlighted)", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.slopeHandleLength, (float)handle.Length);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetVertexBuffer(visualslopehandle.LineGeometry);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.Draw(PrimitiveType.TriangleList, 0, 2);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.slopeHandleLength, 1.0f);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetVertexBuffer(visualslopehandle.VertexGeometry);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.Draw(PrimitiveType.TriangleList, 0, 1);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetTexture(null);", source, StringComparison.Ordinal);
        Assert.Contains("solidgeo = null;", source, StringComparison.Ordinal);
        Assert.Contains("maskedgeo = null;", source, StringComparison.Ordinal);
        Assert.Contains("translucentgeo = null;", source, StringComparison.Ordinal);
        Assert.Contains("skygeo = null;", source, StringComparison.Ordinal);
        Assert.Contains("solidthings = null;", source, StringComparison.Ordinal);
        Assert.Contains("maskedthings = null;", source, StringComparison.Ordinal);
        Assert.Contains("translucentthings = null;", source, StringComparison.Ordinal);
        Assert.Contains("allthings = null;", source, StringComparison.Ordinal);
        Assert.Contains("lightthings = null;", source, StringComparison.Ordinal);
        Assert.Contains("maskedmodelthings = null;", source, StringComparison.Ordinal);
        Assert.Contains("translucentmodelthings = null;", source, StringComparison.Ordinal);
        Assert.Contains("visualvertices = null;", source, StringComparison.Ordinal);
    }
}
