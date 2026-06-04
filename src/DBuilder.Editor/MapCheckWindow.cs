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
    private readonly TextBlock _selectionInfo = new();
    private readonly List<ListBoxItem> _rows = new();
    private readonly Button _ignoreSelected;
    private readonly Button _showAll;
    private readonly Button _copySelected;
    private readonly Button _hideType;
    private readonly Button _selectType;
    private readonly Button _showOnlyType;
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
        _selectionInfo.Margin = new Avalonia.Thickness(10, 0, 10, 8);
        _selectionInfo.Foreground = Brushes.LightGray;
        _selectionInfo.TextWrapping = TextWrapping.Wrap;

        _ignoreSelected = ActionButton("Ignore Selected", leading: true);
        _ignoreSelected.Click += (_, _) => IgnoreSelected();

        _showAll = ActionButton("Show All");
        _showAll.Click += (_, _) => ShowAll();

        _copySelected = ActionButton("Copy");
        _copySelected.Click += async (_, _) => await CopySelectedToClipboard();

        _hideType = ActionButton("Hide Type");
        _hideType.Click += (_, _) => HideSelectedTypes();

        _selectType = ActionButton("Select Type");
        _selectType.Click += (_, _) => SelectSelectedTypes();

        _showOnlyType = ActionButton("Show Only Type");
        _showOnlyType.Click += (_, _) => ShowOnlySelectedTypes();

        var header = new StackPanel();
        header.Children.Add(_header);
        header.Children.Add(_selectionInfo);
        if (checkerSelection is not null)
            header.Children.Add(CheckerSelectionPanel(checkerSelection, runChecks));
        header.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _ignoreSelected, _showAll, _copySelected, _hideType, _selectType, _showOnlyType },
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
            UpdateActionButtons();
            UpdateFixButtons();
            UpdateSelectionInfo();
            UpdateWindowTitle();
        };
        _list.KeyUp += async (_, e) =>
        {
            if (e.Key == Key.C && HasCopyModifier(e.KeyModifiers))
            {
                await CopySelectedToClipboard();
                e.Handled = true;
            }
            else if (e.Key == Key.A && HasCopyModifier(e.KeyModifiers))
            {
                SelectAllVisibleResults();
                e.Handled = true;
            }
        };

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(new ScrollViewer { Content = _list });
        Content = root;
        UpdateFixButtons();
        UpdateActionButtons();
        UpdateSelectionInfo();
        UpdateWindowTitle();
    }

    private static Button ActionButton(string content, bool leading = false) => new()
    {
        Content = content,
        Margin = new Avalonia.Thickness(leading ? 10 : 0, 0, 10, 8),
        HorizontalAlignment = HorizontalAlignment.Left,
    };

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

    private void SelectAllVisibleResults()
    {
        var visible = _model.AllVisibleIssues().ToHashSet();
        if (visible.Count == 0) return;

        _list.SelectedItems?.Clear();
        foreach (var row in _rows)
        {
            if (row.Tag is MapIssue issue && visible.Contains(issue))
                _list.SelectedItems?.Add(row);
        }
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
        var selected = SelectedIssues();
        var fixes = MapIssueListModel.HaveSameFixSignature(selected)
            ? SelectedIssue?.Fixes ?? Array.Empty<MapIssueFix>()
            : Array.Empty<MapIssueFix>();
        for (int i = 0; i < _fixButtons.Length; i++)
        {
            bool visible = _applyFix is not null && i < fixes.Count;
            _fixButtons[i].IsVisible = visible;
            _fixButtons[i].Content = visible ? fixes[i].Label : "";
        }
    }

    private void UpdateActionButtons()
    {
        bool hasSelection = SelectedIssues().Length > 0;
        bool hasHidden = _model.VisibleIssues.Count < _model.AllIssues.Count;
        _ignoreSelected.IsEnabled = hasSelection;
        _copySelected.IsEnabled = hasSelection;
        _hideType.IsEnabled = hasSelection;
        _selectType.IsEnabled = hasSelection;
        _showOnlyType.IsEnabled = hasSelection;
        _showAll.IsEnabled = hasHidden;
    }

    private void UpdateSelectionInfo()
    {
        var selected = SelectedIssues();
        if (!MapIssueListModel.HaveSameFixSignature(selected))
        {
            _selectionInfo.Text = "Several types of map analysis results are selected. To display fixes, make sure that only a single result type is selected.";
            return;
        }

        var issue = SelectedIssue;
        if (issue is not null)
        {
            string fixes = issue.Fixes.Count == 0
                ? ""
                : " Fixes: " + string.Join(", ", issue.Fixes.Take(3).Select(fix => fix.Label)) + ".";
            _selectionInfo.Text = issue.Message + fixes;
            return;
        }

        _selectionInfo.Text = _model.AllIssues.Count > 0 && _model.VisibleIssues.Count == 0
            ? "All results are hidden. Use Show All to restore them."
            : "Select a result to view details. Hold Ctrl to select several results. Hold Shift to select a range.";
    }

    private void UpdateWindowTitle()
    {
        Title = MapIssueListModel.WindowTitleText(
            _model.AllIssues.Count,
            _model.VisibleIssues.Count,
            SelectedIssues().Length);
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
        if (_model.AllIssues.Count == 0)
        {
            _rows.Add(new ListBoxItem
            {
                Content = new TextBlock
                {
                    Text = MapIssueListModel.NoErrorsResultText,
                    Foreground = Brushes.LightGreen,
                    TextWrapping = TextWrapping.Wrap,
                },
                IsEnabled = false,
            });
        }

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
        UpdateActionButtons();
        UpdateSelectionInfo();
        UpdateWindowTitle();
    }

    private void UpdateHeader(IReadOnlyList<MapIssue> issues)
    {
        _header.Text = MapIssueListModel.HeaderText(issues);
        _header.Foreground = issues.Count == 0 ? Brushes.LightGreen : Brushes.LightSkyBlue;
    }
}
