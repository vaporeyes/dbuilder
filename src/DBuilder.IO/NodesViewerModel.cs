// ABOUTME: Formats parsed classic node structures for the editor Nodes Viewer rows.
// ABOUTME: Keeps Nodes Viewer text presentation testable outside the Avalonia window.

namespace DBuilder.IO;

public sealed record NodesViewerTabRows(string Title, IReadOnlyList<string> Rows)
{
    public string Header => $"{Title} ({Rows.Count})";
}

public static class NodesViewerModel
{
    public static string StatusText(ClassicNodesStructure structure)
        => structure.IsValid ? "Classic nodes: OK" : $"Classic nodes: {structure.Status}";

    public static string CountsText(ClassicNodesStructure structure)
    {
        string overflow = structure.SegCountExceedsSignedLimit ? " signed seg index limit exceeded" : "";
        return $"{structure.Nodes.Count} node(s), {structure.Segs.Count} seg(s), {structure.Subsectors.Count} subsector(s), {structure.Vertices.Count} vertex record(s).{overflow}";
    }

    public static IReadOnlyList<string> NodeRows(ClassicNodesStructure structure)
    {
        var rows = new List<string>(structure.Nodes.Count);
        for (int i = 0; i < structure.Nodes.Count; i++)
            rows.Add(FormatNode(i, structure.Nodes[i]));
        return rows;
    }

    public static IReadOnlyList<string> SegRows(ClassicNodesStructure structure)
    {
        var rows = new List<string>(structure.Segs.Count);
        for (int i = 0; i < structure.Segs.Count; i++)
            rows.Add(FormatSeg(i, structure.Segs[i]));
        return rows;
    }

    public static IReadOnlyList<string> SubsectorRows(ClassicNodesStructure structure)
    {
        var rows = new List<string>(structure.Subsectors.Count);
        for (int i = 0; i < structure.Subsectors.Count; i++)
            rows.Add(FormatSubsector(i, structure.Subsectors[i]));
        return rows;
    }

    public static IReadOnlyList<string> VertexRows(ClassicNodesStructure structure)
    {
        var rows = new List<string>(structure.Vertices.Count);
        for (int i = 0; i < structure.Vertices.Count; i++)
            rows.Add(FormatVertex(i, structure.Vertices[i]));
        return rows;
    }

    public static IReadOnlyList<NodesViewerTabRows> TabRows(ClassicNodesStructure structure)
    {
        if (!structure.IsValid) return Array.Empty<NodesViewerTabRows>();

        return new[]
        {
            new NodesViewerTabRows("Nodes", NodeRows(structure)),
            new NodesViewerTabRows("Segs", SegRows(structure)),
            new NodesViewerTabRows("Subsectors", SubsectorRows(structure)),
            new NodesViewerTabRows("Vertices", VertexRows(structure)),
        };
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

    private static string FormatVertex(int index, ClassicNodeVertex vertex)
        => $"#{index}: ({vertex.X}, {vertex.Y})";
}
