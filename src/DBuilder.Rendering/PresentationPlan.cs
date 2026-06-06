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
    bool ResetGridSize)
{
    public const int ThingBufferSize = 100;
    public const int ThingVerticesPerBufferItem = 12;

    public static PresentationRenderTargetPlan Create(int width, int height, PresentationPlan? presentation)
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
            ResetGridSize: true);
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
}
