// ABOUTME: Applies UDB-style fixed-step floor and ceiling height changes to sectors.
// ABOUTME: Keeps sector height action messages and undo labels testable outside the editor.

namespace DBuilder.Map;

public enum SectorHeightPart
{
    Floor,
    Ceiling,
}

public readonly record struct SectorHeightAdjustmentResult(int ChangedCount, string UndoDescription, string StatusMessage);

public static class SectorHeightAdjustment
{
    public static string UndoDescription(SectorHeightPart part)
        => part == SectorHeightPart.Floor ? "Floor heights change" : "Ceiling heights change";

    public static SectorHeightAdjustmentResult Apply(IReadOnlyList<Sector> sectors, SectorHeightPart part, int delta)
    {
        foreach (Sector sector in sectors)
        {
            if (part == SectorHeightPart.Floor)
                sector.FloorHeight += delta;
            else
                sector.CeilHeight += delta;
        }

        string surface = part == SectorHeightPart.Floor ? "floor" : "ceiling";
        string verb = delta < 0 ? "Lowered" : "Raised";
        int amount = Math.Abs(delta);
        return new SectorHeightAdjustmentResult(
            sectors.Count,
            UndoDescription(part),
            verb + " " + surface + " heights by " + amount + "mp.");
    }
}
