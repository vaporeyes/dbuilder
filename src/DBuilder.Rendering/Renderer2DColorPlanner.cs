// ABOUTME: Plans UDB Renderer2D color choices for map things, vertices, and linedefs.
// ABOUTME: Keeps selection, preset, and two-sided alpha rules testable outside the UI renderer.
namespace DBuilder.Rendering;

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
}
