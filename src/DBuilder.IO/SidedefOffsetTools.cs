// ABOUTME: Provides UDB-style sidedef vertical texture offset normalization helpers.
// ABOUTME: Resolves game-configuration peg flags and sky flat names outside the map model.

using System.Globalization;
using DBuilder.Map;

namespace DBuilder.IO;

public static class SidedefOffsetTools
{
    public static double GetOffsetY(
        Sidedef side,
        SidedefPart part,
        double offset,
        double scaleY,
        bool fromNormalized,
        GameConfiguration? config,
        int decimals = 3)
        => part switch
        {
            SidedefPart.Upper => GetTopOffsetY(side, offset, scaleY, fromNormalized, config, decimals),
            SidedefPart.Middle => GetMiddleOffsetY(side, offset, scaleY, fromNormalized, config, decimals),
            SidedefPart.Lower => GetBottomOffsetY(side, offset, scaleY, fromNormalized, config, decimals),
            _ => throw new NotSupportedException($"Sidedef part {part} does not support vertical offset normalization."),
        };

    public static double GetTopOffsetY(
        Sidedef side,
        double offset,
        double scaleY,
        bool fromNormalized,
        GameConfiguration? config,
        int decimals = 3)
    {
        if (IsLineFlagSet(side.Line, config?.UpperUnpeggedFlag) || side.Other?.Sector == null)
            return offset;

        double surfaceHeight = side.GetHighHeight() * Math.Abs(scaleY);
        return NormalizeOffset(offset, surfaceHeight, fromNormalized, decimals);
    }

    public static double GetMiddleOffsetY(
        Sidedef side,
        double offset,
        double scaleY,
        bool fromNormalized,
        GameConfiguration? config,
        int decimals = 3)
    {
        if (side.Sector == null) return offset;

        double surfaceHeight;
        double scale = Math.Abs(scaleY);
        if (side.Other?.Sector != null)
        {
            if (IsLineFlagSet(side.Line, config?.LowerUnpeggedFlag))
            {
                surfaceHeight = (side.Sector.CeilHeight - Math.Max(side.Sector.FloorHeight, side.Other.Sector.FloorHeight)) * scale;
            }
            else
            {
                surfaceHeight = Math.Abs(side.Sector.CeilHeight - side.Other.Sector.CeilHeight) * scale;
            }
        }
        else if (IsLineFlagSet(side.Line, config?.LowerUnpeggedFlag))
        {
            surfaceHeight = Math.Abs(side.Sector.CeilHeight - side.Sector.FloorHeight) * scale;
        }
        else
        {
            return offset;
        }

        return NormalizeOffset(offset, surfaceHeight, fromNormalized, decimals);
    }

    public static double GetBottomOffsetY(
        Sidedef side,
        double offset,
        double scaleY,
        bool fromNormalized,
        GameConfiguration? config,
        int decimals = 3)
    {
        if (side.Sector == null || side.Other?.Sector == null) return offset;

        double surfaceHeight;
        double scale = Math.Abs(scaleY);
        if (IsLineFlagSet(side.Line, config?.LowerUnpeggedFlag))
        {
            string skyFlatName = config?.SkyFlatName ?? "F_SKY1";
            if (!HasSkyCeiling(side.Sector, skyFlatName) || !HasSkyCeiling(side.Other.Sector, skyFlatName))
                return offset;

            surfaceHeight = (side.Sector.CeilHeight - side.Other.Sector.CeilHeight) * scale;
        }
        else
        {
            surfaceHeight = (side.Sector.CeilHeight - side.Other.Sector.FloorHeight) * scale;
        }

        return NormalizeOffset(offset, surfaceHeight, fromNormalized, decimals);
    }

    private static double NormalizeOffset(double offset, double surfaceHeight, bool fromNormalized, int decimals)
        => Math.Round(fromNormalized ? offset + surfaceHeight : offset - surfaceHeight, decimals);

    private static bool IsLineFlagSet(Linedef line, string? flag)
    {
        if (string.IsNullOrWhiteSpace(flag) || flag == "0") return false;

        if (int.TryParse(flag, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bit))
            return bit != 0 && (line.Flags & bit) == bit;

        return line.IsFlagSet(flag);
    }

    private static bool HasSkyCeiling(Sector sector, string skyFlatName)
        => string.Equals(sector.CeilTexture, skyFlatName, StringComparison.OrdinalIgnoreCase);
}
