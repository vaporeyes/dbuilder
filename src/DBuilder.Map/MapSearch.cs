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
    LinedefActionArguments,
    ThingActionArguments,
    SectorEffect,
    Tag,
    LinedefTag,
    SectorTag,
    ThingTag,
    TextureOrFlat,
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
    SectorFlags,
    ThingFlags,
    LinedefSectorReference,
    LinedefThingReference,
    ThingSectorReference,
    ThingThingReference,
    AnyUdmfField,
    VertexUdmfField,
    LinedefUdmfField,
    SidedefUdmfField,
    SectorUdmfField,
    ThingUdmfField,
}

public sealed record FindCategoryDescriptor(FindCategory Category, string Label, bool BrowseButton = false)
{
    public override string ToString() => Label;
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
    private const int ClassicTextureNameLength = 8;
    private const int ClassicMinThingType = short.MinValue;
    private const int ClassicMaxThingType = short.MaxValue;

    public static IReadOnlyList<FindCategoryDescriptor> CategoryDescriptors { get; } =
    [
        new(FindCategory.ThingType, "Thing Type", BrowseButton: true),
        new(FindCategory.ThingIndex, "Thing Index"),
        new(FindCategory.ThingAngle, "Thing Angle", BrowseButton: true),
        new(FindCategory.ThingActionArguments, "Thing Action and Arguments", BrowseButton: true),
        new(FindCategory.ThingFlags, "Thing Flags", BrowseButton: true),
        new(FindCategory.ThingSectorReference, "Thing Sector Reference"),
        new(FindCategory.ThingThingReference, "Thing Thing Reference"),
        new(FindCategory.LinedefAction, "Linedef Action", BrowseButton: true),
        new(FindCategory.LinedefActionArguments, "Linedef Action and Arguments", BrowseButton: true),
        new(FindCategory.LinedefIndex, "Linedef Index"),
        new(FindCategory.LinedefFlags, "Linedef Flags", BrowseButton: true),
        new(FindCategory.LinedefSectorReference, "Linedef Sector Reference"),
        new(FindCategory.LinedefThingReference, "Linedef Thing Reference"),
        new(FindCategory.SidedefIndex, "Sidedef Index"),
        new(FindCategory.SidedefFlags, "Sidedef Flags", BrowseButton: true),
        new(FindCategory.SectorEffect, "Sector Effect", BrowseButton: true),
        new(FindCategory.SectorIndex, "Sector Index"),
        new(FindCategory.SectorFloorHeight, "Sector Height (Floor)"),
        new(FindCategory.SectorCeilingHeight, "Sector Height (Ceiling)"),
        new(FindCategory.SectorBrightness, "Sector Brightness"),
        new(FindCategory.SectorFlags, "Sector Flags", BrowseButton: true),
        new(FindCategory.Tag, "Tag"),
        new(FindCategory.LinedefTag, "Linedef Tag"),
        new(FindCategory.SectorTag, "Sector Tag"),
        new(FindCategory.ThingTag, "Thing Tag"),
        new(FindCategory.TextureOrFlat, "Any Texture or Flat", BrowseButton: true),
        new(FindCategory.Texture, "Sidedef Texture (Any)", BrowseButton: true),
        new(FindCategory.SidedefUpperTexture, "Sidedef Texture (Upper)", BrowseButton: true),
        new(FindCategory.SidedefMiddleTexture, "Sidedef Texture (Middle)", BrowseButton: true),
        new(FindCategory.SidedefLowerTexture, "Sidedef Texture (Lower)", BrowseButton: true),
        new(FindCategory.Flat, "Sector Flat (Any)", BrowseButton: true),
        new(FindCategory.SectorFloorFlat, "Sector Flat (Floor)", BrowseButton: true),
        new(FindCategory.SectorCeilingFlat, "Sector Flat (Ceiling)", BrowseButton: true),
        new(FindCategory.VertexIndex, "Vertex Index"),
        new(FindCategory.AnyUdmfField, "Any UDMF Field"),
        new(FindCategory.VertexUdmfField, "Vertex UDMF Field"),
        new(FindCategory.LinedefUdmfField, "Linedef UDMF Field"),
        new(FindCategory.SidedefUdmfField, "Sidedef UDMF Field"),
        new(FindCategory.SectorUdmfField, "Sector UDMF Field"),
        new(FindCategory.ThingUdmfField, "Thing UDMF Field"),
    ];

    private readonly record struct SearchLists(
        IReadOnlyList<Vertex> Vertices,
        IReadOnlyList<Linedef> Linedefs,
        IReadOnlyList<Sidedef> Sidedefs,
        IReadOnlyList<Sector> Sectors,
        IReadOnlyList<Thing> Things)
    {
        public static SearchLists From(MapSet map, bool withinSelection)
            => withinSelection
                ? new SearchLists(
                    map.GetSelectedVertices(),
                    map.GetSelectedLinedefs(),
                    map.GetSidedefsFromSelectedLinedefs(true),
                    map.GetSelectedSectors(),
                    map.GetSelectedThings())
                : new SearchLists(map.Vertices, map.Linedefs, map.Sidedefs, map.Sectors, map.Things);
    }

    /// <summary>True for categories whose value is a string (texture/flat names); the rest are integers.</summary>
    public static bool IsTextual(FindCategory cat) => cat switch
    {
        FindCategory.Texture or
        FindCategory.TextureOrFlat or
        FindCategory.Flat or
        FindCategory.SectorFloorFlat or
        FindCategory.SectorCeilingFlat or
        FindCategory.SidedefUpperTexture or
        FindCategory.SidedefMiddleTexture or
        FindCategory.SidedefLowerTexture => true,
        _ => false,
    };

    public static bool CanReplace(FindCategory cat, bool mixTexturesFlats = false) => cat switch
    {
        FindCategory.VertexIndex or
        FindCategory.LinedefIndex or
        FindCategory.SidedefIndex or
        FindCategory.SectorIndex or
        FindCategory.ThingIndex or
        FindCategory.AnyUdmfField or
        FindCategory.VertexUdmfField or
        FindCategory.LinedefUdmfField or
        FindCategory.SidedefUdmfField or
        FindCategory.SectorUdmfField or
        FindCategory.ThingUdmfField => false,
        FindCategory.TextureOrFlat => mixTexturesFlats,
        _ => true,
    };

    public static string FormatFindResult(int matchCount)
        => matchCount == 0 ? "No matches." : $"Found {CountLabel(matchCount, "match", "matches")}.";

    public static string FormatReplaceResult(int replacementCount)
        => replacementCount == 0 ? "Nothing replaced." : $"Replaced {CountLabel(replacementCount, "element")}.";

    public static string FormatNextFreeTagResult(int tag)
        => $"Next free tag: {tag.ToString(CultureInfo.InvariantCulture)}.";

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";

    /// <summary>
    /// Selects every element matching <paramref name="value"/> in <paramref name="cat"/> (clearing prior selection)
    /// and returns the match count plus a focus point (the first match's location).
    /// </summary>
    public static SearchResult Find(MapSet map, FindCategory cat, string value)
        => Find(map, cat, value, TagSearchOptions.All);

    public static SearchResult Find(MapSet map, FindCategory cat, string value, bool withinSelection)
        => Find(map, cat, value, TagSearchOptions.All, null, null, withinSelection);

    public static SearchResult Find(MapSet map, FindCategory cat, string value, TagSearchOptions tagOptions)
        => Find(map, cat, value, tagOptions, null);

    public static SearchResult Find(
        MapSet map,
        FindCategory cat,
        string value,
        TagSearchOptions tagOptions,
        Func<int, int, bool>? linedefActionMatcher)
        => Find(map, cat, value, tagOptions, linedefActionMatcher, null);

    public static SearchResult Find(
        MapSet map,
        FindCategory cat,
        string value,
        TagSearchOptions tagOptions,
        Func<int, int, bool>? linedefActionMatcher,
        Func<int, int, bool>? sectorEffectMatcher)
        => Find(map, cat, value, tagOptions, linedefActionMatcher, sectorEffectMatcher, false);

    public static SearchResult Find(
        MapSet map,
        FindCategory cat,
        string value,
        TagSearchOptions tagOptions,
        Func<int, int, bool>? linedefActionMatcher,
        Func<int, int, bool>? sectorEffectMatcher,
        bool withinSelection)
    {
        SearchLists lists = SearchLists.From(map, withinSelection);
        map.ClearAllSelected();
        int count = 0;
        Vector2D? focus = null;
        bool numOk = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int num);

        switch (cat)
        {
            case FindCategory.ThingType:
                if (TryParseIntList(value, out var thingTypes))
                    foreach (var t in lists.Things)
                        if (thingTypes.Contains(t.Type)) { t.Selected = true; count++; focus ??= t.Position; }
                break;
            case FindCategory.ThingIndex:
                if (numOk && num >= 0 && num < map.Things.Count) { map.Things[num].Selected = true; count = 1; focus = map.Things[num].Position; }
                break;
            case FindCategory.ThingAngle:
                if (numOk) foreach (var t in lists.Things) if (t.Angle == num) { t.Selected = true; count++; focus ??= t.Position; }
                break;
            case FindCategory.ThingActionArguments:
                if (TryParseActionQuery(value, out var thingActionQuery))
                    foreach (var t in lists.Things)
                        if (ActionQueryMatches(t, t.Action, t.Args, thingActionQuery)) { t.Selected = true; count++; focus ??= t.Position; }
                break;
            case FindCategory.ThingFlags:
                if (TryParseFlagQuery(value, out var thingFlags))
                    foreach (var t in lists.Things)
                        if (FlagsMatch(t, thingFlags)) { t.Selected = true; count++; focus ??= t.Position; }
                break;
            case FindCategory.ThingUdmfField:
                foreach (var t in lists.Things)
                {
                    int matches = CountUdmfFieldMatches(t, value);
                    if (matches > 0) { t.Selected = true; count += matches; focus ??= t.Position; }
                }
                break;
            case FindCategory.LinedefAction:
                if (numOk) foreach (var l in lists.Linedefs) if (NumberMatches(l.Action, num, linedefActionMatcher)) { l.Selected = true; count++; focus ??= Mid(l); }
                break;
            case FindCategory.LinedefActionArguments:
                if (TryParseActionQuery(value, out var lineActionQuery))
                    foreach (var l in lists.Linedefs)
                        if (ActionQueryMatches(l, l.Action, l.Args, lineActionQuery, linedefActionMatcher)) { l.Selected = true; count++; focus ??= Mid(l); }
                break;
            case FindCategory.VertexIndex:
                if (numOk && num >= 0 && num < map.Vertices.Count) { map.Vertices[num].Selected = true; count = 1; focus = map.Vertices[num].Position; }
                break;
            case FindCategory.VertexUdmfField:
                foreach (var v in lists.Vertices)
                {
                    int matches = CountUdmfFieldMatches(v, value);
                    if (matches > 0) { v.Selected = true; count += matches; focus ??= v.Position; }
                }
                break;
            case FindCategory.LinedefIndex:
                if (numOk && num >= 0 && num < map.Linedefs.Count) { map.Linedefs[num].Selected = true; count = 1; focus = Mid(map.Linedefs[num]); }
                break;
            case FindCategory.LinedefUdmfField:
                foreach (var l in lists.Linedefs)
                {
                    int matches = CountUdmfFieldMatches(l, value);
                    if (matches > 0) { l.Selected = true; count += matches; focus ??= Mid(l); }
                }
                break;
            case FindCategory.LinedefFlags:
                if (TryParseFlagQuery(value, out var lineFlags))
                    foreach (var l in lists.Linedefs)
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
                foreach (var sd in lists.Sidedefs)
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
                    foreach (var sd in lists.Sidedefs)
                        if (FlagsMatch(sd, sideFlags))
                        {
                            if (sd.Line != null) { sd.Line.Selected = true; focus ??= Mid(sd.Line); }
                            count++;
                        }
                break;
            case FindCategory.SectorEffect:
                if (numOk)
                    foreach (var s in lists.Sectors)
                        if (num == -1 ? s.Special > 0 : NumberMatches(s.Special, num, sectorEffectMatcher)) { s.Selected = true; count++; }
                break;
            case FindCategory.SectorIndex:
                if (numOk && num >= 0 && num < map.Sectors.Count) { map.Sectors[num].Selected = true; count = 1; }
                break;
            case FindCategory.SectorUdmfField:
                foreach (var s in lists.Sectors)
                {
                    int matches = CountUdmfFieldMatches(s, value);
                    if (matches > 0) { s.Selected = true; count += matches; }
                }
                break;
            case FindCategory.SectorFloorHeight:
                if (numOk) foreach (var s in lists.Sectors) if (s.FloorHeight == num) { s.Selected = true; count++; }
                break;
            case FindCategory.SectorCeilingHeight:
                if (numOk) foreach (var s in lists.Sectors) if (s.CeilHeight == num) { s.Selected = true; count++; }
                break;
            case FindCategory.SectorBrightness:
                if (TryParseComparison(value, out var brightnessQuery))
                    foreach (var s in lists.Sectors)
                        if (ComparisonMatches(s.Brightness, brightnessQuery)) { s.Selected = true; count++; }
                break;
            case FindCategory.SectorFlags:
                if (TryParseFlagQuery(value, out var sectorFlags))
                    foreach (var s in lists.Sectors)
                        if (FlagsMatch(s, sectorFlags)) { s.Selected = true; count++; }
                break;
            case FindCategory.Tag:
                if (numOk)
                {
                    if (tagOptions.IncludeLinedefs)
                        foreach (var l in lists.Linedefs) if (MapElementTags.HasTag(l, num)) { l.Selected = true; count++; focus ??= Mid(l); }
                    foreach (var s in lists.Sectors) if (MapElementTags.HasTag(s, num)) { s.Selected = true; count++; }
                    if (tagOptions.IncludeThings)
                        foreach (var t in lists.Things) if (MapElementTags.HasTag(t, num)) { t.Selected = true; count++; focus ??= t.Position; }
                }
                break;
            case FindCategory.LinedefTag:
                if (numOk)
                    foreach (var l in lists.Linedefs) if (MapElementTags.HasTag(l, num)) { l.Selected = true; count++; focus ??= Mid(l); }
                break;
            case FindCategory.SectorTag:
                if (numOk)
                    foreach (var s in lists.Sectors) if (MapElementTags.HasTag(s, num)) { s.Selected = true; count++; }
                break;
            case FindCategory.ThingTag:
                if (numOk)
                    foreach (var t in lists.Things) if (MapElementTags.HasTag(t, num)) { t.Selected = true; count++; focus ??= t.Position; }
                break;
            case FindCategory.Texture:
                foreach (var sd in lists.Sidedefs)
                {
                    if (sd.Line == null) continue;
                    bool selected = false;
                    if (TextureSlotMatches(sd.HighTexture, value, sd.HighRequired())) { selected = true; count++; }
                    if (TextureSlotMatches(sd.MidTexture, value, sd.MiddleRequired())) { selected = true; count++; }
                    if (TextureSlotMatches(sd.LowTexture, value, sd.LowRequired())) { selected = true; count++; }
                    if (selected) { sd.Line.Selected = true; focus ??= Mid(sd.Line); }
                }
                break;
            case FindCategory.TextureOrFlat:
                foreach (var s in lists.Sectors)
                {
                    if (TexturePatternMatches(s.CeilTexture, value)) { s.Selected = true; count++; }
                    if (TexturePatternMatches(s.FloorTexture, value)) { s.Selected = true; count++; }
                }
                foreach (var sd in lists.Sidedefs)
                {
                    if (sd.Line == null) continue;
                    bool selected = false;
                    if (TextureSlotMatches(sd.HighTexture, value, sd.HighRequired())) { selected = true; count++; }
                    if (TextureSlotMatches(sd.MidTexture, value, sd.MiddleRequired())) { selected = true; count++; }
                    if (TextureSlotMatches(sd.LowTexture, value, sd.LowRequired())) { selected = true; count++; }
                    if (selected) { sd.Line.Selected = true; focus ??= Mid(sd.Line); }
                }
                break;
            case FindCategory.SidedefUpperTexture:
                foreach (var sd in lists.Sidedefs) if (sd.Line != null && TextureSlotMatches(sd.HighTexture, value, sd.HighRequired())) { sd.Line.Selected = true; count++; focus ??= Mid(sd.Line); }
                break;
            case FindCategory.SidedefMiddleTexture:
                foreach (var sd in lists.Sidedefs) if (sd.Line != null && TextureSlotMatches(sd.MidTexture, value, sd.MiddleRequired())) { sd.Line.Selected = true; count++; focus ??= Mid(sd.Line); }
                break;
            case FindCategory.SidedefLowerTexture:
                foreach (var sd in lists.Sidedefs) if (sd.Line != null && TextureSlotMatches(sd.LowTexture, value, sd.LowRequired())) { sd.Line.Selected = true; count++; focus ??= Mid(sd.Line); }
                break;
            case FindCategory.Flat:
                foreach (var s in lists.Sectors)
                {
                    bool selected = false;
                    if (TexturePatternMatches(s.FloorTexture, value)) { selected = true; count++; }
                    if (TexturePatternMatches(s.CeilTexture, value)) { selected = true; count++; }
                    if (selected) s.Selected = true;
                }
                break;
            case FindCategory.SectorFloorFlat:
                foreach (var s in lists.Sectors) if (TexturePatternMatches(s.FloorTexture, value)) { s.Selected = true; count++; }
                break;
            case FindCategory.SectorCeilingFlat:
                foreach (var s in lists.Sectors) if (TexturePatternMatches(s.CeilTexture, value)) { s.Selected = true; count++; }
                break;
            case FindCategory.AnyUdmfField:
                count += SelectUdmfFieldMatches(lists.Vertices, value, element => element.Selected = true, element => focus ??= element.Position);
                count += SelectUdmfFieldMatches(lists.Linedefs, value, element => element.Selected = true, element => focus ??= Mid(element));
                count += SelectUdmfFieldMatches(lists.Sidedefs, value, element => { if (element.Line != null) element.Line.Selected = true; }, element => { if (element.Line != null) focus ??= Mid(element.Line); });
                count += SelectUdmfFieldMatches(lists.Sectors, value, element => element.Selected = true, _ => { });
                count += SelectUdmfFieldMatches(lists.Things, value, element => element.Selected = true, element => focus ??= element.Position);
                break;
        }
        return new SearchResult(count, focus);
    }

    /// <summary>
    /// Replaces <paramref name="find"/> with <paramref name="replace"/> across <paramref name="cat"/> and returns
    /// the number of elements changed. Numeric categories parse both values; textual ones compare case-insensitively.
    /// </summary>
    public static int Replace(MapSet map, FindCategory cat, string find, string replace)
        => Replace(map, cat, find, replace, TagSearchOptions.All, null, null, false, false);

    public static int Replace(MapSet map, FindCategory cat, string find, string replace, bool withinSelection)
        => Replace(map, cat, find, replace, TagSearchOptions.All, null, null, withinSelection, false);

    public static int Replace(MapSet map, FindCategory cat, string find, string replace, bool withinSelection, bool mixTexturesFlats)
        => Replace(map, cat, find, replace, TagSearchOptions.All, null, null, withinSelection, mixTexturesFlats);

    public static int Replace(MapSet map, FindCategory cat, string find, string replace, TagSearchOptions tagOptions)
        => Replace(map, cat, find, replace, tagOptions, null);

    public static int Replace(
        MapSet map,
        FindCategory cat,
        string find,
        string replace,
        TagSearchOptions tagOptions,
        Func<int, int, bool>? linedefActionMatcher)
        => Replace(map, cat, find, replace, tagOptions, linedefActionMatcher, null);

    public static int Replace(
        MapSet map,
        FindCategory cat,
        string find,
        string replace,
        TagSearchOptions tagOptions,
        Func<int, int, bool>? linedefActionMatcher,
        Func<int, int, bool>? sectorEffectMatcher)
        => Replace(map, cat, find, replace, tagOptions, linedefActionMatcher, sectorEffectMatcher, false, false);

    public static int Replace(
        MapSet map,
        FindCategory cat,
        string find,
        string replace,
        TagSearchOptions tagOptions,
        Func<int, int, bool>? linedefActionMatcher,
        Func<int, int, bool>? sectorEffectMatcher,
        bool withinSelection)
        => Replace(map, cat, find, replace, tagOptions, linedefActionMatcher, sectorEffectMatcher, withinSelection, false);

    public static int Replace(
        MapSet map,
        FindCategory cat,
        string find,
        string replace,
        TagSearchOptions tagOptions,
        Func<int, int, bool>? linedefActionMatcher,
        Func<int, int, bool>? sectorEffectMatcher,
        bool withinSelection,
        bool mixTexturesFlats)
        => Replace(map, cat, find, replace, tagOptions, linedefActionMatcher, sectorEffectMatcher, withinSelection, mixTexturesFlats, ClassicTextureNameLength, ClassicMinThingType, ClassicMaxThingType);

    public static int Replace(
        MapSet map,
        FindCategory cat,
        string find,
        string replace,
        TagSearchOptions tagOptions,
        Func<int, int, bool>? linedefActionMatcher,
        Func<int, int, bool>? sectorEffectMatcher,
        bool withinSelection,
        bool mixTexturesFlats,
        int maxTextureNameLength)
        => Replace(map, cat, find, replace, tagOptions, linedefActionMatcher, sectorEffectMatcher, withinSelection, mixTexturesFlats, maxTextureNameLength, ClassicMinThingType, ClassicMaxThingType);

    public static int Replace(
        MapSet map,
        FindCategory cat,
        string find,
        string replace,
        TagSearchOptions tagOptions,
        Func<int, int, bool>? linedefActionMatcher,
        Func<int, int, bool>? sectorEffectMatcher,
        bool withinSelection,
        bool mixTexturesFlats,
        int maxTextureNameLength,
        int minThingType,
        int maxThingType,
        Func<int, bool>? actionArg0StringSupported = null)
    {
        if (!CanReplace(cat, mixTexturesFlats)) return 0;

        int changed = 0;
        SearchLists lists = SearchLists.From(map, withinSelection);
        if (IsTextual(cat))
        {
            if (!IsValidTextureReplacement(replace, maxTextureNameLength)) return 0;

            switch (cat)
            {
                case FindCategory.Texture:
                    foreach (var sd in lists.Sidedefs)
                    {
                        if (TextureSlotMatches(sd.HighTexture, find, sd.HighRequired())) { sd.HighTexture = replace; changed++; }
                        if (TextureSlotMatches(sd.MidTexture, find, sd.MiddleRequired())) { sd.MidTexture = replace; changed++; }
                        if (TextureSlotMatches(sd.LowTexture, find, sd.LowRequired())) { sd.LowTexture = replace; changed++; }
                    }
                    break;
                case FindCategory.TextureOrFlat:
                    foreach (var s in lists.Sectors)
                    {
                        if (TexturePatternMatches(s.CeilTexture, find)) { s.CeilTexture = replace; changed++; }
                        if (TexturePatternMatches(s.FloorTexture, find)) { s.FloorTexture = replace; changed++; }
                    }
                    foreach (var sd in lists.Sidedefs)
                    {
                        if (TextureSlotMatches(sd.HighTexture, find, sd.HighRequired())) { sd.HighTexture = replace; changed++; }
                        if (TextureSlotMatches(sd.MidTexture, find, sd.MiddleRequired())) { sd.MidTexture = replace; changed++; }
                        if (TextureSlotMatches(sd.LowTexture, find, sd.LowRequired())) { sd.LowTexture = replace; changed++; }
                    }
                    break;
                case FindCategory.SidedefUpperTexture:
                    foreach (var sd in lists.Sidedefs) if (TextureSlotMatches(sd.HighTexture, find, sd.HighRequired())) { sd.HighTexture = replace; changed++; }
                    break;
                case FindCategory.SidedefMiddleTexture:
                    foreach (var sd in lists.Sidedefs) if (TextureSlotMatches(sd.MidTexture, find, sd.MiddleRequired())) { sd.MidTexture = replace; changed++; }
                    break;
                case FindCategory.SidedefLowerTexture:
                    foreach (var sd in lists.Sidedefs) if (TextureSlotMatches(sd.LowTexture, find, sd.LowRequired())) { sd.LowTexture = replace; changed++; }
                    break;
                case FindCategory.Flat:
                    foreach (var s in lists.Sectors)
                    {
                        if (TexturePatternMatches(s.FloorTexture, find)) { s.FloorTexture = replace; changed++; }
                        if (TexturePatternMatches(s.CeilTexture, find)) { s.CeilTexture = replace; changed++; }
                    }
                    break;
                case FindCategory.SectorFloorFlat:
                    foreach (var s in lists.Sectors) if (TexturePatternMatches(s.FloorTexture, find)) { s.FloorTexture = replace; changed++; }
                    break;
                case FindCategory.SectorCeilingFlat:
                    foreach (var s in lists.Sectors) if (TexturePatternMatches(s.CeilTexture, find)) { s.CeilTexture = replace; changed++; }
                    break;
            }
            return changed;
        }

        if (IsFlagCategory(cat))
            return ReplaceFlags(lists, cat, find, replace);

        if (cat == FindCategory.LinedefActionArguments || cat == FindCategory.ThingActionArguments)
            return ReplaceActionArguments(lists, cat, find, replace, linedefActionMatcher, actionArg0StringSupported);

        if (cat == FindCategory.ThingType)
        {
            if (!TryParseIntList(find, out var findTypes) || !TryParseIntList(replace, out var replaceTypes)) return 0;
            foreach (int type in replaceTypes)
                if (type < minThingType || type > maxThingType) return 0;

            foreach (var t in lists.Things)
            {
                if (!findTypes.Contains(t.Type)) continue;
                t.Type = replaceTypes[Random.Shared.Next(replaceTypes.Count)];
                changed++;
            }

            return changed;
        }

        if (cat == FindCategory.SectorBrightness)
        {
            if (!int.TryParse(replace, NumberStyles.Integer, CultureInfo.InvariantCulture, out int brightness)) return 0;
            if (brightness < 0 || brightness > 255 || !TryParseComparison(find, out var brightnessQuery)) return 0;
            foreach (var s in lists.Sectors)
                if (ComparisonMatches(s.Brightness, brightnessQuery)) { s.Brightness = brightness; changed++; }

            return changed;
        }

        if (!int.TryParse(find, NumberStyles.Integer, CultureInfo.InvariantCulture, out int from)) return 0;
        if (!int.TryParse(replace, NumberStyles.Integer, CultureInfo.InvariantCulture, out int to)) return 0;
        if (cat == FindCategory.LinedefAction && (to < 0 || to > short.MaxValue)) return 0;
        if (cat == FindCategory.SectorEffect && (to < 0 || to > short.MaxValue)) return 0;
        switch (cat)
        {
            case FindCategory.ThingAngle:
                foreach (var t in lists.Things) if (t.Angle == from) { t.Angle = to; changed++; }
                break;
            case FindCategory.LinedefAction:
                foreach (var l in lists.Linedefs) if (NumberMatches(l.Action, from, linedefActionMatcher)) { l.Action = to; changed++; }
                break;
            case FindCategory.SectorEffect:
                foreach (var s in lists.Sectors)
                    if (from == -1 ? s.Special > 0 : NumberMatches(s.Special, from, sectorEffectMatcher)) { s.Special = to; changed++; }
                break;
            case FindCategory.SectorFloorHeight:
                foreach (var s in lists.Sectors) if (s.FloorHeight == from) { s.FloorHeight = to; changed++; }
                break;
            case FindCategory.SectorCeilingHeight:
                foreach (var s in lists.Sectors) if (s.CeilHeight == from) { s.CeilHeight = to; changed++; }
                break;
            case FindCategory.Tag:
                if (tagOptions.IncludeLinedefs)
                    foreach (var l in lists.Linedefs) if (MapElementTags.ReplaceTag(l, from, to)) changed++;
                foreach (var s in lists.Sectors) if (MapElementTags.ReplaceTag(s, from, to)) changed++;
                if (tagOptions.IncludeThings)
                    foreach (var t in lists.Things) if (MapElementTags.ReplaceTag(t, from, to)) changed++;
                break;
            case FindCategory.LinedefTag:
                foreach (var l in lists.Linedefs) if (MapElementTags.ReplaceTag(l, from, to)) changed++;
                break;
            case FindCategory.SectorTag:
                foreach (var s in lists.Sectors) if (MapElementTags.ReplaceTag(s, from, to)) changed++;
                break;
            case FindCategory.ThingTag:
                foreach (var t in lists.Things) if (MapElementTags.ReplaceTag(t, from, to)) changed++;
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

    private readonly record struct NumericComparison(string Prefix, int Value);

    private static bool TryParseComparison(string input, out NumericComparison comparison)
    {
        string value = input.Trim().Replace(" ", "", StringComparison.Ordinal);
        string prefix = "";
        if (value.StartsWith(">=", StringComparison.Ordinal) || value.StartsWith("<=", StringComparison.Ordinal))
            prefix = value[..2];
        else if (value.StartsWith(">", StringComparison.Ordinal) || value.StartsWith("<", StringComparison.Ordinal))
            prefix = value[..1];

        value = value[prefix.Length..];
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
        {
            comparison = default;
            return false;
        }

        comparison = new NumericComparison(prefix, number);
        return true;
    }

    private static bool ComparisonMatches(int value, NumericComparison comparison) => comparison.Prefix switch
    {
        "" => value == comparison.Value,
        "<=" => value <= comparison.Value,
        ">=" => value >= comparison.Value,
        "<" => value < comparison.Value,
        ">" => value > comparison.Value,
        _ => false,
    };

    private static bool TryParseIntList(string input, out List<int> values)
    {
        values = new List<int>();
        foreach (string part in input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                values.Clear();
                return false;
            }

            values.Add(value);
        }

        return values.Count > 0;
    }

    private static bool TexturePatternMatches(string name, string pattern)
    {
        if (pattern.IndexOf('*') == -1 && pattern.IndexOf('?') == -1) return Eq(name, pattern);
        var regex = new Regex("^" + Regex.Escape(pattern).Replace("\\?", ".").Replace("\\*", ".*") + "$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return regex.IsMatch(name);
    }

    private static bool TextureSlotMatches(string name, string pattern, bool required)
        => TexturePatternMatches(name, pattern) && (pattern != "-" || required);

    private static bool IsValidTextureReplacement(string replace, int maxTextureNameLength)
        => !string.IsNullOrEmpty(replace) && replace.Length <= maxTextureNameLength;

    private static Vector2D Mid(Linedef l)
        => new Vector2D((l.Start.Position.x + l.End.Position.x) * 0.5, (l.Start.Position.y + l.End.Position.y) * 0.5);

    private readonly record struct ActionArgQuery(int Action, string? Arg0String, int?[] Args);

    private static bool TryParseActionQuery(string input, out ActionArgQuery query)
    {
        query = default;
        string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int action))
            return false;

        var args = new int?[5];
        string? arg0String = null;
        int start = 1;
        if (parts.Length > 1 && parts[1] != "*" && !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            arg0String = parts[1].Replace("\"", "", StringComparison.Ordinal);
            start = 2;
        }

        for (int i = start; i < parts.Length && i - 1 < args.Length; i++)
        {
            if (parts[i] == "*") continue;
            if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int arg))
                args[i - 1] = arg;
        }

        query = new ActionArgQuery(action, arg0String, args);
        return true;
    }

    private static bool NumberMatches(int actual, int expected, Func<int, int, bool>? matcher)
        => actual == expected || (matcher?.Invoke(actual, expected) ?? false);

    private static bool ActionQueryMatches(IFielded element, int action, int[] args, ActionArgQuery query)
        => ActionQueryMatches(element, action, args, query, null);

    private static bool ActionQueryMatches(
        IFielded element,
        int action,
        int[] args,
        ActionArgQuery query,
        Func<int, int, bool>? actionMatcher)
    {
        if (query.Action == -1)
        {
            if (action == 0) return false;
        }
        else if (!NumberMatches(action, query.Action, actionMatcher))
        {
            return false;
        }

        if (query.Arg0String != null &&
            !string.Equals(element.GetStringField("arg0str"), query.Arg0String, StringComparison.OrdinalIgnoreCase))
            return false;

        for (int i = 0; i < query.Args.Length && i < args.Length; i++)
            if (query.Args[i] is int expected && args[i] != expected) return false;
        return true;
    }

    private static int ReplaceActionArguments(
        SearchLists lists,
        FindCategory category,
        string find,
        string replace,
        Func<int, int, bool>? linedefActionMatcher,
        Func<int, bool>? actionArg0StringSupported)
    {
        if (!TryParseActionQuery(find, out var findQuery) ||
            !TryParseActionQuery(replace, out var replaceQuery) ||
            replaceQuery.Action < 0 ||
            replaceQuery.Action > short.MaxValue)
            return 0;

        int changed = 0;
        if (category == FindCategory.LinedefActionArguments)
        {
            foreach (var line in lists.Linedefs)
            {
                if (!ActionQueryMatches(line, line.Action, line.Args, findQuery, linedefActionMatcher)) continue;
                ApplyActionReplacement(line.Args, replaceQuery);
                if (replaceQuery.Arg0String != null && ActionArg0StringSupported(replaceQuery.Action, actionArg0StringSupported))
                    line.SetStringField("arg0str", replaceQuery.Arg0String);
                line.Action = replaceQuery.Action;
                changed++;
            }
        }
        else
        {
            foreach (var thing in lists.Things)
            {
                if (!ActionQueryMatches(thing, thing.Action, thing.Args, findQuery)) continue;
                ApplyActionReplacement(thing.Args, replaceQuery);
                if (replaceQuery.Arg0String != null && ActionArg0StringSupported(replaceQuery.Action, actionArg0StringSupported))
                    thing.SetStringField("arg0str", replaceQuery.Arg0String);
                thing.Action = replaceQuery.Action;
                changed++;
            }
        }

        return changed;
    }

    private static bool ActionArg0StringSupported(int action, Func<int, bool>? actionArg0StringSupported)
        => actionArg0StringSupported?.Invoke(action) ?? true;

    private static void ApplyActionReplacement(int[] args, ActionArgQuery replaceQuery)
    {
        for (int i = 0; i < replaceQuery.Args.Length && i < args.Length; i++)
            if (replaceQuery.Args[i] is int value) args[i] = value;
    }

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

    private static bool FlagsMatch(Sector sector, IReadOnlyList<(string Flag, bool Set)> flags)
    {
        foreach (var flag in flags)
            if (sector.IsFlagSet(flag.Flag) != flag.Set) return false;
        return true;
    }

    private static bool FlagsMatch(Thing thing, IReadOnlyList<(string Flag, bool Set)> flags)
    {
        foreach (var flag in flags)
            if (thing.IsFlagSet(flag.Flag) != flag.Set) return false;
        return true;
    }

    private static bool IsFlagCategory(FindCategory category)
        => category is FindCategory.LinedefFlags or FindCategory.SidedefFlags or FindCategory.SectorFlags or FindCategory.ThingFlags;

    private static int ReplaceFlags(SearchLists lists, FindCategory category, string find, string replace)
    {
        if (!TryParseFlagQuery(find, out var findFlags) || !TryParseFlagQuery(replace, out var replaceFlags)) return 0;

        int changed = 0;
        switch (category)
        {
            case FindCategory.LinedefFlags:
                foreach (var line in lists.Linedefs)
                {
                    if (!FlagsMatch(line, findFlags)) continue;
                    foreach (var flag in replaceFlags) line.SetFlag(flag.Flag, flag.Set);
                    changed++;
                }
                break;
            case FindCategory.SidedefFlags:
                foreach (var side in lists.Sidedefs)
                {
                    if (!FlagsMatch(side, findFlags)) continue;
                    foreach (var flag in replaceFlags) side.SetFlag(flag.Flag, flag.Set);
                    changed++;
                }
                break;
            case FindCategory.SectorFlags:
                foreach (var sector in lists.Sectors)
                {
                    if (!FlagsMatch(sector, findFlags)) continue;
                    foreach (var flag in replaceFlags) sector.SetFlag(flag.Flag, flag.Set);
                    changed++;
                }
                break;
            case FindCategory.ThingFlags:
                foreach (var thing in lists.Things)
                {
                    if (!FlagsMatch(thing, findFlags)) continue;
                    foreach (var flag in replaceFlags) thing.SetFlag(flag.Flag, flag.Set);
                    changed++;
                }
                break;
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
