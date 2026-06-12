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
    int Order);

public sealed record TabbedDockerGroup(
    TabbedDockerArea Area,
    IReadOnlyList<TabbedDockerDescriptor> Tabs);

public static class TabbedDockerLayoutModel
{
    public static IReadOnlyList<TabbedDockerDescriptor> All { get; } = new[]
    {
        Descriptor("tag-explorer", TagExplorerModel.DockerTitle, "window.tag-explorer", TabbedDockerArea.Right, 10),
        Descriptor("comments", CommentsPanelModel.DockerTitle, "window.comments-panel", TabbedDockerArea.Right, 20),
        Descriptor("scripts", UdbScriptDockerModel.DockerTitle, "window.udbscripts", TabbedDockerArea.Right, 30),
        Descriptor("sound-environments", SoundEnvironmentModeModel.DockerTitle, "window.sound-environment-mode", TabbedDockerArea.Right, 40),
        Descriptor("blockmap-explorer", "Blockmap Explorer", "window.blockmap-explorer", TabbedDockerArea.Bottom, 10),
        Descriptor("reject-explorer", "Reject Explorer", "window.reject-explorer", TabbedDockerArea.Bottom, 20),
        Descriptor("nodes-viewer", "Nodes Viewer", "window.nodes-viewer", TabbedDockerArea.Bottom, 30),
        Descriptor("status-history", "Status History", "window.status-history", TabbedDockerArea.Bottom, 40),
        Descriptor("error-log", "Error Log", "window.show-errors", TabbedDockerArea.Bottom, 50),
    };

    public static IReadOnlyList<TabbedDockerGroup> BuildGroups(IEnumerable<string> activeCommandIds)
    {
        var active = new HashSet<string>(activeCommandIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        return All
            .Where(descriptor => active.Contains(descriptor.CommandId))
            .OrderBy(descriptor => descriptor.Area)
            .ThenBy(descriptor => descriptor.Order)
            .GroupBy(descriptor => descriptor.Area)
            .Select(group => new TabbedDockerGroup(group.Key, group.ToArray()))
            .ToArray();
    }

    public static TabbedDockerDescriptor? FindByCommandId(string commandId)
        => All.FirstOrDefault(descriptor => descriptor.CommandId == commandId);

    private static TabbedDockerDescriptor Descriptor(
        string key,
        string title,
        string commandId,
        TabbedDockerArea area,
        int order)
    {
        EditorCommandDescriptor? command = EditorCommandCatalog.Find(commandId);
        return new TabbedDockerDescriptor(key, title, command?.Id ?? commandId, area, order);
    }
}
