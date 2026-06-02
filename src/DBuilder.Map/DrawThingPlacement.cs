// ABOUTME: Provides UDB-style thing placement helpers for draw modes that place things at vertices.
// ABOUTME: Deduplicates generated draw positions and selects the created things for follow-up editing.

using DBuilder.Geometry;

namespace DBuilder.Map;

public static class DrawThingPlacement
{
    public static IReadOnlyList<Vector2D> UniquePositions(IEnumerable<Vector2D> positions)
    {
        var result = new List<Vector2D>();
        foreach (Vector2D position in positions)
        {
            if (!result.Contains(position)) result.Add(position);
        }

        return result;
    }

    public static int PlaceAtPositions(MapSet map, IEnumerable<Vector2D> positions, int thingType, bool clearSelection = true)
    {
        IReadOnlyList<Vector2D> unique = UniquePositions(positions);
        if (unique.Count == 0) return 0;

        if (clearSelection) map.ClearAllSelected();

        int count = 0;
        foreach (Vector2D position in unique)
        {
            Thing thing = map.AddThing(position, thingType);
            thing.Selected = true;
            count++;
        }

        return count;
    }

    public static IReadOnlyList<Vector2D> PositionsFromVertices(IEnumerable<Vertex> vertices)
        => UniquePositions(vertices.Select(vertex => vertex.Position));

    public static IReadOnlyList<Vector2D> PositionsFromLinedefs(IEnumerable<Linedef> linedefs)
    {
        var positions = new List<Vector2D>();
        foreach (Linedef linedef in linedefs)
        {
            positions.Add(linedef.Start.Position);
            positions.Add(linedef.End.Position);
        }

        return UniquePositions(positions);
    }

    public static IReadOnlyList<Vector2D> PositionsFromSectors(IEnumerable<Sector> sectors)
    {
        var positions = new List<Vector2D>();
        foreach (Sector sector in sectors)
        {
            List<LabelPositionInfo> labels = Tools.FindLabelPositions(sector);
            if (labels.Count > 0)
            {
                positions.Add(labels[0].position);
                continue;
            }

            positions.Add(new Vector2D(
                sector.BBox.X + sector.BBox.Width * 0.5,
                sector.BBox.Y + sector.BBox.Height * 0.5));
        }

        return UniquePositions(positions);
    }
}
