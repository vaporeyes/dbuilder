// ABOUTME: Find & replace over a MapSet by UDB-style object categories.
// ABOUTME: Find selects matches and reports a focus point; Replace mutates matched attributes without UI dependencies.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using DBuilder.Geometry;

namespace DBuilder.Map;

/// <summary>What a find/replace operation matches against.</summary>
public enum FindCategory
{
    ThingType,
    LinedefAction,
    SectorEffect,
    Tag,
    Texture,
    Flat,
    VertexIndex,
    LinedefIndex,
    SidedefIndex,
    SectorIndex,
    ThingIndex,
    SectorFloorHeight,
    SectorCeilingHeight,
    SectorBrightness,
    SectorFloorFlat,
    SectorCeilingFlat,
    SidedefUpperTexture,
    SidedefMiddleTexture,
    SidedefLowerTexture,
    ThingAngle,
    LinedefFlags,
    SidedefFlags,
    AnyUdmfField,
    VertexUdmfField,
    LinedefUdmfField,
    SidedefUdmfField,
    SectorUdmfField,
    ThingUdmfField,
}

/// <summary>Outcome of a find: how many matched and a representative location to center on.</summary>
public readonly record struct SearchResult(int Count, Vector2D? Focus);

/// <summary>Per-tag usage counts split by element class for statistics UI.</summary>
public readonly record struct TagStatistic(int Tag, int Sectors, int Linedefs, int Things)
{
    public int Total => Sectors + Linedefs + Things;
}

/// <summary>Controls which map-format-specific tag owners participate in tag searches.</summary>
public readonly record struct TagSearchOptions(bool IncludeLinedefs, bool IncludeThings)
{
    public static TagSearchOptions All => new(true, true);
}

/// <summary>Per-thing-type usage count for statistics UI.</summary>
public readonly record struct ThingTypeStatistic(int Type, int Count);

public static class MapSearch
{
    /// <summary>True for categories whose value is a string (texture/flat names); the rest are integers.</summary>
    public static bool IsTextual(FindCategory cat) => cat switch
    {
        FindCategory.Texture or
        FindCategory.Flat or
        FindCategory.SectorFloorFlat or
        FindCategory.SectorCeilingFlat or
        FindCategory.SidedefUpperTexture or
        FindCategory.SidedefMiddleTexture or
        FindCategory.SidedefLowerTexture => true,
        _ => false,
    };

    /// <summary>
    /// Selects every element matching <paramref name="value"/> in <paramref name="cat"/> (clearing prior selection)
    /// and returns the match count plus a focus point (the first match's location).
    /// </summary>
    public static SearchResult Find(MapSet map, FindCategory cat, string value)
        => Find(map, cat, value, TagSearchOptions.All);

    public static SearchResult Find(MapSet map, FindCategory cat, string value, TagSearchOptions tagOptions)
    {
        map.ClearAllSelected();
        int count = 0;
        Vector2D? focus = null;
        bool numOk = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int num);

        switch (cat)
        {
            case FindCategory.ThingType:
                if (numOk) foreach (var t in map.Things) if (t.Type == num) { t.Selected = true; count++; focus ??= t.Position; }
                break;
            case FindCategory.ThingIndex:
                if (numOk && num >= 0 && num < map.Things.Count) { map.Things[num].Selected = true; count = 1; focus = map.Things[num].Position; }
                break;
            case FindCategory.ThingAngle:
                if (numOk) foreach (var t in map.Things) if (t.Angle == num) { t.Selected = true; count++; focus ??= t.Position; }
                break;
            case FindCategory.ThingUdmfField:
                foreach (var t in map.Things)
                {
                    int matches = CountUdmfFieldMatches(t, value);
                    if (matches > 0) { t.Selected = true; count += matches; focus ??= t.Position; }
                }
                break;
            case FindCategory.LinedefAction:
                if (numOk) foreach (var l in map.Linedefs) if (l.Action == num) { l.Selected = true; count++; focus ??= Mid(l); }
                break;
            case FindCategory.VertexIndex:
                if (numOk && num >= 0 && num < map.Vertices.Count) { map.Vertices[num].Selected = true; count = 1; focus = map.Vertices[num].Position; }
                break;
            case FindCategory.VertexUdmfField:
                foreach (var v in map.Vertices)
                {
                    int matches = CountUdmfFieldMatches(v, value);
                    if (matches > 0) { v.Selected = true; count += matches; focus ??= v.Position; }
                }
                break;
            case FindCategory.LinedefIndex:
                if (numOk && num >= 0 && num < map.Linedefs.Count) { map.Linedefs[num].Selected = true; count = 1; focus = Mid(map.Linedefs[num]); }
                break;
            case FindCategory.LinedefUdmfField:
                foreach (var l in map.Linedefs)
                {
                    int matches = CountUdmfFieldMatches(l, value);
                    if (matches > 0) { l.Selected = true; count += matches; focus ??= Mid(l); }
                }
                break;
            case FindCategory.LinedefFlags:
                if (TryParseFlagQuery(value, out var lineFlags))
                    foreach (var l in map.Linedefs)
                        if (FlagsMatch(l, lineFlags)) { l.Selected = true; count++; focus ??= Mid(l); }
                break;
            case FindCategory.SidedefIndex:
                if (numOk && num >= 0 && num < map.Sidedefs.Count)
                {
                    Sidedef side = map.Sidedefs[num];
                    if (side.Line != null) { side.Line.Selected = true; focus = Mid(side.Line); }
                    count = 1;
                }
                break;
            case FindCategory.SidedefUdmfField:
                foreach (var sd in map.Sidedefs)
                {
                    int matches = CountUdmfFieldMatches(sd, value);
                    if (matches > 0)
                    {
                        if (sd.Line != null) { sd.Line.Selected = true; focus ??= Mid(sd.Line); }
                        count += matches;
                    }
                }
                break;
            case FindCategory.SidedefFlags:
                if (TryParseFlagQuery(value, out var sideFlags))
                    foreach (var sd in map.Sidedefs)
                        if (FlagsMatch(sd, sideFlags))
                        {
                            if (sd.Line != null) { sd.Line.Selected = true; focus ??= Mid(sd.Line); }
                            count++;
                        }
                break;
            case FindCategory.SectorEffect:
                if (numOk) foreach (var s in map.Sectors) if (s.Special == num) { s.Selected = true; count++; }
                break;
            case FindCategory.SectorIndex:
                if (numOk && num >= 0 && num < map.Sectors.Count) { map.Sectors[num].Selected = true; count = 1; }
                break;
            case FindCategory.SectorUdmfField:
                foreach (var s in map.Sectors)
                {
                    int matches = CountUdmfFieldMatches(s, value);
                    if (matches > 0) { s.Selected = true; count += matches; }
                }
                break;
            case FindCategory.SectorFloorHeight:
                if (numOk) foreach (var s in map.Sectors) if (s.FloorHeight == num) { s.Selected = true; count++; }
                break;
            case FindCategory.SectorCeilingHeight:
                if (numOk) foreach (var s in map.Sectors) if (s.CeilHeight == num) { s.Selected = true; count++; }
                break;
            case FindCategory.SectorBrightness:
                if (numOk) foreach (var s in map.Sectors) if (s.Brightness == num) { s.Selected = true; count++; }
                break;
            case FindCategory.Tag:
                if (numOk)
                {
                    if (tagOptions.IncludeLinedefs)
                        foreach (var l in map.Linedefs) if (MapElementTags.HasTag(l, num)) { l.Selected = true; count++; focus ??= Mid(l); }
                    foreach (var s in map.Sectors) if (MapElementTags.HasTag(s, num)) { s.Selected = true; count++; }
                    if (tagOptions.IncludeThings)
                        foreach (var t in map.Things) if (MapElementTags.HasTag(t, num)) { t.Selected = true; count++; focus ??= t.Position; }
                }
                break;
            case FindCategory.Texture:
                foreach (var sd in map.Sidedefs)
                    if (sd.Line != null && (Eq(sd.HighTexture, value) || Eq(sd.MidTexture, value) || Eq(sd.LowTexture, value)))
                    { sd.Line.Selected = true; count++; focus ??= Mid(sd.Line); }
                break;
            case FindCategory.SidedefUpperTexture:
                foreach (var sd in map.Sidedefs) if (sd.Line != null && Eq(sd.HighTexture, value)) { sd.Line.Selected = true; count++; focus ??= Mid(sd.Line); }
                break;
            case FindCategory.SidedefMiddleTexture:
                foreach (var sd in map.Sidedefs) if (sd.Line != null && Eq(sd.MidTexture, value)) { sd.Line.Selected = true; count++; focus ??= Mid(sd.Line); }
                break;
            case FindCategory.SidedefLowerTexture:
                foreach (var sd in map.Sidedefs) if (sd.Line != null && Eq(sd.LowTexture, value)) { sd.Line.Selected = true; count++; focus ??= Mid(sd.Line); }
                break;
            case FindCategory.Flat:
                foreach (var s in map.Sectors)
                    if (Eq(s.FloorTexture, value) || Eq(s.CeilTexture, value)) { s.Selected = true; count++; }
                break;
            case FindCategory.SectorFloorFlat:
                foreach (var s in map.Sectors) if (Eq(s.FloorTexture, value)) { s.Selected = true; count++; }
                break;
            case FindCategory.SectorCeilingFlat:
                foreach (var s in map.Sectors) if (Eq(s.CeilTexture, value)) { s.Selected = true; count++; }
                break;
            case FindCategory.AnyUdmfField:
                count += SelectUdmfFieldMatches(map.Vertices, value, element => element.Selected = true, element => focus ??= element.Position);
                count += SelectUdmfFieldMatches(map.Linedefs, value, element => element.Selected = true, element => focus ??= Mid(element));
                count += SelectUdmfFieldMatches(map.Sidedefs, value, element => { if (element.Line != null) element.Line.Selected = true; }, element => { if (element.Line != null) focus ??= Mid(element.Line); });
                count += SelectUdmfFieldMatches(map.Sectors, value, element => element.Selected = true, _ => { });
                count += SelectUdmfFieldMatches(map.Things, value, element => element.Selected = true, element => focus ??= element.Position);
                break;
        }
        return new SearchResult(count, focus);
    }

    /// <summary>
    /// Replaces <paramref name="find"/> with <paramref name="replace"/> across <paramref name="cat"/> and returns
    /// the number of elements changed. Numeric categories parse both values; textual ones compare case-insensitively.
    /// </summary>
    public static int Replace(MapSet map, FindCategory cat, string find, string replace)
        => Replace(map, cat, find, replace, TagSearchOptions.All);

    public static int Replace(MapSet map, FindCategory cat, string find, string replace, TagSearchOptions tagOptions)
    {
        int changed = 0;
        if (IsTextual(cat))
        {
            switch (cat)
            {
                case FindCategory.Texture:
                    foreach (var sd in map.Sidedefs)
                    {
                        bool hit = false;
                        if (Eq(sd.HighTexture, find)) { sd.HighTexture = replace; hit = true; }
                        if (Eq(sd.MidTexture, find)) { sd.MidTexture = replace; hit = true; }
                        if (Eq(sd.LowTexture, find)) { sd.LowTexture = replace; hit = true; }
                        if (hit) changed++;
                    }
                    break;
                case FindCategory.SidedefUpperTexture:
                    foreach (var sd in map.Sidedefs) if (Eq(sd.HighTexture, find)) { sd.HighTexture = replace; changed++; }
                    break;
                case FindCategory.SidedefMiddleTexture:
                    foreach (var sd in map.Sidedefs) if (Eq(sd.MidTexture, find)) { sd.MidTexture = replace; changed++; }
                    break;
                case FindCategory.SidedefLowerTexture:
                    foreach (var sd in map.Sidedefs) if (Eq(sd.LowTexture, find)) { sd.LowTexture = replace; changed++; }
                    break;
                case FindCategory.Flat:
                    foreach (var s in map.Sectors)
                    {
                        bool hit = false;
                        if (Eq(s.FloorTexture, find)) { s.FloorTexture = replace; hit = true; }
                        if (Eq(s.CeilTexture, find)) { s.CeilTexture = replace; hit = true; }
                        if (hit) changed++;
                    }
                    break;
                case FindCategory.SectorFloorFlat:
                    foreach (var s in map.Sectors) if (Eq(s.FloorTexture, find)) { s.FloorTexture = replace; changed++; }
                    break;
                case FindCategory.SectorCeilingFlat:
                    foreach (var s in map.Sectors) if (Eq(s.CeilTexture, find)) { s.CeilTexture = replace; changed++; }
                    break;
            }
            return changed;
        }

        if (cat == FindCategory.LinedefFlags || cat == FindCategory.SidedefFlags)
            return ReplaceFlags(map, cat, find, replace);

        if (!int.TryParse(find, NumberStyles.Integer, CultureInfo.InvariantCulture, out int from)) return 0;
        if (!int.TryParse(replace, NumberStyles.Integer, CultureInfo.InvariantCulture, out int to)) return 0;
        switch (cat)
        {
            case FindCategory.ThingType:
                foreach (var t in map.Things) if (t.Type == from) { t.Type = to; changed++; }
                break;
            case FindCategory.ThingAngle:
                foreach (var t in map.Things) if (t.Angle == from) { t.Angle = to; changed++; }
                break;
            case FindCategory.LinedefAction:
                foreach (var l in map.Linedefs) if (l.Action == from) { l.Action = to; changed++; }
                break;
            case FindCategory.SectorEffect:
                foreach (var s in map.Sectors) if (s.Special == from) { s.Special = to; changed++; }
                break;
            case FindCategory.SectorFloorHeight:
                foreach (var s in map.Sectors) if (s.FloorHeight == from) { s.FloorHeight = to; changed++; }
                break;
            case FindCategory.SectorCeilingHeight:
                foreach (var s in map.Sectors) if (s.CeilHeight == from) { s.CeilHeight = to; changed++; }
                break;
            case FindCategory.SectorBrightness:
                foreach (var s in map.Sectors) if (s.Brightness == from) { s.Brightness = to; changed++; }
                break;
            case FindCategory.Tag:
                if (tagOptions.IncludeLinedefs)
                    foreach (var l in map.Linedefs) if (MapElementTags.ReplaceTag(l, from, to)) changed++;
                foreach (var s in map.Sectors) if (MapElementTags.ReplaceTag(s, from, to)) changed++;
                if (tagOptions.IncludeThings)
                    foreach (var t in map.Things) if (MapElementTags.ReplaceTag(t, from, to)) changed++;
                break;
        }

        return changed;
    }

    /// <summary>All positive tags in use (linedefs, sectors, things) with how many elements use each, ascending.</summary>
    public static List<(int Tag, int Count)> UsedTags(MapSet map)
        => UsedTags(map, TagSearchOptions.All);

    public static List<(int Tag, int Count)> UsedTags(MapSet map, TagSearchOptions options)
    {
        var counts = new Dictionary<int, int>();
        void Add(int t) { if (t != 0) counts[t] = counts.TryGetValue(t, out int c) ? c + 1 : 1; }
        if (options.IncludeLinedefs)
            foreach (var l in map.Linedefs) foreach (int tag in MapElementTags.PositiveTags(l)) Add(tag);
        foreach (var s in map.Sectors) foreach (int tag in MapElementTags.PositiveTags(s)) Add(tag);
        if (options.IncludeThings)
            foreach (var t in map.Things) foreach (int tag in MapElementTags.PositiveTags(t)) Add(tag);
        var list = new List<(int, int)>();
        foreach (var kv in counts) list.Add((kv.Key, kv.Value));
        list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return list;
    }

    /// <summary>All positive tags in use with separate sector, linedef and thing counts.</summary>
    public static List<TagStatistic> UsedTagStatistics(MapSet map)
        => UsedTagStatistics(map, TagSearchOptions.All);

    public static List<TagStatistic> UsedTagStatistics(MapSet map, TagSearchOptions options)
    {
        var sectors = new Dictionary<int, int>();
        var linedefs = new Dictionary<int, int>();
        var things = new Dictionary<int, int>();

        static void Add(Dictionary<int, int> counts, int tag)
        {
            if (tag != 0) counts[tag] = counts.TryGetValue(tag, out int c) ? c + 1 : 1;
        }

        foreach (var s in map.Sectors) foreach (int tag in MapElementTags.PositiveTags(s)) Add(sectors, tag);
        if (options.IncludeLinedefs)
            foreach (var l in map.Linedefs) foreach (int tag in MapElementTags.PositiveTags(l)) Add(linedefs, tag);
        if (options.IncludeThings)
            foreach (var t in map.Things) foreach (int tag in MapElementTags.PositiveTags(t)) Add(things, tag);

        var tags = new SortedSet<int>(sectors.Keys);
        tags.UnionWith(linedefs.Keys);
        tags.UnionWith(things.Keys);

        var list = new List<TagStatistic>();
        foreach (int tag in tags)
        {
            sectors.TryGetValue(tag, out int sectorCount);
            linedefs.TryGetValue(tag, out int linedefCount);
            things.TryGetValue(tag, out int thingCount);
            list.Add(new TagStatistic(tag, sectorCount, linedefCount, thingCount));
        }
        return list;
    }

    /// <summary>Thing type usage counts, ascending by thing type.</summary>
    public static List<ThingTypeStatistic> ThingTypeStatistics(MapSet map)
    {
        var counts = new SortedDictionary<int, int>();
        foreach (var thing in map.Things)
            counts[thing.Type] = counts.TryGetValue(thing.Type, out int c) ? c + 1 : 1;

        var list = new List<ThingTypeStatistic>();
        foreach (var kv in counts) list.Add(new ThingTypeStatistic(kv.Key, kv.Value));
        return list;
    }

    /// <summary>The lowest positive tag not used by any linedef, sector or thing.</summary>
    public static int NextFreeTag(MapSet map)
    {
        var used = new HashSet<int>();
        foreach (var l in map.Linedefs) foreach (int t in MapElementTags.PositiveTags(l)) used.Add(t);
        foreach (var s in map.Sectors) foreach (int t in MapElementTags.PositiveTags(s)) used.Add(t);
        foreach (var thing in map.Things) foreach (int t in MapElementTags.PositiveTags(thing)) used.Add(t);
        int tag = 1;
        while (used.Contains(tag)) tag++;
        return tag;
    }

    private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static Vector2D Mid(Linedef l)
        => new Vector2D((l.Start.Position.x + l.End.Position.x) * 0.5, (l.Start.Position.y + l.End.Position.y) * 0.5);

    private static bool TryParseFlagQuery(string input, out List<(string Flag, bool Set)> flags)
    {
        flags = new List<(string, bool)>();
        foreach (string part in input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            bool set = true;
            string flag = part;
            if (flag.StartsWith("!", StringComparison.Ordinal))
            {
                set = false;
                flag = flag[1..].Trim();
            }

            if (!string.IsNullOrWhiteSpace(flag))
                flags.Add((flag, set));
        }

        return flags.Count > 0;
    }

    private static bool FlagsMatch(Linedef line, IReadOnlyList<(string Flag, bool Set)> flags)
    {
        foreach (var flag in flags)
            if (line.IsFlagSet(flag.Flag) != flag.Set) return false;
        return true;
    }

    private static bool FlagsMatch(Sidedef side, IReadOnlyList<(string Flag, bool Set)> flags)
    {
        foreach (var flag in flags)
            if (side.IsFlagSet(flag.Flag) != flag.Set) return false;
        return true;
    }

    private static int ReplaceFlags(MapSet map, FindCategory category, string find, string replace)
    {
        if (!TryParseFlagQuery(find, out var findFlags) || !TryParseFlagQuery(replace, out var replaceFlags)) return 0;

        int changed = 0;
        if (category == FindCategory.LinedefFlags)
        {
            foreach (var line in map.Linedefs)
            {
                if (!FlagsMatch(line, findFlags)) continue;
                foreach (var flag in replaceFlags) line.SetFlag(flag.Flag, flag.Set);
                changed++;
            }
        }
        else
        {
            foreach (var side in map.Sidedefs)
            {
                if (!FlagsMatch(side, findFlags)) continue;
                foreach (var flag in replaceFlags) side.SetFlag(flag.Flag, flag.Set);
                changed++;
            }
        }

        return changed;
    }

    private static int SelectUdmfFieldMatches<T>(IEnumerable<T> elements, string input, Action<T> select, Action<T> setFocus)
        where T : IFielded
    {
        int count = 0;
        foreach (T element in elements)
        {
            int matches = CountUdmfFieldMatches(element, input);
            if (matches == 0) continue;
            select(element);
            setFocus(element);
            count += matches;
        }

        return count;
    }

    private static int CountUdmfFieldMatches(IFielded element, string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return 0;

        (string key, string value) = SplitUdmfFieldQuery(input);
        Regex keyPattern = WildcardRegex(key);
        Regex? valuePattern = string.IsNullOrEmpty(value) ? null : WildcardRegex(value);
        int count = 0;

        foreach (var field in element.Fields)
        {
            if (!keyPattern.IsMatch(field.Key)) continue;
            if (valuePattern == null) { count++; continue; }

            string fieldValue = Convert.ToString(field.Value, CultureInfo.InvariantCulture) ?? "";
            if (valuePattern.IsMatch(fieldValue)) count++;
        }

        return count;
    }

    private static (string Key, string Value) SplitUdmfFieldQuery(string input)
    {
        input = input.Trim();
        int space = input.IndexOf(' ');
        if (space == -1) return (input, "");
        return (input[..space], input[space..].Trim());
    }

    private static Regex WildcardRegex(string value)
        => new("^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$", RegexOptions.CultureInvariant);
}
