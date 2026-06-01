// ABOUTME: Non-modal window listing map analysis issues; selecting an issue raises IssueActivated so the host can locate it.
// ABOUTME: Errors are shown in red, warnings in yellow, with UDB-style selected-result ignore support.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class MapCheckWindow : Window
{
    private readonly ListBox _list = new();
    private readonly TextBlock _header = new();
    private readonly List<ListBoxItem> _rows = new();
    private readonly MapIssueListModel _model;

    /// <summary>Raised when the user selects an issue row, carrying the issue so the host can navigate to it.</summary>
    public event Action<MapIssue>? IssueActivated;

    public MapCheckWindow(IReadOnlyList<MapIssue> issues)
    {
        _model = new MapIssueListModel(issues);

        Title = "Map Analysis";
        Width = 480;
        Height = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _header.Margin = new Avalonia.Thickness(10, 8);
        _header.TextWrapping = TextWrapping.Wrap;
        UpdateHeader(_model.VisibleIssues.Count, _model.VisibleIssues);

        var ignoreSelected = new Button
        {
            Content = "Ignore Selected",
            Margin = new Avalonia.Thickness(10, 0, 10, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        ignoreSelected.Click += (_, _) => IgnoreSelected();

        var showAll = new Button
        {
            Content = "Show All",
            Margin = new Avalonia.Thickness(0, 0, 10, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        showAll.Click += (_, _) => ShowAll();

        var hideType = new Button
        {
            Content = "Hide Type",
            Margin = new Avalonia.Thickness(0, 0, 10, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        hideType.Click += (_, _) => HideSelectedTypes();

        var showOnlyType = new Button
        {
            Content = "Show Only Type",
            Margin = new Avalonia.Thickness(0, 0, 10, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        showOnlyType.Click += (_, _) => ShowOnlySelectedTypes();

        var header = new StackPanel
        {
            Children =
            {
                _header,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = { ignoreSelected, showAll, hideType, showOnlyType },
                },
            },
        };

        _list.SelectionMode = SelectionMode.Multiple;
        RefreshRows();
        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedItem is ListBoxItem { Tag: MapIssue mi }) IssueActivated?.Invoke(mi);
        };

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(new ScrollViewer { Content = _list });
        Content = root;
    }

    private void IgnoreSelected()
    {
        var selected = _list.SelectedItems?.OfType<ListBoxItem>().ToArray() ?? Array.Empty<ListBoxItem>();
        _model.HideSelected(selected.Select(row => (MapIssue)row.Tag!));
        RefreshRows();
    }

    private void ShowAll()
    {
        _model.ShowAll();
        RefreshRows();
    }

    private void HideSelectedTypes()
    {
        var selected = _list.SelectedItems?.OfType<ListBoxItem>().Select(row => (MapIssue)row.Tag!).ToArray()
            ?? Array.Empty<MapIssue>();
        _model.HideSelectedKinds(selected);
        RefreshRows();
    }

    private void ShowOnlySelectedTypes()
    {
        var selected = _list.SelectedItems?.OfType<ListBoxItem>().Select(row => (MapIssue)row.Tag!).ToArray()
            ?? Array.Empty<MapIssue>();
        _model.ShowOnlySelectedKinds(selected);
        RefreshRows();
    }

    private void RefreshRows()
    {
        _rows.Clear();
        foreach (var issue in _model.VisibleIssues)
        {
            bool err = issue.Severity == MapIssueSeverity.Error;
            _rows.Add(new ListBoxItem
            {
                Content = new TextBlock
                {
                    Text = $"{(err ? "ERROR" : "warn")}: {issue.Message}",
                    Foreground = err ? Brushes.Salmon : Brushes.Khaki,
                    TextWrapping = TextWrapping.Wrap,
                },
                Tag = issue,
            });
        }

        _list.ItemsSource = null;
        _list.ItemsSource = _rows;
        UpdateHeader(_model.VisibleIssues.Count, _model.VisibleIssues);
    }

    private void UpdateHeader(int count, IEnumerable<MapIssue> issues)
    {
        int errors = 0, warnings = 0;
        foreach (var issue in issues)
        {
            if (issue.Severity == MapIssueSeverity.Error) errors++;
            else warnings++;
        }

        _header.Text = count == 0
            ? "No issues found."
            : $"{count} issue(s): {errors} error(s), {warnings} warning(s). Click an issue to locate it.";
        _header.Foreground = count == 0 ? Brushes.LightGreen : Brushes.LightSkyBlue;
    }
}
