// ABOUTME: Builds structured thing info-panel rows from map topology and game configuration labels.
// ABOUTME: Keeps thing info formatting testable without depending on Avalonia controls.

using System.Globalization;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed record ThingInfoPanelState(string Header, IReadOnlyList<InfoPanelField> Fields);

public static class ThingInfoPanelModel
{
    public static ThingInfoPanelState Build(MapSet map, Thing thing, GameConfiguration? config = null, bool hasArgs = true)
    {
        string name = config?.ThingTitle(thing.Type) ?? $"type {thing.Type.ToString(CultureInfo.InvariantCulture)}";
        string action = config?.LinedefActionTitle(thing.Action) ?? (thing.Action == 0 ? "None" : $"action {thing.Action.ToString(CultureInfo.InvariantCulture)}");
        string flags = config != null ? string.Join(", ", config.DescribeThingFlags(thing.Flags)) : $"0x{thing.Flags:X4}";
        if (flags.Length == 0) flags = "none";

        var fields = new List<InfoPanelField>
        {
            new("Type", $"{thing.Type.ToString(CultureInfo.InvariantCulture)} - {name}"),
            new("Action", $"{thing.Action.ToString(CultureInfo.InvariantCulture)} - {action}"),
            new("Position", $"({FormatWhole(thing.Position.x)}, {FormatWhole(thing.Position.y)}, {FormatWhole(thing.Height)})"),
            new("Angle", $"{thing.Angle.ToString(CultureInfo.InvariantCulture)}°"),
            new("Pitch / roll", $"{thing.Pitch.ToString(CultureInfo.InvariantCulture)}° / {thing.Roll.ToString(CultureInfo.InvariantCulture)}°"),
            new("Scale", $"{FormatDouble(thing.ScaleX)} x {FormatDouble(thing.ScaleY)}"),
            new("Tag", thing.Tag.ToString(CultureInfo.InvariantCulture)),
            new("Flags", flags),
            new("UDMF flags", DescribeStringSet(thing.UdmfFlags)),
            new("Groups", DescribeGroups(thing.Groups)),
            new("Custom fields", thing.Fields.Count.ToString(CultureInfo.InvariantCulture)),
        };
        if (hasArgs) AddArgFields(fields, thing.Args, config?.GetThing(thing.Type)?.Args);

        int thingIndex = map.IndexOfThing(thing);
        return new ThingInfoPanelState(
            thingIndex >= 0 ? $"Thing {thingIndex.ToString(CultureInfo.InvariantCulture)}" : "Thing",
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

    private static string FormatWhole(double value)
        => value.ToString("0", CultureInfo.InvariantCulture);

    private static string FormatDouble(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
