// ABOUTME: Non-modal Sound Environments surface for ZDoom sound environment groups.
// ABOUTME: Lists environment sectors, things, and boundary linedefs while map selection stays in the owner.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed record SoundEnvironmentSelection(
    SoundEnvironmentInfo? Environment,
    Thing? Thing,
    Linedef? Linedef);

public sealed class SoundEnvironmentWindow : Window
{
    private readonly CheckBox _warningsOnly = new();
    private readonly TextBlock _header = new();
    private readonly ListBox _list = new();
    private SoundEnvironmentModeModel _model;
    private readonly bool _udmf;

    public event Action<SoundEnvironmentSelection>? SelectionActivated;

    public SoundEnvironmentWindow(SoundEnvironmentModeModel model, bool udmf)
    {
        _model = model;
        _udmf = udmf;

        Title = SoundEnvironmentModeModel.DockerTitle;
        Width = 520;
        Height = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _warningsOnly.Content = SoundEnvironmentModeModel.ShowWarningsOnlyText;
        _warningsOnly.Margin = new Thickness(8, 8, 8, 4);
        _warningsOnly.IsCheckedChanged += (_, _) => RefreshRows();

        _header.Margin = new Thickness(8, 0, 8, 6);
        _header.Foreground = Brushes.LightSkyBlue;
        _header.TextWrapping = TextWrapping.Wrap;

        var select = new Button
        {
            Content = "Select",
            Margin = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        select.Click += (_, _) => ActivateSelectedRow();
        _list.DoubleTapped += (_, _) => ActivateSelectedRow();

        var root = new DockPanel();
        DockPanel.SetDock(_warningsOnly, Dock.Top);
        DockPanel.SetDock(_header, Dock.Top);
        DockPanel.SetDock(select, Dock.Bottom);
        root.Children.Add(_warningsOnly);
        root.Children.Add(_header);
        root.Children.Add(select);
        root.Children.Add(new ScrollViewer { Content = _list });
        Content = root;

        RefreshRows();
    }

    public void SetModel(SoundEnvironmentModeModel model)
    {
        _model = model;
        RefreshRows();
    }

    private void ActivateSelectedRow()
    {
        if (_list.SelectedItem is ListBoxItem { Tag: SoundEnvironmentRow row })
            SelectionActivated?.Invoke(new SoundEnvironmentSelection(row.Environment, row.Thing, row.Linedef));
    }

    private void RefreshRows()
    {
        IReadOnlyList<SoundEnvironmentRow> rows = _model.Rows(_udmf, _warningsOnly.IsChecked == true);
        SoundEnvironmentWarningsOnlyState warningsOnly = _model.WarningsOnlyState(_warningsOnly.IsChecked == true, _udmf);
        _warningsOnly.Content = warningsOnly.Text;
        _warningsOnly.IsEnabled = warningsOnly.Enabled;
        _header.Text = _model.HeaderText(rows.Count);

        var items = new List<ListBoxItem>();
        foreach (SoundEnvironmentRow row in rows)
        {
            items.Add(new ListBoxItem
            {
                Content = RowContent(row),
                Tag = row,
            });
        }

        _list.ItemsSource = items;
    }

    private Control RowContent(SoundEnvironmentRow row)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        panel.Children.Add(new Border
        {
            Width = 12,
            Height = 12,
            Margin = new Thickness(row.Depth * 16, 2, 0, 0),
            Background = new SolidColorBrush(ToColor(row.Color)),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
        });
        panel.Children.Add(new TextBlock
        {
            Text = row.Warning ? "Warning: " + row.Text : row.Text,
            Foreground = row.Warning ? Brushes.OrangeRed : Brushes.LightGray,
            TextWrapping = TextWrapping.Wrap,
        });
        if (row.WarningMessage != null)
            ToolTip.SetTip(panel, row.WarningMessage);
        return panel;
    }

    private static Color ToColor(uint argb)
        => Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));
}
