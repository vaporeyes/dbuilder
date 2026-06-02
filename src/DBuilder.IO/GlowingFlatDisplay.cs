// ABOUTME: Resolves UDB-style glowing flat display data from UDMF sector fields and GLDEFS entries.
// ABOUTME: Exposes the fullbright surface override used by 2D and visual render planning.

using DBuilder.Map;

namespace DBuilder.IO;

public enum GlowingFlatSurface
{
    Floor,
    Ceiling,
}

public sealed record GlowingFlatDisplayState(
    int Color,
    double Height,
    int Brightness,
    bool Fullbright,
    bool CalculateTextureColor);

public sealed record GlowingFlatSurfaceLighting(
    int Color,
    int Light,
    bool Absolute);

public static class GlowingFlatDisplay
{
    public const int DefaultGlowHeight = 128;
    public const int DefaultGlowBrightness = 255;
    public const int DisabledGlowColor = -1;
    public const int WhiteColor = 0xffffff;
    public const int NoColorOverride = -1;

    public static GlowingFlatDisplayState? Resolve(Sector sector, GlowingFlatSurface surface, Gldefs? gldefs, bool isUdmf)
    {
        if (isUdmf)
        {
            string colorField = surface == GlowingFlatSurface.Floor ? "floorglowcolor" : "ceilingglowcolor";
            int glowColor = sector.GetIntegerField(colorField, 0);
            if (glowColor == DisabledGlowColor) return null;

            if (glowColor > 0)
            {
                string heightField = surface == GlowingFlatSurface.Floor ? "floorglowheight" : "ceilingglowheight";
                double glowHeight = sector.GetFloatField(heightField, 0.0);
                if (glowHeight > 0.0)
                {
                    int color = glowColor & WhiteColor;
                    return new GlowingFlatDisplayState(
                        color,
                        glowHeight,
                        Brightness(color),
                        Fullbright: false,
                        CalculateTextureColor: false);
                }
            }
        }

        if (gldefs == null) return null;
        string texture = surface == GlowingFlatSurface.Floor ? sector.FloorTexture : sector.CeilTexture;
        return ResolveGldefs(texture, gldefs);
    }

    public static GlowingFlatSurfaceLighting SurfaceLighting(
        Sector sector,
        GlowingFlatSurface surface,
        Gldefs? gldefs)
    {
        string texture = surface == GlowingFlatSurface.Floor ? sector.FloorTexture : sector.CeilTexture;
        GlowingFlatDisplayState? glow = ResolveGldefs(texture, gldefs);
        if (glow?.Fullbright == true)
            return new GlowingFlatSurfaceLighting(NoColorOverride, DefaultGlowBrightness, Absolute: true);

        string lightField = surface == GlowingFlatSurface.Floor ? "lightfloor" : "lightceiling";
        string absoluteField = surface == GlowingFlatSurface.Floor ? "lightfloorabsolute" : "lightceilingabsolute";
        string colorField = surface == GlowingFlatSurface.Floor ? "color_floor" : "color_ceiling";
        return new GlowingFlatSurfaceLighting(
            ModulateColors(sector.GetIntegerField("lightcolor", NoColorOverride), sector.GetIntegerField(colorField, NoColorOverride)),
            sector.GetIntegerField(lightField, 0),
            sector.GetField(absoluteField, false));
    }

    private static GlowingFlatDisplayState? ResolveGldefs(string texture, Gldefs? gldefs)
    {
        if (gldefs == null || string.IsNullOrWhiteSpace(texture)) return null;
        if (!gldefs.Glows.TryGetValue(texture, out GldefsGlow? glow)) return null;

        int color = ToRgb(glow.R, glow.G, glow.B);
        int brightness = glow.Fullbright ? DefaultGlowBrightness : Brightness(color);
        return new GlowingFlatDisplayState(
            color,
            glow.Height,
            brightness,
            glow.Fullbright,
            glow.CalculateTextureColor);
    }

    private static int ToRgb(float red, float green, float blue)
        => (FloatByte(red) << 16) | (FloatByte(green) << 8) | FloatByte(blue);

    private static int FloatByte(float value)
        => Math.Clamp((int)(Math.Clamp(value, 0.0f, 1.0f) * 255.0f), 0, 255);

    private static int Brightness(int color)
        => (((color >> 16) & 0xff) + ((color >> 8) & 0xff) + (color & 0xff)) / 3;

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
