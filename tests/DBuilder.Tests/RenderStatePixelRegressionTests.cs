// ABOUTME: Adds pixel-level regression coverage for renderer state planning.
// ABOUTME: Verifies blend and mask render states produce stable composited pixels.

using DBuilder.IO;
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
