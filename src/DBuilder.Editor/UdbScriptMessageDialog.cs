// ABOUTME: Modal Avalonia dialog for UDBScript host showMessage and showMessageYesNo prompts.
// ABOUTME: Mirrors the runner message choices and returns the UDBScript message result to the caller.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class UdbScriptMessageDialog : Window
{
    public UdbScriptMessageDialog(UdbScriptMessageDialogPlan plan)
    {
        Title = plan.Title;
        Width = 520;
        Height = 320;
        MinWidth = 420;
        MinHeight = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var message = new TextBox
        {
            Text = plan.Message,
            IsReadOnly = plan.MessageReadOnly,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttons.Children.Add(ResultButton(plan.PrimaryButtonText, plan.SecondaryButtonText is null ? UdbScriptMessageResult.Ok : UdbScriptMessageResult.Yes, isDefault: true));
        if (plan.SecondaryButtonText is not null)
            buttons.Children.Add(ResultButton(plan.SecondaryButtonText, UdbScriptMessageResult.No, isDefault: false));
        buttons.Children.Add(AbortButton(plan));

        var root = new Grid
        {
            Margin = new Avalonia.Thickness(12),
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 10,
        };
        root.Children.Add(new ScrollViewer
        {
            Content = message,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);
        Content = root;
    }

    private Button ResultButton(string text, UdbScriptMessageResult result, bool isDefault)
    {
        var button = new Button { Content = text, MinWidth = 84, IsDefault = isDefault };
        button.Click += (_, _) => Close(result);
        return button;
    }

    private Button AbortButton(UdbScriptMessageDialogPlan plan)
    {
        var button = new Button { Content = plan.AbortButtonText, MinWidth = 84 };
        button.Click += async (_, _) =>
        {
            var confirm = new UdbScriptAbortConfirmationDialog(plan);
            if (await confirm.ShowDialog<bool>(this))
                Close(UdbScriptMessageResult.Abort);
        };
        return button;
    }
}

internal sealed class UdbScriptAbortConfirmationDialog : Window
{
    public UdbScriptAbortConfirmationDialog(UdbScriptMessageDialogPlan plan)
    {
        Title = plan.AbortConfirmationTitle;
        Width = 360;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var message = new TextBlock
        {
            Text = plan.AbortConfirmationMessage,
            TextWrapping = TextWrapping.Wrap,
        };
        var yes = new Button { Content = "Yes", MinWidth = 72, IsDefault = true };
        yes.Click += (_, _) => Close(true);
        var no = new Button { Content = "No", MinWidth = 72, IsCancel = true };
        no.Click += (_, _) => Close(false);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttons.Children.Add(yes);
        buttons.Children.Add(no);

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12),
            Spacing = 12,
            Children =
            {
                message,
                buttons,
            },
        };
    }
}
