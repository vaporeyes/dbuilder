// ABOUTME: Builds the organized shortcut help sections shown by the editor Help menu.
// ABOUTME: Keeps shortcut filtering and grouping testable outside the Avalonia window.

namespace DBuilder.IO;

public sealed record ShortcutHelpRow(EditorCommandDescriptor Command, string GestureText);

public sealed record ShortcutHelpSection(string Title, string Description, IReadOnlyList<ShortcutHelpRow> Rows, bool DefaultExpanded);

public static class ShortcutHelpModel
{
    public static readonly string[] GroupTitles =
    {
        "File and configuration",
        "Window editing",
        "Tools and panels",
        "Selection groups",
        "Script commands",
        "2D view and modes",
        "2D drawing and geometry",
        "2D editing",
        "3D navigation",
        "3D selection and things",
        "3D textures",
        "3D surfaces and rendering",
    };

    public static IReadOnlyList<ShortcutHelpSection> BuildSections(
        IReadOnlyList<EditorCommandDescriptor> commands,
        IReadOnlyList<EditorShortcutBinding> bindings,
        string? filter)
    {
        string text = filter?.Trim() ?? "";
        var sections = new List<ShortcutHelpSection>();
        foreach (string title in GroupTitles)
        {
            var rows = commands
                .Where(command => string.Equals(GroupTitle(command), title, StringComparison.Ordinal))
                .Select(command => new ShortcutHelpRow(command, EditorCommandCatalog.GestureText(command.Id, bindings)))
                .Where(row => row.GestureText != "-")
                .Where(row => text.Length == 0 || Matches(row, title, GroupDescription(title), text))
                .ToArray();

            if (rows.Length > 0)
                sections.Add(new ShortcutHelpSection(title, GroupDescription(title), rows, IsDefaultExpanded(title)));
        }

        return sections;
    }

    public static string MatchSummary(string? filter, int commandCount, int sectionCount, int matchCount)
    {
        string text = filter?.Trim() ?? "";
        return text.Length > 0
            ? $"{matchCount} shortcut{Plural(matchCount)} matched"
            : $"{commandCount} shortcuts in {sectionCount} groups";
    }

    public static bool IsDefaultExpanded(string title)
        => title is "File and configuration" or "Window editing" or "2D view and modes" or "3D navigation";

    public static string GroupDescription(string title)
        => title switch
        {
            "File and configuration" => "Project, map, settings, and Help commands.",
            "Window editing" => "Selection, clipboard, properties, undo, and editing commands.",
            "Tools and panels" => "Explorers, browsers, analysis tools, and utility panels.",
            "Selection groups" => "Stored selection group commands.",
            "Script commands" => "UDBScript browser and script execution shortcuts.",
            "2D view and modes" => "2D camera, zoom, mode, and display commands.",
            "2D drawing and geometry" => "Draw, bridge, slope, and sector construction commands.",
            "2D editing" => "Grid, object, geometry, and selection editing commands.",
            "3D navigation" => "3D camera movement and view controls.",
            "3D selection and things" => "3D object selection, placement, and thing editing commands.",
            "3D textures" => "Texture copy, paste, alignment, offsets, and pegging commands.",
            "3D surfaces and rendering" => "Brightness, heights, slopes, fog, lighting, and render toggles.",
            _ => "Shortcut commands.",
        };

    private static bool Matches(ShortcutHelpRow row, string groupTitle, string groupDescription, string filter)
        => Contains(groupTitle, filter)
            || Contains(groupDescription, filter)
            || Contains(row.Command.Title, filter)
            || Contains(row.Command.Id, filter)
            || Contains(ScopeTitle(row.Command.Scope), filter)
            || Contains(row.GestureText, filter);

    private static bool Contains(string value, string filter)
        => value.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static string GroupTitle(EditorCommandDescriptor command)
    {
        string id = command.Id;
        if (command.Title.Contains("Script", StringComparison.OrdinalIgnoreCase) || id.Contains("udbscript", StringComparison.OrdinalIgnoreCase))
            return "Script commands";

        return command.Scope switch
        {
            EditorCommandScope.Window => WindowGroup(id),
            EditorCommandScope.Map2D => Map2DGroup(id),
            EditorCommandScope.Map3D => Map3DGroup(id),
            _ => ScopeTitle(command.Scope),
        };
    }

    private static string WindowGroup(string id)
    {
        if (id.Contains("-group-", StringComparison.Ordinal)) return "Selection groups";
        if (ContainsAny(id, "tag", "explorer", "viewer", "sound", "panel", "setup", "prefab", "export", "comments"))
            return "Tools and panels";
        if (ContainsAny(id, "cut", "copy", "paste", "duplicate", "delete", "properties", "select", "align", "make-door", "tag-range", "undo", "redo"))
            return "Window editing";
        return "File and configuration";
    }

    private static string Map2DGroup(string id)
    {
        if (ContainsAny(id, "mode", "view-mode", "toggle", "zoom", "fit", "pan")) return "2D view and modes";
        if (ContainsAny(id, "draw", "3dfloor", "slope", "bridge")) return "2D drawing and geometry";
        if (ContainsAny(id, "grid", "align", "sector", "linedef", "thing", "insert", "flip", "select", "make-sector"))
            return "2D editing";
        return "2D editing";
    }

    private static string Map3DGroup(string id)
    {
        if (ContainsAny(id, "move", "camera", "orbit", "look", "walk", "gravity")) return "3D navigation";
        if (ContainsAny(id, "texture", "offset", "unpegged", "align", "auto-align", "fit-textures")) return "3D textures";
        if (ContainsAny(id, "brightness", "slope", "render", "fog", "sky", "light", "scale", "height", "sector")) return "3D surfaces and rendering";
        if (ContainsAny(id, "thing", "select", "copy", "cut", "paste", "delete", "insert", "rotate", "pitch", "roll", "edit", "clear"))
            return "3D selection and things";
        return "3D selection and things";
    }

    private static bool ContainsAny(string value, params string[] parts)
        => parts.Any(part => value.Contains(part, StringComparison.OrdinalIgnoreCase));

    private static string Plural(int count)
        => count == 1 ? "" : "s";

    private static string ScopeTitle(EditorCommandScope scope)
        => scope switch
        {
            EditorCommandScope.Window => "Window commands",
            EditorCommandScope.Map2D => "2D editing",
            EditorCommandScope.Map3D => "3D mode",
            _ => scope.ToString(),
        };
}
