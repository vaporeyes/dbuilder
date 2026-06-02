// ABOUTME: Builds structured linedef info-panel rows from map topology and game configuration labels.
// ABOUTME: Keeps linedef info formatting testable without depending on Avalonia controls.

using System.Globalization;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed record LinedefInfoPanelState(string Header, IReadOnlyList<InfoPanelField> Fields);

public static class LinedefInfoPanelModel
{
    public static LinedefInfoPanelState Build(MapSet map, Linedef linedef, GameConfiguration? config = null, bool hasArgs = true)
    {
        string action = config?.LinedefActionTitle(linedef.Action) ?? (linedef.Action == 0 ? "None" : $"action {linedef.Action.ToString(CultureInfo.InvariantCulture)}");
        string flags = config != null ? string.Join(", ", config.DescribeLinedefFlags(linedef.Flags)) : $"0x{linedef.Flags:X4}";
        if (flags.Length == 0) flags = "none";
        double length = (linedef.End.Position - linedef.Start.Position).GetLength();

        var fields = new List<InfoPanelField>
        {
            new("Action", $"{linedef.Action.ToString(CultureInfo.InvariantCulture)} - {action}"),
            new("Tags", DescribeTags(linedef.Tags)),
            new("Length", FormatDouble(length)),
            new("Angle", $"{FormatAngle(Angle2D.RadToDeg(linedef.Angle))}°"),
            new("Sides", linedef.Back != null ? "two-sided" : "one-sided"),
            new("Front sector", linedef.Front?.Sector is { } frontSector ? frontSector.Index.ToString(CultureInfo.InvariantCulture) : "-"),
            new("Back sector", linedef.Back?.Sector is { } backSector ? backSector.Index.ToString(CultureInfo.InvariantCulture) : "-"),
            new("Front textures", DescribeSideTextures(linedef.Front)),
            new("Back textures", DescribeSideTextures(linedef.Back)),
            new("Front offsets", DescribeSideOffsets(linedef.Front)),
            new("Back offsets", DescribeSideOffsets(linedef.Back)),
            new("Flags", flags),
            new("UDMF flags", DescribeStringSet(linedef.UdmfFlags)),
            new("Groups", DescribeGroups(linedef.Groups)),
            new("Custom fields", linedef.Fields.Count.ToString(CultureInfo.InvariantCulture)),
        };
        if (hasArgs) AddArgFields(fields, linedef.Args, config?.GetLinedefAction(linedef.Action)?.Args);

        int lineIndex = map.IndexOfLinedef(linedef);
        return new LinedefInfoPanelState(
            lineIndex >= 0 ? $"Linedef {lineIndex.ToString(CultureInfo.InvariantCulture)}" : "Linedef",
            fields);
    }

    private static void AddArgFields(List<InfoPanelField> fields, int[] args, ArgInfo[]? meta)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string title = meta != null && i < meta.Length && meta[i].Used ? $" ({meta[i].Title})" : "";
            fields.Add(new InfoPanelField($"Arg{(i + 1).ToString(CultureInfo.InvariantCulture)}{title}", args[i].ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static string DescribeTags(IReadOnlyList<int> tags)
        => tags.Count == 0 ? "0" : string.Join(", ", tags.Select(tag => tag.ToString(CultureInfo.InvariantCulture)));

    private static string DescribeStringSet(IEnumerable<string> names)
    {
        var values = names.Where(name => !string.IsNullOrWhiteSpace(name)).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        return values.Length == 0 ? "none" : string.Join(", ", values);
    }

    private static string DescribeGroups(int groups)
    {
        if (groups == 0) return "-";
        var result = new List<string>();
        for (int i = 0; i < MapOptions.SelectionGroupCount; i++)
            if ((groups & MapSet.GroupMask(i)) != 0) result.Add((i + 1).ToString(CultureInfo.InvariantCulture));
        return result.Count == 0 ? groups.ToString(CultureInfo.InvariantCulture) : string.Join(", ", result);
    }

    private static string DescribeSideTextures(Sidedef? side)
        => side == null ? "-" : $"U:{side.HighTexture} M:{side.MidTexture} L:{side.LowTexture}";

    private static string DescribeSideOffsets(Sidedef? side)
        => side == null ? "-" : $"{side.OffsetX.ToString(CultureInfo.InvariantCulture)}, {side.OffsetY.ToString(CultureInfo.InvariantCulture)}";

    private static string FormatDouble(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatAngle(double value)
        => value.ToString("0.#", CultureInfo.InvariantCulture);
}
