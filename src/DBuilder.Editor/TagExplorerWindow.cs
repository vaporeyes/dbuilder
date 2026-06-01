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
    private readonly Button _export = new();
    private readonly TextBlock _header = new();
    private readonly ListBox _list = new();
    private string _exportText = "";

    public event Action? OptionsChanged;
    public event Action<TagExplorerEntry>? EntryActivated;
    public event Action<string>? ExportRequested;

    public TagExplorerOptions Options => new(
        DisplayMode: _displayMode.SelectedItem is TagExplorerModeOption<TagExplorerDisplayMode> display ? display.Value : TagExplorerDisplayMode.TagsAndActions,
        SortMode: _sortMode.SelectedItem is TagExplorerModeOption<TagExplorerSortMode> sort ? sort.Value : TagExplorerSortMode.ByIndex,
        SearchText: _search.Text ?? "",
        CommentsOnly: _commentsOnly.IsChecked == true);

    public TagExplorerWindow(IReadOnlyList<TagExplorerEntry> entries, IReadOnlyDictionary<int, string>? tagLabels = null)
    {
        Title = "Tag Explorer";
        Width = 520;
        Height = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _displayMode.ItemsSource = TagExplorerModel.DisplayModeOptions;
        _displayMode.SelectedItem = TagExplorerModel.DisplayModeOptions[0];
        _displayMode.SelectionChanged += (_, _) => OptionsChanged?.Invoke();

        _sortMode.ItemsSource = TagExplorerModel.SortModeOptions;
        _sortMode.SelectedItem = TagExplorerModel.SortModeOptions[0];
        _sortMode.SelectionChanged += (_, _) => OptionsChanged?.Invoke();

        _search.Watermark = "Search, #tag, $action, ^polyobject";
        _search.TextChanged += (_, _) => OptionsChanged?.Invoke();
        _commentsOnly.Content = TagExplorerModel.CommentsOnlyText;
        _commentsOnly.Margin = new Avalonia.Thickness(0, 4, 0, 0);
        _commentsOnly.IsCheckedChanged += (_, _) => OptionsChanged?.Invoke();
        _export.Content = TagExplorerModel.ExportToFileText;
        _export.HorizontalAlignment = HorizontalAlignment.Right;
        _export.Click += (_, _) => ExportRequested?.Invoke(_exportText);

        var optionsGrid = new Grid
        {
            Margin = new Avalonia.Thickness(8),
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
        };
        optionsGrid.Children.Add(Labeled(TagExplorerModel.ShowLabel, _displayMode, 0, 0));
        optionsGrid.Children.Add(Labeled(TagExplorerModel.SortLabel, _sortMode, 1, 0));
        Grid.SetColumnSpan(_search, 2);
        Grid.SetRow(_search, 1);
        optionsGrid.Children.Add(_search);
        Grid.SetRow(_commentsOnly, 2);
        Grid.SetColumnSpan(_commentsOnly, 2);
        optionsGrid.Children.Add(_commentsOnly);
        Grid.SetRow(_export, 3);
        Grid.SetColumn(_export, 1);
        optionsGrid.Children.Add(_export);

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

        SetEntries(entries, tagLabels);
    }

    public void SetEntries(IReadOnlyList<TagExplorerEntry> entries, IReadOnlyDictionary<int, string>? tagLabels = null)
    {
        IReadOnlyList<TagExplorerTreeNode> tree = TagExplorerModel.BuildTree(entries, Options, tagLabels);
        _exportText = TagExplorerModel.ExportTreeText(tree, Options.SortMode);
        _export.IsEnabled = entries.Count > 0;
        _header.Text = entries.Count == 0
            ? "No matching tag explorer entries."
            : $"{entries.Count} entr{(entries.Count == 1 ? "y" : "ies")}. Click a row to select and reveal it.";

        var rows = new List<ListBoxItem>();
        foreach (TagExplorerTreeNode node in tree)
            AddRows(rows, node, depth: 0);

        _list.ItemsSource = rows;
    }

    private static void AddRows(List<ListBoxItem> rows, TagExplorerTreeNode node, int depth)
    {
        rows.Add(new ListBoxItem
        {
            Content = new TextBlock { Text = new string(' ', depth * 2) + node.Title, TextWrapping = TextWrapping.Wrap },
            Tag = node.Entry,
            IsEnabled = node.Entry != null,
        });

        foreach (TagExplorerTreeNode child in node.Children)
            AddRows(rows, child, depth + 1);
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

}
