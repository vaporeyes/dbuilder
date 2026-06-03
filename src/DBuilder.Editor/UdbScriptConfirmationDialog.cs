// ABOUTME: Modal Avalonia confirmation dialog for UDBScript yes/no runtime prompts.
// ABOUTME: Returns true for the primary choice and false for the secondary choice or window cancellation.

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DBuilder.Editor;

public sealed class UdbScriptConfirmationDialog : Window
{
    public UdbScriptConfirmationDialog(string title, string message, string primaryButtonText = "Yes", string secondaryButtonText = "No")
    {
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
        };
        var primary = new Button { Content = primaryButtonText, MinWidth = 72, IsDefault = true };
        primary.Click += (_, _) => Close(true);
        var secondary = new Button { Content = secondaryButtonText, MinWidth = 72, IsCancel = true };
        secondary.Click += (_, _) => Close(false);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttons.Children.Add(primary);
        buttons.Children.Add(secondary);

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12),
            Spacing = 12,
            Children =
            {
                text,
                buttons,
            },
        };
    }
}
