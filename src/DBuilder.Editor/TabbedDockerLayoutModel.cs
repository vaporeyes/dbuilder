// ABOUTME: Models the editor's tabbed docker layout without depending on Avalonia controls.
// ABOUTME: Provides stable docker descriptors for the existing UDB-style panel commands.

using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Editor;

public enum TabbedDockerArea
{
    Left,
    Right,
    Bottom,
}

public sealed record TabbedDockerDescriptor(
    string Key,
    string Title,
    string CommandId,
    TabbedDockerArea Area,
    int Order,
    IReadOnlyList<string> Aliases);

public sealed record TabbedDockerGroup(
    TabbedDockerArea Area,
    IReadOnlyList<TabbedDockerDescriptor> Tabs,
    string? ActiveTabKey = null);

public sealed record TabbedDockerLayoutState(
    IReadOnlyList<string> ActiveCommandIds,
    IReadOnlyDictionary<TabbedDockerArea, string> ActiveTabKeysByArea);

public static class TabbedDockerLayoutModel
{
    public static IReadOnlyList<TabbedDockerDescriptor> All { get; } = new[]
    {
        Descriptor("tag-explorer", TagExplorerModel.DockerTitle, "window.tag-explorer", TabbedDockerArea.Right, 10),
        Descriptor("comments", CommentsPanelModel.DockerTitle, "window.comments-panel", TabbedDockerArea.Right, 20),
        Descriptor("scripts", UdbScriptDockerModel.DockerTitle, "window.udbscripts", TabbedDockerArea.Right, 30, "window.openscripteditor"),
        Descriptor("sound-environments", SoundEnvironmentModeModel.DockerTitle, "window.sound-environment-mode", TabbedDockerArea.Right, 40, "window.soundenvironmentmode"),
        Descriptor("blockmap-explorer", "Blockmap Explorer", "window.blockmap-explorer", TabbedDockerArea.Bottom, 10, "window.blockmapexplorermode"),
        Descriptor("reject-explorer", "Reject Explorer", "window.reject-explorer", TabbedDockerArea.Bottom, 20, "window.rejectexplorermode"),
        Descriptor("nodes-viewer", "Nodes Viewer", "window.nodes-viewer", TabbedDockerArea.Bottom, 30, "window.nodesviewermode"),
        Descriptor("status-history", "Status History", "window.status-history", TabbedDockerArea.Bottom, 40),
        Descriptor("error-log", "Error Log", "window.show-errors", TabbedDockerArea.Bottom, 50, "window.showerrors"),
    };

    public static IReadOnlyList<TabbedDockerGroup> BuildGroups(IEnumerable<string> activeCommandIds)
        => BuildGroups(activeCommandIds, activeTabKeysByArea: null);

    public static IReadOnlyList<TabbedDockerGroup> BuildGroups(
        IEnumerable<string> activeCommandIds,
        IReadOnlyDictionary<TabbedDockerArea, string>? activeTabKeysByArea)
    {
        var active = activeCommandIds
            .Select(FindByCommandId)
            .Where(descriptor => descriptor is not null)
            .Select(descriptor => descriptor!)
            .DistinctBy(descriptor => descriptor.Key);

        return All
            .Where(active.Contains)
            .OrderBy(descriptor => descriptor.Area)
            .ThenBy(descriptor => descriptor.Order)
            .GroupBy(descriptor => descriptor.Area)
            .Select(group =>
            {
                TabbedDockerDescriptor[] tabs = group.ToArray();
                return new TabbedDockerGroup(group.Key, tabs, ActiveTabKey(group.Key, tabs, activeTabKeysByArea));
            })
            .ToArray();
    }

    public static TabbedDockerDescriptor? FindByCommandId(string commandId)
        => All.FirstOrDefault(descriptor =>
            descriptor.CommandId == commandId
            || descriptor.Aliases.Contains(commandId, StringComparer.Ordinal));

    public static TabbedDockerLayoutState ShowDocker(
        IEnumerable<string> activeCommandIds,
        IReadOnlyDictionary<TabbedDockerArea, string>? activeTabKeysByArea,
        string commandId)
    {
        TabbedDockerDescriptor? target = FindByCommandId(commandId);
        var activeKeys = activeCommandIds
            .Select(FindByCommandId)
            .Where(descriptor => descriptor is not null)
            .Select(descriptor => descriptor!.Key)
            .ToHashSet(StringComparer.Ordinal);

        if (target is not null)
            activeKeys.Add(target.Key);

        var activeTabs = activeTabKeysByArea is null
            ? new Dictionary<TabbedDockerArea, string>()
            : new Dictionary<TabbedDockerArea, string>(activeTabKeysByArea);
        if (target is not null)
            activeTabs[target.Area] = target.Key;

        string[] activeCanonicalCommands = All
            .Where(descriptor => activeKeys.Contains(descriptor.Key))
            .Select(descriptor => descriptor.CommandId)
            .ToArray();
        IReadOnlyList<TabbedDockerGroup> groups = BuildGroups(activeCanonicalCommands, activeTabs);
        return new TabbedDockerLayoutState(
            activeCanonicalCommands,
            groups.ToDictionary(group => group.Area, group => group.ActiveTabKey ?? group.Tabs[0].Key));
    }

    private static string? ActiveTabKey(
        TabbedDockerArea area,
        IReadOnlyList<TabbedDockerDescriptor> tabs,
        IReadOnlyDictionary<TabbedDockerArea, string>? activeTabKeysByArea)
    {
        if (tabs.Count == 0) return null;
        if (activeTabKeysByArea is not null
            && activeTabKeysByArea.TryGetValue(area, out string? key)
            && tabs.Any(tab => tab.Key == key))
        {
            return key;
        }

        return tabs[0].Key;
    }

    private static TabbedDockerDescriptor Descriptor(
        string key,
        string title,
        string commandId,
        TabbedDockerArea area,
        int order,
        params string[] aliases)
    {
        EditorCommandDescriptor? command = EditorCommandCatalog.Find(commandId);
        return new TabbedDockerDescriptor(key, title, command?.Id ?? commandId, area, order, aliases);
    }
}
