// ABOUTME: Modal categorized picker for thing types / linedef actions / sector effects with live text filtering.
// ABOUTME: Categories are collapsible tree nodes; double-clicking a leaf (or OK on a selected leaf) returns its number.

using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class BrowserDialog : Window
{
    private readonly TreeView _tree = new();
    private readonly TextBox _search = new() { Watermark = "Filter by name or number..." };
    private readonly List<BrowseEntry> _all;
    private readonly int _current;

    public int? SelectedNumber { get; private set; }
    public string SelectedTitle { get; private set; } = "";

    public BrowserDialog(string title, List<BrowseEntry> entries, int current)
    {
        Title = title;
        Width = 460;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _all = entries;
        _current = current;

        _search.TextChanged += (_, _) => Rebuild();

        var ok = new Button { Content = "OK", MinWidth = 72, IsDefault = true };
        ok.Click += (_, _) => { if (CaptureSelection()) Close(true); };
        var cancel = new Button { Content = "Cancel", MinWidth = 72, IsCancel = true };
        cancel.Click += (_, _) => Close(false);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 6, 0, 0),
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        _tree.DoubleTapped += (_, _) => { if (CaptureSelection()) Close(true); };

        var root = new DockPanel { Margin = new Avalonia.Thickness(10) };
        DockPanel.SetDock(_search, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(_search);
        root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer { Content = _tree, Margin = new Avalonia.Thickness(0, 6) });
        Content = root;

        Rebuild();
    }

    // Rebuilds the category tree from the current filter, expanding groups and pre-selecting the current entry.
    private void Rebuild()
    {
        var filtered = CatalogBrowse.Filter(_all, _search.Text);
        var groups = CatalogBrowse.Grouped(filtered);
        bool searching = !string.IsNullOrWhiteSpace(_search.Text);

        var nodes = new List<TreeViewItem>();
        foreach (var (category, entries) in groups)
        {
            var node = new TreeViewItem { Header = $"{category} ({entries.Count})", IsExpanded = searching };
            var leaves = new List<TreeViewItem>();
            foreach (var e in entries)
            {
                var leaf = new TreeViewItem { Header = $"{e.Number} - {e.Title}", Tag = e };
                if (e.Number == _current && !searching) { node.IsExpanded = true; leaf.IsSelected = true; }
                leaves.Add(leaf);
            }
            node.ItemsSource = leaves;
            nodes.Add(node);
        }
        _tree.ItemsSource = nodes;
    }

    private bool CaptureSelection()
    {
        if (_tree.SelectedItem is TreeViewItem { Tag: BrowseEntry e })
        {
            SelectedNumber = e.Number;
            SelectedTitle = e.Title;
            return true;
        }
        return false;
    }
}
