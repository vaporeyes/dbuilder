// ABOUTME: Non-modal status history window that lists recent editor notifications.
// ABOUTME: Lets users inspect prior status messages after they have left the status bar.

using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class StatusHistoryWindow : Window
{
    private readonly IReadOnlyList<StatusHistoryEntry> _entries;
    private readonly Action? _onClear;
    private readonly TextBlock _header = new();
    private readonly StackPanel _rows = new() { Margin = new Avalonia.Thickness(10, 0, 10, 10), Spacing = 3 };

    public StatusHistoryWindow(IReadOnlyList<StatusHistoryEntry> entries, Action? onClear = null)
    {
        _entries = entries;
        _onClear = onClear;
        Title = "Status History";
        Width = 640;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel();
        _header.Margin = new Avalonia.Thickness(10, 8);
        _header.TextWrapping = TextWrapping.Wrap;
        DockPanel.SetDock(_header, Dock.Top);
        root.Children.Add(_header);

        if (_onClear != null)
        {
            var clear = new Button
            {
                Content = "Clear",
                MinWidth = 80,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Avalonia.Thickness(10),
            };
            clear.Click += (_, _) =>
            {
                _onClear();
                RefreshRows();
            };
            DockPanel.SetDock(clear, Dock.Bottom);
            root.Children.Add(clear);
        }

        RefreshRows();

        root.Children.Add(new ScrollViewer { Content = _rows });
        Content = root;
    }

    private void RefreshRows()
    {
        _header.Text = StatusHistory.HeaderTextFor(_entries);
        _header.Foreground = _entries.Count == 0 ? Brushes.LightGreen : Brushes.LightSkyBlue;

        _rows.Children.Clear();
        _rows.Children.Add(HeaderRow());
        foreach (var entry in _entries) _rows.Children.Add(DataRow(entry));
    }

    private static Grid HeaderRow()
    {
        var grid = RowGrid();
        AddText(grid, "Time", 0, Brushes.LightSkyBlue, FontWeight.Bold);
        AddText(grid, "Kind", 1, Brushes.LightSkyBlue, FontWeight.Bold);
        AddText(grid, "Message", 2, Brushes.LightSkyBlue, FontWeight.Bold);
        return grid;
    }

    private static Grid DataRow(StatusHistoryEntry entry)
    {
        var grid = RowGrid();
        AddText(grid, entry.Timestamp.LocalDateTime.ToString("g", CultureInfo.CurrentCulture), 0, Brushes.Khaki);
        AddText(grid, entry.Kind.ToString(), 1, KindBrush(entry.Kind));
        AddText(grid, entry.Message, 2, new SolidColorBrush(Color.FromRgb(0xd0, 0xd8, 0xe0)));
        return grid;
    }

    private static IBrush KindBrush(StatusHistoryKind kind)
        => kind switch
        {
            StatusHistoryKind.Warning => Brushes.Orange,
            StatusHistoryKind.Busy => Brushes.Khaki,
            StatusHistoryKind.Action => Brushes.LightGreen,
            StatusHistoryKind.Selection => Brushes.LightSteelBlue,
            StatusHistoryKind.Ready => Brushes.LightGreen,
            _ => Brushes.LightSkyBlue,
        };

    private static Grid RowGrid()
        => new() { ColumnDefinitions = new ColumnDefinitions("150,90,*") };

    private static void AddText(Grid grid, string text, int column, IBrush brush, FontWeight weight = default)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = brush,
            FontWeight = weight,
            VerticalAlignment = VerticalAlignment.Top,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(4, 2),
        };
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }
}
