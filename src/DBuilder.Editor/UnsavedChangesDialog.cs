// ABOUTME: Modal discard-confirmation dialog shown before replacing or closing a dirty map.
// ABOUTME: Returns true only when the user explicitly chooses to discard unsaved changes.

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DBuilder.Editor;

public sealed class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog(string mapName)
    {
        Title = "Unsaved Changes";
        Width = 420;
        Height = 170;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var message = new TextBlock
        {
            Text = $"Discard unsaved changes to {mapName}?",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.White,
        };

        var discard = new Button { Content = "Discard", MinWidth = 90 };
        discard.Click += (_, _) => Close(true);
        var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true, IsDefault = true };
        cancel.Click += (_, _) => Close(false);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttons.Children.Add(discard);
        buttons.Children.Add(cancel);

        var root = new StackPanel { Margin = new Avalonia.Thickness(14), Spacing = 16 };
        root.Children.Add(message);
        root.Children.Add(buttons);
        Content = root;
    }
}
