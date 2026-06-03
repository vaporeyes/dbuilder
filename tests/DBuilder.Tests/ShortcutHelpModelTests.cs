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
        Assert.DoesNotContain(sections.SelectMany(section => section.Rows), row => row.GestureText == "-");
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

        var unbound = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "window.new-map");
        Assert.Empty(unbound);
    }

    [Fact]
    public void BuildSectionsMatchesMultiWordFiltersAcrossRowMetadata()
    {
        var titleAndGroup = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "3D texture copy");
        Assert.Contains(titleAndGroup.SelectMany(section => section.Rows), row => row.Command.Id == "map3d.texture-copy");

        var titleAndGesture = ShortcutHelpModel.BuildSections(
            EditorCommandCatalog.All,
            EditorCommandCatalog.DefaultShortcuts,
            "save ctrl");
        Assert.Contains(titleAndGesture.SelectMany(section => section.Rows), row => row.Command.Id == "window.save");
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

    [Theory]
    [InlineData("", 120, 12, 0, "120 shortcuts in 12 groups")]
    [InlineData("texture", 120, 12, 1, "1 shortcut matched")]
    [InlineData("texture", 120, 12, 2, "2 shortcuts matched")]
    public void MatchSummaryDescribesAllAndFilteredStates(string filter, int commandCount, int sectionCount, int matchCount, string expected)
        => Assert.Equal(expected, ShortcutHelpModel.MatchSummary(filter, commandCount, sectionCount, matchCount));
}
