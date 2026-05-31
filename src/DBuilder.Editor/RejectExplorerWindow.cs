// ABOUTME: Non-modal RejectExplorer surface for REJECT lump validation and sector relation summaries.
// ABOUTME: Presents parsed visibility relationships while leaving map navigation and selection to MainWindow.

using System;
using System.Collections.Generic;
using System.Linq;
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
        root.Children.Add(new TextBlock { Text = FormatValidation(validation), FontWeight = Avalonia.Media.FontWeight.Bold });
        root.Children.Add(new TextBlock { Text = highlightedSector is int h ? $"Highlighted sector: {h}" : "Highlighted sector: none" });

        if (reject != null && reject.HasData)
        {
            var rows = BuildRows(reject, sectorCount, highlightedSector);
            root.Children.Add(new TextBlock { Text = FormatCounts(rows) });
            foreach (RejectExplorerRow row in rows)
                _rows.Items.Add(new ListBoxItem { Content = FormatRow(row), Tag = row });
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

        var close = new Button { Content = "Close" };
        close.Click += OnClose;
        buttons.Children.Add(close);
        root.Children.Add(buttons);

        Content = root;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private static IReadOnlyList<RejectExplorerRow> BuildRows(RejectTable reject, int sectorCount, int? highlightedSector)
    {
        var rows = new List<RejectExplorerRow>(sectorCount);
        for (int i = 0; i < sectorCount; i++)
        {
            RejectExplorerRelation relation = RejectExplorerModel.RelationToHighlight(reject, i, highlightedSector);
            bool fromHighlighted = highlightedSector is int h && RejectExplorerModel.SectorHasLineOfSight(reject, h, i);
            bool toHighlighted = highlightedSector is int h2 && RejectExplorerModel.SectorHasLineOfSight(reject, i, h2);
            rows.Add(new RejectExplorerRow(i, relation, fromHighlighted, toHighlighted));
        }

        return rows;
    }

    private static string FormatValidation(RejectExplorerValidation validation)
        => $"REJECT: {validation.Status} ({validation.ActualBytes} byte(s), expected {validation.ExpectedBytes})";

    private static string FormatCounts(IReadOnlyList<RejectExplorerRow> rows)
    {
        int bidirectional = rows.Count(row => row.Relation == RejectExplorerRelation.Bidirectional);
        int from = rows.Count(row => row.Relation == RejectExplorerRelation.UnidirectionalFrom);
        int to = rows.Count(row => row.Relation == RejectExplorerRelation.UnidirectionalTo);
        int blocked = rows.Count(row => row.Relation == RejectExplorerRelation.Default);
        return $"Relations: {bidirectional} bidirectional, {from} visible from highlighted, {to} visible to highlighted, {blocked} no line of sight or default.";
    }

    private static string FormatRow(RejectExplorerRow row)
        => $"Sector {row.SectorIndex}: {Label(row.Relation)}  from highlighted: {YesNo(row.FromHighlighted)}  to highlighted: {YesNo(row.ToHighlighted)}";

    private static string Label(RejectExplorerRelation relation)
        => relation switch
        {
            RejectExplorerRelation.Highlight => "highlighted",
            RejectExplorerRelation.Bidirectional => "bidirectional",
            RejectExplorerRelation.UnidirectionalFrom => "from highlighted",
            RejectExplorerRelation.UnidirectionalTo => "to highlighted",
            _ => "no line of sight",
        };

    private static string YesNo(bool value) => value ? "yes" : "no";

    private sealed record RejectExplorerRow(
        int SectorIndex,
        RejectExplorerRelation Relation,
        bool FromHighlighted,
        bool ToHighlighted);
}
