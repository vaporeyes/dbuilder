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

    public static bool Nudge(
        Sidedef side,
        SidedefPart part,
        int horizontal,
        int vertical,
        bool useLocalOffsets,
        int? textureWidth = null,
        int? textureHeight = null)
    {
        if (horizontal == 0 && vertical == 0) return false;

        if (useLocalOffsets && part != SidedefPart.None)
        {
            string xField = OffsetXField(part);
            string yField = OffsetYField(part);
            side.Fields[xField] = NudgeLocal(side.GetFloatField(xField, 0.0), -horizontal, textureWidth);
            side.Fields[yField] = NudgeLocal(side.GetFloatField(yField, 0.0), -vertical, textureHeight);
            return true;
        }

        side.OffsetX -= horizontal;
        if (textureWidth is > 0) side.OffsetX %= textureWidth.Value;
        side.OffsetY -= vertical;
        if (part != SidedefPart.Middle && textureHeight is > 0) side.OffsetY %= textureHeight.Value;
        return true;
    }

    private static double NudgeLocal(double oldValue, int offset, int? textureSize)
    {
        if (offset == 0) return oldValue;
        if (textureSize is > 0 && offset % textureSize.Value == 0) return oldValue;

        double result = Math.Round(oldValue + offset);
        if (textureSize is > 0) result %= textureSize.Value;
        if (result == oldValue) result += offset < 0 ? -1 : 1;
        return result;
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
