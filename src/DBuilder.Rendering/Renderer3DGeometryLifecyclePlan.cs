// ABOUTME: Plans UDB-style Renderer3D world-geometry collection lifecycle state.
// ABOUTME: Keeps StartGeometry render buckets explicit before live 3D rendering is complete.

namespace DBuilder.Rendering;

public enum Renderer3DGeometryBucketKind
{
    SolidGeometry,
    MaskedGeometry,
    TranslucentGeometry,
    SkyGeometry,
    SolidThings,
    MaskedThings,
    TranslucentThings,
    MaskedModelThings,
    TranslucentModelThings,
    LightThings,
    AllThings,
    VisualVertices,
}

public enum Renderer3DGeometryCollectionKind
{
    ImageDictionary,
    ModelDictionary,
    List,
}

public sealed record Renderer3DGeometryBucketPlan(
    Renderer3DGeometryBucketKind Kind,
    Renderer3DGeometryCollectionKind CollectionKind,
    bool InitializedEmpty);

public sealed record Renderer3DStartGeometryPlan(IReadOnlyList<Renderer3DGeometryBucketPlan> Buckets)
{
    public bool InitializesAllBuckets => Buckets.All(bucket => bucket.InitializedEmpty);
}

public sealed record Renderer3DFinishGeometryCleanupPlan(
    bool UnbindTexture,
    IReadOnlyList<Renderer3DGeometryBucketKind> ClearedBuckets);

public sealed record Renderer3DFinishGeometryInitialStatePlan(
    Cull CullMode,
    bool DepthEnabled,
    bool DepthWriteEnabled,
    bool AlphaBlendEnabled,
    bool AlphaTestEnabled);

public enum Renderer3DGeometryPassOperationKind
{
    SetIdentityWorld,
    SetWorldUniform,
    RenderSky,
    RenderSinglePass,
    SetAlphaTest,
    SetAlphaBlend,
    SetZWrite,
    SetSourceBlend,
    SetCullMode,
    RenderModels,
    RenderTranslucentPass,
    RenderThingCages,
}

public sealed record Renderer3DGeometryPassOperation(
    Renderer3DGeometryPassOperationKind Kind,
    Renderer3DGeometryBucketKind? GeometryBucket = null,
    Renderer3DGeometryBucketKind? ThingBucket = null,
    Renderer3DGeometryBucketKind? LightBucket = null,
    Renderer3DGeometryBucketKind? ModelBucket = null,
    bool? Enabled = null,
    Cull? CullMode = null,
    Blend? SourceBlend = null,
    bool? TranslucentModels = null);

public sealed record Renderer3DSkySolidPassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations);

public sealed record Renderer3DModelPassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations)
{
    public bool ShouldRender => Operations.Count > 0;
}

public sealed record Renderer3DMaskPassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations)
{
    public bool ShouldRender => Operations.Count > 0;
}

public sealed record Renderer3DTranslucentPassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations)
{
    public bool ShouldRender => Operations.Count > 0;
}

public sealed record Renderer3DThingCagePassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations)
{
    public bool ShouldRender => Operations.Count > 0;
}

public static class Renderer3DGeometryLifecyclePlan
{
    public static Renderer3DStartGeometryPlan BuildStartGeometryPlan()
        => new(
            [
                new(Renderer3DGeometryBucketKind.SolidGeometry, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.MaskedGeometry, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.TranslucentGeometry, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.SkyGeometry, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.SolidThings, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.MaskedThings, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.TranslucentThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.MaskedModelThings, Renderer3DGeometryCollectionKind.ModelDictionary, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.TranslucentModelThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.LightThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.AllThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
            ]);

    public static Renderer3DFinishGeometryInitialStatePlan BuildFinishGeometryInitialStatePlan()
        => new(
            Cull.Clockwise,
            DepthEnabled: true,
            DepthWriteEnabled: true,
            AlphaBlendEnabled: false,
            AlphaTestEnabled: false);

    public static Renderer3DSkySolidPassPlan BuildSkySolidPassPlan(int skyGeometryCount)
    {
        if (skyGeometryCount < 0) throw new ArgumentOutOfRangeException(nameof(skyGeometryCount));

        var operations = new List<Renderer3DGeometryPassOperation>();
        if (skyGeometryCount > 0)
        {
            operations.Add(new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetIdentityWorld));
            operations.Add(new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetWorldUniform));
            operations.Add(new Renderer3DGeometryPassOperation(
                Renderer3DGeometryPassOperationKind.RenderSky,
                GeometryBucket: Renderer3DGeometryBucketKind.SkyGeometry));
        }

        operations.Add(new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetIdentityWorld));
        operations.Add(new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetWorldUniform));
        operations.Add(new Renderer3DGeometryPassOperation(
            Renderer3DGeometryPassOperationKind.RenderSinglePass,
            GeometryBucket: Renderer3DGeometryBucketKind.SolidGeometry,
            ThingBucket: Renderer3DGeometryBucketKind.SolidThings,
            LightBucket: Renderer3DGeometryBucketKind.LightThings));

        return new Renderer3DSkySolidPassPlan(operations);
    }

    public static Renderer3DModelPassPlan BuildMaskedModelPassPlan(int maskedModelThingCount)
    {
        if (maskedModelThingCount < 0) throw new ArgumentOutOfRangeException(nameof(maskedModelThingCount));
        if (maskedModelThingCount == 0) return new Renderer3DModelPassPlan([]);

        return new Renderer3DModelPassPlan(
            [
                new(Renderer3DGeometryPassOperationKind.SetAlphaTest, Enabled: true),
                new(Renderer3DGeometryPassOperationKind.SetCullMode, CullMode: Cull.None),
                new(
                    Renderer3DGeometryPassOperationKind.RenderModels,
                    LightBucket: Renderer3DGeometryBucketKind.LightThings,
                    ModelBucket: Renderer3DGeometryBucketKind.MaskedModelThings,
                    TranslucentModels: false),
                new(Renderer3DGeometryPassOperationKind.SetCullMode, CullMode: Cull.Clockwise),
            ]);
    }

    public static Renderer3DMaskPassPlan BuildMaskPassPlan(int maskedGeometryCount, int maskedThingCount)
    {
        if (maskedGeometryCount < 0) throw new ArgumentOutOfRangeException(nameof(maskedGeometryCount));
        if (maskedThingCount < 0) throw new ArgumentOutOfRangeException(nameof(maskedThingCount));
        if (maskedGeometryCount == 0 && maskedThingCount == 0) return new Renderer3DMaskPassPlan([]);

        return new Renderer3DMaskPassPlan(
            [
                new(Renderer3DGeometryPassOperationKind.SetIdentityWorld),
                new(Renderer3DGeometryPassOperationKind.SetWorldUniform),
                new(Renderer3DGeometryPassOperationKind.SetAlphaTest, Enabled: true),
                new(
                    Renderer3DGeometryPassOperationKind.RenderSinglePass,
                    GeometryBucket: Renderer3DGeometryBucketKind.MaskedGeometry,
                    ThingBucket: Renderer3DGeometryBucketKind.MaskedThings,
                    LightBucket: Renderer3DGeometryBucketKind.LightThings),
            ]);
    }

    public static Renderer3DTranslucentPassPlan BuildTranslucentPassPlan(int translucentGeometryCount, int translucentThingCount)
    {
        if (translucentGeometryCount < 0) throw new ArgumentOutOfRangeException(nameof(translucentGeometryCount));
        if (translucentThingCount < 0) throw new ArgumentOutOfRangeException(nameof(translucentThingCount));
        if (translucentGeometryCount == 0 && translucentThingCount == 0) return new Renderer3DTranslucentPassPlan([]);

        return new Renderer3DTranslucentPassPlan(
            [
                new(Renderer3DGeometryPassOperationKind.SetIdentityWorld),
                new(Renderer3DGeometryPassOperationKind.SetWorldUniform),
                new(Renderer3DGeometryPassOperationKind.SetAlphaBlend, Enabled: true),
                new(Renderer3DGeometryPassOperationKind.SetAlphaTest, Enabled: false),
                new(Renderer3DGeometryPassOperationKind.SetZWrite, Enabled: false),
                new(Renderer3DGeometryPassOperationKind.SetSourceBlend, SourceBlend: Blend.SourceAlpha),
                new(
                    Renderer3DGeometryPassOperationKind.RenderTranslucentPass,
                    GeometryBucket: Renderer3DGeometryBucketKind.TranslucentGeometry,
                    ThingBucket: Renderer3DGeometryBucketKind.TranslucentThings,
                    LightBucket: Renderer3DGeometryBucketKind.LightThings),
            ]);
    }

    public static Renderer3DModelPassPlan BuildTranslucentModelPassPlan(int translucentModelThingCount)
    {
        if (translucentModelThingCount < 0) throw new ArgumentOutOfRangeException(nameof(translucentModelThingCount));
        if (translucentModelThingCount == 0) return new Renderer3DModelPassPlan([]);

        return new Renderer3DModelPassPlan(
            [
                new(Renderer3DGeometryPassOperationKind.SetAlphaBlend, Enabled: true),
                new(Renderer3DGeometryPassOperationKind.SetAlphaTest, Enabled: false),
                new(Renderer3DGeometryPassOperationKind.SetZWrite, Enabled: false),
                new(Renderer3DGeometryPassOperationKind.SetSourceBlend, SourceBlend: Blend.SourceAlpha),
                new(
                    Renderer3DGeometryPassOperationKind.RenderModels,
                    LightBucket: Renderer3DGeometryBucketKind.LightThings,
                    ModelBucket: Renderer3DGeometryBucketKind.TranslucentModelThings,
                    TranslucentModels: true),
            ]);
    }

    public static Renderer3DThingCagePassPlan BuildThingCagePassPlan(bool renderThingCages)
        => renderThingCages
            ? new Renderer3DThingCagePassPlan(
                [
                    new(Renderer3DGeometryPassOperationKind.SetIdentityWorld),
                    new(Renderer3DGeometryPassOperationKind.SetWorldUniform),
                    new(Renderer3DGeometryPassOperationKind.RenderThingCages),
                ])
            : new Renderer3DThingCagePassPlan([]);

    public static Renderer3DFinishGeometryCleanupPlan BuildFinishGeometryCleanupPlan()
        => new(
            UnbindTexture: true,
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
            ]);
}
