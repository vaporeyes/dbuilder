// ABOUTME: Focused port of UDB's game configuration - parses a .cfg tree into thing-type, linedef-action and sector-effect catalogs.
// ABOUTME: Gives raw map numbers meaning ("3001" -> "Imp", action "11" -> "Exit Level"); a subset of UDB's 1473-line GameConfiguration.

/*
 * Schema (after include resolution by Configuration):
 *   thingtypes  { <category> { <category props>; <number> { title; sprite; class; ... } } }
 *   linedeftypes{ <category> { title?; <number> { title; prefix; } } }
 *   sectortypes { <number> = "title"; ... }   // flat
 *
 * Things inherit their category's scalar properties (color/width/height/...) unless they override them.
 * This port keeps the commonly-used display fields; the full UDB surface (args, flags, sprite frames,
 * generalized types, enums) can be layered on later.
 */

using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace DBuilder.IO;

public sealed class ThingTypeInfo
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
    public string Category { get; init; } = "";
    public string Sprite { get; init; } = "";
    public string ClassName { get; init; } = "";
    public int Color { get; init; }
    public int Width { get; init; } = 16;
    public int Height { get; init; } = 16;
}

public sealed class LinedefActionInfo
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
    public string Prefix { get; init; } = "";
    public string Category { get; init; } = "";
}

public sealed class SectorEffectInfo
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
}

public sealed class GameConfiguration
{
    private readonly Dictionary<int, ThingTypeInfo> things = new();
    private readonly Dictionary<int, LinedefActionInfo> linedefActions = new();
    private readonly Dictionary<int, SectorEffectInfo> sectorEffects = new();
    private readonly Dictionary<int, string> linedefFlags = new();
    private readonly Dictionary<int, string> thingFlags = new();

    public IReadOnlyDictionary<int, ThingTypeInfo> Things => things;
    public IReadOnlyDictionary<int, LinedefActionInfo> LinedefActions => linedefActions;
    public IReadOnlyDictionary<int, SectorEffectInfo> SectorEffects => sectorEffects;

    /// <summary>Linedef flag bit value -> display name (e.g. 1 -> "Impassable", 4 -> "Double Sided").</summary>
    public IReadOnlyDictionary<int, string> LinedefFlags => linedefFlags;

    /// <summary>Thing flag bit value -> display name (e.g. 1 -> "Easy", 8 -> "Ambush players").</summary>
    public IReadOnlyDictionary<int, string> ThingFlags => thingFlags;

    /// <summary>Loads a game configuration file (resolving its include() statements) into catalogs.</summary>
    public static GameConfiguration FromFile(string path)
    {
        var cfg = new Configuration(path);
        return FromConfiguration(cfg);
    }

    /// <summary>Builds catalogs from already-parsed configuration text (no include resolution).</summary>
    public static GameConfiguration FromText(string cfgText)
    {
        var cfg = new Configuration();
        cfg.InputConfiguration(cfgText);
        return FromConfiguration(cfg);
    }

    public static GameConfiguration FromConfiguration(Configuration cfg)
    {
        var gc = new GameConfiguration();
        if (cfg.Root is IDictionary root)
        {
            if (root["thingtypes"] is IDictionary tt) gc.ParseThingTypes(tt);
            if (root["linedeftypes"] is IDictionary lt) gc.ParseLinedefTypes(lt);
            if (root["sectortypes"] is IDictionary st) gc.ParseSectorTypes(st);
            if (root["linedefflags"] is IDictionary lf) gc.ParseFlatIntStrings(lf, gc.linedefFlags);
            if (root["thingflags"] is IDictionary tf) gc.ParseFlatIntStrings(tf, gc.thingFlags);
        }
        return gc;
    }

    public ThingTypeInfo? GetThing(int index) => things.TryGetValue(index, out var t) ? t : null;
    public LinedefActionInfo? GetLinedefAction(int index) => linedefActions.TryGetValue(index, out var a) ? a : null;
    public SectorEffectInfo? GetSectorEffect(int index) => sectorEffects.TryGetValue(index, out var s) ? s : null;

    /// <summary>Display title for a thing type, e.g. "Imp" or "Unknown (12345)".</summary>
    public string ThingTitle(int index) => GetThing(index)?.Title ?? $"Unknown ({index})";

    /// <summary>Display title for a linedef action, e.g. "Door Open Stay" or "Unknown (999)".
    /// Falls back to a decoded Boom generalized-type description for numbers in the generalized range.</summary>
    public string LinedefActionTitle(int index)
    {
        if (index == 0) return "None";
        var a = GetLinedefAction(index);
        if (a != null) return a.Title;
        return BoomGeneralized.Describe(index) ?? $"Unknown ({index})";
    }

    /// <summary>Display title for a sector effect, or "None"/"Unknown".</summary>
    public string SectorEffectTitle(int index)
        => GetSectorEffect(index)?.Title ?? (index == 0 ? "None" : $"Unknown ({index})");

    /// <summary>Names of the set bits in a linedef flags value, in ascending bit order.</summary>
    public IEnumerable<string> DescribeLinedefFlags(int flags) => DescribeFlags(linedefFlags, flags);

    /// <summary>Names of the set bits in a thing flags value, in ascending bit order.</summary>
    public IEnumerable<string> DescribeThingFlags(int flags) => DescribeFlags(thingFlags, flags);

    private static IEnumerable<string> DescribeFlags(Dictionary<int, string> defs, int flags)
    {
        var bits = new List<int>(defs.Keys);
        bits.Sort();
        foreach (int bit in bits)
            if (bit != 0 && (flags & bit) == bit) yield return defs[bit];
    }

    // ============================================================

    private void ParseThingTypes(IDictionary thingtypes)
    {
        foreach (DictionaryEntry catEntry in thingtypes)
        {
            string catName = catEntry.Key.ToString() ?? "";
            if (catEntry.Value is not IDictionary cat) continue;

            // Category-level defaults inherited by things in this category.
            int defColor = GetInt(cat, "color", 0);
            int defWidth = GetInt(cat, "width", 16);
            int defHeight = GetInt(cat, "height", 16);

            foreach (DictionaryEntry e in cat)
            {
                string key = e.Key.ToString() ?? "";
                if (e.Value is not IDictionary thing) continue;       // skip scalar category props
                if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) continue;

                things[number] = new ThingTypeInfo
                {
                    Index = number,
                    Category = catName,
                    Title = GetString(thing, "title", key),
                    Sprite = GetString(thing, "sprite", ""),
                    ClassName = GetString(thing, "class", ""),
                    Color = GetInt(thing, "color", defColor),
                    Width = GetInt(thing, "width", defWidth),
                    Height = GetInt(thing, "height", defHeight),
                };
            }
        }
    }

    private void ParseLinedefTypes(IDictionary linedeftypes)
    {
        foreach (DictionaryEntry catEntry in linedeftypes)
        {
            string catName = catEntry.Key.ToString() ?? "";
            if (catEntry.Value is not IDictionary cat) continue;
            string catTitle = GetString(cat, "title", catName);

            foreach (DictionaryEntry e in cat)
            {
                string key = e.Key.ToString() ?? "";
                if (e.Value is not IDictionary action) continue;
                if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) continue;

                linedefActions[number] = new LinedefActionInfo
                {
                    Index = number,
                    Category = catTitle,
                    Title = GetString(action, "title", key),
                    Prefix = GetString(action, "prefix", ""),
                };
            }
        }
    }

    private void ParseSectorTypes(IDictionary sectortypes)
    {
        foreach (DictionaryEntry e in sectortypes)
        {
            string key = e.Key.ToString() ?? "";
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) continue;
            if (e.Value is string title)
                sectorEffects[number] = new SectorEffectInfo { Index = number, Title = title };
        }
    }

    // Parses a flat "<int> = "<string>";" map (flags, etc.) into the destination dictionary.
    private void ParseFlatIntStrings(IDictionary src, Dictionary<int, string> dest)
    {
        foreach (DictionaryEntry e in src)
        {
            string key = e.Key.ToString() ?? "";
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) continue;
            if (e.Value is string s) dest[number] = s;
        }
    }

    // ---- scalar readers over the IDictionary tree ----

    private static int GetInt(IDictionary d, string key, int fallback)
    {
        var v = d[key];
        return v switch
        {
            int i => i,
            long l => (int)l,
            double db => (int)db,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) => p,
            _ => fallback,
        };
    }

    private static string GetString(IDictionary d, string key, string fallback)
        => d[key] is string s ? s : fallback;
}
