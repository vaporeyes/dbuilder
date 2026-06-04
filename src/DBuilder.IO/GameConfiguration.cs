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

public sealed record ArgColor(byte R, byte G, byte B, byte A);

public sealed record ResourceRangeInfo(string Name, string Start, string End);

/// <summary>Metadata for one of a linedef action's / thing's 5 args: display name, type code, enum reference, default.</summary>
public sealed class ArgInfo
{
    public string Title { get; init; } = "";
    public string ToolTip { get; init; } = "";
    public int Type { get; init; }
    public string? Enum { get; init; }
    public string? Flags { get; init; }
    public IReadOnlyList<EnumItemInfo> InlineEnumItems { get; init; } = Array.Empty<EnumItemInfo>();
    public IReadOnlyList<EnumItemInfo> InlineFlagsItems { get; init; } = Array.Empty<EnumItemInfo>();
    public int Default { get; init; }
    public object DefaultValue { get; init; } = 0;
    public IReadOnlySet<string> TargetClasses { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public string RenderStyle { get; init; } = "";
    public ArgColor? RenderColor { get; init; }
    public int MinRange { get; init; }
    public int MaxRange { get; init; }
    public ArgColor? MinRangeColor { get; init; }
    public ArgColor? MaxRangeColor { get; init; }
    public bool Str { get; init; }
    public string TitleStr { get; init; } = "";
    /// <summary>True when this arg slot is configured by the game config or actor metadata.</summary>
    public bool Used { get; init; }
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

public enum ThingRenderMode
{
    Normal,
    Model,
    Voxel,
    WallSprite,
    FlatSprite,
}

public sealed class ThingTypeInfo
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
    public string Category { get; init; } = "";
    public string Sprite { get; init; } = "";
    public string LightName { get; init; } = "";
    public string ClassName { get; init; } = "";
    public int Color { get; init; }
    public int Width { get; init; } = 16;
    public double RenderRadius { get; init; } = 10.0;
    public double DistanceCheckSq { get; init; } = double.MaxValue;
    public int Height { get; init; } = 16;
    public double Alpha { get; init; } = 1.0;
    public string RenderStyle { get; init; } = "normal";
    public bool Bright { get; init; }
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
    public bool IsObsolete { get; init; }
    public string ObsoleteMessage { get; init; } = "";
    public bool XYBillboard { get; init; }
    public ThingRenderMode RenderMode { get; init; }
    public bool RollSprite { get; init; }
    public bool RollCenter { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> FlagsRename { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
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
    string LightName,
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
    public const int DefaultVisplanes = 128;
    public const int DefaultDrawsegs = 256;
    public const int DefaultSolidsegs = 32;
    public const int DefaultOpenings = 320 * 64;

    public int Get(string name, int fallback = 0) => Values.TryGetValue(name, out int value) ? value : fallback;

    public int Visplanes => Get("visplanes", DefaultVisplanes);
    public int Drawsegs => Get("drawsegs", DefaultDrawsegs);
    public int Solidsegs => Get("solidsegs", DefaultSolidsegs);
    public int Openings => Get("openings", DefaultOpenings);

    public byte InterpolateVisplanes(byte value)
    {
        int visplanes = Visplanes;
        if (visplanes <= 0 || visplanes == DefaultVisplanes) return value;

        double scaled = DefaultVisplanes * value / (double)visplanes;
        return (byte)Math.Ceiling(scaled);
    }
}

public sealed record RequiredArchiveEntry(string Name, string? Lump, string? ClassName);

public sealed record RequiredArchiveInfo(string Name, string Filename, bool NeedExclude, IReadOnlyList<RequiredArchiveEntry> Entries);

public sealed record LinedefActivationInfo(string Key, int Index, string Title, bool IsTrigger);

public sealed record ThingFlagCompareInfo(
    string Flag,
    string CompareMethod,
    bool Invert,
    IReadOnlySet<string> RequiredGroups,
    IReadOnlySet<string> IgnoredGroups,
    string RequiredFlag,
    bool IgnoreGroupWhenUnset);

public sealed record ThingFlagsCompareGroupInfo(
    string Name,
    bool IsOptional,
    IReadOnlyDictionary<string, ThingFlagCompareInfo> Flags);

public sealed record UniversalFieldAssociationInfo(
    string Property,
    string Modify,
    bool NeverShowEventLines,
    bool ConsolidateEventLines);

public sealed record UniversalFieldInfo(
    string Element,
    string Name,
    int Type,
    object? DefaultValue,
    bool ThingTypeSpecific,
    bool Managed,
    string? EnumName,
    IReadOnlyList<EnumItemInfo> InlineEnumItems,
    IReadOnlyDictionary<string, UniversalFieldAssociationInfo> Associations);

public sealed record ThingsFilterCustomFieldInfo(string Name, int Type, object? Value);

public sealed record ThingsFilterInfo(
    string Key,
    string Name,
    string Category,
    bool Invert,
    int DisplayMode,
    int ThingType,
    int ThingAngle,
    int ThingZHeight,
    int ThingAction,
    IReadOnlyList<int> ThingArgs,
    int ThingTag,
    IReadOnlyList<string> RequiredFields,
    IReadOnlyList<string> ForbiddenFields,
    IReadOnlyDictionary<string, ThingsFilterCustomFieldInfo> CustomFields);

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
    private const string UnknownThingSprite = "internal:unknownthing";

    private const int ThingFixedSize = 14;
    private const string UnknownBaseGame = "UNKNOWN_GAME";
    private static readonly IReadOnlySet<string> EmptyTargetClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> KnownBaseGames = new(StringComparer.OrdinalIgnoreCase)
    {
        "doom",
        "heretic",
        "hexen",
        "strife",
        "chex",
    };

    private readonly Dictionary<int, ThingTypeInfo> things = new();
    private readonly Dictionary<string, ThingCategoryInfo> thingCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, LinedefActionInfo> linedefActions = new();
    private readonly Dictionary<string, LinedefActionCategoryInfo> linedefActionCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, SectorEffectInfo> sectorEffects = new();
    private readonly Dictionary<int, string> linedefFlags = new();
    private readonly Dictionary<int, string> thingFlags = new();
    private readonly HashSet<string> thingFlagKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> skills = new();
    private readonly Dictionary<string, Dictionary<int, string>> enums = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EnumListInfo> enumLists = new(StringComparer.Ordinal);
    private readonly List<GeneralizedCategory> genLinedefs = new();
    private readonly List<GeneralizedCategory> genSectors = new();
    private readonly List<GeneralizedOption> genSectorEffects = new();
    private readonly List<FlagTranslation> linedefFlagsTranslation = new();
    private readonly List<FlagTranslation> thingFlagsTranslation = new();
    private readonly Dictionary<string, MapLumpInfo> mapLumpNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RequiredArchiveInfo> requiredArchives = new();
    private readonly List<LinedefActivationInfo> linedefActivations = new();
    private readonly List<TextureSetInfo> textureSets = new();
    private readonly Dictionary<string, string> defaultSkyTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ResourceRangeInfo> textureRanges = new();
    private readonly List<ResourceRangeInfo> hiResRanges = new();
    private readonly List<ResourceRangeInfo> flatRanges = new();
    private readonly List<ResourceRangeInfo> patchRanges = new();
    private readonly List<ResourceRangeInfo> spriteRanges = new();
    private readonly List<ResourceRangeInfo> colormapRanges = new();
    private readonly List<ResourceRangeInfo> voxelRanges = new();
    private readonly Dictionary<string, bool> makeDoorFlags = new(StringComparer.Ordinal);
    private readonly List<string> defaultThingFlags = new();
    private readonly Dictionary<string, string> thingRenderStyles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> linedefRenderStyles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> sidedefFlags = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> sectorFlags = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> ceilingPortalFlags = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> floorPortalFlags = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> sectorRenderStyles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> sectorPortalRenderStyles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> visplaneViewHeights = new(StringComparer.Ordinal);
    private readonly List<int> brightnessLevels = new();
    private readonly HashSet<string> damageTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> internalSoundNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> ignoredDirectories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> ignoredExtensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ThingFlagsCompareGroupInfo> thingFlagsCompare = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, UniversalFieldInfo>> universalFields = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ThingsFilterInfo> thingsFilters = new();
    private StaticLimitsInfo staticLimits = new(new Dictionary<string, int>());

    public UniversalTypeRegistry Types { get; } = new();
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
    public IReadOnlyDictionary<string, string> ThingRenderStyles => thingRenderStyles;
    public IReadOnlyDictionary<string, string> LinedefRenderStyles => linedefRenderStyles;
    public IReadOnlyDictionary<string, string> SidedefFlags => sidedefFlags;
    public IReadOnlyDictionary<string, string> SectorFlags => sectorFlags;
    public IReadOnlyDictionary<string, string> CeilingPortalFlags => ceilingPortalFlags;
    public IReadOnlyDictionary<string, string> FloorPortalFlags => floorPortalFlags;
    public IReadOnlyDictionary<string, string> SectorRenderStyles => sectorRenderStyles;
    public IReadOnlyDictionary<string, string> SectorPortalRenderStyles => sectorPortalRenderStyles;
    public IReadOnlyDictionary<string, string> VisplaneViewHeights => visplaneViewHeights;
    public IReadOnlyList<int> BrightnessLevels => brightnessLevels;
    public IReadOnlySet<string> DamageTypes => damageTypes;
    public IReadOnlySet<string> InternalSoundNames => internalSoundNames;
    public IReadOnlySet<string> IgnoredDirectories => ignoredDirectories;
    public IReadOnlySet<string> IgnoredExtensions => ignoredExtensions;
    public IReadOnlyDictionary<string, ThingFlagsCompareGroupInfo> ThingFlagsCompare => thingFlagsCompare;
    public IReadOnlyDictionary<string, Dictionary<string, UniversalFieldInfo>> UniversalFields => universalFields;
    public IReadOnlyList<ThingsFilterInfo> ThingsFilters => thingsFilters;

    public string DefaultSaveCompiler { get; private set; } = "";
    public string DefaultTestCompiler { get; private set; } = "";
    public string DefaultScriptCompiler { get; private set; } = "";
    public string NodeBuilderSave { get; private set; } = "";
    public string NodeBuilderTest { get; private set; } = "";
    public string GameName { get; private set; } = "<unnamed game>";
    public string EngineName { get; private set; } = "";
    public string BaseGame { get; private set; } = UnknownBaseGame;
    public FileTitleStyle FileTitleStyle { get; private set; } = FileTitleStyle.DEFAULT;
    public string MapNameFormat { get; private set; } = "";
    public bool ScaledTextureOffsets { get; private set; } = true;
    public string FormatInterface { get; private set; } = "";
    public MapFormat MapFormat => MapFormatFromInterface(FormatInterface);
    public bool HasLinedefTag => FormatInterfaceSupports("HasLinedefTag");
    public bool HasThingTag => FormatInterfaceSupports("HasThingTag");
    public bool HasThingAction => FormatInterfaceSupports("HasThingAction");
    public bool HasThingHeight => FormatInterfaceSupports("HasThingHeight");
    public bool HasActionArgs => FormatInterfaceSupports("HasActionArgs");
    public bool HasCustomFields => FormatInterfaceSupports("HasCustomFields");
    public string DefaultLinedefActivationFlag { get; private set; } = "";
    public string SingleSidedFlag { get; private set; } = "0";
    public string DoubleSidedFlag { get; private set; } = "0";
    public string ImpassableFlag { get; private set; } = "0";
    public string UpperUnpeggedFlag { get; private set; } = "0";
    public string LowerUnpeggedFlag { get; private set; } = "0";
    public bool GeneralizedActions { get; private set; }
    public bool GeneralizedEffects { get; private set; }
    public int Start3DModeThingType { get; private set; }
    public int LinedefActivationsFilter { get; private set; }
    public int VisplaneViewHeightDefault { get; private set; } = 41;
    public string MakeDoorTrack { get; private set; } = "-";
    public string MakeDoorDoor { get; private set; } = "-";
    public string MakeDoorCeiling { get; private set; } = "-";
    public int MakeDoorAction { get; private set; }
    public int MakeDoorActivate { get; private set; }
    public int[] MakeDoorArgs { get; private set; } = new int[5];
    public IReadOnlyDictionary<string, bool> MakeDoorFlags => makeDoorFlags;
    public IReadOnlyList<string> DefaultThingFlags => defaultThingFlags;
    public string TestParameters { get; private set; } = "";
    public bool TestShortPaths { get; private set; }
    public bool TestLinuxPaths { get; private set; }
    public bool LineTagIndicatesSectors { get; private set; }
    public bool DoomThingRotationAngles { get; private set; }
    public string ActionSpecialHelp { get; private set; } = "";
    public string ThingClassHelp { get; private set; } = "";
    public bool SidedefCompressionIgnoresAction { get; private set; }
    public string DecorateGames { get; private set; } = "";
    public string SkyFlatName { get; private set; } = "F_SKY1";
    public int MaxTextureNameLength { get; private set; } = 8;
    public bool UseLongTextureNames { get; private set; }
    public int LeftBoundary { get; private set; } = -32768;
    public int RightBoundary { get; private set; } = 32767;
    public int TopBoundary { get; private set; } = 32767;
    public int BottomBoundary { get; private set; } = -32768;
    public int SafeBoundary { get; private set; } = 32767;
    public bool DoomLightLevels { get; private set; } = true;
    public bool UseLocalSidedefTextureOffsets { get; private set; }
    public bool Effect3DFloorSupport { get; private set; }
    public bool PlaneEquationSupport { get; private set; }
    public bool VertexHeightSupport { get; private set; }
    public bool DistinctFloorAndCeilingBrightness { get; private set; }
    public bool DistinctWallBrightness { get; private set; }
    public bool DistinctSidedefPartBrightness { get; private set; }
    public bool SectorMultiTag { get; private set; }
    public bool SidedefTextureSkewing { get; private set; }
    public bool BuggyModelDefPitch { get; private set; }
    public bool FixNegativePatchOffsets { get; private set; }
    public bool FixMaskedPatchOffsets { get; private set; }
    public double DefaultTextureScale { get; private set; } = 1.0;
    public double DefaultFlatScale { get; private set; } = 1.0;
    public string DefaultWallTexture { get; private set; } = "STARTAN";
    public string DefaultFloorTexture { get; private set; } = "FLOOR0_1";
    public string DefaultCeilingTexture { get; private set; } = "CEIL1_1";
    public bool MixTexturesFlats { get; private set; }
    public IReadOnlyDictionary<string, string> DefaultSkyTextures => defaultSkyTextures;
    public IReadOnlyList<ResourceRangeInfo> TextureRanges => textureRanges;
    public IReadOnlyList<ResourceRangeInfo> HiResRanges => hiResRanges;
    public IReadOnlyList<ResourceRangeInfo> FlatRanges => flatRanges;
    public IReadOnlyList<ResourceRangeInfo> PatchRanges => patchRanges;
    public IReadOnlyList<ResourceRangeInfo> SpriteRanges => spriteRanges;
    public IReadOnlyList<ResourceRangeInfo> ColormapRanges => colormapRanges;
    public IReadOnlyList<ResourceRangeInfo> VoxelRanges => voxelRanges;
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
    public static GameConfiguration FromFile(string path, ScriptConfigurationCatalog? scriptConfigurations = null)
    {
        var cfg = new Configuration(path, true);
        return FromConfiguration(cfg, scriptConfigurations);
    }

    /// <summary>Builds catalogs from already-parsed configuration text (no include resolution).</summary>
    public static GameConfiguration FromText(string cfgText, ScriptConfigurationCatalog? scriptConfigurations = null)
    {
        var cfg = new Configuration(true);
        cfg.InputConfiguration(cfgText, true);
        return FromConfiguration(cfg, scriptConfigurations);
    }

    public static GameConfiguration FromConfiguration(Configuration cfg, ScriptConfigurationCatalog? scriptConfigurations = null)
    {
        var gc = new GameConfiguration();
        if (cfg.Root is IDictionary root)
        {
            gc.GameName = GetString(root, "game", "<unnamed game>");
            gc.EngineName = GetString(root, "engine", "");
            gc.DefaultSaveCompiler = GetString(root, "defaultsavecompiler", "");
            gc.DefaultTestCompiler = GetString(root, "defaulttestcompiler", "");
            gc.DefaultScriptCompiler = GetString(root, "defaultscriptcompiler", "");
            gc.NodeBuilderSave = GetString(root, "nodebuildersave", "");
            gc.NodeBuilderTest = GetString(root, "nodebuildertest", "");
            gc.BaseGame = NormalizeBaseGame(GetString(root, "basegame", ""));
            gc.FileTitleStyle = ParseFileTitleStyle(GetString(root, "filetitlestyle", "default"));
            gc.MapNameFormat = GetString(root, "mapnameformat", "");
            gc.ScaledTextureOffsets = GetBool(root, "scaledtextureoffsets", true);
            gc.FormatInterface = GetString(root, "formatinterface", "");
            gc.DefaultLinedefActivationFlag = GetString(root, "defaultlinedefactivation", "");
            gc.SingleSidedFlag = GetFlagString(root, "singlesidedflag", "0");
            gc.DoubleSidedFlag = GetFlagString(root, "doublesidedflag", "0");
            gc.ImpassableFlag = GetFlagString(root, "impassableflag", "0");
            gc.UpperUnpeggedFlag = GetFlagString(root, "upperunpeggedflag", "0");
            gc.LowerUnpeggedFlag = GetFlagString(root, "lowerunpeggedflag", "0");
            gc.GeneralizedActions = GetBool(root, "generalizedlinedefs", false);
            gc.GeneralizedEffects = GetBool(root, "generalizedsectors", false);
            gc.Start3DModeThingType = GetInt(root, "start3dmode", 0);
            gc.LinedefActivationsFilter = GetInt(root, "linedefactivationsfilter", 0);
            gc.TestParameters = GetString(root, "testparameters", "");
            gc.TestShortPaths = GetBool(root, "testshortpaths", false);
            gc.TestLinuxPaths = GetBool(root, "testlinuxpaths", false);
            if (root["visplaneexplorer"] is IDictionary visplane)
            {
                gc.VisplaneViewHeightDefault = GetInt(visplane, "viewheightdefault", 41);
                if (visplane["viewheights"] is IDictionary viewHeights) ParseStringDictionary(viewHeights, gc.visplaneViewHeights);
            }
            gc.MakeDoorTrack = GetString(root, "makedoortrack", "-");
            gc.MakeDoorDoor = GetString(root, "makedoordoor", "-");
            gc.MakeDoorCeiling = GetString(root, "makedoorceil", "-");
            gc.MakeDoorAction = GetInt(root, "makedooraction", 0);
            gc.MakeDoorActivate = GetInt(root, "makedooractivate", 0);
            for (int i = 0; i < gc.MakeDoorArgs.Length; i++)
                gc.MakeDoorArgs[i] = GetInt(root, "makedoorarg" + i.ToString(CultureInfo.InvariantCulture), 0);
            if (root["makedoorflags"] is IDictionary mdf) gc.ParseMakeDoorFlags(mdf);
            gc.LineTagIndicatesSectors = GetBool(root, "linetagindicatesectors", false);
            gc.DoomThingRotationAngles = GetBool(root, "doomthingrotationangles", false);
            gc.ActionSpecialHelp = GetString(root, "actionspecialhelp", "");
            gc.ThingClassHelp = GetString(root, "thingclasshelp", "");
            gc.SidedefCompressionIgnoresAction = GetBool(root, "sidedefcompressionignoresaction", false);
            gc.DecorateGames = GetString(root, "decorategames", "");
            gc.SkyFlatName = GetString(root, "skyflatname", "F_SKY1");
            gc.LeftBoundary = GetInt(root, "leftboundary", -32768);
            gc.RightBoundary = GetInt(root, "rightboundary", 32767);
            gc.TopBoundary = GetInt(root, "topboundary", 32767);
            gc.BottomBoundary = GetInt(root, "bottomboundary", -32768);
            gc.SafeBoundary = GetInt(root, "safeboundary", 32767);
            gc.DoomLightLevels = GetBool(root, "doomlightlevels", true);
            gc.UseLocalSidedefTextureOffsets = GetBool(root, "localsidedeftextureoffsets", false);
            gc.Effect3DFloorSupport = GetBool(root, "effect3dfloorsupport", false);
            gc.PlaneEquationSupport = GetBool(root, "planeequationsupport", false);
            gc.VertexHeightSupport = GetBool(root, "vertexheightsupport", false);
            gc.SidedefTextureSkewing = GetBool(root, "sidedeftextureskewing", false);
            gc.DistinctFloorAndCeilingBrightness = GetBool(root, "distinctfloorandceilingbrightness", false);
            gc.DistinctWallBrightness = GetBool(root, "distinctwallbrightness", false);
            gc.DistinctSidedefPartBrightness = GetBool(root, "distinctsidedefpartbrightness", false);
            gc.SectorMultiTag = GetBool(root, "sectormultitag", false);
            gc.BuggyModelDefPitch = GetBool(root, "buggymodeldefpitch", false);
            if (root["compatibility"] is IDictionary compatibility)
            {
                gc.FixNegativePatchOffsets = GetBool(compatibility, "fixnegativepatchoffsets", false);
                gc.FixMaskedPatchOffsets = GetBool(compatibility, "fixmaskedpatchoffsets", false);
            }
            gc.UseLongTextureNames = GetBool(root, "longtexturenames", false);
            gc.MaxTextureNameLength = gc.UseLongTextureNames ? short.MaxValue : 8;
            gc.DefaultTextureScale = GetDouble(root, "defaulttexturescale", 1.0);
            gc.DefaultFlatScale = GetDouble(root, "defaultflatscale", 1.0);
            gc.DefaultWallTexture = GetString(root, "defaultwalltexture", "STARTAN");
            gc.DefaultFloorTexture = GetString(root, "defaultfloortexture", "FLOOR0_1");
            gc.DefaultCeilingTexture = GetString(root, "defaultceilingtexture", "CEIL1_1");
            ParseStringSet(GetString(root, "damagetypes", "None"), gc.damageTypes);
            ParseStringSet(GetString(root, "internalsoundnames", ""), gc.internalSoundNames);
            ParseStringSet(GetString(root, "ignoreddirectories", ""), gc.ignoredDirectories);
            ParseStringSet(GetString(root, "ignoredextensions", ""), gc.ignoredExtensions);
            gc.MixTexturesFlats = GetBool(root, "mixtexturesflats", false);
            if (root["defaultskytextures"] is IDictionary dst) gc.ParseDefaultSkyTextures(dst);
            if (root["textures"] is IDictionary txr) gc.ParseResourceRanges(txr, gc.textureRanges);
            if (root["hires"] is IDictionary hir) gc.ParseResourceRanges(hir, gc.hiResRanges);
            if (root["flats"] is IDictionary flr) gc.ParseResourceRanges(flr, gc.flatRanges);
            if (root["patches"] is IDictionary par) gc.ParseResourceRanges(par, gc.patchRanges);
            if (root["sprites"] is IDictionary spr) gc.ParseResourceRanges(spr, gc.spriteRanges);
            if (root["colormaps"] is IDictionary cmr) gc.ParseResourceRanges(cmr, gc.colormapRanges);
            if (root["voxels"] is IDictionary vxr) gc.ParseResourceRanges(vxr, gc.voxelRanges);
            if (root["enums"] is IDictionary en) gc.ParseEnums(en);   // before types, so args can reference them
            if (root["thingtypes"] is IDictionary tt) gc.ParseThingTypes(tt);
            if (root["linedeftypes"] is IDictionary lt) gc.ParseLinedefTypes(lt);
            if (root["sectortypes"] is IDictionary st) gc.ParseSectorTypes(st);
            if (root["linedefflags"] is IDictionary lf) gc.ParseFlatIntStrings(lf, gc.linedefFlags);
            if (root["linedefactivations"] is IDictionary la) gc.ParseLinedefActivations(la);
            if (root["thingflags"] is IDictionary tf) gc.ParseThingFlags(tf);
            if (root["defaultthingflags"] is IDictionary dtf) gc.ParseDefaultThingFlags(dtf);
            if (root["thingflagscompare"] is IDictionary tfc) gc.ParseThingFlagsCompare(tfc);
            if (root["universalfields"] is IDictionary uf) gc.ParseUniversalFields(uf);
            if (root["thingsfilters"] is IDictionary tfs) gc.ParseThingsFilters(tfs);
            if (root["thingrenderstyles"] is IDictionary trs) ParseStringDictionary(trs, gc.thingRenderStyles);
            if (root["linedefrenderstyles"] is IDictionary lrs) ParseStringDictionary(lrs, gc.linedefRenderStyles);
            if (root["sidedefflags"] is IDictionary sf) ParseStringDictionary(sf, gc.sidedefFlags);
            if (root["sectorflags"] is IDictionary sef) ParseStringDictionary(sef, gc.sectorFlags);
            if (root["ceilingportalflags"] is IDictionary cpf) ParseStringDictionary(cpf, gc.ceilingPortalFlags);
            if (root["floorportalflags"] is IDictionary fpf) ParseStringDictionary(fpf, gc.floorPortalFlags);
            if (root["sectorrenderstyles"] is IDictionary srs) ParseStringDictionary(srs, gc.sectorRenderStyles);
            if (root["sectorportalrenderstyles"] is IDictionary sprs) ParseStringDictionary(sprs, gc.sectorPortalRenderStyles);
            if (root["sectorbrightness"] is IDictionary sb) gc.ParseBrightnessLevels(sb);
            if (root["skills"] is IDictionary sk) gc.ParseFlatIntStrings(sk, gc.skills);
            if (root["gen_linedeftypes"] is IDictionary gl) gc.genLinedefs.AddRange(GeneralizedCategory.ParseBlock(gl));
            if (root["gen_sectortypes"] is IDictionary gs) gc.genSectorEffects.AddRange(GeneralizedOption.ParseOptionsBlock(gs));
            if (root["linedefflagstranslation"] is IDictionary lft) gc.ParseFlagTranslations(lft, gc.linedefFlagsTranslation);
            if (root["thingflagstranslation"] is IDictionary tft) gc.ParseFlagTranslations(tft, gc.thingFlagsTranslation);
            if (root["maplumpnames"] is IDictionary mln) gc.ParseMapLumpNames(mln, scriptConfigurations);
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

    public static string ActorResourcesStatusText(int actorCount)
        => $"Loaded {CountLabel(actorCount, "actor")} from DECORATE/ZScript resources.";

    public void MergeDamageTypes(IEnumerable<string> names)
    {
        foreach (string name in names)
            if (!string.IsNullOrWhiteSpace(name))
                damageTypes.Add(name);
    }

    /// <summary>
    /// Merges actors, assigning an editor number from <paramref name="doomEdNums"/> (MAPINFO num -&gt; class) when
    /// the actor itself has none (the ZScript case, where the class header carries no DoomEdNum).
    /// </summary>
    public void MergeActors(IEnumerable<ActorInfo> actors, IReadOnlyDictionary<int, string>? doomEdNums)
        => MergeActors(actors, doomEdNums, null);

    public void MergeActors(IEnumerable<ActorInfo> actors, IReadOnlyDictionary<int, string>? doomEdNums, CvarInfo? cvars)
        => MergeActors(actors, doomEdNums, spawnNums: null, cvars);

    public void MergeActors(
        IEnumerable<ActorInfo> actors,
        IReadOnlyDictionary<int, string>? doomEdNums,
        IReadOnlyDictionary<int, string>? spawnNums,
        CvarInfo? cvars = null)
    {
        var allActors = new List<ActorInfo>(actors);
        var allActorsByClass = new Dictionary<string, ActorInfo>(StringComparer.OrdinalIgnoreCase);
        var replacementTargetsByClass = ThingNumbersByClass();
        var replacementBaseCategories = new Dictionary<int, string>();
        foreach (var actor in allActors)
            if (!allActorsByClass.ContainsKey(actor.ClassName))
                allActorsByClass[actor.ClassName] = actor;

        // Invert num->class to class->num so each actor can look up its own number.
        Dictionary<string, int>? classToNum = null;
        if (doomEdNums != null)
        {
            classToNum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (num, cls) in doomEdNums)
            {
                if (!cls.Equals("none", StringComparison.OrdinalIgnoreCase)) classToNum[cls] = num;
            }
        }

        foreach (var a in allActors)
        {
            if (!ActorSupportedByDecorateGames(a)) continue;

            if (!string.IsNullOrWhiteSpace(a.Replaces))
            {
                int replacedNum = replacementTargetsByClass.TryGetValue(a.Replaces, out int targetNum) ? targetNum : -1;
                if (replacedNum >= 0 && things.TryGetValue(replacedNum, out var replaced))
                {
                    if (!replacementBaseCategories.ContainsKey(replacedNum))
                        replacementBaseCategories[replacedNum] = replaced.Category;
                    string? replacementCategory = TryExplicitActorCategory(a, out _)
                        ? null
                        : replacementBaseCategories[replacedNum];
                    things[replacedNum] = BuildThingInfo(
                        a,
                        replacedNum,
                        replaced,
                        inherited: null,
                        cvars: cvars,
                        allowExistingCategoryOverride: true,
                        replacementCategory: replacementCategory);
                }
            }

            int num = a.DoomEdNum;
            if (num < 0 && classToNum != null && classToNum.TryGetValue(a.ClassName, out int mapped)) num = mapped;
            if (num <= 0) continue;
            things.TryGetValue(num, out var existing);
            var inherited = existing == null && a.ParentName != null ? FindThingInfoByClass(a.ParentName) : null;
            things[num] = BuildThingInfo(a, num, existing, inherited, cvars);
        }

        if (doomEdNums != null)
        {
            foreach (var (num, cls) in doomEdNums)
            {
                if (cls.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    things.Remove(num);
                    continue;
                }
                if (things.TryGetValue(num, out var existing) && string.Equals(existing.ClassName, cls, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (allActorsByClass.TryGetValue(cls, out var actor))
                {
                    var inherited = actor.ParentName != null ? FindThingInfoByClass(actor.ParentName) : null;
                    things[num] = BuildThingInfo(actor, num, existing: null, inherited, cvars);
                    continue;
                }

                int sourceNum = FindThingByClass(cls);
                if (sourceNum >= 0 && things.TryGetValue(sourceNum, out var source))
                {
                    things[num] = CopyThingInfo(source, num);
                }
            }
        }

        MergeSpawnThingEnum(allActors, spawnNums);
    }

    private void MergeSpawnThingEnum(IReadOnlyList<ActorInfo> actors, IReadOnlyDictionary<int, string>? spawnNums)
    {
        var items = new Dictionary<int, string>();
        if (GetEnum("spawnthing") is { } existing)
        {
            foreach (var item in existing)
                items[item.Key] = item.Value;
        }

        bool changed = false;
        foreach (var actor in actors)
        {
            int spawnId = ActorPropertyInt(actor, "spawnid");
            if (spawnId == 0) continue;
            items[spawnId] = actor.Title;
            changed = true;
        }

        if (spawnNums != null)
        {
            foreach (var (spawnNum, className) in spawnNums)
            {
                if (spawnNum == 0) continue;
                items[spawnNum] = things.TryGetValue(spawnNum, out var thing)
                    ? thing.Title
                    : ActorSpawnTitle(actors, className);
                changed = true;
            }
        }

        if (!changed) return;
        SetEnum("spawnthing", items);
    }

    private static string ActorSpawnTitle(IReadOnlyList<ActorInfo> actors, string className)
    {
        foreach (var actor in actors)
            if (string.Equals(actor.ClassName, className, StringComparison.OrdinalIgnoreCase))
                return actor.Title;

        return className;
    }

    private void SetEnum(string name, Dictionary<int, string> items)
    {
        var list = new EnumListInfo(name);
        var map = new Dictionary<int, string>();
        foreach (var item in items.OrderBy(item => item.Value, StringComparer.OrdinalIgnoreCase))
        {
            string value = item.Key.ToString(CultureInfo.InvariantCulture);
            list.Add(new EnumItemInfo(value, item.Value));
            map[item.Key] = item.Value;
        }
        enumLists[name] = list;
        enums[name] = map;
    }

    private Dictionary<string, int> ThingNumbersByClass()
    {
        var byClass = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var thing in things)
            if (!string.IsNullOrEmpty(thing.Value.ClassName))
                byClass[thing.Value.ClassName] = thing.Key;
        return byClass;
    }

    private bool ActorSupportedByDecorateGames(ActorInfo actor)
    {
        if (!actor.Properties.TryGetValue("game", out var games) || games.Count == 0) return true;

        string includeGames = DecorateGames.ToLowerInvariant();
        foreach (string game in games)
            if (game.Length > 0 && includeGames.Contains(game.ToLowerInvariant(), StringComparison.Ordinal))
                return true;
        return false;
    }

    private int FindThingByClass(string className)
    {
        foreach (var thing in things)
            if (string.Equals(thing.Value.ClassName, className, StringComparison.OrdinalIgnoreCase))
                return thing.Key;
        return -1;
    }

    private ThingTypeInfo? FindThingInfoByClass(string className)
    {
        int index = FindThingByClass(className);
        return index >= 0 && things.TryGetValue(index, out var thing) ? thing : null;
    }

    private ThingTypeInfo BuildThingInfo(
        ActorInfo actor,
        int index,
        ThingTypeInfo? existing,
        ThingTypeInfo? inherited,
        CvarInfo? cvars,
        bool allowExistingCategoryOverride = false,
        string? replacementCategory = null)
    {
        string title = ActorTitle(actor);
        bool solid = ActorFlag(actor, "solid");
        var fallback = existing ?? inherited;
        RegisterActorUserVariableFields(actor);
        bool fixedSize = ActorRegionPropertyBool(actor, "$fixedsize") ?? fallback?.FixedSize ?? false;
        bool absoluteZ = ActorRegionPropertyBool(actor, "$absolutez") ?? fallback?.AbsoluteZ ?? false;
        bool hangs = actor.Flags.ContainsKey("spawnceiling") ? ActorFlag(actor, "spawnceiling") : SafeThingHangs(fallback?.Hangs ?? false, absoluteZ);
        bool isObsolete = TryActorProperty(actor, "$obsolete", out string obsoleteMessage);
        ThingRenderMode renderMode = ActorRenderMode(actor, fallback);
        bool rollSprite = ActorRollSprite(actor, renderMode, fallback);
        int actorWidth = ActorWidth(actor, fallback);
        int blocking = fallback?.Blocking ?? 0;
        if (actor.Flags.ContainsKey("solid"))
        {
            blocking = solid ? (blocking > 0 ? blocking : 2) : 0;
        }
        int errorCheck = ActorRegionPropertyInt(actor, "$error") ?? fallback?.ErrorCheck ?? 0;
        if (blocking > 0) errorCheck = 2;
        return new ThingTypeInfo
        {
            Index = index,
            ClassName = actor.ClassName,
            Title = title != actor.ClassName ? title : existing?.Title ?? title,
            Category = ActorCategory(actor, existing, inherited, allowExistingCategoryOverride, replacementCategory),
            Sprite = fallback?.LockSprite == true
                ? UnknownThingSpriteIfEmpty(fallback.Sprite)
                : UnknownThingSpriteIfEmpty(actor.EditorSprite ?? ActorRegionProperty(actor, "$sprite") ?? fallback?.Sprite),
            LightName = actor.LightName ?? "",
            Width = SafeThingWidth(actorWidth, fixedSize),
            RenderRadius = ActorRenderRadius(actor, actorWidth, fallback),
            DistanceCheckSq = ActorDistanceCheckSq(actor, fallback, cvars),
            Height = actor.Height > 0 ? actor.Height : fallback?.Height ?? 16,
            Alpha = ActorAlpha(actor, fallback),
            RenderStyle = ActorRenderStyle(actor, fallback),
            Bright = actor.StateBright || ActorFlag(actor, "bright"),
            SpriteScale = ActorSpriteScale(actor, fallback),
            Color = isObsolete ? 4 : ActorColor(actor, fallback),
            Arrow = actor.Properties.ContainsKey("$angled") || actor.Properties.ContainsKey("$notangled")
                ? ActorArrow(actor)
                : ActorRegionPropertyBoolish(actor, "$arrow") ?? fallback?.Arrow ?? false,
            Hangs = hangs,
            Blocking = blocking,
            ErrorCheck = errorCheck,
            FixedSize = fixedSize,
            FixedRotation = ActorRegionPropertyBool(actor, "$fixedrotation") ?? fallback?.FixedRotation ?? false,
            AbsoluteZ = absoluteZ,
            LockSprite = existing?.LockSprite ?? false,
            ThingLink = existing?.ThingLink ?? 0,
            Optional = existing?.Optional ?? false,
            IsKnown = existing?.IsKnown ?? true,
            IsObsolete = isObsolete || fallback?.IsObsolete == true,
            ObsoleteMessage = isObsolete ? obsoleteMessage : fallback?.ObsoleteMessage ?? "",
            XYBillboard = ActorFlag(actor, "forcexybillboard"),
            RenderMode = renderMode,
            RollSprite = rollSprite,
            RollCenter = rollSprite && ActorFlag(actor, "rollcenter"),
            FlagsRename = existing?.FlagsRename ?? EmptyFlagsRename,
            AddUniversalFields = ActorAdditionalUniversalFields(actor, fallback),
            Args = ActorArgs(actor, fallback?.Args),
        };
    }

    private static string UnknownThingSpriteIfEmpty(string? sprite)
        => string.IsNullOrEmpty(sprite) ? UnknownThingSprite : sprite;

    private static IReadOnlyList<string> ActorAdditionalUniversalFields(ActorInfo actor, ThingTypeInfo? fallback)
    {
        if (actor.UserVariables.Count == 0) return fallback?.AddUniversalFields ?? Array.Empty<string>();

        var fields = new List<string>();
        if (fallback != null)
        {
            foreach (string field in fallback.AddUniversalFields)
                if (!fields.Contains(field, StringComparer.OrdinalIgnoreCase)) fields.Add(field);
        }

        foreach (var variable in actor.UserVariables.Values)
            if (!fields.Contains(variable.Name, StringComparer.OrdinalIgnoreCase)) fields.Add(variable.Name);

        return fields;
    }

    private void RegisterActorUserVariableFields(ActorInfo actor)
    {
        if (actor.UserVariables.Count == 0) return;
        if (!universalFields.TryGetValue("thing", out var fields))
        {
            fields = new Dictionary<string, UniversalFieldInfo>(StringComparer.OrdinalIgnoreCase);
            universalFields["thing"] = fields;
        }

        foreach (var variable in actor.UserVariables.Values)
        {
            string name = variable.Name.ToLowerInvariant();
            if (fields.ContainsKey(name)) continue;
            fields[name] = new UniversalFieldInfo(
                "thing",
                name,
                (int)variable.Type,
                variable.DefaultValue,
                ThingTypeSpecific: true,
                Managed: true,
                EnumName: null,
                InlineEnumItems: Array.Empty<EnumItemInfo>(),
                Associations: new Dictionary<string, UniversalFieldAssociationInfo>());
        }
    }

    private static ThingTypeInfo CopyThingInfo(ThingTypeInfo source, int index) => new()
    {
        Index = index,
        ClassName = source.ClassName,
        Title = source.Title,
        Category = source.Category,
        Sprite = source.Sprite,
        LightName = source.LightName,
        Width = source.Width,
        RenderRadius = source.RenderRadius,
        DistanceCheckSq = source.DistanceCheckSq,
        Height = source.Height,
        Alpha = source.Alpha,
        RenderStyle = source.RenderStyle,
        Bright = source.Bright,
        SpriteScale = source.SpriteScale,
        Color = source.Color,
        Arrow = source.Arrow,
        Hangs = source.Hangs,
        Blocking = source.Blocking,
        ErrorCheck = source.ErrorCheck,
        FixedSize = source.FixedSize,
        FixedRotation = source.FixedRotation,
        AbsoluteZ = source.AbsoluteZ,
        LockSprite = source.LockSprite,
        ThingLink = source.ThingLink,
        Optional = source.Optional,
        IsKnown = source.IsKnown,
        IsObsolete = source.IsObsolete,
        ObsoleteMessage = source.ObsoleteMessage,
        XYBillboard = source.XYBillboard,
        RenderMode = source.RenderMode,
        RollSprite = source.RollSprite,
        RollCenter = source.RollCenter,
        FlagsRename = source.FlagsRename,
        AddUniversalFields = source.AddUniversalFields,
        Args = source.Args,
    };

    private static string ActorTitle(ActorInfo actor)
    {
        if (!string.Equals(actor.Title, actor.ClassName, StringComparison.Ordinal)) return actor.Title;
        if (TryActorProperty(actor, "tag", out string? tag) && !tag.StartsWith("$", StringComparison.Ordinal)) return tag;
        return actor.ClassName;
    }

    private static string ActorCategory(
        ActorInfo actor,
        ThingTypeInfo? existing,
        ThingTypeInfo? inherited,
        bool allowExistingCategoryOverride,
        string? replacementCategory)
    {
        if (existing != null)
        {
            if (allowExistingCategoryOverride && TryExplicitActorCategory(actor, out string category))
                return category;

            return replacementCategory ?? existing.Category;
        }

        return actor.Category ?? inherited?.Category ?? "Decorate";
    }

    private static bool TryExplicitActorCategory(ActorInfo actor, out string category)
    {
        if (actor.EditorKeys.TryGetValue("$category", out string? editorCategory) && editorCategory.Length > 0)
        {
            category = editorCategory;
            return true;
        }

        if (actor.Properties.TryGetValue("$category", out var values) && values.Count > 0 && values[0].Length > 0)
        {
            category = values[0];
            return true;
        }

        category = "";
        return false;
    }

    private double ActorAlpha(ActorInfo actor, ThingTypeInfo? existing)
        => TryActorPropertyDouble(actor, "alpha", out double alpha) ? Math.Clamp(alpha, 0.0, 1.0)
            : actor.Properties.ContainsKey("defaultalpha") ? DefaultActorAlpha()
            : existing?.Alpha ?? 1.0;

    private double DefaultActorAlpha()
        => BaseGame.Equals("heretic", StringComparison.OrdinalIgnoreCase) ? 0.4 : 0.6;

    private static int ActorWidth(ActorInfo actor, ThingTypeInfo? existing)
    {
        if (actor.Radius <= 0) return existing?.Width ?? 16;
        return actor.Radius;
    }

    private static double ActorRenderRadius(ActorInfo actor, int actorWidth, ThingTypeInfo? existing)
    {
        if (TryActorPropertyDouble(actor, "renderradius", out double renderRadius) && renderRadius != 0.0)
            return renderRadius;
        if (TryActorProperty(actor, "radius", out _)) return actorWidth;
        return existing?.RenderRadius ?? actorWidth;
    }

    private static double ActorDistanceCheckSq(ActorInfo actor, ThingTypeInfo? existing, CvarInfo? cvars)
    {
        if (!TryActorProperty(actor, "distancecheck", out string cvarName))
            return existing?.DistanceCheckSq ?? double.MaxValue;
        if (cvars == null) return existing?.DistanceCheckSq ?? double.MaxValue;

        foreach (var cvar in cvars.Variables)
        {
            if (!cvar.Name.Equals(cvarName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!cvar.Type.Equals("int", StringComparison.OrdinalIgnoreCase)) return double.MaxValue;
            if (cvar.DefaultValue == null) return 0.0;
            return int.TryParse(cvar.DefaultValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? (double)value * value
                : double.MaxValue;
        }

        return existing?.DistanceCheckSq ?? double.MaxValue;
    }

    private static int SafeThingWidth(int width, bool fixedSize)
        => width < 4 || fixedSize ? ThingFixedSize : width;

    private static bool SafeThingHangs(bool hangs, bool absoluteZ)
        => hangs && !absoluteZ;

    private static string ActorRenderStyle(ActorInfo actor, ThingTypeInfo? existing)
        => actor.Properties.ContainsKey("$ignorerenderstyle")
            ? existing?.RenderStyle ?? "normal"
            : TryActorProperty(actor, "renderstyle", out string? style) ? style.ToLowerInvariant() : existing?.RenderStyle ?? "normal";

    private static double ActorSpriteScale(ActorInfo actor, ThingTypeInfo? existing)
    {
        if (TryActorPropertyDouble(actor, "xscale", out double xscale)) return NormalizeActorSpriteScale(xscale);
        if (TryActorPropertyDouble(actor, "scale", out double scale)) return NormalizeActorSpriteScale(scale);
        return existing?.SpriteScale ?? 1.0;
    }

    private static double NormalizeActorSpriteScale(double scale) => scale == 0.0 ? 1.0 : scale;

    private static int ActorColor(ActorInfo actor, ThingTypeInfo? existing)
    {
        if (!TryActorPropertyInt(actor, "$color", out int color)
            && !TryActorRegionPropertyInt(actor, "$color", out color))
            return existing?.Color ?? 0;
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

    private static ThingRenderMode ActorRenderMode(ActorInfo actor, ThingTypeInfo? existing)
    {
        if (ActorFlag(actor, "wallsprite")) return ThingRenderMode.WallSprite;
        if (ActorFlag(actor, "flatsprite")) return ThingRenderMode.FlatSprite;
        return existing?.RenderMode ?? ThingRenderMode.Normal;
    }

    private static bool ActorRollSprite(ActorInfo actor, ThingRenderMode renderMode, ThingTypeInfo? existing)
    {
        if (actor.Flags.TryGetValue("rollsprite", out bool enabled)) return enabled;
        if (renderMode is ThingRenderMode.WallSprite or ThingRenderMode.FlatSprite) return true;
        return existing?.RollSprite ?? false;
    }

    private static string? ActorRegionProperty(ActorInfo actor, string key)
        => actor.RegionProperties.TryGetValue(key, out var values) && values.Count > 0 ? values[0] : null;

    private static bool TryActorRegionPropertyInt(ActorInfo actor, string key, out int value)
    {
        value = 0;
        return actor.RegionProperties.TryGetValue(key, out var values)
            && values.Count > 0
            && int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static int? ActorRegionPropertyInt(ActorInfo actor, string key)
        => TryActorRegionPropertyInt(actor, key, out int value) ? value : null;

    private static bool? ActorRegionPropertyBool(ActorInfo actor, string key)
    {
        string? value = ActorRegionProperty(actor, key);
        if (value == null) return null;
        return value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool? ActorRegionPropertyBoolish(ActorInfo actor, string key)
    {
        if (!TryActorRegionPropertyInt(actor, key, out int value)) return null;
        return value != 0;
    }

    private static ArgInfo[] ActorArgs(ActorInfo actor, ArgInfo[]? existing)
    {
        ArgInfo[]? args = null;
        bool clearArgs = actor.Properties.ContainsKey("$clearargs") || actor.Properties.ContainsKey("skip_super");
        for (int i = 0; i < 5; i++)
        {
            string prefix = "$arg" + i.ToString(CultureInfo.InvariantCulture);
            if (!TryActorProperty(actor, prefix, out string title)) continue;
            args ??= new ArgInfo[5];
            int type = ActorPropertyInt(actor, prefix + "type");
            if (!Enum.IsDefined(typeof(UniversalType), type)) type = 0;
            string renderStyle = ActorArgRenderStyle(actor, prefix);
            bool hasRenderStyle = renderStyle.Length > 0;
            int minRange = hasRenderStyle ? ActorPropertyInt(actor, prefix + "minrange") : 0;
            int maxRange = hasRenderStyle ? ActorPropertyInt(actor, prefix + "maxrange") : 0;
            string enumValue = ActorProperty(actor, prefix + "enum");
            var inlineEnum = ParseActorArgInlineEnum(enumValue);
            args[i] = new ArgInfo
            {
                Title = title,
                Used = true,
                ToolTip = ActorArgToolTip(actor, prefix, renderStyle, minRange, maxRange),
                Type = type,
                Enum = inlineEnum.Count > 0 ? null : EmptyToNull(enumValue),
                InlineEnumItems = inlineEnum,
                Default = ActorPropertyInt(actor, prefix + "default"),
                DefaultValue = ActorPropertyInt(actor, prefix + "default"),
                TargetClasses = type == (int)UniversalType.ThingTag ? ParseTargetClasses(ActorProperty(actor, prefix + "targetclasses")) : EmptyTargetClasses,
                RenderStyle = renderStyle,
                RenderColor = hasRenderStyle ? ActorArgColor(actor, prefix + "rendercolor", alpha: 192) : null,
                MinRange = minRange,
                MaxRange = maxRange,
                MinRangeColor = hasRenderStyle && minRange > 0 ? ActorArgColor(actor, prefix + "minrangecolor", alpha: 96) : null,
                MaxRangeColor = hasRenderStyle && maxRange > 0 ? ActorArgColor(actor, prefix + "maxrangecolor", alpha: 96) : null,
                Str = actor.Properties.ContainsKey(prefix + "str"),
                TitleStr = ActorProperty(actor, prefix + "str") is { Length: > 0 } titleStr ? titleStr : title,
            };
        }

        if (args == null) return clearArgs ? Array.Empty<ArgInfo>() : existing ?? Array.Empty<ArgInfo>();
        for (int i = 0; i < 5; i++) args[i] ??= !clearArgs && existing != null && i < existing.Length ? existing[i] : new ArgInfo();
        return args;
    }

    private static IReadOnlyList<EnumItemInfo> ParseActorArgInlineEnum(string value)
    {
        value = value.Trim();
        if (!value.StartsWith("{", StringComparison.Ordinal)) return Array.Empty<EnumItemInfo>();

        var cfg = new Configuration(true);
        if (!cfg.InputConfiguration("enum " + value, true)) return Array.Empty<EnumItemInfo>();
        return cfg.ReadSetting("enum", (IDictionary?)null) is IDictionary block ? ParseInlineEnum(block) : Array.Empty<EnumItemInfo>();
    }

    private static string ActorArgRenderStyle(ActorInfo actor, string prefix)
    {
        string renderStyle = ActorProperty(actor, prefix + "renderstyle").ToLowerInvariant();
        return renderStyle is "circle" or "rectangle" ? renderStyle : "";
    }

    private static string ActorArgToolTip(ActorInfo actor, string prefix, string renderStyle, int minRange, int maxRange)
    {
        string tooltip = ActorProperty(actor, prefix + "tooltip").Replace("\\n", Environment.NewLine);
        if (renderStyle.Length == 0 || (minRange <= 0 && maxRange <= 0)) return tooltip;

        if (tooltip.Length > 0) tooltip += Environment.NewLine + Environment.NewLine;
        if (minRange > 0 && maxRange > 0) return tooltip + "Expected range: " + minRange.ToString(CultureInfo.InvariantCulture) + " - " + maxRange.ToString(CultureInfo.InvariantCulture);
        if (minRange > 0) return tooltip + "Minimum: " + minRange.ToString(CultureInfo.InvariantCulture);
        return tooltip + "Maximum: " + maxRange.ToString(CultureInfo.InvariantCulture);
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
        return TryActorNumericProperty(actor, name, out string raw)
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryActorPropertyInt(ActorInfo actor, string name, out int value)
    {
        value = 0;
        return TryActorNumericProperty(actor, name, out string raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryActorNumericProperty(ActorInfo actor, string name, out string value)
    {
        value = "";
        if (!actor.Properties.TryGetValue(name, out var values) || values.Count == 0) return false;

        value = values[0];
        if (value == "-" && values.Count > 1)
        {
            value += values[1];
            return true;
        }

        if (value.StartsWith("- ", StringComparison.Ordinal))
            value = "-" + value[2..].TrimStart();
        return true;
    }

    private static string ActorProperty(ActorInfo actor, string name)
        => TryActorProperty(actor, name, out string value) ? value : "";

    private static int ActorPropertyInt(ActorInfo actor, string name)
        => TryActorPropertyInt(actor, name, out int value) ? value : 0;

    private static ArgColor? ActorArgColor(ActorInfo actor, string name, byte alpha)
        => TryActorProperty(actor, name, out string value) ? ParseArgColor(value, alpha) : null;

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    private static ArgColor? ParseArgColor(string value, byte alpha)
        => ZDoomColorParser.TryParse(value, knownColors: null, out byte red, out byte green, out byte blue)
            ? new ArgColor(red, green, blue, alpha)
            : null;

    private static string NormalizeBaseGame(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return UnknownBaseGame;
        string normalized = value.ToLowerInvariant();
        return KnownBaseGames.Contains(normalized) ? normalized : UnknownBaseGame;
    }

    private static FileTitleStyle ParseFileTitleStyle(string value)
        => Enum.TryParse(value, ignoreCase: true, out FileTitleStyle style) ? style : FileTitleStyle.DEFAULT;

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
            string sprite = UnknownThingSpriteIfEmpty(ResolveDehackedSprite(thing, patch) ?? existing?.Sprite);
            sprite = ApplySpriteReplacement(sprite, spriteReplacements);
            int width = TryReadDehackedInt(thing.Properties, "Width", out int rawWidth) ? FixedToInt(rawWidth) : existing?.Width ?? 16;
            int height = TryReadDehackedInt(thing.Properties, "Height", out int rawHeight) ? FixedToInt(rawHeight) : existing?.Height ?? 16;
            string category = ReadDehackedProperty(thing.Properties, "$Category") ?? existing?.Category ?? "User-defined";
            bool hasBits = TryReadDehackedBits(thing.Properties, out var bits);

            things[doomEdNum] = new ThingTypeInfo
            {
                Index = doomEdNum,
                ClassName = existing?.ClassName ?? "DehackedThing" + thing.Number.ToString(CultureInfo.InvariantCulture),
                Title = thing.Name,
                Category = category,
                Sprite = sprite,
                Width = width > 0 ? width : existing?.Width ?? 16,
                RenderRadius = existing?.RenderRadius ?? 10.0,
                DistanceCheckSq = existing?.DistanceCheckSq ?? double.MaxValue,
                Height = height > 0 ? height : existing?.Height ?? 16,
                Color = DehackedColor(thing.Properties, existing?.Color ?? 0),
                Alpha = existing?.Alpha ?? 1.0,
                RenderStyle = existing?.RenderStyle ?? "normal",
                Bright = existing?.Bright ?? false,
                Arrow = DehackedArrow(thing.Properties, existing?.Arrow ?? false),
                Hangs = hasBits ? bits.Contains("spawnceiling") : existing?.Hangs ?? false,
                Blocking = hasBits ? bits.Contains("solid") ? 1 : 0 : existing?.Blocking ?? 0,
                ErrorCheck = existing?.ErrorCheck ?? 0,
                FixedSize = existing?.FixedSize ?? false,
                FixedRotation = existing?.FixedRotation ?? false,
                AbsoluteZ = existing?.AbsoluteZ ?? false,
                SpriteScale = existing?.SpriteScale ?? 1.0,
                LockSprite = existing?.LockSprite ?? false,
                ThingLink = existing?.ThingLink ?? 0,
                Optional = existing?.Optional ?? false,
                IsKnown = existing?.IsKnown ?? true,
                IsObsolete = existing?.IsObsolete ?? false,
                ObsoleteMessage = existing?.ObsoleteMessage ?? "",
                XYBillboard = existing?.XYBillboard ?? false,
                RenderMode = existing?.RenderMode ?? ThingRenderMode.Normal,
                RollSprite = existing?.RollSprite ?? false,
                RollCenter = existing?.RollCenter ?? false,
                FlagsRename = existing?.FlagsRename ?? EmptyFlagsRename,
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
        LightName = info.LightName,
        Width = info.Width,
        RenderRadius = info.RenderRadius,
        DistanceCheckSq = info.DistanceCheckSq,
        Height = info.Height,
        Alpha = info.Alpha,
        RenderStyle = info.RenderStyle,
        Bright = info.Bright,
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
        IsObsolete = info.IsObsolete,
        ObsoleteMessage = info.ObsoleteMessage,
        XYBillboard = info.XYBillboard,
        RenderMode = info.RenderMode,
        RollSprite = info.RollSprite,
        RollCenter = info.RollCenter,
        FlagsRename = info.FlagsRename,
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

    private static int DehackedColor(Dictionary<string, string> properties, int fallback)
    {
        if (!properties.TryGetValue("$Editor Color ID", out string? value)) return fallback;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int color)) return 18;
        return color is >= 0 and <= 19 ? color : fallback;
    }

    private static bool DehackedArrow(Dictionary<string, string> properties, bool fallback)
        => properties.TryGetValue("$Editor Angled", out string? value)
            ? value.Equals("true", StringComparison.OrdinalIgnoreCase)
            : fallback;

    private static bool TryReadDehackedBits(Dictionary<string, string> properties, out HashSet<string> bits)
    {
        bits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!properties.TryGetValue("Bits", out string? value) || value.Length == 0) return false;
        foreach (string bit in value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            bits.Add(bit);
        return true;
    }

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
        string title = GetString(cat, "title", key);
        if (!IsValidThingCategory(cat, key, title)) return;
        var info = new ThingCategoryInfo(
            key,
            title,
            parent?.Key,
            GetInt(cat, "color", parent?.Color ?? 0),
            SafeThingCategoryWidth(GetInt(cat, "width", parent?.Width ?? 10)),
            GetInt(cat, "height", parent?.Height ?? 20),
            parent == null
                ? Math.Clamp(GetDouble(cat, "alpha", 1.0), 0.0, 1.0)
                : GetDouble(cat, "alpha", parent.Alpha),
            GetString(cat, "renderstyle", parent?.RenderStyle ?? "normal").ToLowerInvariant(),
            GetString(cat, "sprite", parent?.Sprite ?? ""),
            parent?.LightName ?? "",
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
            if (int.TryParse(childKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                if (e.Value is not IDictionary)
                {
                    string? scalarTitle = Convert.ToString(e.Value, CultureInfo.InvariantCulture);
                    if (scalarTitle == null) continue;
                    bool scalarAbsoluteZ = info.AbsoluteZ;
                    AddThingType(number, new ThingTypeInfo
                    {
                        Index = number,
                        Category = key,
                        Title = scalarTitle,
                        Sprite = info.Sprite,
                        LightName = "",
                        Color = info.Color,
                        Width = SafeThingWidth(info.Width, info.FixedSize),
                        Height = info.Height,
                        Alpha = info.Alpha,
                        RenderStyle = info.RenderStyle,
                        Arrow = info.Arrow != 0,
                        Hangs = SafeThingHangs(info.Hangs != 0, scalarAbsoluteZ),
                        Blocking = info.Blocking,
                        ErrorCheck = info.ErrorCheck,
                        FixedSize = info.FixedSize,
                        FixedRotation = info.FixedRotation,
                        AbsoluteZ = scalarAbsoluteZ,
                        SpriteScale = info.SpriteScale,
                    });
                    continue;
                }

                if (e.Value is not IDictionary child) continue;
                bool fixedSize = GetBool(child, "fixedsize", info.FixedSize);
                bool absoluteZ = GetBool(child, "absolutez", info.AbsoluteZ);
                AddThingType(number, new ThingTypeInfo
                {
                    Index = number,
                    Category = key,
                    Title = GetString(child, "title", "<" + childKey + ">"),
                    Sprite = GetString(child, "sprite", info.Sprite),
                    LightName = "",
                    ClassName = GetString(child, "class", ""),
                    Color = GetInt(child, "color", info.Color),
                    Width = SafeThingWidth(GetInt(child, "width", info.Width), fixedSize),
                    Height = GetInt(child, "height", info.Height),
                    Alpha = Math.Clamp(GetDouble(child, "alpha", info.Alpha), 0.0, 1.0),
                    RenderStyle = GetString(child, "renderstyle", info.RenderStyle).ToLowerInvariant(),
                    Arrow = GetBoolishInt(child, "arrow", info.Arrow != 0),
                    Hangs = SafeThingHangs(GetBoolishInt(child, "hangs", info.Hangs != 0), absoluteZ),
                    Blocking = GetInt(child, "blocking", info.Blocking),
                    ErrorCheck = GetInt(child, "error", info.ErrorCheck),
                    FixedSize = fixedSize,
                    FixedRotation = GetBool(child, "fixedrotation", info.FixedRotation),
                    AbsoluteZ = absoluteZ,
                    SpriteScale = GetDouble(child, "spritescale", info.SpriteScale),
                    LockSprite = GetBool(child, "locksprite", false),
                    ThingLink = GetInt(child, "thinglink", 0),
                    Optional = GetBool(child, "optional", info.Optional),
                    FlagsRename = ParseFlagsRename(child),
                    AddUniversalFields = ParseAddUniversalFields(child),
                    Args = ParseArgs(child),
                });
            }
            else
            {
                if (e.Value is not IDictionary child) continue;
                ParseThingCategory(key + "." + childKey, child, info);
            }
        }
    }

    private void AddThingType(int number, ThingTypeInfo thing)
    {
        if (!things.ContainsKey(number)) things.Add(number, thing);
    }

    private static bool IsValidThingCategory(IDictionary cat, string key, string title)
    {
        if (title != key) return true;
        if (cat.Contains("sprite")
            || cat.Contains("sort")
            || cat.Contains("color")
            || cat.Contains("alpha")
            || cat.Contains("renderstyle")
            || cat.Contains("arrow")
            || cat.Contains("width")
            || cat.Contains("height")
            || cat.Contains("hangs")
            || cat.Contains("blocking")
            || cat.Contains("error")
            || cat.Contains("fixedsize")
            || cat.Contains("fixedrotation")
            || cat.Contains("absolutez")
            || cat.Contains("spritescale"))
        {
            return true;
        }

        foreach (DictionaryEntry entry in cat)
            if (int.TryParse(entry.Key.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                return true;

        return false;
    }

    private static int SafeThingCategoryWidth(int width)
        => width < 4 ? 8 : width;

    private void ParseLinedefTypes(IDictionary linedeftypes)
    {
        foreach (DictionaryEntry catEntry in linedeftypes)
        {
            string catName = catEntry.Key.ToString() ?? "";
            if (catEntry.Value is not IDictionary cat) continue;
            string catTitle = GetString(cat, "title", "");
            var category = new LinedefActionCategoryInfo(catName, catTitle);
            linedefActionCategories[catName] = category;

            foreach (DictionaryEntry e in cat)
            {
                string key = e.Key.ToString() ?? "";
                if (e.Value is not IDictionary action) continue;
                if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) continue;
                string name = GetString(action, "title", "Unnamed");
                string prefix = GetString(action, "prefix", "");

                var info = new LinedefActionInfo
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
                if (AddLinedefAction(number, info))
                    category.Add(number);
            }
        }

        SortLinedefActions();
        SortLinedefActionCategories();
    }

    private bool AddLinedefAction(int number, LinedefActionInfo action)
    {
        if (linedefActions.ContainsKey(number)) return false;
        linedefActions.Add(number, action);
        return true;
    }

    private void SortLinedefActions()
    {
        var sorted = new List<KeyValuePair<int, LinedefActionInfo>>(linedefActions);
        sorted.Sort((left, right) => left.Key.CompareTo(right.Key));
        linedefActions.Clear();
        foreach (var action in sorted)
            linedefActions.Add(action.Key, action.Value);
    }

    private void SortLinedefActionCategories()
    {
        var sorted = new List<KeyValuePair<string, LinedefActionCategoryInfo>>(linedefActionCategories);
        sorted.Sort((left, right) => string.Compare(left.Key, right.Key, StringComparison.Ordinal));
        linedefActionCategories.Clear();
        foreach (var category in sorted)
            linedefActionCategories.Add(category.Key, category.Value);
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
    private ArgInfo[] ParseArgs(IDictionary entry)
    {
        ArgInfo[]? args = null;
        for (int i = 0; i < 5; i++)
        {
            if (entry["arg" + i] is not IDictionary ad) continue;
            args ??= new ArgInfo[5];
            string title = GetString(ad, "title", "Argument " + (i + 1).ToString(CultureInfo.InvariantCulture));
            int type = GetInt(ad, "type", 0);
            string renderStyle = ParseArgRenderStyle(GetString(ad, "renderstyle", ""));
            bool hasRenderStyle = renderStyle.Length > 0;
            int minRange = hasRenderStyle ? GetPositiveArgRange(ad, "minrange") : 0;
            int maxRange = hasRenderStyle ? GetPositiveArgRange(ad, "maxrange") : 0;
            args[i] = new ArgInfo
            {
                Title = title,
                Used = true,
                ToolTip = ConfigArgToolTip(ad, minRange, maxRange),
                Type = type,
                Enum = GetKnownArgReferenceName(ad["enum"]),
                Flags = GetKnownArgReferenceName(ad["flags"]),
                InlineEnumItems = ad["enum"] is IDictionary inlineEnum ? ParseInlineEnum(inlineEnum) : Array.Empty<EnumItemInfo>(),
                InlineFlagsItems = ad["flags"] is IDictionary inlineFlags ? ParseInlineEnum(inlineFlags) : Array.Empty<EnumItemInfo>(),
                Default = GetInt(ad, "default", 0),
                DefaultValue = ad["default"] ?? 0,
                TargetClasses = type == (int)UniversalType.ThingTag ? ParseTargetClasses(GetString(ad, "targetclasses", "")) : EmptyTargetClasses,
                RenderStyle = renderStyle,
                RenderColor = hasRenderStyle ? ParseArgColor(GetString(ad, "rendercolor", ""), alpha: 192) : null,
                MinRange = minRange,
                MaxRange = maxRange,
                MinRangeColor = minRange > 0 ? ParseArgColor(GetString(ad, "minrangecolor", ""), alpha: 96) : null,
                MaxRangeColor = maxRange > 0 ? ParseArgColor(GetString(ad, "maxrangecolor", ""), alpha: 96) : null,
                Str = GetBool(ad, "str", false),
                TitleStr = GetString(ad, "titlestr", title),
            };
        }
        if (args == null) return Array.Empty<ArgInfo>();
        for (int i = 0; i < 5; i++) args[i] ??= new ArgInfo();
        return args;
    }

    private static string? GetReferenceName(object? value)
        => value is null or IDictionary ? null : Convert.ToString(value, CultureInfo.InvariantCulture);

    private string? GetKnownArgReferenceName(object? value)
    {
        string? name = GetReferenceName(value);
        return name != null && enumLists.ContainsKey(name) ? name : null;
    }

    private static string ParseArgRenderStyle(string value)
    {
        string normalized = value.ToLowerInvariant();
        return normalized is "circle" or "rectangle" ? normalized : "";
    }

    private static int GetPositiveArgRange(IDictionary ad, string key)
    {
        string text = GetString(ad, key, "");
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0 ? value : 0;
    }

    private static string ConfigArgToolTip(IDictionary ad, int minRange, int maxRange)
    {
        string tooltip = GetString(ad, "tooltip", "").Replace("\\n", Environment.NewLine);
        if (minRange <= 0 && maxRange <= 0) return tooltip;

        if (tooltip.Length > 0) tooltip += Environment.NewLine;
        if (minRange > 0 && maxRange > 0) return tooltip + "Range: " + minRange.ToString(CultureInfo.InvariantCulture) + " - " + maxRange.ToString(CultureInfo.InvariantCulture);
        if (minRange > 0) return tooltip + "Minimum range: " + minRange.ToString(CultureInfo.InvariantCulture);
        return tooltip + "Maximum range: " + maxRange.ToString(CultureInfo.InvariantCulture);
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
            enumLists[name] = list;
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
        => arg.InlineEnumItems.Count > 0 ? CreateInlineEnumList("arg.enum", arg.InlineEnumItems)
            : arg.Enum != null ? GetEnumList(arg.Enum) : null;

    /// <summary>The value-&gt;title map for an arg's enum, or null when the arg has none.</summary>
    public IReadOnlyDictionary<int, string>? GetArgEnum(ArgInfo arg)
        => arg.InlineEnumItems.Count > 0 ? CreateInlineEnumMap(arg.InlineEnumItems)
            : arg.Enum != null ? GetEnum(arg.Enum) : null;

    public EnumListInfo? GetArgFlagsList(ArgInfo arg)
        => arg.InlineFlagsItems.Count > 0 ? CreateInlineEnumList("arg.flags", arg.InlineFlagsItems)
            : arg.Flags != null ? GetEnumList(arg.Flags) : null;

    public IReadOnlyDictionary<int, string>? GetArgFlags(ArgInfo arg)
        => arg.InlineFlagsItems.Count > 0 ? CreateInlineEnumMap(arg.InlineFlagsItems)
            : arg.Flags != null ? GetEnum(arg.Flags) : null;

    private static EnumListInfo CreateInlineEnumList(string name, IReadOnlyList<EnumItemInfo> items)
    {
        var list = new EnumListInfo(name);
        foreach (EnumItemInfo item in items) list.Add(item);
        return list;
    }

    private static IReadOnlyDictionary<int, string> CreateInlineEnumMap(IReadOnlyList<EnumItemInfo> items)
    {
        var map = new Dictionary<int, string>();
        foreach (EnumItemInfo item in items)
            if (int.TryParse(item.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                map[value] = item.Title;
        return map;
    }

    public UniversalTypeHandler CreateArgumentHandler(ArgInfo arg)
        => Types.CreateHandler(arg.Type, arg.DefaultValue, isForArgument: true, GetArgEnumList(arg));

    public UniversalTypeHandler CreateFieldHandler(UniversalFieldInfo field)
        => Types.CreateHandler(field.Type, field.DefaultValue, isForArgument: false, GetFieldEnumList(field));

    public EnumListInfo? GetFieldEnumList(UniversalFieldInfo field)
    {
        if (field.InlineEnumItems.Count > 0)
        {
            var list = new EnumListInfo(field.Name);
            foreach (EnumItemInfo item in field.InlineEnumItems) list.Add(item);
            return list;
        }

        return field.EnumName != null ? GetEnumList(field.EnumName) : null;
    }

    private void ParseDefaultSkyTextures(IDictionary block)
    {
        foreach (DictionaryEntry e in block)
        {
            string skyTexture = e.Key.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(skyTexture)) continue;
            string mapsText = e.Value?.ToString() ?? "";
            foreach (string map in mapsText.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (map.Length > 0 && !defaultSkyTextures.ContainsKey(map))
                    defaultSkyTextures[map] = skyTexture;
            }
        }
    }

    private void ParseResourceRanges(IDictionary block, List<ResourceRangeInfo> ranges)
    {
        foreach (DictionaryEntry e in block)
        {
            string name = e.Key.ToString() ?? "";
            if (name.Length == 0 || e.Value is not IDictionary range) continue;
            string start = Convert.ToString(range["start"], CultureInfo.InvariantCulture) ?? "";
            string end = Convert.ToString(range["end"], CultureInfo.InvariantCulture) ?? "";
            if (start.Length == 0 || end.Length == 0) continue;
            ranges.Add(new ResourceRangeInfo(name, start, end));
        }
    }

    private void ParseMakeDoorFlags(IDictionary block)
    {
        foreach (DictionaryEntry e in block)
        {
            string flag = e.Key.ToString() ?? "";
            if (flag.Length == 0) continue;
            if (flag[0] == '-')
                makeDoorFlags[flag.TrimStart('-')] = false;
            else
                makeDoorFlags[flag] = true;
        }
    }

    private void ParseDefaultThingFlags(IDictionary block)
    {
        foreach (DictionaryEntry e in block)
        {
            string flag = e.Key.ToString() ?? "";
            if (flag.Length > 0 && thingFlagKeys.Contains(flag)) defaultThingFlags.Add(flag);
        }
    }

    private void ParseThingFlags(IDictionary src)
    {
        foreach (DictionaryEntry e in src)
        {
            string key = e.Key.ToString() ?? "";
            if (key.Length == 0) continue;
            thingFlagKeys.Add(key);
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) continue;
            string? value = Convert.ToString(e.Value, CultureInfo.InvariantCulture);
            if (value != null) thingFlags[number] = value;
        }
    }

    private void ParseThingFlagsCompare(IDictionary block)
    {
        foreach (DictionaryEntry e in block)
        {
            string groupName = e.Key.ToString() ?? "";
            if (groupName.Length == 0 || e.Value is not IDictionary group) continue;
            var flags = new Dictionary<string, ThingFlagCompareInfo>(StringComparer.Ordinal);

            foreach (DictionaryEntry child in group)
            {
                string flag = child.Key.ToString() ?? "";
                if (flag.Length == 0 || string.Equals(flag, "optional", StringComparison.OrdinalIgnoreCase)) continue;
                if (child.Value != null && child.Value is not IDictionary) continue;

                IDictionary? flagBlock = child.Value as IDictionary;
                flags[flag] = new ThingFlagCompareInfo(
                    flag,
                    flagBlock != null ? NormalizeThingFlagCompareMethod(GetString(flagBlock, "comparemethod", "and")) : "and",
                    flagBlock != null && GetBool(flagBlock, "invert", false),
                    flagBlock != null ? ParseCommaSet(GetString(flagBlock, "requiredgroups", "")) : new HashSet<string>(),
                    flagBlock != null ? ParseCommaSet(GetString(flagBlock, "ignoredgroups", "")) : new HashSet<string>(),
                    flagBlock != null ? GetString(flagBlock, "requiredflag", "") : "",
                    flagBlock != null && GetBool(flagBlock, "ingnorethisgroupwhenunset", false));
            }

            thingFlagsCompare[groupName] = new ThingFlagsCompareGroupInfo(
                groupName,
                GetBool(group, "optional", false),
                flags);
        }

        NormalizeThingFlagsCompareReferences();
    }

    private static string NormalizeThingFlagCompareMethod(string method)
        => method is "and" or "equal" ? method : "and";

    private void NormalizeThingFlagsCompareReferences()
    {
        var knownFlags = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in thingFlagsCompare.Values)
            foreach (string flag in group.Flags.Keys)
                knownFlags.Add(flag);

        foreach (var group in thingFlagsCompare.Values.ToArray())
        {
            var flags = new Dictionary<string, ThingFlagCompareInfo>(StringComparer.Ordinal);
            foreach (var flag in group.Flags.Values)
            {
                var requiredGroups = new HashSet<string>(
                    flag.RequiredGroups.Where(thingFlagsCompare.ContainsKey),
                    StringComparer.Ordinal);
                var ignoredGroups = new HashSet<string>(
                    flag.IgnoredGroups.Where(thingFlagsCompare.ContainsKey),
                    StringComparer.Ordinal);
                string requiredFlag = knownFlags.Contains(flag.RequiredFlag) ? flag.RequiredFlag : "";

                flags[flag.Flag] = flag with
                {
                    RequiredGroups = requiredGroups,
                    IgnoredGroups = ignoredGroups,
                    RequiredFlag = requiredFlag,
                };
            }

            thingFlagsCompare[group.Name] = group with { Flags = flags };
        }
    }

    private void ParseUniversalFields(IDictionary block)
    {
        foreach (DictionaryEntry elementEntry in block)
        {
            string element = elementEntry.Key.ToString() ?? "";
            if (element.Length == 0 || elementEntry.Value is not IDictionary fieldsBlock) continue;
            var fields = new Dictionary<string, UniversalFieldInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (DictionaryEntry fieldEntry in fieldsBlock)
            {
                string name = fieldEntry.Key.ToString() ?? "";
                if (name.Length == 0 || fieldEntry.Value is not IDictionary fieldBlock) continue;
                string normalizedName = name.ToLowerInvariant();
                int type = NormalizeUniversalFieldType(
                    GetInt(fieldBlock, "type", (int)UniversalType.Integer),
                    fieldBlock["enum"]);
                object? defaultValue = fieldBlock["default"];
                if (type == (int)UniversalType.String && defaultValue == null) defaultValue = "";
                fields[normalizedName] = new UniversalFieldInfo(
                    element,
                    normalizedName,
                    type,
                    defaultValue,
                    GetBool(fieldBlock, "thingtypespecific", false),
                    GetBool(fieldBlock, "managed", true),
                    fieldBlock["enum"] is string enumName ? enumName : null,
                    fieldBlock["enum"] is IDictionary inlineEnum ? ParseInlineEnum(inlineEnum) : Array.Empty<EnumItemInfo>(),
                    fieldBlock["associations"] is IDictionary associations ? ParseUniversalFieldAssociations(associations) : new Dictionary<string, UniversalFieldAssociationInfo>());
            }

            universalFields[element] = fields;
        }
    }

    private static int NormalizeUniversalFieldType(int type, object? enumSetting)
    {
        if (type == (int)UniversalType.EnumOption && enumSetting == null)
            return (int)UniversalType.Integer;
        return new UniversalTypeRegistry().IsKnown(type) ? type : (int)UniversalType.String;
    }

    private static IReadOnlyList<EnumItemInfo> ParseInlineEnum(IDictionary block)
    {
        var items = new List<EnumItemInfo>();
        foreach (DictionaryEntry e in block)
        {
            string value = e.Key.ToString() ?? "";
            if (value.Length > 0) items.Add(new EnumItemInfo(value, e.Value?.ToString() ?? value));
        }
        return items;
    }

    private static IReadOnlyDictionary<string, UniversalFieldAssociationInfo> ParseUniversalFieldAssociations(IDictionary block)
    {
        var associations = new Dictionary<string, UniversalFieldAssociationInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry e in block)
        {
            if (e.Value is not IDictionary association) continue;
            string property = GetString(association, "property", "");
            if (property.Length == 0) continue;
            associations[property] = new UniversalFieldAssociationInfo(
                property,
                GetString(association, "modify", ""),
                GetBool(association, "nevershoweventlines", false),
                GetBool(association, "consolidateeventlines", false));
        }
        return associations;
    }

    private void ParseThingsFilters(IDictionary block)
    {
        foreach (DictionaryEntry e in block)
        {
            string key = e.Key.ToString() ?? "";
            if (key.Length == 0) continue;
            IDictionary filter = e.Value as IDictionary ?? new Hashtable();

            var args = new int[5];
            for (int i = 0; i < args.Length; i++)
                args[i] = GetInt(filter, "arg" + i.ToString(CultureInfo.InvariantCulture), -1);

            var requiredFields = new List<string>();
            var forbiddenFields = new List<string>();
            if (filter["fields"] is IDictionary fields) ParseThingsFilterFields(fields, requiredFields, forbiddenFields);

            var customFields = filter["customfieldvalues"] is IDictionary values
                ? ParseThingsFilterCustomFields(values, filter["customfieldtypes"] as IDictionary)
                : new Dictionary<string, ThingsFilterCustomFieldInfo>(StringComparer.OrdinalIgnoreCase);

            thingsFilters.Add(new ThingsFilterInfo(
                key,
                GetString(filter, "name", "Unnamed filter"),
                GetString(filter, "category", ""),
                GetBool(filter, "invert", false),
                NormalizeThingsFilterDisplayMode(GetInt(filter, "displaymode", 0)),
                GetInt(filter, "type", -1),
                GetInt(filter, "angle", -1),
                GetInt(filter, "zheight", int.MinValue),
                GetInt(filter, "action", -1),
                args,
                GetInt(filter, "tag", -1),
                requiredFields,
                forbiddenFields,
                customFields));
        }
    }

    private static int NormalizeThingsFilterDisplayMode(int displayMode)
        => Math.Clamp(displayMode, 0, 2);

    private static void ParseThingsFilterFields(IDictionary fields, List<string> requiredFields, List<string> forbiddenFields)
    {
        foreach (DictionaryEntry field in fields)
        {
            string name = field.Key.ToString() ?? "";
            if (name.Length == 0 || field.Value is not bool required) continue;
            if (required)
                requiredFields.Add(name);
            else
                forbiddenFields.Add(name);
        }
    }

    private static IReadOnlyDictionary<string, ThingsFilterCustomFieldInfo> ParseThingsFilterCustomFields(
        IDictionary values,
        IDictionary? types)
    {
        var customFields = new Dictionary<string, ThingsFilterCustomFieldInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry field in values)
        {
            string name = field.Key.ToString() ?? "";
            if (name.Length == 0) continue;
            int type = types != null ? GetInt(types, name, 0) : 0;
            customFields[name] = new ThingsFilterCustomFieldInfo(name, type, field.Value);
        }
        return customFields;
    }

    private void ParseBrightnessLevels(IDictionary block)
    {
        foreach (DictionaryEntry e in block)
        {
            string key = e.Key.ToString() ?? "";
            if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
                brightnessLevels.Add(level);
        }
        brightnessLevels.Sort();
    }

    private static void ParseStringDictionary(IDictionary block, Dictionary<string, string> destination)
    {
        foreach (DictionaryEntry e in block)
        {
            string key = e.Key.ToString() ?? "";
            if (key.Length > 0) destination[key] = e.Value?.ToString() ?? "";
        }
    }

    private static void ParseStringSet(string text, HashSet<string> destination)
    {
        foreach (string value in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            destination.Add(value);
    }

    private static IReadOnlySet<string> ParseCommaSet(string text)
        => new HashSet<string>(text.Split(',', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

    /// <summary>True when a lump name is a configured map lump (excluding the ~MAP marker placeholder).</summary>
    public bool IsMapLump(string name)
        => mapLumpNames.TryGetValue(name, out var info) && !info.IsMarker;

    /// <summary>Rejects map marker names that overlap configured map lump names, matching UDB ConfigurationInfo.ValidateMapName.</summary>
    public bool ValidateMapName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        string normalized = name.ToUpperInvariant();
        foreach (string lumpName in mapLumpNames.Keys)
        {
            if (lumpName.ToUpperInvariant().Contains(normalized, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <summary>True when any configured map lump uses a static script configuration or scriptbuild compiler flow.</summary>
    public bool HasScriptLumps()
        => mapLumpNames.Values.Any(info => info.ScriptBuild || info.Script != null);

    private bool FormatInterfaceSupports(string capability)
    {
        if (string.IsNullOrWhiteSpace(FormatInterface)) return true;

        return FormatInterface switch
        {
            "DoomMapSetIO" => capability is "HasLinedefTag",
            "HexenMapSetIO" => capability is not "HasCustomFields" and not "HasLinedefTag",
            "UniversalMapSetIO" => true,
            _ => true,
        };
    }

    public static MapFormat MapFormatFromInterface(string? formatInterface)
        => formatInterface switch
        {
            "HexenMapSetIO" => MapFormat.Hexen,
            "UniversalMapSetIO" => MapFormat.Udmf,
            _ => MapFormat.Doom,
        };

    // Parses the maplumpnames block: each key is a lump name, each value its property sub-dict.
    private void ParseMapLumpNames(IDictionary block, ScriptConfigurationCatalog? scriptConfigurations)
    {
        foreach (DictionaryEntry e in block)
        {
            string name = e.Key.ToString() ?? "";
            IDictionary d = e.Value as IDictionary ?? new Hashtable();
            bool scriptBuild = GetBool(d, "scriptbuild", false);
            string? script = scriptBuild ? null : GetString(d, "script", "");
            if (script != null && script.Length == 0) script = null;
            mapLumpNames[name] = new MapLumpInfo
            {
                Name = name,
                Required = GetBool(d, "required", false),
                BlindCopy = GetBool(d, "blindcopy", false),
                NodeBuild = GetBool(d, "nodebuild", false),
                AllowEmpty = GetBool(d, "allowempty", false),
                Forbidden = GetBool(d, "forbidden", false),
                ScriptBuild = scriptBuild,
                Script = script,
                ScriptConfiguration = ResolveMapLumpScript(script, scriptConfigurations),
            };
        }
    }

    private static ScriptConfigurationInfo? ResolveMapLumpScript(string? script, ScriptConfigurationCatalog? scriptConfigurations)
    {
        if (string.IsNullOrEmpty(script) || scriptConfigurations == null) return null;
        return scriptConfigurations.Configurations.TryGetValue(script.ToLowerInvariant(), out var configuration)
            ? configuration
            : ScriptConfigurationInfo.PlainText;
    }

    // Parses a "<bit> = "<udmf spec>";" block into FlagTranslation entries (compound "a,b" / negated "!a").
    private void ParseFlagTranslations(IDictionary src, List<FlagTranslation> dest)
    {
        foreach (DictionaryEntry e in src)
        {
            if (!int.TryParse(e.Key.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int bit)) continue;
            string? spec = Convert.ToString(e.Value, CultureInfo.InvariantCulture);
            if (spec == null) continue;
            var ft = FlagTranslation.Parse(bit, spec);
            if (ft != null) dest.Add(ft);
        }

        dest.Sort((left, right) => right.Flag.CompareTo(left.Flag));
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
        var parsed = new List<SectorEffectInfo>();
        foreach (DictionaryEntry e in sectortypes)
        {
            string key = e.Key.ToString() ?? "";
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) continue;
            string? title = Convert.ToString(e.Value, CultureInfo.InvariantCulture);
            if (title != null)
                parsed.Add(new SectorEffectInfo { Index = number, Title = title });
        }

        parsed.Sort((left, right) => left.Index.CompareTo(right.Index));
        foreach (var effect in parsed)
            sectorEffects[effect.Index] = effect;
    }

    private static StaticLimitsInfo ParseStaticLimits(IDictionary block)
    {
        var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        values["visplanes"] = GetInt(block, "visplanes", StaticLimitsInfo.DefaultVisplanes);
        return new StaticLimitsInfo(values);
    }

    private void ParseRequiredArchives(IDictionary block)
    {
        foreach (DictionaryEntry e in block)
        {
            string name = e.Key.ToString() ?? "";
            IDictionary archive = e.Value as IDictionary ?? new Hashtable();
            var entries = new List<RequiredArchiveEntry>();
            foreach (DictionaryEntry child in archive)
            {
                string entryName = child.Key.ToString() ?? "";
                if (child.Value is not IDictionary entry) continue;
                entries.Add(new RequiredArchiveEntry(
                    entryName,
                    Convert.ToString(entry["lump"], CultureInfo.InvariantCulture),
                    Convert.ToString(entry["class"], CultureInfo.InvariantCulture)));
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
            IDictionary set = e.Value as IDictionary ?? new Hashtable();
            var filters = new List<string>();
            foreach (DictionaryEntry child in set)
            {
                string childKey = child.Key.ToString() ?? "";
                if (string.Equals(childKey, "name", StringComparison.Ordinal)) continue;
                string? filter = Convert.ToString(child.Value, CultureInfo.InvariantCulture);
                if (filter == null) continue;
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

        if (HasNumericLinedefActivations())
            linedefActivations.Sort((left, right) => left.Index.CompareTo(right.Index));
    }

    private static int ParseActivationIndex(string key)
        => int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) ? index : 0;

    private bool HasNumericLinedefActivations()
        => !string.Equals(FormatInterface, "UniversalMapSetIO", StringComparison.Ordinal);

    // Parses a flat "<int> = <display value>;" map (flags, etc.) into the destination dictionary.
    private void ParseFlatIntStrings(IDictionary src, Dictionary<int, string> dest)
    {
        foreach (DictionaryEntry e in src)
        {
            string key = e.Key.ToString() ?? "";
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) continue;
            string? value = Convert.ToString(e.Value, CultureInfo.InvariantCulture);
            if (value != null) dest[number] = value;
        }
    }

    // ---- scalar readers over the IDictionary tree ----

    private static int GetInt(IDictionary d, string key, int fallback)
    {
        object? value = d[key];
        if (value == null) return fallback;

        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return fallback;
        }
        catch (InvalidCastException)
        {
            return fallback;
        }
        catch (OverflowException)
        {
            return fallback;
        }
    }

    private static string GetString(IDictionary d, string key, string fallback)
    {
        object? value = d[key];
        return value == null ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
    }

    private static string GetFlagString(IDictionary d, string key, string fallback)
    {
        object? value = d[key];
        return value switch
        {
            int i => i.ToString(CultureInfo.InvariantCulture),
            null => fallback,
            _ => value.ToString() ?? fallback,
        };
    }

    private static bool GetBool(IDictionary d, string key, bool fallback)
    {
        object? value = d[key];
        if (value == null) return fallback;

        try
        {
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return fallback;
        }
        catch (InvalidCastException)
        {
            return fallback;
        }
    }

    private static double GetDouble(IDictionary d, string key, double fallback)
    {
        object? value = d[key];
        if (value == null) return fallback;

        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            if (value is string text && double.TryParse(text.TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                return parsed;
            return fallback;
        }
        catch (InvalidCastException)
        {
            return fallback;
        }
        catch (OverflowException)
        {
            return fallback;
        }
    }

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

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> EmptyFlagsRename
        = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ParseFlagsRename(IDictionary entry)
    {
        if (entry["flagsrename"] is not IDictionary main) return EmptyFlagsRename;
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry ioEntry in main)
        {
            string ioName = ioEntry.Key.ToString()?.ToLowerInvariant() ?? "";
            if (!IsSupportedFlagsRenameMapSet(ioName) || ioEntry.Value is not IDictionary flags) continue;
            var renamed = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (DictionaryEntry flagEntry in flags)
            {
                string flag = flagEntry.Key.ToString() ?? "";
                string title = flagEntry.Value?.ToString() ?? "";
                if (flag.Length > 0) renamed[flag] = title;
            }
            result[ioName] = renamed;
        }
        return result;
    }

    private static bool IsSupportedFlagsRenameMapSet(string value)
        => value is "doommapsetio" or "hexenmapsetio" or "universalmapsetio";

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? singular : plural ?? singular + "s")}";

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
