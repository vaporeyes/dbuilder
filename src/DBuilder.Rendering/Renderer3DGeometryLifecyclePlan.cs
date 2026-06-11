// ABOUTME: Plans UDB-style Renderer3D world-geometry collection lifecycle state.
// ABOUTME: Keeps StartGeometry render buckets explicit before live 3D rendering is complete.

using DBuilder.Geometry;

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
    RenderVertices,
    RenderSlopeHandles,
    RenderArrows,
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

public sealed record Renderer3DSectorGeometryCandidate(
    int Id,
    bool HasTexture,
    int Triangles,
    bool RenderAsSky,
    RenderPass RenderPass);

public sealed record Renderer3DSectorGeometryCollectionPlan(
    int GeometryId,
    bool Accepted,
    Renderer3DGeometryBucketKind? Bucket,
    string? UnsupportedRenderPassMessage);

public sealed record Renderer3DThingGeometryCandidate(
    int Id,
    ThingRenderMode RenderMode,
    ModelRenderMode ModelRenderMode,
    RenderPass RenderPass,
    bool HasTexture,
    bool Selected,
    bool FullBrightness,
    LightRenderMode LightRenderMode,
    bool HasLightType,
    bool LightAnimated,
    double LightRadius,
    bool VertexColorOpaque);

public sealed record Renderer3DThingGeometryCollectionPlan(
    int ThingId,
    bool UpdateThing,
    bool UpdateLightRadius,
    bool UpdateBoundingBox,
    bool UpdateSpriteFrame,
    IReadOnlyList<Renderer3DGeometryBucketKind> Buckets,
    string? UnsupportedRenderPassMessage);

public sealed record Renderer3DSkyGeometryCandidate(
    int Id,
    int SectorId,
    bool SectorNeedsUpdate,
    bool SectorHasGeometryBuffer,
    bool SectorHasMap,
    bool Highlighted,
    bool Selected,
    int VertexOffset,
    int Triangles);

public sealed record Renderer3DSkyGeometryDrawPlan(
    int GeometryId,
    int SectorId,
    bool UpdateSectorGeometry,
    bool BindSectorGeometryBuffer,
    bool ClearCurrentSector,
    bool SetHighlightColor,
    bool Draw,
    PrimitiveType PrimitiveType,
    int VertexOffset,
    int Triangles);

public sealed record Renderer3DSkyRenderPlan(
    ShaderName Shader,
    bool SetSkyTexture,
    bool SetCameraUniform,
    IReadOnlyList<Renderer3DSkyGeometryDrawPlan> Draws);

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

public sealed record Renderer3DVisualVerticesPassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations);

public sealed record Renderer3DSlopeHandlesPassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations)
{
    public bool ShouldRender => Operations.Count > 0;
}

public sealed record Renderer3DEventLinesPassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations)
{
    public bool ShouldRender => Operations.Count > 0;
}

public enum Renderer3DFpsUpdateOperationKind
{
    IncrementFrameCounter,
    SetFpsLabelText,
    ResetFrameCounter,
    RestartStopwatch,
}

public sealed record Renderer3DFpsUpdateOperation(
    Renderer3DFpsUpdateOperationKind Kind,
    int? FrameCount = null,
    string? LabelText = null);

public sealed record Renderer3DFpsUpdatePlan(IReadOnlyList<Renderer3DFpsUpdateOperation> Operations)
{
    public bool ShouldUpdate => Operations.Count > 0;
}

public enum Renderer3DDynamicLightRenderStyle
{
    Normal,
    Vavoom,
    Attenuated,
    Additive,
    Subtractive,
    Lightmap,
}

public sealed record Renderer3DDynamicLightCandidate(
    int Id,
    double CameraDistance,
    bool Visible,
    Renderer3DDynamicLightRenderStyle RenderStyle);

public sealed record Renderer3DDynamicLightUpdatePlan(
    IReadOnlyList<int> SelectedLightIds,
    IReadOnlyList<int> LightOffsets);

public enum Renderer3DThingCageRenderOperationKind
{
    SetAlphaBlend,
    SetAlphaTest,
    SetZWrite,
    SetSourceBlend,
    SetDestinationBlend,
    SetShader,
}

public sealed record Renderer3DThingCageRenderOperation(
    Renderer3DThingCageRenderOperationKind Kind,
    bool? Enabled = null,
    Blend? Blend = null,
    ShaderName? Shader = null);

public sealed record Renderer3DThingCageCandidate(
    int Id,
    int CageColor,
    bool Selected,
    bool Highlighted,
    int CageLength);

public sealed record Renderer3DThingCageDrawPlan(
    int ThingId,
    int Color,
    float Alpha,
    PrimitiveType PrimitiveType,
    int StartIndex,
    int PrimitiveCount);

public sealed record Renderer3DThingCageRenderPlan(
    IReadOnlyList<Renderer3DThingCageRenderOperation> StateOperations,
    IReadOnlyList<Renderer3DThingCageDrawPlan> Draws);

public enum Renderer3DVisualVertexHandleKind
{
    Lower,
    Upper,
}

public sealed record Renderer3DVisualVertexCandidate(
    int Id,
    bool Selected,
    bool Highlighted,
    bool HaveHeightOffset,
    bool CeilingVertex);

public sealed record Renderer3DVisualVertexDrawPlan(
    int VertexId,
    int Color,
    float Alpha,
    Renderer3DVisualVertexHandleKind Handle,
    PrimitiveType PrimitiveType,
    int StartIndex,
    int PrimitiveCount);

public sealed record Renderer3DVisualVertexRenderPlan(
    IReadOnlyList<Renderer3DThingCageRenderOperation> StateOperations,
    IReadOnlyList<Renderer3DVisualVertexDrawPlan> Draws);

public enum Renderer3DSlopeHandleKind
{
    Line,
    Vertex,
}

public sealed record Renderer3DSlopeHandleCandidate(
    int Id,
    Renderer3DSlopeHandleKind Kind,
    bool Pivot,
    bool Selected,
    bool Highlighted,
    bool SmartPivot,
    double Length);

public sealed record Renderer3DSlopeHandleDrawPlan(
    int HandleId,
    int Color,
    Renderer3DSlopeHandleKind Geometry,
    float HandleLength,
    PrimitiveType PrimitiveType,
    int StartIndex,
    int PrimitiveCount);

public sealed record Renderer3DSlopeHandleRenderPlan(
    IReadOnlyList<Renderer3DThingCageRenderOperation> StateOperations,
    IReadOnlyList<Renderer3DSlopeHandleDrawPlan> Draws);

public sealed record Renderer3DEventLineRenderPlan(
    WorldVertex[] Vertices,
    IReadOnlyList<Renderer3DThingCageRenderOperation> StateOperations,
    bool SetIdentityWorld,
    PrimitiveType PrimitiveType,
    int StartIndex,
    int PrimitiveCount,
    bool DisposeVertexBuffer);

public sealed record Renderer3DShaderPassPlan(
    ShaderName BaseShader,
    ShaderName HighlightShader,
    ShaderName WantedShader,
    bool UsesHighlightShader,
    bool UsesFogShader,
    bool AppliesFogUniforms);

public enum Renderer3DThingVertexColorSource
{
    None,
    InternalLight,
    DynamicLight,
}

public sealed record Renderer3DThingShaderPassPlan(
    ShaderName BaseShader,
    ShaderName HighlightShader,
    ShaderName WantedShader,
    bool UsesHighlightShader,
    bool UsesFogShader,
    Renderer3DThingVertexColorSource VertexColorSource,
    bool AppliesFogUniforms);

public sealed record Renderer3DThingLightingCandidate(
    int Id,
    int ThingIndex,
    Vector3f Center,
    float Radius,
    Color4 Color,
    Renderer3DDynamicLightRenderStyle RenderStyle,
    bool SpotLight,
    Vector3f SpotDirection,
    float SpotRadius1Degrees,
    float SpotRadius2Degrees,
    float Linearity);

public sealed record Renderer3DThingLightContributionPlan(
    int LightId,
    bool SkippedSelfLight,
    bool InsideRadius,
    float Attenuation,
    float SpotScale,
    float ContributionScale,
    Color4 Contribution);

public sealed record Renderer3DThingLitColorPlan(
    Color4 LitColor,
    IReadOnlyList<Renderer3DThingLightContributionPlan> Contributions);

public enum Renderer3DVisualGeometryType
{
    Floor,
    Ceiling,
    WallUpper,
    WallMiddle,
    WallMiddle3D,
    WallLower,
    FogBoundary,
    Unknown,
}

public sealed record Renderer3DTranslucentGeometryCandidate(
    int Id,
    Renderer3DVisualGeometryType GeometryType,
    Vector3D BoundingBoxCenter,
    RenderPass RenderPass);

public sealed record Renderer3DTranslucentGeometryDrawPlan(
    int GeometryId,
    RenderPass RenderPass,
    Blend? DestinationBlendChange);

public sealed record Renderer3DTranslucentGeometryOrderPlan(IReadOnlyList<Renderer3DTranslucentGeometryDrawPlan> Draws);

public sealed record Renderer3DTranslucentThingCandidate(
    int Id,
    Vector3D BoundingBoxCenter,
    RenderPass RenderPass);

public sealed record Renderer3DTranslucentThingDrawPlan(
    int ThingId,
    RenderPass RenderPass,
    Blend? DestinationBlendChange);

public sealed record Renderer3DTranslucentThingOrderPlan(
    TextureAddress InitialTextureAddress,
    Cull InitialCullMode,
    IReadOnlyList<Renderer3DTranslucentThingDrawPlan> Draws,
    TextureAddress RestoredTextureAddress,
    Cull RestoredCullMode);

public sealed record Renderer3DModelThingCandidate(
    int Id,
    Vector3D BoundingBoxCenter,
    RenderPass RenderPass);

public sealed record Renderer3DModelThingGroup(IReadOnlyList<Renderer3DModelThingCandidate> Things);

public sealed record Renderer3DModelThingDrawPlan(
    int ThingId,
    RenderPass RenderPass,
    Blend? DestinationBlendChange);

public sealed record Renderer3DModelRenderPlan(
    ShaderName InitialShader,
    ShaderName HighlightShader,
    bool LightsEnabled,
    bool IgnoreNormals,
    bool UseLightStrength,
    IReadOnlyList<Renderer3DModelThingDrawPlan> Draws);

public sealed record Renderer3DModelThingDrawStateCandidate(
    int Id,
    Vector3D BoundingBoxCenter,
    double DistanceCheckSq,
    bool Highlighted,
    bool Selected,
    bool SectorHasFog,
    double SectorDesaturation);

public sealed record Renderer3DModelThingDrawStatePlan(
    int ThingId,
    bool UpdateBuffer,
    bool SkippedByDistance,
    ShaderName? WantedShader,
    bool SwitchShader,
    bool SetVertexColor,
    bool SetHighlightColor,
    bool SetFogUniforms,
    double SectorDesaturation);

public sealed record Renderer3DModelDrawStatePlan(
    ShaderName InitialShader,
    IReadOnlyList<Renderer3DModelThingDrawStatePlan> Draws);

public sealed record Renderer3DModelLightCandidate(
    int Id,
    bool BoundingBoxesIntersect,
    bool SpotLight,
    double SpotRadius1Degrees,
    double SpotRadius2Degrees,
    double Radius,
    double Linearity);

public sealed record Renderer3DModelLightSlotPlan(
    int LightId,
    bool SpotLight,
    float SpotRadius1Cosine,
    float SpotRadius2Cosine,
    float Strength,
    float Linearity);

public sealed record Renderer3DModelLightUniformPlan(
    int ThingIndex,
    IReadOnlyList<Renderer3DModelLightSlotPlan> Lights,
    int ClearedLightColorSlots,
    bool SetLightColorUniform,
    bool SetLightDetailUniforms);

public sealed record Renderer3DModelLightUniformsPlan(IReadOnlyList<Renderer3DModelLightUniformPlan> Things);

public sealed record Renderer3DGeometryLightCandidate(
    int Id,
    bool BoundingBoxesIntersect,
    bool SpotLight,
    double SpotRadius1Degrees,
    double SpotRadius2Degrees,
    double Radius,
    double Linearity);

public sealed record Renderer3DGeometryLightSlotPlan(
    int LightId,
    bool SpotLight,
    float SpotRadius1Cosine,
    float SpotRadius2Cosine,
    float Strength,
    float Linearity);

public sealed record Renderer3DGeometryLightUniformPlan(
    int GeometryIndex,
    IReadOnlyList<Renderer3DGeometryLightSlotPlan> Lights,
    int ClearedLightColorSlots,
    bool SetLightColorUniform,
    bool SetLightDetailUniforms);

public sealed record Renderer3DGeometryLightUniformsPlan(IReadOnlyList<Renderer3DGeometryLightUniformPlan> Geometry);

public sealed record Renderer3DModelMeshCandidate(
    int ThingId,
    int MeshCount,
    IReadOnlyList<string?> Textures);

public sealed record Renderer3DModelMeshDrawPlan(
    int ThingId,
    int MeshIndex,
    string? Texture,
    bool SetTexture,
    bool DrawMesh);

public sealed record Renderer3DModelMeshRenderPlan(
    IReadOnlyList<Renderer3DModelMeshDrawPlan> Draws,
    bool DisableLightsEnabledUniform);

public enum Renderer3DThingPositionMatrixStrategy
{
    Billboard,
    XYBillboard,
    DirectPosition,
}

public sealed record Renderer3DThingPositionMatrixPlan(
    ThingRenderMode RequestedRenderMode,
    ThingRenderMode EffectiveRenderMode,
    Renderer3DThingPositionMatrixStrategy Strategy,
    bool DemotedModelRenderMode);

public static class Renderer3DGeometryLifecyclePlan
{
    public const float EventLineArrowheadLength = 20.0f;
    public const float EventLineArrowheadHalfAngle = 0.46f;

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

    public static Renderer3DSectorGeometryCollectionPlan BuildSectorGeometryCollectionPlan(
        Renderer3DSectorGeometryCandidate geometry,
        bool drawSky)
    {
        if (geometry.Triangles < 0) throw new ArgumentOutOfRangeException(nameof(geometry));
        if (!geometry.HasTexture || geometry.Triangles == 0)
        {
            return new Renderer3DSectorGeometryCollectionPlan(
                geometry.Id,
                Accepted: false,
                Bucket: null,
                UnsupportedRenderPassMessage: null);
        }

        if (geometry.RenderAsSky && drawSky)
        {
            return new Renderer3DSectorGeometryCollectionPlan(
                geometry.Id,
                Accepted: true,
                Renderer3DGeometryBucketKind.SkyGeometry,
                UnsupportedRenderPassMessage: null);
        }

        Renderer3DGeometryBucketKind? bucket = geometry.RenderPass switch
        {
            RenderPass.Solid => Renderer3DGeometryBucketKind.SolidGeometry,
            RenderPass.Mask => Renderer3DGeometryBucketKind.MaskedGeometry,
            RenderPass.Additive or RenderPass.Alpha => Renderer3DGeometryBucketKind.TranslucentGeometry,
            _ => null,
        };

        return bucket.HasValue
            ? new Renderer3DSectorGeometryCollectionPlan(
                geometry.Id,
                Accepted: true,
                bucket,
                UnsupportedRenderPassMessage: null)
            : new Renderer3DSectorGeometryCollectionPlan(
                geometry.Id,
                Accepted: false,
                Bucket: null,
                UnsupportedRenderPassMessage: "Geometry rendering of " + geometry.RenderPass + " render pass is not implemented!");
    }

    public static Renderer3DThingGeometryCollectionPlan BuildThingGeometryCollectionPlan(Renderer3DThingGeometryCandidate thing)
    {
        if (double.IsNaN(thing.LightRadius) || thing.LightRadius < 0.0) throw new ArgumentOutOfRangeException(nameof(thing));

        var buckets = new List<Renderer3DGeometryBucketKind>();
        bool updateLightRadius = thing.LightRenderMode != LightRenderMode.NONE && !thing.FullBrightness && thing.HasLightType;
        bool updateBoundingBox = false;
        if (updateLightRadius && thing.LightRadius > 0.0)
        {
            updateBoundingBox = thing.LightAnimated;
            buckets.Add(Renderer3DGeometryBucketKind.LightThings);
        }

        bool usesModelBucket =
            (thing.RenderMode == ThingRenderMode.MODEL || thing.RenderMode == ThingRenderMode.VOXEL) &&
            (thing.ModelRenderMode == ModelRenderMode.ALL ||
             thing.ModelRenderMode == ModelRenderMode.ACTIVE_THINGS_FILTER ||
             (thing.ModelRenderMode == ModelRenderMode.SELECTION && thing.Selected));

        string? unsupportedRenderPassMessage = null;
        bool updateSpriteFrame = !usesModelBucket;
        if (usesModelBucket)
        {
            if (thing.RenderPass == RenderPass.Mask ||
                thing.RenderPass == RenderPass.Solid ||
                (thing.RenderPass == RenderPass.Alpha && thing.VertexColorOpaque))
            {
                buckets.Add(Renderer3DGeometryBucketKind.MaskedModelThings);
            }
            else if (thing.RenderPass == RenderPass.Alpha || thing.RenderPass == RenderPass.Additive)
            {
                buckets.Add(Renderer3DGeometryBucketKind.TranslucentModelThings);
            }
            else
            {
                unsupportedRenderPassMessage = "Thing model rendering of " + thing.RenderPass + " render pass is not implemented!";
            }
        }
        else if (thing.HasTexture)
        {
            Renderer3DGeometryBucketKind? thingBucket = thing.RenderPass switch
            {
                RenderPass.Solid => Renderer3DGeometryBucketKind.SolidThings,
                RenderPass.Mask => Renderer3DGeometryBucketKind.MaskedThings,
                RenderPass.Additive or RenderPass.Alpha => Renderer3DGeometryBucketKind.TranslucentThings,
                _ => null,
            };

            if (thingBucket.HasValue)
            {
                buckets.Add(thingBucket.Value);
            }
            else
            {
                unsupportedRenderPassMessage = "Thing rendering of " + thing.RenderPass + " render pass is not implemented!";
            }
        }

        buckets.Add(Renderer3DGeometryBucketKind.AllThings);
        return new Renderer3DThingGeometryCollectionPlan(
            thing.Id,
            UpdateThing: true,
            updateLightRadius,
            updateBoundingBox,
            updateSpriteFrame,
            buckets,
            unsupportedRenderPassMessage);
    }

    public static Renderer3DSkyRenderPlan BuildSkyRenderPlan(IReadOnlyList<Renderer3DSkyGeometryCandidate> geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        var draws = new List<Renderer3DSkyGeometryDrawPlan>(geometry.Count);
        int? currentSectorId = null;
        foreach (Renderer3DSkyGeometryCandidate item in geometry)
        {
            if (item.VertexOffset < 0) throw new ArgumentOutOfRangeException(nameof(geometry));
            if (item.Triangles < 0) throw new ArgumentOutOfRangeException(nameof(geometry));

            bool sectorChanged = currentSectorId != item.SectorId;
            bool sectorAvailable = item.SectorHasGeometryBuffer && item.SectorHasMap;
            bool updateSectorGeometry = sectorChanged && item.SectorNeedsUpdate;
            bool bindSectorGeometryBuffer = sectorChanged && sectorAvailable;
            bool clearCurrentSector = sectorChanged && !sectorAvailable;
            if (sectorChanged)
            {
                currentSectorId = sectorAvailable ? item.SectorId : null;
            }

            bool draw = currentSectorId.HasValue;
            draws.Add(new Renderer3DSkyGeometryDrawPlan(
                item.Id,
                item.SectorId,
                updateSectorGeometry,
                bindSectorGeometryBuffer,
                clearCurrentSector,
                SetHighlightColor: draw,
                draw,
                PrimitiveType.TriangleList,
                item.VertexOffset,
                draw ? item.Triangles : 0));
        }

        return new Renderer3DSkyRenderPlan(
            ShaderName.world3d_skybox,
            SetSkyTexture: true,
            SetCameraUniform: true,
            draws);
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

    public static Renderer3DVisualVerticesPassPlan BuildVisualVerticesPassPlan()
        => new(
            [
                new(Renderer3DGeometryPassOperationKind.RenderVertices),
            ]);

    public static Renderer3DSlopeHandlesPassPlan BuildSlopeHandlesPassPlan(bool isUdmfMap)
        => isUdmfMap
            ? new Renderer3DSlopeHandlesPassPlan(
                [
                    new(Renderer3DGeometryPassOperationKind.RenderSlopeHandles),
                ])
            : new Renderer3DSlopeHandlesPassPlan([]);

    public static Renderer3DEventLinesPassPlan BuildEventLinesPassPlan(bool showGzEventLines)
        => showGzEventLines
            ? new Renderer3DEventLinesPassPlan(
                [
                    new(Renderer3DGeometryPassOperationKind.RenderArrows),
                ])
            : new Renderer3DEventLinesPassPlan([]);

    public static Renderer3DFpsUpdatePlan BuildFpsUpdatePlan(bool showFps, int currentFps, long elapsedMilliseconds)
    {
        if (currentFps < 0) throw new ArgumentOutOfRangeException(nameof(currentFps));
        if (elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
        if (!showFps) return new Renderer3DFpsUpdatePlan([]);

        int incrementedFps = currentFps + 1;
        var operations = new List<Renderer3DFpsUpdateOperation>
        {
            new(Renderer3DFpsUpdateOperationKind.IncrementFrameCounter, FrameCount: incrementedFps),
        };

        if (elapsedMilliseconds > 1000)
        {
            operations.Add(new Renderer3DFpsUpdateOperation(Renderer3DFpsUpdateOperationKind.SetFpsLabelText, LabelText: $"{incrementedFps} FPS"));
            operations.Add(new Renderer3DFpsUpdateOperation(Renderer3DFpsUpdateOperationKind.ResetFrameCounter, FrameCount: 0));
            operations.Add(new Renderer3DFpsUpdateOperation(Renderer3DFpsUpdateOperationKind.RestartStopwatch));
        }

        return new Renderer3DFpsUpdatePlan(operations);
    }

    public static Renderer3DDynamicLightUpdatePlan BuildDynamicLightUpdatePlan(
        IReadOnlyList<Renderer3DDynamicLightCandidate> lightCandidates,
        int maxDynamicLights)
    {
        ArgumentNullException.ThrowIfNull(lightCandidates);
        if (maxDynamicLights < 0) throw new ArgumentOutOfRangeException(nameof(maxDynamicLights));
        foreach (Renderer3DDynamicLightCandidate light in lightCandidates)
        {
            if (!double.IsFinite(light.CameraDistance)) throw new ArgumentOutOfRangeException(nameof(lightCandidates));
        }

        Renderer3DDynamicLightCandidate[] selected = lightCandidates
            .OrderBy(light => light.CameraDistance)
            .Where(light => light.Visible)
            .Take(maxDynamicLights)
            .OrderBy(light => DynamicLightRenderStyleSortValue(light.RenderStyle))
            .ToArray();

        int[] lightOffsets = new int[4];
        foreach (Renderer3DDynamicLightCandidate light in selected)
        {
            lightOffsets[DynamicLightOffsetIndex(light.RenderStyle)]++;
        }

        return new Renderer3DDynamicLightUpdatePlan(
            selected.Select(light => light.Id).ToArray(),
            lightOffsets);
    }

    private static int DynamicLightRenderStyleSortValue(Renderer3DDynamicLightRenderStyle renderStyle)
        => renderStyle switch
        {
            Renderer3DDynamicLightRenderStyle.Additive => 25,
            Renderer3DDynamicLightRenderStyle.Vavoom => 50,
            Renderer3DDynamicLightRenderStyle.Attenuated => 98,
            Renderer3DDynamicLightRenderStyle.Lightmap => 98,
            Renderer3DDynamicLightRenderStyle.Normal => 99,
            Renderer3DDynamicLightRenderStyle.Subtractive => 100,
            _ => throw new ArgumentOutOfRangeException(nameof(renderStyle)),
        };

    private static int DynamicLightOffsetIndex(Renderer3DDynamicLightRenderStyle renderStyle)
        => renderStyle switch
        {
            Renderer3DDynamicLightRenderStyle.Normal => 0,
            Renderer3DDynamicLightRenderStyle.Vavoom => 0,
            Renderer3DDynamicLightRenderStyle.Additive => 2,
            Renderer3DDynamicLightRenderStyle.Subtractive => 3,
            Renderer3DDynamicLightRenderStyle.Lightmap => 1,
            Renderer3DDynamicLightRenderStyle.Attenuated => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(renderStyle)),
        };

    public static Renderer3DThingCageRenderPlan BuildThingCageRenderPlan(
        IReadOnlyList<Renderer3DThingCageCandidate> things,
        bool showSelection,
        int selectionColor)
    {
        ArgumentNullException.ThrowIfNull(things);
        foreach (Renderer3DThingCageCandidate thing in things)
        {
            if (thing.CageLength < 0) throw new ArgumentOutOfRangeException(nameof(things));
        }

        return new Renderer3DThingCageRenderPlan(
            [
                new(Renderer3DThingCageRenderOperationKind.SetAlphaBlend, Enabled: true),
                new(Renderer3DThingCageRenderOperationKind.SetAlphaTest, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetZWrite, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetSourceBlend, Blend: Blend.SourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetDestinationBlend, Blend: Blend.SourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetShader, Shader: ShaderName.world3d_constant_color),
            ],
            things.Select(thing => new Renderer3DThingCageDrawPlan(
                thing.Id,
                thing.Selected && showSelection ? selectionColor : thing.CageColor,
                thing.Selected && showSelection || thing.Highlighted ? 1.0f : 0.6f,
                PrimitiveType.LineList,
                StartIndex: 0,
                PrimitiveCount: thing.CageLength)).ToArray());
    }

    public static Renderer3DVisualVertexRenderPlan BuildVisualVertexRenderPlan(
        IReadOnlyList<Renderer3DVisualVertexCandidate>? visualVertices,
        bool showSelection,
        int selectionColor,
        int infoLineColor,
        int verticesColor)
    {
        if (visualVertices == null)
        {
            return new Renderer3DVisualVertexRenderPlan([], []);
        }

        return new Renderer3DVisualVertexRenderPlan(
            [
                new(Renderer3DThingCageRenderOperationKind.SetAlphaBlend, Enabled: true),
                new(Renderer3DThingCageRenderOperationKind.SetAlphaTest, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetZWrite, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetSourceBlend, Blend: Blend.SourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetDestinationBlend, Blend: Blend.SourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetShader, Shader: ShaderName.world3d_constant_color),
            ],
            visualVertices.Select(vertex => new Renderer3DVisualVertexDrawPlan(
                vertex.Id,
                vertex.Selected && showSelection ? selectionColor : vertex.HaveHeightOffset ? infoLineColor : verticesColor,
                vertex.Selected && showSelection || vertex.Highlighted ? 1.0f : 0.6f,
                vertex.CeilingVertex ? Renderer3DVisualVertexHandleKind.Upper : Renderer3DVisualVertexHandleKind.Lower,
                PrimitiveType.LineList,
                StartIndex: 0,
                PrimitiveCount: 8)).ToArray());
    }

    public static Renderer3DSlopeHandleRenderPlan BuildSlopeHandleRenderPlan(
        IReadOnlyList<Renderer3DSlopeHandleCandidate>? slopeHandles,
        bool showSelection,
        int verticesColor,
        int guidelineColor,
        int selectionColor,
        int highlightColor)
    {
        if (slopeHandles == null || !showSelection)
        {
            return new Renderer3DSlopeHandleRenderPlan([], []);
        }

        return new Renderer3DSlopeHandleRenderPlan(
            [
                new(Renderer3DThingCageRenderOperationKind.SetAlphaBlend, Enabled: true),
                new(Renderer3DThingCageRenderOperationKind.SetAlphaTest, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetZWrite, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetSourceBlend, Blend: Blend.SourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetDestinationBlend, Blend: Blend.InverseSourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetShader, Shader: ShaderName.world3d_slope_handle),
            ],
            slopeHandles
                .Where(handle => handle.Kind is Renderer3DSlopeHandleKind.Line or Renderer3DSlopeHandleKind.Vertex)
                .Select(handle => new Renderer3DSlopeHandleDrawPlan(
                    handle.Id,
                    handle.Pivot ? guidelineColor : handle.Selected ? selectionColor : handle.Highlighted ? highlightColor : verticesColor,
                    handle.Kind,
                    handle.Kind == Renderer3DSlopeHandleKind.Line ? (float)handle.Length : 1.0f,
                    PrimitiveType.TriangleList,
                    StartIndex: 0,
                    PrimitiveCount: handle.Kind == Renderer3DSlopeHandleKind.Line ? 2 : 1))
                .ToArray());
    }

    public static Renderer3DEventLineRenderPlan BuildEventLineRenderPlan(IReadOnlyList<Line3D> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var vertices = new List<WorldVertex>(lines.Count * 6);
        foreach (Line3D line in lines)
        {
            if (!line.Start.IsFinite() || !line.End.IsFinite()) throw new ArgumentOutOfRangeException(nameof(lines));

            int color = unchecked((int)line.Color);
            WorldVertex end = Vertex(line.End, color);
            vertices.Add(Vertex(line.Start, color));
            vertices.Add(end);

            if (!line.RenderArrowhead) continue;

            double normalZ = line.GetDelta().GetNormal().z * EventLineArrowheadLength;
            double angle = line.GetAngle();
            Vector3D first = new(
                line.End.x - EventLineArrowheadLength * Math.Sin(angle - EventLineArrowheadHalfAngle),
                line.End.y + EventLineArrowheadLength * Math.Cos(angle - EventLineArrowheadHalfAngle),
                line.End.z - normalZ);
            Vector3D second = new(
                line.End.x - EventLineArrowheadLength * Math.Sin(angle + EventLineArrowheadHalfAngle),
                line.End.y + EventLineArrowheadLength * Math.Cos(angle + EventLineArrowheadHalfAngle),
                line.End.z - normalZ);

            vertices.Add(end);
            vertices.Add(Vertex(first, color));
            vertices.Add(end);
            vertices.Add(Vertex(second, color));
        }

        if (vertices.Count < 2)
        {
            return new Renderer3DEventLineRenderPlan([], [], SetIdentityWorld: false, PrimitiveType.LineList, StartIndex: 0, PrimitiveCount: 0, DisposeVertexBuffer: false);
        }

        return new Renderer3DEventLineRenderPlan(
            vertices.ToArray(),
            [
                new(Renderer3DThingCageRenderOperationKind.SetAlphaBlend, Enabled: true),
                new(Renderer3DThingCageRenderOperationKind.SetAlphaTest, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetZWrite, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetSourceBlend, Blend: Blend.SourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetDestinationBlend, Blend: Blend.SourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetShader, Shader: ShaderName.world3d_vertex_color),
            ],
            SetIdentityWorld: true,
            PrimitiveType.LineList,
            StartIndex: 0,
            PrimitiveCount: vertices.Count / 2,
            DisposeVertexBuffer: true);
    }

    public static Renderer3DShaderPassPlan BuildGeometryShaderPassPlan(
        ShaderName baseShader,
        bool highlighted,
        bool showHighlight,
        bool selected,
        bool showSelection,
        bool drawFog,
        bool fullBrightness,
        bool classicRendering,
        bool sectorHasFog)
    {
        ShaderName highlightShader = (ShaderName)(baseShader + 2);
        bool useHighlightShader = highlighted && showHighlight || selected && showSelection;
        ShaderName wantedShader = useHighlightShader ? highlightShader : baseShader;
        bool useFogShader = drawFog && !fullBrightness && !classicRendering && sectorHasFog;
        if (useFogShader)
        {
            wantedShader += 8;
        }

        return new Renderer3DShaderPassPlan(
            baseShader,
            highlightShader,
            wantedShader,
            useHighlightShader,
            useFogShader,
            AppliesFogUniforms: wantedShader > ShaderName.world3d_p7);
    }

    public static Renderer3DThingShaderPassPlan BuildThingShaderPassPlan(
        ShaderName baseShader,
        bool highlighted,
        bool showHighlight,
        bool selected,
        bool showSelection,
        bool drawFog,
        bool fullBrightness,
        bool classicRendering,
        bool thingSectorHasFog,
        bool lightInternal,
        bool lightIsSun,
        bool drawLights,
        bool hasDynamicLights,
        bool litColorNonZero)
    {
        ShaderName highlightShader = (ShaderName)(baseShader + 2);
        bool useHighlightShader = highlighted && showHighlight || selected && showSelection;
        ShaderName wantedShader = useHighlightShader ? highlightShader : baseShader;
        bool useFogShader = drawFog && !fullBrightness && !classicRendering && thingSectorHasFog;
        if (useFogShader)
        {
            wantedShader += 8;
        }

        Renderer3DThingVertexColorSource vertexColorSource = Renderer3DThingVertexColorSource.None;
        if (lightInternal && !lightIsSun && !fullBrightness && !classicRendering)
        {
            wantedShader += 4;
            vertexColorSource = Renderer3DThingVertexColorSource.InternalLight;
        }
        else if (drawLights && !fullBrightness && !classicRendering && hasDynamicLights && litColorNonZero)
        {
            wantedShader += 4;
            vertexColorSource = Renderer3DThingVertexColorSource.DynamicLight;
        }

        return new Renderer3DThingShaderPassPlan(
            baseShader,
            highlightShader,
            wantedShader,
            useHighlightShader,
            useFogShader,
            vertexColorSource,
            AppliesFogUniforms: wantedShader > ShaderName.world3d_p7);
    }

    public static Renderer3DThingLitColorPlan BuildThingLitColorPlan(
        int thingIndex,
        bool thingTypeDoesNotLightSelf,
        Vector3f thingCenter,
        IReadOnlyList<Renderer3DThingLightingCandidate> lights,
        bool inverseSquareLightAttenuation)
    {
        ArgumentNullException.ThrowIfNull(lights);
        if (!IsFinite(thingCenter)) throw new ArgumentOutOfRangeException(nameof(thingCenter));

        Color4 litColor = new();
        var contributions = new List<Renderer3DThingLightContributionPlan>(lights.Count);
        foreach (Renderer3DThingLightingCandidate light in lights)
        {
            if (!IsFinite(light.Center)) throw new ArgumentOutOfRangeException(nameof(lights));
            if (!double.IsFinite(light.Radius) || light.Radius < 0.0f) throw new ArgumentOutOfRangeException(nameof(lights));
            if (!IsFinite(light.Color)) throw new ArgumentOutOfRangeException(nameof(lights));
            if (!IsFinite(light.SpotDirection)) throw new ArgumentOutOfRangeException(nameof(lights));
            if (!double.IsFinite(light.SpotRadius1Degrees)) throw new ArgumentOutOfRangeException(nameof(lights));
            if (!double.IsFinite(light.SpotRadius2Degrees)) throw new ArgumentOutOfRangeException(nameof(lights));
            if (!double.IsFinite(light.Linearity)) throw new ArgumentOutOfRangeException(nameof(lights));

            bool skippedSelfLight = thingTypeDoesNotLightSelf && thingIndex == light.ThingIndex;
            if (skippedSelfLight)
            {
                contributions.Add(new Renderer3DThingLightContributionPlan(
                    light.Id,
                    SkippedSelfLight: true,
                    InsideRadius: false,
                    Attenuation: 0.0f,
                    SpotScale: 0.0f,
                    ContributionScale: 0.0f,
                    new Color4()));
                continue;
            }

            float distanceSquared = Vector3f.DistanceSquared(light.Center, thingCenter);
            float radiusSquared = light.Radius * light.Radius;
            bool insideRadius = distanceSquared < radiusSquared;
            if (!insideRadius)
            {
                contributions.Add(new Renderer3DThingLightContributionPlan(
                    light.Id,
                    SkippedSelfLight: false,
                    InsideRadius: false,
                    Attenuation: 0.0f,
                    SpotScale: 0.0f,
                    ContributionScale: 0.0f,
                    new Color4()));
                continue;
            }

            int sign = light.RenderStyle == Renderer3DDynamicLightRenderStyle.Subtractive ? -1 : 1;
            Vector3f lightToThing = thingCenter - light.Center;
            float distance = lightToThing.Length();
            float attenuation = inverseSquareLightAttenuation
                ? BuildInverseSquareDistanceAttenuation(
                    Math.Max(distance, (float)Math.Sqrt(light.Radius) * 2.0f),
                    light.Radius * 2.0f,
                    Math.Min(1500.0f, (light.Radius * 2.0f) * (light.Radius * 2.0f) / 10.0f),
                    light.Linearity)
                : 1.0f - distance / light.Radius;

            float scaler = attenuation * light.Color.Alpha;
            float spotScale = 1.0f;
            if (light.SpotLight)
            {
                lightToThing.Normalize();
                float cosDirection = Vector3f.Dot(-lightToThing, light.SpotDirection);
                spotScale = Smoothstep(CosDegrees(light.SpotRadius2Degrees), CosDegrees(light.SpotRadius1Degrees), cosDirection);
                scaler *= spotScale;
            }

            Color4 contribution = new();
            if (scaler > 0.0f)
            {
                contribution = new Color4(
                    light.Color.Red * scaler * sign,
                    light.Color.Green * scaler * sign,
                    light.Color.Blue * scaler * sign,
                    0.0f);
                litColor.Red += contribution.Red;
                litColor.Green += contribution.Green;
                litColor.Blue += contribution.Blue;
            }

            contributions.Add(new Renderer3DThingLightContributionPlan(
                light.Id,
                SkippedSelfLight: false,
                InsideRadius: true,
                attenuation,
                spotScale,
                scaler,
                contribution));
        }

        return new Renderer3DThingLitColorPlan(litColor, contributions);
    }

    public static Renderer3DTranslucentGeometryOrderPlan BuildTranslucentGeometryOrderPlan(
        IReadOnlyList<Renderer3DTranslucentGeometryCandidate> geometry,
        Vector3D cameraPosition)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (!cameraPosition.IsFinite()) throw new ArgumentOutOfRangeException(nameof(cameraPosition));
        foreach (Renderer3DTranslucentGeometryCandidate candidate in geometry)
        {
            if (!candidate.BoundingBoxCenter.IsFinite()) throw new ArgumentOutOfRangeException(nameof(geometry));
        }

        Renderer3DTranslucentGeometryCandidate[] ordered = geometry.ToArray();
        Array.Sort(ordered, (first, second) => CompareTranslucentGeometry(first, second, cameraPosition));

        var draws = new List<Renderer3DTranslucentGeometryDrawPlan>(ordered.Length);
        RenderPass currentPass = RenderPass.Solid;
        foreach (Renderer3DTranslucentGeometryCandidate candidate in ordered)
        {
            Blend? destinationBlend = null;
            if (candidate.RenderPass != currentPass)
            {
                destinationBlend = candidate.RenderPass switch
                {
                    RenderPass.Additive => Blend.One,
                    RenderPass.Alpha => Blend.InverseSourceAlpha,
                    _ => null,
                };
                currentPass = candidate.RenderPass;
            }

            draws.Add(new Renderer3DTranslucentGeometryDrawPlan(candidate.Id, candidate.RenderPass, destinationBlend));
        }

        return new Renderer3DTranslucentGeometryOrderPlan(draws);
    }

    public static Renderer3DTranslucentThingOrderPlan BuildTranslucentThingOrderPlan(
        IReadOnlyList<Renderer3DTranslucentThingCandidate> things,
        Vector3D cameraPosition)
    {
        ArgumentNullException.ThrowIfNull(things);
        if (!cameraPosition.IsFinite()) throw new ArgumentOutOfRangeException(nameof(cameraPosition));
        foreach (Renderer3DTranslucentThingCandidate thing in things)
        {
            if (!thing.BoundingBoxCenter.IsFinite()) throw new ArgumentOutOfRangeException(nameof(things));
        }

        Renderer3DTranslucentThingCandidate[] ordered = things
            .OrderByDescending(thing => (cameraPosition - thing.BoundingBoxCenter).GetLengthSq())
            .ToArray();

        var draws = new List<Renderer3DTranslucentThingDrawPlan>(ordered.Length);
        RenderPass currentPass = RenderPass.Solid;
        foreach (Renderer3DTranslucentThingCandidate thing in ordered)
        {
            Blend? destinationBlend = null;
            if (thing.RenderPass != currentPass)
            {
                destinationBlend = thing.RenderPass switch
                {
                    RenderPass.Additive => Blend.One,
                    RenderPass.Alpha => Blend.InverseSourceAlpha,
                    _ => null,
                };
                currentPass = thing.RenderPass;
            }

            draws.Add(new Renderer3DTranslucentThingDrawPlan(thing.Id, thing.RenderPass, destinationBlend));
        }

        return new Renderer3DTranslucentThingOrderPlan(
            TextureAddress.Clamp,
            Cull.None,
            draws,
            TextureAddress.Wrap,
            Cull.Clockwise);
    }

    public static Renderer3DModelRenderPlan BuildModelRenderPlan(
        bool translucent,
        IReadOnlyList<Renderer3DModelThingGroup> maskedModelThingGroups,
        IReadOnlyList<Renderer3DModelThingCandidate> translucentModelThings,
        Vector3D cameraPosition,
        bool fullBrightness,
        int lightCount,
        bool inverseSquareLightAttenuation)
    {
        ArgumentNullException.ThrowIfNull(maskedModelThingGroups);
        ArgumentNullException.ThrowIfNull(translucentModelThings);
        if (!cameraPosition.IsFinite()) throw new ArgumentOutOfRangeException(nameof(cameraPosition));
        if (lightCount < 0) throw new ArgumentOutOfRangeException(nameof(lightCount));

        foreach (Renderer3DModelThingGroup group in maskedModelThingGroups)
        {
            ArgumentNullException.ThrowIfNull(group.Things);
            foreach (Renderer3DModelThingCandidate thing in group.Things)
            {
                if (!thing.BoundingBoxCenter.IsFinite()) throw new ArgumentOutOfRangeException(nameof(maskedModelThingGroups));
            }
        }

        foreach (Renderer3DModelThingCandidate thing in translucentModelThings)
        {
            if (!thing.BoundingBoxCenter.IsFinite()) throw new ArgumentOutOfRangeException(nameof(translucentModelThings));
        }

        ShaderName shaderPass = fullBrightness ? ShaderName.world3d_fullbright : ShaderName.world3d_main_vertexcolor;
        Renderer3DModelThingCandidate[] things = translucent
            ? translucentModelThings
                .OrderByDescending(thing => (cameraPosition - thing.BoundingBoxCenter).GetLengthSq())
                .ToArray()
            : maskedModelThingGroups
                .SelectMany(group => group.Things)
                .ToArray();

        var draws = new List<Renderer3DModelThingDrawPlan>(things.Length);
        RenderPass currentPass = RenderPass.Solid;
        foreach (Renderer3DModelThingCandidate thing in things)
        {
            Blend? destinationBlend = null;
            if (translucent && thing.RenderPass != currentPass)
            {
                destinationBlend = thing.RenderPass switch
                {
                    RenderPass.Additive => Blend.One,
                    RenderPass.Alpha => Blend.InverseSourceAlpha,
                    _ => null,
                };
                currentPass = thing.RenderPass;
            }

            draws.Add(new Renderer3DModelThingDrawPlan(thing.Id, thing.RenderPass, destinationBlend));
        }

        return new Renderer3DModelRenderPlan(
            shaderPass,
            (ShaderName)(shaderPass + 2),
            LightsEnabled: lightCount > 0,
            IgnoreNormals: true,
            UseLightStrength: inverseSquareLightAttenuation,
            draws);
    }

    public static Renderer3DModelDrawStatePlan BuildModelDrawStatePlan(
        IReadOnlyList<Renderer3DModelThingDrawStateCandidate> things,
        Vector3D cameraPosition,
        bool fullBrightness,
        bool drawFog,
        bool classicRendering,
        bool showHighlight,
        bool showSelection)
    {
        ArgumentNullException.ThrowIfNull(things);
        if (!cameraPosition.IsFinite()) throw new ArgumentOutOfRangeException(nameof(cameraPosition));

        foreach (Renderer3DModelThingDrawStateCandidate thing in things)
        {
            if (!thing.BoundingBoxCenter.IsFinite()) throw new ArgumentOutOfRangeException(nameof(things));
            if (!double.IsFinite(thing.DistanceCheckSq) || thing.DistanceCheckSq < 0.0) throw new ArgumentOutOfRangeException(nameof(things));
            if (!double.IsFinite(thing.SectorDesaturation)) throw new ArgumentOutOfRangeException(nameof(things));
        }

        ShaderName shaderPass = fullBrightness ? ShaderName.world3d_fullbright : ShaderName.world3d_main_vertexcolor;
        ShaderName highShaderPass = (ShaderName)(shaderPass + 2);
        ShaderName currentShaderPass = shaderPass;
        var draws = new List<Renderer3DModelThingDrawStatePlan>(things.Count);

        foreach (Renderer3DModelThingDrawStateCandidate thing in things)
        {
            double cameraDistance = (cameraPosition - thing.BoundingBoxCenter).GetLengthSq();
            bool skippedByDistance = thing.DistanceCheckSq < double.MaxValue && cameraDistance > thing.DistanceCheckSq;
            if (skippedByDistance)
            {
                draws.Add(new Renderer3DModelThingDrawStatePlan(
                    thing.Id,
                    UpdateBuffer: true,
                    SkippedByDistance: true,
                    WantedShader: null,
                    SwitchShader: false,
                    SetVertexColor: false,
                    SetHighlightColor: false,
                    SetFogUniforms: false,
                    SectorDesaturation: 0.0));
                continue;
            }

            bool useHighlightShader = thing.Highlighted && showHighlight || thing.Selected && showSelection;
            ShaderName wantedShaderPass = useHighlightShader ? highShaderPass : shaderPass;
            bool useFogShader = drawFog && !fullBrightness && !classicRendering && thing.SectorHasFog;
            if (useFogShader)
            {
                wantedShaderPass += 8;
            }

            bool switchShader = currentShaderPass != wantedShaderPass;
            if (switchShader)
            {
                currentShaderPass = wantedShaderPass;
            }

            draws.Add(new Renderer3DModelThingDrawStatePlan(
                thing.Id,
                UpdateBuffer: true,
                SkippedByDistance: false,
                wantedShaderPass,
                switchShader,
                SetVertexColor: true,
                SetHighlightColor: true,
                SetFogUniforms: wantedShaderPass > ShaderName.world3d_p7,
                thing.SectorDesaturation));
        }

        return new Renderer3DModelDrawStatePlan(shaderPass, draws);
    }

    public static Renderer3DModelLightUniformsPlan BuildModelLightUniformsPlan(
        IReadOnlyList<IReadOnlyList<Renderer3DModelLightCandidate>> thingLightCandidates,
        int maxDynamicLightsPerSurface)
    {
        ArgumentNullException.ThrowIfNull(thingLightCandidates);
        if (maxDynamicLightsPerSurface < 0) throw new ArgumentOutOfRangeException(nameof(maxDynamicLightsPerSurface));

        bool hadLights = false;
        var things = new List<Renderer3DModelLightUniformPlan>(thingLightCandidates.Count);
        for (int thingIndex = 0; thingIndex < thingLightCandidates.Count; thingIndex++)
        {
            IReadOnlyList<Renderer3DModelLightCandidate> candidates = thingLightCandidates[thingIndex];
            ArgumentNullException.ThrowIfNull(candidates);

            var lights = new List<Renderer3DModelLightSlotPlan>(maxDynamicLightsPerSurface);
            foreach (Renderer3DModelLightCandidate candidate in candidates)
            {
                if (!double.IsFinite(candidate.SpotRadius1Degrees)) throw new ArgumentOutOfRangeException(nameof(thingLightCandidates));
                if (!double.IsFinite(candidate.SpotRadius2Degrees)) throw new ArgumentOutOfRangeException(nameof(thingLightCandidates));
                if (!double.IsFinite(candidate.Radius) || candidate.Radius < 0.0) throw new ArgumentOutOfRangeException(nameof(thingLightCandidates));
                if (!double.IsFinite(candidate.Linearity)) throw new ArgumentOutOfRangeException(nameof(thingLightCandidates));
                if (!candidate.BoundingBoxesIntersect) continue;
                if (lights.Count >= maxDynamicLightsPerSurface) break;

                double diameter = candidate.Radius * 2.0;
                lights.Add(new Renderer3DModelLightSlotPlan(
                    candidate.Id,
                    candidate.SpotLight,
                    candidate.SpotLight ? CosDegrees(candidate.SpotRadius1Degrees) : 0.0f,
                    candidate.SpotLight ? CosDegrees(candidate.SpotRadius2Degrees) : 0.0f,
                    (float)Math.Min(1500.0, diameter * diameter / 10.0),
                    (float)candidate.Linearity));
            }

            bool haveLights = lights.Count > 0;
            bool setUniforms = hadLights != haveLights || haveLights;
            things.Add(new Renderer3DModelLightUniformPlan(
                thingIndex,
                lights,
                maxDynamicLightsPerSurface - lights.Count,
                SetLightColorUniform: setUniforms,
                SetLightDetailUniforms: setUniforms && haveLights));
            hadLights = haveLights;
        }

        return new Renderer3DModelLightUniformsPlan(things);
    }

    public static Renderer3DGeometryLightUniformsPlan BuildGeometryLightUniformsPlan(
        IReadOnlyList<IReadOnlyList<Renderer3DGeometryLightCandidate>> geometryLightCandidates,
        int maxDynamicLightsPerSurface)
    {
        ArgumentNullException.ThrowIfNull(geometryLightCandidates);
        if (maxDynamicLightsPerSurface < 0) throw new ArgumentOutOfRangeException(nameof(maxDynamicLightsPerSurface));

        bool hadLights = false;
        var geometry = new List<Renderer3DGeometryLightUniformPlan>(geometryLightCandidates.Count);
        for (int geometryIndex = 0; geometryIndex < geometryLightCandidates.Count; geometryIndex++)
        {
            IReadOnlyList<Renderer3DGeometryLightCandidate> candidates = geometryLightCandidates[geometryIndex];
            ArgumentNullException.ThrowIfNull(candidates);

            var lights = new List<Renderer3DGeometryLightSlotPlan>(maxDynamicLightsPerSurface);
            foreach (Renderer3DGeometryLightCandidate candidate in candidates)
            {
                if (!double.IsFinite(candidate.SpotRadius1Degrees)) throw new ArgumentOutOfRangeException(nameof(geometryLightCandidates));
                if (!double.IsFinite(candidate.SpotRadius2Degrees)) throw new ArgumentOutOfRangeException(nameof(geometryLightCandidates));
                if (!double.IsFinite(candidate.Radius) || candidate.Radius < 0.0) throw new ArgumentOutOfRangeException(nameof(geometryLightCandidates));
                if (!double.IsFinite(candidate.Linearity)) throw new ArgumentOutOfRangeException(nameof(geometryLightCandidates));
                if (!candidate.BoundingBoxesIntersect) continue;
                if (lights.Count >= maxDynamicLightsPerSurface) break;

                double diameter = candidate.Radius * 2.0;
                lights.Add(new Renderer3DGeometryLightSlotPlan(
                    candidate.Id,
                    candidate.SpotLight,
                    candidate.SpotLight ? CosDegrees(candidate.SpotRadius1Degrees) : 0.0f,
                    candidate.SpotLight ? CosDegrees(candidate.SpotRadius2Degrees) : 0.0f,
                    (float)Math.Min(1500.0, diameter * diameter / 10.0),
                    (float)candidate.Linearity));
            }

            bool haveLights = lights.Count > 0;
            bool setUniforms = hadLights != haveLights || haveLights;
            geometry.Add(new Renderer3DGeometryLightUniformPlan(
                geometryIndex,
                lights,
                maxDynamicLightsPerSurface - lights.Count,
                SetLightColorUniform: setUniforms,
                SetLightDetailUniforms: setUniforms && haveLights));
            hadLights = haveLights;
        }

        return new Renderer3DGeometryLightUniformsPlan(geometry);
    }

    public static Renderer3DModelMeshRenderPlan BuildModelMeshRenderPlan(
        IReadOnlyList<Renderer3DModelMeshCandidate> models)
    {
        ArgumentNullException.ThrowIfNull(models);

        var draws = new List<Renderer3DModelMeshDrawPlan>();
        foreach (Renderer3DModelMeshCandidate model in models)
        {
            ArgumentNullException.ThrowIfNull(model.Textures);
            if (model.MeshCount < 0) throw new ArgumentOutOfRangeException(nameof(models));
            if (model.Textures.Count < model.MeshCount) throw new ArgumentOutOfRangeException(nameof(models));

            for (int meshIndex = 0; meshIndex < model.MeshCount; meshIndex++)
            {
                draws.Add(new Renderer3DModelMeshDrawPlan(
                    model.ThingId,
                    meshIndex,
                    model.Textures[meshIndex],
                    SetTexture: true,
                    DrawMesh: true));
            }
        }

        return new Renderer3DModelMeshRenderPlan(
            draws,
            DisableLightsEnabledUniform: true);
    }

    public static Renderer3DThingPositionMatrixPlan BuildThingPositionMatrixPlan(
        ThingRenderMode renderMode,
        ModelRenderMode modelRenderMode,
        bool selected,
        bool xyBillboard)
    {
        ThingRenderMode effectiveRenderMode = renderMode;
        bool demotedModelRenderMode = false;
        if (renderMode is ThingRenderMode.MODEL or ThingRenderMode.VOXEL &&
            (modelRenderMode == ModelRenderMode.NONE ||
             modelRenderMode == ModelRenderMode.SELECTION && !selected))
        {
            effectiveRenderMode = ThingRenderMode.NORMAL;
            demotedModelRenderMode = true;
        }

        Renderer3DThingPositionMatrixStrategy strategy = effectiveRenderMode switch
        {
            ThingRenderMode.NORMAL => xyBillboard
                ? Renderer3DThingPositionMatrixStrategy.XYBillboard
                : Renderer3DThingPositionMatrixStrategy.Billboard,
            ThingRenderMode.FLATSPRITE or ThingRenderMode.WALLSPRITE or ThingRenderMode.MODEL or ThingRenderMode.VOXEL
                => Renderer3DThingPositionMatrixStrategy.DirectPosition,
            _ => throw new ArgumentOutOfRangeException(nameof(renderMode)),
        };

        return new Renderer3DThingPositionMatrixPlan(
            renderMode,
            effectiveRenderMode,
            strategy,
            demotedModelRenderMode);
    }

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

    public static bool BoundingBoxesIntersect(IReadOnlyList<Vector3D> firstBoundingBox, IReadOnlyList<Vector3D> secondBoundingBox)
    {
        ArgumentNullException.ThrowIfNull(firstBoundingBox);
        ArgumentNullException.ThrowIfNull(secondBoundingBox);
        if (firstBoundingBox.Count < 2) throw new ArgumentException("Bounding boxes must contain center and half-size corner points.", nameof(firstBoundingBox));
        if (secondBoundingBox.Count < 2) throw new ArgumentException("Bounding boxes must contain center and half-size corner points.", nameof(secondBoundingBox));
        if (!IsFinite(firstBoundingBox[0]) || !IsFinite(firstBoundingBox[1])) throw new ArgumentOutOfRangeException(nameof(firstBoundingBox));
        if (!IsFinite(secondBoundingBox[0]) || !IsFinite(secondBoundingBox[1])) throw new ArgumentOutOfRangeException(nameof(secondBoundingBox));

        Vector3D dist = firstBoundingBox[0] - secondBoundingBox[0];
        Vector3D halfSize1 = firstBoundingBox[0] - firstBoundingBox[1];
        Vector3D halfSize2 = secondBoundingBox[0] - secondBoundingBox[1];

        return halfSize1.x + halfSize2.x >= Math.Abs(dist.x) &&
            halfSize1.y + halfSize2.y >= Math.Abs(dist.y) &&
            halfSize1.z + halfSize2.z >= Math.Abs(dist.z);
    }

    private static WorldVertex Vertex(Vector3D position, int color)
        => new((float)position.x, (float)position.y, (float)position.z, color, u: 0.0f, v: 0.0f);

    private static int CompareTranslucentGeometry(
        Renderer3DTranslucentGeometryCandidate first,
        Renderer3DTranslucentGeometryCandidate second,
        Vector3D cameraPosition)
    {
        if (first == second) return 0;

        bool comparePlanes = IsPlaneGeometry(first.GeometryType) || IsPlaneGeometry(second.GeometryType);
        double firstDistance = comparePlanes
            ? Math.Abs(first.BoundingBoxCenter.z - cameraPosition.z)
            : (cameraPosition - first.BoundingBoxCenter).GetLengthSq();
        double secondDistance = comparePlanes
            ? Math.Abs(second.BoundingBoxCenter.z - cameraPosition.z)
            : (cameraPosition - second.BoundingBoxCenter).GetLengthSq();
        return secondDistance.CompareTo(firstDistance);
    }

    private static bool IsPlaneGeometry(Renderer3DVisualGeometryType geometryType)
        => geometryType is Renderer3DVisualGeometryType.Floor or Renderer3DVisualGeometryType.Ceiling;

    private static float CosDegrees(double angleDegrees)
        => (float)Math.Cos(angleDegrees * Math.PI / 180.0);

    private static float BuildInverseSquareDistanceAttenuation(float distance, float radius, float strength, float linearity)
    {
        float a = distance / radius;
        float b = Clamp(1.0f - a * a * a * a, 0.0f, 1.0f);
        return Mix((b * b) / (distance * distance + 1.0f) * strength, Clamp((radius - distance) / radius, 0.0f, 1.0f), linearity);
    }

    private static float Smoothstep(float edge0, float edge1, float value)
    {
        double t = Math.Min(Math.Max((value - edge0) / (edge1 - edge0), 0.0), 1.0);
        return (float)(t * t * (3.0 - 2.0 * t));
    }

    private static float Clamp(float value, float minimum, float maximum)
        => Math.Min(Math.Max(value, minimum), maximum);

    private static float Mix(float first, float second, float amount)
        => first * (1.0f - amount) + second * amount;

    private static bool IsFinite(Vector3f vector)
        => float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);

    private static bool IsFinite(Vector3D vector)
        => double.IsFinite(vector.x) && double.IsFinite(vector.y) && double.IsFinite(vector.z);

    private static bool IsFinite(Color4 color)
        => float.IsFinite(color.Red) && float.IsFinite(color.Green) && float.IsFinite(color.Blue) && float.IsFinite(color.Alpha);
}
