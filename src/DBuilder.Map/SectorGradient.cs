// ABOUTME: Applies UDB-style selected-sector floor, ceiling, and brightness gradients.
// ABOUTME: Keeps the interpolation math UI-independent so editor commands and tests share one implementation.

using DBuilder.Geometry;

namespace DBuilder.Map;

public enum SectorGradientTarget
{
    FloorHeight,
    CeilingHeight,
    Brightness,
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

    private static int ReadValue(Sector sector, SectorGradientTarget target)
        => target switch
        {
            SectorGradientTarget.FloorHeight => sector.FloorHeight,
            SectorGradientTarget.CeilingHeight => sector.CeilHeight,
            SectorGradientTarget.Brightness => sector.Brightness,
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
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }

    private static string SuccessMessage(SectorGradientTarget target)
        => target switch
        {
            SectorGradientTarget.FloorHeight => "Created gradient floor heights over selected sectors.",
            SectorGradientTarget.CeilingHeight => "Created gradient ceiling heights over selected sectors.",
            SectorGradientTarget.Brightness => "Created gradient brightness over selected sectors.",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };
}
