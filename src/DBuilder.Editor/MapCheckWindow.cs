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

    /// <summary>Raised when the user selects an issue row, carrying the issue so the host can navigate to it.</summary>
    public event Action<MapIssue>? IssueActivated;

    public MapCheckWindow(IReadOnlyList<MapIssue> issues)
    {
        Title = "Map Analysis";
        Width = 480;
        Height = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _header.Margin = new Avalonia.Thickness(10, 8);
        _header.TextWrapping = TextWrapping.Wrap;
        UpdateHeader(issues.Count, issues);

        var ignoreSelected = new Button
        {
            Content = "Ignore Selected",
            Margin = new Avalonia.Thickness(10, 0, 10, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        ignoreSelected.Click += (_, _) => IgnoreSelected();

        var header = new StackPanel
        {
            Children = { _header, ignoreSelected },
        };

        foreach (var iss in issues)
        {
            bool err = iss.Severity == MapIssueSeverity.Error;
            _rows.Add(new ListBoxItem
            {
                Content = new TextBlock
                {
                    Text = $"{(err ? "ERROR" : "warn")}: {iss.Message}",
                    Foreground = err ? Brushes.Salmon : Brushes.Khaki,
                    TextWrapping = TextWrapping.Wrap,
                },
                Tag = iss,
            });
        }
        _list.SelectionMode = SelectionMode.Multiple;
        _list.ItemsSource = _rows;
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
        foreach (var row in selected)
        {
            if (row.Tag is MapIssue issue)
                issue.SetIgnored(true);
            _rows.Remove(row);
        }

        _list.ItemsSource = null;
        _list.ItemsSource = _rows;
        UpdateHeader(_rows.Count, _rows.Select(row => (MapIssue)row.Tag!));
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
