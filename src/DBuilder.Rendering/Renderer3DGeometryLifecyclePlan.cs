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
