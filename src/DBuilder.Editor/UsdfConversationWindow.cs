// ABOUTME: Non-modal USDF conversation viewer for parsed DIALOGUE map lumps.
// ABOUTME: Presents includes, conversations, pages, inventory conditions, and choices without save-back editing.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class UsdfConversationWindow : Window
{
    private readonly ListBox _list = new();

    public UsdfConversationWindow(UsdfParseResult result)
    {
        Title = UsdfDialogEditorModel.MainFormTitle;
        Width = UsdfDialogEditorModel.DefaultClientWidth;
        Height = UsdfDialogEditorModel.DefaultClientHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel { Margin = new Thickness(10) };
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock { Text = UsdfDialogueParser.ViewerStatus(result), FontWeight = FontWeight.Bold });
        if (result.Success)
            header.Children.Add(new TextBlock { Text = UsdfDialogueParser.ViewerSummary(result.Document) });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        foreach (UsdfConversationRow row in UsdfDialogueParser.ViewerRows(result))
            _list.Items.Add(Row(row));

        root.Children.Add(new ScrollViewer { Content = _list });
        Content = root;
    }

    private static Control Row(UsdfConversationRow row)
        => new TextBlock
        {
            Text = new string(' ', row.Depth * 2) + row.Text,
            Foreground = BrushFor(row.Kind),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2),
        };

    private static IBrush BrushFor(UsdfConversationRowKind kind)
        => kind switch
        {
            UsdfConversationRowKind.Include => Brushes.LightSkyBlue,
            UsdfConversationRowKind.Conversation => Brushes.White,
            UsdfConversationRowKind.Page => Brushes.Gainsboro,
            _ => Brushes.LightGray,
        };
}
