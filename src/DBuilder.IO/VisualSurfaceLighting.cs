// ABOUTME: Computes visual-mode lighting tints for renderable map surfaces.
// ABOUTME: Keeps visual wall and thing sector-color behavior testable outside editor rendering.

using DBuilder.Map;

namespace DBuilder.IO;

public enum VisualWallPart
{
    Middle,
    Top,
    Bottom,
}

public static class VisualSurfaceLighting
{
    public const int NoColorOverride = -1;
    public const int FullBrightness = 255;

    public static int WallRenderTint(Sidedef side, VisualWallPart part, bool fullBrightness, double scale, bool classicRendering = false)
    {
        int brightness = fullBrightness
            ? FullBrightness
            : side.GetField("lightabsolute", false)
                ? side.GetIntegerField("light")
                : (side.Sector?.Brightness ?? 0) + side.GetIntegerField("light");
        int color = ModulateColors(
            side.Sector?.GetIntegerField("lightcolor", NoColorOverride) ?? NoColorOverride,
            WallPartColor(side.Sector, part));
        return RenderTint(brightness, color, scale, classicRendering);
    }

    public static int ThingRenderTint(Sector? sector, bool fullBrightness, double scale, bool classicRendering = false)
    {
        if (sector == null) return DefaultTint(scale);

        int brightness = fullBrightness ? FullBrightness : sector?.Brightness ?? 0;
        int color = ModulateColors(
            sector?.GetIntegerField("lightcolor", NoColorOverride) ?? NoColorOverride,
            sector?.GetIntegerField("color_sprites", NoColorOverride) ?? NoColorOverride);
        return RenderTint(brightness, color, scale, classicRendering);
    }

    public static int RenderTint(int brightness, int color, double scale, bool classicRendering = false)
    {
        int effectiveBrightness = classicRendering ? ClassicBrightness(brightness) : brightness;
        double factor = Math.Clamp(Math.Clamp(effectiveBrightness, 0, FullBrightness) / 255.0, 0.15, 1.0)
            * scale;

        if (color == NoColorOverride)
        {
            byte gray = Channel(FullBrightness, factor);
            return unchecked((int)(0xff000000u | ((uint)gray << 16) | ((uint)gray << 8) | gray));
        }

        byte red = Channel((color >> 16) & 0xff, factor);
        byte green = Channel((color >> 8) & 0xff, factor);
        byte blue = Channel(color & 0xff, factor);
        return unchecked((int)(0xff000000u | ((uint)red << 16) | ((uint)green << 8) | blue));
    }

    private static byte Channel(int value, double factor)
        => (byte)Math.Clamp(value * factor, 0.0, 255.0);

    private static int ClassicBrightness(int brightness)
    {
        int clamped = Math.Clamp(brightness, 0, FullBrightness);
        if (clamped == FullBrightness) return FullBrightness;
        return (clamped / 16) * 16;
    }

    private static int DefaultTint(double scale)
    {
        byte gray = Channel(FullBrightness, scale);
        return unchecked((int)(0xff000000u | ((uint)gray << 16) | ((uint)gray << 8) | gray));
    }

    private static int WallPartColor(Sector? sector, VisualWallPart part)
        => part switch
        {
            VisualWallPart.Top => sector?.GetIntegerField("color_walltop", NoColorOverride) ?? NoColorOverride,
            VisualWallPart.Bottom => sector?.GetIntegerField("color_wallbottom", NoColorOverride) ?? NoColorOverride,
            _ => NoColorOverride,
        };

    private static int ModulateColors(int lightColor, int surfaceColor)
    {
        if (lightColor == NoColorOverride) return surfaceColor;
        if (surfaceColor == NoColorOverride) return lightColor;

        int red = (((lightColor >> 16) & 0xff) * ((surfaceColor >> 16) & 0xff)) / 255;
        int green = (((lightColor >> 8) & 0xff) * ((surfaceColor >> 8) & 0xff)) / 255;
        int blue = ((lightColor & 0xff) * (surfaceColor & 0xff)) / 255;
        return (red << 16) | (green << 8) | blue;
    }
}
