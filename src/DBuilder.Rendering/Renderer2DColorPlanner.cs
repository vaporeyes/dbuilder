// ABOUTME: Plans UDB Renderer2D color choices for map things, vertices, and linedefs.
// ABOUTME: Keeps selection, preset, and two-sided alpha rules testable outside the UI renderer.
namespace DBuilder.Rendering;

public enum RendererBrightnessLightMode
{
    Configuration,
    Doom,
    Standard,
}

public static class Renderer2DColorPlanner
{
    public static PixelColor DetermineThingColor(
        bool selected,
        PixelColor defaultColor,
        PixelColor selectionColor,
        PixelColor? dynamicLightColor = null)
    {
        if (selected) return selectionColor;
        return dynamicLightColor ?? defaultColor;
    }

    public static int DetermineVertexColor(
        bool selected,
        int selectionColorIndex = ColorCollection.SelectionIndex,
        int verticesColorIndex = ColorCollection.VerticesIndex)
        => selected ? selectionColorIndex : verticesColorIndex;

    public static PixelColor DetermineLinedefColor(
        bool selected,
        bool impassable,
        PixelColor linedefColor,
        PixelColor selectionColor,
        byte doubleSidedAlpha,
        PixelColor? presetColor = null)
    {
        if (selected) return selectionColor;

        PixelColor color = presetColor ?? linedefColor;
        return impassable ? color : color.WithAlpha(doubleSidedAlpha);
    }

    public static int CalculateBrightness(
        int level,
        bool doomLightLevels,
        RendererBrightnessLightMode lightMode = RendererBrightnessLightMode.Configuration)
    {
        bool useDoomLightLevels = lightMode switch
        {
            RendererBrightnessLightMode.Doom => true,
            RendererBrightnessLightMode.Standard => false,
            _ => doomLightLevels,
        };

        if (level < 192 && useDoomLightLevels)
            level = (int)(192.0f - (192 - level) * 1.5f);

        byte brightness = (byte)Math.Clamp(level, 0, 255);
        return new PixelColor(255, brightness, brightness, brightness).ToInt();
    }
}
