// ABOUTME: Modal reference listing the 2D and 3D editing keyboard/mouse shortcuts in a readable two-column layout.
// ABOUTME: Replaces the cluttered shortcut dump that used to fill the info panel.

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class ShortcutsWindow : Window
{
    private static readonly SolidColorBrush TextBrush = new(Color.FromRgb(0xd0, 0xd8, 0xe0));
    private static readonly SolidColorBrush MutedBrush = new(Color.FromRgb(0x9a, 0xa8, 0xb6));
    private static readonly SolidColorBrush RowBrush = new(Color.FromRgb(0x20, 0x26, 0x2d));

    private readonly IReadOnlyList<EditorShortcutBinding> _bindings;
    private readonly StackPanel _sections = new() { Spacing = 8 };
    private readonly TextBlock _matchSummary = new() { Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Center };
    private readonly Dictionary<string, bool> _expandedSections = ShortcutHelpModel.GroupTitles.ToDictionary(title => title, ShortcutHelpModel.IsDefaultExpanded);
    private readonly TextBox _search = new()
    {
        Watermark = "Search shortcuts",
        MinHeight = 32,
    };

    public ShortcutsWindow(IReadOnlyList<EditorShortcutBinding>? bindings = null)
    {
        _bindings = bindings ?? EditorCommandCatalog.DefaultShortcuts;
        Title = "Keyboard & Mouse Shortcuts";
        Width = 720;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid
        {
            Margin = new Avalonia.Thickness(14),
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 10,
        };

        var toolbar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 8,
        };
        _search.TextChanged += (_, _) => RebuildSections(_search.Text);
        toolbar.Children.Add(_search);

        var expandAll = new Button { Content = "Expand All", MinWidth = 92 };
        expandAll.Click += (_, _) => SetAllSectionsExpanded(true);
        Grid.SetColumn(expandAll, 1);
        toolbar.Children.Add(expandAll);

        var collapseAll = new Button { Content = "Collapse All", MinWidth = 92 };
        collapseAll.Click += (_, _) => SetAllSectionsExpanded(false);
        Grid.SetColumn(collapseAll, 2);
        toolbar.Children.Add(collapseAll);
        root.Children.Add(toolbar);

        var scroll = new ScrollViewer { Content = _sections };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        footer.Children.Add(_matchSummary);

        var close = new Button { Content = "Close", MinWidth = 80, IsCancel = true, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 1);
        footer.Children.Add(close);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Content = root;
        RebuildSections("");
    }

    private void RebuildSections(string? filter)
    {
        string text = filter?.Trim() ?? "";
        bool searching = text.Length > 0;
        var groups = ShortcutHelpModel.BuildSections(EditorCommandCatalog.All, _bindings, text);
        int matchCount = groups.Sum(group => group.Rows.Count);
        _sections.Children.Clear();

        foreach (var group in groups)
        {
            bool expanded = searching || (_expandedSections.TryGetValue(group.Title, out bool value) ? value : group.DefaultExpanded);
            _sections.Children.Add(Section(group, expanded, searching));
        }

        if (_sections.Children.Count == 0)
            _sections.Children.Add(new TextBlock { Text = "No shortcuts found.", Foreground = MutedBrush, Margin = new Avalonia.Thickness(2, 12) });

        _matchSummary.Text = ShortcutHelpModel.MatchSummary(text, EditorCommandCatalog.All.Count, groups.Count, matchCount);
    }

    private Control Section(ShortcutHelpSection section, bool expand, bool searching)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2, Margin = new Avalonia.Thickness(0, 6, 0, 2) };
        for (int i = 0; i < section.Rows.Count; i++)
            panel.Children.Add(Row(section.Rows[i], i));

        var count = new TextBlock
        {
            Text = section.Rows.Count.ToString(),
            Foreground = Brushes.Khaki,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(12, 0, 0, 0),
        };
        Grid.SetColumn(count, 1);
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = section.Title, Foreground = Brushes.LightSkyBlue, FontWeight = FontWeight.Bold, FontSize = 14 },
                        new TextBlock { Text = section.Description, Foreground = MutedBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap },
                    },
                },
                count,
            },
        };

        var expander = new Expander
        {
            Header = header,
            Content = panel,
            IsExpanded = expand,
        };
        expander.PropertyChanged += (_, e) =>
        {
            if (!searching && e.Property == Expander.IsExpandedProperty)
                _expandedSections[section.Title] = expander.IsExpanded;
        };
        return expander;
    }

    private Control Row(ShortcutHelpRow row, int index)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("180,*"),
            Margin = new Avalonia.Thickness(0, 1),
        };
        grid.Children.Add(new TextBlock
        {
            Text = row.GestureText,
            Foreground = Brushes.Khaki,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Top,
        });
        var description = new TextBlock
        {
            Text = row.Command.Title,
            Foreground = TextBrush,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetColumn(description, 1);
        grid.Children.Add(description);

        return new Border
        {
            Background = index % 2 == 0 ? Brushes.Transparent : RowBrush,
            CornerRadius = new Avalonia.CornerRadius(2),
            Padding = new Avalonia.Thickness(6, 4),
            Child = grid,
        };
    }

    private void SetAllSectionsExpanded(bool expanded)
    {
        foreach (string title in ShortcutHelpModel.GroupTitles)
            _expandedSections[title] = expanded;
        RebuildSections(_search.Text);
    }
}
