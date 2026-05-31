// ABOUTME: Ports UDB ColorPicker color conversion and sector color field behavior.
// ABOUTME: Keeps RGB/HSV math, display formatting, and UDMF color fields independent of picker UI.

using System.Globalization;

namespace DBuilder.Map;

public readonly record struct ColorRgb(int Red, int Green, int Blue);

public readonly record struct ColorHsv(int Hue, int Saturation, int Value);

public enum ColorPickerInfoMode
{
    Rgb,
    Hex,
    Float,
}

public enum SectorColorField
{
    LightColor,
    FadeColor,
}

public static class ColorPickerModel
{
    public const int DefaultLightColor = 0xffffff;
    public const int DefaultFadeColor = 0;
    public const string LightColorField = "lightcolor";
    public const string FadeColorField = "fadecolor";

    public static ColorRgb HsvToRgb(int hue, int saturation, int value)
        => HsvToRgb(new ColorHsv(hue, saturation, value));

    public static ColorRgb HsvToRgb(ColorHsv hsv)
    {
        float r = 0;
        float g = 0;
        float b = 0;

        float h = ((float)hsv.Hue / 255 * 360) % 360;
        float s = (float)hsv.Saturation / 255;
        float v = (float)hsv.Value / 255;

        if (s == 0)
        {
            r = v;
            g = v;
            b = v;
        }
        else
        {
            float sectorPos = h / 60;
            int sectorNumber = (int)Math.Floor(sectorPos);
            float fractionalSector = sectorPos - sectorNumber;

            float p = v * (1 - s);
            float q = v * (1 - (s * fractionalSector));
            float t = v * (1 - (s * (1 - fractionalSector)));

            switch (sectorNumber)
            {
                case 0:
                    r = v;
                    g = t;
                    b = p;
                    break;
                case 1:
                    r = q;
                    g = v;
                    b = p;
                    break;
                case 2:
                    r = p;
                    g = v;
                    b = t;
                    break;
                case 3:
                    r = p;
                    g = q;
                    b = v;
                    break;
                case 4:
                    r = t;
                    g = p;
                    b = v;
                    break;
                case 5:
                    r = v;
                    g = p;
                    b = q;
                    break;
            }
        }

        return new ColorRgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    public static ColorHsv RgbToHsv(ColorRgb rgb)
    {
        float r = (float)rgb.Red / 255;
        float g = (float)rgb.Green / 255;
        float b = (float)rgb.Blue / 255;

        float min = Math.Min(Math.Min(r, g), b);
        float max = Math.Max(Math.Max(r, g), b);
        float v = max;
        float delta = max - min;

        float h;
        float s;
        if (max == 0 || delta == 0)
        {
            s = 0;
            h = 0;
        }
        else
        {
            s = delta / max;
            if (r == max) h = (g - b) / delta;
            else if (g == max) h = 2 + (b - r) / delta;
            else h = 4 + (r - g) / delta;
        }

        h *= 60;
        if (h < 0) h += 360;

        return new ColorHsv((int)(h / 360 * 255), (int)(s * 255), (int)(v * 255));
    }

    public static int PackRgb(ColorRgb rgb)
        => rgb.Red << 16 | rgb.Green << 8 | rgb.Blue;

    public static ColorRgb UnpackRgb(int value)
        => new((value >> 16) & 0xff, (value >> 8) & 0xff, value & 0xff);

    public static string Format(ColorRgb rgb, ColorPickerInfoMode mode)
        => mode switch
        {
            ColorPickerInfoMode.Rgb => $"{rgb.Red} {rgb.Green} {rgb.Blue}",
            ColorPickerInfoMode.Hex => $"{rgb.Red:X02}{rgb.Green:X02}{rgb.Blue:X02}",
            ColorPickerInfoMode.Float => string.Join(
                " ",
                ((float)Math.Round(rgb.Red / 255f, 2)).ToString("F02", CultureInfo.InvariantCulture),
                ((float)Math.Round(rgb.Green / 255f, 2)).ToString("F02", CultureInfo.InvariantCulture),
                ((float)Math.Round(rgb.Blue / 255f, 2)).ToString("F02", CultureInfo.InvariantCulture)),
            _ => "",
        };

    public static ColorRgb? TryParse(ColorPickerInfoMode mode, string text)
        => mode switch
        {
            ColorPickerInfoMode.Hex => TryParseHex(text),
            ColorPickerInfoMode.Float => TryParseFloatTriplet(text),
            _ => null,
        };

    public static void EnsureSectorColorFields(IEnumerable<Sector> sectors, int lightColor, int fadeColor)
    {
        foreach (var sector in sectors)
        {
            if (!sector.Fields.ContainsKey(LightColorField)) sector.Fields[LightColorField] = lightColor;
            if (!sector.Fields.ContainsKey(FadeColorField)) sector.Fields[FadeColorField] = fadeColor;
        }
    }

    public static int GetSectorColor(Sector sector, SectorColorField field)
        => field == SectorColorField.LightColor
            ? sector.GetIntegerField(LightColorField, DefaultLightColor)
            : sector.GetIntegerField(FadeColorField, DefaultFadeColor);

    public static void SetSectorColor(IEnumerable<Sector> sectors, SectorColorField field, ColorRgb rgb)
    {
        string key = field == SectorColorField.LightColor ? LightColorField : FadeColorField;
        int value = PackRgb(rgb);
        foreach (var sector in sectors)
            sector.Fields[key] = value;
    }

    public static void RemoveDefaultSectorColors(IEnumerable<Sector> sectors)
    {
        foreach (var sector in sectors)
        {
            if (sector.GetIntegerField(LightColorField, DefaultLightColor) == DefaultLightColor)
                sector.Fields.Remove(LightColorField);

            if (sector.GetIntegerField(FadeColorField, DefaultFadeColor) == DefaultFadeColor)
                sector.Fields.Remove(FadeColorField);
        }
    }

    private static ColorRgb? TryParseHex(string text)
    {
        string hexColor = text.Trim().Replace("-", "", StringComparison.Ordinal);
        if (hexColor.Length != 6) return null;

        if (!int.TryParse(hexColor[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int red)) return null;
        if (!int.TryParse(hexColor.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int green)) return null;
        if (!int.TryParse(hexColor.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int blue)) return null;

        return new ColorRgb(red, green, blue);
    }

    private static ColorRgb? TryParseFloatTriplet(string text)
    {
        string[] parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) return null;

        if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float red)) return null;
        if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float green)) return null;
        if (!float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float blue)) return null;

        return new ColorRgb(FloatComponentToByte(red), FloatComponentToByte(green), FloatComponentToByte(blue));
    }

    private static int FloatComponentToByte(float value)
        => (int)(Math.Clamp(Math.Abs(value), 0.0f, 1.0f) * 255);
}
