// ABOUTME: Computes visual-mode lighting tints for renderable map surfaces.
// ABOUTME: Keeps wall sidedef light and sector lightcolor behavior testable outside editor rendering.

using DBuilder.Map;

namespace DBuilder.IO;

public static class VisualSurfaceLighting
{
    public const int NoColorOverride = -1;
    public const int FullBrightness = 255;

    public static int WallRenderTint(Sidedef side, bool fullBrightness, double scale)
    {
        int brightness = fullBrightness
            ? FullBrightness
            : side.GetField("lightabsolute", false)
                ? side.GetIntegerField("light")
                : (side.Sector?.Brightness ?? 0) + side.GetIntegerField("light");
        int color = side.Sector?.GetIntegerField("lightcolor", NoColorOverride) ?? NoColorOverride;
        return RenderTint(brightness, color, scale);
    }

    public static int RenderTint(int brightness, int color, double scale)
    {
        double factor = Math.Clamp(Math.Clamp(brightness, 0, FullBrightness) / 255.0, 0.15, 1.0)
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
}
