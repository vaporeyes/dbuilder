// ABOUTME: Applies UDB visual Toggle Slope action semantics to selected visual surfaces.
// ABOUTME: Mutates Plane_Align linedef action args for lower, upper, floor, and ceiling targets.

namespace DBuilder.Map;

public sealed record VisualSlopeToggleTarget(
    Sidedef? Sidedef = null,
    Sector? Sector = null,
    SidedefPart Part = SidedefPart.None,
    bool Floor = false,
    bool Ceiling = false);

public sealed record VisualSlopeToggleResult(int ChangedSurfaces, string StatusMessage)
{
    public bool Changed => ChangedSurfaces > 0;
}

public static class VisualSlopeToggle
{
    public const int PlaneAlignAction = 181;
    public const string EmptySelectionMessage = "Toggle Slope action requires selected surfaces!";

    public static VisualSlopeToggleResult Toggle(IEnumerable<VisualSlopeToggleTarget> targets)
    {
        if (targets == null) throw new ArgumentNullException(nameof(targets));

        VisualSlopeToggleTarget[] all = targets.ToArray();
        if (all.Length == 0)
            return new VisualSlopeToggleResult(0, EmptySelectionMessage);

        int changed = 0;
        foreach (VisualSlopeToggleTarget target in all)
        {
            bool targetChanged = target.Part switch
            {
                SidedefPart.Lower when target.Sidedef != null => SetWallAlignment(target.Sidedef, floor: true),
                SidedefPart.Upper when target.Sidedef != null => SetWallAlignment(target.Sidedef, floor: false),
                _ when target.Floor && target.Sector != null => ClearSectorAlignment(target.Sector, floor: true),
                _ when target.Ceiling && target.Sector != null => ClearSectorAlignment(target.Sector, floor: false),
                _ => false,
            };
            if (targetChanged) changed++;
        }

        return new VisualSlopeToggleResult(
            changed,
            $"Toggled Slope for {changed} surface{(changed == 1 ? "." : "s.")}");
    }

    private static bool SetWallAlignment(Sidedef side, bool floor)
    {
        int argIndex = floor ? 0 : 1;
        Linedef line = side.Line;
        if (line.Action != 0 && (line.Action != PlaneAlignAction || line.Args[argIndex] != 0))
            return false;

        if (side.Sector != null)
            ClearExistingSectorAlignment(side.Sector, side, floor);

        line.Action = PlaneAlignAction;
        line.Args[argIndex] = SideArg(side);
        return true;
    }

    private static void ClearExistingSectorAlignment(Sector sector, Sidedef except, bool floor)
    {
        int argIndex = floor ? 0 : 1;
        foreach (Sidedef side in sector.Sidedefs)
        {
            if (ReferenceEquals(side, except)) continue;
            Linedef line = side.Line;
            if (line.Action != PlaneAlignAction || line.Args[argIndex] != SideArg(side)) continue;
            ClearLineAlignment(line, floor);
        }
    }

    private static bool ClearSectorAlignment(Sector sector, bool floor)
    {
        bool changed = false;
        int argIndex = floor ? 0 : 1;
        foreach (Sidedef side in sector.Sidedefs)
        {
            Linedef line = side.Line;
            if (line.Action != PlaneAlignAction || line.Args[argIndex] != SideArg(side)) continue;
            ClearLineAlignment(line, floor);
            changed = true;
        }

        return changed;
    }

    private static void ClearLineAlignment(Linedef line, bool floor)
    {
        int argIndex = floor ? 0 : 1;
        int otherArgIndex = floor ? 1 : 0;
        if (line.Args[otherArgIndex] == 0)
            line.Action = 0;
        else
            line.Args[argIndex] = 0;
    }

    private static int SideArg(Sidedef side) => side.IsFront ? 1 : 2;
}
