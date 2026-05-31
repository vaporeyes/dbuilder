// ABOUTME: Tests UDB-style UndoRedoPanel timeline construction and selection operation mapping.
// ABOUTME: Covers begin, current undo level, redo rows, elision, and multi-level undo or redo requests.

using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class UndoRedoPanelModelTests
{
    [Fact]
    public void EmptyHistoryShowsBeginAsCurrent()
    {
        UndoRedoPanelState state = UndoRedoPanelModel.Build(
            "Map loaded",
            Array.Empty<string>(),
            Array.Empty<string>());

        UndoRedoPanelItem item = Assert.Single(state.Items);
        Assert.Equal("Map loaded", item.Description);
        Assert.Equal(UndoRedoPanelItemKind.Current, item.Kind);
        Assert.Equal(0, state.CurrentSelection);
        Assert.Equal(UndoRedoPanelOperation.None, state.OperationForSelection(0));
    }

    [Fact]
    public void TimelineOrdersUndoOldestToNewestThenRedo()
    {
        UndoRedoPanelState state = UndoRedoPanelModel.Build(
            "Map loaded",
            new[] { "move 3", "move 2", "move 1" },
            new[] { "move 4", "move 5" });

        Assert.Collection(
            state.Items,
            item => AssertItem(item, "Map loaded", UndoRedoPanelItemKind.Begin),
            item => AssertItem(item, "move 1", UndoRedoPanelItemKind.Undo),
            item => AssertItem(item, "move 2", UndoRedoPanelItemKind.Undo),
            item => AssertItem(item, "move 3", UndoRedoPanelItemKind.Current),
            item => AssertItem(item, "move 4", UndoRedoPanelItemKind.Redo),
            item => AssertItem(item, "move 5", UndoRedoPanelItemKind.Redo));
        Assert.Equal(3, state.CurrentSelection);
    }

    [Fact]
    public void SelectionBeforeCurrentRequestsUndoLevels()
    {
        UndoRedoPanelState state = UndoRedoPanelModel.Build(
            "Map loaded",
            new[] { "move 3", "move 2", "move 1" },
            Array.Empty<string>());

        UndoRedoPanelOperation operation = state.OperationForSelection(1);

        Assert.Equal(UndoRedoPanelOperationKind.Undo, operation.Kind);
        Assert.Equal(2, operation.Levels);
    }

    [Fact]
    public void SelectionAfterCurrentRequestsRedoLevels()
    {
        UndoRedoPanelState state = UndoRedoPanelModel.Build(
            "Map loaded",
            new[] { "move 2", "move 1" },
            new[] { "move 3", "move 4", "move 5" });

        UndoRedoPanelOperation operation = state.OperationForSelection(5);

        Assert.Equal(UndoRedoPanelOperationKind.Redo, operation.Kind);
        Assert.Equal(3, operation.Levels);
    }

    [Fact]
    public void LongHistoryUsesUdbElisionWindowAroundCurrentLevel()
    {
        string[] undos = Enumerable.Range(1, 450).Reverse().Select(i => $"undo {i}").ToArray();
        string[] redos = Enumerable.Range(451, 60).Select(i => $"redo {i}").ToArray();

        UndoRedoPanelState state = UndoRedoPanelModel.Build("Map loaded", undos, redos);

        Assert.Equal(UndoRedoPanelItemKind.Elided, state.Items[0].Kind);
        Assert.Equal("...", state.Items[0].Description);
        Assert.Contains(state.Items, item => item.Kind == UndoRedoPanelItemKind.Current && item.Description == "undo 450");
        Assert.True(state.Items.Count <= UndoRedoPanelModel.MaxDisplayLevels + 2);
        Assert.Equal(UndoRedoPanelOperation.None, state.OperationForSelection(0));
    }

    [Fact]
    public void BuildsFromUndoManagerDescriptionsAndPerformsMultiLevelOperations()
    {
        var map = new DBuilder.Map.MapSet();
        var vertex = map.AddVertex(new DBuilder.Geometry.Vector2D(0, 0));
        var undo = new UndoManager(map);

        undo.CreateUndo("move 1");
        vertex.Position = new DBuilder.Geometry.Vector2D(1, 0);
        undo.CreateUndo("move 2");
        vertex.Position = new DBuilder.Geometry.Vector2D(2, 0);
        undo.CreateUndo("move 3");
        vertex.Position = new DBuilder.Geometry.Vector2D(3, 0);

        UndoRedoPanelState state = UndoRedoPanelModel.Build("Map loaded", undo);
        Assert.Equal(3, state.CurrentSelection);

        Assert.Equal(2, undo.PerformUndo(state.OperationForSelection(1).Levels));
        Assert.Equal(new DBuilder.Geometry.Vector2D(1, 0), map.Vertices[0].Position);
        Assert.Equal(2, undo.RedoCount);

        state = UndoRedoPanelModel.Build("Map loaded", undo);
        Assert.Equal(2, undo.PerformRedo(state.OperationForSelection(3).Levels));
        Assert.Equal(new DBuilder.Geometry.Vector2D(3, 0), map.Vertices[0].Position);
    }

    private static void AssertItem(UndoRedoPanelItem item, string description, UndoRedoPanelItemKind kind)
    {
        Assert.Equal(description, item.Description);
        Assert.Equal(kind, item.Kind);
    }
}
