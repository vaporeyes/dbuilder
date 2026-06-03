// ABOUTME: Modal Avalonia dialog for UDBScript JavaScript and internal exception stack traces.
// ABOUTME: Presents the script error metadata produced by the shared runner model.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class UdbScriptErrorDialogWindow : Window
{
    public UdbScriptErrorDialogWindow(UdbScriptErrorDialog dialog)
    {
        Title = dialog.Title;
        Width = 640;
        Height = 420;
        MinWidth = 480;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var message = new TextBlock
        {
            Text = dialog.MessageLabel,
            TextWrapping = TextWrapping.Wrap,
        };

        var tabs = new TabControl
        {
            SelectedIndex = dialog.SelectedTabIndex,
            Items =
            {
                StackTraceTab(dialog.JavaScriptStackTraceTabText, dialog.StackTraceText),
                StackTraceTab(dialog.InternalStackTraceTabText, dialog.InternalStackTraceText),
            },
        };

        var ok = new Button
        {
            Content = dialog.OkButtonText,
            MinWidth = 84,
            IsDefault = true,
            IsCancel = true,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        ok.Click += (_, _) => Close();

        Content = new Grid
        {
            Margin = new Avalonia.Thickness(12),
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 10,
            Children =
            {
                message,
                tabs,
                ok,
            },
        };
        Grid.SetRow(tabs, 1);
        Grid.SetRow(ok, 2);
    }

    private static TabItem StackTraceTab(string header, string text)
        => new()
        {
            Header = header,
            Content = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new TextBox
                {
                    Text = text,
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.NoWrap,
                },
            },
        };
}

public sealed class UdbScriptParserErrorDialogWindow : Window
{
    public UdbScriptParserErrorDialogWindow(string title, string message)
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
        var ok = new Button
        {
            Content = UdbScriptRunnerModel.ErrorDialogOkButtonText,
            MinWidth = 72,
            IsDefault = true,
            IsCancel = true,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        ok.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12),
            Spacing = 12,
            Children =
            {
                text,
                ok,
            },
        };
    }
}
