// ABOUTME: Adds pixel-level regression coverage for renderer state planning.
// ABOUTME: Verifies blend and mask render states produce stable composited pixels.

using DBuilder.IO;
using DBuilder.Geometry;
using DBuilder.Map;
using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class RenderStatePixelRegressionTests
{
    [Fact]
    public void AlphaPresentationLayerCompositesPixelsWithUdbBlendFactors()
    {
        PresentationDrawCommand command = PresentationDrawCommand.FromLayer(
            new PresentationLayer(PresentationRendererLayer.Things, PresentationBlendingMode.Alpha, Alpha: 0.25f),
            qualityDisplay: false,
            overlayIndex: 0);

        PixelColor pixel = Composite(command, source: new PixelColor(255, 200, 80, 40), destination: new PixelColor(255, 20, 40, 100));

        Assert.True(command.AlphaBlendEnabled);
        Assert.False(command.AlphaTestEnabled);
        Assert.Equal(Blend.SourceAlpha, command.SourceBlend);
        Assert.Equal(Blend.InverseSourceAlpha, command.DestinationBlend);
        Assert.Equal(new PixelColor(255, 65, 50, 85), pixel);
    }

    [Fact]
    public void AdditivePresentationLayerClampsBrightPixelsLikeUdb()
    {
        PresentationDrawCommand command = PresentationDrawCommand.FromLayer(
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Additive, Alpha: 0.75f),
            qualityDisplay: false,
            overlayIndex: 0);

        PixelColor pixel = Composite(command, source: new PixelColor(255, 240, 120, 80), destination: new PixelColor(255, 40, 160, 210));

        Assert.True(command.AlphaBlendEnabled);
        Assert.False(command.AlphaTestEnabled);
        Assert.Equal(Blend.SourceAlpha, command.SourceBlend);
        Assert.Equal(Blend.One, command.DestinationBlend);
        Assert.Equal(new PixelColor(255, 220, 250, 255), pixel);
    }

    [Fact]
    public void MaskPresentationLayerKeepsOpaquePixelsAndRejectsTransparentPixels()
    {
        PresentationDrawCommand command = PresentationDrawCommand.FromLayer(
            new PresentationLayer(PresentationRendererLayer.Surface, PresentationBlendingMode.Mask),
            qualityDisplay: false,
            overlayIndex: 0);
        PixelColor destination = new(255, 10, 20, 30);

        PixelColor opaque = Composite(command, source: new PixelColor(255, 200, 80, 40), destination);
        PixelColor transparent = Composite(command, source: new PixelColor(0, 200, 80, 40), destination);

        Assert.False(command.AlphaBlendEnabled);
        Assert.True(command.AlphaTestEnabled);
        Assert.Equal(new PixelColor(255, 200, 80, 40), opaque);
        Assert.Equal(destination, transparent);
    }

    [Fact]
    public void StandardPresentationStackCompositesThingsBeforeGeometry()
    {
        IReadOnlyList<PresentationDrawCommand> commands = PresentationPlan
            .Standard(backgroundAlpha: 1.0f, inactiveThingsAlpha: 0.25f)
            .BuildDrawCommands(qualityDisplay: false);
        PixelColor pixel = new(255, 0, 0, 0);

        pixel = Composite(commands[0], source: new PixelColor(255, 20, 40, 80), pixel);
        pixel = Composite(commands[1], source: new PixelColor(0, 200, 0, 0), pixel);
        pixel = Composite(commands[2], source: new PixelColor(255, 220, 20, 20), pixel);
        Assert.Equal(new PixelColor(255, 70, 35, 65), pixel);
        pixel = Composite(commands[3], source: new PixelColor(0, 255, 255, 255), pixel);
        pixel = Composite(commands[4], source: new PixelColor(255, 40, 180, 60), pixel);

        Assert.Equal(
            [
                PresentationRendererLayer.Background,
                PresentationRendererLayer.Surface,
                PresentationRendererLayer.Things,
                PresentationRendererLayer.Grid,
                PresentationRendererLayer.Geometry,
                PresentationRendererLayer.Overlay,
            ],
            commands.Select(command => command.Layer).ToArray());
        Assert.Equal(new PixelColor(255, 40, 180, 60), pixel);
    }

    [Fact]
    public void QualityDisplayUsesFsaaCommandsWithoutChangingStandardPixelComposite()
    {
        IReadOnlyList<PresentationDrawCommand> commands = PresentationPlan
            .Standard(backgroundAlpha: 1.0f, inactiveThingsAlpha: 0.25f)
            .BuildDrawCommands(qualityDisplay: true);
        PixelColor pixel = new(255, 0, 0, 0);

        pixel = Composite(commands[0], source: new PixelColor(255, 20, 40, 80), pixel);
        pixel = Composite(commands[1], source: new PixelColor(0, 200, 0, 0), pixel);
        pixel = Composite(commands[2], source: new PixelColor(255, 220, 20, 20), pixel);
        pixel = Composite(commands[3], source: new PixelColor(0, 255, 255, 255), pixel);
        pixel = Composite(commands[4], source: new PixelColor(255, 40, 180, 60), pixel);

        Assert.Equal(PresentationPlan.Display2DFsaaShaderName, commands[4].ShaderName);
        Assert.Equal(PresentationPlan.Display2DFsaaShaderName, commands[5].ShaderName);
        Assert.Equal(new PixelColor(255, 40, 180, 60), pixel);
    }

    [Fact]
    public void Translucent3DGeometryCompositesAlphaAndAdditivePassesBackToFront()
    {
        Renderer3DTranslucentGeometryOrderPlan plan = Renderer3DGeometryLifecyclePlan.BuildTranslucentGeometryOrderPlan(
            [
                new Renderer3DTranslucentGeometryCandidate(1, Renderer3DVisualGeometryType.WallMiddle, new Vector3D(4, 0, 0), RenderPass.Alpha),
                new Renderer3DTranslucentGeometryCandidate(2, Renderer3DVisualGeometryType.WallMiddle, new Vector3D(3, 0, 0), RenderPass.Additive),
                new Renderer3DTranslucentGeometryCandidate(3, Renderer3DVisualGeometryType.WallMiddle, new Vector3D(2, 0, 0), RenderPass.Alpha),
            ],
            new Vector3D());
        var sourceColors = new Dictionary<int, PixelColor>
        {
            [1] = new(128, 100, 60, 20),
            [2] = new(128, 80, 100, 120),
            [3] = new(128, 200, 40, 10),
        };
        PixelColor pixel = new(255, 10, 20, 30);

        foreach (Renderer3DTranslucentGeometryDrawPlan draw in plan.Draws)
            pixel = Composite3D(draw.RenderPass, sourceColors[draw.GeometryId], pixel);

        Assert.Equal(
            [
                new Renderer3DTranslucentGeometryDrawPlan(1, RenderPass.Alpha, Blend.InverseSourceAlpha),
                new Renderer3DTranslucentGeometryDrawPlan(2, RenderPass.Additive, Blend.One),
                new Renderer3DTranslucentGeometryDrawPlan(3, RenderPass.Alpha, Blend.InverseSourceAlpha),
            ],
            plan.Draws);
        Assert.Equal(new PixelColor(255, 147, 64, 46), pixel);
    }

    [Fact]
    public void ThingsPresentationStackReappliesThingsAfterGeometryLikeUdb()
    {
        IReadOnlyList<PresentationDrawCommand> commands = PresentationPlan
            .Things(backgroundAlpha: 1.0f)
            .BuildDrawCommands(qualityDisplay: false);
        PixelColor pixel = new(255, 0, 0, 0);

        pixel = Composite(commands[0], source: new PixelColor(255, 20, 40, 80), pixel);
        pixel = Composite(commands[1], source: new PixelColor(0, 200, 0, 0), pixel);
        pixel = Composite(commands[2], source: new PixelColor(255, 220, 20, 20), pixel);
        pixel = Composite(commands[3], source: new PixelColor(0, 255, 255, 255), pixel);
        pixel = Composite(commands[4], source: new PixelColor(255, 40, 180, 60), pixel);
        pixel = Composite(commands[5], source: new PixelColor(255, 220, 20, 20), pixel);

        Assert.Equal(
            [
                PresentationRendererLayer.Background,
                PresentationRendererLayer.Surface,
                PresentationRendererLayer.Things,
                PresentationRendererLayer.Grid,
                PresentationRendererLayer.Geometry,
                PresentationRendererLayer.Things,
                PresentationRendererLayer.Overlay,
            ],
            commands.Select(command => command.Layer).ToArray());
        Assert.Equal(1.0f, commands[2].Alpha);
        Assert.Equal(0.5f, commands[5].Alpha);
        Assert.Equal(new PixelColor(255, 130, 100, 40), pixel);
    }

    [Fact]
    public void AutomapPresentationStackCompositesMaskedLayersBeforeGeometry()
    {
        AutomapPresentationDescriptor presentation = AutomapModeModel.Presentation;
        PixelColor pixel = new(255, 0, 0, 0);

        pixel = CompositeAutomap(presentation.Layers[0], source: new PixelColor(255, 20, 40, 80), pixel);
        pixel = CompositeAutomap(presentation.Layers[1], source: new PixelColor(0, 200, 0, 0), pixel);
        pixel = CompositeAutomap(presentation.Layers[2], source: new PixelColor(255, 10, 80, 110), pixel);
        pixel = CompositeAutomap(presentation.Layers[3], source: new PixelColor(255, 240, 30, 20), pixel);

        Assert.False(presentation.DrawMapCenter);
        Assert.True(presentation.SkipHiddenSectors);
        Assert.Equal(
            [
                AutomapPresentationLayerKind.Surface,
                AutomapPresentationLayerKind.Overlay,
                AutomapPresentationLayerKind.Grid,
                AutomapPresentationLayerKind.Geometry,
            ],
            presentation.Layers.Select(layer => layer.Kind).ToArray());
        Assert.Equal(new PixelColor(255, 240, 30, 20), pixel);
    }

    [Fact]
    public void FarOverviewThingMarkersCullOverlappingPixelsToSelectedRepresentative()
    {
        const int width = 64;
        const int height = 64;
        const double viewScale = ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold;
        const int selectedColor = unchecked((int)0xffffee00);
        const int normalColor = unchecked((int)0xffd0d0d0);

        var things = new[]
        {
            new ThingMarkerPixelCandidate(ScreenX: 20, ScreenY: 20, MapRadius: 64, Selected: false),
            new ThingMarkerPixelCandidate(ScreenX: 22, ScreenY: 22, MapRadius: 16, Selected: true),
            new ThingMarkerPixelCandidate(ScreenX: 24, ScreenY: 24, MapRadius: 128, Selected: false),
        };

        int[] pixels = DrawOverviewThingMarkers(things, width, height, viewScale, selectedColor, normalColor);

        Assert.Equal(13, pixels.Count(pixel => pixel == selectedColor));
        Assert.DoesNotContain(normalColor, pixels);
        Assert.Equal(selectedColor, pixels[22 + 22 * width]);
    }

    private static PixelColor Composite(PresentationDrawCommand command, PixelColor source, PixelColor destination)
    {
        if (command.AlphaTestEnabled && source.A == 0)
            return destination;

        if (!command.AlphaBlendEnabled)
            return source;

        float sourceFactor = command.SourceBlend == Blend.SourceAlpha ? command.Alpha : 1.0f;
        float destinationFactor = command.DestinationBlend switch
        {
            Blend.InverseSourceAlpha => 1.0f - command.Alpha,
            Blend.One => 1.0f,
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

        return new PixelColor(
            255,
            Channel(source.R, sourceFactor, destination.R, destinationFactor),
            Channel(source.G, sourceFactor, destination.G, destinationFactor),
            Channel(source.B, sourceFactor, destination.B, destinationFactor));
    }

    private static PixelColor CompositeAutomap(AutomapPresentationLayer layer, PixelColor source, PixelColor destination)
    {
        if (layer.BlendMode == AutomapPresentationBlendMode.Mask)
            return source.A == 0 ? destination : source;

        float sourceFactor = (float)layer.Alpha;
        float destinationFactor = 1.0f - sourceFactor;
        return new PixelColor(
            255,
            Channel(source.R, sourceFactor, destination.R, destinationFactor),
            Channel(source.G, sourceFactor, destination.G, destinationFactor),
            Channel(source.B, sourceFactor, destination.B, destinationFactor));
    }

    private static PixelColor Composite3D(RenderPass renderPass, PixelColor source, PixelColor destination)
    {
        float sourceFactor = source.A / 255.0f;
        float destinationFactor = renderPass == RenderPass.Additive ? 1.0f : 1.0f - sourceFactor;
        return new PixelColor(
            255,
            Channel(source.R, sourceFactor, destination.R, destinationFactor),
            Channel(source.G, sourceFactor, destination.G, destinationFactor),
            Channel(source.B, sourceFactor, destination.B, destinationFactor));
    }

    private static byte Channel(byte source, float sourceFactor, byte destination, float destinationFactor)
        => (byte)Math.Clamp((int)(source * sourceFactor + destination * destinationFactor), 0, 255);

    private static int[] DrawOverviewThingMarkers(
        IReadOnlyList<ThingMarkerPixelCandidate> things,
        int width,
        int height,
        double viewScale,
        int selectedColor,
        int normalColor)
    {
        var representatives = new Dictionary<(int X, int Y), ThingMarkerPixelCandidate>();
        foreach (ThingMarkerPixelCandidate thing in things)
        {
            var cell = (
                ThingIconRenderPolicy.OverviewCullCell(thing.ScreenX, viewScale, thingArrows: false),
                ThingIconRenderPolicy.OverviewCullCell(thing.ScreenY, viewScale, thingArrows: false));
            if (!representatives.TryGetValue(cell, out ThingMarkerPixelCandidate existing)
                || ThingIconRenderPolicy.ShouldReplaceOverviewCellThing(
                    existing.Selected,
                    existing.MapRadius,
                    thing.Selected,
                    thing.MapRadius))
            {
                representatives[cell] = thing;
            }
        }

        var pixels = new int[width * height];
        foreach (ThingMarkerPixelCandidate thing in representatives.Values)
        {
            int color = thing.Selected ? selectedColor : normalColor;
            DrawDiamond(pixels, width, height, thing.ScreenX, thing.ScreenY, radius: 2, color);
        }

        return pixels;
    }

    private static void DrawDiamond(int[] pixels, int width, int height, int centerX, int centerY, int radius, int color)
    {
        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            if (y < 0 || y >= height) continue;
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                if (x < 0 || x >= width) continue;
                if (Math.Abs(x - centerX) + Math.Abs(y - centerY) > radius) continue;
                pixels[x + y * width] = color;
            }
        }
    }

    private readonly record struct ThingMarkerPixelCandidate(
        int ScreenX,
        int ScreenY,
        double MapRadius,
        bool Selected);
}
