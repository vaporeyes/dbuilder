// ABOUTME: UI-independent map-check issue list state for hiding and restoring results.
// ABOUTME: Mirrors UDB ErrorCheckForm result list operations without depending on a window toolkit.

namespace DBuilder.Map;

public sealed class MapIssueListModel
{
    private readonly List<MapIssue> allIssues;
    private readonly List<MapIssue> visibleIssues;

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

    public void ShowAll()
    {
        foreach (var issue in allIssues)
            issue.SetIgnored(false);

        visibleIssues.Clear();
        visibleIssues.AddRange(allIssues);
    }
}
