// ABOUTME: Applies UDB visual Reset Plane Slope action semantics to selected visual planes.
// ABOUTME: Clears explicit UDMF floor and ceiling slope vectors without changing sector heights.

namespace DBuilder.Map;

public sealed record VisualSlopeResetTarget(Sector Sector, bool Ceiling);

public sealed record VisualSlopeResetResult(int Floors, int Ceilings, string StatusMessage)
{
    public int ChangedSurfaces => Floors + Ceilings;
    public bool Changed => ChangedSurfaces > 0;
}

public static class VisualSlopeReset
{
    public const string EmptySelectionMessage = "You need to select at least one floor or ceiling to reset slope.";

    public static VisualSlopeResetResult Reset(IEnumerable<VisualSlopeResetTarget> targets)
    {
        if (targets == null) throw new ArgumentNullException(nameof(targets));

        int floors = 0;
        int ceilings = 0;
        var seen = new HashSet<(Sector Sector, bool Ceiling)>();

        foreach (VisualSlopeResetTarget target in targets)
        {
            if (!seen.Add((target.Sector, target.Ceiling))) continue;

            if (target.Ceiling)
            {
                target.Sector.CeilSlopeOffset = double.NaN;
                target.Sector.CeilSlope = new Geometry.Vector3D();
                ceilings++;
            }
            else
            {
                target.Sector.FloorSlopeOffset = double.NaN;
                target.Sector.FloorSlope = new Geometry.Vector3D();
                floors++;
            }
        }

        string planeType = "plane";
        if (floors == 0) planeType = "ceiling";
        else if (ceilings == 0) planeType = "floor";

        return new VisualSlopeResetResult(
            floors,
            ceilings,
            $"{floors + ceilings} {planeType} slopes reset.");
    }
}
