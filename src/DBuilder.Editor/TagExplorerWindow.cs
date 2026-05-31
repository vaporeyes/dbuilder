// ABOUTME: Non-modal TagExplorer surface for tags, actions, comments, and polyobject entries.
// ABOUTME: Presents the UDB-style TagExplorer model while leaving map selection to the owner window.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class TagExplorerWindow : Window
{
    private readonly ComboBox _displayMode = new();
    private readonly ComboBox _sortMode = new();
    private readonly TextBox _search = new();
    private readonly CheckBox _commentsOnly = new();
    private readonly TextBlock _header = new();
    private readonly ListBox _list = new();

    public event Action? OptionsChanged;
    public event Action<TagExplorerEntry>? EntryActivated;

    public TagExplorerOptions Options => new(
        DisplayMode: _displayMode.SelectedItem is TagExplorerDisplayMode display ? display : TagExplorerDisplayMode.TagsAndActions,
        SortMode: _sortMode.SelectedItem is TagExplorerSortMode sort ? sort : TagExplorerSortMode.ByIndex,
        SearchText: _search.Text ?? "",
        CommentsOnly: _commentsOnly.IsChecked == true);

    public TagExplorerWindow(IReadOnlyList<TagExplorerEntry> entries)
    {
        Title = "Tag Explorer";
        Width = 520;
        Height = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _displayMode.ItemsSource = new[]
        {
            TagExplorerDisplayMode.TagsAndActions,
            TagExplorerDisplayMode.Tags,
            TagExplorerDisplayMode.Actions,
            TagExplorerDisplayMode.Polyobjects,
        };
        _displayMode.SelectedItem = TagExplorerDisplayMode.TagsAndActions;
        _displayMode.SelectionChanged += (_, _) => OptionsChanged?.Invoke();

        _sortMode.ItemsSource = new[]
        {
            TagExplorerSortMode.ByIndex,
            TagExplorerSortMode.ByTag,
            TagExplorerSortMode.ByAction,
            TagExplorerSortMode.ByPolyobjectNumber,
        };
        _sortMode.SelectedItem = TagExplorerSortMode.ByIndex;
        _sortMode.SelectionChanged += (_, _) => OptionsChanged?.Invoke();

        _search.Watermark = "Search, #tag, $action, ^polyobject";
        _search.TextChanged += (_, _) => OptionsChanged?.Invoke();
        _commentsOnly.Content = "Comments only";
        _commentsOnly.Margin = new Avalonia.Thickness(0, 4, 0, 0);
        _commentsOnly.IsCheckedChanged += (_, _) => OptionsChanged?.Invoke();

        var optionsGrid = new Grid
        {
            Margin = new Avalonia.Thickness(8),
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
        };
        optionsGrid.Children.Add(Labeled("Show", _displayMode, 0, 0));
        optionsGrid.Children.Add(Labeled("Sort", _sortMode, 1, 0));
        Grid.SetColumnSpan(_search, 2);
        Grid.SetRow(_search, 1);
        optionsGrid.Children.Add(_search);
        Grid.SetRow(_commentsOnly, 2);
        Grid.SetColumnSpan(_commentsOnly, 2);
        optionsGrid.Children.Add(_commentsOnly);

        _header.Margin = new Avalonia.Thickness(8, 0, 8, 6);
        _header.Foreground = Brushes.LightSkyBlue;
        _header.TextWrapping = TextWrapping.Wrap;
        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedItem is ListBoxItem { Tag: TagExplorerEntry entry }) EntryActivated?.Invoke(entry);
        };

        var root = new DockPanel();
        DockPanel.SetDock(optionsGrid, Dock.Top);
        DockPanel.SetDock(_header, Dock.Top);
        root.Children.Add(optionsGrid);
        root.Children.Add(_header);
        root.Children.Add(new ScrollViewer { Content = _list });
        Content = root;

        SetEntries(entries);
    }

    public void SetEntries(IReadOnlyList<TagExplorerEntry> entries)
    {
        _header.Text = entries.Count == 0
            ? "No matching tag explorer entries."
            : $"{entries.Count} entr{(entries.Count == 1 ? "y" : "ies")}. Click a row to select and reveal it.";

        var rows = new List<ListBoxItem>();
        foreach (TagExplorerEntry entry in entries)
        {
            rows.Add(new ListBoxItem
            {
                Content = new TextBlock { Text = FormatEntry(entry), TextWrapping = TextWrapping.Wrap },
                Tag = entry,
            });
        }
        _list.ItemsSource = rows;
    }

    private static Control Labeled(string label, Control control, int column, int row)
    {
        var panel = new StackPanel { Spacing = 2, Margin = new Avalonia.Thickness(0, 0, 8, 6) };
        panel.Children.Add(new TextBlock { Text = label, Foreground = Brushes.LightGray });
        panel.Children.Add(control);
        Grid.SetColumn(panel, column);
        Grid.SetRow(panel, row);
        return panel;
    }

    private static string FormatEntry(TagExplorerEntry entry)
    {
        string tag = entry.Tag == 0 ? "" : $" tag {entry.Tag}";
        string action = entry.Action == 0 ? "" : $" action {entry.Action}";
        string polyobject = entry.PolyobjectNumber == TagExplorerModel.NoPolyobjectNumber ? "" : $" polyobject {entry.PolyobjectNumber}";
        string comment = entry.Comment.Length == 0 ? "" : $" - {entry.Comment}";
        return $"{entry.DefaultName} {entry.Index}:{tag}{action}{polyobject}{comment}";
    }
}
