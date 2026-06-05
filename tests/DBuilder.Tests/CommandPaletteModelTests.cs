// ABOUTME: Verifies command palette grouping and UDB-style search matching.
// ABOUTME: Keeps command palette behavior stable before the Avalonia control is wired.

using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class CommandPaletteModelTests
{
    private static readonly HashSet<string> UsableCommands = new(StringComparer.Ordinal)
    {
        "window.open-map",
        "window.open-command-palette",
        "map3d.toggle-classic-rendering",
    };

    [Fact]
    public void MatchesTextAcceptsWholeTextMatches()
    {
        Assert.True(CommandPaletteModel.MatchesText("Open Command Palette", "command palette"));
        Assert.True(CommandPaletteModel.MatchesText("Open Command Palette", "  OPEN   COMMAND  "));
    }

    [Theory]
    [InlineData("Open Map", "op ma", true)]
    [InlineData("Open Command Palette", "op ma", false)]
    [InlineData("Toggle classic rendering", "le cl", true)]
    [InlineData("Toggle classic rendering", "tore", true)]
    [InlineData("Toggle classic rendering", "tcl", true)]
    [InlineData("Toggle Full Brightness", "tof", true)]
    [InlineData("Open Map", "mo", false)]
    public void MatchesTextUsesUdbStyleWordStartFallback(string text, string search, bool expected)
        => Assert.Equal(expected, CommandPaletteModel.MatchesText(text, search));

    [Fact]
    public void BuildGroupsSplitsUsableAndUnavailableActions()
    {
        var groups = CommandPaletteModel.BuildGroups(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            UsableCommands,
            "open");

        var usable = Assert.Single(groups, group => group.Title == "Usable");
        Assert.Contains(usable.Rows, row => row.Command.Id == "window.open-map");
        Assert.Contains(usable.Rows, row => row.Command.Id == "window.open-command-palette");
        Assert.All(usable.Rows, row => Assert.True(row.IsUsable));

        var unavailable = Assert.Single(groups, group => group.Title == "Unavailable");
        Assert.Contains(unavailable.Rows, row => row.Command.Id == "window.opencommandpalette");
        Assert.All(unavailable.Rows, row => Assert.False(row.IsUsable));
        Assert.Equal(unavailable.Rows.OrderBy(row => row.Command.Title, StringComparer.Ordinal), unavailable.Rows);
    }

    [Fact]
    public void BuildGroupsShowsRecentCommandsOnlyWithoutSearchText()
    {
        var groups = CommandPaletteModel.BuildGroups(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            UsableCommands,
            "",
            new[]
            {
                "window.open-command-palette",
                "window.open-map",
                "window.open-command-palette",
            });

        var recent = Assert.Single(groups, group => group.Title == "Recent");
        Assert.Collection(
            recent.Rows,
            row => Assert.Equal("window.open-command-palette", row.Command.Id),
            row => Assert.Equal("window.open-map", row.Command.Id));

        var filtered = CommandPaletteModel.BuildGroups(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            UsableCommands,
            "open",
            new[] { "window.open-command-palette" });

        Assert.DoesNotContain(filtered, group => group.Title == "Recent");
    }

    [Fact]
    public void AddRecentCommandMatchesUdbOverflowTrimming()
    {
        var recent = new List<string>
        {
            "window.open-map",
            "window.save",
            "window.reload-map",
            "window.close-map",
            "window.preferences",
        };

        CommandPaletteModel.AddRecentCommand(recent, "window.open-command-palette");

        Assert.Equal(
            [
                "window.open-command-palette",
                "window.open-map",
                "window.save",
                "window.reload-map",
                "window.preferences",
            ],
            recent);

        CommandPaletteModel.AddRecentCommand(recent, "window.save");

        Assert.Equal(
            [
                "window.save",
                "window.open-command-palette",
                "window.open-map",
                "window.reload-map",
                "window.preferences",
            ],
            recent);
    }

    [Fact]
    public void BuildGroupsExposesCategoryAndShortcutText()
    {
        var groups = CommandPaletteModel.BuildGroups(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            UsableCommands,
            "toggle classic");

        var row = Assert.Single(groups.SelectMany(group => group.Rows), row => row.Command.Id == "map3d.toggle-classic-rendering");
        Assert.Equal("Rendering", row.CategoryText);
        Assert.Equal(EditorCommandCatalog.GestureText(row.Command.Id, EditorCommandCatalog.DefaultShortcuts), row.GestureText);
    }
}
