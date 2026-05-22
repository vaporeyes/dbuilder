// ABOUTME: Modal dialog listing the maps in the open WAD so the user can switch to any of them.
// ABOUTME: Returns the chosen MapEntry via Selected; ShowDialog<bool> yields true on confirm.

using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class MapPickerDialog : Window
{
    private readonly ListBox _list;
    private readonly List<MapEntry> _maps;

    /// <summary>The map the user chose, or null if cancelled.</summary>
    public MapEntry? Selected { get; private set; }

    public MapPickerDialog(List<MapEntry> maps, string? current)
    {
        _maps = maps;
        Title = "Open Map";
        Width = 320;
        Height = 440;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _list = new ListBox
        {
            ItemsSource = maps.Select(m => $"{m.Name}   [{m.Format}]").ToList(),
        };
        int cur = current != null ? maps.FindIndex(m => m.Name == current) : 0;
        _list.SelectedIndex = cur < 0 ? 0 : cur;
        _list.DoubleTapped += (_, _) => Accept();

        var ok = new Button { Content = "Open", MinWidth = 72, IsDefault = true };
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
        root.Children.Add(_list);
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);
        Content = root;
    }

    private void Accept()
    {
        int i = _list.SelectedIndex;
        if (i >= 0 && i < _maps.Count) { Selected = _maps[i]; Close(true); }
        else Close(false);
    }
}
