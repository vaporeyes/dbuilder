// ABOUTME: Applies UDB-style selected-sector floor, ceiling, and brightness gradients.
// ABOUTME: Keeps the interpolation math UI-independent so editor commands and tests share one implementation.

using DBuilder.Geometry;

namespace DBuilder.Map;

public enum SectorGradientTarget
{
    FloorHeight,
    CeilingHeight,
    Brightness,
    FloorLight,
    CeilingLight,
    LightColor,
    FadeColor,
    LightAndFadeColor,
}

public readonly record struct SectorGradientResult(bool Applied, int SectorCount, string Message);

public static class SectorGradient
{
    public const int MinimumSectorCount = 3;

    public static SectorGradientResult Apply(
        IReadOnlyList<Sector> sectors,
        SectorGradientTarget target,
        InterpolationTools.Mode interpolationMode = InterpolationTools.Mode.LINEAR)
    {
        if (sectors.Count < MinimumSectorCount)
            return new SectorGradientResult(false, sectors.Count, "Select at least 3 sectors first!");

        interpolationMode = InterpolationTools.NormalizeMode(interpolationMode);

        if (target == SectorGradientTarget.LightColor)
            return ApplyColorGradient(sectors, interpolationMode, "lightcolor", 0xFFFFFF, SuccessMessage(target));
        if (target == SectorGradientTarget.FadeColor)
            return ApplyColorGradient(sectors, interpolationMode, "fadecolor", 0, SuccessMessage(target));
        if (target == SectorGradientTarget.LightAndFadeColor)
        {
            SectorGradientResult fade = ApplyColorGradient(sectors, interpolationMode, "fadecolor", 0, SuccessMessage(SectorGradientTarget.FadeColor));
            SectorGradientResult light = ApplyColorGradient(sectors, interpolationMode, "lightcolor", 0xFFFFFF, SuccessMessage(SectorGradientTarget.LightColor));
            if (!fade.Applied && !light.Applied)
                return new SectorGradientResult(false, sectors.Count, "First or last selected sector must have the \"fadecolor\" or \"lightcolor\" property!");
            return new SectorGradientResult(fade.Applied || light.Applied, sectors.Count, SuccessMessage(target));
        }

        int start = ReadValue(sectors[0], target);
        int end = ReadValue(sectors[^1], target);

        for (int i = 0; i < sectors.Count; i++)
        {
            double u = i / (double)(sectors.Count - 1);
            int value = (int)Math.Round(InterpolationTools.Interpolate(start, end, u, interpolationMode));
            WriteValue(sectors[i], target, value);
        }

        return new SectorGradientResult(true, sectors.Count, SuccessMessage(target));
    }

    private static SectorGradientResult ApplyColorGradient(
        IReadOnlyList<Sector> sectors,
        InterpolationTools.Mode interpolationMode,
        string key,
        int defaultValue,
        string successMessage)
    {
        if (!sectors[0].Fields.ContainsKey(key) && !sectors[^1].Fields.ContainsKey(key))
            return new SectorGradientResult(false, sectors.Count, $"First or last selected sector must have the \"{key}\" property!");

        uint start = (uint)sectors[0].GetIntegerField(key, defaultValue);
        uint end = (uint)sectors[^1].GetIntegerField(key, defaultValue);

        for (int i = 1; i < sectors.Count - 1; i++)
        {
            double u = i / (double)(sectors.Count - 1);
            int color = (int)(InterpolationTools.InterpolateColor(start, end, u, interpolationMode) & 0x00FFFFFFu);
            sectors[i].SetIntegerField(key, color, defaultValue);
        }

        return new SectorGradientResult(true, sectors.Count, successMessage);
    }

    private static int ReadValue(Sector sector, SectorGradientTarget target)
        => target switch
        {
            SectorGradientTarget.FloorHeight => sector.FloorHeight,
            SectorGradientTarget.CeilingHeight => sector.CeilHeight,
            SectorGradientTarget.Brightness => sector.Brightness,
            SectorGradientTarget.FloorLight => SurfaceLightValue(sector, "lightfloor", "lightfloorabsolute"),
            SectorGradientTarget.CeilingLight => SurfaceLightValue(sector, "lightceiling", "lightceilingabsolute"),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };

    private static void WriteValue(Sector sector, SectorGradientTarget target, int value)
    {
        switch (target)
        {
            case SectorGradientTarget.FloorHeight:
                sector.FloorHeight = value;
                break;
            case SectorGradientTarget.CeilingHeight:
                sector.CeilHeight = value;
                break;
            case SectorGradientTarget.Brightness:
                sector.Brightness = value;
                break;
            case SectorGradientTarget.FloorLight:
                WriteSurfaceLightValue(sector, "lightfloor", "lightfloorabsolute", value);
                break;
            case SectorGradientTarget.CeilingLight:
                WriteSurfaceLightValue(sector, "lightceiling", "lightceilingabsolute", value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }

    private static int SurfaceLightValue(Sector sector, string lightKey, string absoluteKey)
        => sector.GetField(absoluteKey, false)
            ? sector.GetIntegerField(lightKey)
            : Math.Clamp(sector.Brightness + sector.GetIntegerField(lightKey), 0, 255);

    private static void WriteSurfaceLightValue(Sector sector, string lightKey, string absoluteKey, int value)
    {
        if (sector.GetField(absoluteKey, false))
            sector.SetIntegerField(lightKey, value, 0);
        else
            sector.SetIntegerField(lightKey, value - sector.Brightness, 0);
    }

    private static string SuccessMessage(SectorGradientTarget target)
        => target switch
        {
            SectorGradientTarget.FloorHeight => "Created gradient floor heights over selected sectors.",
            SectorGradientTarget.CeilingHeight => "Created gradient ceiling heights over selected sectors.",
            SectorGradientTarget.Brightness => "Created gradient brightness over selected sectors.",
            SectorGradientTarget.FloorLight => "Created gradient floor brightness over selected sectors.",
            SectorGradientTarget.CeilingLight => "Created gradient ceiling brightness over selected sectors.",
            SectorGradientTarget.LightColor => "Created gradient light color over selected sectors.",
            SectorGradientTarget.FadeColor => "Created gradient fade color over selected sectors.",
            SectorGradientTarget.LightAndFadeColor => "Created gradient light and fade colors over selected sectors.",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };
}
