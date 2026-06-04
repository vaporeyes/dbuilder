// ABOUTME: Non-modal thing statistics window listing map counts by thing type.
// ABOUTME: Includes configured thing titles/classes and lets the host select all things of a type.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class ThingStatisticsWindow : Window
{
    private readonly StackPanel _rows = new() { Margin = new Avalonia.Thickness(10, 0, 10, 10), Spacing = 3 };
    private readonly IReadOnlyList<ThingTypeStatistic> _usedTypes;
    private readonly GameConfiguration? _config;
    private readonly CheckBox _hideUnused = new() { Content = "Hide unused thing types", IsChecked = true };

    /// <summary>Raised when the user requests selecting all things with a type.</summary>
    public event Action<int>? ThingTypeActivated;

    public ThingStatisticsWindow(IReadOnlyList<ThingTypeStatistic> usedTypes, GameConfiguration? config)
    {
        Title = "Thing Types";
        Width = 680;
        Height = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _usedTypes = usedTypes;
        _config = config;

        var root = new DockPanel();
        var top = new StackPanel { Margin = new Avalonia.Thickness(10, 8), Spacing = 5 };
        top.Children.Add(new TextBlock
        {
            Text = ThingStatisticsWindowModel.HeaderText(
                usedTypes.Sum(t => t.Count),
                ThingStatisticsWindowModel.BuildRows(usedTypes, config, hideUnused: false).Count),
            Foreground = Brushes.LightSkyBlue,
        });
        _hideUnused.IsCheckedChanged += (_, _) => RebuildRows();
        top.Children.Add(_hideUnused);
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        root.Children.Add(new ScrollViewer { Content = _rows });
        Content = root;
        RebuildRows();
    }

    private void RebuildRows()
    {
        _rows.Children.Clear();
        _rows.Children.Add(HeaderRow());
        bool hideUnused = _hideUnused.IsChecked == true;
        foreach (var row in ThingStatisticsWindowModel.BuildRows(_usedTypes, _config, hideUnused))
            _rows.Children.Add(DataRow(row));
    }

    private static Grid HeaderRow()
    {
        var grid = RowGrid();
        AddHeader(grid, "Type", 0);
        AddHeader(grid, "Title", 1);
        AddHeader(grid, "Class", 2);
        AddHeader(grid, "Count", 3);
        return grid;
    }

    private Grid DataRow(ThingStatisticsRow row)
    {
        var grid = RowGrid();
        AddText(grid, row.Type.ToString(), 0, Brushes.Khaki);
        AddText(grid, row.Title, 1, Brushes.White);
        AddText(grid, row.ClassName, 2, Brushes.White);

        if (row.Count == 0)
        {
            AddText(grid, "0", 3, Brushes.Gray);
        }
        else
        {
            var button = new Button
            {
                Content = row.Count.ToString(),
                MinWidth = 58,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Avalonia.Thickness(4, 1),
            };
            button.Click += (_, _) => ThingTypeActivated?.Invoke(row.Type);
            Grid.SetColumn(button, 3);
            grid.Children.Add(button);
        }

        return grid;
    }

    private static Grid RowGrid()
        => new() { ColumnDefinitions = new ColumnDefinitions("70,220,250,70") };

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
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(4, 2),
        };
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }
}
