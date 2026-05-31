// ABOUTME: Non-modal BlockmapExplorer surface for parsed Doom BLOCKMAP lump data.
// ABOUTME: Shows UDB-style blockmap totals, diagnostics, questionable offsets, and block-list rows.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class BlockmapExplorerWindow : Window
{
    private const int MaxRows = 5000;

    private readonly ListBox _rows = new();
    private readonly CheckBox _questionableOnly = new() { Content = "Questionable offsets only" };
    private readonly CheckBox _highlightSharedBlocks = new() { Content = "Highlight shared block lists" };
    private readonly CheckBox _showQuestionableOverlay = new() { Content = "Overlay questionable offsets" };
    private readonly TextBlock _header = new();
    private readonly BlockmapLumpData _blockmap;
    private readonly int _linedefCount;

    public event Action<int, int>? BlockActivated;
    public event Action<int?, int?, bool, bool>? OverlayChanged;

    public BlockmapExplorerWindow(BlockmapLumpData blockmap, int linedefCount)
    {
        _blockmap = blockmap;
        _linedefCount = linedefCount;

        Title = "Blockmap Explorer";
        Width = 760;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel { Margin = new Thickness(10) };
        var top = new StackPanel { Spacing = 5 };
        top.Children.Add(new TextBlock { Text = FormatStatus(blockmap), FontWeight = FontWeight.Bold });
        top.Children.Add(new TextBlock { Text = FormatInfo(blockmap, linedefCount), TextWrapping = TextWrapping.Wrap });

        foreach (BlockmapLumpDiagnostic diagnostic in blockmap.Diagnostics.Take(6))
            top.Children.Add(new TextBlock { Text = diagnostic.Message, Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap });
        if (blockmap.Diagnostics.Count > 6)
            top.Children.Add(new TextBlock { Text = $"{blockmap.Diagnostics.Count - 6} more diagnostic(s).", Foreground = Brushes.OrangeRed });

        _questionableOnly.IsVisible = blockmap.GetQuestionableOffsetCount() > 0;
        _questionableOnly.IsCheckedChanged += (_, _) => RefreshRows();
        top.Children.Add(_questionableOnly);
        _highlightSharedBlocks.IsCheckedChanged += (_, _) => EmitOverlayChanged();
        top.Children.Add(_highlightSharedBlocks);
        _showQuestionableOverlay.IsVisible = blockmap.GetQuestionableOffsetCount() > 0;
        _showQuestionableOverlay.IsCheckedChanged += (_, _) => EmitOverlayChanged();
        top.Children.Add(_showQuestionableOverlay);
        top.Children.Add(_header);

        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        _rows.DoubleTapped += (_, _) =>
        {
            if (_rows.SelectedItem is ListBoxItem { Tag: BlockmapRow row })
                BlockActivated?.Invoke(row.Column, row.Row);
        };
        _rows.SelectionChanged += (_, _) => EmitOverlayChanged();
        root.Children.Add(new ScrollViewer { Content = _rows });
        Content = root;

        RefreshRows();
    }

    private void EmitOverlayChanged()
    {
        if (_rows.SelectedItem is ListBoxItem { Tag: BlockmapRow row })
        {
            OverlayChanged?.Invoke(
                row.Column,
                row.Row,
                _highlightSharedBlocks.IsChecked == true,
                _showQuestionableOverlay.IsChecked == true);
            return;
        }

        OverlayChanged?.Invoke(
            null,
            null,
            _highlightSharedBlocks.IsChecked == true,
            _showQuestionableOverlay.IsChecked == true);
    }

    private void RefreshRows()
    {
        var rows = BuildRows().ToList();
        int shown = Math.Min(MaxRows, rows.Count);
        _header.Text = rows.Count == 0
            ? "No block rows to display."
            : $"Showing {shown} of {rows.Count} block row(s). Double-click a row to center that block.";

        var items = new List<ListBoxItem>(shown);
        foreach (BlockmapRow row in rows.Take(MaxRows))
        {
            items.Add(new ListBoxItem
            {
                Content = new TextBlock { Text = FormatRow(row), TextWrapping = TextWrapping.Wrap },
                Tag = row,
            });
        }
        _rows.ItemsSource = items;
    }

    private IEnumerable<BlockmapRow> BuildRows()
    {
        bool questionableOnly = _questionableOnly.IsChecked == true;
        for (int row = 0; row < _blockmap.Rows; row++)
        {
            for (int column = 0; column < _blockmap.Columns; column++)
            {
                int offset = _blockmap.GetBlockListOffset(column, row);
                bool questionable = offset >= 0 && offset < BlockmapLump.HeaderSize + _blockmap.Columns * _blockmap.Rows * sizeof(ushort);
                if (questionableOnly && !questionable) continue;
                IReadOnlyList<int> lines = _blockmap.GetLinesInBlock(column, row);
                bool isSublist = offset >= 0 && _blockmap.Blocks.TryGetValue(offset, out BlockmapBlockList? block) && block.IsSublist;
                int sharedCount = _blockmap.GetSharedBlocks(column, row).Count;
                yield return new BlockmapRow(column, row, offset, lines.Count, isSublist, sharedCount, questionable);
            }
        }
    }

    private static string FormatStatus(BlockmapLumpData blockmap)
        => $"BLOCKMAP: {blockmap.Status} ({blockmap.LumpSize} byte(s))";

    private static string FormatInfo(BlockmapLumpData blockmap, int linedefCount)
    {
        int totalBlocks = blockmap.Columns * blockmap.Rows;
        int offsetListEnd = BlockmapLump.HeaderSize + totalBlocks * sizeof(ushort);
        return $"Origin ({blockmap.OriginX}, {blockmap.OriginY}), {blockmap.Columns} columns x {blockmap.Rows} rows, {totalBlocks} total block(s), {blockmap.Blocks.Count} unique block list(s), offset list ends at byte {offsetListEnd}, {blockmap.GetQuestionableOffsetCount()} questionable offset(s), {blockmap.CountLinesNotInBlocks(linedefCount)} linedef(s) not in blocks.";
    }

    private static string FormatRow(BlockmapRow row)
    {
        string questionable = row.Questionable ? " questionable" : "";
        string sublist = row.IsSublist ? " sublist" : "";
        return $"({row.Column}, {row.Row}) offset {row.Offset}: {row.LineCount} line(s), shared by {row.SharedCount} block(s){sublist}{questionable}";
    }

    private sealed record BlockmapRow(
        int Column,
        int Row,
        int Offset,
        int LineCount,
        bool IsSublist,
        int SharedCount,
        bool Questionable);
}
