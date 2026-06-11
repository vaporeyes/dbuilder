// ABOUTME: Models UDB-style 2D renderer presentation layer stacks.
// ABOUTME: Keeps Standard, Things, and custom presentation behavior testable outside live rendering.

namespace DBuilder.Rendering;

public enum PresentationRendererLayer
{
    Background,
    Grid,
    Things,
    Geometry,
    Overlay,
    Surface,
}

public enum PresentationRenderLayerMask
{
    None = 0,
    Background = 1,
    Plotter = 2,
    Things = 3,
    Overlay = 4,
    Surface = 5,
}

public enum PresentationBlendingMode
{
    None,
    Mask,
    Alpha,
    Additive,
}

public readonly record struct PresentationLayer(
    PresentationRendererLayer Layer,
    PresentationBlendingMode Blending,
    float Alpha = 1.0f,
    bool Antialiasing = false);

public sealed record PresentationPlan(
    IReadOnlyList<PresentationLayer> Layers,
    bool SkipHiddenSectors = false)
{
    public const string Display2DNormalShaderName = "display2d_normal";
    public const string Display2DFsaaShaderName = "display2d_fsaa";
    public const float ThingsBackAlpha = 0.3f;
    public const float ThingsHiddenAlpha = 0.66f;
    public const float ThingsAlpha = 1.0f;

    public static PresentationPlan Standard(float backgroundAlpha, float inactiveThingsAlpha)
        => new(new[]
        {
            new PresentationLayer(PresentationRendererLayer.Background, PresentationBlendingMode.Mask, backgroundAlpha),
            new PresentationLayer(PresentationRendererLayer.Surface, PresentationBlendingMode.Mask),
            new PresentationLayer(PresentationRendererLayer.Things, PresentationBlendingMode.Alpha, inactiveThingsAlpha),
            new PresentationLayer(PresentationRendererLayer.Grid, PresentationBlendingMode.Mask),
            new PresentationLayer(PresentationRendererLayer.Geometry, PresentationBlendingMode.Alpha, 1.0f, Antialiasing: true),
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Alpha, 1.0f, Antialiasing: true),
        });

    public static PresentationPlan Things(float backgroundAlpha)
        => new(new[]
        {
            new PresentationLayer(PresentationRendererLayer.Background, PresentationBlendingMode.Mask, backgroundAlpha),
            new PresentationLayer(PresentationRendererLayer.Surface, PresentationBlendingMode.Mask),
            new PresentationLayer(PresentationRendererLayer.Things, PresentationBlendingMode.Alpha, ThingsAlpha),
            new PresentationLayer(PresentationRendererLayer.Grid, PresentationBlendingMode.Mask),
            new PresentationLayer(PresentationRendererLayer.Geometry, PresentationBlendingMode.Alpha, 1.0f, Antialiasing: true),
            new PresentationLayer(PresentationRendererLayer.Things, PresentationBlendingMode.Alpha, 0.5f),
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Alpha, 1.0f, Antialiasing: true),
        });

    public PresentationPlan AddLayer(PresentationLayer layer)
        => this with { Layers = Layers.Concat(new[] { layer }).ToArray() };

    public PresentationPlan WithSkipHiddenSectors(bool skipHiddenSectors)
        => this with { SkipHiddenSectors = skipHiddenSectors };

    public static PresentationRenderLayerMask RenderLayerMaskFor(PresentationRendererLayer layer)
        => layer switch
        {
            PresentationRendererLayer.Background => PresentationRenderLayerMask.Background,
            PresentationRendererLayer.Grid => PresentationRenderLayerMask.Plotter,
            PresentationRendererLayer.Geometry => PresentationRenderLayerMask.Plotter,
            PresentationRendererLayer.Things => PresentationRenderLayerMask.Things,
            PresentationRendererLayer.Overlay => PresentationRenderLayerMask.Overlay,
            PresentationRendererLayer.Surface => PresentationRenderLayerMask.Surface,
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, null),
        };

    public IReadOnlyList<PresentationDrawCommand> BuildDrawCommands(bool qualityDisplay)
    {
        var commands = new List<PresentationDrawCommand>(Layers.Count);
        int overlayIndex = 0;

        foreach (PresentationLayer layer in Layers)
        {
            commands.Add(PresentationDrawCommand.FromLayer(layer, qualityDisplay, overlayIndex));
            if (layer.Layer == PresentationRendererLayer.Overlay)
                overlayIndex++;
        }

        return commands;
    }
}

public sealed record PresentationDrawCommand(
    PresentationRendererLayer Layer,
    PresentationBlendingMode Blending,
    bool AlphaBlendEnabled,
    bool AlphaTestEnabled,
    bool BlendFactorsApplied,
    Blend SourceBlend,
    Blend DestinationBlend,
    string ShaderName,
    TextureAddress SamplerAddress,
    float Alpha,
    int? OverlayIndex)
{
    public static PresentationDrawCommand FromLayer(
        PresentationLayer layer,
        bool qualityDisplay,
        int overlayIndex)
    {
        bool alphaBlend = layer.Blending is PresentationBlendingMode.Alpha or PresentationBlendingMode.Additive;
        bool alphaTest = layer.Blending == PresentationBlendingMode.Mask;
        Blend destinationBlend = layer.Blending == PresentationBlendingMode.Additive
            ? Blend.One
            : Blend.InverseSourceAlpha;

        return new PresentationDrawCommand(
            layer.Layer,
            layer.Blending,
            alphaBlend,
            alphaTest,
            BlendFactorsApplied: layer.Blending is PresentationBlendingMode.Alpha or PresentationBlendingMode.Additive,
            SourceBlend: Blend.SourceAlpha,
            DestinationBlend: destinationBlend,
            ShaderName: layer.Antialiasing && qualityDisplay
                ? PresentationPlan.Display2DFsaaShaderName
                : PresentationPlan.Display2DNormalShaderName,
            SamplerAddress: layer.Layer == PresentationRendererLayer.Things ? TextureAddress.Clamp : TextureAddress.Wrap,
            layer.Alpha,
            OverlayIndex: layer.Layer == PresentationRendererLayer.Overlay ? overlayIndex : null);
    }
}

public enum PresentationRenderTargetKind
{
    Plotter,
    Texture,
}

public sealed record PresentationRenderTargetResource(
    string Name,
    PresentationRenderTargetKind Kind,
    int Width,
    int Height,
    TextureFormat? Format = null);

public sealed record PresentationSetPlan(
    int ExistingOverlayTextureCount,
    int RequestedOverlayLayerCount,
    bool CopyPresentation,
    bool AddOverlayTexture,
    bool ClearAddedOverlayTexture,
    int OverlayTextureCountAfter);

public enum PresentationRenderTargetLifecycleOperation
{
    Construct,
    Dispose,
    UnloadResource,
    ReloadResource,
    DestroyRenderTargets,
    CreateRenderTargets,
}

public sealed record PresentationRenderTargetLifecyclePlan(
    PresentationRenderTargetLifecycleOperation Operation,
    bool CreateSurfaceManager,
    bool DisposeSurfaceManager,
    bool DestroyRenderTargets,
    bool CreateRenderTargets,
    bool SuppressFinalize,
    bool DisposeThingBatchBuffer,
    bool ResetGridCache);

public enum PresentationRenderTargetDestroyStepKind
{
    DisposeResource,
    ClearReference,
    ResetGridCache,
}

public sealed record PresentationRenderTargetDestroyStep(
    PresentationRenderTargetDestroyStepKind Kind,
    string? TargetName = null);

public sealed record PresentationRenderTargetDestroyPlan(
    int OverlayTextureCount,
    IReadOnlyList<PresentationRenderTargetDestroyStep> Steps,
    float LastGridScaleAfter,
    double LastGridSizeAfter);

public enum PresentationRenderTargetCreateStepKind
{
    DestroyExistingTargets,
    ReadRenderTargetSize,
    AllocateResource,
    ClearTarget,
    CreateVertexBuffer,
    UploadThingsVertexBuffer,
    UploadScreenVertexBuffer,
    ResetGridCache,
    UpdateTransformations,
    RedrawExistingMap,
}

public sealed record PresentationRenderTargetCreateStep(
    PresentationRenderTargetCreateStepKind Kind,
    string? TargetName = null);

public sealed record PresentationRenderTargetCreateSequencePlan(
    PresentationRenderTargetPlan TargetPlan,
    IReadOnlyList<PresentationRenderTargetCreateStep> Steps);

public enum PresentationProjectionTransformKind
{
    WorldView,
    WorldViewFlipY,
}

public sealed record PresentationDisplaySettings(
    PresentationRendererLayer Layer,
    string SourceTargetName,
    string ShaderName,
    float TexelX,
    float TexelY,
    float FsaaFactor,
    float Alpha,
    bool FlipY,
    PresentationProjectionTransformKind ProjectionTransform,
    TextureFilter SamplerFilter,
    int? OverlayIndex);

public enum PresentationDisplaySettingStepKind
{
    SetRenderSettingsUniform,
    SetProjectionUniform,
    SetSamplerFilter,
}

public sealed record PresentationDisplaySettingStep(
    PresentationDisplaySettingStepKind Kind,
    string TargetName);

public readonly record struct PresentationRenderSettingsVector(
    float TexelX,
    float TexelY,
    float FsaaFactor,
    float Alpha);

public enum PresentationLayerDrawStepKind
{
    SetShader,
    SetTexture,
    SetSamplerState,
    ApplyDisplaySettings,
    Draw,
    RestoreScreenVertexBuffer,
    AdvanceOverlayLayer,
}

public sealed record PresentationLayerDrawStep(
    PresentationLayerDrawStepKind Kind,
    string? TargetName = null);

public sealed record PresentationLayerDrawPlan(
    PresentationRendererLayer Layer,
    string SourceTargetName,
    bool Draws,
    string? SkipReason,
    string VertexSourceName,
    string TextureBindingName,
    bool UsesRenderTargetTexture,
    PrimitiveType PrimitiveType,
    int StartVertex,
    int PrimitiveCount,
    bool RestoreScreenVertexBufferAfterDraw,
    int? OverlayIndex);

public enum PresentationFrameOperationKind
{
    PluginPresentBegin,
    StartRendering,
    SetCullNone,
    DisableDepth,
    BindScreenVertexBuffer,
    ResetWorldMatrix,
    DrawLayer,
    FinishRendering,
    PresentSwapChain,
    ReleaseTexture,
    ReleaseVertexBuffer,
}

public sealed record PresentationFrameOperation(
    PresentationFrameOperationKind Kind,
    PresentationRendererLayer? Layer = null,
    string? SourceTargetName = null,
    int? OverlayIndex = null);

public sealed record PresentationFramePlan(
    IReadOnlyList<PresentationFrameOperation> Operations,
    IReadOnlyList<PresentationDrawCommand> DrawCommands,
    IReadOnlyList<PresentationDisplaySettings> DisplaySettings);

public sealed record PresentationFrameStartPlan(
    bool ClearTarget,
    string ClearColorName,
    string CullMode,
    bool DepthEnabled,
    string VertexBufferName,
    string WorldMatrixName);

public sealed record PresentationFrameReleasePlan(
    string TextureBindingAfter,
    string VertexBufferBindingAfter);

public sealed record PresentationRenderTargetPlan(
    int Width,
    int Height,
    int OverlayTextureCount,
    int ThingVertexCapacity,
    int ScreenVertexCapacity,
    IReadOnlyList<PresentationRenderTargetResource> Resources,
    IReadOnlyList<string> ClearTargets,
    FlatVertex[] ScreenVertices,
    bool ResetGridScale,
    bool ResetGridSize,
    bool ResetGridOrigin,
    bool RedrawExistingMap)
{
    public const int ThingBufferSize = 100;
    public const int ThingVerticesPerBufferItem = 12;
    public const float FsaaFactor = 0.6f;

    public static PresentationRenderTargetLifecyclePlan BuildLifecyclePlan(
        PresentationRenderTargetLifecycleOperation operation)
        => operation switch
        {
            PresentationRenderTargetLifecycleOperation.Construct => new(
                operation,
                CreateSurfaceManager: true,
                DisposeSurfaceManager: false,
                DestroyRenderTargets: false,
                CreateRenderTargets: true,
                SuppressFinalize: true,
                DisposeThingBatchBuffer: false,
                ResetGridCache: false),
            PresentationRenderTargetLifecycleOperation.Dispose => new(
                operation,
                CreateSurfaceManager: false,
                DisposeSurfaceManager: true,
                DestroyRenderTargets: true,
                CreateRenderTargets: false,
                SuppressFinalize: false,
                DisposeThingBatchBuffer: true,
                ResetGridCache: true),
            PresentationRenderTargetLifecycleOperation.UnloadResource => new(
                operation,
                CreateSurfaceManager: false,
                DisposeSurfaceManager: false,
                DestroyRenderTargets: true,
                CreateRenderTargets: false,
                SuppressFinalize: false,
                DisposeThingBatchBuffer: true,
                ResetGridCache: true),
            PresentationRenderTargetLifecycleOperation.ReloadResource => new(
                operation,
                CreateSurfaceManager: false,
                DisposeSurfaceManager: false,
                DestroyRenderTargets: false,
                CreateRenderTargets: true,
                SuppressFinalize: false,
                DisposeThingBatchBuffer: false,
                ResetGridCache: false),
            PresentationRenderTargetLifecycleOperation.DestroyRenderTargets => new(
                operation,
                CreateSurfaceManager: false,
                DisposeSurfaceManager: false,
                DestroyRenderTargets: true,
                CreateRenderTargets: false,
                SuppressFinalize: false,
                DisposeThingBatchBuffer: true,
                ResetGridCache: true),
            PresentationRenderTargetLifecycleOperation.CreateRenderTargets => new(
                operation,
                CreateSurfaceManager: false,
                DisposeSurfaceManager: false,
                DestroyRenderTargets: true,
                CreateRenderTargets: true,
                SuppressFinalize: false,
                DisposeThingBatchBuffer: true,
                ResetGridCache: true),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

    public static PresentationRenderTargetCreateSequencePlan BuildCreateSequencePlan(
        int width,
        int height,
        PresentationPlan? presentation,
        bool hasMapConfiguration = false)
    {
        PresentationRenderTargetPlan targetPlan = Create(width, height, presentation, hasMapConfiguration);
        var steps = new List<PresentationRenderTargetCreateStep>(
            targetPlan.Resources.Count + targetPlan.ClearTargets.Count + (hasMapConfiguration ? 10 : 9))
        {
            new(PresentationRenderTargetCreateStepKind.DestroyExistingTargets),
            new(PresentationRenderTargetCreateStepKind.ReadRenderTargetSize),
        };

        foreach (PresentationRenderTargetResource resource in targetPlan.Resources)
            steps.Add(new PresentationRenderTargetCreateStep(PresentationRenderTargetCreateStepKind.AllocateResource, resource.Name));

        foreach (string target in targetPlan.ClearTargets)
            steps.Add(new PresentationRenderTargetCreateStep(PresentationRenderTargetCreateStepKind.ClearTarget, target));

        steps.Add(new PresentationRenderTargetCreateStep(PresentationRenderTargetCreateStepKind.CreateVertexBuffer, "screenverts"));
        steps.Add(new PresentationRenderTargetCreateStep(PresentationRenderTargetCreateStepKind.CreateVertexBuffer, "thingsvertices"));
        steps.Add(new PresentationRenderTargetCreateStep(PresentationRenderTargetCreateStepKind.UploadThingsVertexBuffer, "thingsvertices"));
        steps.Add(new PresentationRenderTargetCreateStep(PresentationRenderTargetCreateStepKind.UploadScreenVertexBuffer, "screenverts"));
        steps.Add(new PresentationRenderTargetCreateStep(PresentationRenderTargetCreateStepKind.ResetGridCache));
        steps.Add(new PresentationRenderTargetCreateStep(PresentationRenderTargetCreateStepKind.UpdateTransformations));

        if (hasMapConfiguration)
            steps.Add(new PresentationRenderTargetCreateStep(PresentationRenderTargetCreateStepKind.RedrawExistingMap));

        return new PresentationRenderTargetCreateSequencePlan(targetPlan, steps);
    }

    public static PresentationRenderTargetDestroyPlan BuildDestroyPlan(int overlayTextureCount)
    {
        if (overlayTextureCount < 0) throw new ArgumentOutOfRangeException(nameof(overlayTextureCount));

        var steps = new List<PresentationRenderTargetDestroyStep>(overlayTextureCount * 2 + 12)
        {
            new(PresentationRenderTargetDestroyStepKind.DisposeResource, "plotter"),
            new(PresentationRenderTargetDestroyStepKind.DisposeResource, "things"),
        };

        for (int i = 0; i < overlayTextureCount; i++)
        {
            string name = "overlay" + i;
            steps.Add(new PresentationRenderTargetDestroyStep(PresentationRenderTargetDestroyStepKind.DisposeResource, name));
            steps.Add(new PresentationRenderTargetDestroyStep(PresentationRenderTargetDestroyStepKind.ClearReference, name));
        }

        steps.Add(new PresentationRenderTargetDestroyStep(PresentationRenderTargetDestroyStepKind.DisposeResource, "surface"));
        steps.Add(new PresentationRenderTargetDestroyStep(PresentationRenderTargetDestroyStepKind.DisposeResource, "gridplotter"));
        steps.Add(new PresentationRenderTargetDestroyStep(PresentationRenderTargetDestroyStepKind.DisposeResource, "screenverts"));
        steps.Add(new PresentationRenderTargetDestroyStep(PresentationRenderTargetDestroyStepKind.ClearReference, "things"));
        steps.Add(new PresentationRenderTargetDestroyStep(PresentationRenderTargetDestroyStepKind.ClearReference, "gridplotter"));
        steps.Add(new PresentationRenderTargetDestroyStep(PresentationRenderTargetDestroyStepKind.ClearReference, "screenverts"));
        steps.Add(new PresentationRenderTargetDestroyStep(PresentationRenderTargetDestroyStepKind.ClearReference, "overlaytex"));
        steps.Add(new PresentationRenderTargetDestroyStep(PresentationRenderTargetDestroyStepKind.ClearReference, "surface"));
        steps.Add(new PresentationRenderTargetDestroyStep(PresentationRenderTargetDestroyStepKind.DisposeResource, "thingsvertices"));
        steps.Add(new PresentationRenderTargetDestroyStep(PresentationRenderTargetDestroyStepKind.ClearReference, "thingsvertices"));
        steps.Add(new PresentationRenderTargetDestroyStep(PresentationRenderTargetDestroyStepKind.ResetGridCache));

        return new PresentationRenderTargetDestroyPlan(
            overlayTextureCount,
            steps,
            LastGridScaleAfter: -1.0f,
            LastGridSizeAfter: 0.0);
    }

    public static PresentationSetPlan BuildSetPresentationPlan(
        PresentationPlan presentation,
        int existingOverlayTextureCount)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (existingOverlayTextureCount < 0) throw new ArgumentOutOfRangeException(nameof(existingOverlayTextureCount));

        int requestedOverlayLayerCount = presentation.Layers.Count(layer => layer.Layer == PresentationRendererLayer.Overlay);
        bool addOverlayTexture = requestedOverlayLayerCount > existingOverlayTextureCount;

        return new PresentationSetPlan(
            existingOverlayTextureCount,
            requestedOverlayLayerCount,
            CopyPresentation: true,
            addOverlayTexture,
            ClearAddedOverlayTexture: addOverlayTexture,
            addOverlayTexture ? existingOverlayTextureCount + 1 : existingOverlayTextureCount);
    }

    public static PresentationRenderTargetPlan Create(
        int width,
        int height,
        PresentationPlan? presentation,
        bool hasMapConfiguration = false)
    {
        int overlayCount = presentation?.Layers.Count(layer => layer.Layer == PresentationRendererLayer.Overlay) ?? 1;

        return new PresentationRenderTargetPlan(
            width,
            height,
            overlayCount,
            ThingBufferSize * ThingVerticesPerBufferItem,
            4,
            ResourcesFor(width, height, overlayCount),
            ClearTargetsFor(overlayCount),
            CreateScreenVertices(width, height),
            ResetGridScale: true,
            ResetGridSize: true,
            ResetGridOrigin: true,
            RedrawExistingMap: hasMapConfiguration);
    }

    private static IReadOnlyList<PresentationRenderTargetResource> ResourcesFor(int width, int height, int overlayCount)
    {
        var resources = new List<PresentationRenderTargetResource>(overlayCount + 4)
        {
            new("plotter", PresentationRenderTargetKind.Plotter, width, height),
            new("gridplotter", PresentationRenderTargetKind.Plotter, width, height),
            new("things", PresentationRenderTargetKind.Texture, width, height, TextureFormat.Rgba8),
            new("surface", PresentationRenderTargetKind.Texture, width, height, TextureFormat.Rgba8),
        };

        for (int i = 0; i < overlayCount; i++)
            resources.Add(new PresentationRenderTargetResource("overlay" + i, PresentationRenderTargetKind.Texture, width, height, TextureFormat.Rgba8));

        return resources;
    }

    private static IReadOnlyList<string> ClearTargetsFor(int overlayCount)
    {
        var targets = new List<string>(overlayCount + 1) { "things" };
        for (int i = 0; i < overlayCount; i++)
            targets.Add("overlay" + i);
        return targets;
    }

    private static FlatVertex[] CreateScreenVertices(int width, int height)
        => new[]
        {
            ScreenVertex(0.0f, 0.0f, 0.0f, 0.0f),
            ScreenVertex(width, 0.0f, 1.0f, 0.0f),
            ScreenVertex(0.0f, height, 0.0f, 1.0f),
            ScreenVertex(width, height, 1.0f, 1.0f),
        };

    private static FlatVertex ScreenVertex(float x, float y, float u, float v)
        => new()
        {
            x = x,
            y = y,
            z = 0.0f,
            w = 1.0f,
            c = -1,
            u = u,
            v = v,
        };

    public IReadOnlyList<PresentationDisplaySettings> BuildDisplaySettings(
        PresentationPlan presentation,
        bool qualityDisplay,
        bool bilinear = false)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        var settings = new List<PresentationDisplaySettings>(presentation.Layers.Count);
        int overlayIndex = 0;

        foreach (PresentationLayer layer in presentation.Layers)
        {
            int? currentOverlay = layer.Layer == PresentationRendererLayer.Overlay ? overlayIndex : null;
            settings.Add(DisplaySettingsFor(layer, qualityDisplay, bilinear, currentOverlay));
            if (layer.Layer == PresentationRendererLayer.Overlay)
                overlayIndex++;
        }

        return settings;
    }

    private PresentationDisplaySettings DisplaySettingsFor(
        PresentationLayer layer,
        bool qualityDisplay,
        bool bilinear,
        int? overlayIndex)
    {
        string sourceName = SourceTargetName(layer.Layer, overlayIndex);
        PresentationRenderTargetResource? resource = Resources.FirstOrDefault(resource => resource.Name == sourceName);
        int sourceWidth = resource?.Width ?? Width;
        int sourceHeight = resource?.Height ?? Height;

        return new PresentationDisplaySettings(
            layer.Layer,
            sourceName,
            layer.Antialiasing && qualityDisplay
                ? PresentationPlan.Display2DFsaaShaderName
                : PresentationPlan.Display2DNormalShaderName,
            1.0f / sourceWidth,
            1.0f / sourceHeight,
            FsaaFactor,
            layer.Alpha,
            FlipY(layer.Layer),
            ProjectionTransformFor(layer.Layer),
            bilinear ? TextureFilter.Linear : TextureFilter.Nearest,
            overlayIndex);
    }

    private static string SourceTargetName(PresentationRendererLayer layer, int? overlayIndex)
        => layer switch
        {
            PresentationRendererLayer.Background => "background",
            PresentationRendererLayer.Grid => "gridplotter",
            PresentationRendererLayer.Geometry => "plotter",
            PresentationRendererLayer.Things => "things",
            PresentationRendererLayer.Overlay => "overlay" + (overlayIndex ?? 0),
            PresentationRendererLayer.Surface => "surface",
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, null),
        };

    private static bool FlipY(PresentationRendererLayer layer)
        => layer != PresentationRendererLayer.Geometry;

    private static PresentationProjectionTransformKind ProjectionTransformFor(PresentationRendererLayer layer)
        => FlipY(layer)
            ? PresentationProjectionTransformKind.WorldViewFlipY
            : PresentationProjectionTransformKind.WorldView;

    public IReadOnlyList<PresentationDisplaySettingStep> BuildDisplaySettingSteps(
        PresentationDisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new[]
        {
            new PresentationDisplaySettingStep(PresentationDisplaySettingStepKind.SetRenderSettingsUniform, "rendersettings"),
            new PresentationDisplaySettingStep(PresentationDisplaySettingStepKind.SetProjectionUniform, "projection"),
            new PresentationDisplaySettingStep(PresentationDisplaySettingStepKind.SetSamplerFilter, settings.SamplerFilter.ToString()),
        };
    }

    public static PresentationRenderSettingsVector BuildRenderSettingsVector(
        PresentationDisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new PresentationRenderSettingsVector(settings.TexelX, settings.TexelY, settings.FsaaFactor, settings.Alpha);
    }

    public static IReadOnlyList<PresentationLayerDrawStep> BuildLayerDrawSteps(
        PresentationLayerDrawPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.Draws) return Array.Empty<PresentationLayerDrawStep>();

        var steps = new List<PresentationLayerDrawStep>
        {
            new(PresentationLayerDrawStepKind.SetShader),
            new(PresentationLayerDrawStepKind.SetTexture, plan.TextureBindingName),
            new(PresentationLayerDrawStepKind.SetSamplerState, plan.UsesRenderTargetTexture ? plan.SourceTargetName : plan.TextureBindingName),
            new(PresentationLayerDrawStepKind.ApplyDisplaySettings, plan.SourceTargetName),
            new(PresentationLayerDrawStepKind.Draw, plan.VertexSourceName),
        };

        if (plan.RestoreScreenVertexBufferAfterDraw)
            steps.Add(new PresentationLayerDrawStep(PresentationLayerDrawStepKind.RestoreScreenVertexBuffer, "screenverts"));

        if (plan.Layer == PresentationRendererLayer.Overlay)
            steps.Add(new PresentationLayerDrawStep(PresentationLayerDrawStepKind.AdvanceOverlayLayer));

        return steps;
    }

    public static PresentationFrameStartPlan BuildFrameStartPlan()
        => new(
            ClearTarget: true,
            ClearColorName: "background",
            CullMode: "none",
            DepthEnabled: false,
            VertexBufferName: "screenverts",
            WorldMatrixName: "identity");

    public static PresentationFrameReleasePlan BuildFrameReleasePlan()
        => new(
            TextureBindingAfter: "null",
            VertexBufferBindingAfter: "null");

    public IReadOnlyList<PresentationLayerDrawPlan> BuildLayerDrawPlans(
        PresentationPlan presentation,
        bool qualityDisplay,
        bool hasBackgroundVertices,
        bool hasBackgroundTexture,
        bool bilinear = false)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        IReadOnlyList<PresentationDisplaySettings> settings = BuildDisplaySettings(presentation, qualityDisplay, bilinear);
        var plans = new List<PresentationLayerDrawPlan>(settings.Count);

        foreach (PresentationDisplaySettings setting in settings)
        {
            bool skipBackground = setting.Layer == PresentationRendererLayer.Background
                && (!hasBackgroundVertices || !hasBackgroundTexture);
            plans.Add(new PresentationLayerDrawPlan(
                setting.Layer,
                setting.SourceTargetName,
                Draws: !skipBackground,
                SkipReason: skipBackground ? "Missing background vertices or texture" : null,
                VertexSourceName: setting.Layer == PresentationRendererLayer.Background ? "backimageverts" : "screenverts",
                TextureBindingName: setting.Layer == PresentationRendererLayer.Background ? "map-grid-background" : setting.SourceTargetName,
                UsesRenderTargetTexture: setting.Layer != PresentationRendererLayer.Background,
                PrimitiveType: PrimitiveType.TriangleStrip,
                StartVertex: 0,
                PrimitiveCount: 2,
                RestoreScreenVertexBufferAfterDraw: setting.Layer == PresentationRendererLayer.Background && !skipBackground,
                setting.OverlayIndex));
        }

        return plans;
    }

    public PresentationFramePlan BuildFramePlan(
        PresentationPlan presentation,
        bool qualityDisplay,
        bool bilinear = false)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        IReadOnlyList<PresentationDrawCommand> commands = presentation.BuildDrawCommands(qualityDisplay);
        IReadOnlyList<PresentationDisplaySettings> settings = BuildDisplaySettings(presentation, qualityDisplay, bilinear);
        var operations = new List<PresentationFrameOperation>(settings.Count + 9)
        {
            new(PresentationFrameOperationKind.PluginPresentBegin),
            new(PresentationFrameOperationKind.StartRendering),
            new(PresentationFrameOperationKind.SetCullNone),
            new(PresentationFrameOperationKind.DisableDepth),
            new(PresentationFrameOperationKind.BindScreenVertexBuffer),
            new(PresentationFrameOperationKind.ResetWorldMatrix),
        };

        foreach (PresentationDisplaySettings setting in settings)
            operations.Add(new PresentationFrameOperation(
                PresentationFrameOperationKind.DrawLayer,
                setting.Layer,
                setting.SourceTargetName,
                setting.OverlayIndex));

        operations.Add(new PresentationFrameOperation(PresentationFrameOperationKind.FinishRendering));
        operations.Add(new PresentationFrameOperation(PresentationFrameOperationKind.PresentSwapChain));
        operations.Add(new PresentationFrameOperation(PresentationFrameOperationKind.ReleaseTexture));
        operations.Add(new PresentationFrameOperation(PresentationFrameOperationKind.ReleaseVertexBuffer));

        return new PresentationFramePlan(operations, commands, settings);
    }
}
