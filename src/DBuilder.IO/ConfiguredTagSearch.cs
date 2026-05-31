// ABOUTME: Config-aware tag search helpers for map tag fields and tag-typed action arguments.
// ABOUTME: Mirrors UDB tag ownership rules without adding GameConfiguration dependencies to DBuilder.Map.

using System.Collections.Generic;
using System.Globalization;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public static class ConfiguredTagSearch
{
    public static bool IsReferenceCategory(FindCategory category)
        => category is FindCategory.LinedefSectorReference or FindCategory.LinedefThingReference
            or FindCategory.ThingSectorReference or FindCategory.ThingThingReference;

    public static SearchResult FindReference(MapSet map, FindCategory category, string value, GameConfiguration? config)
    {
        map.ClearAllSelected();
        if (!TryParseReference(value, requireByteRange: false, out int reference) || !IsReferenceCategory(category))
            return new SearchResult(0, null);

        int count = 0;
        Vector2D? focus = null;

        if (category is FindCategory.LinedefSectorReference or FindCategory.LinedefThingReference)
        {
            if (!(config?.HasActionArgs ?? true)) return new SearchResult(0, null);
            UniversalType type = category == FindCategory.LinedefSectorReference ? UniversalType.SectorTag : UniversalType.ThingTag;
            foreach (var line in map.Linedefs)
                if (HasMatchingActionArg(line.Action, line.Args, reference, config, type))
                    SelectLine(line, ref count, ref focus);
        }
        else
        {
            if (!((config?.HasThingAction ?? true) && (config?.HasActionArgs ?? true))) return new SearchResult(0, null);
            UniversalType type = category == FindCategory.ThingSectorReference ? UniversalType.SectorTag : UniversalType.ThingTag;
            foreach (var thing in map.Things)
                if (HasMatchingActionArg(thing.Action, thing.Args, reference, config, type))
                    SelectThing(thing, ref count, ref focus);
        }

        return new SearchResult(count, focus);
    }

    public static int ReplaceReference(MapSet map, FindCategory category, string find, string replace, GameConfiguration? config)
    {
        if (!TryParseReference(find, requireByteRange: false, out int from) ||
            !TryParseReference(replace, requireByteRange: true, out int to) ||
            !IsReferenceCategory(category))
            return 0;

        int changed = 0;
        if (category is FindCategory.LinedefSectorReference or FindCategory.LinedefThingReference)
        {
            if (!(config?.HasActionArgs ?? true)) return 0;
            UniversalType type = category == FindCategory.LinedefSectorReference ? UniversalType.SectorTag : UniversalType.ThingTag;
            foreach (var line in map.Linedefs)
                if (ReplaceActionArgs(line.Action, line.Args, from, to, config, type)) changed++;
        }
        else
        {
            if (!((config?.HasThingAction ?? true) && (config?.HasActionArgs ?? true))) return 0;
            UniversalType type = category == FindCategory.ThingSectorReference ? UniversalType.SectorTag : UniversalType.ThingTag;
            foreach (var thing in map.Things)
                if (ReplaceActionArgs(thing.Action, thing.Args, from, to, config, type)) changed++;
        }

        return changed;
    }

    public static SearchResult Find(MapSet map, string value, GameConfiguration? config)
    {
        map.ClearAllSelected();
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int tag))
            return new SearchResult(0, null);

        int count = 0;
        Vector2D? focus = null;

        foreach (var sector in map.Sectors)
        {
            if (!MapElementTags.HasTag(sector, tag)) continue;
            sector.Selected = true;
            count++;
        }

        if (config?.HasThingTag ?? true)
        {
            foreach (var thing in map.Things)
                if (MapElementTags.HasTag(thing, tag)) SelectThing(thing, ref count, ref focus);
        }

        if ((config?.HasThingAction ?? true) && (config?.HasActionArgs ?? true))
        {
            foreach (var thing in map.Things)
                if (HasMatchingActionArg(thing.Action, thing.Args, tag, config)) SelectThing(thing, ref count, ref focus);
        }

        if (config?.HasLinedefTag ?? true)
        {
            foreach (var line in map.Linedefs)
                if (MapElementTags.HasTag(line, tag)) SelectLine(line, ref count, ref focus);
        }

        if (config?.HasActionArgs ?? true)
        {
            foreach (var line in map.Linedefs)
                if (HasMatchingActionArg(line.Action, line.Args, tag, config)) SelectLine(line, ref count, ref focus);
        }

        return new SearchResult(count, focus);
    }

    public static int Replace(MapSet map, string find, string replace, GameConfiguration? config)
    {
        if (!int.TryParse(find, NumberStyles.Integer, CultureInfo.InvariantCulture, out int from)) return 0;
        if (!int.TryParse(replace, NumberStyles.Integer, CultureInfo.InvariantCulture, out int to)) return 0;

        int changed = 0;
        foreach (var sector in map.Sectors)
            if (MapElementTags.ReplaceTag(sector, from, to)) changed++;

        foreach (var thing in map.Things)
        {
            bool hit = false;
            if (config?.HasThingTag ?? true) hit |= MapElementTags.ReplaceTag(thing, from, to);
            if ((config?.HasThingAction ?? true) && (config?.HasActionArgs ?? true))
                hit |= ReplaceActionArgs(thing.Action, thing.Args, from, to, config);
            if (hit) changed++;
        }

        foreach (var line in map.Linedefs)
        {
            bool hit = false;
            if (config?.HasLinedefTag ?? true) hit |= MapElementTags.ReplaceTag(line, from, to);
            if (config?.HasActionArgs ?? true) hit |= ReplaceActionArgs(line.Action, line.Args, from, to, config);
            if (hit) changed++;
        }

        return changed;
    }

    public static List<(int Tag, int Count)> UsedTags(MapSet map, GameConfiguration? config)
    {
        var stats = UsedTagStatistics(map, config);
        var tags = new List<(int, int)>();
        foreach (var stat in stats) tags.Add((stat.Tag, stat.Total));
        return tags;
    }

    public static List<TagStatistic> UsedTagStatistics(MapSet map, GameConfiguration? config)
    {
        var sectors = new Dictionary<int, int>();
        var linedefs = new Dictionary<int, int>();
        var things = new Dictionary<int, int>();

        foreach (var sector in map.Sectors)
            foreach (int tag in MapElementTags.PositiveTags(sector)) Add(sectors, tag);

        if (config?.HasThingTag ?? true)
        {
            foreach (var thing in map.Things)
                foreach (int tag in MapElementTags.PositiveTags(thing)) Add(things, tag);
        }

        if ((config?.HasThingAction ?? true) && (config?.HasActionArgs ?? true))
        {
            foreach (var thing in map.Things)
                foreach (int tag in PositiveActionArgTags(thing.Action, thing.Args, config)) Add(things, tag);
        }

        if (config?.HasLinedefTag ?? true)
        {
            foreach (var line in map.Linedefs)
                foreach (int tag in MapElementTags.PositiveTags(line)) Add(linedefs, tag);
        }

        if (config?.HasActionArgs ?? true)
        {
            foreach (var line in map.Linedefs)
                foreach (int tag in PositiveActionArgTags(line.Action, line.Args, config)) Add(linedefs, tag);
        }

        var allTags = new SortedSet<int>(sectors.Keys);
        allTags.UnionWith(linedefs.Keys);
        allTags.UnionWith(things.Keys);

        var result = new List<TagStatistic>();
        foreach (int tag in allTags)
        {
            sectors.TryGetValue(tag, out int sectorCount);
            linedefs.TryGetValue(tag, out int linedefCount);
            things.TryGetValue(tag, out int thingCount);
            result.Add(new TagStatistic(tag, sectorCount, linedefCount, thingCount));
        }

        return result;
    }

    public static int NextFreeTag(MapSet map, GameConfiguration? config, int maxTag = int.MaxValue)
    {
        var used = new HashSet<int>();
        foreach (var stat in UsedTagStatistics(map, config))
            if (stat.Tag > 0) used.Add(stat.Tag);

        for (int tag = 1; tag <= maxTag; tag++)
            if (!used.Contains(tag)) return tag;

        return 0;
    }

    private static void Add(Dictionary<int, int> counts, int tag)
    {
        if (tag > 0) counts[tag] = counts.TryGetValue(tag, out int count) ? count + 1 : 1;
    }

    private static void SelectLine(Linedef line, ref int count, ref Vector2D? focus)
    {
        if (line.Selected) return;
        line.Selected = true;
        count++;
        focus ??= Mid(line);
    }

    private static void SelectThing(Thing thing, ref int count, ref Vector2D? focus)
    {
        if (thing.Selected) return;
        thing.Selected = true;
        count++;
        focus ??= thing.Position;
    }

    private static bool HasMatchingActionArg(int action, int[] values, int tag, GameConfiguration? config)
        => HasMatchingActionArg(action, values, tag, config, null);

    private static bool HasMatchingActionArg(int action, int[] values, int tag, GameConfiguration? config, UniversalType? type)
    {
        var args = config?.GetLinedefAction(action)?.Args;
        if (args == null) return false;

        for (int i = 0; i < args.Length && i < values.Length; i++)
            if (IsMatchingTagArg(args[i], type) && values[i] == tag) return true;

        return false;
    }

    private static bool ReplaceActionArgs(int action, int[] values, int from, int to, GameConfiguration? config)
        => ReplaceActionArgs(action, values, from, to, config, null);

    private static bool ReplaceActionArgs(int action, int[] values, int from, int to, GameConfiguration? config, UniversalType? type)
    {
        var args = config?.GetLinedefAction(action)?.Args;
        if (args == null) return false;

        bool changed = false;
        for (int i = 0; i < args.Length && i < values.Length; i++)
        {
            if (!IsMatchingTagArg(args[i], type) || values[i] != from) continue;
            values[i] = to;
            changed = true;
        }

        return changed;
    }

    private static IEnumerable<int> PositiveActionArgTags(int action, int[] values, GameConfiguration? config)
    {
        var args = config?.GetLinedefAction(action)?.Args;
        if (args == null) yield break;

        for (int i = 0; i < args.Length && i < values.Length; i++)
            if (IsTagArg(args[i]) && values[i] > 0) yield return values[i];
    }

    private static bool IsTagArg(ArgInfo arg)
        => IsMatchingTagArg(arg, null);

    private static bool IsMatchingTagArg(ArgInfo arg, UniversalType? type)
    {
        if (!arg.Used) return false;
        var argType = (UniversalType)arg.Type;
        return type == null
            ? argType is UniversalType.LinedefTag or UniversalType.SectorTag or UniversalType.ThingTag
            : argType == type.Value;
    }

    private static bool TryParseReference(string value, bool requireByteRange, out int reference)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out reference)) return false;
        return !requireByteRange || reference is >= 0 and <= 255;
    }

    private static Vector2D Mid(Linedef line)
        => new((line.Start.Position.x + line.End.Position.x) * 0.5, (line.Start.Position.y + line.End.Position.y) * 0.5);
}
