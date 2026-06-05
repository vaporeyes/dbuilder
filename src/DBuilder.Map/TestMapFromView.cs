// ABOUTME: Builds temporary Test Map copies with Player 1 start moved to the current editor view.
// ABOUTME: Mirrors UDB classic and visual test-from-current-position validation without dirtying the source map.

using DBuilder.Geometry;

namespace DBuilder.Map;

public sealed record TestMapFromViewPlacement(
    Vector2D Position,
    double Height,
    double? AngleRadians,
    bool VisualMode);

public sealed record TestMapFromViewResult(bool Success, MapSet? Map, string Message);

public static class TestMapFromView
{
    public const int PlayerStartType = 1;
    public const int DefaultPlayerHeight = 56;
    public const int VisualPlayerHeight = 41;

    public static TestMapFromViewResult Prepare(
        MapSet source,
        TestMapFromViewPlacement placement,
        bool usesHubPlayerStartArgs,
        int playerHeight = DefaultPlayerHeight)
    {
        Sector? sector = source.GetSectorAt(placement.Position);
        if (sector is null)
        {
            string target = placement.VisualMode ? "cursor is not inside sector" : "mouse cursor must be inside a sector";
            return new TestMapFromViewResult(false, null, "Can't test from current position: " + target + "!");
        }

        int requiredHeight = placement.VisualMode ? VisualPlayerHeight : playerHeight;
        if (sector.CeilHeight - sector.FloorHeight < requiredHeight)
            return new TestMapFromViewResult(false, null, "Can't test from current position: sector is too low!");

        if (placement.VisualMode && FindVisualPlayerStart(source) is null)
            return new TestMapFromViewResult(false, null, "Can't test from current position: no Player 1 start found!");

        MapSet map = source.Clone();
        map.BuildIndexes();
        Sector clonedSector = map.Sectors[source.Sectors.IndexOf(sector)];
        Thing start = placement.VisualMode
            ? FindVisualPlayerStart(map)!
            : FindPlayerStart(map, usesHubPlayerStartArgs) ?? map.AddThing(placement.Position, PlayerStartType);

        double z = placement.VisualMode
            ? Math.Clamp(placement.Height - clonedSector.FloorHeight, 0, clonedSector.CeilHeight - clonedSector.FloorHeight - VisualPlayerHeight)
            : clonedSector.FloorHeight;

        start.Move(placement.Position.x, placement.Position.y, z);
        if (placement.AngleRadians is { } angle)
            start.Rotate(angle - Angle2D.PI);

        map.BuildIndexes();
        return new TestMapFromViewResult(true, map, "");
    }

    private static Thing? FindPlayerStart(MapSet map, bool usesHubPlayerStartArgs)
        => map.Things
            .Select((thing, index) => (thing, index))
            .Where(pair => pair.thing.Type == PlayerStartType)
            .Where(pair => !usesHubPlayerStartArgs || pair.thing.Args[0] == 0)
            .OrderByDescending(pair => pair.index)
            .Select(pair => pair.thing)
            .FirstOrDefault();

    private static Thing? FindVisualPlayerStart(MapSet map)
        => map.Things.FirstOrDefault(thing => thing.Type == PlayerStartType);
}
