// ABOUTME: Tests UDBScript docker tree and slot menu metadata against upstream behavior.
// ABOUTME: Covers sorting, filtering, labels, selection, and slot assignment state.

using DBuilder.IO;

namespace DBuilder.Tests;

public class UdbScriptDockerModelTests
{
    [Fact]
    public void DockerLabelsMatchUdbControl()
    {
        Assert.Equal("udbscript", UdbScriptDockerModel.DockerKey);
        Assert.Equal("Scripts", UdbScriptDockerModel.DockerTitle);
        Assert.Equal("Folder", UdbScriptDockerModel.FolderImageKey);
        Assert.Equal("Script", UdbScriptDockerModel.ScriptImageKey);
        Assert.Equal("Filter:", UdbScriptDockerModel.FilterLabel);
        Assert.Equal("Script description", UdbScriptDockerModel.DescriptionLabel);
        Assert.Equal("Script options", UdbScriptDockerModel.OptionsLabel);
        Assert.Equal("Options", UdbScriptDockerModel.OptionsButtonText);
        Assert.Equal("Reset", UdbScriptDockerModel.ResetButtonText);
        Assert.Equal("Run", UdbScriptDockerModel.RunButtonText);
        Assert.Equal("Edit", UdbScriptDockerModel.EditMenuText);
        Assert.Equal("Set slot", UdbScriptDockerModel.SetSlotMenuText);
        Assert.Equal("Clear slot", UdbScriptDockerModel.ClearSlotMenuText);
        Assert.Equal("Open in Explorer", UdbScriptDockerModel.OpenInExplorerMenuText);
        Assert.Equal("scriptslots.slot", UdbScriptDockerModel.ScriptSlotSettingPrefix);
    }

    [Fact]
    public void DockerLayoutMetadataMatchesUdbControl()
    {
        UdbScriptDockerLayoutMetadata metadata = UdbScriptDockerModel.LayoutMetadata();

        Assert.Equal("Horizontal", metadata.SplitOrientation);
        Assert.Equal("SingleSelect", metadata.TreeSelectionMode);
        Assert.False(metadata.TreeHideSelection);
        Assert.True(metadata.TreeShowNodeToolTips);
        Assert.True(metadata.DescriptionReadOnly);
        Assert.Equal("Both", metadata.DescriptionScrollBars);
        Assert.Equal(2, metadata.ActionButtonColumns);
    }

    [Fact]
    public void DockerContextMenuMetadataMatchesUdbControl()
    {
        IReadOnlyList<UdbScriptDockerMenuItem> fileMenu = UdbScriptDockerModel.FileContextMenuItems(slotCount: 3);

        Assert.Equal(2, fileMenu.Count);
        Assert.Equal(UdbScriptDockerMenuItemKind.Command, fileMenu[0].Kind);
        Assert.Equal("Edit", fileMenu[0].Text);
        Assert.Equal(UdbScriptDockerMenuItemKind.Submenu, fileMenu[1].Kind);
        Assert.Equal("Set slot", fileMenu[1].Text);

        IReadOnlyList<UdbScriptDockerMenuItem> slotItems = fileMenu[1].Children;
        Assert.Equal(5, slotItems.Count);
        Assert.Equal(UdbScriptDockerMenuItemKind.Command, slotItems[0].Kind);
        Assert.Equal("Clear slot", slotItems[0].Text);
        Assert.Equal(UdbScriptDockerMenuItemKind.Separator, slotItems[1].Kind);
        Assert.Equal("", slotItems[1].Text);
        Assert.Equal(new[] { "Slot 1", "Slot 2", "Slot 3" }, slotItems.Skip(2).Select(item => item.Text).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, slotItems.Skip(2).Select(item => item.Slot).ToArray());
        Assert.All(slotItems.Skip(2), item => Assert.Equal(UdbScriptDockerMenuItemKind.Slot, item.Kind));
        Assert.Equal(UdbScriptActions.ScriptSlotCount + 2, UdbScriptDockerModel.FileContextMenuItems()[1].Children.Count);

        UdbScriptDockerMenuItem folderItem = Assert.Single(UdbScriptDockerModel.FolderContextMenuItems());
        Assert.Equal(UdbScriptDockerMenuItemKind.Command, folderItem.Kind);
        Assert.Equal("Open in Explorer", folderItem.Text);
    }

    [Fact]
    public void BuildTreeSortsFoldersAndScriptsAndAppliesFilter()
    {
        UdbScriptInfo alpha = Script("Alpha", "Contains doors", "/scripts/alpha.js");
        UdbScriptInfo beta = Script("Beta", "Contains lifts", "/scripts/beta.js");
        UdbScriptInfo nested = Script("Nested", "Secret doors", "/scripts/a/nested.js");
        var root = new UdbScriptDirectory(
            "/scripts",
            "Scripts",
            "root",
            new[]
            {
                new UdbScriptDirectory("/scripts/z", "Zeta", "z", Array.Empty<UdbScriptDirectory>(), Array.Empty<UdbScriptInfo>()),
                new UdbScriptDirectory("/scripts/a", "AlphaFolder", "a", Array.Empty<UdbScriptDirectory>(), new[] { nested }),
            },
            new[] { beta, alpha });

        IReadOnlyList<UdbScriptDockerNode> nodes = UdbScriptDockerModel.BuildTree(
            root,
            "door",
            new Dictionary<int, UdbScriptInfo?> { [2] = alpha },
            new Dictionary<int, string> { [2] = "Ctrl+2" },
            new HashSet<string> { "z" });

        Assert.Equal(new[] { "AlphaFolder", "Zeta", "Alpha [Ctrl+2]" }, nodes.Select(node => node.Text).ToArray());
        Assert.Equal(UdbScriptDockerNodeKind.Folder, nodes[0].Kind);
        Assert.True(nodes[0].Expanded);
        Assert.Equal("Nested", Assert.Single(nodes[0].Children).Text);
        Assert.False(nodes[1].Expanded);
        Assert.Equal(UdbScriptDockerNodeKind.Script, nodes[2].Kind);
        Assert.Equal(UdbScriptDockerModel.ScriptImageKey, nodes[2].ImageKey);
    }

    [Fact]
    public void DirectoryExpansionStateTracksCollapseAndExpand()
    {
        var child = new UdbScriptDirectory(
            "/scripts/a",
            "AlphaFolder",
            "hash-a",
            Array.Empty<UdbScriptDirectory>(),
            Array.Empty<UdbScriptInfo>());
        var root = new UdbScriptDirectory(
            "/scripts",
            "Scripts",
            "root",
            new[] { child },
            Array.Empty<UdbScriptInfo>());

        IReadOnlySet<string> collapsed = UdbScriptDockerModel.CollapseDirectory(new HashSet<string>(), child);
        IReadOnlyList<UdbScriptDockerNode> collapsedNodes = UdbScriptDockerModel.BuildTree(
            root,
            "",
            new Dictionary<int, UdbScriptInfo?>(),
            new Dictionary<int, string>(),
            collapsed);

        Assert.False(Assert.Single(collapsedNodes).Expanded);

        IReadOnlySet<string> expanded = UdbScriptDockerModel.ExpandDirectory(collapsed, child);
        IReadOnlyList<UdbScriptDockerNode> expandedNodes = UdbScriptDockerModel.BuildTree(
            root,
            "",
            new Dictionary<int, UdbScriptInfo?>(),
            new Dictionary<int, string>(),
            expanded);

        Assert.True(Assert.Single(expandedNodes).Expanded);
    }

    [Fact]
    public void DirectoryExpansionStateCanTrackByHash()
    {
        IReadOnlySet<string> collapsed = UdbScriptDockerModel.SetDirectoryCollapsed(
            new HashSet<string>(),
            "hash-child",
            collapsed: true);
        IReadOnlySet<string> expanded = UdbScriptDockerModel.SetDirectoryCollapsed(
            collapsed,
            "hash-child",
            collapsed: false);

        Assert.Contains("hash-child", collapsed);
        Assert.DoesNotContain("hash-child", expanded);
    }

    [Fact]
    public void SaveDirectoryExpansionOperationsMatchUdbRecursiveSettings()
    {
        var collapsedChild = new UdbScriptDirectory(
            "/scripts/a/collapsed",
            "Collapsed",
            "hash-collapsed",
            Array.Empty<UdbScriptDirectory>(),
            Array.Empty<UdbScriptInfo>());
        var expandedChild = new UdbScriptDirectory(
            "/scripts/a/expanded",
            "Expanded",
            "hash-expanded",
            Array.Empty<UdbScriptDirectory>(),
            Array.Empty<UdbScriptInfo>());
        var root = new UdbScriptDirectory(
            "/scripts",
            "Scripts",
            "hash-root",
            new[] { collapsedChild, expandedChild },
            Array.Empty<UdbScriptInfo>());

        IReadOnlyList<UdbScriptSettingOperation> operations = UdbScriptDockerModel.SaveDirectoryExpansionOperations(
            root,
            new HashSet<string> { collapsedChild.Hash });

        Assert.Equal(3, operations.Count);
        Assert.Equal(UdbScriptSettingOperationKind.Delete, operations[0].Kind);
        Assert.Equal("directoryexpand.hash-root", operations[0].Key);
        Assert.Null(operations[0].Value);
        Assert.Equal(UdbScriptSettingOperationKind.Write, operations[1].Kind);
        Assert.Equal("directoryexpand.hash-collapsed", operations[1].Key);
        Assert.Equal(false, operations[1].Value);
        Assert.Equal(UdbScriptSettingOperationKind.Delete, operations[2].Kind);
        Assert.Equal("directoryexpand.hash-expanded", operations[2].Key);
        Assert.Null(operations[2].Value);
    }

    [Fact]
    public void LoadCollapsedDirectoryHashesMatchesUdbDefaultExpandedSettings()
    {
        var nested = new UdbScriptDirectory(
            "/scripts/a/nested",
            "Nested",
            "hash-nested",
            Array.Empty<UdbScriptDirectory>(),
            Array.Empty<UdbScriptInfo>());
        var child = new UdbScriptDirectory(
            "/scripts/a",
            "AlphaFolder",
            "hash-child",
            new[] { nested },
            Array.Empty<UdbScriptInfo>());
        var root = new UdbScriptDirectory(
            "/scripts",
            "Scripts",
            "hash-root",
            new[] { child },
            Array.Empty<UdbScriptInfo>());

        IReadOnlySet<string> collapsed = UdbScriptDockerModel.LoadCollapsedDirectoryHashes(
            root,
            new Dictionary<string, object?>
            {
                ["directoryexpand.hash-root"] = true,
                ["directoryexpand.hash-child"] = false,
                ["directoryexpand.hash-nested"] = "false",
                ["directoryexpand.missing"] = false,
            });

        Assert.Equal(new[] { "hash-child", "hash-nested" }, collapsed.OrderBy(hash => hash, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void FindScriptNodeRecursivelyMatchesScriptFile()
    {
        UdbScriptInfo nested = Script("Nested", "Secret doors", "/scripts/a/nested.js");
        var root = new UdbScriptDirectory(
            "/scripts",
            "Scripts",
            "root",
            new[]
            {
                new UdbScriptDirectory("/scripts/a", "AlphaFolder", "a", Array.Empty<UdbScriptDirectory>(), new[] { nested }),
            },
            Array.Empty<UdbScriptInfo>());
        IReadOnlyList<UdbScriptDockerNode> nodes = UdbScriptDockerModel.BuildTree(
            root,
            "",
            new Dictionary<int, UdbScriptInfo?>(),
            new Dictionary<int, string>());

        UdbScriptDockerNode? found = UdbScriptDockerModel.FindScriptNode(nodes, nested.ScriptFile);

        Assert.NotNull(found);
        Assert.Equal(nested, found.Script);
        Assert.Null(UdbScriptDockerModel.FindScriptNode(nodes, "/scripts/missing.js"));
    }

    [Fact]
    public void ReplaceScriptUpdatesMatchingScriptInDirectoryTree()
    {
        var originalOption = new UdbScriptOption(
            "length",
            "Length",
            (int)UniversalType.Integer,
            128,
            128,
            Array.Empty<UdbScriptEnumValue>(),
            "scripts.hash.options.length");
        UdbScriptInfo alpha = Script("Alpha", "A", "/scripts/a/alpha.js", originalOption);
        UdbScriptInfo beta = Script("Beta", "B", "/scripts/beta.js");
        var root = new UdbScriptDirectory(
            "/scripts",
            "Scripts",
            "root",
            new[]
            {
                new UdbScriptDirectory("/scripts/a", "AlphaFolder", "a", Array.Empty<UdbScriptDirectory>(), new[] { alpha }),
            },
            new[] { beta });
        UdbScriptInfo edited = alpha with { Options = new[] { originalOption with { Value = 256 } } };

        UdbScriptDirectory replaced = UdbScriptDockerModel.ReplaceScript(root, edited);

        UdbScriptInfo replacedAlpha = Assert.Single(Assert.Single(replaced.Directories).Scripts);
        Assert.Equal(256, replacedAlpha.Options[0].Value);
        Assert.Equal(beta, Assert.Single(replaced.Scripts));
    }

    [Fact]
    public void SlotMenuItemsShowAssignedScriptsAndHotkeys()
    {
        UdbScriptInfo alpha = Script("Alpha", "A", "/scripts/alpha.js");

        IReadOnlyList<UdbScriptSlotMenuItem> items = UdbScriptDockerModel.SlotMenuItems(
            new Dictionary<int, UdbScriptInfo?> { [1] = alpha },
            new Dictionary<int, string> { [1] = "Ctrl+1" },
            slotCount: 3);

        Assert.Equal("Slot 1: Alpha [Ctrl+1]", items[0].Text);
        Assert.Equal("Slot 2: not assigned [no hotkey]", items[1].Text);
        Assert.Equal("Slot 3: not assigned [no hotkey]", items[2].Text);
    }

    [Fact]
    public void AssignSlotMovesExistingScriptAndCanClearSlot()
    {
        UdbScriptInfo alpha = Script("Alpha", "A", "/scripts/alpha.js");
        UdbScriptInfo beta = Script("Beta", "B", "/scripts/beta.js");

        IReadOnlyDictionary<int, UdbScriptInfo?> moved = UdbScriptDockerModel.AssignSlot(
            new Dictionary<int, UdbScriptInfo?> { [1] = alpha, [2] = beta },
            3,
            alpha);

        Assert.Null(moved[1]);
        Assert.Equal(beta, moved[2]);
        Assert.Equal(alpha, moved[3]);
        Assert.Equal(3, UdbScriptDockerModel.SlotForScript(alpha, moved));

        IReadOnlyDictionary<int, UdbScriptInfo?> cleared = UdbScriptDockerModel.AssignSlot(moved, 3, null);

        Assert.False(cleared.ContainsKey(3));
    }

    [Fact]
    public void LoadSlotAssignmentsMatchesUdbSettingsByScriptPath()
    {
        UdbScriptInfo alpha = Script("Alpha", "A", "/scripts/alpha.js");
        UdbScriptInfo beta = Script("Beta", "B", "/scripts/beta.js");

        IReadOnlyDictionary<int, UdbScriptInfo?> assignments = UdbScriptDockerModel.LoadSlotAssignments(
            new[] { alpha, beta },
            new Dictionary<string, object?>
            {
                ["scriptslots.slot1"] = alpha.ScriptFile,
                ["scriptslots.slot2"] = "   ",
                ["scriptslots.slot3"] = "/scripts/missing.js",
                ["scriptslots.slot4"] = beta.ScriptFile,
            },
            slotCount: 4);

        Assert.Equal(alpha, assignments[1]);
        Assert.False(assignments.ContainsKey(2));
        Assert.False(assignments.ContainsKey(3));
        Assert.Equal(beta, assignments[4]);
    }

    [Fact]
    public void SaveSlotAssignmentOperationsWriteOnlyAssignedScripts()
    {
        UdbScriptInfo alpha = Script("Alpha", "A", "/scripts/alpha.js");
        UdbScriptInfo blank = Script("Blank", "B", "   ");

        IReadOnlyList<UdbScriptSettingOperation> operations = UdbScriptDockerModel.SaveSlotAssignmentOperations(
            new Dictionary<int, UdbScriptInfo?> { [2] = alpha, [1] = null, [3] = blank });

        UdbScriptSettingOperation operation = Assert.Single(operations);
        Assert.Equal(UdbScriptSettingOperationKind.Write, operation.Kind);
        Assert.Equal("scriptslots.slot2", operation.Key);
        Assert.Equal(alpha.ScriptFile, operation.Value);
    }

    [Fact]
    public void SelectionReturnsScriptDescriptionAndOptions()
    {
        var option = new UdbScriptOption(
            "length",
            "Length",
            (int)UniversalType.Integer,
            128,
            128,
            Array.Empty<UdbScriptEnumValue>(),
            "settings.length");
        UdbScriptInfo alpha = Script("Alpha", "A description", "/scripts/alpha.js", option);
        var node = new UdbScriptDockerNode(
            UdbScriptDockerNodeKind.Script,
            "Alpha",
            UdbScriptDockerModel.ScriptImageKey,
            alpha.ScriptFile,
            alpha.PathHash,
            false,
            alpha,
            Array.Empty<UdbScriptDockerNode>());

        UdbScriptDockerSelection selection = UdbScriptDockerModel.Selection(node);

        Assert.Equal(alpha, selection.CurrentScript);
        Assert.Equal("A description", selection.Description);
        Assert.Equal(new[] { option }, selection.Options);
        Assert.Null(UdbScriptDockerModel.Selection(null).CurrentScript);
        Assert.Equal("", UdbScriptDockerModel.Selection(null).Description);
    }

    [Fact]
    public void ApplySelectionMatchesUdbScriptFolderAndEmptyTransitions()
    {
        var option = new UdbScriptOption(
            "length",
            "Length",
            (int)UniversalType.Integer,
            128,
            128,
            Array.Empty<UdbScriptEnumValue>(),
            "settings.length");
        UdbScriptInfo alpha = Script("Alpha", "A description", "/scripts/alpha.js", option);
        var scriptNode = new UdbScriptDockerNode(
            UdbScriptDockerNodeKind.Script,
            "Alpha",
            UdbScriptDockerModel.ScriptImageKey,
            alpha.ScriptFile,
            alpha.PathHash,
            false,
            alpha,
            Array.Empty<UdbScriptDockerNode>());
        var folderNode = new UdbScriptDockerNode(
            UdbScriptDockerNodeKind.Folder,
            "Folder",
            UdbScriptDockerModel.FolderImageKey,
            "/scripts/folder",
            "hash-folder",
            true,
            null,
            Array.Empty<UdbScriptDockerNode>());

        UdbScriptDockerSelection scriptSelection = UdbScriptDockerModel.ApplySelection(
            UdbScriptDockerModel.Selection(null),
            scriptNode);
        UdbScriptDockerSelection folderSelection = UdbScriptDockerModel.ApplySelection(scriptSelection, folderNode);
        UdbScriptDockerSelection emptySelection = UdbScriptDockerModel.ApplySelection(folderSelection, null);

        Assert.Equal(alpha, scriptSelection.CurrentScript);
        Assert.Equal("A description", scriptSelection.Description);
        Assert.Equal(new[] { option }, scriptSelection.Options);
        Assert.Equal(alpha, folderSelection.CurrentScript);
        Assert.Equal("A description", folderSelection.Description);
        Assert.Empty(folderSelection.Options);
        Assert.Null(emptySelection.CurrentScript);
        Assert.Equal("", emptySelection.Description);
        Assert.Empty(emptySelection.Options);
    }

    [Fact]
    public void ResetSelectedScriptOptionsRestoresDefaultsAndDeletesSettings()
    {
        var length = new UdbScriptOption(
            "length",
            "Length",
            (int)UniversalType.Integer,
            128,
            256,
            Array.Empty<UdbScriptEnumValue>(),
            "scripts.hash.options.length");
        var texture = new UdbScriptOption(
            "texture",
            "Texture",
            (int)UniversalType.Texture,
            "STARTAN3",
            "BROWN1",
            Array.Empty<UdbScriptEnumValue>(),
            "scripts.hash.options.texture");
        UdbScriptInfo script = Script("Alpha", "A description", "/scripts/alpha.js", length, texture);

        UdbScriptDockerResetOptionsResult result = UdbScriptDockerModel.ResetSelectedScriptOptions(script);
        UdbScriptDockerResetOptionsResult empty = UdbScriptDockerModel.ResetSelectedScriptOptions(null);

        Assert.NotNull(result.Script);
        Assert.Equal(new object[] { 128, "STARTAN3" }, result.Script.Options.Select(option => option.Value).ToArray());
        Assert.Equal(new[] { "scripts.hash.options.length", "scripts.hash.options.texture" }, result.Operations.Select(operation => operation.Key).ToArray());
        Assert.All(result.Operations, operation => Assert.Equal(UdbScriptSettingOperationKind.Delete, operation.Kind));
        Assert.Null(empty.Script);
        Assert.Empty(empty.Operations);
    }

    [Fact]
    public void ApplyEditedScriptOptionsReturnsUpdatedScriptAndSaveOperations()
    {
        var length = new UdbScriptOption(
            "length",
            "Length",
            (int)UniversalType.Integer,
            128,
            128,
            Array.Empty<UdbScriptEnumValue>(),
            "scripts.hash.options.length");
        var texture = new UdbScriptOption(
            "texture",
            "Texture",
            (int)UniversalType.Texture,
            "STARTAN3",
            "STARTAN3",
            Array.Empty<UdbScriptEnumValue>(),
            "scripts.hash.options.texture");
        UdbScriptInfo script = Script("Alpha", "A description", "/scripts/alpha.js", length, texture);

        UdbScriptDockerApplyOptionsResult changed = UdbScriptDockerModel.ApplyEditedScriptOptions(
            script,
            new[] { length with { Value = 256 }, texture });
        UdbScriptDockerApplyOptionsResult defaults = UdbScriptDockerModel.ApplyEditedScriptOptions(script, new[] { length, texture });
        UdbScriptDockerApplyOptionsResult empty = UdbScriptDockerModel.ApplyEditedScriptOptions(null, new[] { length });

        Assert.NotNull(changed.Script);
        Assert.Equal(new object[] { 256, "STARTAN3" }, changed.Script.Options.Select(option => option.Value).ToArray());
        Assert.Equal(new[] { UdbScriptSettingOperationKind.Write, UdbScriptSettingOperationKind.Delete }, changed.Operations.Select(operation => operation.Kind).ToArray());
        Assert.Equal(256, changed.Operations[0].Value);

        Assert.Equal(new[]
        {
            "scripts.hash.options.length",
            "scripts.hash.options.texture",
            "scripts." + script.PathHash + ".options",
            "scripts." + script.PathHash,
        }, defaults.Operations.Select(operation => operation.Key).ToArray());
        Assert.All(defaults.Operations, operation => Assert.Equal(UdbScriptSettingOperationKind.Delete, operation.Kind));
        Assert.Null(empty.Script);
        Assert.Empty(empty.Operations);
    }

    [Theory]
    [InlineData(1, "1 setting change")]
    [InlineData(2, "2 setting changes")]
    public void StatusTextFormatsSingularAndPluralSettingChangeCounts(int settingChangeCount, string countText)
    {
        UdbScriptInfo script = Script("Alpha", "A", "/scripts/alpha.js");

        Assert.Equal(
            $"UDBScript assigned to slot 3: Alpha ({countText})",
            UdbScriptDockerModel.AssignedSlotStatusText(script, 3, settingChangeCount));
        Assert.Equal(
            $"UDBScript cleared from slot 3: Alpha ({countText})",
            UdbScriptDockerModel.ClearedSlotStatusText(script, 3, settingChangeCount));
        Assert.Equal(
            $"UDBScript options edited: Alpha ({countText})",
            UdbScriptDockerModel.OptionsEditedStatusText(script, settingChangeCount));
        Assert.Equal(
            $"UDBScript reset options requested: Alpha ({countText})",
            UdbScriptDockerModel.OptionsResetStatusText(script, settingChangeCount));
    }

    private static UdbScriptInfo Script(string name, string description, string file, params UdbScriptOption[] options)
        => new(name, description, 1, file, UdbScriptDiscovery.HashPath(file), null, options);
}
