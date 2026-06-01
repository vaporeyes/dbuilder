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
}
