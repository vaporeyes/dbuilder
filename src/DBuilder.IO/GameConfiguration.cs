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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DBuilder.IO;

/// <summary>Metadata for one of a linedef action's / thing's 5 args: display name, type code, enum reference, default.</summary>
public sealed class ArgInfo
{
    public string Title { get; init; } = "";
    public int Type { get; init; }
    public string? Enum { get; init; }
    public int Default { get; init; }
    /// <summary>True when this arg slot is actually used (has a title).</summary>
    public bool Used => Title.Length > 0;
}

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
    public ArgInfo[] Args { get; init; } = System.Array.Empty<ArgInfo>();
}

public sealed class LinedefActionInfo
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
    public string Prefix { get; init; } = "";
    public string Category { get; init; } = "";
    public ArgInfo[] Args { get; init; } = System.Array.Empty<ArgInfo>();
}

public sealed class SectorEffectInfo
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
}

public sealed record StaticLimitsInfo(IReadOnlyDictionary<string, int> Values)
{
    public int Get(string name, int fallback = 0) => Values.TryGetValue(name, out int value) ? value : fallback;
}

public sealed record RequiredArchiveEntry(string Name, string? Lump, string? ClassName);

public sealed record RequiredArchiveInfo(string Name, string Filename, bool NeedExclude, IReadOnlyList<RequiredArchiveEntry> Entries);

public sealed class TextureSetInfo
{
    private readonly Regex regex;

    public TextureSetInfo(string key, string name, IReadOnlyList<string> filters)
    {
        Key = key;
        Name = name;
        Filters = filters;
        regex = BuildRegex(filters);
    }

    public string Key { get; }
    public string Name { get; }
    public IReadOnlyList<string> Filters { get; }

    public bool Matches(string textureName) => regex.IsMatch(textureName.ToUpperInvariant());

    private static Regex BuildRegex(IReadOnlyList<string> filters)
    {
        var pattern = new StringBuilder();
        foreach (string filter in filters)
        {
            if (pattern.Length > 0) pattern.Append('|');
            pattern.Append("(?:\\A");
            pattern.Append(WildcardToRegex(filter.ToUpperInvariant()));
            pattern.Append("\\Z)");
        }
        if (pattern.Length == 0) pattern.Append("\\Z\\A");
        return new Regex(pattern.ToString(), RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    private static string WildcardToRegex(string filter)
    {
        var pattern = new StringBuilder(filter.Length);
        foreach (char c in filter)
        {
            pattern.Append(c switch
            {
                '?' => ".",
                '*' => ".*?",
                _ => Regex.Escape(c.ToString()),
            });
        }
        return pattern.ToString();
    }
}

public sealed class GameConfiguration
{
    private readonly Dictionary<int, ThingTypeInfo> things = new();
    private readonly Dictionary<int, LinedefActionInfo> linedefActions = new();
    private readonly Dictionary<int, SectorEffectInfo> sectorEffects = new();
    private readonly Dictionary<int, string> linedefFlags = new();
    private readonly Dictionary<int, string> thingFlags = new();
    private readonly Dictionary<int, string> skills = new();
    private readonly Dictionary<string, Dictionary<int, string>> enums = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<GeneralizedCategory> genLinedefs = new();
    private readonly List<GeneralizedCategory> genSectors = new();
    private readonly List<FlagTranslation> linedefFlagsTranslation = new();
    private readonly List<FlagTranslation> thingFlagsTranslation = new();
    private readonly Dictionary<string, MapLumpInfo> mapLumpNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RequiredArchiveInfo> requiredArchives = new();
    private readonly List<TextureSetInfo> textureSets = new();
    private StaticLimitsInfo staticLimits = new(new Dictionary<string, int>());

    public IReadOnlyDictionary<int, ThingTypeInfo> Things => things;
    public IReadOnlyDictionary<int, LinedefActionInfo> LinedefActions => linedefActions;
    public IReadOnlyDictionary<int, SectorEffectInfo> SectorEffects => sectorEffects;

    /// <summary>Boom generalized linedef categories parsed from gen_linedeftypes (empty if not configured).</summary>
    public IReadOnlyList<GeneralizedCategory> GeneralizedLinedefs => genLinedefs;

    /// <summary>Boom generalized sector categories parsed from gen_sectortypes (empty if not configured).</summary>
    public IReadOnlyList<GeneralizedCategory> GeneralizedSectors => genSectors;

    /// <summary>Binary linedef flag bit -> UDMF field translations (empty if not configured).</summary>
    public IReadOnlyList<FlagTranslation> LinedefFlagsTranslation => linedefFlagsTranslation;

    /// <summary>Binary thing flag bit -> UDMF field translations (empty if not configured).</summary>
    public IReadOnlyList<FlagTranslation> ThingFlagsTranslation => thingFlagsTranslation;

    /// <summary>Lump name -> map lump description from the maplumpnames block (empty if not configured).</summary>
    public IReadOnlyDictionary<string, MapLumpInfo> MapLumpNames => mapLumpNames;

    public string DefaultSaveCompiler { get; private set; } = "";
    public string DefaultTestCompiler { get; private set; } = "";
    public string DefaultScriptCompiler { get; private set; } = "";
    public string NodeBuilderSave { get; private set; } = "";
    public string NodeBuilderTest { get; private set; } = "";
    public StaticLimitsInfo StaticLimits => staticLimits;
    public IReadOnlyList<RequiredArchiveInfo> RequiredArchives => requiredArchives;
    public IReadOnlyList<TextureSetInfo> TextureSets => textureSets;

    /// <summary>Linedef flag bit value -> display name (e.g. 1 -> "Impassable", 4 -> "Double Sided").</summary>
    public IReadOnlyDictionary<int, string> LinedefFlags => linedefFlags;

    /// <summary>Thing flag bit value -> display name (e.g. 1 -> "Easy", 8 -> "Ambush players").</summary>
    public IReadOnlyDictionary<int, string> ThingFlags => thingFlags;

    /// <summary>Skill level number -> display name (e.g. 1 -> "I'm too young to die").</summary>
    public IReadOnlyDictionary<int, string> Skills => skills;

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
            gc.DefaultSaveCompiler = GetString(root, "defaultsavecompiler", "");
            gc.DefaultTestCompiler = GetString(root, "defaulttestcompiler", "");
            gc.DefaultScriptCompiler = GetString(root, "defaultscriptcompiler", "");
            gc.NodeBuilderSave = GetString(root, "nodebuildersave", "");
            gc.NodeBuilderTest = GetString(root, "nodebuildertest", "");
            if (root["enums"] is IDictionary en) gc.ParseEnums(en);   // before types, so args can reference them
            if (root["thingtypes"] is IDictionary tt) gc.ParseThingTypes(tt);
            if (root["linedeftypes"] is IDictionary lt) gc.ParseLinedefTypes(lt);
            if (root["sectortypes"] is IDictionary st) gc.ParseSectorTypes(st);
            if (root["linedefflags"] is IDictionary lf) gc.ParseFlatIntStrings(lf, gc.linedefFlags);
            if (root["thingflags"] is IDictionary tf) gc.ParseFlatIntStrings(tf, gc.thingFlags);
            if (root["skills"] is IDictionary sk) gc.ParseFlatIntStrings(sk, gc.skills);
            if (root["gen_linedeftypes"] is IDictionary gl) gc.genLinedefs.AddRange(GeneralizedCategory.ParseBlock(gl));
            if (root["gen_sectortypes"] is IDictionary gs) gc.genSectors.AddRange(GeneralizedCategory.ParseBlock(gs));
            if (root["linedefflagstranslation"] is IDictionary lft) gc.ParseFlagTranslations(lft, gc.linedefFlagsTranslation);
            if (root["thingflagstranslation"] is IDictionary tft) gc.ParseFlagTranslations(tft, gc.thingFlagsTranslation);
            if (root["maplumpnames"] is IDictionary mln) gc.ParseMapLumpNames(mln);
            if (root["staticlimits"] is IDictionary sl) gc.staticLimits = ParseStaticLimits(sl);
            if (root["requiredarchives"] is IDictionary ra) gc.ParseRequiredArchives(ra);
            if (root["texturesets"] is IDictionary ts) gc.ParseTextureSets(ts);
        }
        return gc;
    }

    /// <summary>
    /// Merges DECORATE actor definitions into the thing catalog. Actors with an editor number override or add
    /// to existing entries (so mod things get titles/sprites/categories). Actors without a number are skipped.
    /// </summary>
    public void MergeActors(IEnumerable<ActorInfo> actors) => MergeActors(actors, null);

    /// <summary>
    /// Merges actors, assigning an editor number from <paramref name="doomEdNums"/> (MAPINFO num -&gt; class) when
    /// the actor itself has none (the ZScript case, where the class header carries no DoomEdNum).
    /// </summary>
    public void MergeActors(IEnumerable<ActorInfo> actors, IReadOnlyDictionary<int, string>? doomEdNums)
    {
        // Invert num->class to class->num so each actor can look up its own number.
        Dictionary<string, int>? classToNum = null;
        if (doomEdNums != null)
        {
            classToNum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (num, cls) in doomEdNums) classToNum[cls] = num;
        }

        foreach (var a in actors)
        {
            int num = a.DoomEdNum;
            if (num < 0 && classToNum != null && classToNum.TryGetValue(a.ClassName, out int mapped)) num = mapped;
            if (num < 0) continue;
            things[num] = new ThingTypeInfo
            {
                Index = num,
                ClassName = a.ClassName,
                Title = a.Title,
                Category = a.Category ?? "Decorate",
                Sprite = a.EditorSprite ?? "",
                Width = a.Radius > 0 ? a.Radius : 16,
                Height = a.Height > 0 ? a.Height : 16,
            };
        }
    }

    /// <summary>
    /// Applies parsed DeHackEd display data to the thing catalog. This only uses patch-local data that does not
    /// require the full UDB DehackedData baseline tables.
    /// </summary>
    public void MergeDehacked(DehackedPatch patch)
    {
        var spriteReplacements = BuildSpriteReplacementMap(patch);
        if (spriteReplacements.Count > 0)
        {
            var keys = new List<int>(things.Keys);
            foreach (int key in keys)
                things[key] = WithSprite(things[key], ApplySpriteReplacement(things[key].Sprite, spriteReplacements));
        }

        foreach (var thing in patch.Things)
        {
            if (!TryReadDehackedInt(thing.Properties, "ID #", out int doomEdNum) || doomEdNum < 0) continue;
            things.TryGetValue(doomEdNum, out var existing);
            string sprite = ResolveDehackedSprite(thing, patch) ?? existing?.Sprite ?? "";
            sprite = ApplySpriteReplacement(sprite, spriteReplacements);
            int width = TryReadDehackedInt(thing.Properties, "Width", out int rawWidth) ? FixedToInt(rawWidth) : existing?.Width ?? 16;
            int height = TryReadDehackedInt(thing.Properties, "Height", out int rawHeight) ? FixedToInt(rawHeight) : existing?.Height ?? 16;
            string category = ReadDehackedProperty(thing.Properties, "$Category") ?? existing?.Category ?? "Dehacked";

            things[doomEdNum] = new ThingTypeInfo
            {
                Index = doomEdNum,
                ClassName = existing?.ClassName ?? "DehackedThing" + thing.Number.ToString(CultureInfo.InvariantCulture),
                Title = thing.Name,
                Category = category,
                Sprite = sprite,
                Width = width > 0 ? width : existing?.Width ?? 16,
                Height = height > 0 ? height : existing?.Height ?? 16,
                Color = existing?.Color ?? 0,
                Args = existing?.Args ?? System.Array.Empty<ArgInfo>(),
            };
        }
    }

    private static Dictionary<string, string> BuildSpriteReplacementMap(DehackedPatch patch)
    {
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in patch.Texts)
            if (kvp.Key.Length == 4 && kvp.Value.Length == 4) replacements[kvp.Key] = kvp.Value;
        foreach (var kvp in patch.SpriteReplacements)
            replacements[kvp.Key] = kvp.Value;
        return replacements;
    }

    private static string ApplySpriteReplacement(string sprite, Dictionary<string, string> replacements)
    {
        if (sprite.Length < 4) return sprite;
        string baseName = sprite.Substring(0, 4);
        return replacements.TryGetValue(baseName, out string? replacement) ? replacement + sprite.Substring(4) : sprite;
    }

    private static ThingTypeInfo WithSprite(ThingTypeInfo info, string sprite) => new()
    {
        Index = info.Index,
        ClassName = info.ClassName,
        Title = info.Title,
        Category = info.Category,
        Sprite = sprite,
        Width = info.Width,
        Height = info.Height,
        Color = info.Color,
        Args = info.Args,
    };

    private static string? ResolveDehackedSprite(DehackedThing thing, DehackedPatch patch)
    {
        if (!TryReadDehackedInt(thing.Properties, "Initial frame", out int frameNumber)) return null;
        if (!patch.Frames.TryGetValue(frameNumber, out var frame)) return null;
        if (!TryReadDehackedInt(frame.Properties, "Sprite number", out int spriteNumber)) return null;
        if (!patch.NewSprites.TryGetValue(spriteNumber, out string? spriteName)) return null;
        int subNumber = 0;
        if (TryReadDehackedInt(frame.Properties, "Sprite subnumber", out int rawSubNumber))
        {
            subNumber = rawSubNumber >= 32768 ? rawSubNumber - 32768 : rawSubNumber;
        }
        char frameLetter = (char)('A' + Math.Clamp(subNumber, 0, 25));
        return spriteName + frameLetter + "0";
    }

    private static bool TryReadDehackedInt(Dictionary<string, string> properties, string key, out int value)
    {
        value = 0;
        return properties.TryGetValue(key, out string? text)
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string? ReadDehackedProperty(Dictionary<string, string> properties, string key)
        => properties.TryGetValue(key, out string? value) && value.Length > 0 ? value : null;

    private static int FixedToInt(int value) => value >> 16;

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
        return DescribeGeneralizedLinedef(index) ?? BoomGeneralized.Describe(index) ?? $"Unknown ({index})";
    }

    /// <summary>Decodes a generalized linedef number using the configured categories, or null if none matches.</summary>
    public string? DescribeGeneralizedLinedef(int action)
    {
        foreach (var cat in genLinedefs)
            if (cat.Contains(action)) return cat.Describe(action);
        return null;
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
                    Args = ParseArgs(thing),
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
                    Args = ParseArgs(action),
                };
            }
        }
    }

    // Parses up to 5 argN { title; type; enum; default } sub-dicts from a linedef action / thing entry.
    private static ArgInfo[] ParseArgs(IDictionary entry)
    {
        ArgInfo[]? args = null;
        for (int i = 0; i < 5; i++)
        {
            if (entry["arg" + i] is not IDictionary ad) continue;
            args ??= new ArgInfo[5];
            args[i] = new ArgInfo
            {
                Title = GetString(ad, "title", ""),
                Type = GetInt(ad, "type", 0),
                Enum = ad["enum"] as string,
                Default = GetInt(ad, "default", 0),
            };
        }
        if (args == null) return Array.Empty<ArgInfo>();
        for (int i = 0; i < 5; i++) args[i] ??= new ArgInfo();
        return args;
    }

    // Parses the "enums" block: each named enum maps int values to titles (flat "v = title" or nested "v { title }").
    private void ParseEnums(IDictionary enumsDict)
    {
        foreach (DictionaryEntry e in enumsDict)
        {
            string name = e.Key.ToString() ?? "";
            if (e.Value is not IDictionary vals) continue;
            var map = new Dictionary<int, string>();
            foreach (DictionaryEntry kv in vals)
            {
                if (!int.TryParse(kv.Key.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) continue;
                map[v] = kv.Value switch
                {
                    string s => s,
                    IDictionary d => GetString(d, "title", v.ToString(CultureInfo.InvariantCulture)),
                    _ => v.ToString(CultureInfo.InvariantCulture),
                };
            }
            if (map.Count > 0) enums[name] = map;
        }
    }

    /// <summary>The value-&gt;title map for a named enum, or null.</summary>
    public IReadOnlyDictionary<int, string>? GetEnum(string name)
        => enums.TryGetValue(name, out var m) ? m : null;

    /// <summary>The value-&gt;title map for an arg's enum, or null when the arg has none.</summary>
    public IReadOnlyDictionary<int, string>? GetArgEnum(ArgInfo arg)
        => arg.Enum != null ? GetEnum(arg.Enum) : null;

    /// <summary>True when a lump name is a configured map lump (excluding the ~MAP marker placeholder).</summary>
    public bool IsMapLump(string name)
        => mapLumpNames.TryGetValue(name, out var info) && !info.IsMarker;

    // Parses the maplumpnames block: each key is a lump name, each value its property sub-dict.
    private void ParseMapLumpNames(IDictionary block)
    {
        foreach (DictionaryEntry e in block)
        {
            string name = e.Key.ToString() ?? "";
            if (e.Value is not IDictionary d) continue;
            mapLumpNames[name] = new MapLumpInfo
            {
                Name = name,
                Required = GetBool(d, "required", false),
                BlindCopy = GetBool(d, "blindcopy", false),
                NodeBuild = GetBool(d, "nodebuild", false),
                AllowEmpty = GetBool(d, "allowempty", false),
                Forbidden = GetBool(d, "forbidden", false),
                Script = d["script"] as string,
            };
        }
    }

    // Parses a "<bit> = "<udmf spec>";" block into FlagTranslation entries (compound "a,b" / negated "!a").
    private void ParseFlagTranslations(IDictionary src, List<FlagTranslation> dest)
    {
        foreach (DictionaryEntry e in src)
        {
            if (!int.TryParse(e.Key.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int bit)) continue;
            if (e.Value is not string spec) continue;
            var ft = FlagTranslation.Parse(bit, spec);
            if (ft != null) dest.Add(ft);
        }
    }

    /// <summary>Converts a binary linedef flags value into the set of true UDMF flag field names.</summary>
    public ISet<string> LinedefFlagsToUdmf(int bits) => FlagsToUdmf(linedefFlagsTranslation, bits);

    /// <summary>Converts a binary thing flags value into the set of true UDMF flag field names.</summary>
    public ISet<string> ThingFlagsToUdmf(int bits) => FlagsToUdmf(thingFlagsTranslation, bits);

    /// <summary>Converts a set of true UDMF linedef flag names back into a binary flags value.</summary>
    public int LinedefFlagsFromUdmf(ICollection<string> flags) => FlagsFromUdmf(linedefFlagsTranslation, flags);

    /// <summary>Converts a set of true UDMF thing flag names back into a binary flags value.</summary>
    public int ThingFlagsFromUdmf(ICollection<string> flags) => FlagsFromUdmf(thingFlagsTranslation, flags);

    // A field is true iff its declared value equals whether its bit is set; only true fields are emitted.
    private static ISet<string> FlagsToUdmf(List<FlagTranslation> table, int bits)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ft in table)
        {
            bool bitSet = (bits & ft.Flag) == ft.Flag;
            for (int i = 0; i < ft.Fields.Count; i++)
                if (ft.Values[i] == bitSet) result.Add(ft.Fields[i]);
        }
        return result;
    }

    // A bit is set iff every one of its fields matches its declared value (present == true).
    private static int FlagsFromUdmf(List<FlagTranslation> table, ICollection<string> flags)
    {
        var set = new HashSet<string>(flags, StringComparer.OrdinalIgnoreCase);
        int bits = 0;
        foreach (var ft in table)
        {
            bool match = true;
            for (int i = 0; i < ft.Fields.Count; i++)
                if (set.Contains(ft.Fields[i]) != ft.Values[i]) { match = false; break; }
            if (match) bits |= ft.Flag;
        }
        return bits;
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

    private static StaticLimitsInfo ParseStaticLimits(IDictionary block)
    {
        var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry e in block)
        {
            string key = e.Key.ToString() ?? "";
            int value = e.Value switch
            {
                int i => i,
                long l => (int)l,
                double d => (int)d,
                string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
                _ => 0,
            };
            if (key.Length > 0) values[key] = value;
        }
        return new StaticLimitsInfo(values);
    }

    private void ParseRequiredArchives(IDictionary block)
    {
        foreach (DictionaryEntry e in block)
        {
            string name = e.Key.ToString() ?? "";
            if (e.Value is not IDictionary archive) continue;
            var entries = new List<RequiredArchiveEntry>();
            foreach (DictionaryEntry child in archive)
            {
                string entryName = child.Key.ToString() ?? "";
                if (child.Value is not IDictionary entry) continue;
                entries.Add(new RequiredArchiveEntry(entryName, entry["lump"] as string, entry["class"] as string));
            }
            requiredArchives.Add(new RequiredArchiveInfo(
                name,
                GetString(archive, "filename", "gzdoom.pk3"),
                GetBool(archive, "need_exclude", true),
                entries));
        }
    }

    private void ParseTextureSets(IDictionary block)
    {
        foreach (DictionaryEntry e in block)
        {
            string key = e.Key.ToString() ?? "";
            if (e.Value is not IDictionary set) continue;
            var filters = new List<string>();
            foreach (DictionaryEntry child in set)
            {
                string childKey = child.Key.ToString() ?? "";
                if (string.Equals(childKey, "name", StringComparison.OrdinalIgnoreCase)) continue;
                if (child.Value is not string filter) continue;
                filters.Add(filter.ToUpperInvariant());
            }
            textureSets.Add(new TextureSetInfo(key, GetString(set, "name", "Unnamed Set"), filters));
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

    private static bool GetBool(IDictionary d, string key, bool fallback)
        => d[key] switch
        {
            bool b => b,
            int i => i != 0,
            string s when bool.TryParse(s, out bool p) => p,
            _ => fallback,
        };
}
