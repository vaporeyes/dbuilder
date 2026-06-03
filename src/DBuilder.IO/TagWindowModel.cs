// ABOUTME: Formats tag list and tag statistics window labels outside the Avalonia layer.
// ABOUTME: Keeps UDB-style tag count headers and rows testable for tag-related editor windows.

namespace DBuilder.IO;

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

    public static string TagActivatedStatusText(int tag, int elementCount)
        => $"Tag {tag}: {CountLabel(elementCount, "element")}.";

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}
