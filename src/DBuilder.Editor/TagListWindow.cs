// ABOUTME: Non-modal panel listing every tag in use with its element count; selecting a tag asks the host to select it.
// ABOUTME: A lightweight TagExplorer - the host runs the find/reveal so this window stays presentation-only.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class TagListWindow : Window
{
    private readonly ListBox _list = new();

    /// <summary>Raised with a tag number when the user selects a row.</summary>
    public event Action<int>? TagActivated;

    public TagListWindow(IReadOnlyList<(int Tag, int Count)> tags)
    {
        Title = "Tags";
        Width = 280;
        Height = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var header = new TextBlock
        {
            Text = tags.Count == 0 ? "No tags in use." : $"{tags.Count} tag(s). Click to select its elements.",
            Foreground = Brushes.LightSkyBlue, Margin = new Avalonia.Thickness(10, 8), TextWrapping = TextWrapping.Wrap,
        };

        var rows = new List<ListBoxItem>();
        foreach (var (tag, count) in tags)
            rows.Add(new ListBoxItem { Content = $"Tag {tag}  ({count} element{(count == 1 ? "" : "s")})", Tag = tag });
        _list.ItemsSource = rows;
        _list.SelectionChanged += (_, _) => { if (_list.SelectedItem is ListBoxItem { Tag: int t }) TagActivated?.Invoke(t); };

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(new ScrollViewer { Content = _list });
        Content = root;
    }
}
