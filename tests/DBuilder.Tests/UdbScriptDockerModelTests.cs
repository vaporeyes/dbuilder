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
        Assert.Equal("Reset", UdbScriptDockerModel.ResetButtonText);
        Assert.Equal("Run", UdbScriptDockerModel.RunButtonText);
        Assert.Equal("Edit", UdbScriptDockerModel.EditMenuText);
        Assert.Equal("Set slot", UdbScriptDockerModel.SetSlotMenuText);
        Assert.Equal("Clear slot", UdbScriptDockerModel.ClearSlotMenuText);
        Assert.Equal("Open in Explorer", UdbScriptDockerModel.OpenInExplorerMenuText);
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

    private static UdbScriptInfo Script(string name, string description, string file, params UdbScriptOption[] options)
        => new(name, description, 1, file, UdbScriptDiscovery.HashPath(file), null, options);
}
