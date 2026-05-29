// ABOUTME: Non-modal status history window that lists recent editor notifications.
// ABOUTME: Lets users inspect prior status messages after they have left the status bar.

using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class StatusHistoryWindow : Window
{
    public StatusHistoryWindow(IReadOnlyList<StatusHistoryEntry> entries)
    {
        Title = "Status History";
        Width = 640;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel();
        var header = new TextBlock
        {
            Text = entries.Count == 0 ? "No status messages yet." : $"{entries.Count} recent status message(s).",
            Foreground = entries.Count == 0 ? Brushes.LightGreen : Brushes.LightSkyBlue,
            Margin = new Avalonia.Thickness(10, 8),
            TextWrapping = TextWrapping.Wrap,
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var rows = new StackPanel { Margin = new Avalonia.Thickness(10, 0, 10, 10), Spacing = 3 };
        rows.Children.Add(HeaderRow());
        foreach (var entry in entries) rows.Children.Add(DataRow(entry));

        root.Children.Add(new ScrollViewer { Content = rows });
        Content = root;
    }

    private static Grid HeaderRow()
    {
        var grid = RowGrid();
        AddText(grid, "Time", 0, Brushes.LightSkyBlue, FontWeight.Bold);
        AddText(grid, "Message", 1, Brushes.LightSkyBlue, FontWeight.Bold);
        return grid;
    }

    private static Grid DataRow(StatusHistoryEntry entry)
    {
        var grid = RowGrid();
        AddText(grid, entry.Timestamp.LocalDateTime.ToString("g", CultureInfo.CurrentCulture), 0, Brushes.Khaki);
        AddText(grid, entry.Message, 1, new SolidColorBrush(Color.FromRgb(0xd0, 0xd8, 0xe0)));
        return grid;
    }

    private static Grid RowGrid()
        => new() { ColumnDefinitions = new ColumnDefinitions("150,*") };

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
