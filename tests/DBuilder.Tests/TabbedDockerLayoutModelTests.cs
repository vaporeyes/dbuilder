// ABOUTME: Verifies the shared tabbed docker layout metadata for existing editor panels.
// ABOUTME: Keeps UDB-style docker grouping stable while the Avalonia shell grows around it.

using System.Text.Json;
using DBuilder.Editor;
using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class TabbedDockerLayoutModelTests
{
    [Fact]
    public void AllDescriptorsHaveStableKeysAndRegisteredCommands()
    {
        IReadOnlyList<TabbedDockerDescriptor> descriptors = TabbedDockerLayoutModel.All;

        Assert.Equal(descriptors.Count, descriptors.Select(descriptor => descriptor.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(descriptors.Count, descriptors.Select(descriptor => descriptor.CommandId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(descriptors, descriptor =>
        {
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Title));
            Assert.NotNull(EditorCommandCatalog.Find(descriptor.CommandId));
            Assert.All(descriptor.Aliases, alias => Assert.NotNull(EditorCommandCatalog.Find(alias)));
        });
    }

    [Fact]
    public void DescriptorsCoverExistingDockerWindowCommands()
    {
        string[] commandIds = TabbedDockerLayoutModel.All.Select(descriptor => descriptor.CommandId).ToArray();

        Assert.Equal(
            new[]
            {
                "window.tag-explorer",
                "window.comments-panel",
                "window.udbscripts",
                "window.sound-environment-mode",
                "window.blockmap-explorer",
                "window.reject-explorer",
                "window.nodes-viewer",
                "window.status-history",
                "window.show-errors",
            },
            commandIds);
    }

    [Fact]
    public void BuildGroupsDeduplicatesUnknownCommandsAndPreservesTabOrder()
    {
        IReadOnlyList<TabbedDockerGroup> groups = TabbedDockerLayoutModel.BuildGroups(new[]
        {
            "window.rejectexplorermode",
            "missing",
            "window.comments-panel",
            "window.tag-explorer",
            "window.reject-explorer",
            "window.showerrors",
        });

        Assert.Equal(new[] { TabbedDockerArea.Right, TabbedDockerArea.Bottom }, groups.Select(group => group.Area).ToArray());
        Assert.Equal(new[] { "tag-explorer", "comments" }, groups[0].Tabs.Select(tab => tab.Key).ToArray());
        Assert.Equal(new[] { "reject-explorer", "error-log" }, groups[1].Tabs.Select(tab => tab.Key).ToArray());
        Assert.Equal("tag-explorer", groups[0].ActiveTabKey);
        Assert.Equal("reject-explorer", groups[1].ActiveTabKey);
    }

    [Fact]
    public void BuildGroupsRestoresActiveTabsWhenTheyAreVisible()
    {
        IReadOnlyList<TabbedDockerGroup> groups = TabbedDockerLayoutModel.BuildGroups(
            new[]
            {
                "window.tag-explorer",
                "window.comments-panel",
                "window.reject-explorer",
                "window.show-errors",
            },
            new Dictionary<TabbedDockerArea, string>
            {
                [TabbedDockerArea.Right] = "comments",
                [TabbedDockerArea.Bottom] = "error-log",
            });

        Assert.Equal("comments", groups[0].ActiveTabKey);
        Assert.Equal("error-log", groups[1].ActiveTabKey);
    }

    [Fact]
    public void BuildGroupsFallsBackWhenPersistedActiveTabsAreHidden()
    {
        IReadOnlyList<TabbedDockerGroup> groups = TabbedDockerLayoutModel.BuildGroups(
            new[]
            {
                "window.tag-explorer",
                "window.reject-explorer",
            },
            new Dictionary<TabbedDockerArea, string>
            {
                [TabbedDockerArea.Right] = "comments",
                [TabbedDockerArea.Bottom] = "error-log",
            });

        Assert.Equal("tag-explorer", groups[0].ActiveTabKey);
        Assert.Equal("reject-explorer", groups[1].ActiveTabKey);
    }

    [Fact]
    public void ShowDockerAddsMissingDockerAndActivatesItsTab()
    {
        TabbedDockerLayoutState state = TabbedDockerLayoutModel.ShowDocker(
            new[] { "window.tag-explorer", "window.reject-explorer" },
            new Dictionary<TabbedDockerArea, string>
            {
                [TabbedDockerArea.Right] = "tag-explorer",
                [TabbedDockerArea.Bottom] = "reject-explorer",
            },
            "window.comments-panel");

        Assert.Equal(
            new[]
            {
                "window.tag-explorer",
                "window.comments-panel",
                "window.reject-explorer",
            },
            state.ActiveCommandIds);
        Assert.Equal("comments", state.ActiveTabKeysByArea[TabbedDockerArea.Right]);
        Assert.Equal("reject-explorer", state.ActiveTabKeysByArea[TabbedDockerArea.Bottom]);
    }

    [Fact]
    public void ShowDockerCanonicalizesAliasesAndDropsUnknownActiveCommands()
    {
        TabbedDockerLayoutState state = TabbedDockerLayoutModel.ShowDocker(
            new[] { "missing", "window.showerrors" },
            null,
            "window.openscripteditor");

        Assert.Equal(
            new[]
            {
                "window.udbscripts",
                "window.show-errors",
            },
            state.ActiveCommandIds);
        Assert.Equal("scripts", state.ActiveTabKeysByArea[TabbedDockerArea.Right]);
        Assert.Equal("error-log", state.ActiveTabKeysByArea[TabbedDockerArea.Bottom]);
    }

    [Fact]
    public void ShowDockerIgnoresUnknownTargetAndCleansStaleActiveTabs()
    {
        TabbedDockerLayoutState state = TabbedDockerLayoutModel.ShowDocker(
            new[] { "window.tag-explorer" },
            new Dictionary<TabbedDockerArea, string>
            {
                [TabbedDockerArea.Right] = "comments",
                [TabbedDockerArea.Bottom] = "error-log",
            },
            "missing");

        Assert.Equal(new[] { "window.tag-explorer" }, state.ActiveCommandIds);
        Assert.Equal("tag-explorer", state.ActiveTabKeysByArea[TabbedDockerArea.Right]);
        Assert.False(state.ActiveTabKeysByArea.ContainsKey(TabbedDockerArea.Bottom));
    }

    [Fact]
    public void HideDockerRemovesTargetAndKeepsVisibleAreaActiveTab()
    {
        TabbedDockerLayoutState state = TabbedDockerLayoutModel.HideDocker(
            new[] { "window.tag-explorer", "window.comments-panel", "window.reject-explorer" },
            new Dictionary<TabbedDockerArea, string>
            {
                [TabbedDockerArea.Right] = "comments",
                [TabbedDockerArea.Bottom] = "reject-explorer",
            },
            "window.comments-panel");

        Assert.Equal(
            new[]
            {
                "window.tag-explorer",
                "window.reject-explorer",
            },
            state.ActiveCommandIds);
        Assert.Equal("tag-explorer", state.ActiveTabKeysByArea[TabbedDockerArea.Right]);
        Assert.Equal("reject-explorer", state.ActiveTabKeysByArea[TabbedDockerArea.Bottom]);
    }

    [Fact]
    public void HideDockerCanonicalizesAliasTargets()
    {
        TabbedDockerLayoutState state = TabbedDockerLayoutModel.HideDocker(
            new[] { "window.udbscripts", "window.show-errors" },
            new Dictionary<TabbedDockerArea, string>
            {
                [TabbedDockerArea.Right] = "scripts",
                [TabbedDockerArea.Bottom] = "error-log",
            },
            "window.openscripteditor");

        Assert.Equal(new[] { "window.show-errors" }, state.ActiveCommandIds);
        Assert.False(state.ActiveTabKeysByArea.ContainsKey(TabbedDockerArea.Right));
        Assert.Equal("error-log", state.ActiveTabKeysByArea[TabbedDockerArea.Bottom]);
    }

    [Fact]
    public void HideDockerIgnoresUnknownTargetAndCleansUnknownActiveCommands()
    {
        TabbedDockerLayoutState state = TabbedDockerLayoutModel.HideDocker(
            new[] { "missing", "window.showerrors" },
            new Dictionary<TabbedDockerArea, string>
            {
                [TabbedDockerArea.Bottom] = "missing",
            },
            "also-missing");

        Assert.Equal(new[] { "window.show-errors" }, state.ActiveCommandIds);
        Assert.Equal("error-log", state.ActiveTabKeysByArea[TabbedDockerArea.Bottom]);
    }

    [Fact]
    public void ToggleDockerShowsHiddenDockerAndActivatesItsTab()
    {
        TabbedDockerLayoutState state = TabbedDockerLayoutModel.ToggleDocker(
            new[] { "window.tag-explorer" },
            new Dictionary<TabbedDockerArea, string>
            {
                [TabbedDockerArea.Right] = "tag-explorer",
            },
            "window.comments-panel");

        Assert.Equal(new[] { "window.tag-explorer", "window.comments-panel" }, state.ActiveCommandIds);
        Assert.Equal("comments", state.ActiveTabKeysByArea[TabbedDockerArea.Right]);
    }

    [Fact]
    public void ToggleDockerHidesVisibleDockerResolvedThroughAlias()
    {
        TabbedDockerLayoutState state = TabbedDockerLayoutModel.ToggleDocker(
            new[] { "window.udbscripts", "window.show-errors" },
            new Dictionary<TabbedDockerArea, string>
            {
                [TabbedDockerArea.Right] = "scripts",
                [TabbedDockerArea.Bottom] = "error-log",
            },
            "window.openscripteditor");

        Assert.Equal(new[] { "window.show-errors" }, state.ActiveCommandIds);
        Assert.False(state.ActiveTabKeysByArea.ContainsKey(TabbedDockerArea.Right));
        Assert.Equal("error-log", state.ActiveTabKeysByArea[TabbedDockerArea.Bottom]);
    }

    [Fact]
    public void ToggleDockerIgnoresUnknownTargetWhileCleaningState()
    {
        TabbedDockerLayoutState state = TabbedDockerLayoutModel.ToggleDocker(
            new[] { "missing", "window.showerrors" },
            new Dictionary<TabbedDockerArea, string>
            {
                [TabbedDockerArea.Bottom] = "missing",
            },
            "also-missing");

        Assert.Equal(new[] { "window.show-errors" }, state.ActiveCommandIds);
        Assert.Equal("error-log", state.ActiveTabKeysByArea[TabbedDockerArea.Bottom]);
    }

    [Fact]
    public void CollapseGroupStoresExpandedSizeAndActiveTab()
    {
        TabbedDockerGroup group = TabbedDockerLayoutModel.BuildGroups(
            new[] { "window.tag-explorer", "window.comments-panel" },
            new Dictionary<TabbedDockerArea, string>
            {
                [TabbedDockerArea.Right] = "comments",
            })[0];

        TabbedDockerCollapseState state = TabbedDockerLayoutModel.CollapseGroup(group, 320);

        Assert.Equal(TabbedDockerArea.Right, state.Area);
        Assert.True(state.IsCollapsed);
        Assert.Equal(320, state.ExpandedSize);
        Assert.Equal("comments", state.ExpandedActiveTabKey);
    }

    [Fact]
    public void ExpandGroupRestoresVisibleCollapsedActiveTab()
    {
        TabbedDockerGroup group = TabbedDockerLayoutModel.BuildGroups(
            new[] { "window.tag-explorer", "window.comments-panel" },
            new Dictionary<TabbedDockerArea, string>
            {
                [TabbedDockerArea.Right] = "tag-explorer",
            })[0];
        var state = new TabbedDockerCollapseState(TabbedDockerArea.Right, true, 320, "comments");

        TabbedDockerGroup expanded = TabbedDockerLayoutModel.ExpandGroup(group, state);

        Assert.Equal("comments", expanded.ActiveTabKey);
    }

    [Fact]
    public void ExpandGroupFallsBackWhenCollapsedActiveTabIsHidden()
    {
        TabbedDockerGroup group = TabbedDockerLayoutModel.BuildGroups(
            new[] { "window.tag-explorer" },
            new Dictionary<TabbedDockerArea, string>
            {
                [TabbedDockerArea.Right] = "tag-explorer",
            })[0];
        var state = new TabbedDockerCollapseState(TabbedDockerArea.Right, true, 320, "comments");

        TabbedDockerGroup expanded = TabbedDockerLayoutModel.ExpandGroup(group, state);

        Assert.Equal("tag-explorer", expanded.ActiveTabKey);
    }

    [Fact]
    public void SettingsRoundTripPreservesActiveDockersAndTabs()
    {
        var state = new TabbedDockerLayoutState(
            ["window.tag-explorer", "window.comments-panel", "window.show-errors"],
            new Dictionary<TabbedDockerArea, string>
            {
                [TabbedDockerArea.Right] = "comments",
                [TabbedDockerArea.Bottom] = "error-log",
            });

        IReadOnlyDictionary<string, object> written = TabbedDockerLayoutModel.WriteSettings(state);
        TabbedDockerLayoutState restored = TabbedDockerLayoutModel.ReadSettings(
            written.ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal));

        Assert.Equal(state.ActiveCommandIds, restored.ActiveCommandIds);
        Assert.Equal(state.ActiveTabKeysByArea, restored.ActiveTabKeysByArea);
    }

    [Fact]
    public void ReadSettingsCanonicalizesAliasesAndDropsStaleActiveTabs()
    {
        TabbedDockerLayoutState state = TabbedDockerLayoutModel.ReadSettings(new Dictionary<string, object?>
        {
            [TabbedDockerLayoutModel.ActiveDockersSettingKey] = new[] { "window.showerrors", "missing", "window.openscripteditor" },
            [TabbedDockerLayoutModel.ActiveRightTabSettingKey] = "comments",
            [TabbedDockerLayoutModel.ActiveBottomTabSettingKey] = "missing",
        });

        Assert.Equal(new[] { "window.udbscripts", "window.show-errors" }, state.ActiveCommandIds);
        Assert.Equal("scripts", state.ActiveTabKeysByArea[TabbedDockerArea.Right]);
        Assert.Equal("error-log", state.ActiveTabKeysByArea[TabbedDockerArea.Bottom]);
    }

    [Fact]
    public void ReadSettingsAcceptsJsonElementValues()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "activedockers": [ "window.comments-panel", "window.showerrors" ],
              "activerighttab": "comments",
              "activebottomtab": "error-log"
            }
            """);
        Dictionary<string, object?> settings = document.RootElement
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value, StringComparer.Ordinal);

        TabbedDockerLayoutState state = TabbedDockerLayoutModel.ReadSettings(settings);

        Assert.Equal(new[] { "window.comments-panel", "window.show-errors" }, state.ActiveCommandIds);
        Assert.Equal("comments", state.ActiveTabKeysByArea[TabbedDockerArea.Right]);
        Assert.Equal("error-log", state.ActiveTabKeysByArea[TabbedDockerArea.Bottom]);
    }

    [Fact]
    public void FindByCommandIdReturnsDescriptor()
    {
        TabbedDockerDescriptor? descriptor = TabbedDockerLayoutModel.FindByCommandId("window.openscripteditor");

        Assert.NotNull(descriptor);
        Assert.Equal("scripts", descriptor.Key);
        Assert.Equal("Scripts", descriptor.Title);
        Assert.Equal(TabbedDockerArea.Right, descriptor.Area);
        Assert.Equal("window.udbscripts", descriptor.CommandId);
    }
}
