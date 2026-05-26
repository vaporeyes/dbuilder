// ABOUTME: Minimal map-options container for UDB-compatible per-map settings backed by Configuration.
// ABOUTME: Currently ports selection group persistence while leaving UI and resource options for later slices.

using System.Collections;
using System.Collections.Specialized;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed class MapOptions
{
    public const string SelectionGroupsPath = "selectiongroups";
    public const int SelectionGroupCount = 10;

    public Configuration MapConfiguration { get; }

    public MapOptions() : this(new Configuration(sorted: true)) { }

    public MapOptions(Configuration mapConfiguration)
    {
        MapConfiguration = mapConfiguration;
    }

    public void WriteSelectionGroups(MapSet map)
    {
        var groups = new ListDictionary();

        for (int groupIndex = 0; groupIndex < SelectionGroupCount; groupIndex++)
        {
            int mask = MapSet.GroupMask(groupIndex);
            var group = new ListDictionary();
            AddGroupIndices(group, "vertices", map.Vertices, mask);
            AddGroupIndices(group, "linedefs", map.Linedefs, mask);
            AddGroupIndices(group, "sectors", map.Sectors, mask);
            AddGroupIndices(group, "things", map.Things, mask);
            if (group.Count > 0) groups.Add(groupIndex, group);
        }

        MapConfiguration.DeleteSetting(SelectionGroupsPath);
        if (groups.Count > 0) MapConfiguration.WriteSetting(SelectionGroupsPath, groups);
    }

    public void ReadSelectionGroups(MapSet map)
    {
        ClearSelectionGroups(map);

        var groupList = MapConfiguration.ReadSetting(SelectionGroupsPath, new Hashtable());
        if (groupList == null) return;

        foreach (DictionaryEntry entry in groupList)
        {
            if (entry.Value is not IDictionary groupInfo) continue;
            if (!int.TryParse(entry.Key?.ToString(), out int groupIndex)) continue;

            groupIndex = System.Math.Clamp(groupIndex, 0, SelectionGroupCount - 1);
            int mask = MapSet.GroupMask(groupIndex);
            ApplyGroupIndices(map.Vertices, groupInfo, "vertices", mask);
            ApplyGroupIndices(map.Linedefs, groupInfo, "linedefs", mask);
            ApplyGroupIndices(map.Sectors, groupInfo, "sectors", mask);
            ApplyGroupIndices(map.Things, groupInfo, "things", mask);
        }
    }

    private static void AddGroupIndices<T>(IDictionary group, string key, IReadOnlyList<T> items, int mask)
        where T : IGroupable
    {
        var indices = new List<string>();
        for (int i = 0; i < items.Count; i++)
            if ((items[i].Groups & mask) != 0) indices.Add(i.ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (indices.Count > 0) group.Add(key, string.Join(" ", indices));
    }

    private static void ApplyGroupIndices<T>(IList<T> items, IDictionary groupInfo, string key, int mask)
        where T : IGroupable
    {
        if (!groupInfo.Contains(key) || groupInfo[key] is not string value) return;

        foreach (int index in ParseIndices(value))
        {
            if (index < 0 || index >= items.Count) continue;
            items[index].Groups |= mask;
        }
    }

    private static IEnumerable<int> ParseIndices(string value)
    {
        string[] parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            if (int.TryParse(part, out int index)) yield return index;
        }
    }

    private static void ClearSelectionGroups(MapSet map)
    {
        int mask = 0;
        for (int groupIndex = 0; groupIndex < SelectionGroupCount; groupIndex++) mask |= MapSet.GroupMask(groupIndex);
        ClearGroupBits(map.Vertices, mask);
        ClearGroupBits(map.Linedefs, mask);
        ClearGroupBits(map.Sectors, mask);
        ClearGroupBits(map.Things, mask);
    }

    private static void ClearGroupBits<T>(IEnumerable<T> items, int mask)
        where T : IGroupable
    {
        foreach (var item in items) item.Groups &= ~mask;
    }
}
