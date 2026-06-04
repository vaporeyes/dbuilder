// ABOUTME: Copies and pastes visual sidedef texture offsets using UDB's local-offset rules.
// ABOUTME: Chooses global sidedef offsets or per-part UDMF fields based on map and game configuration.

namespace DBuilder.Map;

public static class VisualSidedefTextureOffsets
{
    public static (int X, int Y) Copy(Sidedef side, SidedefPart part, bool useLocalOffsets)
        => useLocalOffsets && part != SidedefPart.None
            ? ((int)side.GetFloatField(OffsetXField(part), 0.0), (int)side.GetFloatField(OffsetYField(part), 0.0))
            : (side.OffsetX, side.OffsetY);

    public static void Paste(Sidedef side, SidedefPart part, (int X, int Y) offsets, bool useLocalOffsets)
    {
        if (useLocalOffsets && part != SidedefPart.None)
        {
            side.Fields[OffsetXField(part)] = (double)offsets.X;
            side.Fields[OffsetYField(part)] = (double)offsets.Y;
            return;
        }

        side.OffsetX = offsets.X;
        side.OffsetY = offsets.Y;
    }

    private static string OffsetXField(SidedefPart part) => "offsetx_" + PartName(part);
    private static string OffsetYField(SidedefPart part) => "offsety_" + PartName(part);

    private static string PartName(SidedefPart part) => part switch
    {
        SidedefPart.Upper => "top",
        SidedefPart.Middle => "mid",
        SidedefPart.Lower => "bottom",
        _ => "mid",
    };
}
