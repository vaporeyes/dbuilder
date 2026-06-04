// ABOUTME: Non-modal window listing map analysis issues; selecting an issue raises IssueActivated so the host can locate it.
// ABOUTME: Errors are shown in red, warnings in yellow, with UDB-style selected-result ignore support.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class MapCheckWindow : Window
{
    private readonly ListBox _list = new();
    private readonly TextBlock _header = new();
    private readonly List<ListBoxItem> _rows = new();
    private readonly Button[] _fixButtons;
    private readonly MapIssueListModel _model;
    private readonly Func<MapIssueFix, bool>? _applyFix;
    private Func<IReadOnlyList<MapErrorCheckerDescriptor>, IReadOnlyList<MapIssue>>? _runChecks;
    private MapErrorCheckerSelectionModel? _checkerSelection;

    /// <summary>Raised when the user selects an issue row, carrying the issue so the host can navigate to it.</summary>
    public event Action<MapIssue>? IssueActivated;

    public event Action<int>? IssuesChanged;

    public MapCheckWindow(
        IReadOnlyList<MapIssue> issues,
        MapErrorCheckerSelectionModel? checkerSelection = null,
        Func<IReadOnlyList<MapErrorCheckerDescriptor>, IReadOnlyList<MapIssue>>? runChecks = null,
        Func<MapIssueFix, bool>? applyFix = null)
    {
        _model = new MapIssueListModel(issues);
        _checkerSelection = checkerSelection;
        _runChecks = runChecks;
        _applyFix = applyFix;
        _fixButtons = Enumerable.Range(0, 3).Select(FixButton).ToArray();

        Title = "Map Analysis";
        Width = 480;
        Height = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _header.Margin = new Avalonia.Thickness(10, 8);
        _header.TextWrapping = TextWrapping.Wrap;
        UpdateHeader(_model.VisibleIssues);

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

        var copySelected = new Button
        {
            Content = "Copy",
            Margin = new Avalonia.Thickness(0, 0, 10, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        copySelected.Click += async (_, _) => await CopySelectedToClipboard();

        var hideType = new Button
        {
            Content = "Hide Type",
            Margin = new Avalonia.Thickness(0, 0, 10, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        hideType.Click += (_, _) => HideSelectedTypes();

        var selectType = new Button
        {
            Content = "Select Type",
            Margin = new Avalonia.Thickness(0, 0, 10, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        selectType.Click += (_, _) => SelectSelectedTypes();

        var showOnlyType = new Button
        {
            Content = "Show Only Type",
            Margin = new Avalonia.Thickness(0, 0, 10, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        showOnlyType.Click += (_, _) => ShowOnlySelectedTypes();

        var header = new StackPanel();
        header.Children.Add(_header);
        if (checkerSelection is not null)
            header.Children.Add(CheckerSelectionPanel(checkerSelection, runChecks));
        header.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { ignoreSelected, showAll, copySelected, hideType, selectType, showOnlyType },
        });
        header.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _fixButtons[0], _fixButtons[1], _fixButtons[2] },
        });

        _list.SelectionMode = SelectionMode.Multiple;
        RefreshRows();
        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedItem is ListBoxItem { Tag: MapIssue mi }) IssueActivated?.Invoke(mi);
            UpdateFixButtons();
        };
        _list.KeyUp += async (_, e) =>
        {
            if (e.Key == Key.C && HasCopyModifier(e.KeyModifiers))
            {
                await CopySelectedToClipboard();
                e.Handled = true;
            }
        };

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(new ScrollViewer { Content = _list });
        Content = root;
        UpdateFixButtons();
    }

    private Button FixButton(int index)
    {
        var button = new Button
        {
            Margin = new Avalonia.Thickness(index == 0 ? 10 : 0, 0, 10, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            IsVisible = false,
            Tag = index,
        };
        button.Click += (_, _) => ApplySelectedFix(index);
        return button;
    }

    private void IgnoreSelected()
    {
        _model.HideSelected(SelectedIssues());
        RefreshRows();
    }

    private void ShowAll()
    {
        _model.ShowAll();
        RefreshRows();
    }

    private void HideSelectedTypes()
    {
        _model.HideSelectedKinds(SelectedIssues());
        RefreshRows();
    }

    private void SelectSelectedTypes()
    {
        var matching = _model.VisibleIssuesWithSelectedKinds(SelectedIssues()).ToHashSet();
        if (matching.Count == 0) return;

        _list.SelectedItems?.Clear();
        foreach (var row in _rows)
        {
            if (row.Tag is MapIssue issue && matching.Contains(issue))
                _list.SelectedItems?.Add(row);
        }
    }

    private void ShowOnlySelectedTypes()
    {
        _model.ShowOnlySelectedKinds(SelectedIssues());
        RefreshRows();
    }

    private async System.Threading.Tasks.Task CopySelectedToClipboard()
    {
        string text = MapIssueListModel.FormatIssueDescriptions(SelectedIssues());
        if (text.Length == 0) return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;

        await clipboard.SetTextAsync(text);
    }

    private MapIssue[] SelectedIssues() =>
        _list.SelectedItems?.OfType<ListBoxItem>().Select(row => (MapIssue)row.Tag!).ToArray()
        ?? Array.Empty<MapIssue>();

    private MapIssue? SelectedIssue =>
        _list.SelectedItem is ListBoxItem { Tag: MapIssue issue } ? issue : null;

    private void UpdateFixButtons()
    {
        var fixes = SelectedIssue?.Fixes ?? Array.Empty<MapIssueFix>();
        for (int i = 0; i < _fixButtons.Length; i++)
        {
            bool visible = _applyFix is not null && i < fixes.Count;
            _fixButtons[i].IsVisible = visible;
            _fixButtons[i].Content = visible ? fixes[i].Label : "";
        }
    }

    private void ApplySelectedFix(int index)
    {
        var issue = SelectedIssue;
        if (issue is null || _applyFix is null || index >= issue.Fixes.Count) return;
        if (!_applyFix(issue.Fixes[index])) return;

        foreach (var similarIssue in SelectedIssues())
        {
            if (ReferenceEquals(similarIssue, issue)) continue;
            if (similarIssue.Kind != issue.Kind || index >= similarIssue.Fixes.Count) continue;
            if (!_applyFix(similarIssue.Fixes[index])) break;
        }

        if (_runChecks is not null && _checkerSelection is not null)
            RunChecks(_checkerSelection, _runChecks);
        else
            RefreshRows();
    }

    private static bool HasCopyModifier(KeyModifiers modifiers) =>
        modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);

    private Control CheckerSelectionPanel(
        MapErrorCheckerSelectionModel selection,
        Func<IReadOnlyList<MapErrorCheckerDescriptor>, IReadOnlyList<MapIssue>>? runChecks)
    {
        var rows = new StackPanel { Spacing = 2, Margin = new Avalonia.Thickness(8, 4) };
        var expander = new Expander
        {
            IsExpanded = false,
            Margin = new Avalonia.Thickness(10, 0, 10, 8),
        };
        foreach (var row in selection.Rows)
        {
            var check = new CheckBox
            {
                Content = row.DisplayName,
                IsChecked = row.IsChecked,
                Margin = new Avalonia.Thickness(0, 1),
            };
            check.PropertyChanged += (_, e) =>
            {
                if (e.Property == CheckBox.IsCheckedProperty)
                {
                    selection.SetChecked(row.SettingsKey, check.IsChecked == true);
                    UpdateCheckerHeader(expander, selection);
                }
            };
            rows.Children.Add(check);
        }

        var content = new StackPanel();
        if (runChecks is not null)
        {
            var runButton = new Button
            {
                Content = "Run Checks",
                Margin = new Avalonia.Thickness(8, 6, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            runButton.Click += (_, _) => RunChecks(selection, runChecks);
            content.Children.Add(runButton);
        }

        content.Children.Add(new ScrollViewer
        {
            MaxHeight = 150,
            Content = rows,
        });

        expander.Content = content;
        UpdateCheckerHeader(expander, selection);
        return expander;
    }

    private void RunChecks(
        MapErrorCheckerSelectionModel selection,
        Func<IReadOnlyList<MapErrorCheckerDescriptor>, IReadOnlyList<MapIssue>> runChecks)
    {
        var issues = runChecks(selection.EnabledDescriptors());
        _model.ReplaceIssues(issues);
        RefreshRows();
        IssuesChanged?.Invoke(_model.VisibleIssues.Count);
    }

    private static void UpdateCheckerHeader(Expander expander, MapErrorCheckerSelectionModel selection)
    {
        int enabledCount = selection.EnabledDescriptors().Count;
        expander.Header = $"Checks ({enabledCount}/{selection.Rows.Count})";
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
        UpdateHeader(_model.VisibleIssues);
    }

    private void UpdateHeader(IReadOnlyList<MapIssue> issues)
    {
        _header.Text = MapIssueListModel.HeaderText(issues);
        _header.Foreground = issues.Count == 0 ? Brushes.LightGreen : Brushes.LightSkyBlue;
    }
}
