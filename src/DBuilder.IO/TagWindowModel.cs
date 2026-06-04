// ABOUTME: Formats tag list and tag statistics window labels outside the Avalonia layer.
// ABOUTME: Keeps UDB-style tag count headers and rows testable for tag-related editor windows.

using DBuilder.Map;

namespace DBuilder.IO;

public sealed record TagStatisticsRow(int Tag, string Label, int Sectors, int Linedefs, int Things)
{
    public int Total => Sectors + Linedefs + Things;
}

public static class TagWindowModel
{
    public static string TagListHeaderText(int tagCount)
        => tagCount == 0
            ? "No tags in use."
            : $"{CountLabel(tagCount, "tag")}. Click to select its elements.";

    public static string TagStatisticsHeaderText(int tagCount)
        => tagCount == 0
            ? "No tags in use."
            : $"{CountLabel(tagCount, "tag")} in use.";

    public static string TagListRowText(int tag, int count, IReadOnlyDictionary<int, string>? labels)
    {
        string label = labels != null && labels.TryGetValue(tag, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? $" - {value}"
            : "";
        return $"Tag {tag}{label}  ({CountLabel(count, "element")})";
    }

    public static IReadOnlyList<TagStatisticsRow> BuildTagStatisticsRows(
        IEnumerable<TagStatistic> tags,
        IReadOnlyDictionary<int, string>? labels)
    {
        var rows = new List<TagStatisticsRow>();
        foreach (var tag in tags)
        {
            string label = labels != null && labels.TryGetValue(tag.Tag, out string? value) ? value : "";
            rows.Add(new TagStatisticsRow(tag.Tag, label, tag.Sectors, tag.Linedefs, tag.Things));
        }
        rows.Sort((a, b) => a.Tag.CompareTo(b.Tag));
        return rows;
    }

    public static string TagActivatedStatusText(int tag, int elementCount)
        => $"Tag {tag}: {CountLabel(elementCount, "element")}.";

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}
