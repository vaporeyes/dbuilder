// ABOUTME: Applies UDB visual-mode raise/lower-to-nearest height rules to editable 3D hits.
// ABOUTME: Keeps adjacent-sector height selection testable outside the Avalonia editor surface.

using DBuilder.Map;

namespace DBuilder.IO;

public sealed record VisualNearestHeightResult(int ChangedSurfaces, string Message);

public static class VisualNearestHeight
{
    public const string NoSuitableObjectsMessage = "No suitable objects found!";
    public const string LowestCeilingBelowHighestFloorMessage = "Can't do: lowest ceiling is lower than highest floor!";

    public static VisualNearestHeightResult Apply(IEnumerable<VisualHit> hits, bool raise, bool withinSelection, bool hasThingHeight = true)
    {
        var floors = new Dictionary<Sector, Sector>(ReferenceEqualityComparer.Instance);
        var ceilings = new Dictionary<Sector, Sector>(ReferenceEqualityComparer.Instance);
        var things = new List<Thing>();

        foreach (VisualHit hit in hits)
        {
            if (hit.Kind == VisualHitKind.Floor && hit.Sector is { } floor)
                floors[floor] = floor;
            else if (hit.Kind == VisualHitKind.Ceiling && hit.Sector is { } ceiling)
                ceilings[ceiling] = ceiling;
            else if (hit.Kind == VisualHitKind.Thing && hit.Thing is { } thing)
                things.Add(thing);
        }

        if (floors.Count + ceilings.Count == 0 && (things.Count == 0 || !hasThingHeight))
            return new VisualNearestHeightResult(0, NoSuitableObjectsMessage);

        if (withinSelection)
        {
            string required = string.Empty;
            if (floors.Count == 1) required = "floors";
            if (ceilings.Count == 1)
            {
                if (!string.IsNullOrEmpty(required)) required += " and ";
                required += "ceilings";
            }

            if (!string.IsNullOrEmpty(required))
                return new VisualNearestHeightResult(0, $"Can't do: at least 2 selected {required} are required!");

            if (raise && floors.Count > 0 && floors.Keys.Min(sector => sector.CeilHeight) < floors.Keys.Max(sector => sector.FloorHeight))
                return new VisualNearestHeightResult(0, LowestCeilingBelowHighestFloorMessage);

            if (!raise && ceilings.Count > 0 && ceilings.Keys.Min(sector => sector.CeilHeight) < ceilings.Keys.Max(sector => sector.FloorHeight))
                return new VisualNearestHeightResult(0, LowestCeilingBelowHighestFloorMessage);
        }

        if (!withinSelection)
        {
            string failed = AlignFailureDescription(floors.Keys, ceilings.Keys, raise);
            if (!string.IsNullOrEmpty(failed))
                return new VisualNearestHeightResult(0, $"Unable to align selected {failed}!");
        }

        int changed = 0;
        changed += raise
            ? RaiseFloors(floors.Keys, withinSelection)
            : LowerFloors(floors.Keys, withinSelection);
        changed += raise
            ? RaiseCeilings(ceilings.Keys, withinSelection)
            : LowerCeilings(ceilings.Keys, withinSelection);
        if (hasThingHeight) changed += AlignThings(things, raise);

        string verb = raise ? "raised" : "lowered";
        return new VisualNearestHeightResult(changed, $"{verb} {changed} object{(changed == 1 ? "" : "s")} to nearest height");
    }

    private static string AlignFailureDescription(IEnumerable<Sector> floors, IEnumerable<Sector> ceilings, bool raise)
    {
        var floorList = new HashSet<Sector>(floors, ReferenceEqualityComparer.Instance).ToList();
        var ceilingList = new HashSet<Sector>(ceilings, ReferenceEqualityComparer.Instance).ToList();
        string failed = string.Empty;

        if (floorList.Count > 0)
        {
            bool hasTarget = raise
                ? TryGetRaiseFloorTarget(floorList, withinSelection: false, out _)
                : TryGetLowerFloorTarget(floorList, withinSelection: false, out _);
            if (!hasTarget) failed = floorList.Count > 1 ? "floors" : "floor";
        }

        if (ceilingList.Count > 0)
        {
            bool hasTarget = raise
                ? TryGetRaiseCeilingTarget(ceilingList, withinSelection: false, out _)
                : TryGetLowerCeilingTarget(ceilingList, withinSelection: false, out _);
            if (!hasTarget)
            {
                if (!string.IsNullOrEmpty(failed)) failed += " and ";
                failed += ceilingList.Count > 1 ? "ceilings" : "ceiling";
            }
        }

        return failed;
    }

    private static int RaiseFloors(IEnumerable<Sector> sectors, bool withinSelection)
    {
        var selected = new HashSet<Sector>(sectors, ReferenceEqualityComparer.Instance).ToList();
        if (selected.Count == 0) return 0;

        if (!TryGetRaiseFloorTarget(selected, withinSelection, out int target)) return 0;
        int changed = 0;
        foreach (Sector sector in selected)
        {
            if (sector.FloorHeight == target) continue;
            sector.FloorHeight = target;
            changed++;
        }

        return changed;
    }

    private static bool TryGetRaiseFloorTarget(IReadOnlyCollection<Sector> selected, bool withinSelection, out int target)
    {
        int highestFloor = selected.Max(sector => sector.FloorHeight);
        int lowestCeiling = selected.Min(sector => sector.CeilHeight);
        target = withinSelection
            ? highestFloor
            : AdjacentHeights(selected)
                .Where(height => height > highestFloor && height <= lowestCeiling)
                .DefaultIfEmpty(lowestCeiling > highestFloor ? lowestCeiling : int.MaxValue)
                .Min();

        return target != int.MaxValue;
    }

    private static int LowerFloors(IEnumerable<Sector> sectors, bool withinSelection)
    {
        var selected = new HashSet<Sector>(sectors, ReferenceEqualityComparer.Instance).ToList();
        if (selected.Count == 0) return 0;

        if (!TryGetLowerFloorTarget(selected, withinSelection, out int target)) return 0;
        int changed = 0;
        foreach (Sector sector in selected)
        {
            if (sector.FloorHeight == target) continue;
            sector.FloorHeight = target;
            changed++;
        }

        return changed;
    }

    private static bool TryGetLowerFloorTarget(IReadOnlyCollection<Sector> selected, bool withinSelection, out int target)
    {
        int lowestFloor = selected.Min(sector => sector.FloorHeight);
        target = withinSelection
            ? lowestFloor
            : AdjacentHeights(selected)
                .Where(height => height < lowestFloor)
                .DefaultIfEmpty(int.MinValue)
                .Max();

        return target != int.MinValue;
    }

    private static int RaiseCeilings(IEnumerable<Sector> sectors, bool withinSelection)
    {
        var selected = new HashSet<Sector>(sectors, ReferenceEqualityComparer.Instance).ToList();
        if (selected.Count == 0) return 0;

        if (!TryGetRaiseCeilingTarget(selected, withinSelection, out int target)) return 0;
        int changed = 0;
        foreach (Sector sector in selected)
        {
            if (sector.CeilHeight == target) continue;
            sector.CeilHeight = target;
            changed++;
        }

        return changed;
    }

    private static bool TryGetRaiseCeilingTarget(IReadOnlyCollection<Sector> selected, bool withinSelection, out int target)
    {
        int highestCeiling = selected.Max(sector => sector.CeilHeight);
        target = withinSelection
            ? highestCeiling
            : AdjacentHeights(selected)
                .Where(height => height > highestCeiling)
                .DefaultIfEmpty(int.MaxValue)
                .Min();

        return target != int.MaxValue;
    }

    private static int LowerCeilings(IEnumerable<Sector> sectors, bool withinSelection)
    {
        var selected = new HashSet<Sector>(sectors, ReferenceEqualityComparer.Instance).ToList();
        if (selected.Count == 0) return 0;

        if (!TryGetLowerCeilingTarget(selected, withinSelection, out int target)) return 0;
        int changed = 0;
        foreach (Sector sector in selected)
        {
            if (sector.CeilHeight == target) continue;
            sector.CeilHeight = target;
            changed++;
        }

        return changed;
    }

    private static bool TryGetLowerCeilingTarget(IReadOnlyCollection<Sector> selected, bool withinSelection, out int target)
    {
        int lowestCeiling = selected.Min(sector => sector.CeilHeight);
        int highestFloor = selected.Max(sector => sector.FloorHeight);
        target = withinSelection
            ? lowestCeiling
            : AdjacentHeights(selected)
                .Where(height => height < lowestCeiling && height >= highestFloor)
                .DefaultIfEmpty(highestFloor < lowestCeiling ? highestFloor : int.MinValue)
                .Max();

        return target != int.MinValue;
    }

    private static int AlignThings(IEnumerable<Thing> things, bool raise)
    {
        int changed = 0;
        foreach (Thing thing in things)
        {
            Sector? sector = thing.Sector;
            if (sector == null) continue;
            double target = raise ? sector.CeilHeight - sector.FloorHeight : 0.0;
            if (Math.Abs(thing.Height - target) < 0.001) continue;
            thing.Height = target;
            changed++;
        }

        return changed;
    }

    private static IEnumerable<int> AdjacentHeights(IEnumerable<Sector> sectors)
    {
        var selected = new HashSet<Sector>(sectors, ReferenceEqualityComparer.Instance);
        var seen = new HashSet<Sector>(ReferenceEqualityComparer.Instance);

        foreach (Sector sector in selected)
        {
            foreach (Sidedef side in sector.Sidedefs)
            {
                Sector? other = side.Other?.Sector;
                if (other == null || selected.Contains(other) || !seen.Add(other)) continue;
                yield return other.FloorHeight;
                yield return other.CeilHeight;
            }
        }
    }
}
