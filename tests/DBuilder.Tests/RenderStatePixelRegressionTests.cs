// ABOUTME: Adds pixel-level regression coverage for renderer state planning.
// ABOUTME: Verifies blend and mask render states produce stable composited pixels.

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
}
