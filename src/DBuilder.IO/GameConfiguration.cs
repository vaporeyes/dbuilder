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
    public string ToolTip { get; init; } = "";
    public int Type { get; init; }
    public string? Enum { get; init; }
    public string? Flags { get; init; }
    public int Default { get; init; }
    public object DefaultValue { get; init; } = 0;
    public IReadOnlySet<string> TargetClasses { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public string RenderStyle { get; init; } = "";
    public int MinRange { get; init; }
    public int MaxRange { get; init; }
    public bool Str { get; init; }
    public string TitleStr { get; init; } = "";
    /// <summary>True when this arg slot is actually used (has a title).</summary>
    public bool Used => Title.Length > 0;
}

public sealed record EnumItemInfo(string Value, string Title) : IComparable<EnumItemInfo>
{
    public override string ToString() => Title;

    public int CompareTo(EnumItemInfo? other)
    {
        if (other == null) return 1;
        return GetIntValue().CompareTo(other.GetIntValue());
    }

    public int GetIntValue()
        => int.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
}

public sealed class EnumListInfo
{
    private readonly List<EnumItemInfo> items = new();

    public EnumListInfo(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public IReadOnlyList<EnumItemInfo> Items => items;

    internal void Add(EnumItemInfo item) => items.Add(item);

    public EnumItemInfo? GetByEnumIndex(string value)
    {
        foreach (var item in items)
            if (item.Value == value) return item;
        return null;
    }
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
    public double Alpha { get; init; } = 1.0;
    public string RenderStyle { get; init; } = "normal";
    public bool Arrow { get; init; }
    public bool Hangs { get; init; }
    public int Blocking { get; init; }
    public int ErrorCheck { get; init; }
    public bool FixedSize { get; init; }
    public bool FixedRotation { get; init; }
    public bool AbsoluteZ { get; init; }
    public double SpriteScale { get; init; } = 1.0;
    public bool LockSprite { get; init; }
    public int ThingLink { get; init; }
    public bool Optional { get; init; }
    public bool IsKnown { get; init; } = true;
    public bool IsNull => Index == 0;
    public IReadOnlyList<string> AddUniversalFields { get; init; } = Array.Empty<string>();
    public ArgInfo[] Args { get; init; } = System.Array.Empty<ArgInfo>();

    public bool HasAdditionalUniversalField(string fieldName)
    {
        foreach (string field in AddUniversalFields)
            if (string.Equals(field, fieldName, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}

public sealed record ThingCategoryInfo(
    string Key,
    string Title,
    string? ParentKey,
    int Color,
    int Width,
    int Height,
    double Alpha,
    string RenderStyle,
    string Sprite,
    bool Sorted,
    int Arrow,
    int Hangs,
    int Blocking,
    int ErrorCheck,
    bool FixedSize,
    bool FixedRotation,
    bool AbsoluteZ,
    double SpriteScale,
    bool Optional);

public sealed class LinedefActionInfo
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
    public string DisplayTitle { get; init; } = "";
    public string Name { get; init; } = "";
    public string Id { get; init; } = "";
    public string Prefix { get; init; } = "";
    public string Category { get; init; } = "";
    public string CategoryKey { get; init; } = "";
    public ArgInfo[] Args { get; init; } = System.Array.Empty<ArgInfo>();
    public bool IsKnown { get; init; } = true;
    public bool IsGeneralized { get; init; }
    public bool IsNull => Index == 0;
    public bool RequiresActivation { get; init; } = true;
    public bool LineToLineTag { get; init; }
    public bool LineToLineSameAction { get; init; }
    public LinedefActionErrorCheckerExemptions ErrorChecker { get; init; } = new();
}

public sealed record LinedefActionErrorCheckerExemptions(
    bool IgnoreUpperTexture = false,
    bool IgnoreMiddleTexture = false,
    bool IgnoreLowerTexture = false,
    bool RequiresUpperTexture = false,
    bool FloorLowerToLowest = false,
    bool FloorRaiseToNextHigher = false,
    bool FloorRaiseToHighest = false);

public sealed class LinedefActionCategoryInfo
{
    private readonly List<int> actions = new();

    public LinedefActionCategoryInfo(string key, string title)
    {
        Key = key;
        Title = title;
    }

    public string Key { get; }
    public string Title { get; }
    public IReadOnlyList<int> Actions => actions;

    internal void Add(int action) => actions.Add(action);
}

public sealed class SectorEffectInfo
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
    public bool IsKnown { get; init; } = true;
    public bool IsGeneralized { get; init; }
    public bool IsNull => Index == 0;
}

public sealed class SectorEffectDataInfo
{
    private readonly HashSet<int> generalizedBits = new();

    public int Effect { get; internal set; }
    public IReadOnlySet<int> GeneralizedBits => generalizedBits;

    internal void AddGeneralizedBit(int bit) => generalizedBits.Add(bit);
}

public sealed record StaticLimitsInfo(IReadOnlyDictionary<string, int> Values)
{
    public int Get(string name, int fallback = 0) => Values.TryGetValue(name, out int value) ? value : fallback;
}

public sealed record RequiredArchiveEntry(string Name, string? Lump, string? ClassName);

public sealed record RequiredArchiveInfo(string Name, string Filename, bool NeedExclude, IReadOnlyList<RequiredArchiveEntry> Entries);

public sealed record LinedefActivationInfo(string Key, int Index, string Title, bool IsTrigger);

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
    private readonly Dictionary<string, ThingCategoryInfo> thingCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, LinedefActionInfo> linedefActions = new();
    private readonly Dictionary<string, LinedefActionCategoryInfo> linedefActionCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, SectorEffectInfo> sectorEffects = new();
    private readonly Dictionary<int, string> linedefFlags = new();
    private readonly Dictionary<int, string> thingFlags = new();
    private readonly Dictionary<int, string> skills = new();
    private readonly Dictionary<string, Dictionary<int, string>> enums = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EnumListInfo> enumLists = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<GeneralizedCategory> genLinedefs = new();
    private readonly List<GeneralizedCategory> genSectors = new();
    private readonly List<GeneralizedOption> genSectorEffects = new();
    private readonly List<FlagTranslation> linedefFlagsTranslation = new();
    private readonly List<FlagTranslation> thingFlagsTranslation = new();
    private readonly Dictionary<string, MapLumpInfo> mapLumpNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RequiredArchiveInfo> requiredArchives = new();
    private readonly List<LinedefActivationInfo> linedefActivations = new();
    private readonly List<TextureSetInfo> textureSets = new();
    private StaticLimitsInfo staticLimits = new(new Dictionary<string, int>());

    public IReadOnlyDictionary<int, ThingTypeInfo> Things => things;
    public IReadOnlyDictionary<string, ThingCategoryInfo> ThingCategories => thingCategories;
    public IReadOnlyDictionary<int, LinedefActionInfo> LinedefActions => linedefActions;
    public IReadOnlyDictionary<string, LinedefActionCategoryInfo> LinedefActionCategories => linedefActionCategories;
    public IReadOnlyDictionary<int, SectorEffectInfo> SectorEffects => sectorEffects;

    /// <summary>Boom generalized linedef categories parsed from gen_linedeftypes (empty if not configured).</summary>
    public IReadOnlyList<GeneralizedCategory> GeneralizedLinedefs => genLinedefs;

    /// <summary>Boom generalized sector categories parsed from gen_sectortypes (empty if not configured).</summary>
    public IReadOnlyList<GeneralizedCategory> GeneralizedSectors => genSectors;

    /// <summary>Boom generalized sector effect options parsed from gen_sectortypes.</summary>
    public IReadOnlyList<GeneralizedOption> GeneralizedSectorEffects => genSectorEffects;

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
    public IReadOnlyList<LinedefActivationInfo> LinedefActivations => linedefActivations;
    public IReadOnlyList<TextureSetInfo> TextureSets => textureSets;

    /// <summary>Linedef flag bit value -> display name (e.g. 1 -> "Impassable", 4 -> "Double Sided").</summary>
    public IReadOnlyDictionary<int, string> LinedefFlags => linedefFlags;

    /// <summary>Thing flag bit value -> display name (e.g. 1 -> "Easy", 8 -> "Ambush players").</summary>
    public IReadOnlyDictionary<int, string> ThingFlags => thingFlags;

    /// <summary>Skill level number -> display name (e.g. 1 -> "I'm too young to die").</summary>
    public IReadOnlyDictionary<int, string> Skills => skills;

    /// <summary>Named enum lists with string values and titles, matching UDB's enum metadata model.</summary>
    public IReadOnlyDictionary<string, EnumListInfo> EnumLists => enumLists;

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
            if (root["linedefactivations"] is IDictionary la) gc.ParseLinedefActivations(la);
            if (root["thingflags"] is IDictionary tf) gc.ParseFlatIntStrings(tf, gc.thingFlags);
            if (root["skills"] is IDictionary sk) gc.ParseFlatIntStrings(sk, gc.skills);
            if (root["gen_linedeftypes"] is IDictionary gl) gc.genLinedefs.AddRange(GeneralizedCategory.ParseBlock(gl));
            if (root["gen_sectortypes"] is IDictionary gs) gc.genSectorEffects.AddRange(GeneralizedOption.ParseOptionsBlock(gs));
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
            things.TryGetValue(num, out var existing);
            things[num] = new ThingTypeInfo
            {
                Index = num,
                ClassName = a.ClassName,
                Title = ActorTitle(a),
                Category = a.Category ?? "Decorate",
                Sprite = a.EditorSprite ?? "",
                Width = a.Radius > 0 ? a.Radius : 16,
                Height = a.Height > 0 ? a.Height : 16,
                Alpha = ActorAlpha(a),
                RenderStyle = ActorRenderStyle(a),
                SpriteScale = ActorSpriteScale(a),
                Color = ActorColor(a),
                Arrow = ActorArrow(a),
                Hangs = ActorFlag(a, "spawnceiling"),
                Blocking = ActorFlag(a, "solid") ? 2 : 0,
                ErrorCheck = ActorFlag(a, "solid") ? 1 : 0,
                Args = ActorArgs(a, existing?.Args),
            };
        }
    }

    private static string ActorTitle(ActorInfo actor)
    {
        if (!string.Equals(actor.Title, actor.ClassName, StringComparison.Ordinal)) return actor.Title;
        if (TryActorProperty(actor, "tag", out string? tag) && !tag.StartsWith("$", StringComparison.Ordinal)) return tag;
        return actor.ClassName;
    }

    private static double ActorAlpha(ActorInfo actor)
        => TryActorPropertyDouble(actor, "alpha", out double alpha) ? Math.Clamp(alpha, 0.0, 1.0) : 1.0;

    private static string ActorRenderStyle(ActorInfo actor)
        => actor.Properties.ContainsKey("$ignorerenderstyle")
            ? "normal"
            : TryActorProperty(actor, "renderstyle", out string? style) ? style.ToLowerInvariant() : "normal";

    private static double ActorSpriteScale(ActorInfo actor)
    {
        if (TryActorPropertyDouble(actor, "xscale", out double xscale)) return xscale;
        if (TryActorPropertyDouble(actor, "scale", out double scale)) return scale;
        return 1.0;
    }

    private static int ActorColor(ActorInfo actor)
    {
        if (!TryActorPropertyInt(actor, "$color", out int color)) return 0;
        return color == 0 || color > 19 ? 18 : color;
    }

    private static bool ActorArrow(ActorInfo actor)
    {
        if (actor.Properties.ContainsKey("$angled")) return true;
        if (actor.Properties.ContainsKey("$notangled")) return false;
        return false;
    }

    private static bool ActorFlag(ActorInfo actor, string flag)
        => actor.Flags.TryGetValue(flag, out bool enabled) && enabled;

    private static ArgInfo[] ActorArgs(ActorInfo actor, ArgInfo[]? existing)
    {
        ArgInfo[]? args = null;
        for (int i = 0; i < 5; i++)
        {
            string prefix = "$arg" + i.ToString(CultureInfo.InvariantCulture);
            if (!TryActorProperty(actor, prefix, out string title)) continue;
            args ??= new ArgInfo[5];
            args[i] = new ArgInfo
            {
                Title = title,
                ToolTip = ActorProperty(actor, prefix + "tooltip").Replace("\\n", Environment.NewLine),
                Type = ActorPropertyInt(actor, prefix + "type"),
                Enum = EmptyToNull(ActorProperty(actor, prefix + "enum")),
                Default = ActorPropertyInt(actor, prefix + "default"),
                DefaultValue = ActorPropertyInt(actor, prefix + "default"),
                TargetClasses = ParseTargetClasses(ActorProperty(actor, prefix + "targetclasses")),
                RenderStyle = ActorProperty(actor, prefix + "renderstyle").ToLowerInvariant(),
                MinRange = ActorPropertyInt(actor, prefix + "minrange"),
                MaxRange = ActorPropertyInt(actor, prefix + "maxrange"),
                Str = actor.Properties.ContainsKey(prefix + "str"),
                TitleStr = ActorProperty(actor, prefix + "str") is { Length: > 0 } titleStr ? titleStr : title,
            };
        }

        if (args == null) return existing ?? Array.Empty<ArgInfo>();
        for (int i = 0; i < 5; i++) args[i] ??= existing != null && i < existing.Length ? existing[i] : new ArgInfo();
        return args;
    }

    private static bool TryActorProperty(ActorInfo actor, string name, out string value)
    {
        value = "";
        if (!actor.Properties.TryGetValue(name, out var values) || values.Count == 0) return false;
        value = values[0];
        return true;
    }

    private static bool TryActorPropertyDouble(ActorInfo actor, string name, out double value)
    {
        value = 0.0;
        return TryActorProperty(actor, name, out string raw)
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryActorPropertyInt(ActorInfo actor, string name, out int value)
    {
        value = 0;
        return TryActorProperty(actor, name, out string raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string ActorProperty(ActorInfo actor, string name)
        => TryActorProperty(actor, name, out string value) ? value : "";

    private static int ActorPropertyInt(ActorInfo actor, string name)
        => TryActorPropertyInt(actor, name, out int value) ? value : 0;

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

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
                Alpha = existing?.Alpha ?? 1.0,
                RenderStyle = existing?.RenderStyle ?? "normal",
                Arrow = existing?.Arrow ?? false,
                Hangs = existing?.Hangs ?? false,
                Blocking = existing?.Blocking ?? 0,
                ErrorCheck = existing?.ErrorCheck ?? 0,
                FixedSize = existing?.FixedSize ?? false,
                FixedRotation = existing?.FixedRotation ?? false,
                AbsoluteZ = existing?.AbsoluteZ ?? false,
                SpriteScale = existing?.SpriteScale ?? 1.0,
                LockSprite = existing?.LockSprite ?? false,
                ThingLink = existing?.ThingLink ?? 0,
                Optional = existing?.Optional ?? false,
                IsKnown = existing?.IsKnown ?? true,
                AddUniversalFields = existing?.AddUniversalFields ?? Array.Empty<string>(),
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
        Alpha = info.Alpha,
        RenderStyle = info.RenderStyle,
        Arrow = info.Arrow,
        Hangs = info.Hangs,
        Blocking = info.Blocking,
        ErrorCheck = info.ErrorCheck,
        FixedSize = info.FixedSize,
        FixedRotation = info.FixedRotation,
        AbsoluteZ = info.AbsoluteZ,
        SpriteScale = info.SpriteScale,
        LockSprite = info.LockSprite,
        ThingLink = info.ThingLink,
        Optional = info.Optional,
        IsKnown = info.IsKnown,
        AddUniversalFields = info.AddUniversalFields,
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
    {
        var effect = GetSectorEffect(index);
        if (effect != null) return effect.Title;
        if (index == 0) return "None";
        return DescribeGeneralizedSectorEffect(index) ?? $"Unknown ({index})";
    }

    public bool IsGeneralizedSectorEffect(int effect)
        => IsGeneralizedSectorEffect(effect, genSectorEffects);

    public static bool IsGeneralizedSectorEffect(int effect, IReadOnlyList<GeneralizedOption> options)
    {
        if (effect == 0) return false;
        int current = effect;
        for (int i = options.Count - 1; i >= 0; i--)
        {
            for (int j = options[i].Bits.Count - 1; j >= 0; j--)
            {
                GeneralizedBit bit = options[i].Bits[j];
                if (bit.Value > current) continue;
                if (bit.Value > 0 && (current & bit.Value) == bit.Value) return true;
                current -= bit.Value;
            }
        }

        return false;
    }

    public SectorEffectDataInfo GetSectorEffectData(int effect)
    {
        var result = new SectorEffectDataInfo();
        if (effect <= 0) return result;

        int current = effect;
        for (int i = genSectorEffects.Count - 1; i >= 0; i--)
        {
            for (int j = genSectorEffects[i].Bits.Count - 1; j >= 0; j--)
            {
                GeneralizedBit bit = genSectorEffects[i].Bits[j];
                if (bit.Value > 0 && (current & bit.Value) == bit.Value)
                {
                    current -= bit.Value;
                    result.AddGeneralizedBit(bit.Value);
                }
            }
        }

        if (current > 0) result.Effect = current;
        return result;
    }

    public string? DescribeGeneralizedSectorEffect(int effect)
    {
        if (effect == 0) return "None";
        string title = "Unknown generalized effect";
        int matches = 0;
        int nonGeneralizedEffect = effect;

        for (int i = genSectorEffects.Count - 1; i >= 0; i--)
        {
            for (int j = genSectorEffects[i].Bits.Count - 1; j >= 0; j--)
            {
                GeneralizedBit bit = genSectorEffects[i].Bits[j];
                if (bit.Value > 0 && (effect & bit.Value) == bit.Value)
                {
                    title = genSectorEffects[i].Name + ": " + bit.Title;
                    nonGeneralizedEffect -= bit.Value;
                    matches++;
                    break;
                }
            }
        }

        if (matches == 0) return null;
        string generalizedTitle = matches > 1 ? $"Generalized ({matches} effects)" : title;
        if (nonGeneralizedEffect <= 0) return generalizedTitle;
        if (sectorEffects.TryGetValue(nonGeneralizedEffect, out var known))
            return known.Title + " + " + generalizedTitle;
        return "Unknown effect + " + generalizedTitle;
    }

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
            ParseThingCategory(catName, cat, null);
        }
    }

    private void ParseThingCategory(string key, IDictionary cat, ThingCategoryInfo? parent)
    {
        var info = new ThingCategoryInfo(
            key,
            GetString(cat, "title", key),
            parent?.Key,
            GetInt(cat, "color", parent?.Color ?? 0),
            GetInt(cat, "width", parent?.Width ?? 16),
            GetInt(cat, "height", parent?.Height ?? 16),
            Math.Clamp(GetDouble(cat, "alpha", parent?.Alpha ?? 1.0), 0.0, 1.0),
            GetString(cat, "renderstyle", parent?.RenderStyle ?? "normal").ToLowerInvariant(),
            GetString(cat, "sprite", parent?.Sprite ?? ""),
            GetBoolishInt(cat, "sort", parent?.Sorted ?? false),
            GetInt(cat, "arrow", parent?.Arrow ?? 0),
            GetInt(cat, "hangs", parent?.Hangs ?? 0),
            GetInt(cat, "blocking", parent?.Blocking ?? 0),
            GetInt(cat, "error", parent?.ErrorCheck ?? 1),
            GetBool(cat, "fixedsize", parent?.FixedSize ?? false),
            GetBool(cat, "fixedrotation", parent?.FixedRotation ?? false),
            GetBool(cat, "absolutez", parent?.AbsoluteZ ?? false),
            GetDouble(cat, "spritescale", parent?.SpriteScale ?? 1.0),
            GetBool(cat, "optional", parent?.Optional ?? false));
        thingCategories[key] = info;

        foreach (DictionaryEntry e in cat)
        {
            string childKey = e.Key.ToString() ?? "";
            if (e.Value is not IDictionary child) continue;
            if (int.TryParse(childKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                things[number] = new ThingTypeInfo
                {
                    Index = number,
                    Category = key,
                    Title = GetString(child, "title", childKey),
                    Sprite = GetString(child, "sprite", info.Sprite),
                    ClassName = GetString(child, "class", ""),
                    Color = GetInt(child, "color", info.Color),
                    Width = GetInt(child, "width", info.Width),
                    Height = GetInt(child, "height", info.Height),
                    Alpha = Math.Clamp(GetDouble(child, "alpha", info.Alpha), 0.0, 1.0),
                    RenderStyle = GetString(child, "renderstyle", info.RenderStyle).ToLowerInvariant(),
                    Arrow = GetBoolishInt(child, "arrow", info.Arrow != 0),
                    Hangs = GetBoolishInt(child, "hangs", info.Hangs != 0),
                    Blocking = GetInt(child, "blocking", info.Blocking),
                    ErrorCheck = GetInt(child, "error", info.ErrorCheck),
                    FixedSize = GetBool(child, "fixedsize", info.FixedSize),
                    FixedRotation = GetBool(child, "fixedrotation", info.FixedRotation),
                    AbsoluteZ = GetBool(child, "absolutez", info.AbsoluteZ),
                    SpriteScale = GetDouble(child, "spritescale", info.SpriteScale),
                    LockSprite = GetBool(child, "locksprite", false),
                    ThingLink = GetInt(child, "thinglink", 0),
                    Optional = GetBool(child, "optional", info.Optional),
                    AddUniversalFields = ParseAddUniversalFields(child),
                    Args = ParseArgs(child),
                };
            }
            else
            {
                ParseThingCategory(key + "." + childKey, child, info);
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
            var category = new LinedefActionCategoryInfo(catName, catTitle);
            linedefActionCategories[catName] = category;

            foreach (DictionaryEntry e in cat)
            {
                string key = e.Key.ToString() ?? "";
                if (e.Value is not IDictionary action) continue;
                if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) continue;
                string name = GetString(action, "title", key);
                string prefix = GetString(action, "prefix", "");

                linedefActions[number] = new LinedefActionInfo
                {
                    Index = number,
                    Category = catTitle,
                    CategoryKey = catName,
                    Name = name,
                    Title = name,
                    DisplayTitle = (prefix + " " + name).Trim(),
                    Id = GetString(action, "id", ""),
                    Prefix = prefix,
                    RequiresActivation = GetBool(action, "requiresactivation", true),
                    LineToLineTag = GetBool(action, "linetolinetag", false),
                    LineToLineSameAction = GetBool(action, "linetolinesameaction", false),
                    ErrorChecker = ParseLinedefActionErrorChecker(action),
                    Args = ParseArgs(action),
                };
                category.Add(number);
            }
        }
    }

    private static LinedefActionErrorCheckerExemptions ParseLinedefActionErrorChecker(IDictionary action)
    {
        if (action["errorchecker"] is not IDictionary errorchecker) return new LinedefActionErrorCheckerExemptions();
        return new LinedefActionErrorCheckerExemptions(
            GetBool(errorchecker, "ignoreuppertexture", false),
            GetBool(errorchecker, "ignoremiddletexture", false),
            GetBool(errorchecker, "ignorelowertexture", false),
            GetBool(errorchecker, "requiresuppertexture", false),
            GetBool(errorchecker, "floorlowertolowest", false),
            GetBool(errorchecker, "floorraisetonexthigher", false),
            GetBool(errorchecker, "floorraisetohighest", false));
    }

    // Parses up to 5 argN { title; type; enum; default } sub-dicts from a linedef action / thing entry.
    private static ArgInfo[] ParseArgs(IDictionary entry)
    {
        ArgInfo[]? args = null;
        for (int i = 0; i < 5; i++)
        {
            if (entry["arg" + i] is not IDictionary ad) continue;
            args ??= new ArgInfo[5];
            string title = GetString(ad, "title", "");
            args[i] = new ArgInfo
            {
                Title = title,
                ToolTip = GetString(ad, "tooltip", "").Replace("\\n", Environment.NewLine),
                Type = GetInt(ad, "type", 0),
                Enum = ad["enum"] as string,
                Flags = ad["flags"] as string,
                Default = GetInt(ad, "default", 0),
                DefaultValue = ad["default"] ?? 0,
                TargetClasses = ParseTargetClasses(GetString(ad, "targetclasses", "")),
                RenderStyle = GetString(ad, "renderstyle", "").ToLowerInvariant(),
                MinRange = GetInt(ad, "minrange", 0),
                MaxRange = GetInt(ad, "maxrange", 0),
                Str = GetBool(ad, "str", false),
                TitleStr = GetString(ad, "titlestr", title),
            };
        }
        if (args == null) return Array.Empty<ArgInfo>();
        for (int i = 0; i < 5; i++) args[i] ??= new ArgInfo();
        return args;
    }

    // Parses the "enums" block: UDB stores string value/title pairs; the int map is kept for existing callers.
    private void ParseEnums(IDictionary enumsDict)
    {
        foreach (DictionaryEntry e in enumsDict)
        {
            string name = e.Key.ToString() ?? "";
            if (e.Value is not IDictionary vals) continue;
            var list = new EnumListInfo(name);
            var map = new Dictionary<int, string>();
            foreach (DictionaryEntry kv in vals)
            {
                string value = kv.Key.ToString() ?? "";
                string title = kv.Value switch
                {
                    string s => s,
                    IDictionary d => GetString(d, "title", value),
                    _ => kv.Value?.ToString() ?? value,
                };
                list.Add(new EnumItemInfo(value, title));
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                    map[intValue] = title;
            }
            if (list.Items.Count > 0) enumLists[name] = list;
            if (map.Count > 0) enums[name] = map;
        }
    }

    /// <summary>The string value/title list for a named enum, or null.</summary>
    public EnumListInfo? GetEnumList(string name)
        => enumLists.TryGetValue(name, out var list) ? list : null;

    /// <summary>The value-&gt;title map for a named enum, or null.</summary>
    public IReadOnlyDictionary<int, string>? GetEnum(string name)
        => enums.TryGetValue(name, out var m) ? m : null;

    /// <summary>The string value/title list for an arg's enum, or null when the arg has none.</summary>
    public EnumListInfo? GetArgEnumList(ArgInfo arg)
        => arg.Enum != null ? GetEnumList(arg.Enum) : null;

    /// <summary>The value-&gt;title map for an arg's enum, or null when the arg has none.</summary>
    public IReadOnlyDictionary<int, string>? GetArgEnum(ArgInfo arg)
        => arg.Enum != null ? GetEnum(arg.Enum) : null;

    public EnumListInfo? GetArgFlagsList(ArgInfo arg)
        => arg.Flags != null ? GetEnumList(arg.Flags) : null;

    public IReadOnlyDictionary<int, string>? GetArgFlags(ArgInfo arg)
        => arg.Flags != null ? GetEnum(arg.Flags) : null;

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

    private void ParseLinedefActivations(IDictionary block)
    {
        foreach (DictionaryEntry e in block)
        {
            string key = e.Key.ToString() ?? "";
            if (key.Length == 0) continue;
            if (e.Value is IDictionary activation)
            {
                linedefActivations.Add(new LinedefActivationInfo(
                    key,
                    ParseActivationIndex(key),
                    GetString(activation, "name", key),
                    GetBool(activation, "istrigger", true)));
            }
            else
            {
                linedefActivations.Add(new LinedefActivationInfo(
                    key,
                    ParseActivationIndex(key),
                    e.Value?.ToString() ?? key,
                    true));
            }
        }
    }

    private static int ParseActivationIndex(string key)
        => int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) ? index : 0;

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

    private static double GetDouble(IDictionary d, string key, double fallback)
        => d[key] switch
        {
            double db => db,
            float f => f,
            int i => i,
            long l => l,
            string s when double.TryParse(s.TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out double p) => p,
            _ => fallback,
        };

    private static bool GetBoolishInt(IDictionary d, string key, bool fallback)
        => d[key] switch
        {
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) => p != 0,
            string s when bool.TryParse(s, out bool p) => p,
            _ => fallback,
        };

    private static IReadOnlyList<string> ParseAddUniversalFields(IDictionary entry)
    {
        if (entry["adduniversalfields"] is not IDictionary fields) return Array.Empty<string>();
        var result = new List<string>();
        foreach (DictionaryEntry field in fields)
        {
            string name = field.Key.ToString()?.ToLowerInvariant() ?? "";
            if (name.Length > 0 && !result.Contains(name, StringComparer.OrdinalIgnoreCase)) result.Add(name);
        }
        return result;
    }

    private static IReadOnlySet<string> ParseTargetClasses(string value)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string item in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = item.Trim();
            if (trimmed.Length > 0) result.Add(trimmed);
        }
        return result;
    }
}
