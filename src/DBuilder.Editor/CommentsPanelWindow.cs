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
    private readonly CheckBox _filterCurrentMode = new();
    private readonly CheckBox _clickSelects = new();
    private readonly TextBox _search = new();
    private readonly TextBox _comment = new();
    private readonly ListBox _list = new();
    private readonly TextBlock _header = new();
    private CommentsPanelMode _currentMode;
    private CommentsPanelMode _selectedFilterMode = CommentsPanelMode.All;

    public event Action<CommentsPanelMode>? FilterChanged;
    public event Action<CommentsPanelPersistedSettings>? OptionsChanged;
    public event Action<CommentGroup>? GroupActivated;
    public event Action<CommentGroup>? RemoveRequested;
    public event Action<string>? SetSelectedCommentRequested;

    public CommentsPanelPersistedSettings Settings =>
        new(_filterCurrentMode.IsChecked == true, _clickSelects.IsChecked == true);

    public CommentsPanelMode FilterMode =>
        CommentsPanelModel.EffectiveFilterMode(Settings, _currentMode, _selectedFilterMode);

    public string SearchText => _search.Text ?? "";

    public CommentsPanelWindow(
        IReadOnlyList<CommentGroup> groups,
        CommentsPanelPersistedSettings settings,
        CommentsPanelMode currentMode)
    {
        Title = "Comments";
        Width = 420;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _currentMode = currentMode;

        _filter.ItemsSource = new[]
        {
            CommentsPanelMode.All,
            CommentsPanelMode.Vertices,
            CommentsPanelMode.Linedefs,
            CommentsPanelMode.Sectors,
            CommentsPanelMode.Things,
        };
        _filter.SelectedItem = _selectedFilterMode;
        _filter.SelectionChanged += (_, _) =>
        {
            if (_filter.SelectedItem is not CommentsPanelMode mode || mode == _selectedFilterMode) return;
            _selectedFilterMode = mode;
            FilterChanged?.Invoke(FilterMode);
        };
        _filterCurrentMode.Content = "Comments from this mode only";
        _filterCurrentMode.IsChecked = settings.FilterMode;
        _filterCurrentMode.IsCheckedChanged += (_, _) =>
        {
            UpdateFilterState();
            OptionsChanged?.Invoke(Settings);
        };
        _clickSelects.Content = "Select on click";
        _clickSelects.IsChecked = settings.ClickSelects;
        _clickSelects.IsCheckedChanged += (_, _) => OptionsChanged?.Invoke(Settings);
        UpdateFilterState();

        _search.Watermark = "Search comments";
        _search.TextChanged += (_, _) => FilterChanged?.Invoke(FilterMode);
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
        var optionsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Margin = new Avalonia.Thickness(8, 0, 8, 8),
            Children =
            {
                _filterCurrentMode,
                _clickSelects,
            },
        };
        var searchRow = new DockPanel { Margin = new Avalonia.Thickness(8, 0, 8, 8), LastChildFill = true };
        searchRow.Children.Add(_search);

        _header.Margin = new Avalonia.Thickness(8, 0, 8, 6);
        _header.Foreground = Brushes.LightSkyBlue;
        _header.TextWrapping = TextWrapping.Wrap;
        _list.SelectionChanged += (_, _) =>
        {
            if (_clickSelects.IsChecked == true && _list.SelectedItem is ListBoxItem { Tag: CommentGroup group })
                GroupActivated?.Invoke(group);
        };

        var selectButton = new Button
        {
            Content = "Select Comment",
            Margin = new Avalonia.Thickness(8, 8, 0, 8),
        };
        selectButton.Click += (_, _) =>
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
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                selectButton,
                removeButton,
            },
        };

        var root = new DockPanel();
        DockPanel.SetDock(filterRow, Dock.Top);
        DockPanel.SetDock(optionsRow, Dock.Top);
        DockPanel.SetDock(searchRow, Dock.Top);
        DockPanel.SetDock(entryRow, Dock.Top);
        DockPanel.SetDock(_header, Dock.Top);
        DockPanel.SetDock(buttonRow, Dock.Bottom);
        root.Children.Add(filterRow);
        root.Children.Add(optionsRow);
        root.Children.Add(searchRow);
        root.Children.Add(entryRow);
        root.Children.Add(_header);
        root.Children.Add(buttonRow);
        root.Children.Add(new ScrollViewer { Content = _list });
        Content = root;

        SetGroups(groups);
    }

    public void SetCurrentMode(CommentsPanelMode mode)
    {
        if (_currentMode == mode) return;
        _currentMode = mode;
        UpdateFilterState();
    }

    public void SetGroups(IReadOnlyList<CommentGroup> groups)
    {
        _header.Text = CommentsPanelModel.HeaderText(groups.Count);

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

    public void SetSelectionComment(string comment)
    {
        if (string.Equals(_comment.Text ?? "", comment, StringComparison.Ordinal)) return;
        _comment.Text = comment;
    }

    private static string FormatGroup(CommentGroup group)
        => $"{group.Group}: {group.Comment} ({group.Elements.Count} element{(group.Elements.Count == 1 ? "" : "s")})";

    private void UpdateFilterState()
    {
        _filter.IsEnabled = _filterCurrentMode.IsChecked != true;
        if (_filterCurrentMode.IsChecked == true) _filter.SelectedItem = _currentMode;
    }
}
