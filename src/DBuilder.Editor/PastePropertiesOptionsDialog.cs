// ABOUTME: Modal paste-properties options dialog for selecting copied element fields.
// ABOUTME: Applies checkbox state back to the shared paste-properties option model on OK.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class PastePropertiesOptionsDialog : Window
{
    private readonly PastePropertiesOptionsResult _options;
    private readonly Dictionary<string, CheckBox> _boxes = new(StringComparer.Ordinal);

    public PastePropertiesOptionsDialog(PastePropertiesOptionsResult options)
    {
        _options = options;
        Title = "Paste Properties";
        Width = 360;
        Height = 460;
        MinWidth = 320;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel { Margin = new Thickness(12) };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 0),
        };

        var ok = new Button { Content = "OK", MinWidth = 72, IsDefault = true };
        ok.Click += (_, _) =>
        {
            Apply();
            Close(true);
        };
        var cancel = new Button { Content = "Cancel", MinWidth = 72, IsCancel = true };
        cancel.Click += (_, _) => Close(false);
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

        var tabs = new TabControl();
        foreach (PastePropertiesOptionsTab tab in options.Tabs)
            tabs.Items.Add(CreateTab(tab));
        root.Children.Add(tabs);

        Content = root;
    }

    private TabItem CreateTab(PastePropertiesOptionsTab tab)
    {
        var rows = new StackPanel { Spacing = 6, Margin = new Thickness(8) };
        var checks = new List<CheckBox>();
        var tabButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 4),
        };

        var all = new Button { Content = "All", MinWidth = 64 };
        all.Click += (_, _) => SetChecks(checks, true);
        var none = new Button { Content = "None", MinWidth = 64 };
        none.Click += (_, _) => SetChecks(checks, false);
        tabButtons.Children.Add(all);
        tabButtons.Children.Add(none);
        rows.Children.Add(tabButtons);

        foreach (PastePropertiesOption option in tab.Options)
        {
            var box = new CheckBox
            {
                Content = option.Description,
                IsChecked = option.IsChecked,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _boxes[option.Key] = box;
            checks.Add(box);
            rows.Children.Add(box);
        }

        return new TabItem
        {
            Header = tab.Title,
            Content = new ScrollViewer
            {
                Content = rows,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            },
        };
    }

    private void Apply()
    {
        var values = _boxes.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.IsChecked == true,
            StringComparer.Ordinal);
        PastePropertiesOptionsModel.Apply(_options, values);
    }

    private static void SetChecks(IEnumerable<CheckBox> checks, bool value)
    {
        foreach (CheckBox check in checks)
            check.IsChecked = value;
    }
}
