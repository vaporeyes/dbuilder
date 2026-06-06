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
