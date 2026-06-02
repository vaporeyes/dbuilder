// ABOUTME: Builds structured vertex info-panel rows from map topology and UDMF metadata.
// ABOUTME: Keeps vertex info formatting testable without depending on Avalonia controls.

using System.Globalization;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed record VertexInfoPanelState(string Header, IReadOnlyList<InfoPanelField> Fields);

public static class VertexInfoPanelModel
{
    public static VertexInfoPanelState Build(MapSet map, Vertex vertex)
    {
        int vertexIndex = map.Vertices.IndexOf(vertex);

        return new VertexInfoPanelState(
            vertexIndex >= 0 ? $"Vertex {vertexIndex}" : "Vertex",
            new[]
            {
                new InfoPanelField("Position", $"({FormatDouble(vertex.Position.x)}, {FormatDouble(vertex.Position.y)})"),
                new InfoPanelField("Linedefs", vertex.Linedefs.Count.ToString(CultureInfo.InvariantCulture)),
                new InfoPanelField("Groups", DescribeGroups(vertex.Groups)),
                new InfoPanelField("Z floor", FormatOptionalDouble(vertex.ZFloor)),
                new InfoPanelField("Z ceiling", FormatOptionalDouble(vertex.ZCeiling)),
                new InfoPanelField("Custom fields", vertex.Fields.Count.ToString(CultureInfo.InvariantCulture)),
            });
    }

    private static string FormatOptionalDouble(double value)
        => double.IsNaN(value) ? "-" : FormatDouble(value);

    private static string FormatDouble(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string DescribeGroups(int groups)
    {
        if (groups == 0) return "-";
        var result = new List<string>();
        for (int i = 0; i < MapOptions.SelectionGroupCount; i++)
            if ((groups & MapSet.GroupMask(i)) != 0) result.Add((i + 1).ToString(CultureInfo.InvariantCulture));
        return result.Count == 0 ? groups.ToString(CultureInfo.InvariantCulture) : string.Join(", ", result);
    }
}
