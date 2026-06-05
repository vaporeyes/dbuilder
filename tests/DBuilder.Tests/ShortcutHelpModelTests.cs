// ABOUTME: Verifies the Help menu shortcut list filtering and collapsible section model.
// ABOUTME: Keeps shortcut help organization stable without opening the Avalonia window.

using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class ShortcutHelpModelTests
{
    [Fact]
    public void BuildSectionsOrganizesEveryCommandIntoStableGroups()
    {
        var sections = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            filter: "");
        int shortcutCount = EditorCommandCatalog.DefaultShortcuts
            .Select(binding => binding.CommandId)
            .Distinct(StringComparer.Ordinal)
            .Count();

        Assert.Equal(shortcutCount, sections.Sum(section => section.Rows.Count));
        Assert.Equal("File and configuration", sections[0].Title);
        Assert.Equal("Project, map, settings, and Help commands.", sections[0].Description);
        Assert.Contains(sections, section => section.Title == "Window editing");
        Assert.Contains(sections, section => section.Title == "2D view and modes");
        Assert.Contains(sections, section => section.Title == "3D navigation");
        Assert.All(sections, section => Assert.Contains(section.Title, ShortcutHelpModel.GroupTitles));
        Assert.All(sections, section => Assert.Equal(section.Rows.Count, section.TotalRows));
        Assert.DoesNotContain(sections.SelectMany(section => section.Rows), row => row.GestureText == "-");
        Assert.All(sections, section =>
            Assert.Equal(section.Rows.OrderBy(row => row.Command.Title, StringComparer.Ordinal), section.Rows));
    }

    [Fact]
    public void EffectiveShortcutCountMatchesVisibleUnfilteredRows()
    {
        var sections = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            filter: "");

        Assert.Equal(
            sections.Sum(section => section.Rows.Count),
            ShortcutHelpModel.EffectiveShortcutCount(EditorCommandCatalog.All, EditorCommandCatalog.DefaultShortcuts));
    }

    [Fact]
    public void BuildSectionsFiltersByCommandTitleIdScopeGroupAndGesture()
    {
        var title = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "texture");
        Assert.Contains(title.SelectMany(section => section.Rows), row => row.Command.Id == "map3d.texture-copy");

        var id = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "window.save");
        Assert.Single(id.SelectMany(section => section.Rows), row => row.Command.Id == "window.save");

        var scope = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "Window commands");
        Assert.All(scope.SelectMany(section => section.Rows), row => Assert.Equal(EditorCommandScope.Window, row.Command.Scope));

        var group = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "Selection groups");
        Assert.All(group.SelectMany(section => section.Rows), row => Assert.Contains("-group-", row.Command.Id, StringComparison.Ordinal));

        var description = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "texture copy");
        Assert.All(description, section => Assert.Equal("3D textures", section.Title));

        var gesture = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "Ctrl/Cmd+S");
        Assert.Contains(gesture.SelectMany(section => section.Rows), row => row.Command.Id == "window.save");
        Assert.Contains(gesture, section => section.TotalRows > section.Rows.Count);

        var actionDescription = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "opened source WAD");
        Assert.Contains(actionDescription.SelectMany(section => section.Rows), row => row.Command.Id == "window.save");

        var modifiers = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "Alt Shift");
        Assert.Contains(modifiers.SelectMany(section => section.Rows), row => row.Command.Id == "map2d.select");

        var unbound = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "window.new-map");
        Assert.Empty(unbound);
    }

    [Fact]
    public void BuildSectionsMatchesMultiWordFiltersAcrossRowMetadata()
    {
        var unfiltered = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "");
        var titleAndGroup = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "3D texture copy");
        Assert.Contains(titleAndGroup.SelectMany(section => section.Rows), row => row.Command.Id == "map3d.texture-copy");
        Assert.All(titleAndGroup, section =>
        {
            var original = Assert.Single(unfiltered, candidate => candidate.Title == section.Title);
            Assert.Equal(original.Rows.Count, section.TotalRows);
        });

        var titleAndGesture = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "save ctrl");
        Assert.Contains(titleAndGesture.SelectMany(section => section.Rows), row => row.Command.Id == "window.save");
    }

    [Theory]
    [InlineData("Toggle Classic Rendering", "tcl", true)]
    [InlineData("Toggle Classic Rendering", "to cl", true)]
    [InlineData("Toggle Classic Rendering", "le cl", true)]
    [InlineData("Open Map", "mo", false)]
    public void MatchesTextUsesUdbStyleWordStartFallback(string text, string search, bool expected)
        => Assert.Equal(expected, ShortcutHelpModel.MatchesText(text, search));

    [Fact]
    public void BuildSectionsUsesUdbStyleWordStartFallbackAcrossMetadata()
    {
        var title = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "tof");
        Assert.Contains(title.SelectMany(section => section.Rows), row => row.Command.Id == "map2d.toggle-full-brightness");

        var metadata = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "win co");
        Assert.Contains(metadata.SelectMany(section => section.Rows), row => row.Command.Id == "window.copy");
    }

    [Fact]
    public void ShortcutRowsExposeUdbStyleActionDescriptions()
    {
        var sections = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            filter: "opened source WAD");

        ShortcutHelpRow save = Assert.Single(sections.SelectMany(section => section.Rows));
        Assert.Equal("window.save", save.Command.Id);
        Assert.Equal("Saves the current map to the opened source WAD file.", save.DescriptionText);
        Assert.Equal(save.Command.Description, save.DescriptionText);
    }

    [Fact]
    public void ShortcutRowsShowStableActionIds()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/ShortcutsWindow.cs"));

        Assert.Contains("Text = row.Command.Id", body, StringComparison.Ordinal);
        Assert.Contains("Foreground = MutedBrush", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ShortcutWindowKeepsFilterBarAndColumnHeaders()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/ShortcutsWindow.cs"));

        Assert.Contains("private Control FilterBar()", body, StringComparison.Ordinal);
        Assert.Contains("Opened += (_, _) => _search.Focus();", body, StringComparison.Ordinal);
        Assert.Contains("Content = \"Clear\"", body, StringComparison.Ordinal);
        Assert.Contains("Content = \"Expand All\"", body, StringComparison.Ordinal);
        Assert.Contains("Content = \"Collapse All\"", body, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions = new ColumnDefinitions(\"Auto,*,Auto,Auto,Auto\")", body, StringComparison.Ordinal);
        Assert.Contains("ShortcutColumnHeader", body, StringComparison.Ordinal);
        Assert.Contains("OptionColumnHeader", body, StringComparison.Ordinal);
        Assert.Contains("CommandColumnHeader", body, StringComparison.Ordinal);
        Assert.Contains("DescriptionColumnHeader", body, StringComparison.Ordinal);
        Assert.Contains("Text = row.DescriptionText", body, StringComparison.Ordinal);
        Assert.Contains("_searchExpandedOverride = null;", body, StringComparison.Ordinal);
        Assert.Contains("ShortcutHelpModel.ResolveSectionExpanded(searching, rememberedExpanded, _searchExpandedOverride)", body, StringComparison.Ordinal);
        Assert.Contains("_searchExpandedOverride = expanded;", body, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultExpansionKeepsCommonSectionsOpen()
    {
        Assert.True(ShortcutHelpModel.IsDefaultExpanded("File and configuration"));
        Assert.True(ShortcutHelpModel.IsDefaultExpanded("Window editing"));
        Assert.True(ShortcutHelpModel.IsDefaultExpanded("2D view and modes"));
        Assert.True(ShortcutHelpModel.IsDefaultExpanded("3D navigation"));
        Assert.False(ShortcutHelpModel.IsDefaultExpanded("3D textures"));
    }

    [Fact]
    public void GroupDescriptionsDescribeEveryStableGroup()
    {
        Assert.All(ShortcutHelpModel.GroupTitles, title =>
        {
            string description = ShortcutHelpModel.GroupDescription(title);
            Assert.False(string.IsNullOrWhiteSpace(description));
            Assert.NotEqual("Shortcut commands.", description);
        });
    }

    [Fact]
    public void ModifierTextListsIgnoredShortcutModifiersAndRepeatableState()
    {
        var command = EditorCommandCatalog.Find("map2d.select");

        Assert.NotNull(command);
        Assert.Equal("Ctrl/Cmd, Alt, Shift", ShortcutHelpModel.ModifierText(command));
        Assert.Equal("Repeatable", ShortcutHelpModel.ModifierText(EditorCommandCatalog.Find("map2d.zoom-in")!));
        Assert.Equal("", ShortcutHelpModel.ModifierText(EditorCommandCatalog.Find("window.save")!));
    }

    [Fact]
    public void BuildSectionsFiltersByRepeatableState()
    {
        var sections = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "repeatable");

        Assert.Contains(sections.SelectMany(section => section.Rows), row => row.Command.Id == "map2d.zoom-in");
        Assert.All(sections.SelectMany(section => section.Rows), row => Assert.True(row.Command.Repeat));
    }

    [Theory]
    [InlineData("", 120, 12, 0, "120 shortcuts in 12 groups")]
    [InlineData("", 1, 1, 0, "1 shortcut in 1 group")]
    [InlineData("texture", 120, 1, 1, "1 shortcut in 1 group matched")]
    [InlineData("texture", 120, 2, 3, "3 shortcuts in 2 groups matched")]
    public void MatchSummaryDescribesAllAndFilteredStates(string filter, int commandCount, int sectionCount, int matchCount, string expected)
        => Assert.Equal(expected, ShortcutHelpModel.MatchSummary(filter, commandCount, sectionCount, matchCount));

    [Fact]
    public void SectionCountTextDescribesFilteredAndUnfilteredRows()
    {
        var command = EditorCommandCatalog.Find("window.save")!;
        var row = new ShortcutHelpRow(command, "Ctrl/Cmd+S", "", command.HelpDescription);
        var section = new ShortcutHelpSection("File and configuration", "Project commands.", [row], true, 12);

        Assert.Equal("1 shortcut", ShortcutHelpModel.SectionCountText(section, searching: false));
        Assert.Equal("1 of 12 shortcuts", ShortcutHelpModel.SectionCountText(section, searching: true));
        Assert.Equal("1 shortcut", ShortcutHelpModel.SectionCountText(section with { TotalRows = 1 }, searching: true));
    }

    [Fact]
    public void ResolveSectionExpandedLetsFilteredToolbarOverrideDefaultExpansion()
    {
        Assert.True(ShortcutHelpModel.ResolveSectionExpanded(searching: true, rememberedExpanded: false, searchExpandedOverride: null));
        Assert.False(ShortcutHelpModel.ResolveSectionExpanded(searching: true, rememberedExpanded: true, searchExpandedOverride: false));
        Assert.True(ShortcutHelpModel.ResolveSectionExpanded(searching: true, rememberedExpanded: false, searchExpandedOverride: true));
        Assert.False(ShortcutHelpModel.ResolveSectionExpanded(searching: false, rememberedExpanded: false, searchExpandedOverride: true));
    }
}
