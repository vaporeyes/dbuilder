// ABOUTME: Models UDBScript docker tree, menu labels, and slot assignment behavior.
// ABOUTME: Keeps script docker UI data aligned with upstream sorting, filtering, and labels.

namespace DBuilder.IO;

public enum UdbScriptDockerNodeKind
{
    Folder,
    Script,
}

public sealed record UdbScriptDockerNode(
    UdbScriptDockerNodeKind Kind,
    string Text,
    string ImageKey,
    string Path,
    string Hash,
    bool Expanded,
    UdbScriptInfo? Script,
    IReadOnlyList<UdbScriptDockerNode> Children);

public sealed record UdbScriptSlotMenuItem(
    int Slot,
    string Text);

public enum UdbScriptDockerMenuItemKind
{
    Command,
    Submenu,
    Separator,
    Slot,
}

public sealed record UdbScriptDockerMenuItem(
    UdbScriptDockerMenuItemKind Kind,
    string Text,
    int Slot,
    IReadOnlyList<UdbScriptDockerMenuItem> Children);

public sealed record UdbScriptDockerSelection(
    UdbScriptInfo? CurrentScript,
    string Description,
    IReadOnlyList<UdbScriptOption> Options);

public sealed record UdbScriptDockerResetOptionsResult(
    UdbScriptInfo? Script,
    IReadOnlyList<UdbScriptSettingOperation> Operations);

public sealed record UdbScriptDockerApplyOptionsResult(
    UdbScriptInfo? Script,
    IReadOnlyList<UdbScriptSettingOperation> Operations);

public sealed record UdbScriptDockerLayoutMetadata(
    string SplitOrientation,
    string TreeSelectionMode,
    bool TreeHideSelection,
    bool TreeShowNodeToolTips,
    bool DescriptionReadOnly,
    string DescriptionScrollBars,
    int ActionButtonColumns);

public static class UdbScriptDockerModel
{
    public const string DockerKey = "udbscript";
    public const string DockerTitle = "Scripts";
    public const string FolderImageKey = "Folder";
    public const string ScriptImageKey = "Script";
    public const string FilterLabel = "Filter:";
    public const string DescriptionLabel = "Script description";
    public const string OptionsLabel = "Script options";
    public const string OptionsButtonText = "Options";
    public const string ResetButtonText = "Reset";
    public const string RunButtonText = "Run";
    public const string EditMenuText = "Edit";
    public const string SetSlotMenuText = "Set slot";
    public const string ClearSlotMenuText = "Clear slot";
    public const string OpenInExplorerMenuText = "Open in Explorer";
    public const string NotAssignedSlotText = "not assigned";
    public const string NoHotkeyText = "no hotkey";
    public const string ScriptSlotSettingPrefix = "scriptslots.slot";
    public const string SplitOrientation = "Horizontal";
    public const string TreeSelectionMode = "SingleSelect";
    public const string DescriptionScrollBars = "Both";
    public const int ActionButtonColumns = 2;

    public static UdbScriptDockerLayoutMetadata LayoutMetadata()
        => new(
            SplitOrientation,
            TreeSelectionMode,
            TreeHideSelection: false,
            TreeShowNodeToolTips: true,
            DescriptionReadOnly: true,
            DescriptionScrollBars,
            ActionButtonColumns);

    public static IReadOnlyList<UdbScriptDockerNode> BuildTree(
        UdbScriptDirectory root,
        string filterText,
        IReadOnlyDictionary<int, UdbScriptInfo?> slotAssignments,
        IReadOnlyDictionary<int, string> hotkeys,
        IReadOnlySet<string>? collapsedDirectoryHashes = null)
    {
        string normalizedFilter = filterText.ToLowerInvariant().Trim();
        return BuildDirectoryChildren(root, normalizedFilter, slotAssignments, hotkeys, collapsedDirectoryHashes);
    }

    public static UdbScriptDockerSelection Selection(UdbScriptDockerNode? node)
    {
        if (node?.Script is not UdbScriptInfo script)
            return new UdbScriptDockerSelection(null, "", Array.Empty<UdbScriptOption>());

        return new UdbScriptDockerSelection(script, script.Description, script.Options);
    }

    public static UdbScriptDockerSelection ApplySelection(
        UdbScriptDockerSelection current,
        UdbScriptDockerNode? node)
    {
        if (node is null)
            return new UdbScriptDockerSelection(null, "", Array.Empty<UdbScriptOption>());

        if (node.Script is UdbScriptInfo script)
            return new UdbScriptDockerSelection(script, script.Description, script.Options);

        return new UdbScriptDockerSelection(current.CurrentScript, current.Description, Array.Empty<UdbScriptOption>());
    }

    public static UdbScriptDirectory ReplaceScript(
        UdbScriptDirectory root,
        UdbScriptInfo script)
    {
        UdbScriptDirectory[] directories = root.Directories
            .Select(directory => ReplaceScript(directory, script))
            .ToArray();
        UdbScriptInfo[] scripts = root.Scripts
            .Select(existing => string.Equals(existing.ScriptFile, script.ScriptFile, StringComparison.Ordinal)
                ? script
                : existing)
            .ToArray();

        return root with { Directories = directories, Scripts = scripts };
    }

    public static UdbScriptDockerResetOptionsResult ResetSelectedScriptOptions(UdbScriptInfo? script)
    {
        if (script is null)
            return new UdbScriptDockerResetOptionsResult(null, Array.Empty<UdbScriptSettingOperation>());

        UdbScriptOption[] resetOptions = script.Options
            .Select(option => option with { Value = option.DefaultValue })
            .ToArray();
        UdbScriptSettingOperation[] operations = script.Options
            .Select(option => new UdbScriptSettingOperation(UdbScriptSettingOperationKind.Delete, option.SettingKey))
            .ToArray();

        return new UdbScriptDockerResetOptionsResult(script with { Options = resetOptions }, operations);
    }

    public static UdbScriptDockerApplyOptionsResult ApplyEditedScriptOptions(
        UdbScriptInfo? script,
        IReadOnlyList<UdbScriptOption> options)
    {
        if (script is null)
            return new UdbScriptDockerApplyOptionsResult(null, Array.Empty<UdbScriptSettingOperation>());

        UdbScriptInfo edited = script with { Options = options.ToArray() };
        return new UdbScriptDockerApplyOptionsResult(edited, UdbScriptDiscovery.SaveOptionValueOperations(edited));
    }

    public static string AssignedSlotStatusText(UdbScriptInfo script, int slot, int settingChangeCount)
        => $"UDBScript assigned to slot {slot}: {script.Name} ({SettingChangeText(settingChangeCount)})";

    public static string ClearedSlotStatusText(UdbScriptInfo script, int slot, int settingChangeCount)
        => $"UDBScript cleared from slot {slot}: {script.Name} ({SettingChangeText(settingChangeCount)})";

    public static string OptionsEditedStatusText(UdbScriptInfo script, int settingChangeCount)
        => $"UDBScript options edited: {script.Name} ({SettingChangeText(settingChangeCount)})";

    public static string OptionsResetStatusText(UdbScriptInfo script, int settingChangeCount)
        => $"UDBScript reset options requested: {script.Name} ({SettingChangeText(settingChangeCount)})";

    public static IReadOnlyList<UdbScriptDockerMenuItem> FileContextMenuItems(
        int slotCount = UdbScriptActions.ScriptSlotCount)
    {
        var slotItems = new List<UdbScriptDockerMenuItem>
        {
            MenuCommand(ClearSlotMenuText),
            new(UdbScriptDockerMenuItemKind.Separator, "", 0, Array.Empty<UdbScriptDockerMenuItem>()),
        };

        for (int slot = 1; slot <= slotCount; slot++)
        {
            slotItems.Add(new(
                UdbScriptDockerMenuItemKind.Slot,
                "Slot " + slot,
                slot,
                Array.Empty<UdbScriptDockerMenuItem>()));
        }

        return new[]
        {
            MenuCommand(EditMenuText),
            new UdbScriptDockerMenuItem(UdbScriptDockerMenuItemKind.Submenu, SetSlotMenuText, 0, slotItems),
        };
    }

    public static IReadOnlyList<UdbScriptDockerMenuItem> FolderContextMenuItems()
        => new[] { MenuCommand(OpenInExplorerMenuText) };

    public static IReadOnlySet<string> CollapseDirectory(
        IReadOnlySet<string> collapsedDirectoryHashes,
        UdbScriptDirectory directory)
        => SetDirectoryCollapsed(collapsedDirectoryHashes, directory.Hash, collapsed: true);

    public static IReadOnlySet<string> ExpandDirectory(
        IReadOnlySet<string> collapsedDirectoryHashes,
        UdbScriptDirectory directory)
        => SetDirectoryCollapsed(collapsedDirectoryHashes, directory.Hash, collapsed: false);

    public static IReadOnlySet<string> SetDirectoryCollapsed(
        IReadOnlySet<string> collapsedDirectoryHashes,
        string directoryHash,
        bool collapsed)
    {
        var result = new HashSet<string>(collapsedDirectoryHashes, StringComparer.Ordinal);
        if (collapsed)
            result.Add(directoryHash);
        else
            result.Remove(directoryHash);
        return result;
    }

    public static UdbScriptDockerNode? FindScriptNode(
        IReadOnlyList<UdbScriptDockerNode> nodes,
        string scriptFile)
    {
        foreach (UdbScriptDockerNode node in nodes)
        {
            if (node.Script is not null && node.Script.ScriptFile == scriptFile)
                return node;

            UdbScriptDockerNode? child = FindScriptNode(node.Children, scriptFile);
            if (child is not null)
                return child;
        }

        return null;
    }

    public static IReadOnlyList<UdbScriptSlotMenuItem> SlotMenuItems(
        IReadOnlyDictionary<int, UdbScriptInfo?> slotAssignments,
        IReadOnlyDictionary<int, string> hotkeys,
        int slotCount = UdbScriptActions.ScriptSlotCount)
    {
        var items = new List<UdbScriptSlotMenuItem>();
        for (int slot = 1; slot <= slotCount; slot++)
        {
            string name = slotAssignments.TryGetValue(slot, out UdbScriptInfo? script) && script is not null
                ? script.Name
                : NotAssignedSlotText;
            items.Add(new UdbScriptSlotMenuItem(slot, SlotMenuText(slot, name, HotkeyText(slot, hotkeys))));
        }

        return items;
    }

    public static IReadOnlyDictionary<int, UdbScriptInfo?> AssignSlot(
        IReadOnlyDictionary<int, UdbScriptInfo?> slotAssignments,
        int slot,
        UdbScriptInfo? script)
    {
        var result = slotAssignments.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (script is null)
        {
            result.Remove(slot);
            return result;
        }

        foreach (int assignedSlot in result.Keys.ToArray())
        {
            if (result[assignedSlot] == script)
                result[assignedSlot] = null;
        }

        result[slot] = script;
        return result;
    }

    public static IReadOnlyDictionary<int, UdbScriptInfo?> LoadSlotAssignments(
        IEnumerable<UdbScriptInfo> scripts,
        IReadOnlyDictionary<string, object?> settings,
        int slotCount = UdbScriptActions.ScriptSlotCount)
    {
        var scriptsByPath = scripts.ToDictionary(script => script.ScriptFile, StringComparer.Ordinal);
        var result = new Dictionary<int, UdbScriptInfo?>();
        for (int slot = 1; slot <= slotCount; slot++)
        {
            string key = SlotSettingKey(slot);
            if (!settings.TryGetValue(key, out object? value))
                continue;

            string path = value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(path))
                continue;

            if (scriptsByPath.TryGetValue(path, out UdbScriptInfo? script))
                result[slot] = script;
        }

        return result;
    }

    public static IReadOnlySet<string> LoadCollapsedDirectoryHashes(
        UdbScriptDirectory root,
        IReadOnlyDictionary<string, object?> settings)
    {
        var collapsed = new HashSet<string>(StringComparer.Ordinal);
        AddCollapsedDirectoryHashes(root, settings, collapsed);
        return collapsed;
    }

    public static IReadOnlyList<UdbScriptSettingOperation> SaveDirectoryExpansionOperations(
        UdbScriptDirectory root,
        IReadOnlySet<string> collapsedDirectoryHashes)
    {
        var operations = new List<UdbScriptSettingOperation>();
        AddDirectoryExpansionOperations(root, collapsedDirectoryHashes, operations);
        return operations;
    }

    public static IReadOnlyList<UdbScriptSettingOperation> SaveSlotAssignmentOperations(
        IReadOnlyDictionary<int, UdbScriptInfo?> slotAssignments)
    {
        var operations = new List<UdbScriptSettingOperation>();
        foreach (KeyValuePair<int, UdbScriptInfo?> pair in slotAssignments.OrderBy(pair => pair.Key))
        {
            if (pair.Value is null || string.IsNullOrWhiteSpace(pair.Value.ScriptFile))
                continue;

            operations.Add(new UdbScriptSettingOperation(
                UdbScriptSettingOperationKind.Write,
                SlotSettingKey(pair.Key),
                pair.Value.ScriptFile));
        }

        return operations;
    }

    public static int SlotForScript(
        UdbScriptInfo script,
        IReadOnlyDictionary<int, UdbScriptInfo?> slotAssignments)
    {
        foreach (KeyValuePair<int, UdbScriptInfo?> pair in slotAssignments)
        {
            if (pair.Value == script)
                return pair.Key;
        }

        return 0;
    }

    private static IReadOnlyList<UdbScriptDockerNode> BuildDirectoryChildren(
        UdbScriptDirectory directory,
        string filterText,
        IReadOnlyDictionary<int, UdbScriptInfo?> slotAssignments,
        IReadOnlyDictionary<int, string> hotkeys,
        IReadOnlySet<string>? collapsedDirectoryHashes)
    {
        var nodes = new List<UdbScriptDockerNode>();
        foreach (UdbScriptDirectory child in directory.Directories.OrderBy(child => child.Name, StringComparer.Ordinal))
        {
            nodes.Add(new UdbScriptDockerNode(
                UdbScriptDockerNodeKind.Folder,
                child.Name,
                FolderImageKey,
                child.Path,
                child.Hash,
                collapsedDirectoryHashes is null || !collapsedDirectoryHashes.Contains(child.Hash),
                null,
                BuildDirectoryChildren(child, filterText, slotAssignments, hotkeys, collapsedDirectoryHashes)));
        }

        foreach (UdbScriptInfo script in directory.Scripts.OrderBy(script => script.Name, StringComparer.Ordinal))
        {
            if (!MatchesFilter(script, filterText))
                continue;

            int slot = SlotForScript(script, slotAssignments);
            nodes.Add(new UdbScriptDockerNode(
                UdbScriptDockerNodeKind.Script,
                ScriptNodeText(script, slot, HotkeyText(slot, hotkeys)),
                ScriptImageKey,
                script.ScriptFile,
                script.PathHash,
                false,
                script,
                Array.Empty<UdbScriptDockerNode>()));
        }

        return nodes;
    }

    private static void AddDirectoryExpansionOperations(
        UdbScriptDirectory directory,
        IReadOnlySet<string> collapsedDirectoryHashes,
        List<UdbScriptSettingOperation> operations)
    {
        string key = DirectoryExpandSettingKey(directory.Hash);
        if (collapsedDirectoryHashes.Contains(directory.Hash))
        {
            operations.Add(new UdbScriptSettingOperation(UdbScriptSettingOperationKind.Write, key, false));
        }
        else
        {
            operations.Add(new UdbScriptSettingOperation(UdbScriptSettingOperationKind.Delete, key));
        }

        foreach (UdbScriptDirectory child in directory.Directories)
            AddDirectoryExpansionOperations(child, collapsedDirectoryHashes, operations);
    }

    private static void AddCollapsedDirectoryHashes(
        UdbScriptDirectory directory,
        IReadOnlyDictionary<string, object?> settings,
        HashSet<string> collapsed)
    {
        if (settings.TryGetValue(DirectoryExpandSettingKey(directory.Hash), out object? value)
            && IsExplicitlyCollapsed(value))
        {
            collapsed.Add(directory.Hash);
        }

        foreach (UdbScriptDirectory child in directory.Directories)
            AddCollapsedDirectoryHashes(child, settings, collapsed);
    }

    private static bool IsExplicitlyCollapsed(object? value)
        => value is bool boolValue
            ? !boolValue
            : string.Equals(value?.ToString(), "false", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesFilter(UdbScriptInfo script, string filterText)
        => string.IsNullOrWhiteSpace(filterText)
            || script.Name.ToLowerInvariant().Contains(filterText)
            || script.Description.ToLowerInvariant().Contains(filterText);

    private static string ScriptNodeText(UdbScriptInfo script, int slot, string hotkey)
        => slot == 0 ? script.Name : script.Name + " [" + hotkey + "]";

    private static string SlotMenuText(int slot, string name, string hotkey)
        => "Slot " + slot + ": " + name + " [" + hotkey + "]";

    private static string SlotSettingKey(int slot)
        => ScriptSlotSettingPrefix + slot;

    private static string DirectoryExpandSettingKey(string hash)
        => "directoryexpand." + hash;

    private static string HotkeyText(int slot, IReadOnlyDictionary<int, string> hotkeys)
        => slot != 0 && hotkeys.TryGetValue(slot, out string? hotkey) && !string.IsNullOrWhiteSpace(hotkey)
            ? hotkey
            : NoHotkeyText;

    private static string SettingChangeText(int count)
        => count == 1 ? "1 setting change" : $"{count} setting changes";

    private static UdbScriptDockerMenuItem MenuCommand(string text)
        => new(UdbScriptDockerMenuItemKind.Command, text, 0, Array.Empty<UdbScriptDockerMenuItem>());
}
