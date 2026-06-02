// ABOUTME: Builds structured sector info-panel rows from map metadata and game configuration labels.
// ABOUTME: Keeps sector info formatting testable without depending on Avalonia controls.

using System.Globalization;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed record SectorInfoPanelState(string Header, IReadOnlyList<InfoPanelField> Fields);

public static class SectorInfoPanelModel
{
    public static SectorInfoPanelState Build(Sector sector, GameConfiguration? config = null)
    {
        string effect = config?.SectorEffectTitle(sector.Special) ?? (sector.Special == 0 ? "None" : $"effect {sector.Special}");

        return new SectorInfoPanelState(
            $"Sector {sector.Index.ToString(CultureInfo.InvariantCulture)}",
            new[]
            {
                new InfoPanelField("Floor height", sector.FloorHeight.ToString(CultureInfo.InvariantCulture)),
                new InfoPanelField("Ceiling height", sector.CeilHeight.ToString(CultureInfo.InvariantCulture)),
                new InfoPanelField("Floor texture", sector.FloorTexture),
                new InfoPanelField("Ceiling texture", sector.CeilTexture),
                new InfoPanelField("Brightness", sector.Brightness.ToString(CultureInfo.InvariantCulture)),
                new InfoPanelField("Effect", $"{sector.Special.ToString(CultureInfo.InvariantCulture)} - {effect}"),
                new InfoPanelField("Tags", DescribeTags(sector.Tags)),
                new InfoPanelField("Sidedefs", sector.Sidedefs.Count.ToString(CultureInfo.InvariantCulture)),
                new InfoPanelField("Groups", DescribeGroups(sector.Groups)),
                new InfoPanelField("Floor slope", DescribeSlope(sector.HasFloorSlope, sector.FloorSlope, sector.FloorSlopeOffset)),
                new InfoPanelField("Ceiling slope", DescribeSlope(sector.HasCeilSlope, sector.CeilSlope, sector.CeilSlopeOffset)),
                new InfoPanelField("Custom fields", sector.Fields.Count.ToString(CultureInfo.InvariantCulture)),
            });
    }

    private static string DescribeTags(IReadOnlyList<int> tags)
        => tags.Count == 0 ? "0" : string.Join(", ", tags.Select(tag => tag.ToString(CultureInfo.InvariantCulture)));

    private static string DescribeGroups(int groups)
    {
        if (groups == 0) return "-";
        var result = new List<string>();
        for (int i = 0; i < MapOptions.SelectionGroupCount; i++)
            if ((groups & MapSet.GroupMask(i)) != 0) result.Add((i + 1).ToString(CultureInfo.InvariantCulture));
        return result.Count == 0 ? groups.ToString(CultureInfo.InvariantCulture) : string.Join(", ", result);
    }

    private static string DescribeSlope(bool active, Vector3D normal, double offset)
    {
        if (!active) return "flat";
        string d = double.IsNaN(offset) ? "-" : FormatDouble(offset);
        return $"({FormatDouble(normal.x)}, {FormatDouble(normal.y)}, {FormatDouble(normal.z)}) d {d}";
    }

    private static string FormatDouble(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
