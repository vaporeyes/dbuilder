// ABOUTME: Declares stable editor command metadata used by shortcut help and future key binding persistence.
// ABOUTME: Keeps command ids, labels, default gestures, and scopes in one catalog instead of duplicating UI text.

namespace DBuilder.IO;

public enum EditorCommandScope
{
    Window,
    Map2D,
    Map3D,
}

public sealed record EditorCommandDescriptor(
    string Id,
    string Title,
    string DefaultGesture,
    EditorCommandScope Scope);

public sealed record EditorShortcutBinding(
    string CommandId,
    EditorCommandScope Scope,
    string Key,
    bool Accelerator = false,
    bool Shift = false,
    bool Alt = false);

public static class EditorCommandCatalog
{
    public static IReadOnlyList<EditorCommandDescriptor> All { get; } = new[]
    {
        new EditorCommandDescriptor("window.undo", "Undo", "Ctrl/Cmd+Z", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.redo", "Redo", "Ctrl/Cmd+Y", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.save", "Save", "Ctrl/Cmd+S", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.cut", "Cut selection", "Ctrl/Cmd+X", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.copy", "Copy selection", "Ctrl/Cmd+C", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.paste", "Paste selection", "Ctrl/Cmd+V", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.duplicate", "Duplicate selection", "Ctrl/Cmd+D", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.delete", "Remove selection", "Delete", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.cancel-draw", "Cancel draw mode", "Esc", EditorCommandScope.Window),

        new EditorCommandDescriptor("map2d.select", "Select element", "Click", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.box-select", "Box-select or move a grabbed vertex/thing", "Left-drag", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.pan", "Pan the view", "Right-drag", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.zoom", "Zoom out / in", "Wheel / - =", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.fit", "Fit map to view", "R", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.edit-properties", "Edit properties", "Double-click", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.split-line", "Split the nearest line", "Right-click", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.mode-vertices", "Vertices mode", "1", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.mode-linedefs", "Linedefs mode", "2", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.mode-sectors", "Sectors mode", "3", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.mode-things", "Things mode", "4", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.toggle-sector-fills", "Toggle sector fills", "S", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.toggle-things", "Toggle things", "T", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.toggle-thing-arrows", "Toggle sprites / direction arrows", "Y", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.draw-sector", "Draw sector", "D", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.draw-lines", "Draw lines", "Shift+D", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.insert", "Insert vertex or thing", "I", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.make-sector", "Make sector at cursor", "M", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.flip", "Flip linedef", "F", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.flip-sidedefs", "Flip sidedefs", "Shift+F", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.align-textures-x", "Align textures X", "A", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.align-textures-y", "Align textures Y", "Shift+A", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.toggle-grid-snap", "Toggle grid snap", "G", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.grid-down", "Decrease grid size", "[", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.grid-up", "Increase grid size", "]", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.toggle-3d", "Enter 3D mode", "Tab", EditorCommandScope.Map2D),

        new EditorCommandDescriptor("map3d.move", "Move", "WASD", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.look", "Look around", "Arrows / drag", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.move-height", "Move up / down", "Q / E", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.walk-mode", "Toggle walk mode (gravity)", "G", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.target-height", "Raise/lower floor, ceiling or thing Z (Shift = by 1)", "Wheel", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.drag-height", "Move a thing or drag a surface height", "Right-drag", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.brightness-down", "Sector brightness down", "[", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.brightness-up", "Sector brightness up", "]", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.copy-texture", "Copy texture", "C", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.apply-texture", "Apply texture", "V", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.browse-texture", "Browse textures", "T", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.align-texture-x", "Align texture X", "A", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.align-texture-y", "Align texture Y", "Shift+A", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.nudge-offset", "Nudge texture offset", "Shift+arrows", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.reset-offsets", "Reset texture offsets", "O", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.select-target", "Select surfaces", "Click", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.clear-target", "Clear selection", "Esc", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.edit-properties", "Edit properties", "Enter", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.delete-target", "Remove targeted thing", "Delete", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.toggle-2d", "Back to 2D mode", "Tab", EditorCommandScope.Map3D),
    };

    public static IReadOnlyList<EditorShortcutBinding> DefaultShortcuts { get; } = new[]
    {
        new EditorShortcutBinding("window.undo", EditorCommandScope.Window, "Z", Accelerator: true),
        new EditorShortcutBinding("window.redo", EditorCommandScope.Window, "Y", Accelerator: true),
        new EditorShortcutBinding("window.save", EditorCommandScope.Window, "S", Accelerator: true),
        new EditorShortcutBinding("window.cut", EditorCommandScope.Window, "X", Accelerator: true),
        new EditorShortcutBinding("window.copy", EditorCommandScope.Window, "C", Accelerator: true),
        new EditorShortcutBinding("window.paste", EditorCommandScope.Window, "V", Accelerator: true),
        new EditorShortcutBinding("window.duplicate", EditorCommandScope.Window, "D", Accelerator: true),
        new EditorShortcutBinding("window.delete", EditorCommandScope.Window, "Delete"),
        new EditorShortcutBinding("window.delete", EditorCommandScope.Window, "Back"),
        new EditorShortcutBinding("window.cancel-draw", EditorCommandScope.Window, "Escape"),
    };

    public static IReadOnlyList<EditorCommandDescriptor> ByScope(EditorCommandScope scope)
        => All.Where(command => command.Scope == scope).ToArray();

    public static string? ResolveShortcut(EditorCommandScope scope, string key, bool accelerator = false, bool shift = false, bool alt = false)
    {
        foreach (var shortcut in DefaultShortcuts)
        {
            if (shortcut.Scope == scope
                && shortcut.Accelerator == accelerator
                && shortcut.Shift == shift
                && shortcut.Alt == alt
                && string.Equals(shortcut.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return shortcut.CommandId;
            }
        }

        return null;
    }
}
