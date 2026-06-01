// ABOUTME: Builds structured sidedef info-panel rows from map topology and UDMF metadata.
// ABOUTME: Keeps sidedef info formatting testable without depending on Avalonia controls.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed record InfoPanelField(string Label, string Value);

public sealed record SidedefInfoPanelState(string Header, IReadOnlyList<InfoPanelField> Fields);

public static class SidedefInfoPanelModel
{
    public static SidedefInfoPanelState Build(MapSet map, Sidedef side)
    {
        int sideIndex = map.IndexOfSidedef(side);
        int lineIndex = side.Line == null ? -1 : map.IndexOfLinedef(side.Line);
        int sectorIndex = side.Sector == null ? -1 : map.IndexOfSector(side.Sector);

        return new SidedefInfoPanelState(
            sideIndex >= 0 ? $"Sidedef {sideIndex}" : "Sidedef",
            new[]
            {
                new InfoPanelField("Side", side.IsFront ? "front" : "back"),
                new InfoPanelField("Linedef", FormatIndex(lineIndex)),
                new InfoPanelField("Sector", FormatIndex(sectorIndex)),
                new InfoPanelField("Angle", $"{Angle2D.RadToDeg(side.Angle):0.#}°"),
                new InfoPanelField("Upper texture", side.HighTexture),
                new InfoPanelField("Middle texture", side.MidTexture),
                new InfoPanelField("Lower texture", side.LowTexture),
                new InfoPanelField("Offset X", side.OffsetX.ToString()),
                new InfoPanelField("Offset Y", side.OffsetY.ToString()),
                new InfoPanelField("UDMF flags", DescribeStringSet(side.UdmfFlags)),
                new InfoPanelField("Custom fields", side.Fields.Count.ToString()),
            });
    }

    private static string FormatIndex(int index)
        => index >= 0 ? index.ToString() : "-";

    private static string DescribeStringSet(IEnumerable<string> names)
    {
        var values = names.Where(name => !string.IsNullOrWhiteSpace(name)).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        return values.Length == 0 ? "none" : string.Join(", ", values);
    }
}
