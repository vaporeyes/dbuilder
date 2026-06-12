// ABOUTME: Resolves UDB-style 2D display colors for internal and GLDEFS-backed dynamic light things.
// ABOUTME: Keeps dynamic light color selection testable outside the Avalonia renderer.

using DBuilder.Map;

namespace DBuilder.IO;

public static class DynamicLightDisplay
{
    public const int DefaultBillboardTint = unchecked((int)0xffffffff);
    public const int SelectedBillboardTint = unchecked((int)0xfffff080);

    public static int BillboardTint(Thing thing, GameConfiguration? config, Gldefs? gldefs)
        => thing.Selected ? SelectedBillboardTint : ThingColor(thing, config, gldefs) ?? DefaultBillboardTint;

    public static int? ThingColor(Thing thing, GameConfiguration? config, Gldefs? gldefs)
    {
        DynamicLightDefinition? definition = ColorPickerModel.InternalDynamicLightDefinitionForThingType(thing.Type);
        if (definition != null)
        {
            ColorRgb color = ColorPickerModel.GetDynamicLightColor(definition, thing.Args, thing.Fields);
            return ApplyLightmapIntensity(ToArgb(color), thing, definition);
        }

        ThingTypeInfo? info = config?.GetThing(thing.Type);
        if (info == null || gldefs == null) return null;

        GldefsLight? light = LightForThing(info, gldefs);
        if (light == null) return null;
        return ToArgb(light.R, light.G, light.B);
    }

    public static double? ThingRadius(Thing thing, GameConfiguration? config, Gldefs? gldefs)
    {
        DynamicLightDefinition? definition = ColorPickerModel.InternalDynamicLightDefinitionForThingType(thing.Type);
        if (definition != null)
        {
            int argIndex = ColorPickerModel.FirstDynamicLightRadiusArgument(definition.LightVavoom);
            if (argIndex >= thing.Args.Length) return null;
            return thing.Args[argIndex] > 0 ? thing.Args[argIndex] : null;
        }

        ThingTypeInfo? info = config?.GetThing(thing.Type);
        if (info == null || gldefs == null) return null;

        GldefsLight? light = LightForThing(info, gldefs);
        if (light == null) return null;
        return light.Size > 0 ? light.Size : null;
    }

    private static GldefsLight? LightForThing(ThingTypeInfo info, Gldefs gldefs)
    {
        if (!string.IsNullOrWhiteSpace(info.LightName) && gldefs.Lights.TryGetValue(info.LightName, out GldefsLight? direct))
            return direct;

        if (!string.IsNullOrWhiteSpace(info.ClassName))
        {
            foreach (GldefsObject obj in gldefs.Objects)
            {
                if (!string.Equals(obj.ClassName, info.ClassName, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (string lightName in obj.Lights)
                    if (gldefs.Lights.TryGetValue(lightName, out GldefsLight? linked)) return linked;
            }
        }

        return null;
    }

    private static int ApplyLightmapIntensity(int argb, Thing thing, DynamicLightDefinition definition)
    {
        if (definition.LightNumber is not (9876 or 9877 or 9878 or 9879 or 9881 or 9882 or 9883 or 9884)) return argb;
        double intensity = FieldDouble(thing, "alpha", 1.0);
        if (intensity == 1.0) return argb;

        uint color = unchecked((uint)argb);
        byte red = Scale((color >> 16) & 0xff, intensity);
        byte green = Scale((color >> 8) & 0xff, intensity);
        byte blue = Scale(color & 0xff, intensity);
        return unchecked((int)(0xff000000u | ((uint)red << 16) | ((uint)green << 8) | blue));
    }

    private static double FieldDouble(Thing thing, string key, double fallback)
    {
        if (!thing.Fields.TryGetValue(key, out object? value)) return fallback;
        return value switch
        {
            double d => d,
            float f => f,
            int i => i,
            string s when double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed) => parsed,
            _ => fallback,
        };
    }

    private static int ToArgb(ColorRgb color)
        => unchecked((int)(0xff000000u | ((uint)color.Red << 16) | ((uint)color.Green << 8) | (uint)color.Blue));

    private static int ToArgb(float red, float green, float blue)
        => unchecked((int)(0xff000000u | ((uint)FloatByte(red) << 16) | ((uint)FloatByte(green) << 8) | (uint)FloatByte(blue)));

    private static byte FloatByte(float value)
        => (byte)Math.Clamp((int)(Math.Clamp(value, 0.0f, 1.0f) * 255.0f), 0, 255);

    private static byte Scale(uint value, double intensity)
        => (byte)Math.Clamp(value * intensity, 0.0, 255.0);
}
