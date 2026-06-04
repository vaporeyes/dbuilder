// ABOUTME: Formats thing statistics window labels outside the Avalonia layer.
// ABOUTME: Keeps UDB-style thing count headers testable for the Thing Types window.

using DBuilder.Map;

namespace DBuilder.IO;

public sealed record ThingStatisticsRow(int Type, string Title, string ClassName, int Count);

public static class ThingStatisticsWindowModel
{
    public static IReadOnlyList<ThingStatisticsRow> BuildRows(
        IReadOnlyList<ThingTypeStatistic> usedTypes,
        GameConfiguration? config,
        bool hideUnused)
    {
        var counts = usedTypes.ToDictionary(type => type.Type, type => type.Count);
        var rows = new List<ThingStatisticsRow>();

        if (config != null)
        {
            foreach (var info in config.Things.Values)
            {
                counts.TryGetValue(info.Index, out int count);
                if (!hideUnused || count != 0)
                    rows.Add(new ThingStatisticsRow(info.Index, info.Title, string.IsNullOrEmpty(info.ClassName) ? "-" : info.ClassName, count));
                counts.Remove(info.Index);
            }
        }

        foreach (var item in counts)
        {
            if (!hideUnused || item.Value != 0)
                rows.Add(new ThingStatisticsRow(item.Key, "Unknown thing", "-", item.Value));
        }

        return rows.OrderBy(row => row.Type).ToArray();
    }

    public static string HeaderText(int thingCount, int rowCount)
        => $"{CountLabel(thingCount, "thing")}, {CountLabel(rowCount, "type row")}.";

    public static string TypeActivatedStatusText(int type, int thingCount)
        => $"Thing type {type}: {CountLabel(thingCount, "thing")}.";

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}
