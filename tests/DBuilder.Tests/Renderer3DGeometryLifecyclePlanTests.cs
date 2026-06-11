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
