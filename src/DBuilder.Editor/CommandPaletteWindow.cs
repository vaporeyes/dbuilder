// ABOUTME: Searchable command palette window for running registered editor commands.
// ABOUTME: Presents UDB-style command groups using the shared command palette model.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class CommandPaletteWindow : Window
{
    private static readonly SolidColorBrush TextBrush = new(Color.FromRgb(0xd0, 0xd8, 0xe0));
    private static readonly SolidColorBrush MutedBrush = new(Color.FromRgb(0x9a, 0xa8, 0xb6));
    private static readonly SolidColorBrush PanelBrush = new(Color.FromRgb(0x16, 0x1b, 0x21));
    private static readonly SolidColorBrush RowBrush = new(Color.FromRgb(0x20, 0x26, 0x2d));
    private static readonly SolidColorBrush DisabledBrush = new(Color.FromRgb(0x63, 0x6d, 0x77));

    private readonly IReadOnlyList<EditorShortcutBinding> _bindings;
    private readonly IReadOnlySet<string> _usableCommandIds;
    private readonly IReadOnlyList<string> _recentCommandIds;
    private readonly Action<string> _runCommand;
    private readonly StackPanel _groups = new() { Spacing = 8 };
    private readonly TextBlock _matchSummary = new() { Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _search = new()
    {
        Watermark = "Search commands",
        MinHeight = 34,
    };

    private CommandPaletteRow? _firstUsableRow;

    public CommandPaletteWindow(
        IReadOnlyList<EditorShortcutBinding> bindings,
        IReadOnlySet<string> usableCommandIds,
        IReadOnlyList<string> recentCommandIds,
        Action<string> runCommand)
    {
        _bindings = bindings;
        _usableCommandIds = usableCommandIds;
        _recentCommandIds = recentCommandIds;
        _runCommand = runCommand;

        Title = "Command Palette";
        Width = 560;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid
        {
            Margin = new Avalonia.Thickness(14),
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 10,
        };

        root.Children.Add(SearchBar());

        var scroll = new ScrollViewer { Content = _groups };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
        };
        footer.Children.Add(_matchSummary);
        var close = new Button { Content = "Close", MinWidth = 80, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 1);
        footer.Children.Add(close);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Content = root;
        Opened += (_, _) => _search.Focus();
        RebuildGroups("");
    }

    private Control SearchBar()
    {
        _search.TextChanged += (_, _) => RebuildGroups(_search.Text);
        _search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _firstUsableRow is { } row)
            {
                e.Handled = true;
                RunCommand(row.Command.Id);
            }
        };

        return new Border
        {
            Background = PanelBrush,
            CornerRadius = new Avalonia.CornerRadius(4),
            Padding = new Avalonia.Thickness(8),
            Child = _search,
        };
    }

    private void RebuildGroups(string? filter)
    {
        string text = filter?.Trim() ?? "";
        var groups = CommandPaletteModel.BuildGroups(
            EditorCommandCatalog.All,
            _bindings,
            _usableCommandIds,
            text,
            _recentCommandIds);

        _firstUsableRow = groups
            .SelectMany(group => group.Rows)
            .FirstOrDefault(row => row.IsUsable);
        _groups.Children.Clear();

        foreach (var group in groups)
            _groups.Children.Add(Group(group));

        if (_groups.Children.Count == 0)
            _groups.Children.Add(new TextBlock { Text = "No commands found.", Foreground = MutedBrush, Margin = new Avalonia.Thickness(2, 12) });

        int matchCount = groups.Sum(group => group.Rows.Count);
        _matchSummary.Text = text.Length == 0
            ? $"{matchCount} commands"
            : $"{matchCount} commands matched";
    }

    private Control Group(CommandPaletteGroup group)
    {
        var rows = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        for (int i = 0; i < group.Rows.Count; i++)
            rows.Children.Add(Row(group.Rows[i], i));

        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = group.Title,
                    Foreground = Brushes.LightSkyBlue,
                    FontWeight = FontWeight.Bold,
                    FontSize = 13,
                    Margin = new Avalonia.Thickness(2, 5, 0, 0),
                },
                rows,
            },
        };
    }

    private Control Row(CommandPaletteRow row, int index)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,90,120"),
            ColumnSpacing = 8,
        };
        var title = new StackPanel { Orientation = Orientation.Vertical, Spacing = 1 };
        title.Children.Add(new TextBlock
        {
            Text = row.Command.Title,
            Foreground = row.IsUsable ? TextBrush : DisabledBrush,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        });
        title.Children.Add(new TextBlock
        {
            Text = row.Command.Id,
            Foreground = MutedBrush,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
        });
        grid.Children.Add(title);

        var category = new TextBlock
        {
            Text = row.CategoryText,
            Foreground = MutedBrush,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(category, 1);
        grid.Children.Add(category);

        var gesture = new TextBlock
        {
            Text = row.GestureText,
            Foreground = row.IsUsable ? Brushes.Khaki : DisabledBrush,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
        };
        Grid.SetColumn(gesture, 2);
        grid.Children.Add(gesture);

        var button = new Button
        {
            Content = grid,
            Padding = new Avalonia.Thickness(7, 5),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsEnabled = row.IsUsable,
        };
        button.Click += (_, _) => RunCommand(row.Command.Id);

        return new Border
        {
            Background = index % 2 == 0 ? Brushes.Transparent : RowBrush,
            CornerRadius = new Avalonia.CornerRadius(2),
            Child = button,
        };
    }

    private void RunCommand(string commandId)
    {
        Close();
        _runCommand(commandId);
    }
}
