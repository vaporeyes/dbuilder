// ABOUTME: Modal reference listing the 2D and 3D editing keyboard/mouse shortcuts in a readable two-column layout.
// ABOUTME: Replaces the cluttered shortcut dump that used to fill the info panel.

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class ShortcutsWindow : Window
{
    private readonly IReadOnlyList<EditorShortcutBinding> _bindings;

    public ShortcutsWindow(IReadOnlyList<EditorShortcutBinding>? bindings = null)
    {
        _bindings = bindings ?? EditorCommandCatalog.DefaultShortcuts;
        Title = "Keyboard & Mouse Shortcuts";
        Width = 560;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var stack = new StackPanel { Margin = new Avalonia.Thickness(14), Spacing = 10 };
        stack.Children.Add(Section("Window commands", EditorCommandCatalog.ByScope(EditorCommandScope.Window)));
        stack.Children.Add(Section("2D editing", EditorCommandCatalog.ByScope(EditorCommandScope.Map2D)));
        stack.Children.Add(Section("3D mode", EditorCommandCatalog.ByScope(EditorCommandScope.Map3D)));

        var close = new Button { Content = "Close", MinWidth = 80, IsCancel = true, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        stack.Children.Add(close);

        Content = new ScrollViewer { Content = stack };
    }

    private Control Section(string title, IReadOnlyList<EditorCommandDescriptor> rows)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        panel.Children.Add(new TextBlock
        {
            Text = title, Foreground = Brushes.LightSkyBlue, FontWeight = FontWeight.Bold, FontSize = 14,
            Margin = new Avalonia.Thickness(0, 0, 0, 4),
        });
        foreach (var command in rows)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("150,*"), Margin = new Avalonia.Thickness(0, 1) };
            grid.Children.Add(new TextBlock { Text = EditorCommandCatalog.GestureText(command.Id, _bindings), Foreground = Brushes.Khaki, FontSize = 12 });
            var d = new TextBlock { Text = command.Title, Foreground = new SolidColorBrush(Color.FromRgb(0xd0, 0xd8, 0xe0)), FontSize = 12, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(d, 1);
            grid.Children.Add(d);
            panel.Children.Add(grid);
        }
        return panel;
    }
}
