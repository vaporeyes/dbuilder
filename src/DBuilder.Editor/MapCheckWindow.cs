// ABOUTME: Non-modal window listing map analysis issues; selecting an issue raises IssueActivated so the host can locate it.
// ABOUTME: Errors are shown in red, warnings in yellow, with a summary header.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class MapCheckWindow : Window
{
    private readonly ListBox _list = new();

    /// <summary>Raised when the user selects an issue row, carrying the issue so the host can navigate to it.</summary>
    public event Action<MapIssue>? IssueActivated;

    public MapCheckWindow(IReadOnlyList<MapIssue> issues)
    {
        Title = "Map Analysis";
        Width = 480;
        Height = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        int errors = 0, warnings = 0;
        foreach (var i in issues) { if (i.Severity == MapIssueSeverity.Error) errors++; else warnings++; }

        var header = new TextBlock
        {
            Text = issues.Count == 0
                ? "No issues found."
                : $"{issues.Count} issue(s): {errors} error(s), {warnings} warning(s). Click an issue to locate it.",
            Margin = new Avalonia.Thickness(10, 8),
            Foreground = issues.Count == 0 ? Brushes.LightGreen : Brushes.LightSkyBlue,
            TextWrapping = TextWrapping.Wrap,
        };

        var rows = new List<ListBoxItem>();
        foreach (var iss in issues)
        {
            bool err = iss.Severity == MapIssueSeverity.Error;
            rows.Add(new ListBoxItem
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
        _list.ItemsSource = rows;
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
}
