// ABOUTME: Formatting and parsing helpers for UDB-style grid setup dialog fields.
// ABOUTME: Keeps UI percentage fields aligned with persisted grid setup scale values.

using System.Globalization;

namespace DBuilder.IO;

public static class GridSetupDialogModel
{
    public static string FormatBackgroundScalePercent(double scale)
        => (GridSetup.ClampBackgroundScale(scale) * 100.0).ToString("0.###", CultureInfo.InvariantCulture);

    public static double ParseBackgroundScalePercent(string? text, double fallbackScale)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
            return GridSetup.ClampBackgroundScale(fallbackScale);

        return GridSetup.ClampBackgroundScale(percent / 100.0);
    }

    public static (string Name, int Source) BackgroundSelection(bool showBackground, string? name, int source)
    {
        if (!showBackground) return ("", 0);
        return ((name ?? "").Trim(), source);
    }
}
