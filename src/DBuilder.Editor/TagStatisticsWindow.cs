// ABOUTME: Non-modal tag statistics window with per-element usage counts and selection buttons.
// ABOUTME: Lets the host select sectors, linedefs, things or all elements that use a chosen tag.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class TagStatisticsWindow : Window
{
    /// <summary>Raised when a row action requests selecting elements that use a tag.</summary>
    public event Action<int, MapControl.EditMode?>? TagActivated;

    public TagStatisticsWindow(IReadOnlyList<TagStatistic> tags)
    {
        Title = "Used Tags";
        Width = 520;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel();
        var header = new TextBlock
        {
            Text = tags.Count == 0 ? "No tags in use." : $"{tags.Count} tag(s) in use.",
            Foreground = tags.Count == 0 ? Brushes.LightGreen : Brushes.LightSkyBlue,
            Margin = new Avalonia.Thickness(10, 8),
            TextWrapping = TextWrapping.Wrap,
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var rows = new StackPanel { Margin = new Avalonia.Thickness(10, 0, 10, 10), Spacing = 3 };
        rows.Children.Add(HeaderRow());
        foreach (var tag in tags) rows.Children.Add(DataRow(tag));

        root.Children.Add(new ScrollViewer { Content = rows });
        Content = root;
    }

    private static Grid HeaderRow()
    {
        var grid = RowGrid();
        AddHeader(grid, "Tag", 0);
        AddHeader(grid, "All", 1);
        AddHeader(grid, "Sectors", 2);
        AddHeader(grid, "Lines", 3);
        AddHeader(grid, "Things", 4);
        return grid;
    }

    private Grid DataRow(TagStatistic tag)
    {
        var grid = RowGrid();
        AddText(grid, tag.Tag.ToString(), 0, Brushes.Khaki);
        AddButton(grid, tag.Tag, 1, tag.Total, null);
        AddButton(grid, tag.Tag, 2, tag.Sectors, MapControl.EditMode.Sectors);
        AddButton(grid, tag.Tag, 3, tag.Linedefs, MapControl.EditMode.Linedefs);
        AddButton(grid, tag.Tag, 4, tag.Things, MapControl.EditMode.Things);
        return grid;
    }

    private void AddButton(Grid grid, int tag, int column, int count, MapControl.EditMode? mode)
    {
        if (count == 0)
        {
            AddText(grid, "0", column, Brushes.Gray);
            return;
        }

        var button = new Button
        {
            Content = count.ToString(),
            MinWidth = 58,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Avalonia.Thickness(4, 1),
        };
        button.Click += (_, _) => TagActivated?.Invoke(tag, mode);
        Grid.SetColumn(button, column);
        grid.Children.Add(button);
    }

    private static Grid RowGrid()
        => new() { ColumnDefinitions = new ColumnDefinitions("70,70,90,90,90") };

    private static void AddHeader(Grid grid, string text, int column)
        => AddText(grid, text, column, Brushes.LightSkyBlue, FontWeight.Bold);

    private static void AddText(Grid grid, string text, int column, IBrush brush, FontWeight weight = default)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = brush,
            FontWeight = weight,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(4, 2),
        };
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }
}
