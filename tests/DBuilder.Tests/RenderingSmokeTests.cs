// ABOUTME: Headless smoke coverage for representative 2D and 3D rendering plans.
// ABOUTME: Verifies minimal editor-view geometry reaches drawable UDB-style render buckets.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class RenderingSmokeTests
{
    [Fact]
    public void TwoDimensionalGeometrySmokeBuildsDrawableTrianglePlan()
    {
        FlatVertex[] vertices =
        [
            Vertex(0, 0, 0x00FF00),
            Vertex(64, 0, 0x00FF00),
            Vertex(0, 64, 0x00FF00),
        ];

        Renderer2DGeometryDrawPlan plan = Renderer2DGeometryDrawPlanner.BuildDrawPlan(
            vertices,
            hasTexture: false,
            transformCoordinates: true);

        Assert.True(plan.ShouldDraw);
        Assert.Equal(1, plan.PrimitiveCount);
        Assert.Equal(PrimitiveType.TriangleList, plan.PrimitiveType);
        Assert.Equal(ShaderName.display2d_normal, plan.Shader);
        Assert.True(plan.BindWhiteTexture);
        Assert.True(plan.UseWorldTransformation);
    }

    [Fact]
    public void ThreeDimensionalGeometrySmokeRoutesSolidMaskedAndSkyPasses()
    {
        Renderer3DStartGeometryPlan start = Renderer3DGeometryLifecyclePlan.BuildStartGeometryPlan();
        Renderer3DFinishGeometryInitialStatePlan state = Renderer3DGeometryLifecyclePlan.BuildFinishGeometryInitialStatePlan();
        Renderer3DSkySolidPassPlan pass = Renderer3DGeometryLifecyclePlan.BuildSkySolidPassPlan(skyGeometryCount: 1);

        Assert.True(start.InitializesAllBuckets);
        Assert.Contains(start.Buckets, bucket => bucket.Kind == Renderer3DGeometryBucketKind.SolidGeometry);
        Assert.Contains(start.Buckets, bucket => bucket.Kind == Renderer3DGeometryBucketKind.MaskedGeometry);
        Assert.Contains(start.Buckets, bucket => bucket.Kind == Renderer3DGeometryBucketKind.SkyGeometry);
        Assert.Equal(Cull.Clockwise, state.CullMode);
        Assert.True(state.DepthEnabled);
        Assert.Contains(pass.Operations, operation => operation.Kind == Renderer3DGeometryPassOperationKind.RenderSky);
        Assert.Contains(pass.Operations, operation => operation.Kind == Renderer3DGeometryPassOperationKind.RenderSinglePass);
    }

    [Fact]
    public void ThreeDimensionalThingSmokeRoutesSpriteLightAndAllThingBuckets()
    {
        Renderer3DThingGeometryCollectionPlan plan = Renderer3DGeometryLifecyclePlan.BuildThingGeometryCollectionPlan(
            new Renderer3DThingGeometryCandidate(
                Id: 1,
                ThingRenderMode.NORMAL,
                ModelRenderMode.NONE,
                RenderPass.Mask,
                HasTexture: true,
                Selected: false,
                FullBrightness: false,
                LightRenderMode.ALL,
                HasLightType: true,
                LightAnimated: true,
                LightRadius: 128,
                VertexColorOpaque: true));

        Assert.True(plan.UpdateThing);
        Assert.True(plan.UpdateLightRadius);
        Assert.Equal(
            [
                Renderer3DGeometryBucketKind.LightThings,
                Renderer3DGeometryBucketKind.MaskedThings,
                Renderer3DGeometryBucketKind.AllThings,
            ],
            plan.Buckets);
        Assert.Null(plan.UnsupportedRenderPassMessage);
    }

    private static FlatVertex Vertex(float x, float y, int color)
        => new()
        {
            x = x,
            y = y,
            c = color,
        };
}
