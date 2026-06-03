// ABOUTME: Models UDB BuilderModes UndoRedoPanel timeline rows without binding to a UI toolkit.
// ABOUTME: Keeps begin, undo, current, redo, and elided display state consistent with UDB selection rules.

namespace DBuilder.IO;

public enum UndoRedoPanelItemKind
{
    Begin,
    Undo,
    Current,
    Redo,
    Elided,
}

public enum UndoRedoPanelOperationKind
{
    None,
    Undo,
    Redo,
}

public sealed record UndoRedoPanelItem(string Description, UndoRedoPanelItemKind Kind);

public sealed record UndoRedoPanelOperation(UndoRedoPanelOperationKind Kind, int Levels)
{
    public static UndoRedoPanelOperation None { get; } = new(UndoRedoPanelOperationKind.None, 0);

    public string StatusText(int performedLevels)
        => Kind switch
        {
            UndoRedoPanelOperationKind.Undo => $"Undo {CountLabel(performedLevels, "level")}.",
            UndoRedoPanelOperationKind.Redo => $"Redo {CountLabel(performedLevels, "level")}.",
            _ => string.Empty,
        };

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}

public sealed record UndoRedoPanelState(
    IReadOnlyList<UndoRedoPanelItem> Items,
    int CurrentSelection,
    int UndoCount,
    int RedoCount)
{
    public string HeaderText
        => Items.Count == 0
            ? "No map loaded."
            : $"{CountLabel(UndoCount, "undo level")}, {CountLabel(RedoCount, "redo level")}. Select a row to jump.";

    public UndoRedoPanelOperation OperationForSelection(int selectedIndex)
    {
        if (selectedIndex < 0 || selectedIndex >= Items.Count) return UndoRedoPanelOperation.None;
        if (Items[selectedIndex].Kind == UndoRedoPanelItemKind.Elided) return UndoRedoPanelOperation.None;
        if (selectedIndex == CurrentSelection) return UndoRedoPanelOperation.None;

        int delta = CurrentSelection - selectedIndex;
        return delta > 0
            ? new UndoRedoPanelOperation(UndoRedoPanelOperationKind.Undo, delta)
            : new UndoRedoPanelOperation(UndoRedoPanelOperationKind.Redo, -delta);
    }

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}

public static class UndoRedoPanelModel
{
    public const int MaxDisplayLevels = 400;

    public static UndoRedoPanelState Build(
        string beginDescription,
        IReadOnlyList<string> undoDescriptionsNextFirst,
        IReadOnlyList<string> redoDescriptionsNextFirst)
    {
        var levels = new List<string>(undoDescriptionsNextFirst.Count + redoDescriptionsNextFirst.Count);
        for (int i = undoDescriptionsNextFirst.Count - 1; i >= 0; i--)
            levels.Add(undoDescriptionsNextFirst[i]);
        levels.AddRange(redoDescriptionsNextFirst);

        int undoCount = undoDescriptionsNextFirst.Count;
        int offset = undoCount - (MaxDisplayLevels >> 1);
        if (offset + MaxDisplayLevels > levels.Count) offset = levels.Count - MaxDisplayLevels;
        if (offset < 0) offset = 0;

        var items = new List<UndoRedoPanelItem>();
        int currentSelection = -1;

        if (offset > 0)
        {
            items.Add(new UndoRedoPanelItem("...", UndoRedoPanelItemKind.Elided));
        }
        else
        {
            UndoRedoPanelItemKind kind = undoCount == 0 ? UndoRedoPanelItemKind.Current : UndoRedoPanelItemKind.Begin;
            items.Add(new UndoRedoPanelItem(beginDescription, kind));
            if (undoCount == 0) currentSelection = 0;
        }

        for (int i = offset; i < levels.Count; i++)
        {
            bool elided = items.Count - 1 == MaxDisplayLevels;
            UndoRedoPanelItemKind kind = ItemKindForLevel(i, undoCount, elided);
            items.Add(new UndoRedoPanelItem(elided ? "..." : levels[i], kind));

            if (!elided && i == undoCount - 1)
                currentSelection = items.Count - 1;

            if (items.Count - 1 > MaxDisplayLevels)
                break;
        }

        if (currentSelection == -1)
            currentSelection = items.FindIndex(item => item.Kind == UndoRedoPanelItemKind.Current);

        return new UndoRedoPanelState(items, currentSelection, undoCount, redoDescriptionsNextFirst.Count);
    }

    public static UndoRedoPanelState Build(string beginDescription, UndoManager? undoManager)
        => undoManager == null
            ? new UndoRedoPanelState(Array.Empty<UndoRedoPanelItem>(), -1, 0, 0)
            : Build(beginDescription, undoManager.GetUndoDescriptions(), undoManager.GetRedoDescriptions());

    private static UndoRedoPanelItemKind ItemKindForLevel(int levelIndex, int undoCount, bool elided)
    {
        if (elided) return UndoRedoPanelItemKind.Elided;
        if (levelIndex == undoCount - 1) return UndoRedoPanelItemKind.Current;
        if (levelIndex >= undoCount) return UndoRedoPanelItemKind.Redo;
        return UndoRedoPanelItemKind.Undo;
    }
}
