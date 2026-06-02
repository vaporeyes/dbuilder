// ABOUTME: Applies UDB-style grid transform actions to selected classic map elements.
// ABOUTME: Keeps grid origin and rotation rules testable outside the editor shell.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed record SmartGridTransformResult(bool Applied, string Message);

public static class SmartGridTransform
{
    public const string ExactlyOneLinedefMessage = "Exactly one linedef must be selected";
    public const string ExactlyOneVertexMessage = "Exactly one vertex must be selected";
    public const string OneOrNoLinedefMessage = "Either nothing or exactly one linedef must be selected";
    public const string OneOrNoThingMessage = "Either nothing or exactly one thing must be selected";
    public const string OneOrNoVertexMessage = "Either nothing or exactly one vertex must be selected";

    public static SmartGridTransformResult AlignToSelectedLinedef(GridSetup grid, IReadOnlyList<Linedef> selected)
    {
        if (selected.Count != 1) return new(false, ExactlyOneLinedefMessage);
        ApplyLinedef(grid, selected[0], selected[0].Start);
        return new(true, "Aligned grid to selected linedef.");
    }

    public static SmartGridTransformResult SetOriginToSelectedVertex(GridSetup grid, IReadOnlyList<Vertex> selected)
    {
        if (selected.Count != 1) return new(false, ExactlyOneVertexMessage);
        ApplyOrigin(grid, selected[0].Position);
        return new(true, "Set grid origin to selected vertex.");
    }

    public static SmartGridTransformResult Reset(GridSetup grid)
    {
        grid.SetGridRotation(0.0);
        grid.SetGridOrigin(0, 0);
        return new(true, "Reset grid transform.");
    }

    public static SmartGridTransformResult SmartFromVertices(
        GridSetup grid,
        IReadOnlyList<Vertex> selected,
        Vertex? highlighted)
    {
        if (selected.Count > 1) return new(false, OneOrNoVertexMessage);
        Vertex? vertex = selected.Count == 1 ? selected[0] : highlighted;
        if (vertex == null) return Reset(grid);

        ApplyOrigin(grid, vertex.Position);
        return new(true, "Set grid origin to vertex.");
    }

    public static SmartGridTransformResult SmartFromLinedefs(
        GridSetup grid,
        IReadOnlyList<Linedef> selected,
        Linedef? highlighted,
        Vector2D cursor)
    {
        if (selected.Count > 1) return new(false, OneOrNoLinedefMessage);
        Linedef? line = selected.Count == 1 ? selected[0] : highlighted;
        if (line == null) return Reset(grid);

        Vertex origin = Vector2D.Distance(line.Start.Position, cursor) <= Vector2D.Distance(line.End.Position, cursor)
            ? line.Start
            : line.End;
        ApplyLinedef(grid, line, origin);
        return new(true, "Aligned grid to linedef.");
    }

    public static SmartGridTransformResult SmartFromSectors(GridSetup grid)
        => Reset(grid);

    public static SmartGridTransformResult SmartFromThings(
        GridSetup grid,
        IReadOnlyList<Thing> selected,
        Thing? highlighted)
    {
        if (selected.Count > 1) return new(false, OneOrNoThingMessage);
        Thing? thing = selected.Count == 1 ? selected[0] : highlighted;
        if (thing == null) return Reset(grid);

        ApplyOrigin(grid, thing.Position);
        return new(true, "Set grid origin to thing.");
    }

    private static void ApplyOrigin(GridSetup grid, Vector2D origin)
        => grid.SetGridOrigin(origin.x, origin.y);

    private static void ApplyLinedef(GridSetup grid, Linedef line, Vertex origin)
    {
        grid.SetGridRotation(line.Angle);
        ApplyOrigin(grid, origin.Position);
    }
}
