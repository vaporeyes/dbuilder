// ABOUTME: Non-modal Sound Environments surface for ZDoom sound environment groups.
// ABOUTME: Lists environment sectors, things, and boundary linedefs while map selection stays in the owner.

using System;
using System.Collections.Generic;
using System.Linq;
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
    private int _warningCount;

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
        if (_list.SelectedItem is ListBoxItem { Tag: SoundEnvironmentSelection selection })
            SelectionActivated?.Invoke(selection);
    }

    private void RefreshRows()
    {
        List<SoundEnvironmentRow> rows = BuildRows().ToList();
        _warningsOnly.Content = $"{SoundEnvironmentModeModel.ShowWarningsOnlyText} ({_warningCount})";
        _warningsOnly.IsEnabled = _warningCount > 0 || _warningsOnly.IsChecked == true;
        _header.Text = rows.Count == 0
            ? "No sound environments to display."
            : $"{_model.Environments.Count} sound environment(s), {_model.UnassignedSectors.Count} unassigned sector(s), {_model.BoundaryLinedefs.Count} boundary linedef(s).";

        var items = new List<ListBoxItem>();
        foreach (SoundEnvironmentRow row in rows)
        {
            items.Add(new ListBoxItem
            {
                Content = RowContent(row),
                Tag = row.Selection,
            });
        }

        _list.ItemsSource = items;
    }

    private IEnumerable<SoundEnvironmentRow> BuildRows()
    {
        _warningCount = 0;
        bool warningsOnly = _warningsOnly.IsChecked == true;
        foreach (SoundEnvironmentInfo environment in _model.Environments.OrderBy(e => e.Id))
        {
            bool activeThingWarning = ActiveThingCount(environment) > 1;
            List<Linedef> warningLines = environment.BoundaryLinedefs.Where(line => LineHasWarning(environment, line)).ToList();
            int rowWarnings = (activeThingWarning ? environment.Things.Count(thing => !SoundPropagation.ThingDormant(thing, _udmf)) : 0)
                + warningLines.Count;
            _warningCount += rowWarnings;
            if (warningsOnly && rowWarnings == 0) continue;

            yield return new SoundEnvironmentRow(
                Text: $"{environment.Name}  Sectors: {environment.Sectors.Count}, things: {environment.Things.Count}, boundary lines: {environment.BoundaryLinedefs.Count}",
                Depth: 0,
                Warning: rowWarnings > 0,
                Color: environment.Color,
                Selection: new SoundEnvironmentSelection(environment, null, null));

            foreach (Thing thing in environment.Things)
            {
                bool warning = activeThingWarning && !SoundPropagation.ThingDormant(thing, _udmf);
                if (warningsOnly && !warning) continue;
                string dormant = SoundPropagation.ThingDormant(thing, _udmf) ? " (dormant)" : "";
                yield return new SoundEnvironmentRow(
                    Text: $"Thing type {thing.Type}{dormant}",
                    Depth: 1,
                    Warning: warning,
                    Color: environment.Color,
                    Selection: new SoundEnvironmentSelection(environment, thing, null));
            }

            foreach (Linedef line in environment.BoundaryLinedefs)
            {
                bool warning = LineHasWarning(environment, line);
                if (warningsOnly && !warning) continue;
                yield return new SoundEnvironmentRow(
                    Text: "Boundary linedef",
                    Depth: 1,
                    Warning: warning,
                    Color: environment.Color,
                    Selection: new SoundEnvironmentSelection(environment, null, line));
            }
        }
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
        return panel;
    }

    private int ActiveThingCount(SoundEnvironmentInfo environment)
        => environment.Things.Count(thing => !SoundPropagation.ThingDormant(thing, _udmf));

    private static bool LineHasWarning(SoundEnvironmentInfo environment, Linedef line)
    {
        if (line.Front?.Sector == null || line.Back?.Sector == null) return true;
        return environment.Sectors.Contains(line.Front.Sector) && environment.Sectors.Contains(line.Back.Sector);
    }

    private static Color ToColor(uint argb)
        => Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));
}

internal sealed record SoundEnvironmentRow(
    string Text,
    int Depth,
    bool Warning,
    uint Color,
    SoundEnvironmentSelection Selection);
