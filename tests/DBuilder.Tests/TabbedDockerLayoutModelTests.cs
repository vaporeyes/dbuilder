// ABOUTME: Verifies the shared tabbed docker layout metadata for existing editor panels.
// ABOUTME: Keeps UDB-style docker grouping stable while the Avalonia shell grows around it.

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
