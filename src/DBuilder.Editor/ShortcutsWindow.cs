// ABOUTME: Modal reference listing the 2D and 3D editing keyboard/mouse shortcuts in a readable two-column layout.
// ABOUTME: Replaces the cluttered shortcut dump that used to fill the info panel.

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DBuilder.Editor;

public sealed class ShortcutsWindow : Window
{
    private static readonly (string Key, string Desc)[] Mode2D =
    {
        ("Click", "Select element"),
        ("Left-drag", "Box-select (or move a grabbed vertex/thing)"),
        ("Right-drag", "Pan the view"),
        ("Wheel / - =", "Zoom out / in"),
        ("R", "Fit map to view"),
        ("Double-click", "Edit properties"),
        ("Right-click", "Split the nearest line"),
        ("1 / 2 / 3 / 4", "Vertices / Lines / Sectors / Things mode"),
        ("S / T", "Toggle sector fills / things"),
        ("Y", "Toggle sprites / direction arrows"),
        ("D / Shift+D", "Draw sector / draw lines"),
        ("I", "Insert vertex or thing"),
        ("M", "Make sector at cursor"),
        ("F / Shift+F", "Flip linedef / flip sidedefs"),
        ("A / Shift+A", "Align textures X / Y"),
        ("Ctrl/Cmd+C / V", "Copy / paste selection"),
        ("G", "Toggle grid snap"),
        ("[ / ]", "Decrease / increase grid size"),
        ("Delete", "Remove selection (undoable)"),
        ("Tab", "Enter 3D mode"),
    };

    private static readonly (string Key, string Desc)[] Mode3D =
    {
        ("WASD", "Move"),
        ("Arrows / drag", "Look around"),
        ("Q / E", "Move up / down"),
        ("G", "Toggle walk mode (gravity)"),
        ("Wheel", "Raise/lower floor, ceiling or thing Z (Shift = by 1)"),
        ("Right-drag", "Move a thing or drag a surface height"),
        ("[ / ]", "Sector brightness down / up"),
        ("C / V", "Copy / apply texture"),
        ("T", "Browse textures"),
        ("A / Shift+A", "Align texture"),
        ("Shift+arrows", "Nudge texture offset"),
        ("O", "Reset texture offsets"),
        ("Click / Esc", "Select surfaces / clear selection"),
        ("Enter", "Edit properties"),
        ("Delete", "Remove targeted thing"),
        ("Tab", "Back to 2D mode"),
    };

    public ShortcutsWindow()
    {
        Title = "Keyboard & Mouse Shortcuts";
        Width = 560;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var stack = new StackPanel { Margin = new Avalonia.Thickness(14), Spacing = 10 };
        stack.Children.Add(Section("2D editing", Mode2D));
        stack.Children.Add(Section("3D mode", Mode3D));

        var close = new Button { Content = "Close", MinWidth = 80, IsCancel = true, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        stack.Children.Add(close);

        Content = new ScrollViewer { Content = stack };
    }

    private static Control Section(string title, (string Key, string Desc)[] rows)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        panel.Children.Add(new TextBlock
        {
            Text = title, Foreground = Brushes.LightSkyBlue, FontWeight = FontWeight.Bold, FontSize = 14,
            Margin = new Avalonia.Thickness(0, 0, 0, 4),
        });
        foreach (var (key, desc) in rows)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("150,*"), Margin = new Avalonia.Thickness(0, 1) };
            grid.Children.Add(new TextBlock { Text = key, Foreground = Brushes.Khaki, FontSize = 12 });
            var d = new TextBlock { Text = desc, Foreground = new SolidColorBrush(Color.FromRgb(0xd0, 0xd8, 0xe0)), FontSize = 12, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(d, 1);
            grid.Children.Add(d);
            panel.Children.Add(grid);
        }
        return panel;
    }
}
