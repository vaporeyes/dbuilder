// ABOUTME: Modal dialog listing recoverable autosave snapshots.
// ABOUTME: Returns the selected autosave entry so MainWindow can load it as a dirty recovered map.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class AutoSaveRecoveryDialog : Window
{
    private readonly ListBox _list;
    private readonly IReadOnlyList<AutoSaveEntry> _entries;

    public AutoSaveEntry? Selected { get; private set; }

    public AutoSaveRecoveryDialog(IReadOnlyList<AutoSaveEntry> entries)
    {
        _entries = entries;
        Title = "Recover Autosave";
        Width = 560;
        Height = 420;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _list = new ListBox
        {
            ItemsSource = entries.Select(DisplayText).ToList(),
        };
        if (entries.Count > 0) _list.SelectedIndex = 0;
        _list.DoubleTapped += (_, _) => Accept();

        var recover = new Button { Content = "Recover", MinWidth = 82, IsDefault = true };
        recover.Click += (_, _) => Accept();
        var cancel = new Button { Content = "Cancel", MinWidth = 82, IsCancel = true };
        cancel.Click += (_, _) => Close(false);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 8, 0, 0),
        };
        buttons.Children.Add(recover);
        buttons.Children.Add(cancel);

        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Avalonia.Thickness(12) };
        root.Children.Add(_list);
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);
        Content = root;
    }

    private static string DisplayText(AutoSaveEntry entry)
        => $"{entry.DisplayName}   {entry.LastWriteTime.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)}";

    private void Accept()
    {
        int i = _list.SelectedIndex;
        if (i >= 0 && i < _entries.Count) { Selected = _entries[i]; Close(true); }
        else Close(false);
    }
}
