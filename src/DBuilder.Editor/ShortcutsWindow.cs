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

        var search = new TextBox
        {
            Watermark = "Search shortcuts",
            MinHeight = 32,
        };
        search.TextChanged += (_, _) => RebuildSections(search.Text);
        root.Children.Add(search);

        var scroll = new ScrollViewer { Content = _sections };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var close = new Button { Content = "Close", MinWidth = 80, IsCancel = true, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        Grid.SetRow(close, 2);
        root.Children.Add(close);

        Content = root;
        RebuildSections("");
    }

    private void RebuildSections(string? filter)
    {
        string text = filter?.Trim() ?? "";
        _sections.Children.Clear();

        foreach (var group in Groups())
        {
            var rows = group.Commands
                .Where(command => string.IsNullOrWhiteSpace(text) || Matches(command, group.Title, text))
                .ToArray();

            if (rows.Length == 0) continue;
            _sections.Children.Add(Section(group.Title, rows, expand: text.Length > 0 || IsDefaultExpanded(group.Title)));
        }

        if (_sections.Children.Count == 0)
            _sections.Children.Add(new TextBlock { Text = "No shortcuts found.", Foreground = MutedBrush, Margin = new Avalonia.Thickness(2, 12) });
    }

    private Control Section(string title, IReadOnlyList<EditorCommandDescriptor> rows, bool expand)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2, Margin = new Avalonia.Thickness(0, 6, 0, 2) };
        for (int i = 0; i < rows.Count; i++)
            panel.Children.Add(Row(rows[i], i));

        return new Expander
        {
            Header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = title, Foreground = Brushes.LightSkyBlue, FontWeight = FontWeight.Bold, FontSize = 14 },
                    new TextBlock { Text = rows.Count.ToString(), Foreground = MutedBrush, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                },
            },
            Content = panel,
            IsExpanded = expand,
        };
    }

    private Control Row(EditorCommandDescriptor command, int index)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("180,*"),
            Margin = new Avalonia.Thickness(0, 1),
        };
        grid.Children.Add(new TextBlock
        {
            Text = EditorCommandCatalog.GestureText(command.Id, _bindings),
            Foreground = Brushes.Khaki,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Top,
        });
        var description = new TextBlock
        {
            Text = command.Title,
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

    private bool Matches(EditorCommandDescriptor command, string groupTitle, string filter)
        => Contains(groupTitle, filter)
            || Contains(command.Title, filter)
            || Contains(command.Id, filter)
            || Contains(ScopeTitle(command.Scope), filter)
            || Contains(EditorCommandCatalog.GestureText(command.Id, _bindings), filter);

    private static bool Contains(string value, string filter)
        => value.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<ShortcutGroup> Groups()
    {
        var groups = new List<ShortcutGroup>();
        foreach (string title in GroupTitles)
        {
            var commands = EditorCommandCatalog.All
                .Where(command => string.Equals(GroupTitle(command), title, StringComparison.Ordinal))
                .ToArray();
            if (commands.Length > 0) groups.Add(new ShortcutGroup(title, commands));
        }

        return groups;
    }

    private static string GroupTitle(EditorCommandDescriptor command)
    {
        string id = command.Id;
        if (command.Title.Contains("Script", StringComparison.OrdinalIgnoreCase) || id.Contains("udbscript", StringComparison.OrdinalIgnoreCase))
            return "Script commands";

        return command.Scope switch
        {
            EditorCommandScope.Window => WindowGroup(id),
            EditorCommandScope.Map2D => Map2DGroup(id),
            EditorCommandScope.Map3D => Map3DGroup(id),
            _ => ScopeTitle(command.Scope),
        };
    }

    private static string WindowGroup(string id)
    {
        if (id.Contains("-group-", StringComparison.Ordinal)) return "Selection groups";
        if (ContainsAny(id, "tag", "explorer", "viewer", "sound", "panel", "setup", "prefab", "export", "comments"))
            return "Tools and panels";
        if (ContainsAny(id, "cut", "copy", "paste", "duplicate", "delete", "properties", "select", "align", "make-door", "tag-range", "undo", "redo"))
            return "Window editing";
        return "File and configuration";
    }

    private static string Map2DGroup(string id)
    {
        if (ContainsAny(id, "mode", "view-mode", "toggle", "zoom", "fit", "pan")) return "2D view and modes";
        if (ContainsAny(id, "draw", "3dfloor", "slope", "bridge")) return "2D drawing and geometry";
        if (ContainsAny(id, "grid", "align", "sector", "linedef", "thing", "insert", "flip", "select", "make-sector"))
            return "2D editing";
        return "2D editing";
    }

    private static string Map3DGroup(string id)
    {
        if (ContainsAny(id, "move", "camera", "orbit", "look", "walk", "gravity")) return "3D navigation";
        if (ContainsAny(id, "texture", "offset", "unpegged", "align", "auto-align", "fit-textures")) return "3D textures";
        if (ContainsAny(id, "brightness", "slope", "render", "fog", "sky", "light", "scale", "height", "sector")) return "3D surfaces and rendering";
        if (ContainsAny(id, "thing", "select", "copy", "cut", "paste", "delete", "insert", "rotate", "pitch", "roll", "edit", "clear"))
            return "3D selection and things";
        return "3D selection and things";
    }

    private static bool ContainsAny(string value, params string[] parts)
        => parts.Any(part => value.Contains(part, StringComparison.OrdinalIgnoreCase));

    private static bool IsDefaultExpanded(string title)
        => title is "File and configuration" or "Window editing" or "2D view and modes" or "3D navigation";

    private static string ScopeTitle(EditorCommandScope scope)
        => scope switch
        {
            EditorCommandScope.Window => "Window commands",
            EditorCommandScope.Map2D => "2D editing",
            EditorCommandScope.Map3D => "3D mode",
            _ => scope.ToString(),
        };

    private static readonly string[] GroupTitles =
    {
        "File and configuration",
        "Window editing",
        "Tools and panels",
        "Selection groups",
        "Script commands",
        "2D view and modes",
        "2D drawing and geometry",
        "2D editing",
        "3D navigation",
        "3D selection and things",
        "3D textures",
        "3D surfaces and rendering",
    };

    private sealed record ShortcutGroup(string Title, IReadOnlyList<EditorCommandDescriptor> Commands);
}
