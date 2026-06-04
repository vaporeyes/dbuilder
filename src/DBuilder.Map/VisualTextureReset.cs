// ABOUTME: Resets visual texture and thing transform fields using UDB's target-specific field sets.
// ABOUTME: Gives editor dispatch a small model helper for selected or highlighted visual targets.

namespace DBuilder.Map;

public static class VisualTextureReset
{
    public static bool ResetSidedefOffsets(Sidedef side)
    {
        bool changed = side.OffsetX != 0 || side.OffsetY != 0;
        side.OffsetX = 0;
        side.OffsetY = 0;
        return changed;
    }

    public static bool ResetLocalSidedef(Sidedef side, SidedefPart part)
    {
        if (part == SidedefPart.None) return false;
        string name = PartName(part);
        return RemoveFields(side, new[]
        {
            "offsetx_" + name,
            "offsety_" + name,
            "scalex_" + name,
            "scaley_" + name,
            "light",
            "lightabsolute",
        });
    }

    public static bool ResetSidedefForCommand(Sidedef side, SidedefPart part, bool local, bool isUdmf)
        => local && isUdmf ? ResetLocalSidedef(side, part) : ResetSidedefOffsets(side);

    public static bool ResetSectorFlat(Sector sector, bool ceiling, bool local)
        => RemoveFields(sector, local ? LocalFlatFields(ceiling) : FlatOffsetFields(ceiling));

    public static bool ResetThing(Thing thing, bool local)
    {
        bool changed = thing.ScaleX != 1.0 || thing.ScaleY != 1.0;
        thing.SetScale(1.0, 1.0);

        if (local)
        {
            changed |= thing.Pitch != 0 || thing.Roll != 0;
            thing.SetPitch(0);
            thing.SetRoll(0);
        }

        return changed;
    }

    private static bool RemoveFields(IFielded element, IEnumerable<string> fields)
    {
        bool changed = false;
        foreach (string field in fields)
            changed |= element.RemoveField(field);
        return changed;
    }

    private static IEnumerable<string> FlatOffsetFields(bool ceiling)
    {
        yield return ceiling ? "xpanningceiling" : "xpanningfloor";
        yield return ceiling ? "ypanningceiling" : "ypanningfloor";
    }

    private static IEnumerable<string> LocalFlatFields(bool ceiling)
    {
        foreach (string field in FlatOffsetFields(ceiling)) yield return field;
        yield return ceiling ? "xscaleceiling" : "xscalefloor";
        yield return ceiling ? "yscaleceiling" : "yscalefloor";
        yield return ceiling ? "rotationceiling" : "rotationfloor";
        yield return ceiling ? "lightceiling" : "lightfloor";
        yield return ceiling ? "lightceilingabsolute" : "lightfloorabsolute";
    }

    private static string PartName(SidedefPart part) => part switch
    {
        SidedefPart.Upper => "top",
        SidedefPart.Middle => "mid",
        SidedefPart.Lower => "bottom",
        _ => "mid",
    };
}
