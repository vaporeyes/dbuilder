// ABOUTME: Non-modal tag statistics window with per-element usage counts and selection buttons.
// ABOUTME: Lets the host select sectors, linedefs, things or all elements that use a chosen tag.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class TagStatisticsWindow : Window
{
    /// <summary>Raised when a row action requests selecting elements that use a tag.</summary>
    public event Action<int, MapControl.EditMode?>? TagActivated;

    /// <summary>Raised when a row label is edited.</summary>
    public event Action<int, string>? LabelChanged;

    public TagStatisticsWindow(IReadOnlyList<TagStatistic> tags, IReadOnlyDictionary<int, string>? labels = null)
    {
        Title = "Used Tags";
        Width = 700;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel();
        var header = new TextBlock
        {
            Text = TagWindowModel.TagStatisticsHeaderText(tags.Count),
            Foreground = tags.Count == 0 ? Brushes.LightGreen : Brushes.LightSkyBlue,
            Margin = new Avalonia.Thickness(10, 8),
            TextWrapping = TextWrapping.Wrap,
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var rowModels = TagWindowModel.BuildTagStatisticsRows(tags, labels);
        var rows = new StackPanel { Margin = new Avalonia.Thickness(10, 0, 10, 10), Spacing = 3 };
        rows.Children.Add(HeaderRow());
        foreach (var row in rowModels) rows.Children.Add(DataRow(row));

        root.Children.Add(new ScrollViewer { Content = rows });
        Content = root;
    }

    private static Grid HeaderRow()
    {
        var grid = RowGrid();
        AddHeader(grid, "Tag", 0);
        AddHeader(grid, "Label", 1);
        AddHeader(grid, "All", 2);
        AddHeader(grid, "Sectors", 3);
        AddHeader(grid, "Lines", 4);
        AddHeader(grid, "Things", 5);
        return grid;
    }

    private Grid DataRow(TagStatisticsRow row)
    {
        var grid = RowGrid();
        AddText(grid, row.Tag.ToString(), 0, Brushes.Khaki);
        AddLabelEditor(grid, row.Tag, row.Label);
        AddButton(grid, row.Tag, 2, row.Total, null);
        AddButton(grid, row.Tag, 3, row.Sectors, MapControl.EditMode.Sectors);
        AddButton(grid, row.Tag, 4, row.Linedefs, MapControl.EditMode.Linedefs);
        AddButton(grid, row.Tag, 5, row.Things, MapControl.EditMode.Things);
        return grid;
    }

    private void AddLabelEditor(Grid grid, int tag, string label)
    {
        var box = new TextBox
        {
            Text = label,
            Watermark = "Label",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Avalonia.Thickness(4, 1),
            Padding = new Avalonia.Thickness(4, 1),
        };
        box.TextChanged += (_, _) => LabelChanged?.Invoke(tag, box.Text?.Trim() ?? "");
        Grid.SetColumn(box, 1);
        grid.Children.Add(box);
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
        => new() { ColumnDefinitions = new ColumnDefinitions("70,180,70,90,90,90") };

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
