// ABOUTME: Non-modal RejectExplorer surface for REJECT lump validation and sector relation summaries.
// ABOUTME: Presents parsed visibility relationships while leaving map navigation and selection to MainWindow.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class RejectExplorerWindow : Window
{
    private readonly ListBox _rows = new();

    public event Action<int>? SectorActivated;
    public event Action? SelectNoLineOfSightRequested;
    public event Action? ConfigureColorsRequested;

    public RejectExplorerWindow(
        RejectExplorerValidation validation,
        RejectTable? reject,
        int sectorCount,
        int? highlightedSector)
    {
        Title = "Reject Explorer";
        Width = 620;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 8 };
        root.Children.Add(new TextBlock { Text = RejectExplorerModel.FormatValidation(validation), FontWeight = Avalonia.Media.FontWeight.Bold });
        root.Children.Add(new TextBlock { Text = highlightedSector is int h ? $"Highlighted sector: {h}" : "Highlighted sector: none" });

        if (reject != null && reject.HasData)
        {
            var rows = RejectExplorerModel.BuildRows(reject, sectorCount, highlightedSector);
            root.Children.Add(new TextBlock { Text = RejectExplorerModel.FormatCounts(rows) });
            foreach (RejectExplorerRow row in rows)
                _rows.Items.Add(new ListBoxItem { Content = RejectExplorerModel.FormatRow(row), Tag = row });
        }

        _rows.DoubleTapped += (_, _) =>
        {
            if (_rows.SelectedItem is ListBoxItem { Tag: RejectExplorerRow row })
                SectorActivated?.Invoke(row.SectorIndex);
        };
        root.Children.Add(new ScrollViewer { Content = _rows });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var selectNoLos = new Button { Content = "Select No Line Of Sight" };
        selectNoLos.Click += (_, _) => SelectNoLineOfSightRequested?.Invoke();
        selectNoLos.IsEnabled = reject?.HasData == true && highlightedSector != null;
        buttons.Children.Add(selectNoLos);
        var configureColors = new Button { Content = RejectExplorerModel.ColorConfigurationAction.Title };
        configureColors.Click += (_, _) => ConfigureColorsRequested?.Invoke();
        buttons.Children.Add(configureColors);

        var close = new Button { Content = "Close" };
        close.Click += OnClose;
        buttons.Children.Add(close);
        root.Children.Add(buttons);

        Content = root;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
