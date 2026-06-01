// ABOUTME: Modal dialog for editing named UDMF flags on a selected map element.
// ABOUTME: Combines config-known flag checkboxes with a freeform list for mod-specific flag names.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;

namespace DBuilder.Editor;

public sealed class UdmfFlagsDialog : Window
{
    private readonly List<CheckBox> _knownBoxes = new();
    private readonly TextBox _custom;

    public HashSet<string> ResultFlags { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public UdmfFlagsDialog(string elementName, IEnumerable<string> knownFlags, IEnumerable<string> currentFlags)
    {
        Title = "Flags";
        Width = 420;
        Height = 500;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var current = new HashSet<string>(currentFlags.Where(f => !string.IsNullOrWhiteSpace(f)), StringComparer.OrdinalIgnoreCase);
        var known = knownFlags.Where(f => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = elementName,
            Foreground = Avalonia.Media.Brushes.LightSkyBlue,
            Margin = new Avalonia.Thickness(0, 0, 0, 4),
        });

        foreach (string flag in known)
        {
            var box = new CheckBox { Content = flag, IsChecked = current.Contains(flag) };
            _knownBoxes.Add(box);
            panel.Children.Add(box);
        }

        var customFlags = current.Except(known, StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        panel.Children.Add(new TextBlock { Text = "Additional flags", Margin = new Avalonia.Thickness(0, 8, 0, 0) });
        _custom = new TextBox
        {
            Text = string.Join(Environment.NewLine, customFlags),
            AcceptsReturn = true,
            MinHeight = 90,
            TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
            FontFamily = new Avalonia.Media.FontFamily("monospace"),
        };
        panel.Children.Add(_custom);

        var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };

        var ok = new Button { Content = "OK", MinWidth = 72, IsDefault = true };
        ok.Click += (_, _) => Accept();
        var cancel = new Button { Content = "Cancel", MinWidth = 72, IsCancel = true };
        cancel.Click += (_, _) => Close(false);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 8, 0, 0),
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Avalonia.Thickness(12) };
        root.Children.Add(scroll);
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);
        Content = root;
    }

    private void Accept()
    {
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var box in _knownBoxes)
            if (box.IsChecked == true && box.Content is string flag) flags.Add(flag);

        foreach (string raw in (_custom.Text ?? "").Replace("\r", "").Split('\n'))
        {
            string flag = raw.Trim();
            if (flag.Length > 0) flags.Add(flag);
        }

        ResultFlags = flags;
        Close(true);
    }
}
