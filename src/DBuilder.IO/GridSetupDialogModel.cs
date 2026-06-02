// ABOUTME: Formatting and parsing helpers for UDB-style grid setup dialog fields.
// ABOUTME: Keeps UI percentage fields aligned with persisted grid setup scale values.

using System.Globalization;

namespace DBuilder.IO;

public static class GridSetupDialogModel
{
    public static double ParseGridSize(string? text, double fallbackSize)
    {
        double size = ParseDouble(text, fallbackSize);
        return Math.Max(GridSetup.MinimumGridSize, size);
    }

    public static double ParseDouble(string? text, double fallback)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return fallback;

        return double.IsFinite(value) ? value : fallback;
    }

    public static int ParseInt(string? text, int fallback)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

    public static string FormatBackgroundScalePercent(double scale)
        => (GridSetup.ClampBackgroundScale(scale) * 100.0).ToString("0.###", CultureInfo.InvariantCulture);

    public static double ParseBackgroundScalePercent(string? text, double fallbackScale)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
            return GridSetup.ClampBackgroundScale(fallbackScale);

        if (!double.IsFinite(percent)) return GridSetup.ClampBackgroundScale(fallbackScale);

        return GridSetup.ClampBackgroundScale(percent / 100.0);
    }

    public static (string Name, int Source) BackgroundSelection(bool showBackground, string? name, int source)
    {
        if (!showBackground) return ("", 0);
        return ((name ?? "").Trim(), source);
    }
}
