// ABOUTME: Aligns selected UDB model, wall-sprite, and flat-sprite things to their nearest usable linedef.
// ABOUTME: Keeps the ThingsMode align-to-wall workflow testable outside the Avalonia editor shell.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed record ThingWallAlignmentResult(
    int SelectedCount,
    int EligibleCount,
    int AlignedCount,
    int FailedCount,
    IReadOnlyList<int> FailedThingIndexes,
    string Message);

public static class ThingWallAlignment
{
    public static ThingWallAlignmentResult AlignSelectedToNearestWalls(
        MapSet map,
        GameConfiguration? config,
        int vertexDecimals = 3,
        bool usePrecisePosition = true,
        bool preserveHeight = true)
    {
        var selected = map.GetSelectedThings();
        return AlignThingsToNearestWalls(map, config, selected, vertexDecimals, usePrecisePosition, preserveHeight);
    }

    public static ThingWallAlignmentResult AlignThingsToNearestWalls(
        MapSet map,
        GameConfiguration? config,
        IReadOnlyList<Thing> things,
        int vertexDecimals = 3,
        bool usePrecisePosition = true,
        bool preserveHeight = true)
    {
        if (things.Count == 0)
            return new ThingWallAlignmentResult(0, 0, 0, 0, Array.Empty<int>(), "This action requires a selection!");

        map.BuildIndexes();

        var failures = new List<int>();
        int eligible = 0;
        int aligned = 0;

        foreach (var thing in things)
        {
            ThingTypeInfo? info = config?.GetThing(thing.Type);
            if (info == null || !IsAlignable(info.RenderMode)) continue;

            eligible++;
            if (thing.Sector == null) thing.DetermineSector(map);
            if (TryAlignThing(map, thing, info, vertexDecimals, usePrecisePosition, preserveHeight))
            {
                aligned++;
                continue;
            }

            failures.Add(map.Things.IndexOf(thing));
        }

        if (eligible == 0)
        {
            return new ThingWallAlignmentResult(
                things.Count,
                eligible,
                aligned,
                failures.Count,
                failures,
                "This action only works for models or things with FLATSPRITE/WALLSPRITE flags!");
        }

        return new ThingWallAlignmentResult(things.Count, eligible, aligned, failures.Count, failures, Message(aligned, failures.Count));
    }

    public static bool IsAlignable(ThingRenderMode renderMode)
        => renderMode is ThingRenderMode.Model or ThingRenderMode.WallSprite or ThingRenderMode.FlatSprite;

    private static bool TryAlignThing(
        MapSet map,
        Thing thing,
        ThingTypeInfo info,
        int vertexDecimals,
        bool usePrecisePosition,
        bool preserveHeight)
    {
        var excluded = new HashSet<Linedef>();
        while (excluded.Count < map.Linedefs.Count)
        {
            Linedef? line = NearestLinedef(map.Linedefs, thing.Position, excluded);
            if (line == null) return false;

            if (ThingGeometryTools.TryAlignThingToLine(
                    thing,
                    line,
                    info,
                    map,
                    vertexDecimals,
                    usePrecisePosition,
                    preserveHeight))
                return true;

            excluded.Add(line);
        }

        return false;
    }

    private static Linedef? NearestLinedef(IReadOnlyList<Linedef> lines, Vector2D position, HashSet<Linedef> excluded)
    {
        Linedef? closest = null;
        double bestSq = double.MaxValue;

        foreach (var line in lines)
        {
            if (excluded.Contains(line)) continue;

            double distanceSq = line.SafeDistanceToSq(position, bounded: true);
            if (distanceSq < bestSq)
            {
                bestSq = distanceSq;
                closest = line;
            }
        }

        return closest;
    }

    private static string Message(int aligned, int failed)
    {
        if (failed > 0)
            return $"Aligned {aligned} thing{(aligned == 1 ? "" : "s")}; {failed} could not be aligned.";

        return aligned == 1 ? "Aligned a thing." : $"Aligned {aligned} things.";
    }
}
