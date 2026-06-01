// ABOUTME: UI-independent map-check issue list state for hiding and restoring results.
// ABOUTME: Mirrors UDB ErrorCheckForm result list operations without depending on a window toolkit.

namespace DBuilder.Map;

public sealed class MapIssueListModel
{
    private readonly List<MapIssue> allIssues;
    private readonly List<MapIssue> visibleIssues;
    private readonly HashSet<MapIssueKind> hiddenKinds = new();

    public MapIssueListModel(IEnumerable<MapIssue> issues)
    {
        allIssues = issues.ToList();
        visibleIssues = new List<MapIssue>(allIssues);
    }

    public IReadOnlyList<MapIssue> AllIssues => allIssues;

    public IReadOnlyList<MapIssue> VisibleIssues => visibleIssues;

    public int HideSelected(IEnumerable<MapIssue> selected)
    {
        int hidden = 0;
        foreach (var issue in selected.ToArray())
        {
            issue.SetIgnored(true);
            if (visibleIssues.Remove(issue))
                hidden++;
        }

        return hidden;
    }

    public int HideSelectedKinds(IEnumerable<MapIssue> selected)
    {
        foreach (var issue in selected)
            hiddenKinds.Add(issue.Kind);

        int before = visibleIssues.Count;
        RefreshVisible();
        return before - visibleIssues.Count;
    }

    public void ShowOnlySelectedKinds(IEnumerable<MapIssue> selected)
    {
        var shownKinds = selected.Select(issue => issue.Kind).ToHashSet();
        if (shownKinds.Count == 0) return;

        hiddenKinds.Clear();
        foreach (var issue in visibleIssues.ToArray())
            if (!shownKinds.Contains(issue.Kind))
                hiddenKinds.Add(issue.Kind);

        RefreshVisible();
    }

    public IReadOnlyList<MapIssue> VisibleIssuesWithSelectedKinds(IEnumerable<MapIssue> selected)
    {
        var selectedKinds = selected.Select(issue => issue.Kind).ToHashSet();
        if (selectedKinds.Count == 0) return Array.Empty<MapIssue>();

        return visibleIssues.Where(issue => selectedKinds.Contains(issue.Kind)).ToArray();
    }

    public static string FormatIssueDescriptions(IEnumerable<MapIssue> issues)
    {
        var lines = issues.Select(issue => issue.Message).ToArray();
        if (lines.Length == 0) return string.Empty;

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    public void ShowAll()
    {
        foreach (var issue in allIssues)
            issue.SetIgnored(false);

        hiddenKinds.Clear();
        RefreshVisible();
    }

    private void RefreshVisible()
    {
        visibleIssues.Clear();
        visibleIssues.AddRange(allIssues.Where(issue => !hiddenKinds.Contains(issue.Kind) && !IsIgnored(issue)));
    }

    private static bool IsIgnored(MapIssue issue)
    {
        var elements = issue.SuppressionTargets;
        return elements.Count > 0 && elements.All(element => element.IgnoredErrorChecks.Contains(issue.Kind));
    }
}
