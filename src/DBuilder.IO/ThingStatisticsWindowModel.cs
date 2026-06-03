// ABOUTME: Formats thing statistics window labels outside the Avalonia layer.
// ABOUTME: Keeps UDB-style thing count headers testable for the Thing Types window.

namespace DBuilder.IO;

public static class ThingStatisticsWindowModel
{
    public static string HeaderText(int thingCount, int rowCount)
        => $"{CountLabel(thingCount, "thing")}, {CountLabel(rowCount, "type row")}.";

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}
