// ABOUTME: Non-modal NodesViewer surface for classic Doom NODES, SEGS, SSECTORS, and VERTEXES data.
// ABOUTME: Presents parsed BSP structures from NodesReader without owning map rendering or overlay state.

using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class NodesViewerWindow : Window
{
    private readonly ListBox _nodes = new();
    private readonly ListBox _segs = new();
    private readonly ListBox _subsectors = new();
    private readonly ListBox _vertices = new();

    public NodesViewerWindow(ClassicNodesStructure structure)
    {
        Title = "Nodes Viewer";
        Width = 760;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel { Margin = new Avalonia.Thickness(10) };
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock { Text = NodesViewerModel.StatusText(structure), FontWeight = Avalonia.Media.FontWeight.Bold });
        if (structure.IsValid)
            header.Children.Add(new TextBlock { Text = NodesViewerModel.CountsText(structure) });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var tabs = new TabControl();
        FillRows(structure, tabs);
        root.Children.Add(tabs);

        Content = root;
    }

    private static TabItem Tab(string header, ListBox list)
        => new() { Header = header, Content = new ScrollViewer { Content = list } };

    private void FillRows(ClassicNodesStructure structure, TabControl tabs)
    {
        if (!structure.IsValid) return;

        ListBox[] lists = [_nodes, _segs, _subsectors, _vertices];
        IReadOnlyList<NodesViewerTabRows> tabRows = NodesViewerModel.TabRows(structure);
        for (int i = 0; i < tabRows.Count && i < lists.Length; i++)
        {
            foreach (string row in tabRows[i].Rows)
                lists[i].Items.Add(row);
            tabs.Items.Add(Tab(tabRows[i].Header, lists[i]));
        }
    }
}
