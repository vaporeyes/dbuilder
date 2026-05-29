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
    private static readonly HashSet<string> RepeatableCommandIds = new(StringComparer.Ordinal)
    {
        "map2d.grid-down",
        "map2d.grid-up",
        "map2d.zoom-in",
        "map2d.zoom-out",
        "map3d.brightness-down",
        "map3d.brightness-up",
        "map3d.nudge-offset-left",
        "map3d.nudge-offset-right",
        "map3d.nudge-offset-up",
        "map3d.nudge-offset-down",
    };

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
        new EditorCommandDescriptor("map2d.zoom-in", "Zoom in", "+", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.zoom-out", "Zoom out", "-", EditorCommandScope.Map2D),
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
        new EditorCommandDescriptor("map2d.finish-draw", "Finish drawing", "Enter", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.cancel-draw", "Cancel drawing", "Esc", EditorCommandScope.Map2D),
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
        new EditorCommandDescriptor("map3d.nudge-offset-left", "Nudge texture offset left", "Shift+Left", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.nudge-offset-right", "Nudge texture offset right", "Shift+Right", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.nudge-offset-up", "Nudge texture offset up", "Shift+Up", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.nudge-offset-down", "Nudge texture offset down", "Shift+Down", EditorCommandScope.Map3D),
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

        new EditorShortcutBinding("map2d.toggle-sector-fills", EditorCommandScope.Map2D, "S"),
        new EditorShortcutBinding("map2d.toggle-things", EditorCommandScope.Map2D, "T"),
        new EditorShortcutBinding("map2d.toggle-thing-arrows", EditorCommandScope.Map2D, "Y"),
        new EditorShortcutBinding("map2d.draw-sector", EditorCommandScope.Map2D, "D"),
        new EditorShortcutBinding("map2d.draw-lines", EditorCommandScope.Map2D, "D", Shift: true),
        new EditorShortcutBinding("map2d.make-sector", EditorCommandScope.Map2D, "M"),
        new EditorShortcutBinding("map2d.insert", EditorCommandScope.Map2D, "I"),
        new EditorShortcutBinding("map2d.insert", EditorCommandScope.Map2D, "Insert"),
        new EditorShortcutBinding("map2d.mode-vertices", EditorCommandScope.Map2D, "D1"),
        new EditorShortcutBinding("map2d.mode-vertices", EditorCommandScope.Map2D, "NumPad1"),
        new EditorShortcutBinding("map2d.mode-linedefs", EditorCommandScope.Map2D, "D2"),
        new EditorShortcutBinding("map2d.mode-linedefs", EditorCommandScope.Map2D, "NumPad2"),
        new EditorShortcutBinding("map2d.mode-sectors", EditorCommandScope.Map2D, "D3"),
        new EditorShortcutBinding("map2d.mode-sectors", EditorCommandScope.Map2D, "NumPad3"),
        new EditorShortcutBinding("map2d.mode-things", EditorCommandScope.Map2D, "D4"),
        new EditorShortcutBinding("map2d.mode-things", EditorCommandScope.Map2D, "NumPad4"),
        new EditorShortcutBinding("map2d.flip", EditorCommandScope.Map2D, "F"),
        new EditorShortcutBinding("map2d.flip-sidedefs", EditorCommandScope.Map2D, "F", Shift: true),
        new EditorShortcutBinding("map2d.align-textures-x", EditorCommandScope.Map2D, "A"),
        new EditorShortcutBinding("map2d.align-textures-y", EditorCommandScope.Map2D, "A", Shift: true),
        new EditorShortcutBinding("map2d.toggle-grid-snap", EditorCommandScope.Map2D, "G"),
        new EditorShortcutBinding("map2d.grid-down", EditorCommandScope.Map2D, "OemOpenBrackets"),
        new EditorShortcutBinding("map2d.grid-up", EditorCommandScope.Map2D, "OemCloseBrackets"),
        new EditorShortcutBinding("map2d.finish-draw", EditorCommandScope.Map2D, "Enter"),
        new EditorShortcutBinding("map2d.cancel-draw", EditorCommandScope.Map2D, "Escape"),
        new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, "R"),
        new EditorShortcutBinding("map2d.zoom-in", EditorCommandScope.Map2D, "OemPlus"),
        new EditorShortcutBinding("map2d.zoom-in", EditorCommandScope.Map2D, "Add"),
        new EditorShortcutBinding("map2d.zoom-out", EditorCommandScope.Map2D, "OemMinus"),
        new EditorShortcutBinding("map2d.zoom-out", EditorCommandScope.Map2D, "Subtract"),
        new EditorShortcutBinding("map2d.toggle-3d", EditorCommandScope.Map2D, "Tab"),
        new EditorShortcutBinding("map3d.toggle-2d", EditorCommandScope.Map3D, "Tab"),
        new EditorShortcutBinding("map3d.walk-mode", EditorCommandScope.Map3D, "G"),
        new EditorShortcutBinding("map3d.brightness-down", EditorCommandScope.Map3D, "OemOpenBrackets"),
        new EditorShortcutBinding("map3d.brightness-up", EditorCommandScope.Map3D, "OemCloseBrackets"),
        new EditorShortcutBinding("map3d.copy-texture", EditorCommandScope.Map3D, "C"),
        new EditorShortcutBinding("map3d.apply-texture", EditorCommandScope.Map3D, "V"),
        new EditorShortcutBinding("map3d.align-texture-x", EditorCommandScope.Map3D, "A"),
        new EditorShortcutBinding("map3d.align-texture-y", EditorCommandScope.Map3D, "A", Shift: true),
        new EditorShortcutBinding("map3d.edit-properties", EditorCommandScope.Map3D, "Enter"),
        new EditorShortcutBinding("map3d.clear-target", EditorCommandScope.Map3D, "Escape"),
        new EditorShortcutBinding("map3d.reset-offsets", EditorCommandScope.Map3D, "O"),
        new EditorShortcutBinding("map3d.delete-target", EditorCommandScope.Map3D, "Delete"),
        new EditorShortcutBinding("map3d.delete-target", EditorCommandScope.Map3D, "Back"),
        new EditorShortcutBinding("map3d.browse-texture", EditorCommandScope.Map3D, "T"),
        new EditorShortcutBinding("map3d.nudge-offset-left", EditorCommandScope.Map3D, "Left", Shift: true),
        new EditorShortcutBinding("map3d.nudge-offset-right", EditorCommandScope.Map3D, "Right", Shift: true),
        new EditorShortcutBinding("map3d.nudge-offset-up", EditorCommandScope.Map3D, "Up", Shift: true),
        new EditorShortcutBinding("map3d.nudge-offset-down", EditorCommandScope.Map3D, "Down", Shift: true),
    };

    public static IReadOnlyList<EditorCommandDescriptor> ByScope(EditorCommandScope scope)
        => All.Where(command => command.Scope == scope).ToArray();

    public static string GestureText(string commandId, IReadOnlyList<EditorShortcutBinding> bindings)
    {
        var gestures = bindings
            .Where(binding => string.Equals(binding.CommandId, commandId, StringComparison.Ordinal))
            .Select(GestureText)
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return gestures.Length == 0 ? "-" : string.Join(" / ", gestures);
    }

    public static string CommandHint(string commandId, IReadOnlyList<EditorShortcutBinding> bindings)
    {
        var command = All.FirstOrDefault(item => string.Equals(item.Id, commandId, StringComparison.Ordinal));
        if (command is null) return commandId;
        string gesture = GestureText(commandId, bindings);
        return gesture == "-" ? command.Title : $"{gesture} {command.Title}";
    }

    public static string CommandHints(IReadOnlyList<EditorShortcutBinding> bindings, params string[] commandIds)
        => string.Join("; ", commandIds.Select(commandId => CommandHint(commandId, bindings)));

    public static string GestureText(EditorShortcutBinding binding)
    {
        if (string.IsNullOrWhiteSpace(binding.Key)) return "";
        var parts = new List<string>();
        if (binding.Accelerator) parts.Add("Ctrl/Cmd");
        if (binding.Alt) parts.Add("Alt");
        if (binding.Shift) parts.Add("Shift");
        parts.Add(DisplayKey(binding.Key));
        return string.Join("+", parts);
    }

    public static string OverrideText(IEnumerable<EditorShortcutBinding> overrides)
        => string.Join("; ", overrides.Select(binding => $"{binding.CommandId}={GestureText(binding)}"));

    public static List<EditorShortcutBinding> ParseOverrideText(string? text)
    {
        var result = new List<EditorShortcutBinding>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var byId = All.ToDictionary(command => command.Id, StringComparer.Ordinal);
        foreach (string rawEntry in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int equals = rawEntry.IndexOf('=');
            if (equals <= 0 || equals == rawEntry.Length - 1) continue;

            string commandId = rawEntry[..equals].Trim();
            string gesture = rawEntry[(equals + 1)..].Trim();
            if (!byId.TryGetValue(commandId, out var command)) continue;
            if (!TryParseGesture(commandId, command.Scope, gesture, out var binding)) continue;
            result.Add(binding);
        }

        return result;
    }

    public static bool TryParseGesture(string commandId, EditorCommandScope scope, string gesture, out EditorShortcutBinding binding)
    {
        binding = new EditorShortcutBinding(commandId, scope, "");
        if (string.IsNullOrWhiteSpace(gesture)) return false;

        bool accelerator = false, shift = false, alt = false;
        string key = "";
        foreach (string rawPart in SplitGestureParts(gesture))
        {
            string part = rawPart.Trim();
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Cmd", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Command", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Ctrl/Cmd", StringComparison.OrdinalIgnoreCase))
                accelerator = true;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                shift = true;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Option", StringComparison.OrdinalIgnoreCase))
                alt = true;
            else
                key = ParseDisplayKey(part);
        }

        if (key.Length == 0) return false;
        binding = new EditorShortcutBinding(commandId, scope, key, accelerator, shift, alt);
        return true;
    }

    private static IEnumerable<string> SplitGestureParts(string gesture)
    {
        string text = gesture.Trim();
        if (text.Equals("+", StringComparison.OrdinalIgnoreCase))
        {
            yield return "+";
            yield break;
        }

        const string numpadPlus = "NumPad+";
        if (text.EndsWith(numpadPlus, StringComparison.OrdinalIgnoreCase))
            text = text[..^numpadPlus.Length] + "Add";
        else if (text.EndsWith("+", StringComparison.Ordinal))
            text = text[..^1] + "+OemPlus";

        foreach (string part in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return part;
    }

    private static string DisplayKey(string key) => key switch
    {
        "OemOpenBrackets" => "[",
        "OemCloseBrackets" => "]",
        "OemTilde" => "~",
        "OemSemicolon" => ";",
        "OemQuotes" => "'",
        "OemComma" => ",",
        "OemPeriod" => ".",
        "OemQuestion" => "?",
        "OemBackslash" => "\\",
        "OemPlus" => "+",
        "Add" => "NumPad+",
        "OemMinus" => "-",
        "Subtract" => "NumPad-",
        "Decimal" => "NumPad.",
        "Multiply" => "NumPad*",
        "Divide" => "NumPad/",
        "Escape" => "Esc",
        "Back" => "Backspace",
        "CapsLock" => "CapsLock",
        "D1" => "1",
        "D2" => "2",
        "D3" => "3",
        "D4" => "4",
        _ => key,
    };

    private static string ParseDisplayKey(string key)
    {
        if (key.Equals("Esc", StringComparison.OrdinalIgnoreCase)) return "Escape";
        if (key.Equals("Del", StringComparison.OrdinalIgnoreCase)) return "Delete";
        if (key.Equals("Backspace", StringComparison.OrdinalIgnoreCase)) return "Back";
        if (key.Equals("CapsLock", StringComparison.OrdinalIgnoreCase)) return "CapsLock";
        if (key.Equals("NumPad+", StringComparison.OrdinalIgnoreCase)) return "Add";
        if (key.Equals("NumPad-", StringComparison.OrdinalIgnoreCase)) return "Subtract";
        if (key.Equals("NumPad.", StringComparison.OrdinalIgnoreCase)) return "Decimal";
        if (key.Equals("NumPad*", StringComparison.OrdinalIgnoreCase)) return "Multiply";
        if (key.Equals("NumPad/", StringComparison.OrdinalIgnoreCase)) return "Divide";

        return key switch
        {
            "[" => "OemOpenBrackets",
            "]" => "OemCloseBrackets",
            "~" => "OemTilde",
            ";" => "OemSemicolon",
            "'" => "OemQuotes",
            "," => "OemComma",
            "." => "OemPeriod",
            "?" => "OemQuestion",
            "\\" => "OemBackslash",
            "+" => "OemPlus",
            "-" => "OemMinus",
            "1" => "D1",
            "2" => "D2",
            "3" => "D3",
            "4" => "D4",
            _ => key,
        };
    }

    public static IReadOnlyList<EditorShortcutBinding> EffectiveShortcuts(IEnumerable<EditorShortcutBinding>? overrides)
    {
        if (overrides is null) return DefaultShortcuts;

        var known = All.Select(command => command.Id).ToHashSet(StringComparer.Ordinal);
        var validOverrides = overrides
            .Where(binding => known.Contains(binding.CommandId) && !string.IsNullOrWhiteSpace(binding.Key))
            .ToArray();
        if (validOverrides.Length == 0) return DefaultShortcuts;

        var replaced = validOverrides.Select(binding => binding.CommandId).ToHashSet(StringComparer.Ordinal);
        return DefaultShortcuts
            .Where(binding => !replaced.Contains(binding.CommandId))
            .Concat(validOverrides)
            .ToArray();
    }

    public static string? ResolveShortcut(EditorCommandScope scope, string key, bool accelerator = false, bool shift = false, bool alt = false)
        => ResolveShortcut(DefaultShortcuts, scope, key, accelerator, shift, alt);

    public static string? ResolveShortcut(
        IReadOnlyList<EditorShortcutBinding> bindings,
        EditorCommandScope scope,
        string key,
        bool accelerator = false,
        bool shift = false,
        bool alt = false)
    {
        for (int i = bindings.Count - 1; i >= 0; i--)
        {
            var shortcut = bindings[i];
            if (shortcut.Scope == scope
                && shortcut.Accelerator == accelerator
                && shortcut.Shift == shift
                && shortcut.Alt == alt
                && string.Equals(NormalizeKey(shortcut.Key), NormalizeKey(key), StringComparison.OrdinalIgnoreCase))
            {
                return shortcut.CommandId;
            }
        }

        return null;
    }

    public static bool IsRepeatable(string commandId) => RepeatableCommandIds.Contains(commandId);

    public static string ShortcutPressKey(EditorCommandScope scope, string key, bool accelerator = false, bool shift = false, bool alt = false)
        => $"{scope}:{NormalizeKey(key)}:{accelerator}:{shift}:{alt}";

    public static string ShortcutReleasePrefix(EditorCommandScope scope, string key)
        => $"{scope}:{NormalizeKey(key)}:";

    private static string NormalizeKey(string key) => ParseDisplayKey(key);
}
