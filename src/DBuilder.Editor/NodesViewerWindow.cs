// ABOUTME: Non-modal NodesViewer surface for classic Doom NODES, SEGS, SSECTORS, and VERTEXES data.
// ABOUTME: Presents parsed BSP structures from NodesReader without owning map rendering or overlay state.

using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class NodesViewerWindow : Window
{
    private readonly ListBox _nodes = new();
    private readonly ListBox _segs = new();
    private readonly ListBox _subsectors = new();

    public NodesViewerWindow(ClassicNodesStructure structure)
    {
        Title = "Nodes Viewer";
        Width = 760;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel { Margin = new Avalonia.Thickness(10) };
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock { Text = FormatStatus(structure), FontWeight = Avalonia.Media.FontWeight.Bold });
        if (structure.IsValid)
            header.Children.Add(new TextBlock { Text = FormatCounts(structure) });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var tabs = new TabControl();
        tabs.Items.Add(Tab("Nodes", _nodes));
        tabs.Items.Add(Tab("Segs", _segs));
        tabs.Items.Add(Tab("Subsectors", _subsectors));
        root.Children.Add(tabs);

        FillRows(structure);
        Content = root;
    }

    private static TabItem Tab(string header, ListBox list)
        => new() { Header = header, Content = new ScrollViewer { Content = list } };

    private void FillRows(ClassicNodesStructure structure)
    {
        if (!structure.IsValid) return;

        for (int i = 0; i < structure.Nodes.Count; i++)
            _nodes.Items.Add(FormatNode(i, structure.Nodes[i]));

        for (int i = 0; i < structure.Segs.Count; i++)
            _segs.Items.Add(FormatSeg(i, structure.Segs[i]));

        for (int i = 0; i < structure.Subsectors.Count; i++)
            _subsectors.Items.Add(FormatSubsector(i, structure.Subsectors[i]));
    }

    private static string FormatStatus(ClassicNodesStructure structure)
        => structure.IsValid ? "Classic nodes: OK" : $"Classic nodes: {structure.Status}";

    private static string FormatCounts(ClassicNodesStructure structure)
    {
        string overflow = structure.SegCountExceedsSignedLimit ? " signed seg index limit exceeded" : "";
        return $"{structure.Nodes.Count} node(s), {structure.Segs.Count} seg(s), {structure.Subsectors.Count} subsector(s), {structure.Vertices.Count} vertex record(s).{overflow}";
    }

    private static string FormatNode(int index, ClassicNode node)
    {
        string right = node.RightChildIsSubsector ? $"subsector {node.RightChildIndex}" : $"node {node.RightChildIndex}";
        string left = node.LeftChildIsSubsector ? $"subsector {node.LeftChildIndex}" : $"node {node.LeftChildIndex}";
        return $"#{index}: ({node.X}, {node.Y}) -> ({node.X + node.Dx}, {node.Y + node.Dy})  parent {node.ParentIndex}  right {right}  left {left}";
    }

    private static string FormatSeg(int index, ClassicSeg seg)
        => $"#{index}: v{seg.StartVertex} -> v{seg.EndVertex}  line {seg.LineIndex}  side {(seg.LeftSide ? "left" : "right")}  offset {seg.Offset}  subsector {seg.SubsectorIndex}";

    private static string FormatSubsector(int index, ClassicSubsector subsector)
        => $"#{index}: {subsector.SegCount} seg(s), first seg {subsector.FirstSeg}";
}
