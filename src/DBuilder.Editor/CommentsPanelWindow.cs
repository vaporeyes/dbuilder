// ABOUTME: Non-modal comments panel listing grouped UDMF comments from map elements.
// ABOUTME: Lets the host select, remove, and assign comments while keeping map mutation outside the window.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class CommentsPanelWindow : Window
{
    private readonly ComboBox _filter = new();
    private readonly TextBox _comment = new();
    private readonly ListBox _list = new();
    private readonly TextBlock _header = new();

    public event Action<CommentsPanelMode>? FilterChanged;
    public event Action<CommentGroup>? GroupActivated;
    public event Action<CommentGroup>? RemoveRequested;
    public event Action<string>? SetSelectedCommentRequested;

    public CommentsPanelMode FilterMode { get; private set; } = CommentsPanelMode.All;

    public CommentsPanelWindow(IReadOnlyList<CommentGroup> groups)
    {
        Title = "Comments";
        Width = 420;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _filter.ItemsSource = new[]
        {
            CommentsPanelMode.All,
            CommentsPanelMode.Vertices,
            CommentsPanelMode.Linedefs,
            CommentsPanelMode.Sectors,
            CommentsPanelMode.Things,
        };
        _filter.SelectedItem = FilterMode;
        _filter.SelectionChanged += (_, _) =>
        {
            if (_filter.SelectedItem is not CommentsPanelMode mode || mode == FilterMode) return;
            FilterMode = mode;
            FilterChanged?.Invoke(mode);
        };

        _comment.Watermark = "Comment for current selection";
        var setButton = new Button { Content = "Set Selection" };
        setButton.Click += (_, _) => SetSelectedCommentRequested?.Invoke(_comment.Text ?? "");

        var entryRow = new DockPanel { Margin = new Avalonia.Thickness(8, 0, 8, 8), LastChildFill = true };
        DockPanel.SetDock(setButton, Dock.Right);
        entryRow.Children.Add(setButton);
        entryRow.Children.Add(_comment);

        var filterRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Avalonia.Thickness(8, 8, 8, 6),
            Children =
            {
                new TextBlock { Text = "Show", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                _filter,
            },
        };

        _header.Margin = new Avalonia.Thickness(8, 0, 8, 6);
        _header.Foreground = Brushes.LightSkyBlue;
        _header.TextWrapping = TextWrapping.Wrap;
        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedItem is ListBoxItem { Tag: CommentGroup group }) GroupActivated?.Invoke(group);
        };

        var removeButton = new Button
        {
            Content = "Remove Selected Comment",
            Margin = new Avalonia.Thickness(8, 8),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        };
        removeButton.Click += (_, _) =>
        {
            if (_list.SelectedItem is ListBoxItem { Tag: CommentGroup group }) RemoveRequested?.Invoke(group);
        };

        var root = new DockPanel();
        DockPanel.SetDock(filterRow, Dock.Top);
        DockPanel.SetDock(entryRow, Dock.Top);
        DockPanel.SetDock(_header, Dock.Top);
        DockPanel.SetDock(removeButton, Dock.Bottom);
        root.Children.Add(filterRow);
        root.Children.Add(entryRow);
        root.Children.Add(_header);
        root.Children.Add(removeButton);
        root.Children.Add(new ScrollViewer { Content = _list });
        Content = root;

        SetGroups(groups);
    }

    public void SetGroups(IReadOnlyList<CommentGroup> groups)
    {
        _header.Text = groups.Count == 0
            ? "No comments found."
            : $"{groups.Count} comment group(s). Click a row to select and reveal it.";

        var rows = new List<ListBoxItem>();
        foreach (CommentGroup group in groups)
        {
            rows.Add(new ListBoxItem
            {
                Content = new TextBlock
                {
                    Text = FormatGroup(group),
                    TextWrapping = TextWrapping.Wrap,
                },
                Tag = group,
            });
        }
        _list.ItemsSource = rows;
    }

    private static string FormatGroup(CommentGroup group)
        => $"{group.Group}: {group.Comment} ({group.Elements.Count} element{(group.Elements.Count == 1 ? "" : "s")})";
}
