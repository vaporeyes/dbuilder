// ABOUTME: Non-modal USDF Dialog Editor window for parsed DIALOGUE map lumps.
// ABOUTME: Presents UDB-style conversation tree nodes and detail rows without save-back editing.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class UsdfConversationWindow : Window
{
    private readonly TreeView _tree = new();
    private readonly ListBox _list = new();

    public UsdfConversationWindow(UsdfParseResult result)
        : this(result, UsdfDialogEditorModel.DefaultWindowState, applyPosition: false)
    {
    }

    public UsdfConversationWindow(UsdfParseResult result, UsdfDialogEditorWindowState windowState)
        : this(result, windowState, applyPosition: true)
    {
    }

    private UsdfConversationWindow(UsdfParseResult result, UsdfDialogEditorWindowState windowState, bool applyPosition)
    {
        Title = UsdfDialogEditorModel.MainFormTitle;
        Width = Math.Max(1, windowState.SizeWidth);
        Height = Math.Max(1, windowState.SizeHeight);
        WindowState = (Avalonia.Controls.WindowState)windowState.WindowState;
        if (applyPosition)
            Position = new PixelPoint(windowState.PositionX, windowState.PositionY);
        else
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel { Margin = new Thickness(10) };
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock { Text = UsdfDialogueParser.ViewerStatus(result), FontWeight = FontWeight.Bold });
        if (result.Success)
            header.Children.Add(new TextBlock { Text = UsdfDialogueParser.ViewerSummary(result.Document) });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        foreach (TreeViewItem item in TreeItems(UsdfDialogEditorModel.BuildTree(result)))
            _tree.Items.Add(item);

        foreach (UsdfConversationRow row in UsdfDialogueParser.ViewerRows(result))
            _list.Items.Add(Row(row));

        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{UsdfDialogEditorModel.TreeWidth},*"),
        };
        var treeScroll = new ScrollViewer { Content = _tree };
        var listScroll = new ScrollViewer { Content = _list };
        Grid.SetColumn(treeScroll, 0);
        Grid.SetColumn(listScroll, 1);
        content.Children.Add(treeScroll);
        content.Children.Add(listScroll);
        root.Children.Add(content);
        Content = root;
    }

    private static IReadOnlyList<TreeViewItem> TreeItems(IReadOnlyList<UsdfDialogEditorTreeNode> nodes)
    {
        var roots = new List<TreeViewItem>();
        var parents = new List<TreeViewItem>();
        foreach (UsdfDialogEditorTreeNode node in nodes)
        {
            var item = new TreeViewItem
            {
                Header = node.Text,
                Tag = node.ImageKey,
                IsExpanded = true,
            };

            if (node.Depth == 0 || node.Depth - 1 >= parents.Count)
                roots.Add(item);
            else
                parents[node.Depth - 1].Items.Add(item);

            if (parents.Count <= node.Depth) parents.Add(item);
            else parents[node.Depth] = item;
            if (parents.Count > node.Depth + 1) parents.RemoveRange(node.Depth + 1, parents.Count - node.Depth - 1);
        }

        return roots;
    }

    private static Control Row(UsdfConversationRow row)
        => new TextBlock
        {
            Text = new string(' ', row.Depth * 2) + row.Text,
            Foreground = BrushFor(row.Kind),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2),
        };

    private static IBrush BrushFor(UsdfConversationRowKind kind)
        => kind switch
        {
            UsdfConversationRowKind.Include => Brushes.LightSkyBlue,
            UsdfConversationRowKind.Conversation => Brushes.White,
            UsdfConversationRowKind.Page => Brushes.Gainsboro,
            _ => Brushes.LightGray,
        };
}
