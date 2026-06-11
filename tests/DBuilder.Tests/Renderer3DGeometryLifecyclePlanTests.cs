// ABOUTME: Verifies UDB-style Renderer3D.StartGeometry collection lifecycle planning.
// ABOUTME: Pins solid, masked, translucent, sky, model, light, and all-things buckets.

using DBuilder.Geometry;
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

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void BuildSectorGeometryCollectionPlanSkipsGeometryWithoutTextureOrTriangles(bool hasTexture, int triangles)
    {
        Renderer3DSectorGeometryCollectionPlan plan = Renderer3DGeometryLifecyclePlan.BuildSectorGeometryCollectionPlan(
            new Renderer3DSectorGeometryCandidate(1, hasTexture, triangles, RenderAsSky: false, RenderPass.Solid),
            drawSky: true);

        Assert.False(plan.Accepted);
        Assert.Null(plan.Bucket);
        Assert.Null(plan.UnsupportedRenderPassMessage);
    }

    [Fact]
    public void BuildSectorGeometryCollectionPlanRoutesSkyOnlyWhenSkyRenderingIsEnabled()
    {
        Renderer3DSectorGeometryCollectionPlan skyPlan = Renderer3DGeometryLifecyclePlan.BuildSectorGeometryCollectionPlan(
            new Renderer3DSectorGeometryCandidate(1, HasTexture: true, Triangles: 3, RenderAsSky: true, RenderPass.Solid),
            drawSky: true);
        Renderer3DSectorGeometryCollectionPlan solidPlan = Renderer3DGeometryLifecyclePlan.BuildSectorGeometryCollectionPlan(
            new Renderer3DSectorGeometryCandidate(2, HasTexture: true, Triangles: 3, RenderAsSky: true, RenderPass.Solid),
            drawSky: false);

        Assert.True(skyPlan.Accepted);
        Assert.Equal(Renderer3DGeometryBucketKind.SkyGeometry, skyPlan.Bucket);
        Assert.True(solidPlan.Accepted);
        Assert.Equal(Renderer3DGeometryBucketKind.SolidGeometry, solidPlan.Bucket);
    }

    [Theory]
    [InlineData(RenderPass.Solid, Renderer3DGeometryBucketKind.SolidGeometry)]
    [InlineData(RenderPass.Mask, Renderer3DGeometryBucketKind.MaskedGeometry)]
    [InlineData(RenderPass.Alpha, Renderer3DGeometryBucketKind.TranslucentGeometry)]
    [InlineData(RenderPass.Additive, Renderer3DGeometryBucketKind.TranslucentGeometry)]
    public void BuildSectorGeometryCollectionPlanRoutesRenderPassBuckets(RenderPass renderPass, Renderer3DGeometryBucketKind bucket)
    {
        Renderer3DSectorGeometryCollectionPlan plan = Renderer3DGeometryLifecyclePlan.BuildSectorGeometryCollectionPlan(
            new Renderer3DSectorGeometryCandidate(1, HasTexture: true, Triangles: 3, RenderAsSky: false, renderPass),
            drawSky: true);

        Assert.True(plan.Accepted);
        Assert.Equal(bucket, plan.Bucket);
        Assert.Null(plan.UnsupportedRenderPassMessage);
    }

    [Fact]
    public void BuildSectorGeometryCollectionPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Renderer3DGeometryLifecyclePlan.BuildSectorGeometryCollectionPlan(
                new Renderer3DSectorGeometryCandidate(1, HasTexture: true, Triangles: -1, RenderAsSky: false, RenderPass.Solid),
                drawSky: true));

        Renderer3DSectorGeometryCollectionPlan unsupported = Renderer3DGeometryLifecyclePlan.BuildSectorGeometryCollectionPlan(
            new Renderer3DSectorGeometryCandidate(2, HasTexture: true, Triangles: 1, RenderAsSky: false, (RenderPass)99),
            drawSky: true);

        Assert.False(unsupported.Accepted);
        Assert.Null(unsupported.Bucket);
        Assert.Equal("Geometry rendering of 99 render pass is not implemented!", unsupported.UnsupportedRenderPassMessage);
    }

    [Fact]
    public void BuildThingGeometryCollectionPlanCollectsAnimatedLightsBeforeThingBuckets()
    {
        Renderer3DThingGeometryCollectionPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingGeometryCollectionPlan(
            new Renderer3DThingGeometryCandidate(
                1,
                ThingRenderMode.NORMAL,
                ModelRenderMode.NONE,
                RenderPass.Mask,
                HasTexture: true,
                Selected: false,
                FullBrightness: false,
                LightRenderMode.ALL,
                HasLightType: true,
                LightAnimated: true,
                LightRadius: 64.0,
                VertexColorOpaque: true));

        Assert.True(plan.UpdateThing);
        Assert.True(plan.UpdateLightRadius);
        Assert.True(plan.UpdateBoundingBox);
        Assert.True(plan.UpdateSpriteFrame);
        Assert.Equal(
            [
                Renderer3DGeometryBucketKind.LightThings,
                Renderer3DGeometryBucketKind.MaskedThings,
                Renderer3DGeometryBucketKind.AllThings,
            ],
            plan.Buckets);
        Assert.Null(plan.UnsupportedRenderPassMessage);
    }

    [Theory]
    [InlineData(ThingRenderMode.MODEL, ModelRenderMode.ALL, false, RenderPass.Solid, true, Renderer3DGeometryBucketKind.MaskedModelThings)]
    [InlineData(ThingRenderMode.VOXEL, ModelRenderMode.ACTIVE_THINGS_FILTER, false, RenderPass.Mask, true, Renderer3DGeometryBucketKind.MaskedModelThings)]
    [InlineData(ThingRenderMode.MODEL, ModelRenderMode.SELECTION, true, RenderPass.Alpha, true, Renderer3DGeometryBucketKind.MaskedModelThings)]
    [InlineData(ThingRenderMode.MODEL, ModelRenderMode.ALL, false, RenderPass.Alpha, false, Renderer3DGeometryBucketKind.TranslucentModelThings)]
    [InlineData(ThingRenderMode.VOXEL, ModelRenderMode.ALL, false, RenderPass.Additive, true, Renderer3DGeometryBucketKind.TranslucentModelThings)]
    public void BuildThingGeometryCollectionPlanRoutesModelBuckets(
        ThingRenderMode renderMode,
        ModelRenderMode modelRenderMode,
        bool selected,
        RenderPass renderPass,
        bool vertexColorOpaque,
        Renderer3DGeometryBucketKind bucket)
    {
        Renderer3DThingGeometryCollectionPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingGeometryCollectionPlan(
            new Renderer3DThingGeometryCandidate(
                2,
                renderMode,
                modelRenderMode,
                renderPass,
                HasTexture: false,
                Selected: selected,
                FullBrightness: false,
                LightRenderMode.NONE,
                HasLightType: false,
                LightAnimated: false,
                LightRadius: 0.0,
                VertexColorOpaque: vertexColorOpaque));

        Assert.False(plan.UpdateSpriteFrame);
        Assert.Equal([bucket, Renderer3DGeometryBucketKind.AllThings], plan.Buckets);
        Assert.Null(plan.UnsupportedRenderPassMessage);
    }

    [Theory]
    [InlineData(RenderPass.Solid, Renderer3DGeometryBucketKind.SolidThings)]
    [InlineData(RenderPass.Mask, Renderer3DGeometryBucketKind.MaskedThings)]
    [InlineData(RenderPass.Alpha, Renderer3DGeometryBucketKind.TranslucentThings)]
    [InlineData(RenderPass.Additive, Renderer3DGeometryBucketKind.TranslucentThings)]
    public void BuildThingGeometryCollectionPlanRoutesSpriteBuckets(RenderPass renderPass, Renderer3DGeometryBucketKind bucket)
    {
        Renderer3DThingGeometryCollectionPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingGeometryCollectionPlan(
            new Renderer3DThingGeometryCandidate(
                3,
                ThingRenderMode.NORMAL,
                ModelRenderMode.ALL,
                renderPass,
                HasTexture: true,
                Selected: false,
                FullBrightness: false,
                LightRenderMode.NONE,
                HasLightType: false,
                LightAnimated: false,
                LightRadius: 0.0,
                VertexColorOpaque: false));

        Assert.True(plan.UpdateSpriteFrame);
        Assert.Equal([bucket, Renderer3DGeometryBucketKind.AllThings], plan.Buckets);
        Assert.Null(plan.UnsupportedRenderPassMessage);
    }

    [Fact]
    public void BuildThingGeometryCollectionPlanFallsBackToSpriteRulesWhenModelDisplayIsDisabled()
    {
        Renderer3DThingGeometryCollectionPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingGeometryCollectionPlan(
            new Renderer3DThingGeometryCandidate(
                4,
                ThingRenderMode.MODEL,
                ModelRenderMode.NONE,
                RenderPass.Solid,
                HasTexture: true,
                Selected: true,
                FullBrightness: false,
                LightRenderMode.NONE,
                HasLightType: false,
                LightAnimated: false,
                LightRadius: 0.0,
                VertexColorOpaque: true));

        Assert.True(plan.UpdateSpriteFrame);
        Assert.Equal([Renderer3DGeometryBucketKind.SolidThings, Renderer3DGeometryBucketKind.AllThings], plan.Buckets);
    }

    [Fact]
    public void BuildThingGeometryCollectionPlanAddsTexturelessThingsOnlyToAllThings()
    {
        Renderer3DThingGeometryCollectionPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingGeometryCollectionPlan(
            new Renderer3DThingGeometryCandidate(
                5,
                ThingRenderMode.NORMAL,
                ModelRenderMode.ALL,
                RenderPass.Solid,
                HasTexture: false,
                Selected: false,
                FullBrightness: false,
                LightRenderMode.ALL,
                HasLightType: true,
                LightAnimated: false,
                LightRadius: 0.0,
                VertexColorOpaque: true));

        Assert.True(plan.UpdateLightRadius);
        Assert.False(plan.UpdateBoundingBox);
        Assert.Equal([Renderer3DGeometryBucketKind.AllThings], plan.Buckets);
    }

    [Fact]
    public void BuildThingGeometryCollectionPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Renderer3DGeometryLifecyclePlan.BuildThingGeometryCollectionPlan(
                new Renderer3DThingGeometryCandidate(1, ThingRenderMode.NORMAL, ModelRenderMode.NONE, RenderPass.Solid, HasTexture: true, Selected: false, FullBrightness: false, LightRenderMode.NONE, HasLightType: false, LightAnimated: false, LightRadius: -1.0, VertexColorOpaque: true)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Renderer3DGeometryLifecyclePlan.BuildThingGeometryCollectionPlan(
                new Renderer3DThingGeometryCandidate(1, ThingRenderMode.NORMAL, ModelRenderMode.NONE, RenderPass.Solid, HasTexture: true, Selected: false, FullBrightness: false, LightRenderMode.NONE, HasLightType: false, LightAnimated: false, LightRadius: double.NaN, VertexColorOpaque: true)));

        Renderer3DThingGeometryCollectionPlan unsupportedModel = Renderer3DGeometryLifecyclePlan.BuildThingGeometryCollectionPlan(
            new Renderer3DThingGeometryCandidate(6, ThingRenderMode.MODEL, ModelRenderMode.ALL, (RenderPass)99, HasTexture: true, Selected: false, FullBrightness: false, LightRenderMode.NONE, HasLightType: false, LightAnimated: false, LightRadius: 0.0, VertexColorOpaque: true));
        Renderer3DThingGeometryCollectionPlan unsupportedSprite = Renderer3DGeometryLifecyclePlan.BuildThingGeometryCollectionPlan(
            new Renderer3DThingGeometryCandidate(7, ThingRenderMode.NORMAL, ModelRenderMode.ALL, (RenderPass)99, HasTexture: true, Selected: false, FullBrightness: false, LightRenderMode.NONE, HasLightType: false, LightAnimated: false, LightRadius: 0.0, VertexColorOpaque: true));

        Assert.Equal([Renderer3DGeometryBucketKind.AllThings], unsupportedModel.Buckets);
        Assert.Equal("Thing model rendering of 99 render pass is not implemented!", unsupportedModel.UnsupportedRenderPassMessage);
        Assert.Equal([Renderer3DGeometryBucketKind.AllThings], unsupportedSprite.Buckets);
        Assert.Equal("Thing rendering of 99 render pass is not implemented!", unsupportedSprite.UnsupportedRenderPassMessage);
    }

    [Fact]
    public void BuildSkyRenderPlanSetsUdbSkyStateAndDrawsValidSectors()
    {
        Renderer3DSkyRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildSkyRenderPlan(
            [
                new Renderer3DSkyGeometryCandidate(1, SectorId: 10, SectorNeedsUpdate: true, SectorHasGeometryBuffer: true, SectorHasMap: true, Highlighted: false, Selected: false, VertexOffset: 2, Triangles: 4),
                new Renderer3DSkyGeometryCandidate(2, SectorId: 10, SectorNeedsUpdate: true, SectorHasGeometryBuffer: true, SectorHasMap: true, Highlighted: true, Selected: false, VertexOffset: 6, Triangles: 8),
                new Renderer3DSkyGeometryCandidate(3, SectorId: 20, SectorNeedsUpdate: false, SectorHasGeometryBuffer: true, SectorHasMap: true, Highlighted: false, Selected: true, VertexOffset: 14, Triangles: 16),
            ]);

        Assert.Equal(ShaderName.world3d_skybox, plan.Shader);
        Assert.True(plan.SetSkyTexture);
        Assert.True(plan.SetCameraUniform);
        Assert.Equal(
            [
                new Renderer3DSkyGeometryDrawPlan(1, SectorId: 10, UpdateSectorGeometry: true, BindSectorGeometryBuffer: true, ClearCurrentSector: false, SetHighlightColor: true, Draw: true, PrimitiveType.TriangleList, VertexOffset: 2, Triangles: 4),
                new Renderer3DSkyGeometryDrawPlan(2, SectorId: 10, UpdateSectorGeometry: false, BindSectorGeometryBuffer: false, ClearCurrentSector: false, SetHighlightColor: true, Draw: true, PrimitiveType.TriangleList, VertexOffset: 6, Triangles: 8),
                new Renderer3DSkyGeometryDrawPlan(3, SectorId: 20, UpdateSectorGeometry: false, BindSectorGeometryBuffer: true, ClearCurrentSector: false, SetHighlightColor: true, Draw: true, PrimitiveType.TriangleList, VertexOffset: 14, Triangles: 16),
            ],
            plan.Draws);
    }

    [Fact]
    public void BuildSkyRenderPlanClearsCurrentSectorForUnavailableSectorAndResumesWhenValid()
    {
        Renderer3DSkyRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildSkyRenderPlan(
            [
                new Renderer3DSkyGeometryCandidate(1, SectorId: 10, SectorNeedsUpdate: false, SectorHasGeometryBuffer: true, SectorHasMap: true, Highlighted: false, Selected: false, VertexOffset: 2, Triangles: 4),
                new Renderer3DSkyGeometryCandidate(2, SectorId: 20, SectorNeedsUpdate: true, SectorHasGeometryBuffer: false, SectorHasMap: true, Highlighted: false, Selected: false, VertexOffset: 6, Triangles: 8),
                new Renderer3DSkyGeometryCandidate(3, SectorId: 20, SectorNeedsUpdate: true, SectorHasGeometryBuffer: true, SectorHasMap: true, Highlighted: false, Selected: false, VertexOffset: 14, Triangles: 16),
                new Renderer3DSkyGeometryCandidate(4, SectorId: 30, SectorNeedsUpdate: false, SectorHasGeometryBuffer: true, SectorHasMap: false, Highlighted: false, Selected: false, VertexOffset: 30, Triangles: 32),
            ]);

        Assert.Equal(
            [
                new Renderer3DSkyGeometryDrawPlan(1, SectorId: 10, UpdateSectorGeometry: false, BindSectorGeometryBuffer: true, ClearCurrentSector: false, SetHighlightColor: true, Draw: true, PrimitiveType.TriangleList, VertexOffset: 2, Triangles: 4),
                new Renderer3DSkyGeometryDrawPlan(2, SectorId: 20, UpdateSectorGeometry: true, BindSectorGeometryBuffer: false, ClearCurrentSector: true, SetHighlightColor: false, Draw: false, PrimitiveType.TriangleList, VertexOffset: 6, Triangles: 0),
                new Renderer3DSkyGeometryDrawPlan(3, SectorId: 20, UpdateSectorGeometry: true, BindSectorGeometryBuffer: true, ClearCurrentSector: false, SetHighlightColor: true, Draw: true, PrimitiveType.TriangleList, VertexOffset: 14, Triangles: 16),
                new Renderer3DSkyGeometryDrawPlan(4, SectorId: 30, UpdateSectorGeometry: false, BindSectorGeometryBuffer: false, ClearCurrentSector: true, SetHighlightColor: false, Draw: false, PrimitiveType.TriangleList, VertexOffset: 30, Triangles: 0),
            ],
            plan.Draws);
    }

    [Fact]
    public void BuildSkyRenderPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildSkyRenderPlan(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildSkyRenderPlan(
            [
                new Renderer3DSkyGeometryCandidate(1, SectorId: 10, SectorNeedsUpdate: false, SectorHasGeometryBuffer: true, SectorHasMap: true, Highlighted: false, Selected: false, VertexOffset: -1, Triangles: 4),
            ]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildSkyRenderPlan(
            [
                new Renderer3DSkyGeometryCandidate(1, SectorId: 10, SectorNeedsUpdate: false, SectorHasGeometryBuffer: true, SectorHasMap: true, Highlighted: false, Selected: false, VertexOffset: 2, Triangles: -1),
            ]));
    }

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
    public void BuildEventLineRenderPlanSkipsWhenLinesAreEmpty()
    {
        Renderer3DEventLineRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildEventLineRenderPlan([]);

        Assert.Empty(plan.Vertices);
        Assert.Empty(plan.StateOperations);
        Assert.False(plan.SetIdentityWorld);
        Assert.Equal(PrimitiveType.LineList, plan.PrimitiveType);
        Assert.Equal(0, plan.StartIndex);
        Assert.Equal(0, plan.PrimitiveCount);
        Assert.False(plan.DisposeVertexBuffer);
    }

    [Fact]
    public void BuildEventLineRenderPlanMatchesUdbRenderStateSetup()
    {
        Renderer3DEventLineRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildEventLineRenderPlan(
            [
                new Line3D(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0), 0xff102030u, renderArrowhead: false),
            ]);

        Assert.Equal(
            [
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetAlphaBlend, Enabled: true),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetAlphaTest, Enabled: false),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetZWrite, Enabled: false),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetSourceBlend, Blend: Blend.SourceAlpha),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetDestinationBlend, Blend: Blend.SourceAlpha),
                new Renderer3DThingCageRenderOperation(Renderer3DThingCageRenderOperationKind.SetShader, Shader: ShaderName.world3d_vertex_color),
            ],
            plan.StateOperations);
        Assert.True(plan.SetIdentityWorld);
        Assert.Equal(PrimitiveType.LineList, plan.PrimitiveType);
        Assert.Equal(0, plan.StartIndex);
        Assert.Equal(1, plan.PrimitiveCount);
        Assert.True(plan.DisposeVertexBuffer);
    }

    [Fact]
    public void BuildEventLineRenderPlanMatchesUdbArrowheadVertices()
    {
        var line = new Line3D(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0), 0xff102030u);
        Renderer3DEventLineRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildEventLineRenderPlan([line]);

        double angle = line.GetAngle();
        Vector3D firstArrowhead = new(
            line.End.x - Renderer3DGeometryLifecyclePlan.EventLineArrowheadLength * Math.Sin(angle - Renderer3DGeometryLifecyclePlan.EventLineArrowheadHalfAngle),
            line.End.y + Renderer3DGeometryLifecyclePlan.EventLineArrowheadLength * Math.Cos(angle - Renderer3DGeometryLifecyclePlan.EventLineArrowheadHalfAngle),
            line.End.z);
        Vector3D secondArrowhead = new(
            line.End.x - Renderer3DGeometryLifecyclePlan.EventLineArrowheadLength * Math.Sin(angle + Renderer3DGeometryLifecyclePlan.EventLineArrowheadHalfAngle),
            line.End.y + Renderer3DGeometryLifecyclePlan.EventLineArrowheadLength * Math.Cos(angle + Renderer3DGeometryLifecyclePlan.EventLineArrowheadHalfAngle),
            line.End.z);

        Assert.Equal(6, plan.Vertices.Length);
        AssertWorldVertex(plan.Vertices[0], 0.0f, 0.0f, 0.0f, unchecked((int)0xff102030));
        AssertWorldVertex(plan.Vertices[1], 10.0f, 0.0f, 0.0f, unchecked((int)0xff102030));
        AssertWorldVertex(plan.Vertices[2], 10.0f, 0.0f, 0.0f, unchecked((int)0xff102030));
        AssertWorldVertex(plan.Vertices[3], (float)firstArrowhead.x, (float)firstArrowhead.y, (float)firstArrowhead.z, unchecked((int)0xff102030));
        AssertWorldVertex(plan.Vertices[4], 10.0f, 0.0f, 0.0f, unchecked((int)0xff102030));
        AssertWorldVertex(plan.Vertices[5], (float)secondArrowhead.x, (float)secondArrowhead.y, (float)secondArrowhead.z, unchecked((int)0xff102030));
        Assert.Equal(3, plan.PrimitiveCount);
    }

    [Fact]
    public void BuildEventLineRenderPlanKeepsPlainLineWhenArrowheadIsDisabled()
    {
        Renderer3DEventLineRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildEventLineRenderPlan(
            [
                new Line3D(new Vector3D(1, 2, 3), new Vector3D(4, 5, 6), 0xff405060u, renderArrowhead: false),
            ]);

        Assert.Equal(2, plan.Vertices.Length);
        AssertWorldVertex(plan.Vertices[0], 1.0f, 2.0f, 3.0f, unchecked((int)0xff405060));
        AssertWorldVertex(plan.Vertices[1], 4.0f, 5.0f, 6.0f, unchecked((int)0xff405060));
        Assert.Equal(1, plan.PrimitiveCount);
    }

    [Fact]
    public void BuildEventLineRenderPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildEventLineRenderPlan(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildEventLineRenderPlan(
            [
                new Line3D(new Vector3D(double.NaN, 0, 0), new Vector3D(1, 0, 0)),
            ]));
    }

    [Fact]
    public void BuildGeometryShaderPassPlanKeepsBaseShaderWithoutHighlightOrFog()
    {
        Renderer3DShaderPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildGeometryShaderPassPlan(
            ShaderName.world3d_main,
            highlighted: false,
            showHighlight: true,
            selected: false,
            showSelection: true,
            drawFog: false,
            fullBrightness: false,
            classicRendering: false,
            sectorHasFog: false);

        Assert.Equal(ShaderName.world3d_main, plan.BaseShader);
        Assert.Equal(ShaderName.world3d_main_highlight, plan.HighlightShader);
        Assert.Equal(ShaderName.world3d_main, plan.WantedShader);
        Assert.False(plan.UsesHighlightShader);
        Assert.False(plan.UsesFogShader);
        Assert.False(plan.AppliesFogUniforms);
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(false, true, true, true)]
    public void BuildGeometryShaderPassPlanUsesHighlightShaderForVisibleHighlightedOrSelectedGeometry(
        bool highlighted,
        bool showHighlight,
        bool selected,
        bool showSelection)
    {
        Renderer3DShaderPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildGeometryShaderPassPlan(
            ShaderName.world3d_fullbright,
            highlighted,
            showHighlight,
            selected,
            showSelection,
            drawFog: false,
            fullBrightness: true,
            classicRendering: false,
            sectorHasFog: false);

        Assert.Equal(ShaderName.world3d_fullbright_highlight, plan.WantedShader);
        Assert.True(plan.UsesHighlightShader);
    }

    [Fact]
    public void BuildGeometryShaderPassPlanSuppressesHiddenHighlightAndSelection()
    {
        Renderer3DShaderPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildGeometryShaderPassPlan(
            ShaderName.world3d_main,
            highlighted: true,
            showHighlight: false,
            selected: true,
            showSelection: false,
            drawFog: false,
            fullBrightness: false,
            classicRendering: false,
            sectorHasFog: false);

        Assert.Equal(ShaderName.world3d_main, plan.WantedShader);
        Assert.False(plan.UsesHighlightShader);
    }

    [Fact]
    public void BuildGeometryShaderPassPlanAddsUdbFogOffsetWhenFogRenderingIsActive()
    {
        Renderer3DShaderPassPlan basePlan = Renderer3DGeometryLifecyclePlan.BuildGeometryShaderPassPlan(
            ShaderName.world3d_main,
            highlighted: false,
            showHighlight: true,
            selected: false,
            showSelection: true,
            drawFog: true,
            fullBrightness: false,
            classicRendering: false,
            sectorHasFog: true);
        Renderer3DShaderPassPlan highlightedPlan = Renderer3DGeometryLifecyclePlan.BuildGeometryShaderPassPlan(
            ShaderName.world3d_main,
            highlighted: true,
            showHighlight: true,
            selected: false,
            showSelection: true,
            drawFog: true,
            fullBrightness: false,
            classicRendering: false,
            sectorHasFog: true);

        Assert.Equal(ShaderName.world3d_main_fog, basePlan.WantedShader);
        Assert.True(basePlan.UsesFogShader);
        Assert.True(basePlan.AppliesFogUniforms);
        Assert.Equal(ShaderName.world3d_main_highlight_fog, highlightedPlan.WantedShader);
        Assert.True(highlightedPlan.UsesHighlightShader);
        Assert.True(highlightedPlan.UsesFogShader);
        Assert.True(highlightedPlan.AppliesFogUniforms);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void BuildGeometryShaderPassPlanSuppressesFogForFullBrightClassicOrFoglessSectors(
        bool fullBrightness,
        bool classicRendering,
        bool sectorHasFog)
    {
        Renderer3DShaderPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildGeometryShaderPassPlan(
            ShaderName.world3d_main,
            highlighted: false,
            showHighlight: true,
            selected: false,
            showSelection: true,
            drawFog: true,
            fullBrightness,
            classicRendering,
            sectorHasFog);

        Assert.Equal(ShaderName.world3d_main, plan.WantedShader);
        Assert.False(plan.UsesFogShader);
        Assert.False(plan.AppliesFogUniforms);
    }

    [Fact]
    public void BuildThingShaderPassPlanKeepsBaseShaderWithoutHighlightFogOrVertexColor()
    {
        Renderer3DThingShaderPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingShaderPassPlan(
            ShaderName.world3d_main,
            highlighted: false,
            showHighlight: true,
            selected: false,
            showSelection: true,
            drawFog: false,
            fullBrightness: false,
            classicRendering: false,
            thingSectorHasFog: false,
            lightInternal: false,
            lightIsSun: false,
            drawLights: false,
            hasDynamicLights: false,
            litColorNonZero: false);

        Assert.Equal(ShaderName.world3d_main, plan.BaseShader);
        Assert.Equal(ShaderName.world3d_main_highlight, plan.HighlightShader);
        Assert.Equal(ShaderName.world3d_main, plan.WantedShader);
        Assert.False(plan.UsesHighlightShader);
        Assert.False(plan.UsesFogShader);
        Assert.Equal(Renderer3DThingVertexColorSource.None, plan.VertexColorSource);
        Assert.False(plan.AppliesFogUniforms);
    }

    [Fact]
    public void BuildThingShaderPassPlanAppliesHighlightFogAndInternalLightOffsetsInUdbOrder()
    {
        Renderer3DThingShaderPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingShaderPassPlan(
            ShaderName.world3d_main,
            highlighted: true,
            showHighlight: true,
            selected: false,
            showSelection: true,
            drawFog: true,
            fullBrightness: false,
            classicRendering: false,
            thingSectorHasFog: true,
            lightInternal: true,
            lightIsSun: false,
            drawLights: true,
            hasDynamicLights: true,
            litColorNonZero: true);

        Assert.Equal(ShaderName.world3d_main_highlight_fog_vertexcolor, plan.WantedShader);
        Assert.True(plan.UsesHighlightShader);
        Assert.True(plan.UsesFogShader);
        Assert.Equal(Renderer3DThingVertexColorSource.InternalLight, plan.VertexColorSource);
        Assert.True(plan.AppliesFogUniforms);
    }

    [Fact]
    public void BuildThingShaderPassPlanUsesDynamicLightVertexColorWhenNoInternalLightApplies()
    {
        Renderer3DThingShaderPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingShaderPassPlan(
            ShaderName.world3d_main,
            highlighted: false,
            showHighlight: true,
            selected: true,
            showSelection: true,
            drawFog: false,
            fullBrightness: false,
            classicRendering: false,
            thingSectorHasFog: false,
            lightInternal: false,
            lightIsSun: false,
            drawLights: true,
            hasDynamicLights: true,
            litColorNonZero: true);

        Assert.Equal(ShaderName.world3d_main_highlight_vertexcolor, plan.WantedShader);
        Assert.True(plan.UsesHighlightShader);
        Assert.False(plan.UsesFogShader);
        Assert.Equal(Renderer3DThingVertexColorSource.DynamicLight, plan.VertexColorSource);
        Assert.False(plan.AppliesFogUniforms);
    }

    [Theory]
    [InlineData(true, false, false, true, true, true)]
    [InlineData(false, true, false, true, true, true)]
    [InlineData(false, false, true, false, true, true)]
    [InlineData(false, false, false, false, true, true)]
    [InlineData(false, false, false, true, false, true)]
    [InlineData(false, false, false, true, true, false)]
    public void BuildThingShaderPassPlanSuppressesVertexColorWhenUdbConditionsDoNotApply(
        bool fullBrightness,
        bool classicRendering,
        bool lightIsSun,
        bool drawLights,
        bool hasDynamicLights,
        bool litColorNonZero)
    {
        Renderer3DThingShaderPassPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingShaderPassPlan(
            ShaderName.world3d_main,
            highlighted: false,
            showHighlight: true,
            selected: false,
            showSelection: true,
            drawFog: false,
            fullBrightness,
            classicRendering,
            thingSectorHasFog: false,
            lightInternal: lightIsSun,
            lightIsSun,
            drawLights,
            hasDynamicLights,
            litColorNonZero);

        Assert.Equal(ShaderName.world3d_main, plan.WantedShader);
        Assert.Equal(Renderer3DThingVertexColorSource.None, plan.VertexColorSource);
    }

    [Fact]
    public void BuildThingLitColorPlanAppliesLinearDynamicLightsInsideRadius()
    {
        Renderer3DThingLitColorPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingLitColorPlan(
            thingIndex: 1,
            thingTypeDoesNotLightSelf: false,
            thingCenter: new Vector3f(3.0f, 0.0f, 0.0f),
            [
                new Renderer3DThingLightingCandidate(
                    10,
                    ThingIndex: 2,
                    Center: new Vector3f(0.0f, 0.0f, 0.0f),
                    Radius: 6.0f,
                    new Color4(0.2f, 0.4f, 0.6f, 0.5f),
                    Renderer3DDynamicLightRenderStyle.Normal,
                    SpotLight: false,
                    SpotDirection: new Vector3f(1.0f, 0.0f, 0.0f),
                    SpotRadius1Degrees: 0.0f,
                    SpotRadius2Degrees: 0.0f,
                    Linearity: 0.0f),
            ],
            inverseSquareLightAttenuation: false);

        Renderer3DThingLightContributionPlan contribution = Assert.Single(plan.Contributions);
        Assert.False(contribution.SkippedSelfLight);
        Assert.True(contribution.InsideRadius);
        Assert.Equal(0.5f, contribution.Attenuation, precision: 5);
        Assert.Equal(1.0f, contribution.SpotScale, precision: 5);
        Assert.Equal(0.25f, contribution.ContributionScale, precision: 5);
        AssertColor(new Color4(0.05f, 0.1f, 0.15f, 0.0f), contribution.Contribution);
        AssertColor(new Color4(0.05f, 0.1f, 0.15f, 0.0f), plan.LitColor);
    }

    [Fact]
    public void BuildThingLitColorPlanAppliesSubtractiveSignAndSkipsSelfOrOutOfRadiusLights()
    {
        Renderer3DThingLitColorPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingLitColorPlan(
            thingIndex: 7,
            thingTypeDoesNotLightSelf: true,
            thingCenter: new Vector3f(5.0f, 0.0f, 0.0f),
            [
                new Renderer3DThingLightingCandidate(1, ThingIndex: 7, Center: new Vector3f(0.0f, 0.0f, 0.0f), Radius: 20.0f, new Color4(1.0f, 1.0f, 1.0f, 1.0f), Renderer3DDynamicLightRenderStyle.Normal, SpotLight: false, SpotDirection: new Vector3f(1.0f, 0.0f, 0.0f), SpotRadius1Degrees: 0.0f, SpotRadius2Degrees: 0.0f, Linearity: 0.0f),
                new Renderer3DThingLightingCandidate(2, ThingIndex: 8, Center: new Vector3f(0.0f, 0.0f, 0.0f), Radius: 10.0f, new Color4(0.4f, 0.2f, 0.1f, 1.0f), Renderer3DDynamicLightRenderStyle.Subtractive, SpotLight: false, SpotDirection: new Vector3f(1.0f, 0.0f, 0.0f), SpotRadius1Degrees: 0.0f, SpotRadius2Degrees: 0.0f, Linearity: 0.0f),
                new Renderer3DThingLightingCandidate(3, ThingIndex: 9, Center: new Vector3f(100.0f, 0.0f, 0.0f), Radius: 4.0f, new Color4(1.0f, 1.0f, 1.0f, 1.0f), Renderer3DDynamicLightRenderStyle.Normal, SpotLight: false, SpotDirection: new Vector3f(1.0f, 0.0f, 0.0f), SpotRadius1Degrees: 0.0f, SpotRadius2Degrees: 0.0f, Linearity: 0.0f),
            ],
            inverseSquareLightAttenuation: false);

        Assert.True(plan.Contributions[0].SkippedSelfLight);
        Assert.False(plan.Contributions[0].InsideRadius);
        Assert.False(plan.Contributions[2].SkippedSelfLight);
        Assert.False(plan.Contributions[2].InsideRadius);
        AssertColor(new Color4(-0.2f, -0.1f, -0.05f, 0.0f), plan.Contributions[1].Contribution);
        AssertColor(new Color4(-0.2f, -0.1f, -0.05f, 0.0f), plan.LitColor);
    }

    [Fact]
    public void BuildThingLitColorPlanAppliesInverseSquareAttenuationAndSpotFalloff()
    {
        Renderer3DThingLitColorPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingLitColorPlan(
            thingIndex: 1,
            thingTypeDoesNotLightSelf: false,
            thingCenter: new Vector3f(-1.0f, 0.0f, 0.0f),
            [
                new Renderer3DThingLightingCandidate(1, ThingIndex: 2, Center: new Vector3f(0.0f, 0.0f, 0.0f), Radius: 10.0f, new Color4(1.0f, 0.5f, 0.25f, 1.0f), Renderer3DDynamicLightRenderStyle.Normal, SpotLight: true, SpotDirection: new Vector3f(1.0f, 0.0f, 0.0f), SpotRadius1Degrees: 30.0f, SpotRadius2Degrees: 60.0f, Linearity: 0.0f),
            ],
            inverseSquareLightAttenuation: true);

        float distance = (float)Math.Sqrt(10.0f) * 2.0f;
        float diameter = 20.0f;
        float strength = 40.0f;
        float a = distance / diameter;
        float b = Math.Clamp(1.0f - a * a * a * a, 0.0f, 1.0f);
        float expectedAttenuation = b * b / (distance * distance + 1.0f) * strength;

        Renderer3DThingLightContributionPlan contribution = Assert.Single(plan.Contributions);
        Assert.Equal(expectedAttenuation, contribution.Attenuation, precision: 5);
        Assert.Equal(1.0f, contribution.SpotScale, precision: 5);
        Assert.Equal(expectedAttenuation, contribution.ContributionScale, precision: 5);
        AssertColor(new Color4(expectedAttenuation, expectedAttenuation * 0.5f, expectedAttenuation * 0.25f, 0.0f), plan.LitColor);
    }

    [Fact]
    public void BuildThingLitColorPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildThingLitColorPlan(1, thingTypeDoesNotLightSelf: false, new Vector3f(), null!, inverseSquareLightAttenuation: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildThingLitColorPlan(1, thingTypeDoesNotLightSelf: false, new Vector3f(float.NaN, 0.0f, 0.0f), [], inverseSquareLightAttenuation: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildThingLitColorPlan(
            1,
            thingTypeDoesNotLightSelf: false,
            new Vector3f(),
            [
                new Renderer3DThingLightingCandidate(1, ThingIndex: 2, Center: new Vector3f(float.NaN, 0.0f, 0.0f), Radius: 1.0f, new Color4(), Renderer3DDynamicLightRenderStyle.Normal, SpotLight: false, SpotDirection: new Vector3f(), SpotRadius1Degrees: 0.0f, SpotRadius2Degrees: 0.0f, Linearity: 0.0f),
            ],
            inverseSquareLightAttenuation: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildThingLitColorPlan(
            1,
            thingTypeDoesNotLightSelf: false,
            new Vector3f(),
            [
                new Renderer3DThingLightingCandidate(1, ThingIndex: 2, Center: new Vector3f(), Radius: -1.0f, new Color4(), Renderer3DDynamicLightRenderStyle.Normal, SpotLight: false, SpotDirection: new Vector3f(), SpotRadius1Degrees: 0.0f, SpotRadius2Degrees: 0.0f, Linearity: 0.0f),
            ],
            inverseSquareLightAttenuation: false));
    }

    [Fact]
    public void BuildTranslucentGeometryOrderPlanSortsWallsBackToFrontByCameraDistance()
    {
        Renderer3DTranslucentGeometryOrderPlan plan = Renderer3DGeometryLifecyclePlan.BuildTranslucentGeometryOrderPlan(
            [
                new Renderer3DTranslucentGeometryCandidate(1, Renderer3DVisualGeometryType.WallMiddle, new Vector3D(3, 0, 0), RenderPass.Alpha),
                new Renderer3DTranslucentGeometryCandidate(2, Renderer3DVisualGeometryType.WallUpper, new Vector3D(10, 0, 0), RenderPass.Alpha),
                new Renderer3DTranslucentGeometryCandidate(3, Renderer3DVisualGeometryType.WallLower, new Vector3D(1, 0, 0), RenderPass.Alpha),
            ],
            new Vector3D(0, 0, 0));

        Assert.Equal([2, 1, 3], plan.Draws.Select(draw => draw.GeometryId).ToArray());
    }

    [Fact]
    public void BuildTranslucentGeometryOrderPlanUsesVerticalDistanceWhenEitherGeometryIsPlane()
    {
        Renderer3DTranslucentGeometryOrderPlan plan = Renderer3DGeometryLifecyclePlan.BuildTranslucentGeometryOrderPlan(
            [
                new Renderer3DTranslucentGeometryCandidate(1, Renderer3DVisualGeometryType.WallMiddle, new Vector3D(100, 0, 1), RenderPass.Alpha),
                new Renderer3DTranslucentGeometryCandidate(2, Renderer3DVisualGeometryType.Floor, new Vector3D(1, 0, 20), RenderPass.Alpha),
                new Renderer3DTranslucentGeometryCandidate(3, Renderer3DVisualGeometryType.Ceiling, new Vector3D(2, 0, 5), RenderPass.Alpha),
            ],
            new Vector3D(0, 0, 0));

        Assert.Equal([2, 3, 1], plan.Draws.Select(draw => draw.GeometryId).ToArray());
    }

    [Fact]
    public void BuildTranslucentGeometryOrderPlanMatchesUdbDestinationBlendChanges()
    {
        Renderer3DTranslucentGeometryOrderPlan plan = Renderer3DGeometryLifecyclePlan.BuildTranslucentGeometryOrderPlan(
            [
                new Renderer3DTranslucentGeometryCandidate(1, Renderer3DVisualGeometryType.WallMiddle, new Vector3D(4, 0, 0), RenderPass.Alpha),
                new Renderer3DTranslucentGeometryCandidate(2, Renderer3DVisualGeometryType.WallMiddle, new Vector3D(3, 0, 0), RenderPass.Alpha),
                new Renderer3DTranslucentGeometryCandidate(3, Renderer3DVisualGeometryType.WallMiddle, new Vector3D(2, 0, 0), RenderPass.Additive),
                new Renderer3DTranslucentGeometryCandidate(4, Renderer3DVisualGeometryType.WallMiddle, new Vector3D(1, 0, 0), RenderPass.Alpha),
            ],
            new Vector3D(0, 0, 0));

        Assert.Equal(
            [
                new Renderer3DTranslucentGeometryDrawPlan(1, RenderPass.Alpha, Blend.InverseSourceAlpha),
                new Renderer3DTranslucentGeometryDrawPlan(2, RenderPass.Alpha, DestinationBlendChange: null),
                new Renderer3DTranslucentGeometryDrawPlan(3, RenderPass.Additive, Blend.One),
                new Renderer3DTranslucentGeometryDrawPlan(4, RenderPass.Alpha, Blend.InverseSourceAlpha),
            ],
            plan.Draws);
    }

    [Fact]
    public void BuildTranslucentGeometryOrderPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildTranslucentGeometryOrderPlan(null!, new Vector3D()));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildTranslucentGeometryOrderPlan([], new Vector3D(double.NaN, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildTranslucentGeometryOrderPlan(
            [
                new Renderer3DTranslucentGeometryCandidate(1, Renderer3DVisualGeometryType.WallMiddle, new Vector3D(0, double.NaN, 0), RenderPass.Alpha),
            ],
            new Vector3D()));
    }

    [Fact]
    public void BuildTranslucentThingOrderPlanSortsThingsBackToFrontByCameraDistance()
    {
        Renderer3DTranslucentThingOrderPlan plan = Renderer3DGeometryLifecyclePlan.BuildTranslucentThingOrderPlan(
            [
                new Renderer3DTranslucentThingCandidate(1, new Vector3D(3, 0, 0), RenderPass.Alpha),
                new Renderer3DTranslucentThingCandidate(2, new Vector3D(10, 0, 0), RenderPass.Alpha),
                new Renderer3DTranslucentThingCandidate(3, new Vector3D(1, 0, 0), RenderPass.Alpha),
            ],
            new Vector3D(0, 0, 0));

        Assert.Equal(TextureAddress.Clamp, plan.InitialTextureAddress);
        Assert.Equal(Cull.None, plan.InitialCullMode);
        Assert.Equal([2, 1, 3], plan.Draws.Select(draw => draw.ThingId).ToArray());
        Assert.Equal(TextureAddress.Wrap, plan.RestoredTextureAddress);
        Assert.Equal(Cull.Clockwise, plan.RestoredCullMode);
    }

    [Fact]
    public void BuildTranslucentThingOrderPlanMatchesUdbDestinationBlendChanges()
    {
        Renderer3DTranslucentThingOrderPlan plan = Renderer3DGeometryLifecyclePlan.BuildTranslucentThingOrderPlan(
            [
                new Renderer3DTranslucentThingCandidate(1, new Vector3D(4, 0, 0), RenderPass.Alpha),
                new Renderer3DTranslucentThingCandidate(2, new Vector3D(3, 0, 0), RenderPass.Alpha),
                new Renderer3DTranslucentThingCandidate(3, new Vector3D(2, 0, 0), RenderPass.Additive),
                new Renderer3DTranslucentThingCandidate(4, new Vector3D(1, 0, 0), RenderPass.Alpha),
            ],
            new Vector3D(0, 0, 0));

        Assert.Equal(
            [
                new Renderer3DTranslucentThingDrawPlan(1, RenderPass.Alpha, Blend.InverseSourceAlpha),
                new Renderer3DTranslucentThingDrawPlan(2, RenderPass.Alpha, DestinationBlendChange: null),
                new Renderer3DTranslucentThingDrawPlan(3, RenderPass.Additive, Blend.One),
                new Renderer3DTranslucentThingDrawPlan(4, RenderPass.Alpha, Blend.InverseSourceAlpha),
            ],
            plan.Draws);
    }

    [Fact]
    public void BuildTranslucentThingOrderPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildTranslucentThingOrderPlan(null!, new Vector3D()));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildTranslucentThingOrderPlan([], new Vector3D(double.NaN, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildTranslucentThingOrderPlan(
            [
                new Renderer3DTranslucentThingCandidate(1, new Vector3D(0, double.NaN, 0), RenderPass.Alpha),
            ],
            new Vector3D()));
    }

    [Theory]
    [InlineData(false, ShaderName.world3d_main_vertexcolor, ShaderName.world3d_main_highlight_vertexcolor)]
    [InlineData(true, ShaderName.world3d_fullbright, ShaderName.world3d_fullbright_highlight)]
    public void BuildModelRenderPlanUsesUdbInitialShaderAndLightUniformState(
        bool fullBrightness,
        ShaderName initialShader,
        ShaderName highlightShader)
    {
        Renderer3DModelRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildModelRenderPlan(
            translucent: false,
            maskedModelThingGroups: [],
            translucentModelThings: [],
            new Vector3D(),
            fullBrightness,
            lightCount: 2,
            inverseSquareLightAttenuation: true);

        Assert.Equal(initialShader, plan.InitialShader);
        Assert.Equal(highlightShader, plan.HighlightShader);
        Assert.True(plan.LightsEnabled);
        Assert.True(plan.IgnoreNormals);
        Assert.True(plan.UseLightStrength);
        Assert.Empty(plan.Draws);
    }

    [Fact]
    public void BuildModelRenderPlanFlattensMaskedModelGroupsWithoutSortingOrBlendChanges()
    {
        Renderer3DModelRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildModelRenderPlan(
            translucent: false,
            maskedModelThingGroups:
            [
                new Renderer3DModelThingGroup(
                    [
                        new Renderer3DModelThingCandidate(1, new Vector3D(100, 0, 0), RenderPass.Alpha),
                        new Renderer3DModelThingCandidate(2, new Vector3D(1, 0, 0), RenderPass.Additive),
                    ]),
                new Renderer3DModelThingGroup(
                    [
                        new Renderer3DModelThingCandidate(3, new Vector3D(50, 0, 0), RenderPass.Alpha),
                    ]),
            ],
            translucentModelThings:
            [
                new Renderer3DModelThingCandidate(4, new Vector3D(200, 0, 0), RenderPass.Alpha),
            ],
            new Vector3D(),
            fullBrightness: false,
            lightCount: 0,
            inverseSquareLightAttenuation: false);

        Assert.False(plan.LightsEnabled);
        Assert.False(plan.UseLightStrength);
        Assert.Equal(
            [
                new Renderer3DModelThingDrawPlan(1, RenderPass.Alpha, DestinationBlendChange: null),
                new Renderer3DModelThingDrawPlan(2, RenderPass.Additive, DestinationBlendChange: null),
                new Renderer3DModelThingDrawPlan(3, RenderPass.Alpha, DestinationBlendChange: null),
            ],
            plan.Draws);
    }

    [Fact]
    public void BuildModelRenderPlanSortsTranslucentModelsBackToFrontAndAppliesBlendChanges()
    {
        Renderer3DModelRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildModelRenderPlan(
            translucent: true,
            maskedModelThingGroups:
            [
                new Renderer3DModelThingGroup(
                    [
                        new Renderer3DModelThingCandidate(5, new Vector3D(100, 0, 0), RenderPass.Alpha),
                    ]),
            ],
            translucentModelThings:
            [
                new Renderer3DModelThingCandidate(1, new Vector3D(4, 0, 0), RenderPass.Alpha),
                new Renderer3DModelThingCandidate(2, new Vector3D(3, 0, 0), RenderPass.Alpha),
                new Renderer3DModelThingCandidate(3, new Vector3D(2, 0, 0), RenderPass.Additive),
                new Renderer3DModelThingCandidate(4, new Vector3D(1, 0, 0), RenderPass.Alpha),
            ],
            new Vector3D(),
            fullBrightness: false,
            lightCount: 0,
            inverseSquareLightAttenuation: false);

        Assert.Equal(
            [
                new Renderer3DModelThingDrawPlan(1, RenderPass.Alpha, Blend.InverseSourceAlpha),
                new Renderer3DModelThingDrawPlan(2, RenderPass.Alpha, DestinationBlendChange: null),
                new Renderer3DModelThingDrawPlan(3, RenderPass.Additive, Blend.One),
                new Renderer3DModelThingDrawPlan(4, RenderPass.Alpha, Blend.InverseSourceAlpha),
            ],
            plan.Draws);
    }

    [Fact]
    public void BuildModelRenderPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildModelRenderPlan(
            translucent: false,
            maskedModelThingGroups: null!,
            translucentModelThings: [],
            new Vector3D(),
            fullBrightness: false,
            lightCount: 0,
            inverseSquareLightAttenuation: false));
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildModelRenderPlan(
            translucent: false,
            maskedModelThingGroups: [],
            translucentModelThings: null!,
            new Vector3D(),
            fullBrightness: false,
            lightCount: 0,
            inverseSquareLightAttenuation: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildModelRenderPlan(
            translucent: false,
            maskedModelThingGroups: [],
            translucentModelThings: [],
            new Vector3D(double.NaN, 0, 0),
            fullBrightness: false,
            lightCount: 0,
            inverseSquareLightAttenuation: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildModelRenderPlan(
            translucent: false,
            maskedModelThingGroups: [],
            translucentModelThings: [],
            new Vector3D(),
            fullBrightness: false,
            lightCount: -1,
            inverseSquareLightAttenuation: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildModelRenderPlan(
            translucent: false,
            maskedModelThingGroups:
            [
                new Renderer3DModelThingGroup(
                    [
                        new Renderer3DModelThingCandidate(1, new Vector3D(double.NaN, 0, 0), RenderPass.Alpha),
                    ]),
            ],
            translucentModelThings: [],
            new Vector3D(),
            fullBrightness: false,
            lightCount: 0,
            inverseSquareLightAttenuation: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildModelRenderPlan(
            translucent: true,
            maskedModelThingGroups: [],
            translucentModelThings:
            [
                new Renderer3DModelThingCandidate(1, new Vector3D(0, double.NaN, 0), RenderPass.Alpha),
            ],
            new Vector3D(),
            fullBrightness: false,
            lightCount: 0,
            inverseSquareLightAttenuation: false));
    }

    [Fact]
    public void BuildModelDrawStatePlanUpdatesBeforeDistanceCullAndSkipsLaterUniforms()
    {
        Renderer3DModelDrawStatePlan plan = Renderer3DGeometryLifecyclePlan.BuildModelDrawStatePlan(
            [
                new Renderer3DModelThingDrawStateCandidate(
                    1,
                    new Vector3D(10, 0, 0),
                    DistanceCheckSq: 25.0,
                    Highlighted: true,
                    Selected: true,
                    SectorHasFog: true,
                    SectorDesaturation: 0.5),
            ],
            new Vector3D(),
            fullBrightness: false,
            drawFog: true,
            classicRendering: false,
            showHighlight: true,
            showSelection: true);

        Assert.Equal(ShaderName.world3d_main_vertexcolor, plan.InitialShader);
        Assert.Equal(
            [
                new Renderer3DModelThingDrawStatePlan(
                    1,
                    UpdateBuffer: true,
                    SkippedByDistance: true,
                    WantedShader: null,
                    SwitchShader: false,
                    SetVertexColor: false,
                    SetHighlightColor: false,
                    SetFogUniforms: false,
                    SectorDesaturation: 0.0),
            ],
            plan.Draws);
    }

    [Fact]
    public void BuildModelDrawStatePlanSelectsHighlightAndFogShadersAndTracksSwitches()
    {
        Renderer3DModelDrawStatePlan plan = Renderer3DGeometryLifecyclePlan.BuildModelDrawStatePlan(
            [
                new Renderer3DModelThingDrawStateCandidate(
                    1,
                    new Vector3D(3, 0, 0),
                    DistanceCheckSq: double.MaxValue,
                    Highlighted: false,
                    Selected: false,
                    SectorHasFog: false,
                    SectorDesaturation: 0.0),
                new Renderer3DModelThingDrawStateCandidate(
                    2,
                    new Vector3D(4, 0, 0),
                    DistanceCheckSq: double.MaxValue,
                    Highlighted: true,
                    Selected: false,
                    SectorHasFog: true,
                    SectorDesaturation: 0.25),
                new Renderer3DModelThingDrawStateCandidate(
                    3,
                    new Vector3D(5, 0, 0),
                    DistanceCheckSq: double.MaxValue,
                    Highlighted: false,
                    Selected: true,
                    SectorHasFog: true,
                    SectorDesaturation: 0.5),
                new Renderer3DModelThingDrawStateCandidate(
                    4,
                    new Vector3D(6, 0, 0),
                    DistanceCheckSq: double.MaxValue,
                    Highlighted: false,
                    Selected: false,
                    SectorHasFog: false,
                    SectorDesaturation: 0.75),
            ],
            new Vector3D(),
            fullBrightness: false,
            drawFog: true,
            classicRendering: false,
            showHighlight: true,
            showSelection: true);

        Assert.Equal(
            [
                new Renderer3DModelThingDrawStatePlan(
                    1,
                    UpdateBuffer: true,
                    SkippedByDistance: false,
                    ShaderName.world3d_main_vertexcolor,
                    SwitchShader: false,
                    SetVertexColor: true,
                    SetHighlightColor: true,
                    SetFogUniforms: false,
                    SectorDesaturation: 0.0),
                new Renderer3DModelThingDrawStatePlan(
                    2,
                    UpdateBuffer: true,
                    SkippedByDistance: false,
                    ShaderName.world3d_main_highlight_fog_vertexcolor,
                    SwitchShader: true,
                    SetVertexColor: true,
                    SetHighlightColor: true,
                    SetFogUniforms: true,
                    SectorDesaturation: 0.25),
                new Renderer3DModelThingDrawStatePlan(
                    3,
                    UpdateBuffer: true,
                    SkippedByDistance: false,
                    ShaderName.world3d_main_highlight_fog_vertexcolor,
                    SwitchShader: false,
                    SetVertexColor: true,
                    SetHighlightColor: true,
                    SetFogUniforms: true,
                    SectorDesaturation: 0.5),
                new Renderer3DModelThingDrawStatePlan(
                    4,
                    UpdateBuffer: true,
                    SkippedByDistance: false,
                    ShaderName.world3d_main_vertexcolor,
                    SwitchShader: true,
                    SetVertexColor: true,
                    SetHighlightColor: true,
                    SetFogUniforms: false,
                    SectorDesaturation: 0.75),
            ],
            plan.Draws);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void BuildModelDrawStatePlanSuppressesFogForFullBrightClassicOrFoglessSectors(
        bool fullBrightness,
        bool classicRendering,
        bool sectorHasFog)
    {
        Renderer3DModelDrawStatePlan plan = Renderer3DGeometryLifecyclePlan.BuildModelDrawStatePlan(
            [
                new Renderer3DModelThingDrawStateCandidate(
                    1,
                    new Vector3D(1, 0, 0),
                    DistanceCheckSq: double.MaxValue,
                    Highlighted: true,
                    Selected: false,
                    sectorHasFog,
                    SectorDesaturation: 0.0),
            ],
            new Vector3D(),
            fullBrightness,
            drawFog: true,
            classicRendering,
            showHighlight: true,
            showSelection: true);

        Assert.False(plan.Draws[0].SetFogUniforms);
        Assert.Equal(
            fullBrightness ? ShaderName.world3d_fullbright_highlight : ShaderName.world3d_main_highlight_vertexcolor,
            plan.Draws[0].WantedShader);
    }

    [Fact]
    public void BuildModelDrawStatePlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildModelDrawStatePlan(
            null!,
            new Vector3D(),
            fullBrightness: false,
            drawFog: false,
            classicRendering: false,
            showHighlight: true,
            showSelection: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildModelDrawStatePlan(
            [],
            new Vector3D(double.NaN, 0, 0),
            fullBrightness: false,
            drawFog: false,
            classicRendering: false,
            showHighlight: true,
            showSelection: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildModelDrawStatePlan(
            [
                new Renderer3DModelThingDrawStateCandidate(1, new Vector3D(double.NaN, 0, 0), double.MaxValue, Highlighted: false, Selected: false, SectorHasFog: false, SectorDesaturation: 0.0),
            ],
            new Vector3D(),
            fullBrightness: false,
            drawFog: false,
            classicRendering: false,
            showHighlight: true,
            showSelection: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildModelDrawStatePlan(
            [
                new Renderer3DModelThingDrawStateCandidate(1, new Vector3D(), DistanceCheckSq: -1.0, Highlighted: false, Selected: false, SectorHasFog: false, SectorDesaturation: 0.0),
            ],
            new Vector3D(),
            fullBrightness: false,
            drawFog: false,
            classicRendering: false,
            showHighlight: true,
            showSelection: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildModelDrawStatePlan(
            [
                new Renderer3DModelThingDrawStateCandidate(1, new Vector3D(), double.MaxValue, Highlighted: false, Selected: false, SectorHasFog: false, SectorDesaturation: double.NaN),
            ],
            new Vector3D(),
            fullBrightness: false,
            drawFog: false,
            classicRendering: false,
            showHighlight: true,
            showSelection: true));
    }

    [Fact]
    public void BuildModelLightUniformsPlanSelectsIntersectingLightsUntilSurfaceLimit()
    {
        Renderer3DModelLightUniformsPlan plan = Renderer3DGeometryLifecyclePlan.BuildModelLightUniformsPlan(
            [
                [
                    new Renderer3DModelLightCandidate(1, BoundingBoxesIntersect: false, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: 20.0, Linearity: 0.1),
                    new Renderer3DModelLightCandidate(2, BoundingBoxesIntersect: true, SpotLight: true, SpotRadius1Degrees: 60.0, SpotRadius2Degrees: 120.0, Radius: 10.0, Linearity: 0.25),
                    new Renderer3DModelLightCandidate(3, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: 100.0, Linearity: 0.5),
                    new Renderer3DModelLightCandidate(4, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: 5.0, Linearity: 0.75),
                ],
            ],
            maxDynamicLightsPerSurface: 2);

        Renderer3DModelLightUniformPlan thing = Assert.Single(plan.Things);
        Assert.True(thing.SetLightColorUniform);
        Assert.True(thing.SetLightDetailUniforms);
        Assert.Equal(0, thing.ClearedLightColorSlots);
        Assert.Equal(
            [
                new Renderer3DModelLightSlotPlan(2, SpotLight: true, SpotRadius1Cosine: 0.5f, SpotRadius2Cosine: -0.5f, Strength: 40.0f, Linearity: 0.25f),
                new Renderer3DModelLightSlotPlan(3, SpotLight: false, SpotRadius1Cosine: 0.0f, SpotRadius2Cosine: 0.0f, Strength: 1500.0f, Linearity: 0.5f),
            ],
            thing.Lights);
    }

    [Fact]
    public void BuildModelLightUniformsPlanMatchesUdbHadLightsUniformWriteRule()
    {
        Renderer3DModelLightUniformsPlan plan = Renderer3DGeometryLifecyclePlan.BuildModelLightUniformsPlan(
            [
                [],
                [
                    new Renderer3DModelLightCandidate(1, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: 8.0, Linearity: 0.25),
                ],
                [
                    new Renderer3DModelLightCandidate(2, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: 9.0, Linearity: 0.5),
                ],
                [],
                [],
            ],
            maxDynamicLightsPerSurface: 3);

        Assert.Equal([false, true, true, true, false], plan.Things.Select(thing => thing.SetLightColorUniform).ToArray());
        Assert.Equal([false, true, true, false, false], plan.Things.Select(thing => thing.SetLightDetailUniforms).ToArray());
        Assert.Equal([3, 2, 2, 3, 3], plan.Things.Select(thing => thing.ClearedLightColorSlots).ToArray());
    }

    [Fact]
    public void BuildModelLightUniformsPlanAllowsZeroSurfaceLights()
    {
        Renderer3DModelLightUniformsPlan plan = Renderer3DGeometryLifecyclePlan.BuildModelLightUniformsPlan(
            [
                [
                    new Renderer3DModelLightCandidate(1, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: 8.0, Linearity: 0.25),
                ],
            ],
            maxDynamicLightsPerSurface: 0);

        Renderer3DModelLightUniformPlan thing = Assert.Single(plan.Things);
        Assert.Empty(thing.Lights);
        Assert.Equal(0, thing.ClearedLightColorSlots);
        Assert.False(thing.SetLightColorUniform);
        Assert.False(thing.SetLightDetailUniforms);
    }

    [Fact]
    public void BuildModelLightUniformsPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildModelLightUniformsPlan(null!, maxDynamicLightsPerSurface: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildModelLightUniformsPlan([], maxDynamicLightsPerSurface: -1));
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildModelLightUniformsPlan([null!], maxDynamicLightsPerSurface: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildModelLightUniformsPlan(
            [
                [
                    new Renderer3DModelLightCandidate(1, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: double.NaN, SpotRadius2Degrees: 0.0, Radius: 8.0, Linearity: 0.25),
                ],
            ],
            maxDynamicLightsPerSurface: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildModelLightUniformsPlan(
            [
                [
                    new Renderer3DModelLightCandidate(1, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: double.NaN, Radius: 8.0, Linearity: 0.25),
                ],
            ],
            maxDynamicLightsPerSurface: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildModelLightUniformsPlan(
            [
                [
                    new Renderer3DModelLightCandidate(1, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: -1.0, Linearity: 0.25),
                ],
            ],
            maxDynamicLightsPerSurface: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildModelLightUniformsPlan(
            [
                [
                    new Renderer3DModelLightCandidate(1, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: 8.0, Linearity: double.NaN),
                ],
            ],
            maxDynamicLightsPerSurface: 1));
    }

    [Fact]
    public void BuildGeometryLightUniformsPlanSelectsIntersectingLightsUntilSurfaceLimit()
    {
        Renderer3DGeometryLightUniformsPlan plan = Renderer3DGeometryLifecyclePlan.BuildGeometryLightUniformsPlan(
            [
                [
                    new Renderer3DGeometryLightCandidate(1, BoundingBoxesIntersect: false, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: 20.0, Linearity: 0.1),
                    new Renderer3DGeometryLightCandidate(2, BoundingBoxesIntersect: true, SpotLight: true, SpotRadius1Degrees: 60.0, SpotRadius2Degrees: 120.0, Radius: 10.0, Linearity: 0.25),
                    new Renderer3DGeometryLightCandidate(3, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: 100.0, Linearity: 0.5),
                    new Renderer3DGeometryLightCandidate(4, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: 5.0, Linearity: 0.75),
                ],
            ],
            maxDynamicLightsPerSurface: 2);

        Renderer3DGeometryLightUniformPlan geometry = Assert.Single(plan.Geometry);
        Assert.True(geometry.SetLightColorUniform);
        Assert.True(geometry.SetLightDetailUniforms);
        Assert.Equal(0, geometry.ClearedLightColorSlots);
        Assert.Equal(
            [
                new Renderer3DGeometryLightSlotPlan(2, SpotLight: true, SpotRadius1Cosine: 0.5f, SpotRadius2Cosine: -0.5f, Strength: 40.0f, Linearity: 0.25f),
                new Renderer3DGeometryLightSlotPlan(3, SpotLight: false, SpotRadius1Cosine: 0.0f, SpotRadius2Cosine: 0.0f, Strength: 1500.0f, Linearity: 0.5f),
            ],
            geometry.Lights);
    }

    [Fact]
    public void BuildGeometryLightUniformsPlanMatchesUdbHadLightsUniformWriteRule()
    {
        Renderer3DGeometryLightUniformsPlan plan = Renderer3DGeometryLifecyclePlan.BuildGeometryLightUniformsPlan(
            [
                [],
                [
                    new Renderer3DGeometryLightCandidate(1, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: 8.0, Linearity: 0.25),
                ],
                [
                    new Renderer3DGeometryLightCandidate(2, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: 9.0, Linearity: 0.5),
                ],
                [],
                [],
            ],
            maxDynamicLightsPerSurface: 3);

        Assert.Equal([false, true, true, true, false], plan.Geometry.Select(geometry => geometry.SetLightColorUniform).ToArray());
        Assert.Equal([false, true, true, false, false], plan.Geometry.Select(geometry => geometry.SetLightDetailUniforms).ToArray());
        Assert.Equal([3, 2, 2, 3, 3], plan.Geometry.Select(geometry => geometry.ClearedLightColorSlots).ToArray());
    }

    [Fact]
    public void BuildGeometryLightUniformsPlanAllowsZeroSurfaceLights()
    {
        Renderer3DGeometryLightUniformsPlan plan = Renderer3DGeometryLifecyclePlan.BuildGeometryLightUniformsPlan(
            [
                [
                    new Renderer3DGeometryLightCandidate(1, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: 8.0, Linearity: 0.25),
                ],
            ],
            maxDynamicLightsPerSurface: 0);

        Renderer3DGeometryLightUniformPlan geometry = Assert.Single(plan.Geometry);
        Assert.Empty(geometry.Lights);
        Assert.Equal(0, geometry.ClearedLightColorSlots);
        Assert.False(geometry.SetLightColorUniform);
        Assert.False(geometry.SetLightDetailUniforms);
    }

    [Fact]
    public void BuildGeometryLightUniformsPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildGeometryLightUniformsPlan(null!, maxDynamicLightsPerSurface: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildGeometryLightUniformsPlan([], maxDynamicLightsPerSurface: -1));
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildGeometryLightUniformsPlan([null!], maxDynamicLightsPerSurface: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildGeometryLightUniformsPlan(
            [
                [
                    new Renderer3DGeometryLightCandidate(1, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: double.NaN, SpotRadius2Degrees: 0.0, Radius: 8.0, Linearity: 0.25),
                ],
            ],
            maxDynamicLightsPerSurface: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildGeometryLightUniformsPlan(
            [
                [
                    new Renderer3DGeometryLightCandidate(1, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: double.NaN, Radius: 8.0, Linearity: 0.25),
                ],
            ],
            maxDynamicLightsPerSurface: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildGeometryLightUniformsPlan(
            [
                [
                    new Renderer3DGeometryLightCandidate(1, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: -1.0, Linearity: 0.25),
                ],
            ],
            maxDynamicLightsPerSurface: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildGeometryLightUniformsPlan(
            [
                [
                    new Renderer3DGeometryLightCandidate(1, BoundingBoxesIntersect: true, SpotLight: false, SpotRadius1Degrees: 0.0, SpotRadius2Degrees: 0.0, Radius: 8.0, Linearity: double.NaN),
                ],
            ],
            maxDynamicLightsPerSurface: 1));
    }

    [Theory]
    [InlineData(0.0, 0.0, 0.0, true)]
    [InlineData(10.0, 0.0, 0.0, true)]
    [InlineData(10.1, 0.0, 0.0, false)]
    [InlineData(0.0, -10.0, 0.0, true)]
    [InlineData(0.0, -10.1, 0.0, false)]
    [InlineData(0.0, 0.0, 10.0, true)]
    [InlineData(0.0, 0.0, 10.1, false)]
    public void BoundingBoxesIntersectMatchesUdbAxisOverlapRule(double x, double y, double z, bool expected)
    {
        Vector3D[] first =
        [
            new(0.0, 0.0, 0.0),
            new(-5.0, -5.0, -5.0),
        ];
        Vector3D[] second =
        [
            new(x, y, z),
            new(x - 5.0, y - 5.0, z - 5.0),
        ];

        Assert.Equal(expected, Renderer3DGeometryLifecyclePlan.BoundingBoxesIntersect(first, second));
    }

    [Fact]
    public void BoundingBoxesIntersectRejectsInvalidInputs()
    {
        Vector3D[] valid =
        [
            new(0.0, 0.0, 0.0),
            new(-1.0, -1.0, -1.0),
        ];

        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BoundingBoxesIntersect(null!, valid));
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BoundingBoxesIntersect(valid, null!));
        Assert.Throws<ArgumentException>(() => Renderer3DGeometryLifecyclePlan.BoundingBoxesIntersect([new Vector3D()], valid));
        Assert.Throws<ArgumentException>(() => Renderer3DGeometryLifecyclePlan.BoundingBoxesIntersect(valid, [new Vector3D()]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BoundingBoxesIntersect(
            [
                new Vector3D(double.NaN, 0.0, 0.0),
                new Vector3D(-1.0, -1.0, -1.0),
            ],
            valid));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BoundingBoxesIntersect(
            valid,
            [
                new Vector3D(0.0, 0.0, 0.0),
                new Vector3D(double.PositiveInfinity, -1.0, -1.0),
            ]));
    }

    [Fact]
    public void BuildModelMeshRenderPlanSetsTextureAndDrawsEveryMeshInThingOrder()
    {
        Renderer3DModelMeshRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildModelMeshRenderPlan(
            [
                new Renderer3DModelMeshCandidate(1, MeshCount: 2, Textures: ["A", "B"]),
                new Renderer3DModelMeshCandidate(2, MeshCount: 1, Textures: ["C"]),
            ]);

        Assert.True(plan.DisableLightsEnabledUniform);
        Assert.Equal(
            [
                new Renderer3DModelMeshDrawPlan(1, MeshIndex: 0, Texture: "A", SetTexture: true, DrawMesh: true),
                new Renderer3DModelMeshDrawPlan(1, MeshIndex: 1, Texture: "B", SetTexture: true, DrawMesh: true),
                new Renderer3DModelMeshDrawPlan(2, MeshIndex: 0, Texture: "C", SetTexture: true, DrawMesh: true),
            ],
            plan.Draws);
    }

    [Fact]
    public void BuildModelMeshRenderPlanPreservesNullTexturesAndSkipsModelsWithoutMeshes()
    {
        Renderer3DModelMeshRenderPlan plan = Renderer3DGeometryLifecyclePlan.BuildModelMeshRenderPlan(
            [
                new Renderer3DModelMeshCandidate(1, MeshCount: 0, Textures: []),
                new Renderer3DModelMeshCandidate(2, MeshCount: 2, Textures: [null, "B"]),
            ]);

        Assert.True(plan.DisableLightsEnabledUniform);
        Assert.Equal(
            [
                new Renderer3DModelMeshDrawPlan(2, MeshIndex: 0, Texture: null, SetTexture: true, DrawMesh: true),
                new Renderer3DModelMeshDrawPlan(2, MeshIndex: 1, Texture: "B", SetTexture: true, DrawMesh: true),
            ],
            plan.Draws);
    }

    [Fact]
    public void BuildModelMeshRenderPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildModelMeshRenderPlan(null!));
        Assert.Throws<ArgumentNullException>(() => Renderer3DGeometryLifecyclePlan.BuildModelMeshRenderPlan(
            [
                new Renderer3DModelMeshCandidate(1, MeshCount: 1, Textures: null!),
            ]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildModelMeshRenderPlan(
            [
                new Renderer3DModelMeshCandidate(1, MeshCount: -1, Textures: []),
            ]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer3DGeometryLifecyclePlan.BuildModelMeshRenderPlan(
            [
                new Renderer3DModelMeshCandidate(1, MeshCount: 2, Textures: ["A"]),
            ]));
    }

    [Theory]
    [InlineData(false, Renderer3DThingPositionMatrixStrategy.Billboard)]
    [InlineData(true, Renderer3DThingPositionMatrixStrategy.XYBillboard)]
    public void BuildThingPositionMatrixPlanUsesBillboardStrategyForNormalThings(
        bool xyBillboard,
        Renderer3DThingPositionMatrixStrategy strategy)
    {
        Renderer3DThingPositionMatrixPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingPositionMatrixPlan(
            ThingRenderMode.NORMAL,
            ModelRenderMode.ALL,
            selected: false,
            xyBillboard);

        Assert.Equal(ThingRenderMode.NORMAL, plan.RequestedRenderMode);
        Assert.Equal(ThingRenderMode.NORMAL, plan.EffectiveRenderMode);
        Assert.Equal(strategy, plan.Strategy);
        Assert.False(plan.DemotedModelRenderMode);
    }

    [Theory]
    [InlineData(ThingRenderMode.FLATSPRITE)]
    [InlineData(ThingRenderMode.WALLSPRITE)]
    [InlineData(ThingRenderMode.MODEL)]
    [InlineData(ThingRenderMode.VOXEL)]
    public void BuildThingPositionMatrixPlanUsesDirectPositionForNonNormalEffectiveModes(ThingRenderMode renderMode)
    {
        Renderer3DThingPositionMatrixPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingPositionMatrixPlan(
            renderMode,
            ModelRenderMode.ALL,
            selected: false,
            xyBillboard: true);

        Assert.Equal(renderMode, plan.EffectiveRenderMode);
        Assert.Equal(Renderer3DThingPositionMatrixStrategy.DirectPosition, plan.Strategy);
        Assert.False(plan.DemotedModelRenderMode);
    }

    [Theory]
    [InlineData(ThingRenderMode.MODEL, ModelRenderMode.NONE, false)]
    [InlineData(ThingRenderMode.MODEL, ModelRenderMode.SELECTION, false)]
    [InlineData(ThingRenderMode.VOXEL, ModelRenderMode.NONE, true)]
    [InlineData(ThingRenderMode.VOXEL, ModelRenderMode.SELECTION, false)]
    public void BuildThingPositionMatrixPlanDemotesModelAndVoxelModesWhenUdbModelRenderingIsDisabled(
        ThingRenderMode renderMode,
        ModelRenderMode modelRenderMode,
        bool xyBillboard)
    {
        Renderer3DThingPositionMatrixPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingPositionMatrixPlan(
            renderMode,
            modelRenderMode,
            selected: false,
            xyBillboard);

        Assert.Equal(renderMode, plan.RequestedRenderMode);
        Assert.Equal(ThingRenderMode.NORMAL, plan.EffectiveRenderMode);
        Assert.Equal(
            xyBillboard ? Renderer3DThingPositionMatrixStrategy.XYBillboard : Renderer3DThingPositionMatrixStrategy.Billboard,
            plan.Strategy);
        Assert.True(plan.DemotedModelRenderMode);
    }

    [Theory]
    [InlineData(ModelRenderMode.SELECTION)]
    [InlineData(ModelRenderMode.ACTIVE_THINGS_FILTER)]
    [InlineData(ModelRenderMode.ALL)]
    public void BuildThingPositionMatrixPlanKeepsSelectedOrEnabledModelModes(ModelRenderMode modelRenderMode)
    {
        Renderer3DThingPositionMatrixPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingPositionMatrixPlan(
            ThingRenderMode.MODEL,
            modelRenderMode,
            selected: true,
            xyBillboard: true);

        Assert.Equal(ThingRenderMode.MODEL, plan.EffectiveRenderMode);
        Assert.Equal(Renderer3DThingPositionMatrixStrategy.DirectPosition, plan.Strategy);
        Assert.False(plan.DemotedModelRenderMode);
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
        Assert.Contains("public void AddSectorGeometry(VisualGeometry g)", source, StringComparison.Ordinal);
        Assert.Contains("if(g.Texture != null && g.Triangles > 0)", source, StringComparison.Ordinal);
        Assert.Contains("if(g.RenderAsSky && General.Settings.GZDrawSky)", source, StringComparison.Ordinal);
        Assert.Contains("skygeo.Add(g);", source, StringComparison.Ordinal);
        Assert.Contains("case RenderPass.Solid:", source, StringComparison.Ordinal);
        Assert.Contains("solidgeo[g.Texture].Add(g);", source, StringComparison.Ordinal);
        Assert.Contains("case RenderPass.Mask:", source, StringComparison.Ordinal);
        Assert.Contains("maskedgeo[g.Texture].Add(g);", source, StringComparison.Ordinal);
        Assert.Contains("translucentgeo.Add(g);", source, StringComparison.Ordinal);
        Assert.Contains("throw new NotImplementedException(\"Geometry rendering of \" + g.RenderPass + \" render pass is not implemented!\");", source, StringComparison.Ordinal);
        Assert.Contains("public void AddThingGeometry(VisualThing t)", source, StringComparison.Ordinal);
        Assert.Contains("t.Update();", source, StringComparison.Ordinal);
        Assert.Contains("if (General.Settings.GZDrawLightsMode != LightRenderMode.NONE && !fullbrightness && t.LightType != null)", source, StringComparison.Ordinal);
        Assert.Contains("t.UpdateLightRadius();", source, StringComparison.Ordinal);
        Assert.Contains("if (t.LightRadius > 0)", source, StringComparison.Ordinal);
        Assert.Contains("if (t.LightType != null && t.LightType.LightAnimated)", source, StringComparison.Ordinal);
        Assert.Contains("lightthings.Add(t);", source, StringComparison.Ordinal);
        Assert.Contains("(t.Thing.RenderMode == ThingRenderMode.MODEL || t.Thing.RenderMode == ThingRenderMode.VOXEL)", source, StringComparison.Ordinal);
        Assert.Contains("General.Settings.GZDrawModelsMode == ModelRenderMode.SELECTION && t.Selected", source, StringComparison.Ordinal);
        Assert.Contains("(t.RenderPass == RenderPass.Alpha && (t.VertexColor & 0xFF000000) == 0xFF000000)", source, StringComparison.Ordinal);
        Assert.Contains("maskedmodelthings[mde].Add(t);", source, StringComparison.Ordinal);
        Assert.Contains("translucentmodelthings.Add(t);", source, StringComparison.Ordinal);
        Assert.Contains("throw new NotImplementedException(\"Thing model rendering of \" + t.RenderPass + \" render pass is not implemented!\");", source, StringComparison.Ordinal);
        Assert.Contains("t.UpdateSpriteFrame();", source, StringComparison.Ordinal);
        Assert.Contains("if(t.Texture != null)", source, StringComparison.Ordinal);
        Assert.Contains("solidthings[t.Texture].Add(t);", source, StringComparison.Ordinal);
        Assert.Contains("maskedthings[t.Texture].Add(t);", source, StringComparison.Ordinal);
        Assert.Contains("translucentthings.Add(t);", source, StringComparison.Ordinal);
        Assert.Contains("throw new NotImplementedException(\"Thing rendering of \" + t.RenderPass + \" render pass is not implemented!\");", source, StringComparison.Ordinal);
        Assert.Contains("allthings.Add(t);", source, StringComparison.Ordinal);
        Assert.Contains("private static bool BoundingBoxesIntersect(Vector3D[] bbox1, Vector3D[] bbox2)", source, StringComparison.Ordinal);
        Assert.Contains("Vector3D dist = bbox1[0] - bbox2[0];", source, StringComparison.Ordinal);
        Assert.Contains("Vector3D halfSize1 = bbox1[0] - bbox1[1];", source, StringComparison.Ordinal);
        Assert.Contains("Vector3D halfSize2 = bbox2[0] - bbox2[1];", source, StringComparison.Ordinal);
        Assert.Contains("halfSize1.x + halfSize2.x >= Math.Abs(dist.x)", source, StringComparison.Ordinal);
        Assert.Contains("private void RenderSky(IEnumerable<VisualGeometry> geo)", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetShader(ShaderName.world3d_skybox);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetTexture(General.Map.Data.SkyBox);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.campos, new Vector4f((float)cameraposition.x, (float)cameraposition.y, (float)cameraposition.z, 0f));", source, StringComparison.Ordinal);
        Assert.Contains("if(!object.ReferenceEquals(g.Sector, sector))", source, StringComparison.Ordinal);
        Assert.Contains("if(g.Sector.NeedsUpdateGeo) g.Sector.Update(graphics);", source, StringComparison.Ordinal);
        Assert.Contains("if(g.Sector.GeometryBuffer != null && g.Sector.Sector.Map != null)", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetVertexBuffer(sector.GeometryBuffer);", source, StringComparison.Ordinal);
        Assert.Contains("sector = null;", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.highlightcolor, CalculateHighlightColor((g == highlighted) && showhighlight, (g.Selected && showselection)));", source, StringComparison.Ordinal);
        Assert.Contains("graphics.Draw(PrimitiveType.TriangleList, g.VertexOffset, g.Triangles);", source, StringComparison.Ordinal);
        Assert.Contains("RenderSinglePass(solidgeo, solidthings, lightthings);", source, StringComparison.Ordinal);
        Assert.Contains("private void RenderSinglePass(Dictionary<ImageData, List<VisualGeometry>> geopass, Dictionary<ImageData, List<VisualThing>> thingspass, List<VisualThing> lights)", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.lightsEnabled, lights.Count > 0);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.ignoreNormals, false);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.useLightStrength, General.Map.Data.MapInfo.LightAttenuationMode == \"InverseSquare\");", source, StringComparison.Ordinal);
        Assert.Contains("if (BoundingBoxesIntersect(g.BoundingBox, light.BoundingBox))", source, StringComparison.Ordinal);
        Assert.Contains("lightColor[lightIndex] = light.LightColor.ToVector();", source, StringComparison.Ordinal);
        Assert.Contains("lightPosAndRadius[lightIndex] = new Vector4f(light.Center, light.LightRadius);", source, StringComparison.Ordinal);
        Assert.Contains("lightOrientation[lightIndex] = new Vector4f(light.VectorLookAt, 1f);", source, StringComparison.Ordinal);
        Assert.Contains("light2Radius[lightIndex] = new Vector2f(CosDeg(light.LightSpotRadius1), CosDeg(light.LightSpotRadius2));", source, StringComparison.Ordinal);
        Assert.Contains("lightStrengthAndLinearity[lightIndex] = new Vector2f(Math.Min(1500.0f, (diameter * diameter) / 10), light.LightLinearity);", source, StringComparison.Ordinal);
        Assert.Contains("if (lightIndex >= lightColor.Length)", source, StringComparison.Ordinal);
        Assert.Contains("for (int i = lightIndex; i < lightColor.Length; i++)", source, StringComparison.Ordinal);
        Assert.Contains("lightColor[i].W = 0;", source, StringComparison.Ordinal);
        Assert.Contains("if (havelights != hadlights || havelights)", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.lightColor, lightColor);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.lightPosAndRadius, lightPosAndRadius);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.lightOrientation, lightOrientation);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.light2Radius, light2Radius);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.lightStrengthAndLinearity, lightStrengthAndLinearity);", source, StringComparison.Ordinal);
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
        Assert.Contains("private void RenderArrows(ICollection<Line3D> lines)", source, StringComparison.Ordinal);
        Assert.Contains("if(lines.Count == 0) return;", source, StringComparison.Ordinal);
        Assert.Contains("pointscount += (line.RenderArrowhead ? 6 : 2);", source, StringComparison.Ordinal);
        Assert.Contains("const float scaler = 20f;", source, StringComparison.Ordinal);
        Assert.Contains("double nz = line.GetDelta().GetNormal().z * scaler;", source, StringComparison.Ordinal);
        Assert.Contains("Vector3D a1 = new Vector3D(line.End.x - scaler * Math.Sin(angle - 0.46f), line.End.y + scaler * Math.Cos(angle - 0.46f), line.End.z - nz);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetShader(ShaderName.world3d_vertex_color);", source, StringComparison.Ordinal);
        Assert.Contains("world = Matrix.Identity;", source, StringComparison.Ordinal);
        Assert.Contains("graphics.Draw(PrimitiveType.LineList, 0, pointscount / 2);", source, StringComparison.Ordinal);
        Assert.Contains("vb.Dispose();", source, StringComparison.Ordinal);
        Assert.Contains("ShaderName highshaderpass = (ShaderName)(shaderpass + 2);", source, StringComparison.Ordinal);
        Assert.Contains("ShaderName wantedshaderpass = (((g == highlighted) && showhighlight) || (g.Selected && showselection)) ? highshaderpass : shaderpass;", source, StringComparison.Ordinal);
        Assert.Contains("if(General.Settings.GZDrawFog && !fullbrightness && !General.Settings.ClassicRendering && sector.Sector.FogMode != SectorFogMode.NONE)", source, StringComparison.Ordinal);
        Assert.Contains("wantedshaderpass += 8;", source, StringComparison.Ordinal);
        Assert.Contains("if(wantedshaderpass > ShaderName.world3d_p7)", source, StringComparison.Ordinal);
        Assert.Contains("ShaderName wantedshaderpass = (((t == highlighted) && showhighlight) || (t.Selected && showselection)) ? highshaderpass : shaderpass;", source, StringComparison.Ordinal);
        Assert.Contains("if(General.Settings.GZDrawFog && !fullbrightness && !General.Settings.ClassicRendering && t.Thing.Sector != null && t.Thing.Sector.FogMode != SectorFogMode.NONE)", source, StringComparison.Ordinal);
        Assert.Contains("if(t.LightType != null && t.LightType.LightInternal && t.LightType.LightType != GZGeneral.LightType.SUN && !fullbrightness && !General.Settings.ClassicRendering)", source, StringComparison.Ordinal);
        Assert.Contains("wantedshaderpass += 4; // Render using one of passes, which uses World3D.VertexColor", source, StringComparison.Ordinal);
        Assert.Contains("else if(General.Settings.GZDrawLightsMode != LightRenderMode.NONE && !fullbrightness && !General.Settings.ClassicRendering && lightthings.Count > 0)", source, StringComparison.Ordinal);
        Assert.Contains("if(litcolor.ToArgb() != 0)", source, StringComparison.Ordinal);
        Assert.Contains("private Color4 GetLitColorForThing(VisualThing t)", source, StringComparison.Ordinal);
        Assert.Contains("if(General.Map.Data.GldefsEntries.ContainsKey(t.Thing.Type) && General.Map.Data.GldefsEntries[t.Thing.Type].DontLightSelf && t.Thing.Index == lt.Thing.Index)", source, StringComparison.Ordinal);
        Assert.Contains("float distSquared = Vector3f.DistanceSquared(lt.Center, t.Center);", source, StringComparison.Ordinal);
        Assert.Contains("if(distSquared < radiusSquared)", source, StringComparison.Ordinal);
        Assert.Contains("int sign = (lt.LightType.LightRenderStyle == GZGeneral.LightRenderStyle.SUBTRACTIVE ? -1 : 1);", source, StringComparison.Ordinal);
        Assert.Contains("attn = InverseSquareDistanceAttenuation(Math.Max(dist, (float)Math.Sqrt(lt.LightRadius) * 2), diameter, Math.Min(1500.0f, (diameter * diameter) / 10), lt.LightLinearity);", source, StringComparison.Ordinal);
        Assert.Contains("attn = 1 - dist / lt.LightRadius;", source, StringComparison.Ordinal);
        Assert.Contains("scaler *= (float)Smoothstep(CosDeg(lt.LightSpotRadius2), CosDeg(lt.LightSpotRadius1), cosDir);", source, StringComparison.Ordinal);
        Assert.Contains("litColor.Red += lt.LightColor.Red * scaler * sign;", source, StringComparison.Ordinal);
        Assert.Contains("private void RenderTranslucentPass(List<VisualGeometry> geopass, List<VisualThing> thingspass, List<VisualThing> lights)", source, StringComparison.Ordinal);
        Assert.Contains("geopass.Sort(delegate(VisualGeometry vg1, VisualGeometry vg2)", source, StringComparison.Ordinal);
        Assert.Contains("vg1.GeometryType == VisualGeometryType.FLOOR || vg1.GeometryType == VisualGeometryType.CEILING", source, StringComparison.Ordinal);
        Assert.Contains("dist1 = Math.Abs(vg1.BoundingBox[0].z - cameraPos.z);", source, StringComparison.Ordinal);
        Assert.Contains("dist1 = (General.Map.VisualCamera.Position - vg1.BoundingBox[0]).GetLengthSq();", source, StringComparison.Ordinal);
        Assert.Contains("return (int)(dist2 - dist1);", source, StringComparison.Ordinal);
        Assert.Contains("case RenderPass.Additive:", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetDestinationBlend(Blend.One);", source, StringComparison.Ordinal);
        Assert.Contains("case RenderPass.Alpha:", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetSamplerState(TextureAddress.Clamp);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetCullMode(Cull.None); //mxd. Disable backside culling", source, StringComparison.Ordinal);
        Assert.Contains("thingspass.Sort(delegate(VisualThing vt1, VisualThing vt2)", source, StringComparison.Ordinal);
        Assert.Contains("return (int)((General.Map.VisualCamera.Position - vt2.BoundingBox[0]).GetLengthSq()", source, StringComparison.Ordinal);
        Assert.Contains("if(t.RenderPass != currentpass)", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetSamplerState(TextureAddress.Wrap);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetCullMode(Cull.Clockwise); //mxd", source, StringComparison.Ordinal);
        Assert.Contains("private Matrix CreateThingPositionMatrix(VisualThing t)", source, StringComparison.Ordinal);
        Assert.Contains("ThingRenderMode rendermode = t.Thing.RenderMode;", source, StringComparison.Ordinal);
        Assert.Contains("if((t.Thing.RenderMode == ThingRenderMode.MODEL || t.Thing.RenderMode == ThingRenderMode.VOXEL) &&", source, StringComparison.Ordinal);
        Assert.Contains("General.Settings.GZDrawModelsMode == ModelRenderMode.NONE ||", source, StringComparison.Ordinal);
        Assert.Contains("(General.Settings.GZDrawModelsMode == ModelRenderMode.SELECTION && !t.Selected)))", source, StringComparison.Ordinal);
        Assert.Contains("rendermode = ThingRenderMode.NORMAL;", source, StringComparison.Ordinal);
        Assert.Contains("case ThingRenderMode.NORMAL:", source, StringComparison.Ordinal);
        Assert.Contains("if(t.Info.XYBillboard) // Apply billboarding?", source, StringComparison.Ordinal);
        Assert.Contains("case ThingRenderMode.FLATSPRITE:", source, StringComparison.Ordinal);
        Assert.Contains("case ThingRenderMode.WALLSPRITE:", source, StringComparison.Ordinal);
        Assert.Contains("case ThingRenderMode.MODEL:", source, StringComparison.Ordinal);
        Assert.Contains("case ThingRenderMode.VOXEL:", source, StringComparison.Ordinal);
        Assert.Contains("private void RenderModels(bool trans, List<VisualThing> lights)", source, StringComparison.Ordinal);
        Assert.Contains("ShaderName shaderpass = (fullbrightness ? ShaderName.world3d_fullbright : ShaderName.world3d_main_vertexcolor);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.lightsEnabled, lights.Count > 0);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.ignoreNormals, true);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.useLightStrength, General.Map.Data.MapInfo.LightAttenuationMode == \"InverseSquare\");", source, StringComparison.Ordinal);
        Assert.Contains("translucentmodelthings.Sort((vt1, vt2) => (int)((General.Map.VisualCamera.Position - vt2.BoundingBox[0]).GetLengthSq()", source, StringComparison.Ordinal);
        Assert.Contains("foreach (KeyValuePair<ModelData, List<VisualThing>> group in maskedmodelthings)", source, StringComparison.Ordinal);
        Assert.Contains("if (t.RenderPass != currentpass)", source, StringComparison.Ordinal);
        Assert.Contains("t.Update();", source, StringComparison.Ordinal);
        Assert.Contains("if (t.Info.DistanceCheckSq < double.MaxValue)", source, StringComparison.Ordinal);
        Assert.Contains("if (t.CameraDistance > t.Info.DistanceCheckSq)", source, StringComparison.Ordinal);
        Assert.Contains("continue;", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.vertexColor, vertexcolor);", source, StringComparison.Ordinal);
        Assert.Contains("ShaderName wantedshaderpass = ((((t == highlighted) && showhighlight) || (t.Selected && showselection)) ? highshaderpass : shaderpass);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.highlightcolor, CalculateHighlightColor((t == highlighted) && showhighlight, (t.Selected && showselection)));", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.desaturation, (float)t.Thing.Sector.Desaturation);", source, StringComparison.Ordinal);
        Assert.Contains("foreach (VisualThing light in lights)", source, StringComparison.Ordinal);
        Assert.Contains("if (BoundingBoxesIntersect(t.BoundingBox, light.BoundingBox))", source, StringComparison.Ordinal);
        Assert.Contains("lightColor[lightIndex] = light.LightColor.ToVector();", source, StringComparison.Ordinal);
        Assert.Contains("lightPosAndRadius[lightIndex] = new Vector4f(light.Center, light.LightRadius);", source, StringComparison.Ordinal);
        Assert.Contains("if (light.LightType.LightType == GZGeneral.LightType.SPOT)", source, StringComparison.Ordinal);
        Assert.Contains("light2Radius[lightIndex] = new Vector2f(CosDeg(light.LightSpotRadius1), CosDeg(light.LightSpotRadius2));", source, StringComparison.Ordinal);
        Assert.Contains("lightStrengthAndLinearity[lightIndex] = new Vector2f(Math.Min(1500.0f, (diameter * diameter) / 10), light.LightLinearity);", source, StringComparison.Ordinal);
        Assert.Contains("if (lightIndex >= lightColor.Length)", source, StringComparison.Ordinal);
        Assert.Contains("bool havelights = (lightIndex > 0);", source, StringComparison.Ordinal);
        Assert.Contains("if (hadlights != havelights || havelights)", source, StringComparison.Ordinal);
        Assert.Contains("GZModel model = General.Map.Data.ModeldefEntries[t.Thing.Type].Model;", source, StringComparison.Ordinal);
        Assert.Contains("for (int j = 0; j < model.Meshes.Count; j++)", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetTexture(model.Textures[j]);", source, StringComparison.Ordinal);
        Assert.Contains("model.Meshes[j].Draw(graphics);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.lightsEnabled, false);", source, StringComparison.Ordinal);
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

    private static void AssertWorldVertex(WorldVertex vertex, float x, float y, float z, int color)
    {
        Assert.Equal(x, vertex.x, precision: 5);
        Assert.Equal(y, vertex.y, precision: 5);
        Assert.Equal(z, vertex.z, precision: 5);
        Assert.Equal(color, vertex.c);
        Assert.Equal(0.0f, vertex.u);
        Assert.Equal(0.0f, vertex.v);
    }

    private static void AssertColor(Color4 expected, Color4 actual)
    {
        Assert.Equal(expected.Red, actual.Red, precision: 5);
        Assert.Equal(expected.Green, actual.Green, precision: 5);
        Assert.Equal(expected.Blue, actual.Blue, precision: 5);
        Assert.Equal(expected.Alpha, actual.Alpha, precision: 5);
    }
}
