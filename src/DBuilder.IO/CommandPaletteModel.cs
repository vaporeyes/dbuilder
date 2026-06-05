// ABOUTME: Builds command palette rows and search groups from editor command metadata.
// ABOUTME: Keeps UDB-style command palette filtering testable outside the Avalonia UI.

namespace DBuilder.IO;

public sealed record CommandPaletteRow(
    EditorCommandDescriptor Command,
    string CategoryText,
    string GestureText,
    bool IsUsable);

public sealed record CommandPaletteGroup(
    string Title,
    IReadOnlyList<CommandPaletteRow> Rows);

public static class CommandPaletteModel
{
    public const int MaxItems = 20;
    public const int MaxRecentCommands = 5;
    private const int UdbRecentOverflowRemoveIndex = 4;

    public static IReadOnlyList<CommandPaletteGroup> BuildGroups(
        IReadOnlyList<EditorCommandDescriptor> commands,
        IReadOnlyList<EditorShortcutBinding> bindings,
        IReadOnlySet<string> usableCommandIds,
        string? filter,
        IReadOnlyList<string>? recentCommandIds = null)
    {
        string searchText = filter?.Trim() ?? "";
        var matchingRows = commands
            .Where(command => MatchesText(command.Title, searchText))
            .Select(command => new CommandPaletteRow(
                command,
                command.CategoryTitle,
                EditorCommandCatalog.GestureText(command.Id, bindings),
                usableCommandIds.Contains(command.Id)))
            .ToArray();

        var groups = new List<CommandPaletteGroup>(3);
        if (searchText.Length == 0 && recentCommandIds is { Count: > 0 })
        {
            var rowsById = matchingRows.ToDictionary(row => row.Command.Id, StringComparer.Ordinal);
            var recentRows = recentCommandIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .Take(MaxRecentCommands)
                .Select(id => rowsById.TryGetValue(id, out var row) ? row : null)
                .Where(row => row is not null)
                .Cast<CommandPaletteRow>()
                .ToArray();

            if (recentRows.Length > 0)
                groups.Add(new CommandPaletteGroup("Recent", recentRows));
        }

        AddSortedGroup(groups, "Usable actions", matchingRows.Where(row => row.IsUsable));
        AddSortedGroup(groups, "Not usable in this context", matchingRows.Where(row => !row.IsUsable));
        return groups;
    }

    public static void AddRecentCommand(List<string> recentCommandIds, string commandId)
    {
        recentCommandIds.Remove(commandId);
        recentCommandIds.Insert(0, commandId);
        if (recentCommandIds.Count > MaxRecentCommands)
            recentCommandIds.RemoveRange(UdbRecentOverflowRemoveIndex, recentCommandIds.Count - MaxRecentCommands);
    }

    public static bool MatchesText(string text, string? search)
    {
        string normalizedText = Normalize(text);
        string normalizedSearch = Normalize(search ?? "");

        if (normalizedSearch.Length == 0 || normalizedText.Contains(normalizedSearch, StringComparison.Ordinal))
            return true;

        var textItems = normalizedText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        string[] searchItems = normalizedSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < searchItems.Length; i++)
        {
            string searchItem = searchItems[i];
            if (searchItem.Length == 0) continue;

            string? result = null;
            while (searchItem.Length > 0)
            {
                result = textItems.FirstOrDefault(item => item.StartsWith(searchItem, StringComparison.Ordinal));
                if (result is not null)
                {
                    searchItems[i] = searchItems[i].Remove(0, searchItem.Length);
                    i--;
                    break;
                }

                searchItem = searchItem.Remove(searchItem.Length - 1);
            }

            if (result is null)
                return false;

            int index = textItems.IndexOf(result);
            textItems.RemoveRange(0, index + 1);
        }

        return true;
    }

    private static void AddSortedGroup(
        List<CommandPaletteGroup> groups,
        string title,
        IEnumerable<CommandPaletteRow> rows)
    {
        var sortedRows = rows
            .OrderBy(row => row.Command.Title, StringComparer.Ordinal)
            .ToArray();

        if (sortedRows.Length > 0)
            groups.Add(new CommandPaletteGroup(title, sortedRows));
    }

    private static string Normalize(string text)
        => string.Join(
            " ",
            text.ToLowerInvariant().Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

}
