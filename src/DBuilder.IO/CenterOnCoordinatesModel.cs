// ABOUTME: Parses Go To Coordinates dialog input using UDB's round-and-clamp behavior.
// ABOUTME: Keeps coordinate entry semantics testable without opening the Avalonia dialog.

using System.Globalization;
using DBuilder.Map;

namespace DBuilder.IO;

public static class CenterOnCoordinatesModel
{
    public static double ParseCoordinate(string? text, double fallback, MapFormat format)
    {
        double value = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : (int)fallback;
        var limits = MapFormatConstraints.CoordinateLimits(format);
        return Math.Round(Math.Clamp(value, limits.MinCoordinate, limits.MaxCoordinate));
    }
}
