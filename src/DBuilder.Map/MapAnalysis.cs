// ABOUTME: Static map health checker reporting common geometry/structure problems in a MapSet.
// ABOUTME: A focused subset of UDB's error checkers; works off raw lists so it needs no BuildIndexes() call.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DBuilder.Geometry;

namespace DBuilder.Map;

public enum MapIssueSeverity { Warning, Error }

public enum MapAnalysisModeLifecycleAction
{
    Cancel,
    Engage,
    Disengage,
    Accept,
}

[Flags]
public enum ActionTextureCheckKind
{
    None = 0,
    FloorLowerToLowest = 1,
    FloorRaiseToNextHigher = 2,
    FloorRaiseToHighest = 4,
}

public enum MapIssueKind
{
    ZeroLengthLinedef,
    LinedefWithoutSidedefs,
    LinedefMissingFront,
    LinedefNotDoubleSided,
    LinedefNotSingleSided,
    MapTooBig,
    OverlappingVertices,
    VertexOverlappingLinedef,
    UnusedVertex,
    EmptySector,
    UnclosedSector,
    InvalidSector,
    // Context-aware checks (require a MapCheckContext):
    MissingTexture,
    UnknownTexture,
    UnusedTexture,
    MisalignedTexture,
    MissingFlat,
    UnknownFlat,
    UnknownThingType,
    ObsoleteThingType,
    UnusedThing,
    ThingOutsideMap,
    ThingStuckInLinedef,
    ThingStuckInThing,
    InvalidPolyobject,
    UnknownLinedefScript,
    UnknownThingScript,
    UnknownAction,
    UnknownSectorEffect,
    UnknownThingAction,
    OverlappingLinedefs,
    ShortLinedef,
    OffGridVertex,
    MissingActivation,
}

public sealed class MapIssueFix
{
    private readonly Func<MapSet, bool> apply;

    public MapIssueFix(string label, Func<MapSet, bool> apply)
    {
        Label = label;
        this.apply = apply;
    }

    public string Label { get; }

    public bool Apply(MapSet map) => apply(map);
}

public sealed record MapIssueFixOptions(
    string DefaultTopTexture = "STARTAN3",
    string DefaultWallTexture = "STARTAN3",
    string DefaultBottomTexture = "STARTAN3",
    string DefaultFloorTexture = "FLOOR0_1",
    string DefaultCeilingTexture = "CEIL1_1");

public sealed record MapAnalysisModeDescriptor(
    string DisplayName,
    string SwitchAction,
    string ButtonImage,
    int ButtonOrder,
    string ButtonGroup,
    bool AllowCopyPaste,
    bool Volatile,
    bool UseByDefault,
    string HelpTopic);

public sealed record MapAnalysisModeLifecyclePlan(
    MapAnalysisModeLifecycleAction Action,
    bool ReturnToPreviousStableMode,
    bool SetStandardPresentation,
    bool ClearMarks,
    bool MarkSelectedGeometry,
    bool ClearSelection,
    bool SetSelectionTypeAll,
    bool ShowAnalysisWindow,
    bool HideInfo,
    bool RestoreMarkedSelection,
    bool HideAnalysisWindow,
    bool SnapAllToAccuracy,
    bool UpdateMap,
    bool MarkMapChanged);

public sealed record MapAnalysisModeRedrawPlan(
    bool RedrawSurface,
    bool DrawLinedefsAndVertices,
    bool PlotSelectedResults,
    bool DrawThings,
    bool DrawOverlaySelection,
    bool Present);

public sealed record MapErrorCheckerDescriptor(
    string DisplayName,
    string ClassName,
    bool DefaultChecked,
    int Cost,
    IReadOnlyList<MapIssueKind> IssueKinds)
{
    public string SettingsKey => "errorchecks." + ClassName.ToLowerInvariant();

    public override string ToString() => DisplayName;
}

public sealed record MapErrorCheckerSelectionRow(MapErrorCheckerDescriptor Descriptor, bool IsChecked)
{
    public string DisplayName => Descriptor.DisplayName;
    public string SettingsKey => Descriptor.SettingsKey;
}

public sealed class MapErrorCheckerSelectionModel
{
    private readonly List<MapErrorCheckerSelectionRow> rows;

    public MapErrorCheckerSelectionModel(
        IEnumerable<MapErrorCheckerDescriptor> descriptors,
        IReadOnlyDictionary<string, bool>? savedChecks = null)
    {
        savedChecks ??= new Dictionary<string, bool>(StringComparer.Ordinal);
        rows = descriptors
            .OrderBy(descriptor => descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(descriptor => new MapErrorCheckerSelectionRow(
                descriptor,
                savedChecks.TryGetValue(descriptor.SettingsKey, out bool isChecked) ? isChecked : descriptor.DefaultChecked))
            .ToList();
    }

    public IReadOnlyList<MapErrorCheckerSelectionRow> Rows => rows;

    public IReadOnlyList<MapErrorCheckerDescriptor> EnabledDescriptors() =>
        rows.Where(row => row.IsChecked).Select(row => row.Descriptor).ToArray();

    public void SetChecked(string settingsKey, bool isChecked)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (string.Equals(row.SettingsKey, settingsKey, StringComparison.Ordinal))
            {
                rows[i] = row with { IsChecked = isChecked };
                return;
            }
        }
    }

    public IReadOnlyDictionary<string, bool> ToSettings() =>
        rows.ToDictionary(row => row.SettingsKey, row => row.IsChecked, StringComparer.Ordinal);
}

/// <summary>
/// Optional lookups that enable the resource/config-aware checks. Delegates are injected by the host (so this
/// project stays decoupled from resource/config code); a null delegate disables its check.
/// </summary>
public sealed class MapCheckContext
{
    public MapIssueFixOptions FixOptions { get; init; } = new();
    /// <summary>True when the current map uses UDMF; controls UDB checks that only run for UDMF maps.</summary>
    public bool IsUdmf { get; init; } = true;
    /// <summary>Returns true when a wall-texture name resolves in the loaded resources.</summary>
    public Func<string, bool>? TextureExists { get; init; }
    /// <summary>Returns wall-texture dimensions when an image is available.</summary>
    public Func<string, (int Width, int Height)?>? TextureSize { get; init; }
    /// <summary>Returns true when a flat name resolves in the loaded resources.</summary>
    public Func<string, bool>? FlatExists { get; init; }
    /// <summary>Returns true when a flat name is the configured sky flat marker.</summary>
    public Func<string, bool>? IsSkyFlat { get; init; }
    /// <summary>Returns true when a thing editor number is known to the game config.</summary>
    public Func<int, bool>? ThingTypeKnown { get; init; }
    /// <summary>Returns the configured display title for a thing type.</summary>
    public Func<int, string>? ThingTitle { get; init; }
    /// <summary>Runs host thing editing for UDB-style edit thing fixes; returns true when edits were accepted.</summary>
    public Func<Thing, bool>? EditThing { get; init; }
    /// <summary>Returns an obsolete warning for a known thing type, or null when the thing type is current.</summary>
    public Func<int, string?>? ThingObsoleteMessage { get; init; }
    /// <summary>Enable DECORATE-backed obsolete thing checks.</summary>
    public bool CheckObsoleteThings { get; init; } = true;
    /// <summary>Returns UDB thing error-check mode for a thing type, or null when unavailable.</summary>
    public Func<int, int?>? ThingErrorCheck { get; init; }
    /// <summary>Returns UDB thing blocking mode for a thing type, or null when unavailable.</summary>
    public Func<int, int?>? ThingBlocking { get; init; }
    /// <summary>Returns UDB thing height for a thing type, or null when unavailable.</summary>
    public Func<int, int?>? ThingHeight { get; init; }
    /// <summary>Returns true when two things can appear for overlapping flags, matching UDB thingflagscompare rules.</summary>
    public Func<Thing, Thing, bool>? ThingFlagsOverlap { get; init; }
    /// <summary>Returns UDB unused-thing warnings from thingflagscompare metadata.</summary>
    public Func<Thing, IReadOnlyList<string>>? ThingUnusedWarnings { get; init; }
    /// <summary>Configured thing flags applied by UDB's unused-thing default-flags fix.</summary>
    public IReadOnlyList<string> DefaultThingFlags { get; init; } = Array.Empty<string>();
    /// <summary>Returns the configured action id for a linedef action number, or null when unavailable.</summary>
    public Func<int, string?>? LinedefActionId { get; init; }
    /// <summary>Returns the configured class name for a thing type, or null when unavailable.</summary>
    public Func<int, string?>? ThingClassName { get; init; }
    /// <summary>Returns true when an ACS script number exists in the loaded map scripts.</summary>
    public Func<int, bool>? ScriptNumberExists { get; init; }
    /// <summary>Returns true when a named ACS script exists in the loaded map scripts.</summary>
    public Func<string, bool>? ScriptNameExists { get; init; }
    /// <summary>Returns true when a linedef action number is known (incl. generalized) to the game config.</summary>
    public Func<int, bool>? ActionKnown { get; init; }
    /// <summary>Returns a replacement linedef/thing action for UDB-style browse action fixes, or null when cancelled.</summary>
    public Func<int, int?>? BrowseAction { get; init; }
    /// <summary>Runs host linedef editing for UDB-style edit linedef fixes; returns true when edits were accepted.</summary>
    public Func<Linedef, bool>? EditLinedef { get; init; }
    /// <summary>Returns true when a sector effect number is known (incl. generalized) to the game config.</summary>
    public Func<int, bool>? SectorEffectKnown { get; init; }
    /// <summary>Returns a replacement sector effect for UDB-style browse effect fixes, or null when cancelled.</summary>
    public Func<int, int?>? BrowseSectorEffect { get; init; }
    /// <summary>Enable Hexen/UDMF thing action checks.</summary>
    public bool CheckThingActions { get; init; }
    /// <summary>Returns true when an action deliberately uses unresolved names in the given texture slot.</summary>
    public Func<int, SidedefPart, bool>? IgnoreUnknownTexture { get; init; }
    /// <summary>Returns a replacement wall texture name for UDB-style browse texture fixes, or null/"-" when cancelled.</summary>
    public Func<Sidedef, SidedefPart, string?>? BrowseTexture { get; init; }
    /// <summary>Returns a replacement flat name for UDB-style browse flat fixes, or null/"-" when cancelled.</summary>
    public Func<Sector, bool, string?>? BrowseFlat { get; init; }
    /// <summary>Returns true when a linedef action forces an upper texture even without a height gap.</summary>
    public Func<int, bool>? ActionRequiresUpperTexture { get; init; }
    /// <summary>Returns true when a linedef action and args match UDB Static_Init sky transfer behavior.</summary>
    public Func<int, int[], bool>? ActionHasSkyTransferStaticInit { get; init; }
    /// <summary>Returns UDB action-texture checks enabled for a linedef or thing action.</summary>
    public Func<int, ActionTextureCheckKind>? ActionTextureChecks { get; init; }
    /// <summary>Returns sector tags from action arguments for Hexen/UDMF action-texture checks.</summary>
    public Func<int, int[], IEnumerable<int>>? ActionTextureSectorTags { get; init; }
    /// <summary>Enable Hexen/UDMF thing action-texture checks.</summary>
    public bool CheckThingActionTextures { get; init; }
    /// <summary>Enable UDB Sector_Set3dFloor texture requirement checks.</summary>
    public bool CheckThreeDFloorTextures { get; init; }
    /// <summary>Returns true when a linedef action is UDB/ZDoom Plane_Align.</summary>
    public Func<int, bool>? ActionIsPlaneAlign { get; init; }
    /// <summary>Returns true when a linedef action requires an activation flag.</summary>
    public Func<int, bool>? ActionRequiresActivation { get; init; }
    /// <summary>UDMF linedef flags that activate an action; non-trigger flags are excluded.</summary>
    public IReadOnlySet<string>? TriggerActivationFlags { get; init; }
    /// <summary>Enable UDMF-only missing activation checks.</summary>
    public bool CheckMissingActivations { get; init; }
    /// <summary>Number of vertex decimal places used by UDB-style geometric checks; 0 disables rounding.</summary>
    public int VertexDecimals { get; init; }
    /// <summary>Enable Hexen/UDMF polyobject reference checks.</summary>
    public bool CheckPolyobjects { get; init; }
    /// <summary>Enable Hexen/UDMF unknown ACS script reference checks.</summary>
    public bool CheckScripts { get; init; }
    /// <summary>Enable UDMF named script checks through arg0str fields.</summary>
    public bool CheckNamedScripts { get; init; }
    /// <summary>Enable connected wall texture alignment checks.</summary>
    public bool CheckTextureAlignment { get; init; }
    /// <summary>Configured linedef flag that marks a line as double-sided.</summary>
    public string? DoubleSidedFlag { get; init; }
    /// <summary>Configured linedef flag that marks a line as impassable.</summary>
    public string? ImpassableFlag { get; init; }
    /// <summary>Maximum safe map width or height in map units; 0 disables the map-size check.</summary>
    public int SafeBoundary { get; init; }
}

/// <summary>A single detected map problem with a human-readable message and optional navigation hints.</summary>
public sealed record MapIssue(MapIssueSeverity Severity, MapIssueKind Kind, string Message)
{
    /// <summary>Detailed text shown in selected-result panels and copied descriptions.</summary>
    public string Description { get; init; } = Message;

    /// <summary>The offending element, so the editor can select it (null when the issue has no single element).</summary>
    public ISelectable? Target { get; init; }

    /// <summary>Additional map elements tied to this result, used for UDB-style ignored-error suppression.</summary>
    public IReadOnlyList<IMapElement> RelatedTargets { get; init; } = Array.Empty<IMapElement>();

    /// <summary>Error kind stored on map elements when this result is hidden.</summary>
    public MapIssueKind SuppressionKind { get; init; } = Kind;

    /// <summary>A representative world location to center the view on (null when unknown).</summary>
    public Vector2D? Focus { get; init; }

    /// <summary>Padding in map units for result navigation; short linedefs use UDB's tighter zoom area.</summary>
    public double FocusPadding { get; init; } = 100.0;

    public IReadOnlyList<MapIssueFix> Fixes { get; init; } = Array.Empty<MapIssueFix>();

    public IReadOnlyList<IMapElement> SuppressionTargets
    {
        get
        {
            var elements = new List<IMapElement>();
            if (Target is IMapElement target) elements.Add(target);
            elements.AddRange(RelatedTargets);
            return elements;
        }
    }

    public void SetIgnored(bool ignored)
    {
        foreach (var element in SuppressionTargets)
        {
            if (ignored) element.IgnoredErrorChecks.Add(SuppressionKind);
            else element.IgnoredErrorChecks.Remove(SuppressionKind);
        }
    }
}

public static class MapAnalysis
{
    private const string LinedefMissingSidesDescription =
        "This linedef is missing front and back sidedefs.A line must have at least a front side and optionally a back side!";
    private const string LinedefMissingFrontDescription =
        "This linedef has a back sidedef, but is missing a front sidedef. A line must have at least a front side and optionally a back side! Click 'Flip Linedef' button if the line is supposed to be single-sided.";
    private const string LinedefNotDoubleSidedDescription =
        "This linedef is marked as double-sided, but is missing the back sidedef. Click 'Make Single-Sided' button to remove the double-sided flag from the line.";
    private const string LinedefNotSingleSidedDescription =
        "This linedef is marked as single-sided, but has both a front and a back sidedef. Click 'Make Double-Sided' button to flag the line as double-sided. Or click 'Remove Sidedef' button to remove the sidedef on the back side (making the line really single-sided).";
    private const string StrayVertexDescription = "This vertex is not connected to any linedef.";
    private const string OffGridVertexDescription = "This vertex is not aligned with the grid.";
    private const string OverlappingVerticesDescription = "These vertices have the same position.";
    private const string VertexOverlappingLinedefDescription = "This vertex overlaps this linedef without splitting it.";
    private const string MissingTextureDescription =
        "This sidedef is missing a texture where it is required and could cause a 'Hall Of Mirrors' visual problem in the map.";
    private const string UnknownTextureDescription =
        "This sidedef uses an unknown texture. This could be the result of missing resources, or a mistyped texture name.";
    private const string UnusedTextureDescription =
        "This sidedef uses an upper or lower texture, which is not required (it will never be visible ingame). Click the Remove Texture button to remove the texture (this will also reset texture offsets and scale in UDMF map format).";
    private const string MisalignedTextureDescription =
        "Textures are not aligned on given sidedefs. Some players may not like that.";
    private const string UnknownLinedefActionDescription =
        "This linedef uses unknown action. This can potentially cause gameplay issues.";
    private const string UnknownThingActionDescription =
        "This thing uses unknown action. This can potentially cause gameplay issues.";
    private const string UnknownSectorEffectDescription =
        "This sector uses unknown effect. This can potentially cause gameplay issues.";
    private const string MissingActivationDescription =
        "This linedef has an assigned action, but no way to activate it has been set.";
    private const string UnknownThingDescription =
        "This thing has unknown type (it's not defined in DECORATE or current game configuration).";
    private const string ObsoleteThingDefaultDescription =
        "This thing is marked as obsolete in DECORATE. You should probably replace or delete it.";
    private const string UnusedThingDescription =
        "This thing won't be shown in any game mode.";
    private const string ThingOutsideMapDescription =
        "This thing is completely outside the map.";
    private const string ThingStuckInLinedefDescription =
        "This thing is stuck in a wall (single-sided line) and will likely not be able to move around.";
    private const string ThingStuckInThingDescription =
        "This thing is stuck in another thing. Both will likely not be able to move around.";
    private const string MapTooBigDescription = "Map is too big.";
    private const string UnclosedSectorDescription =
        "This sector is not a closed region and could cause problems with clipping and rendering in the game. The 'leaks' in the sector are indicated by the colored vertices.";
    private const string InvalidSectorDescription =
        "This sector has invalid geometry (it has less than 3 sidedefs or linedefs, or it's area is 0). This could cause problems with clipping and rendering in the game.";

    public static MapAnalysisModeDescriptor ModeDescriptor { get; } = new(
        "Map Analysis Mode",
        "errorcheckmode",
        "MapAnalysisMode.png",
        200,
        "002_tools",
        AllowCopyPaste: false,
        Volatile: true,
        UseByDefault: true,
        "e_mapanalysis.html");

    public static MapAnalysisModeRedrawPlan RedrawPlan { get; } = new(
        RedrawSurface: true,
        DrawLinedefsAndVertices: true,
        PlotSelectedResults: true,
        DrawThings: true,
        DrawOverlaySelection: true,
        Present: true);

    public static IReadOnlyList<MapErrorCheckerDescriptor> CheckerDescriptors { get; } =
    [
        new("Check texture alignment", "CheckTextureAlignment", false, 1000, [MapIssueKind.MisalignedTexture]),
        new("Check stuck things", "CheckStuckThings", true, 1000, [MapIssueKind.ThingOutsideMap, MapIssueKind.ThingStuckInLinedef, MapIssueKind.ThingStuckInThing]),
        new("Check overlapping lines", "CheckOverlappingLines", true, 500, [MapIssueKind.OverlappingLinedefs]),
        new("Check overlapping vertices", "CheckOverlappingVertices", true, 500, [MapIssueKind.OverlappingVertices, MapIssueKind.VertexOverlappingLinedef]),
        new("Check invalid sectors", "CheckClosedSectors", true, 300, [MapIssueKind.EmptySector, MapIssueKind.UnclosedSector, MapIssueKind.InvalidSector]),
        new("Check polyobjects", "CheckPolyobjects", true, 100, [MapIssueKind.InvalidPolyobject]),
        new("Check missing textures", "CheckMissingTextures", true, 80, [MapIssueKind.MissingTexture]),
        new("Check unknown textures", "CheckUnknownTextures", true, 60, [MapIssueKind.UnknownTexture]),
        new("Check unused textures", "CheckUnusedTextures", true, 60, [MapIssueKind.UnusedTexture]),
        new("Check unknown ACS scripts", "CheckUnknownScripts", true, 50, [MapIssueKind.UnknownLinedefScript, MapIssueKind.UnknownThingScript]),
        new("Check line references", "CheckLineReferences", true, 50, [MapIssueKind.LinedefMissingFront, MapIssueKind.LinedefWithoutSidedefs, MapIssueKind.LinedefNotDoubleSided, MapIssueKind.LinedefNotSingleSided]),
        new("Check off-grid vertices", "CheckOffGridVertices", true, 50, [MapIssueKind.OffGridVertex]),
        new("Check map size", "CheckMapSize", true, 50, [MapIssueKind.MapTooBig]),
        new("Check missing activations", "CheckMissingActivations", true, 50, [MapIssueKind.MissingActivation]),
        new("Check unknown actions/effects", "CheckUnknownActions", true, 50, [MapIssueKind.UnknownAction, MapIssueKind.UnknownSectorEffect, MapIssueKind.UnknownThingAction]),
        new("Check unknown things", "CheckUnknownThings", true, 50, [MapIssueKind.UnknownThingType]),
        new("Check unconnected vertices", "CheckStrayVertices", true, 50, [MapIssueKind.UnusedVertex]),
        new("Check obsolete things", "CheckObsoleteThings", true, 50, [MapIssueKind.ObsoleteThingType]),
        new("Check unused things", "CheckUnusedThings", true, 50, [MapIssueKind.UnusedThing]),
        new("Check missing flats", "CheckMissingFlats", true, 40, [MapIssueKind.MissingFlat]),
        new("Check unknown flats", "CheckUnknownFlats", true, 40, [MapIssueKind.UnknownFlat]),
        new("Check very short linedefs", "CheckShortLinedefs", false, 10, [MapIssueKind.ShortLinedef]),
    ];

    public static IReadOnlyList<MapErrorCheckerDescriptor> DefaultCheckerDescriptors { get; } =
        CheckerDescriptors.Where(descriptor => descriptor.DefaultChecked).ToArray();

    public static MapAnalysisModeLifecyclePlan ModeLifecyclePlan(MapAnalysisModeLifecycleAction action)
        => action switch
        {
            MapAnalysisModeLifecycleAction.Cancel => new MapAnalysisModeLifecyclePlan(
                action,
                ReturnToPreviousStableMode: true,
                SetStandardPresentation: false,
                ClearMarks: false,
                MarkSelectedGeometry: false,
                ClearSelection: false,
                SetSelectionTypeAll: false,
                ShowAnalysisWindow: false,
                HideInfo: false,
                RestoreMarkedSelection: false,
                HideAnalysisWindow: false,
                SnapAllToAccuracy: false,
                UpdateMap: false,
                MarkMapChanged: false),
            MapAnalysisModeLifecycleAction.Engage => new MapAnalysisModeLifecyclePlan(
                action,
                ReturnToPreviousStableMode: false,
                SetStandardPresentation: true,
                ClearMarks: true,
                MarkSelectedGeometry: true,
                ClearSelection: true,
                SetSelectionTypeAll: true,
                ShowAnalysisWindow: true,
                HideInfo: false,
                RestoreMarkedSelection: false,
                HideAnalysisWindow: false,
                SnapAllToAccuracy: false,
                UpdateMap: false,
                MarkMapChanged: false),
            MapAnalysisModeLifecycleAction.Disengage => new MapAnalysisModeLifecyclePlan(
                action,
                ReturnToPreviousStableMode: false,
                SetStandardPresentation: false,
                ClearMarks: true,
                MarkSelectedGeometry: false,
                ClearSelection: false,
                SetSelectionTypeAll: false,
                ShowAnalysisWindow: false,
                HideInfo: true,
                RestoreMarkedSelection: true,
                HideAnalysisWindow: true,
                SnapAllToAccuracy: false,
                UpdateMap: false,
                MarkMapChanged: false),
            MapAnalysisModeLifecycleAction.Accept => new MapAnalysisModeLifecyclePlan(
                action,
                ReturnToPreviousStableMode: true,
                SetStandardPresentation: false,
                ClearMarks: false,
                MarkSelectedGeometry: false,
                ClearSelection: false,
                SetSelectionTypeAll: false,
                ShowAnalysisWindow: false,
                HideInfo: false,
                RestoreMarkedSelection: false,
                HideAnalysisWindow: false,
                SnapAllToAccuracy: true,
                UpdateMap: true,
                MarkMapChanged: true),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };

    public static IReadOnlyList<MapIssue> FilterIssuesForCheckers(
        IEnumerable<MapIssue> issues,
        IEnumerable<MapErrorCheckerDescriptor> enabledCheckers)
    {
        var enabledKinds = enabledCheckers
            .SelectMany(descriptor => descriptor.IssueKinds)
            .ToHashSet();
        var checkerKinds = CheckerDescriptors
            .SelectMany(descriptor => descriptor.IssueKinds)
            .ToHashSet();

        return issues
            .Where(issue => !checkerKinds.Contains(issue.Kind) || enabledKinds.Contains(issue.Kind))
            .ToArray();
    }

    /// <summary>Scans the map and returns all detected issues (empty list when clean).</summary>
    public static IReadOnlyList<MapIssue> Check(MapSet map) => Check(map, null);

    public static IReadOnlyList<MapIssue> Check(
        MapSet map,
        MapCheckContext? ctx,
        IEnumerable<MapErrorCheckerDescriptor> enabledCheckers)
        => FilterIssuesForCheckers(Check(map, ctx), enabledCheckers);

    /// <summary>
    /// Scans the map, additionally running the resource/config-aware checks when <paramref name="ctx"/> is given.
    /// </summary>
    public static IReadOnlyList<MapIssue> Check(MapSet map, MapCheckContext? ctx)
    {
        var issues = new List<MapIssue>();

        var vertexIndex = new Dictionary<Vertex, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < map.Vertices.Count; i++) vertexIndex[map.Vertices[i]] = i;

        CheckLinedefs(map, ctx, issues);
        if (ctx?.DoubleSidedFlag != null)
            CheckLineReferenceFlags(map, ctx, issues);
        if (ctx != null)
            CheckMapSize(map, ctx, issues);
        CheckOverlappingVertices(map, issues);
        CheckVerticesOverlappingLinedefs(map, vertexIndex, issues);
        CheckUnusedVertices(map, vertexIndex, issues);
        CheckOffGridVertices(map, vertexIndex, issues);
        CheckSectors(map, issues);

        if (ctx != null)
        {
            CheckTextures(map, ctx, issues);
            CheckFlats(map, ctx, issues);
            CheckThingsAndActions(map, ctx, issues);
            CheckPolyobjects(map, ctx, issues);
            CheckTextureAlignment(map, ctx, issues);
            CheckOverlappingLinedefs(map, ctx, issues);
            CheckShortLinedefs(map, ctx, issues);
        }

        return issues.Where(issue => !IsIgnored(issue)).ToArray();
    }

    private static bool IsIgnored(MapIssue issue)
    {
        var elements = issue.SuppressionTargets;
        return elements.Count > 0 && elements.All(element => element.IgnoredErrorChecks.Contains(issue.SuppressionKind));
    }

    // A two-sided line needs an upper/lower texture where its sector is taller/lower than the neighbor; a
    // one-sided line needs a middle texture. Flags required-but-absent ("-") slots and unresolved names.
    private static void CheckTextures(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
        var actionTextures = ActionTextureTags.From(map, ctx);
        var threeDFloorTextures = ThreeDFloorTextureTags.From(map, ctx);

        for (int i = 0; i < map.Linedefs.Count; i++)
        {
            var l = map.Linedefs[i];
            CheckSide(l, l.Front, l.Back, i, "front");
            CheckSide(l, l.Back, l.Front, i, "back");
        }

        void CheckSide(Linedef l, Sidedef? side, Sidedef? other, int index, string which)
        {
            if (side == null) return;
            var mid = new Vector2D((l.Start.Position.x + l.End.Position.x) * 0.5, (l.Start.Position.y + l.End.Position.y) * 0.5);

            if (other == null)
            {
                if (IsBlank(side.MidTexture))
                    issues.Add(MissingTextureIssue(l, side, SidedefPart.Middle, ctx,
                        $"Linedef {index} has missing middle texture ({which} side)", mid));
            }
            else
            {
                if (side.Sector != null && other.Sector != null)
                {
                    bool heightGapNeedsUpper = side.HighRequired() && !IsSkyFlat(ctx, other.Sector.CeilTexture);
                    bool actionNeedsUpper = ctx.ActionRequiresUpperTexture?.Invoke(l.Action) == true || IsSkyTransferStaticInit(l, ctx);
                    if ((heightGapNeedsUpper || actionNeedsUpper || threeDFloorTextures.RequiresUpperTexture(side)) &&
                        IsBlank(side.HighTexture) &&
                        !SuppressPlaneAlignTexture(l, ctx, ceiling: true))
                        issues.Add(MissingTextureIssue(l, side, SidedefPart.Upper, ctx,
                            $"Linedef {index} has missing upper texture ({which} side)", mid));
                    if ((other.Sector.FloorHeight > side.Sector.FloorHeight && !IsSkyFlat(ctx, other.Sector.FloorTexture) ||
                         actionTextures.RequiresLowerTexture(side) ||
                         threeDFloorTextures.RequiresLowerTexture(side)) &&
                        IsBlank(side.LowTexture) &&
                        !SuppressPlaneAlignTexture(l, ctx, ceiling: false))
                        issues.Add(MissingTextureIssue(l, side, SidedefPart.Lower, ctx,
                            $"Linedef {index} has missing lower texture ({which} side)", mid));
                }
            }

            if (ctx.TextureExists != null)
                foreach (var (slot, part, name) in new[]
                {
                    ("upper", SidedefPart.Upper, side.HighTexture),
                    ("middle", SidedefPart.Middle, side.MidTexture),
                    ("lower", SidedefPart.Lower, side.LowTexture),
                })
                    if (!IsBlank(name) && !ctx.TextureExists(name) && ctx.IgnoreUnknownTexture?.Invoke(l.Action, part) != true)
                        issues.Add(UnknownTextureIssue(l, side, part, ctx,
                            $"Linedef {index} has unknown {slot} texture \"{name}\" ({which} side)", mid));

            if (!IsBlank(side.HighTexture) &&
                !side.HighRequired() &&
                ctx.ActionRequiresUpperTexture?.Invoke(l.Action) != true &&
                !IsSkyTransferStaticInit(l, ctx) &&
                !threeDFloorTextures.RequiresUpperTexture(side))
                issues.Add(UnusedTextureIssue(l, side, SidedefPart.Upper,
                    $"Sidedef {map.IndexOfSidedef(side)} has unused upper texture \"{side.HighTexture}\"", mid));

            if (!IsBlank(side.LowTexture) &&
                !side.LowRequired() &&
                !IsSkyTransferStaticInit(l, ctx) &&
                !actionTextures.RequiresLowerTexture(side) &&
                !threeDFloorTextures.RequiresLowerTexture(side))
                issues.Add(UnusedTextureIssue(l, side, SidedefPart.Lower,
                    $"Sidedef {map.IndexOfSidedef(side)} has unused lower texture \"{side.LowTexture}\"", mid));
        }
    }

    private static bool SuppressPlaneAlignTexture(Linedef line, MapCheckContext ctx, bool ceiling)
        => ctx.ActionIsPlaneAlign?.Invoke(line.Action) == true && line.Args[ceiling ? 1 : 0] > 0;

    private static bool IsSkyTransferStaticInit(Linedef line, MapCheckContext ctx)
        => ctx.ActionHasSkyTransferStaticInit?.Invoke(line.Action, line.Args) == true;

    private static void CheckFlats(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
        for (int i = 0; i < map.Sectors.Count; i++)
        {
            var s = map.Sectors[i];
            foreach (var (slot, name) in new[] { ("floor", s.FloorTexture), ("ceiling", s.CeilTexture) })
            {
                if (IsBlank(name))
                    issues.Add(MissingFlatIssue(s, slot == "ceiling", ctx,
                        $"Sector {i} has no {slot} flat."));
                else if (ctx.FlatExists != null && !ctx.FlatExists(name))
                    issues.Add(UnknownFlatIssue(s, slot == "ceiling", ctx,
                        $"Sector {i} has unknown {slot} flat \"{name}\""));
            }
        }
    }

    private sealed class ActionTextureTags
    {
        private readonly HashSet<int> floorLowerToLowest = new();
        private readonly HashSet<int> floorRaiseToNextHigher = new();
        private readonly HashSet<int> floorRaiseToHighest = new();

        private ActionTextureTags() { }

        public static ActionTextureTags From(MapSet map, MapCheckContext ctx)
        {
            var tags = new ActionTextureTags();
            if (ctx.ActionTextureChecks == null) return tags;

            foreach (var line in map.Linedefs)
            {
                var checks = ctx.ActionTextureChecks(line.Action);
                if (checks == ActionTextureCheckKind.None) continue;
                foreach (int tag in TagsForLine(line, ctx))
                    tags.Add(checks, tag);
            }

            if (ctx.CheckThingActionTextures)
            {
                foreach (var thing in map.Things)
                {
                    var checks = ctx.ActionTextureChecks(thing.Action);
                    if (checks == ActionTextureCheckKind.None) continue;
                    foreach (int tag in TagsFromActionArguments(thing.Action, thing.Args, ctx))
                        tags.Add(checks, tag);
                }
            }

            return tags;
        }

        public bool RequiresLowerTexture(Sidedef side)
        {
            if (side.Sector == null || side.Other?.Sector == null || side.Other.Sector == side.Sector)
                return false;

            foreach (int tag in MapElementTags.PositiveTags(side.Sector))
                if (RequiresTexture(side, tag, floorLowerToLowest) && FloorLowerToLowestNeedsLower(side))
                    return true;

            foreach (int tag in MapElementTags.PositiveTags(side.Other.Sector))
            {
                if (RequiresTexture(side.Other, tag, floorRaiseToNextHigher) && FloorRaiseToNextHigherNeedsLower(side.Other))
                    return true;
                if (RequiresTexture(side.Other, tag, floorRaiseToHighest) && FloorRaiseToHighestNeedsLower(side.Other))
                    return true;
            }

            return false;
        }

        private void Add(ActionTextureCheckKind checks, int tag)
        {
            if (tag <= 0) return;
            if ((checks & ActionTextureCheckKind.FloorLowerToLowest) != 0)
                floorLowerToLowest.Add(tag);
            if ((checks & ActionTextureCheckKind.FloorRaiseToNextHigher) != 0)
                floorRaiseToNextHigher.Add(tag);
            if ((checks & ActionTextureCheckKind.FloorRaiseToHighest) != 0)
                floorRaiseToHighest.Add(tag);
        }

        private static IEnumerable<int> TagsForLine(Linedef line, MapCheckContext ctx)
            => ctx.ActionTextureSectorTags == null
                ? MapElementTags.PositiveTags(line)
                : TagsFromActionArguments(line.Action, line.Args, ctx);

        private static IEnumerable<int> TagsFromActionArguments(int action, int[] args, MapCheckContext ctx)
            => ctx.ActionTextureSectorTags?.Invoke(action, args).Where(tag => tag > 0) ?? Array.Empty<int>();

        private static bool RequiresTexture(Sidedef side, int tag, HashSet<int> actionTags)
            => actionTags.Contains(tag)
                && side.Other?.Sector != null
                && side.Sector != null
                && side.Other.Sector != side.Sector
                && !MapElementTags.HasTag(side.Other.Sector, tag);

        private static bool FloorLowerToLowestNeedsLower(Sidedef side)
        {
            if (side.Sector == null || side.Other?.Sector == null) return false;

            int lowest = side.Sector.FloorHeight;
            foreach (var s in side.Sector.Sidedefs)
                if (s.Other?.Sector != null && s.Other.Sector != side.Sector && s.Other.Sector.FloorHeight < lowest)
                    lowest = s.Other.Sector.FloorHeight;

            return side.Other.Sector.FloorHeight > lowest;
        }

        private static bool FloorRaiseToNextHigherNeedsLower(Sidedef side)
        {
            if (side.Sector == null || side.Other?.Sector == null) return false;

            int? next = null;
            foreach (var s in side.Sector.Sidedefs)
                if (s.Other?.Sector != null
                    && s.Other.Sector.FloorHeight > side.Sector.FloorHeight
                    && (!next.HasValue || s.Other.Sector.FloorHeight < next.Value))
                    next = s.Other.Sector.FloorHeight;

            return next.HasValue && side.Other.Sector.FloorHeight < next.Value;
        }

        private static bool FloorRaiseToHighestNeedsLower(Sidedef side)
        {
            if (side.Sector == null || side.Other?.Sector == null) return false;

            int highest = side.Sector.FloorHeight;
            foreach (var s in side.Sector.Sidedefs)
                if (s.Other?.Sector != null && s.Other.Sector != side.Sector && s.Other.Sector.FloorHeight > highest)
                    highest = s.Other.Sector.FloorHeight;

            return side.Other.Sector.FloorHeight < highest;
        }
    }

    private sealed class ThreeDFloorTextureTags
    {
        private const int RenderInsideTypeBit = 4;
        private const int UseUpperFlag = 16;
        private const int UseLowerFlag = 32;

        private readonly Dictionary<int, Flags> byTag = new();

        private ThreeDFloorTextureTags() { }

        public static ThreeDFloorTextureTags From(MapSet map, MapCheckContext ctx)
        {
            var tags = new ThreeDFloorTextureTags();
            if (!ctx.CheckThreeDFloorTextures) return tags;

            foreach (var line in map.Linedefs)
            {
                if (line.Action != ThreeDFloors.Sector3DFloorAction || line.Args[0] <= 0) continue;

                Flags flags = Flags.None;
                if ((line.Args[1] & RenderInsideTypeBit) == RenderInsideTypeBit)
                    flags |= Flags.RenderInside;
                if ((line.Args[2] & UseUpperFlag) == UseUpperFlag)
                    flags |= Flags.UseUpper;
                if ((line.Args[2] & UseLowerFlag) == UseLowerFlag)
                    flags |= Flags.UseLower;

                if (flags == Flags.None) continue;
                tags.byTag[line.Args[0]] = tags.byTag.TryGetValue(line.Args[0], out Flags existing)
                    ? existing | flags
                    : flags;
            }

            return tags;
        }

        public bool RequiresUpperTexture(Sidedef side)
            => RequiresTexture(side, Flags.UseUpper);

        public bool RequiresLowerTexture(Sidedef side)
            => RequiresTexture(side, Flags.UseLower);

        private bool RequiresTexture(Sidedef side, Flags flag)
        {
            if (side.Sector == null || side.Other?.Sector == null || side.Other.Sector == side.Sector)
                return false;

            foreach (int tag in MapElementTags.PositiveTags(side.Sector))
                if (HasFlags(tag, flag | Flags.RenderInside))
                    return true;

            foreach (int tag in MapElementTags.PositiveTags(side.Other.Sector))
                if (HasFlags(tag, flag))
                    return true;

            return false;
        }

        private bool HasFlags(int tag, Flags flags)
            => byTag.TryGetValue(tag, out Flags existing) && (existing & flags) == flags;

        [Flags]
        private enum Flags
        {
            None = 0,
            UseUpper = 1,
            UseLower = 2,
            RenderInside = 4,
        }
    }

    private static void CheckThingsAndActions(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
        if (ctx.ThingTypeKnown != null)
            for (int i = 0; i < map.Things.Count; i++)
            {
                var t = map.Things[i];
                if (!ctx.ThingTypeKnown(t.Type))
                    issues.Add(DeleteThingIssue(MapIssueKind.UnknownThingType, t, ctx.EditThing,
                        $"Thing {i} has unknown type ({t.Type}).",
                        UnknownThingDescription));
            }

        if (ctx.CheckObsoleteThings && ctx.ThingObsoleteMessage != null)
            for (int i = 0; i < map.Things.Count; i++)
            {
                var t = map.Things[i];
                string? message = ctx.ThingObsoleteMessage(t.Type);
                if (!string.IsNullOrWhiteSpace(message))
                    issues.Add(DeleteThingIssue(MapIssueKind.ObsoleteThingType, t, ctx.EditThing,
                        $"Thing {ThingDisplay(ctx, t, i)} at {Coordinate(t.Position.x)}, {Coordinate(t.Position.y)} is obsolete.",
                        ObsoleteThingDescription(message)));
            }

        CheckThingsOutsideMap(map, ctx, issues);
        CheckUnusedThings(map, ctx, issues);
        CheckUnknownScripts(map, ctx, issues);

        if (ctx.ActionKnown != null)
        {
            for (int i = 0; i < map.Linedefs.Count; i++)
            {
                var l = map.Linedefs[i];
                if (l.Action != 0 && !ctx.ActionKnown(l.Action))
                    issues.Add(UnknownLinedefActionIssue(l, ctx,
                        $"Linedef {i} uses unknown action {l.Action}"));
            }

            if (ctx.CheckThingActions)
                for (int i = 0; i < map.Things.Count; i++)
                {
                    var t = map.Things[i];
                    if (t.Action != 0 && !ctx.ActionKnown(t.Action))
                        issues.Add(UnknownThingActionIssue(t, ctx,
                            $"Thing {i} uses unknown action {t.Action}"));
                }
        }

        if (ctx.SectorEffectKnown != null)
            for (int i = 0; i < map.Sectors.Count; i++)
            {
                var s = map.Sectors[i];
                if (s.Special != 0 && !ctx.SectorEffectKnown(s.Special))
                    issues.Add(UnknownSectorEffectIssue(s, ctx,
                        $"Sector {i} uses unknown effect {s.Special}"));
            }

        if (ctx.IsUdmf && ctx.CheckMissingActivations && ctx.ActionRequiresActivation != null && ctx.TriggerActivationFlags != null)
            for (int i = 0; i < map.Linedefs.Count; i++)
            {
                var l = map.Linedefs[i];
                if (l.Action == 0 || !ctx.ActionRequiresActivation(l.Action)) continue;
                bool hasActivation = false;
                foreach (var flag in ctx.TriggerActivationFlags)
                    if (l.UdmfFlags.Contains(flag)) { hasActivation = true; break; }

                if (!hasActivation)
                    issues.Add(MissingActivationIssue(l, ctx,
                        $"Linedef {i} has an action with no activation"));
            }
    }

    private static void CheckUnknownScripts(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
        if (!ctx.CheckScripts || (ctx.ScriptNumberExists == null && ctx.ScriptNameExists == null)) return;

        var acsSpecials = new HashSet<int> { 80, 81, 82, 83, 84, 85, 226 };
        for (int i = 0; i < map.Linedefs.Count; i++)
        {
            var line = map.Linedefs[i];
            if (!acsSpecials.Contains(line.Action)) continue;

            bool named = false;
            string scriptName = "";
            if (ctx.CheckNamedScripts)
                named = TryGetStringField(line.Fields, "arg0str", out scriptName);
            if (named)
            {
                if (ctx.ScriptNameExists != null && !ctx.ScriptNameExists(scriptName))
                    issues.Add(UnknownLinedefScriptIssue(line, ctx,
                        namedScript: true,
                        message: $"Linedef references unknown ACS script name \"{scriptName}\"."));
            }
            else if (ctx.ScriptNumberExists != null && !ctx.ScriptNumberExists(line.Args[0]))
            {
                issues.Add(UnknownLinedefScriptIssue(line, ctx,
                    namedScript: false,
                    message: $"Linedef references unknown ACS script number \"{line.Args[0]}\"."));
            }
        }

        for (int i = 0; i < map.Things.Count; i++)
        {
            var thing = map.Things[i];
            if (!acsSpecials.Contains(thing.Action)) continue;

            bool named = false;
            string scriptName = "";
            if (ctx.CheckNamedScripts)
                named = TryGetStringField(thing.Fields, "arg0str", out scriptName);
            if (named)
            {
                if (ctx.ScriptNameExists != null && !ctx.ScriptNameExists(scriptName))
                    issues.Add(DeleteThingIssue(MapIssueKind.UnknownThingScript, thing, ctx.EditThing,
                        $"Thing references unknown ACS script name \"{scriptName}\".",
                        UnknownThingScriptDescription(namedScript: true)));
            }
            else if (ctx.ScriptNumberExists != null && !ctx.ScriptNumberExists(thing.Args[0]))
            {
                issues.Add(DeleteThingIssue(MapIssueKind.UnknownThingScript, thing, ctx.EditThing,
                    $"Thing references unknown ACS script number \"{thing.Args[0]}\".",
                    UnknownThingScriptDescription(namedScript: false)));
            }
        }
    }

    private static void CheckUnusedThings(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
        if (ctx.ThingUnusedWarnings == null) return;

        for (int i = 0; i < map.Things.Count; i++)
        {
            var t = map.Things[i];
            var warnings = ctx.ThingUnusedWarnings(t);
            if (warnings.Count == 0) continue;

            issues.Add(UnusedThingIssue(t, ctx.DefaultThingFlags,
                $"Thing {ThingDisplay(ctx, t, i)} is unused. {string.Join(" ", warnings)}"));
        }
    }

    private static bool TryGetStringField(IReadOnlyDictionary<string, object> fields, string key, out string value)
    {
        if (fields.TryGetValue(key, out var raw))
        {
            value = raw?.ToString() ?? "";
            return true;
        }

        value = "";
        return false;
    }

    private static Vector2D LinedefMidpoint(Linedef line)
        => new((line.Start.Position.x + line.End.Position.x) * 0.5, (line.Start.Position.y + line.End.Position.y) * 0.5);

    private static MapIssue UnknownLinedefActionIssue(Linedef line, MapCheckContext ctx, string message)
    {
        var fixes = new List<MapIssueFix>
        {
            new("Remove Action", map =>
            {
                if (!map.Linedefs.Contains(line)) return false;
                line.Action = 0;
                return true;
            }),
        };

        if (ctx.BrowseAction != null)
        {
            fixes.Add(new MapIssueFix("Browse Action...", map =>
            {
                if (!map.Linedefs.Contains(line)) return false;
                int? action = ctx.BrowseAction(line.Action);
                if (action == null) return false;
                line.Action = action.Value;
                return true;
            }));
        }

        return new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnknownAction, message)
        {
            Description = UnknownLinedefActionDescription,
            Target = line,
            Focus = LinedefMidpoint(line),
            Fixes = fixes,
        };
    }

    private static MapIssue UnknownThingActionIssue(Thing thing, MapCheckContext ctx, string message)
    {
        var fixes = new List<MapIssueFix>
        {
            new("Remove Action", map =>
            {
                if (!map.Things.Contains(thing)) return false;
                thing.Action = 0;
                return true;
            }),
        };

        if (ctx.BrowseAction != null)
        {
            fixes.Add(new MapIssueFix("Browse Action...", map =>
            {
                if (!map.Things.Contains(thing)) return false;
                int? action = ctx.BrowseAction(thing.Action);
                if (action == null) return false;
                thing.Action = action.Value;
                return true;
            }));
        }

        return new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnknownThingAction, message)
        {
            Description = UnknownThingActionDescription,
            Target = thing,
            Focus = thing.Position,
            Fixes = fixes,
        };
    }

    private static MapIssue UnknownSectorEffectIssue(Sector sector, MapCheckContext ctx, string message)
    {
        var fixes = new List<MapIssueFix>
        {
            new("Remove Effect", map =>
            {
                if (!map.Sectors.Contains(sector)) return false;
                sector.Special = 0;
                return true;
            }),
        };

        if (ctx.BrowseSectorEffect != null)
        {
            fixes.Add(new MapIssueFix("Browse Effect...", map =>
            {
                if (!map.Sectors.Contains(sector)) return false;
                int? effect = ctx.BrowseSectorEffect(sector.Special);
                if (effect == null) return false;
                sector.Special = effect.Value;
                return true;
            }));
        }

        return new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnknownSectorEffect, message)
        {
            Description = UnknownSectorEffectDescription,
            Target = sector,
            Fixes = fixes,
        };
    }

    private static MapIssue MissingActivationIssue(Linedef line, MapCheckContext ctx, string message)
    {
        var fixes = new List<MapIssueFix>();
        if (ctx.EditLinedef != null)
        {
            fixes.Add(new MapIssueFix("Edit Linedef", map =>
            {
                if (!map.Linedefs.Contains(line)) return false;
                return ctx.EditLinedef(line);
            }));
        }

        return new MapIssue(MapIssueSeverity.Warning, MapIssueKind.MissingActivation, message)
        {
            Description = MissingActivationDescription,
            Target = line,
            Focus = LinedefMidpoint(line),
            Fixes = fixes,
        };
    }

    private static MapIssue UnknownLinedefScriptIssue(Linedef line, MapCheckContext ctx, bool namedScript, string message)
    {
        var fixes = new List<MapIssueFix>();
        if (ctx.EditLinedef != null)
        {
            fixes.Add(new MapIssueFix("Edit Linedef...", map =>
            {
                if (!map.Linedefs.Contains(line)) return false;
                return ctx.EditLinedef(line);
            }));
        }

        return new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnknownLinedefScript, message)
        {
            Description = UnknownLinedefScriptDescription(namedScript),
            Target = line,
            Focus = LinedefMidpoint(line),
            Fixes = fixes,
        };
    }

    private static string UnknownLinedefScriptDescription(bool namedScript)
        => $"This linedef references unknown ACS script {(namedScript ? "name" : "number")}.";

    private static string UnknownThingScriptDescription(bool namedScript)
        => $"This thing references unknown ACS script {(namedScript ? "name" : "number")}.";

    private static string ObsoleteThingDescription(string message)
        => string.IsNullOrEmpty(message)
            ? ObsoleteThingDefaultDescription
            : "This thing is marked as obsolete in DECORATE: " + message;

    private static void CheckThingsOutsideMap(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
        if (ctx.ThingErrorCheck == null) return;

        var processedThingPairs = new HashSet<(int, int)>();
        for (int i = 0; i < map.Things.Count; i++)
        {
            var t = map.Things[i];
            int errorCheck = ctx.ThingErrorCheck(t.Type) ?? 0;
            bool stuck = CheckThingStuckInLines(map, ctx, issues, t, i, errorCheck);
            stuck |= CheckThingStuckInThings(map, ctx, issues, t, i, errorCheck, processedThingPairs);
            if (stuck || errorCheck < 1) continue;

            var l = map.NearestLinedef(t.Position);
            if (l == null) continue;

            bool outside = l.SideOfLine(t.Position) <= 0.0 ? l.Front == null : l.Back == null;
            if (outside)
                issues.Add(DeleteThingIssue(MapIssueKind.ThingOutsideMap, t, null,
                    $"Thing {ThingDisplay(ctx, t, i)} is outside the map at {Coordinate(t.Position.x)}, {Coordinate(t.Position.y)}",
                    ThingOutsideMapDescription));
        }
    }

    private const double AllowedStuckDistance = 6.0;

    private static bool CheckThingStuckInLines(MapSet map, MapCheckContext ctx, List<MapIssue> issues, Thing thing, int thingIndex, int errorCheck)
    {
        if (errorCheck != 2 || (ctx.ThingBlocking?.Invoke(thing.Type) ?? 0) <= 0) return false;

        double blockingSize = thing.Size - AllowedStuckDistance;
        double left = thing.Position.x - blockingSize;
        double right = thing.Position.x + blockingSize;
        double top = thing.Position.y - blockingSize;
        double bottom = thing.Position.y + blockingSize;
        bool stuck = false;

        for (int lineIndex = 0; lineIndex < map.Linedefs.Count; lineIndex++)
        {
            var l = map.Linedefs[lineIndex];
            bool blocks = l.Back == null || IsLineFlagSet(l, ctx.ImpassableFlag);
            if (!blocks) continue;

            if (SegmentIntersectsRect(l.Start.Position, l.End.Position, left, right, top, bottom))
            {
                stuck = true;
                issues.Add(DeleteThingIssue(MapIssueKind.ThingStuckInLinedef, thing, null,
                    $"Thing {ThingDisplay(ctx, thing, thingIndex)} is stuck in linedef {lineIndex} at {Coordinate(thing.Position.x)}, {Coordinate(thing.Position.y)}",
                    ThingStuckInLinedefDescription));
            }
        }

        return stuck;
    }

    private static bool CheckThingStuckInThings(MapSet map, MapCheckContext ctx, List<MapIssue> issues, Thing thing, int thingIndex, int errorCheck, HashSet<(int, int)> processedPairs)
    {
        int blocking = ctx.ThingBlocking?.Invoke(thing.Type) ?? 0;
        if (errorCheck != 2 || blocking <= 0) return false;

        bool stuck = false;
        for (int otherIndex = 0; otherIndex < map.Things.Count; otherIndex++)
        {
            if (otherIndex == thingIndex) continue;
            var key = thingIndex < otherIndex ? (thingIndex, otherIndex) : (otherIndex, thingIndex);
            if (!processedPairs.Add(key)) continue;

            var other = map.Things[otherIndex];
            if ((ctx.ThingBlocking?.Invoke(other.Type) ?? 0) <= 0) continue;
            if (ctx.ThingFlagsOverlap != null && !ctx.ThingFlagsOverlap(thing, other)) continue;
            if (!ThingsOverlap(ctx, thing, other)) continue;

            stuck = true;
            issues.Add(ThingStuckInThingIssue(thing, other,
                $"Thing {ThingDisplay(ctx, thing, thingIndex)} is stuck in thing {ThingDisplay(ctx, other, otherIndex)} at {Coordinate(thing.Position.x)}, {Coordinate(thing.Position.y)}"));
        }

        return stuck;
    }

    private static string ThingDisplay(MapCheckContext ctx, Thing thing, int index)
        => $"{index} ({(ctx.ThingTitle?.Invoke(thing.Type) ?? thing.Type.ToString(CultureInfo.InvariantCulture))})";

    private static string Coordinate(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static bool ThingsOverlap(MapCheckContext ctx, Thing a, Thing b)
    {
        double aSize = a.Size - AllowedStuckDistance;
        double bSize = b.Size - AllowedStuckDistance;
        if (a.Position.x + aSize < b.Position.x - bSize ||
            a.Position.x - aSize > b.Position.x + bSize ||
            a.Position.y - aSize > b.Position.y + bSize ||
            a.Position.y + aSize < b.Position.y - bSize)
            return false;

        int aBlocking = ctx.ThingBlocking?.Invoke(a.Type) ?? 0;
        int bBlocking = ctx.ThingBlocking?.Invoke(b.Type) ?? 0;
        if (aBlocking == 1 || bBlocking == 1) return true;

        int aHeight = ctx.ThingHeight?.Invoke(a.Type) ?? 0;
        int bHeight = ctx.ThingHeight?.Invoke(b.Type) ?? 0;
        return !(a.Height > b.Height + bHeight || a.Height + aHeight < b.Height);
    }

    private static MapIssue DeleteThingIssue(MapIssueKind kind, Thing thing, string message)
        => DeleteThingIssue(kind, thing, null, message);

    private static MapIssue DeleteThingIssue(
        MapIssueKind kind,
        Thing thing,
        Func<Thing, bool>? editThing,
        string message,
        string? description = null)
    {
        var fixes = new List<MapIssueFix>();
        if (editThing != null)
        {
            fixes.Add(new MapIssueFix("Edit Thing...", map =>
            {
                if (!map.Things.Contains(thing)) return false;
                return editThing(thing);
            }));
        }

        fixes.Add(new MapIssueFix("Delete Thing", map =>
        {
            if (!map.Things.Contains(thing)) return false;
            map.RemoveThing(thing);
            return true;
        }));

        return new MapIssue(MapIssueSeverity.Warning, kind, message)
        {
            Description = description ?? message,
            Target = thing,
            Focus = thing.Position,
            Fixes = fixes,
        };
    }

    private static MapIssue UnusedThingIssue(Thing thing, IReadOnlyList<string> defaultFlags, string message)
        => new(MapIssueSeverity.Warning, MapIssueKind.UnusedThing, message)
        {
            Description = UnusedThingDescription,
            Target = thing,
            Focus = thing.Position,
            Fixes = new[]
            {
                new MapIssueFix("Delete Thing", map =>
                {
                    if (!map.Things.Contains(thing)) return false;
                    map.RemoveThing(thing);
                    return true;
                }),
                new MapIssueFix("Apply default flags", map =>
                {
                    if (!map.Things.Contains(thing)) return false;
                    foreach (string flag in defaultFlags)
                        if (!string.IsNullOrWhiteSpace(flag))
                            thing.SetFlag(flag, true);
                    return true;
                }),
            },
        };

    private static MapIssue ThingStuckInThingIssue(Thing first, Thing second, string message)
        => new(MapIssueSeverity.Warning, MapIssueKind.ThingStuckInThing, message)
        {
            Description = ThingStuckInThingDescription,
            Target = first,
            RelatedTargets = new[] { second },
            Focus = first.Position,
            Fixes = new[]
            {
                new MapIssueFix("Delete 1-st Thing", map =>
                {
                    if (!map.Things.Contains(first)) return false;
                    map.RemoveThing(first);
                    return true;
                }),
                new MapIssueFix("Delete 2-nd Thing", map =>
                {
                    if (!map.Things.Contains(second)) return false;
                    map.RemoveThing(second);
                    return true;
                }),
            },
        };

    private static bool SegmentIntersectsRect(Vector2D a, Vector2D b, double left, double right, double top, double bottom)
    {
        double minX = Math.Min(left, right);
        double maxX = Math.Max(left, right);
        double minY = Math.Min(top, bottom);
        double maxY = Math.Max(top, bottom);
        return Line2D.GetIntersection(a, b, minX, minY, maxX, minY) ||
               Line2D.GetIntersection(a, b, maxX, minY, maxX, maxY) ||
               Line2D.GetIntersection(a, b, maxX, maxY, minX, maxY) ||
               Line2D.GetIntersection(a, b, minX, maxY, minX, minY);
    }

    private static void CheckPolyobjects(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
        if (!ctx.CheckPolyobjects || ctx.LinedefActionId == null || ctx.ThingClassName == null) return;

        const string polyobjStartLine = "Polyobj_StartLine";
        const string polyobjExplicitLine = "Polyobj_ExplicitLine";
        var allActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            polyobjStartLine, polyobjExplicitLine,
            "Polyobj_RotateLeft", "Polyobj_RotateRight",
            "Polyobj_Move", "Polyobj_MoveTimes8",
            "Polyobj_DoorSwing", "Polyobj_DoorSlide",
            "Polyobj_OR_MoveToSpot", "Polyobj_MoveToSpot",
            "Polyobj_Stop", "Polyobj_MoveTo",
            "Polyobj_OR_MoveTo", "Polyobj_OR_RotateLeft",
            "Polyobj_OR_RotateRight", "Polyobj_OR_Move",
            "Polyobj_OR_MoveTimes8",
        };

        var polyLines = new Dictionary<string, Dictionary<int, List<(int Index, Linedef Line)>>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < map.Linedefs.Count; i++)
        {
            var line = map.Linedefs[i];
            string? id = line.Action > 0 ? ctx.LinedefActionId(line.Action) : null;
            if (id == null || !allActions.Contains(id)) continue;

            if (!polyLines.TryGetValue(id, out var byNumber))
            {
                byNumber = new Dictionary<int, List<(int, Linedef)>>();
                polyLines[id] = byNumber;
            }

            if (!byNumber.TryGetValue(line.Args[0], out var lines))
            {
                lines = new List<(int, Linedef)>();
                byNumber[line.Args[0]] = lines;
            }

            lines.Add((i, line));
        }

        var anchors = new Dictionary<int, List<(int Index, Thing Thing)>>();
        var startSpots = new Dictionary<int, List<(int Index, Thing Thing)>>();
        for (int i = 0; i < map.Things.Count; i++)
        {
            var thing = map.Things[i];
            string? className = ctx.ThingClassName(thing.Type)?.ToLowerInvariant();
            if (className == "$polyanchor") Add(anchors, thing.Angle, i, thing);
            else if (className is "$polyspawn" or "$polyspawncrush" or "$polyspawnhurt") Add(startSpots, thing.Angle, i, thing);
        }

        foreach (var group in polyLines)
            foreach (var linesByNumber in group.Value)
                if (!startSpots.ContainsKey(linesByNumber.Key))
                    AddPolyLineIssue(issues, linesByNumber.Value, $"\"{group.Key}\" action targets non-existing Polyobject Start Spot ({linesByNumber.Key})");

        if (polyLines.TryGetValue(polyobjStartLine, out var startLines))
            foreach (var linesByNumber in startLines)
            {
                if (linesByNumber.Value.Count > 1)
                    AddPolyLineIssue(issues, linesByNumber.Value, $"Several \"{polyobjStartLine}\" actions have the same Polyobject Number assigned ({linesByNumber.Key}). They won't function correctly ingame.");

                foreach (var line in linesByNumber.Value)
                    CheckMirror(linesByNumber.Key, line.Line.Args[1], line, polyobjStartLine);
            }

        if (polyLines.TryGetValue(polyobjExplicitLine, out var explicitLines))
            foreach (var linesByNumber in explicitLines)
                foreach (var line in linesByNumber.Value)
                    CheckMirror(linesByNumber.Key, line.Line.Args[2], line, polyobjStartLine);

        foreach (var group in anchors)
        {
            if (!startSpots.ContainsKey(group.Key))
                AddPolyThingIssue(issues, group.Value, $"Polyobject {(group.Value.Count > 1 ? "Anchors target" : "Anchor targets")} non-existing Polyobject Start Spot ({group.Key})");
            if (group.Value.Count > 1)
                AddPolyThingIssue(issues, group.Value, $"Several Polyobject Anchors target the same Polyobject Start Spot ({group.Key}). They won't function correctly ingame.");
        }

        foreach (var group in startSpots)
        {
            if (!anchors.ContainsKey(group.Key))
                AddPolyThingIssue(issues, group.Value, $"Polyobject Start {(group.Value.Count > 1 ? "Spots are not targeted" : "Spot " + group.Key.ToString(CultureInfo.InvariantCulture) + " is not targeted")} by any Polyobject Anchor");
            if (group.Value.Count > 1)
                AddPolyThingIssue(issues, group.Value, $"Several Polyobject Start Spots have the same Polyobject number ({group.Key}). They won't function correctly ingame.");
        }

        void CheckMirror(int polyNumber, int mirrorNumber, (int Index, Linedef Line) line, string actionId)
        {
            if (mirrorNumber <= 0) return;
            if (!startSpots.ContainsKey(mirrorNumber))
                AddPolyLineIssue(issues, new[] { line }, $"\"{actionId}\" action have non-existing Mirror Polyobject Number assigned ({mirrorNumber}). It won't function correctly ingame.");
            if (mirrorNumber == polyNumber)
                AddPolyLineIssue(issues, new[] { line }, $"\"{actionId}\" action have the same Polyobject and Mirror Polyobject numbers assigned ({mirrorNumber}). It won't function correctly ingame.");
        }

        static void Add(Dictionary<int, List<(int Index, Thing Thing)>> map, int number, int index, Thing thing)
        {
            if (!map.TryGetValue(number, out var things))
            {
                things = new List<(int, Thing)>();
                map[number] = things;
            }
            things.Add((index, thing));
        }
    }

    private static void AddPolyLineIssue(List<MapIssue> issues, IEnumerable<(int Index, Linedef Line)> lines, string message)
    {
        var items = lines.Where(item => item.Line != null).ToArray();
        var elements = items.Select(item => item.Line).ToArray();
        var first = elements.FirstOrDefault();
        if (first == null) return;
        string title = PolyobjectElementTitle("linedef", "linedefs", items.Select(item => item.Index));
        issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.InvalidPolyobject, title)
        {
            Description = title + ": " + message,
            Target = first,
            RelatedTargets = elements.Skip(1).Cast<IMapElement>().ToArray(),
            Focus = new Vector2D((first.Start.Position.x + first.End.Position.x) * 0.5, (first.Start.Position.y + first.End.Position.y) * 0.5),
        });
    }

    private static void AddPolyThingIssue(List<MapIssue> issues, IEnumerable<(int Index, Thing Thing)> things, string message)
    {
        var items = things.Where(item => item.Thing != null).ToArray();
        var elements = items.Select(item => item.Thing).ToArray();
        var first = elements.FirstOrDefault();
        if (first == null) return;
        string title = PolyobjectElementTitle("thing", "things", items.Select(item => item.Index));
        issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.InvalidPolyobject, title)
        {
            Description = title + ": " + message,
            Target = first,
            RelatedTargets = elements.Skip(1).Cast<IMapElement>().ToArray(),
            Focus = first.Position,
        });
    }

    private static string PolyobjectElementTitle(string singular, string plural, IEnumerable<int> indexes)
    {
        int[] values = indexes.ToArray();
        if (values.Length == 1)
            return "Incorrect Polyobject setup for " + singular + " " + values[0].ToString(CultureInfo.InvariantCulture);

        return "Incorrect Polyobject setup for " + plural + " " + JoinedIndexes(values);
    }

    private static string JoinedIndexes(IReadOnlyList<int> indexes)
    {
        if (indexes.Count == 0) return string.Empty;
        if (indexes.Count == 1) return indexes[0].ToString(CultureInfo.InvariantCulture);
        if (indexes.Count == 2)
            return indexes[0].ToString(CultureInfo.InvariantCulture) + " and " + indexes[1].ToString(CultureInfo.InvariantCulture);

        return string.Join(", ", indexes.Take(indexes.Count - 1).Select(index => index.ToString(CultureInfo.InvariantCulture)))
            + " and " + indexes[^1].ToString(CultureInfo.InvariantCulture);
    }

    private static void CheckTextureAlignment(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
        if (!ctx.CheckTextureAlignment || ctx.TextureSize == null) return;

        var lineIndexes = new Dictionary<Linedef, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < map.Linedefs.Count; i++) lineIndexes[map.Linedefs[i]] = i;
        var sideIndexes = new Dictionary<Sidedef, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < map.Sidedefs.Count; i++) sideIndexes[map.Sidedefs[i]] = i;

        var checkedPairs = new HashSet<(int Source, int Target)>();
        foreach (var side in AllSidedefs(map))
        {
            string texture = RequiredAlignmentTexture(side);
            if (IsBlank(texture)) continue;
            var size = ctx.TextureSize(texture);
            if (size is not { Width: > 0, Height: > 0 }) continue;

            var target = NextAlignedSidedef(map, side, texture);
            int sideIndex = sideIndexes.TryGetValue(side, out int sdi) ? sdi : -1;
            int targetSideIndex = target != null && sideIndexes.TryGetValue(target, out int tsi) ? tsi : -1;
            if (target == null || !checkedPairs.Add((sideIndex, targetSideIndex))) continue;

            int expectedX = Mod(side.OffsetX + (int)Math.Round(side.Line.Length), size.Value.Width);
            int expectedY = Mod(side.OffsetY + TopReference(side) - TopReference(target), size.Value.Height);
            if (target.OffsetX == expectedX && target.OffsetY == expectedY) continue;

            int sourceIndex = lineIndexes.TryGetValue(side.Line, out int si) ? si : -1;
            int targetIndex = lineIndexes.TryGetValue(target.Line, out int ti) ? ti : -1;
            issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.MisalignedTexture,
                $"Texture \"{texture}\" is not aligned on linedefs {sourceIndex} ({(side.IsFront ? "front" : "back")}) and {targetIndex} ({(target.IsFront ? "front" : "back")})")
                { Description = MisalignedTextureDescription, Target = side.Line, RelatedTargets = new[] { target.Line }, Focus = LinedefMidpoint(side.Line) });
        }
    }

    private static IEnumerable<Sidedef> AllSidedefs(MapSet map)
    {
        foreach (var line in map.Linedefs)
        {
            if (line.Front != null) yield return line.Front;
            if (line.Back != null) yield return line.Back;
        }
    }

    private static Sidedef? NextAlignedSidedef(MapSet map, Sidedef side, string texture)
    {
        var line = side.Line;
        Vertex forward = side.IsFront ? line.End : line.Start;
        foreach (var other in map.Linedefs)
        {
            if (ReferenceEquals(other, line)) continue;
            if (other.Front != null && ReferenceEquals(other.Start, forward) && SameTexture(RequiredAlignmentTexture(other.Front), texture))
                return other.Front;
            if (other.Back != null && ReferenceEquals(other.End, forward) && SameTexture(RequiredAlignmentTexture(other.Back), texture))
                return other.Back;
        }

        return null;
    }

    private static int TopReference(Sidedef side) => side.Sector?.CeilHeight ?? 0;

    private static bool SameTexture(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string RequiredAlignmentTexture(Sidedef side)
    {
        if (side.MiddleRequired() && !IsBlank(side.MidTexture)) return side.MidTexture;
        if (side.HighRequired() && !IsBlank(side.HighTexture)) return side.HighTexture;
        if (side.LowRequired() && !IsBlank(side.LowTexture)) return side.LowTexture;
        return "-";
    }

    private static int Mod(int value, int modulus)
        => ((value % modulus) + modulus) % modulus;

    // Two linedefs sharing both endpoints or crossing through their interiors overlap; report each extra one once.
    private static void CheckOverlappingLinedefs(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
        var seen = new Dictionary<(Vector2D, Vector2D), Linedef>();
        for (int i = 0; i < map.Linedefs.Count; i++)
        {
            var l = map.Linedefs[i];
            var a = l.Start.Position;
            var b = l.End.Position;
            var key = Compare(a, b) <= 0 ? (a, b) : (b, a);
            if (seen.TryGetValue(key, out var matchingLine))
                issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.OverlappingLinedefs,
                    $"Linedefs {i} and {map.IndexOfLinedef(matchingLine)} are overlapping and reference different sectors")
                    {
                        Description = "These linedefs are overlapping and they do not reference the same sector on all sides. Overlapping lines is only allowed when they reference the same sector on all sides.",
                        Target = l,
                        RelatedTargets = new[] { matchingLine },
                        Focus = new Vector2D((l.Start.Position.x + l.End.Position.x) * 0.5, (l.Start.Position.y + l.End.Position.y) * 0.5),
                    });
            else
                seen[key] = l;

            for (int j = 0; j < i; j++)
            {
                var other = map.Linedefs[j];
                if (!l.Line.GetIntersection(other.Line, out double uLine, out double uOther)) continue;
                if (ctx.VertexDecimals > 0)
                {
                    uLine = Math.Round(uLine, ctx.VertexDecimals);
                    uOther = Math.Round(uOther, ctx.VertexDecimals);
                }
                if (uLine <= 0.0 || uLine >= 1.0 || uOther <= 0.0 || uOther >= 1.0) continue;
                if (ReferencesSameSectorOnAllSides(l, other)) continue;

                issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.OverlappingLinedefs,
                    $"Linedefs {i} and {j} are overlapping and reference different sectors")
                    {
                        Description = "These linedefs are overlapping and they do not reference the same sector on all sides. Overlapping lines is only allowed when they reference the same sector on all sides.",
                        Target = l,
                        RelatedTargets = new[] { other },
                        Focus = l.Line.GetCoordinatesAt(uLine),
                    });
                break;
            }
        }

        static int Compare(Vector2D a, Vector2D b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);
    }

    private static bool ReferencesSameSectorOnAllSides(Linedef a, Linedef b)
    {
        Sector? sector = a.Front?.Sector ?? a.Back?.Sector ?? b.Front?.Sector ?? b.Back?.Sector;
        return sector != null &&
               a.Front?.Sector == sector &&
               a.Back?.Sector == sector &&
               b.Front?.Sector == sector &&
               b.Back?.Sector == sector;
    }

    private static void CheckShortLinedefs(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
        if (!ctx.IsUdmf) return;

        for (int i = 0; i < map.Linedefs.Count; i++)
        {
            var l = map.Linedefs[i];
            double len = (l.End.Position - l.Start.Position).GetLength();
            if (len < 1.0)
                issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.ShortLinedef,
                    $"Linedef {i} is shorter than 1 mu.")
                    {
                        Description = "This linedef is shorter than 1 map unit. This can potentially cause nodebuilding errors.",
                        Target = l,
                        Focus = new Vector2D((l.Start.Position.x + l.End.Position.x) * 0.5, (l.Start.Position.y + l.End.Position.y) * 0.5),
                        FocusPadding = 3.0,
                    });
        }
    }

    private static void CheckOffGridVertices(MapSet map, Dictionary<Vertex, int> index, List<MapIssue> issues)
    {
        foreach (var v in map.Vertices)
            if (!IsWholeMapUnit(v.Position.x) || !IsWholeMapUnit(v.Position.y))
                issues.Add(OffGridVertexIssue(v,
                    $"Vertex {index[v]} at {v.Position.x}, {v.Position.y} is not aligned with the grid."));
    }

    private static bool IsWholeMapUnit(double value)
        => value == (int)value;

    private static MapIssue OffGridVertexIssue(Vertex vertex, string message)
        => new(MapIssueSeverity.Warning, MapIssueKind.OffGridVertex, message)
        {
            Description = OffGridVertexDescription,
            Target = vertex,
            Focus = vertex.Position,
            Fixes = new[]
            {
                new MapIssueFix("Align Vertex", map =>
                {
                    if (!map.Vertices.Contains(vertex)) return false;
                    vertex.Move(
                        Math.Round(vertex.Position.x),
                        Math.Round(vertex.Position.y));
                    map.BuildIndexes();
                    return true;
                }),
            },
        };

    private static MapIssue MissingTextureIssue(
        Linedef line,
        Sidedef side,
        SidedefPart part,
        MapCheckContext ctx,
        string message,
        Vector2D focus)
    {
        var fixes = new List<MapIssueFix>
        {
            new("Add Default Texture", map =>
            {
                if (!map.Sidedefs.Contains(side)) return false;
                side.SetTexture(part, DefaultTexture(part, ctx.FixOptions));
                return true;
            }),
        };

        if (ctx.BrowseTexture != null)
        {
            fixes.Add(new MapIssueFix("Browse Texture...", map =>
            {
                if (!map.Sidedefs.Contains(side)) return false;
                string? texture = ctx.BrowseTexture(side, part);
                if (IsBlank(texture)) return false;
                side.SetTexture(part, texture);
                return true;
            }));
        }

        return new MapIssue(MapIssueSeverity.Error, MapIssueKind.MissingTexture, message)
        {
            Description = MissingTextureDescription,
            Target = line,
            Focus = focus,
            Fixes = fixes,
        };
    }

    private static MapIssue UnusedTextureIssue(
        Linedef line,
        Sidedef side,
        SidedefPart part,
        string message,
        Vector2D focus)
        => new(MapIssueSeverity.Warning, MapIssueKind.UnusedTexture, message)
        {
            Description = UnusedTextureDescription,
            Target = line,
            Focus = focus,
            Fixes = new[]
            {
                new MapIssueFix("Remove Texture", map =>
                {
                    if (!map.Sidedefs.Contains(side)) return false;
                    side.SetTexture(part, "-");
                    if (part == SidedefPart.Upper)
                        side.RemoveFields(new[] { "scalex_top", "scaley_top", "offsetx_top", "offsety_top" });
                    else if (part == SidedefPart.Lower)
                        side.RemoveFields(new[] { "scalex_bottom", "scaley_bottom", "offsetx_bottom", "offsety_bottom" });
                    return true;
                }),
            },
        };

    private static MapIssue UnknownTextureIssue(
        Linedef line,
        Sidedef side,
        SidedefPart part,
        MapCheckContext ctx,
        string message,
        Vector2D focus)
    {
        var fixes = new List<MapIssueFix>
        {
            new("Remove Texture", map =>
            {
                if (!map.Sidedefs.Contains(side)) return false;
                side.SetTexture(part, "-");
                return true;
            }),
            new("Add Default Texture", map =>
            {
                if (!map.Sidedefs.Contains(side)) return false;
                side.SetTexture(part, DefaultTexture(part, ctx.FixOptions));
                return true;
            }),
        };

        if (ctx.BrowseTexture != null)
        {
            fixes.Add(new MapIssueFix("Browse Texture...", map =>
            {
                if (!map.Sidedefs.Contains(side)) return false;
                string? texture = ctx.BrowseTexture(side, part);
                if (IsBlank(texture)) return false;
                side.SetTexture(part, texture);
                return true;
            }));
        }

        return new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnknownTexture, message)
        {
            Description = UnknownTextureDescription,
            Target = line,
            Focus = focus,
            Fixes = fixes,
        };
    }

    private static MapIssue MissingFlatIssue(Sector sector, bool ceiling, MapCheckContext ctx, string message)
    {
        var fixes = FlatFixes(sector, ceiling, ctx);

        return new MapIssue(MapIssueSeverity.Error, MapIssueKind.MissingFlat, message)
        {
            Description = MissingFlatDescription(ceiling),
            Target = sector,
            Fixes = fixes,
        };
    }

    private static MapIssue UnknownFlatIssue(Sector sector, bool ceiling, MapCheckContext ctx, string message)
    {
        var fixes = FlatFixes(sector, ceiling, ctx);

        return new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnknownFlat, message)
        {
            Description = UnknownFlatDescription(ceiling),
            Target = sector,
            Fixes = fixes,
        };
    }

    private static string MissingFlatDescription(bool ceiling)
        => $"This sector's {(ceiling ? "ceiling" : "floor")} is missing a flat where it is required and could cause a 'Hall Of Mirrors' visual problem in the map.";

    private static string UnknownFlatDescription(bool ceiling)
        => $"This sector's {(ceiling ? "ceiling" : "floor")} uses an unknown flat. This could be the result of missing resources, or a mistyped flat name.";

    private static IReadOnlyList<MapIssueFix> FlatFixes(Sector sector, bool ceiling, MapCheckContext ctx)
    {
        var fixes = new List<MapIssueFix>
        {
            new("Add Default Flat", map =>
            {
                if (!map.Sectors.Contains(sector)) return false;
                if (ceiling) sector.SetCeilTexture(ctx.FixOptions.DefaultCeilingTexture);
                else sector.SetFloorTexture(ctx.FixOptions.DefaultFloorTexture);
                return true;
            }),
        };

        if (ctx.BrowseFlat != null)
        {
            fixes.Add(new MapIssueFix("Browse Flat...", map =>
            {
                if (!map.Sectors.Contains(sector)) return false;
                string? flat = ctx.BrowseFlat(sector, ceiling);
                if (IsBlank(flat)) return false;
                if (ceiling) sector.SetCeilTexture(flat);
                else sector.SetFloorTexture(flat);
                return true;
            }));
        }

        return fixes;
    }

    private static string DefaultTexture(SidedefPart part, MapIssueFixOptions options) => part switch
    {
        SidedefPart.Upper => options.DefaultTopTexture,
        SidedefPart.Lower => options.DefaultBottomTexture,
        _ => options.DefaultWallTexture,
    };

    private static bool IsBlank(string? tex) => string.IsNullOrEmpty(tex) || tex == "-";

    private static bool IsSkyFlat(MapCheckContext ctx, string? flat)
        => !IsBlank(flat) && ctx.IsSkyFlat?.Invoke(flat!) == true;

    private static void CheckLinedefs(MapSet map, MapCheckContext? ctx, List<MapIssue> issues)
    {
        for (int i = 0; i < map.Linedefs.Count; i++)
        {
            var l = map.Linedefs[i];
            var mid = new Vector2D((l.Start.Position.x + l.End.Position.x) * 0.5, (l.Start.Position.y + l.End.Position.y) * 0.5);
            double dx = l.End.Position.x - l.Start.Position.x;
            double dy = l.End.Position.y - l.Start.Position.y;
            if (dx * dx + dy * dy < 1e-9)
                issues.Add(new MapIssue(MapIssueSeverity.Error, MapIssueKind.ZeroLengthLinedef,
                    $"Linedef {i} has zero length.") { Target = l, Focus = mid });

            if (l.Front == null && l.Back == null)
                issues.Add(LinedefWithoutSidedefsIssue(map, l, ctx?.DoubleSidedFlag,
                    $"Linedef {i} is missing both sides", mid));
            else if (l.Front == null)
                issues.Add(MissingFrontIssue(map, l, ctx?.DoubleSidedFlag,
                    $"Linedef {i} is missing front side", mid));
        }
    }

    private static void CheckLineReferenceFlags(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
        for (int i = 0; i < map.Linedefs.Count; i++)
        {
            var l = map.Linedefs[i];
            if (l.Front == null) continue;

            var mid = new Vector2D((l.Start.Position.x + l.End.Position.x) * 0.5, (l.Start.Position.y + l.End.Position.y) * 0.5);
            bool markedDoubleSided = IsLineFlagSet(l, ctx.DoubleSidedFlag);
            if (markedDoubleSided && l.Back == null)
                issues.Add(LineNotDoubleSidedIssue(map, l, ctx.DoubleSidedFlag,
                    $"Linedef {i} is marked double-sided but has no back side", mid));
            else if (!markedDoubleSided && l.Back != null)
                issues.Add(LineNotSingleSidedIssue(l, ctx.DoubleSidedFlag,
                    $"Linedef {i} is marked single-sided but has two sides", mid));
        }
    }

    private static bool IsLineFlagSet(Linedef line, string? flag)
    {
        if (string.IsNullOrWhiteSpace(flag) || flag == "0") return false;
        if (int.TryParse(flag, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bit))
            return bit != 0 && (line.Flags & bit) != 0;
        return line.UdmfFlags.Contains(flag);
    }

    private static MapIssue LinedefWithoutSidedefsIssue(MapSet map, Linedef line, string? doubleSidedFlag, string message, Vector2D focus)
    {
        Sidedef? frontSource = FindCopySidedef(map, line, true);
        Sidedef? backSource = FindCopySidedef(map, line, false);
        var fixes = new List<MapIssueFix>();

        if (frontSource != null || backSource != null)
        {
            fixes.Add(new MapIssueFix("Create One Side", targetMap =>
            {
                if (!targetMap.Linedefs.Contains(line) || line.Front != null || line.Back != null) return false;
                if (frontSource != null)
                {
                    CreateSidedefFromSource(targetMap, line, true, frontSource);
                }
                else
                {
                    CreateSidedefFromSource(targetMap, line, true, backSource!);
                    line.FlipVertices();
                }

                targetMap.BuildIndexes();
                return true;
            }));
        }

        if (frontSource != null && backSource != null)
        {
            fixes.Add(new MapIssueFix("Create Both Sides", targetMap =>
            {
                if (!targetMap.Linedefs.Contains(line) || line.Front != null || line.Back != null) return false;
                CreateSidedefFromSource(targetMap, line, true, frontSource);
                CreateSidedefFromSource(targetMap, line, false, backSource);
                SetLineFlag(line, doubleSidedFlag, true);
                targetMap.BuildIndexes();
                return true;
            }));
        }

        return new MapIssue(MapIssueSeverity.Error, MapIssueKind.LinedefWithoutSidedefs, message)
        {
            Description = LinedefMissingSidesDescription,
            Target = line,
            Focus = focus,
            Fixes = fixes,
        };
    }

    private static MapIssue MissingFrontIssue(MapSet map, Linedef line, string? doubleSidedFlag, string message, Vector2D focus)
    {
        Sidedef? source = FindCopySidedef(map, line, true);
        var fixes = new List<MapIssueFix>
        {
            new("Flip Linedef", targetMap =>
            {
                if (!targetMap.Linedefs.Contains(line) || line.Back == null || line.Front != null) return false;
                line.FlipVertices();
                line.FlipSidedefs();
                targetMap.BuildIndexes();
                return true;
            }),
        };

        if (source != null)
        {
            fixes.Add(new MapIssueFix("Create Sidedef", targetMap =>
            {
                if (!targetMap.Linedefs.Contains(line) || line.Front != null || line.Back == null) return false;
                CreateSidedefFromSource(targetMap, line, true, source);
                SetLineFlag(line, doubleSidedFlag, true);
                targetMap.BuildIndexes();
                return true;
            }));
        }

        return new MapIssue(MapIssueSeverity.Error, MapIssueKind.LinedefMissingFront, message)
        {
            Description = LinedefMissingFrontDescription,
            Target = line,
            Focus = focus,
            Fixes = fixes,
        };
    }

    private static MapIssue LineNotDoubleSidedIssue(MapSet map, Linedef line, string? doubleSidedFlag, string message, Vector2D focus)
    {
        Sidedef? source = FindCopySidedef(map, line, false);
        var fixes = new List<MapIssueFix>
        {
            new("Make Single-Sided", targetMap =>
            {
                if (!targetMap.Linedefs.Contains(line)) return false;
                SetLineFlag(line, doubleSidedFlag, false);
                return true;
            }),
        };

        if (source != null)
        {
            fixes.Add(new MapIssueFix("Create Sidedef", targetMap =>
            {
                if (!targetMap.Linedefs.Contains(line) || line.Back != null) return false;
                CreateSidedefFromSource(targetMap, line, false, source);
                SetLineFlag(line, doubleSidedFlag, true);
                targetMap.BuildIndexes();
                return true;
            }));
        }

        return new MapIssue(MapIssueSeverity.Error, MapIssueKind.LinedefNotDoubleSided, message)
        {
            Description = LinedefNotDoubleSidedDescription,
            Target = line,
            Focus = focus,
            Fixes = fixes,
        };
    }

    private static Sidedef? FindCopySidedef(MapSet map, Linedef line, bool front)
    {
        List<LinedefSide>? sides;
        try
        {
            sides = Tools.FindPotentialSectorAt(map, line, front);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }

        if (sides == null) return null;

        foreach (var side in sides)
        {
            if (ReferenceEquals(side.Line, line)) continue;
            var sidedef = side.Front ? side.Line.Front : side.Line.Back;
            if (sidedef != null) return sidedef;
        }

        return null;
    }

    private static Sidedef CreateSidedefFromSource(MapSet map, Linedef line, bool front, Sidedef source)
    {
        var sidedef = map.AddSidedef(line, front, source.Sector);
        source.CopyPropertiesTo(sidedef);
        return sidedef;
    }

    private static MapIssue LineNotSingleSidedIssue(Linedef line, string? doubleSidedFlag, string message, Vector2D focus)
        => new(MapIssueSeverity.Error, MapIssueKind.LinedefNotSingleSided, message)
        {
            Description = LinedefNotSingleSidedDescription,
            Target = line,
            Focus = focus,
            Fixes = new[]
            {
                new MapIssueFix("Make Double-Sided", map =>
                {
                    if (!map.Linedefs.Contains(line)) return false;
                    SetLineFlag(line, doubleSidedFlag, true);
                    return true;
                }),
                new MapIssueFix("Remove Sidedef", map =>
                {
                    if (!map.Linedefs.Contains(line) || line.Back == null) return false;
                    map.RemoveSidedef(line.Back);
                    map.BuildIndexes();
                    return true;
                }),
            },
        };

    private static void SetLineFlag(Linedef line, string? flag, bool value)
    {
        if (string.IsNullOrWhiteSpace(flag) || flag == "0") return;
        if (int.TryParse(flag, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bit))
        {
            if (value) line.Flags |= bit;
            else line.Flags &= ~bit;
            return;
        }

        line.SetFlag(flag, value);
    }

    private static void CheckMapSize(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
        if (ctx.SafeBoundary <= 0 || map.Vertices.Count == 0) return;

        double minX = double.MaxValue;
        double maxX = double.MinValue;
        double minY = double.MaxValue;
        double maxY = double.MinValue;
        foreach (var v in map.Vertices)
        {
            minX = Math.Min(minX, v.Position.x);
            maxX = Math.Max(maxX, v.Position.x);
            minY = Math.Min(minY, v.Position.y);
            maxY = Math.Max(maxY, v.Position.y);
        }

        bool tooWide = maxX - minX > ctx.SafeBoundary;
        bool tooHigh = maxY - minY > ctx.SafeBoundary;
        if (!tooWide && !tooHigh) return;

        string message = tooWide && tooHigh
            ? $"Map's width and height is bigger than {ctx.SafeBoundary} m.u. This can cause rendering and physics issues."
            : tooWide
                ? $"Map is wider than {ctx.SafeBoundary} m.u. This can cause rendering and physics issues."
                : $"Map is taller than {ctx.SafeBoundary} m.u. This can cause rendering and physics issues.";
        issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.MapTooBig, message)
        {
            Description = MapTooBigDescription,
            Focus = new Vector2D((minX + maxX) * 0.5, (minY + maxY) * 0.5),
        });
    }

    private static void CheckOverlappingVertices(MapSet map, List<MapIssue> issues)
    {
        var buckets = new Dictionary<Vector2D, List<int>>();
        for (int i = 0; i < map.Vertices.Count; i++)
        {
            var p = map.Vertices[i].Position;
            if (!buckets.TryGetValue(p, out var list)) { list = new List<int>(); buckets[p] = list; }
            list.Add(i);
        }
        foreach (var (_, list) in buckets)
            if (list.Count > 1)
            {
                var vertices = list.Select(i => map.Vertices[i]).ToArray();
                var p = vertices[0].Position;
                issues.Add(OverlappingVerticesIssue(vertices,
                    vertices.Length == 2
                        ? $"Vertices {list[0]} and {list[1]} have the same position"
                        : $"{vertices.Length} vertices overlap at ({p.x.ToString("0.###", CultureInfo.InvariantCulture)}, {p.y.ToString("0.###", CultureInfo.InvariantCulture)})."));
            }
    }

    private static MapIssue OverlappingVerticesIssue(Vertex[] vertices, string message)
        => new(MapIssueSeverity.Warning, MapIssueKind.OverlappingVertices, message)
        {
            Description = OverlappingVerticesDescription,
            Target = vertices[0],
            RelatedTargets = vertices.Skip(1).Cast<IMapElement>().ToArray(),
            Focus = vertices[0].Position,
            Fixes = new[]
            {
                new MapIssueFix("Merge Vertices", map =>
                {
                    if (!map.Vertices.Contains(vertices[0])) return false;
                    bool changed = false;
                    for (int i = 1; i < vertices.Length; i++)
                    {
                        if (!map.Vertices.Contains(vertices[i])) continue;
                        map.JoinVertices(vertices[0], vertices[i]);
                        changed = true;
                    }
                    if (!changed) return false;
                    map.BuildIndexes();
                    return true;
                }),
            },
        };

    private static void CheckVerticesOverlappingLinedefs(MapSet map, Dictionary<Vertex, int> vertexIndex, List<MapIssue> issues)
    {
        foreach (var v in map.Vertices)
        {
            for (int lineIndex = 0; lineIndex < map.Linedefs.Count; lineIndex++)
            {
                var l = map.Linedefs[lineIndex];
                if (ReferenceEquals(v, l.Start) || ReferenceEquals(v, l.End)) continue;
                if (l.LengthSq < 1e-9) continue;
                if (Math.Round(l.Line.GetDistanceToLine(v.Position, bounded: true), 3) != 0.0) continue;

                issues.Add(VertexOverlappingLinedefIssue(v, l,
                    $"Vertex {vertexIndex[v]} overlaps line {lineIndex} without splitting it"));
            }
        }
    }

    private static MapIssue VertexOverlappingLinedefIssue(Vertex vertex, Linedef line, string message)
        => new(MapIssueSeverity.Warning, MapIssueKind.VertexOverlappingLinedef, message)
        {
            Description = VertexOverlappingLinedefDescription,
            Target = vertex,
            RelatedTargets = new[] { line },
            Focus = vertex.Position,
            Fixes = new[]
            {
                new MapIssueFix("Split Linedef", map =>
                {
                    if (!map.Vertices.Contains(vertex) || !map.Linedefs.Contains(line)) return false;
                    if (ReferenceEquals(line.Start, vertex) || ReferenceEquals(line.End, vertex)) return false;
                    map.SplitLinedefAt(line, vertex);
                    map.BuildIndexes();
                    map.JoinOverlappingLinedefs(vertex.Linedefs);
                    map.BuildIndexes();
                    return true;
                }),
            },
        };

    private static void CheckUnusedVertices(MapSet map, Dictionary<Vertex, int> vertexIndex, List<MapIssue> issues)
    {
        var used = new HashSet<Vertex>(ReferenceEqualityComparer.Instance);
        foreach (var l in map.Linedefs) { used.Add(l.Start); used.Add(l.End); }
        foreach (var v in map.Vertices)
            if (!used.Contains(v))
                issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnusedVertex,
                    $"Vertex {vertexIndex[v]} at {Coordinate(v.Position.x)}, {Coordinate(v.Position.y)} is not connected to any linedef.")
                {
                    Description = StrayVertexDescription,
                    Target = v,
                    Focus = v.Position,
                    Fixes = new[]
                    {
                        new MapIssueFix("Delete Vertex", map =>
                        {
                            if (!map.Vertices.Contains(v)) return false;
                            map.RemoveVertex(v);
                            map.BuildIndexes();
                            return true;
                        }),
                    },
                });
    }

    private static void CheckSectors(MapSet map, List<MapIssue> issues)
    {
        var sidesBySector = new Dictionary<Sector, List<Sidedef>>(ReferenceEqualityComparer.Instance);
        foreach (Sidedef side in map.Sidedefs)
        {
            if (side.Sector == null || side.Line == null) continue;
            if (!sidesBySector.TryGetValue(side.Sector, out List<Sidedef>? sides))
            {
                sides = new List<Sidedef>();
                sidesBySector[side.Sector] = sides;
            }

            sides.Add(side);
        }

        for (int i = 0; i < map.Sectors.Count; i++)
        {
            Sector sector = map.Sectors[i];
            if (!sidesBySector.TryGetValue(sector, out List<Sidedef>? sides))
            {
                issues.Add(EmptySectorIssue(sector, i));
                continue;
            }

            List<Vertex> holes = FindUnclosedSectorVertices(sector, sides);
            if (holes.Count > 0)
                issues.Add(new MapIssue(MapIssueSeverity.Error, MapIssueKind.UnclosedSector,
                    $"Sector {i} is not closed")
                    { Description = UnclosedSectorDescription, Target = sector, Focus = Centroid(holes) });
            else if (IsInvalidSector(sector))
                issues.Add(InvalidSectorIssue(sector, i, Centroid(sides.SelectMany(side => new[] { side.Line.Start, side.Line.End }))));
        }
    }

    private static MapIssue EmptySectorIssue(Sector sector, int index)
    {
        return new MapIssue(MapIssueSeverity.Warning, MapIssueKind.EmptySector,
            $"Sector {index} has no sidedefs")
        {
            Description = InvalidSectorDescription,
            Target = sector,
            SuppressionKind = MapIssueKind.InvalidSector,
            Fixes = new[]
            {
                new MapIssueFix("Dissolve", map => DissolveInvalidSector(map, sector)),
            },
        };
    }

    private static List<Vertex> FindUnclosedSectorVertices(Sector sector, IEnumerable<Sidedef> sides)
    {
        var vertices = new Dictionary<Vertex, int>(ReferenceEqualityComparer.Instance);
        var holes = new List<Vertex>();

        foreach (Sidedef side in sides)
        {
            Linedef line = side.Line;
            if (!vertices.ContainsKey(line.Start)) vertices[line.Start] = 0;
            if (!vertices.ContainsKey(line.End)) vertices[line.End] = 0;

            if (side.IsFront)
            {
                vertices[line.Start] |= 1;
                vertices[line.End] |= 2;
            }
            else
            {
                vertices[line.End] |= 1;
                vertices[line.Start] |= 2;
            }
        }

        foreach ((Vertex vertex, int bits) in vertices)
        {
            if (bits == 3) continue;

            AddUnique(holes, vertex);
            foreach (Linedef line in vertex.Linedefs)
            {
                if ((line.Front != null && ReferenceEquals(line.Front.Sector, sector)) ||
                    (line.Back != null && ReferenceEquals(line.Back.Sector, sector)))
                {
                    continue;
                }

                if (vertices.TryGetValue(line.Start, out int startBits) && startBits == 3)
                    AddUnique(holes, line.Start);
                if (vertices.TryGetValue(line.End, out int endBits) && endBits == 3)
                    AddUnique(holes, line.End);
            }
        }

        return holes;
    }

    private static MapIssue InvalidSectorIssue(Sector sector, int index, Vector2D? focus)
    {
        return new MapIssue(MapIssueSeverity.Error, MapIssueKind.InvalidSector,
            $"Sector {index} has {sector.Sidedefs.Count} sidedefs")
        {
            Description = InvalidSectorDescription,
            Target = sector,
            Focus = focus,
            Fixes = new[]
            {
                new MapIssueFix("Dissolve", map => DissolveInvalidSector(map, sector)),
            },
        };
    }

    private static bool DissolveInvalidSector(MapSet map, Sector sector)
    {
        if (!map.Sectors.Contains(sector)) return false;

        map.BuildIndexes();

        foreach (var line in UniqueSectorLines(sector).ToArray())
        {
            if (line.LengthSq == 0 && map.Linedefs.Contains(line))
                map.RemoveLinedef(line);
        }

        map.BuildIndexes();

        var lines = UniqueSectorLines(sector);
        if (lines.Count == 0)
        {
            map.RemoveSector(sector);
            map.BuildIndexes();
            return true;
        }

        if (lines.Count >= 3) return false;

        var neighbor = sector.Sidedefs
            .Select(side => side.Other?.Sector)
            .FirstOrDefault(other => other != null && !ReferenceEquals(other, sector));

        if (neighbor != null)
        {
            foreach (var side in sector.Sidedefs.ToArray())
                side.Sector = neighbor;
        }

        map.RemoveSector(sector);
        map.BuildIndexes();
        return true;
    }

    private static List<Linedef> UniqueSectorLines(Sector sector)
    {
        var lines = new List<Linedef>();
        foreach (var side in sector.Sidedefs)
        {
            if (side.Line == null || lines.Contains(side.Line)) continue;
            lines.Add(side.Line);
        }

        return lines;
    }

    private static bool IsInvalidSector(Sector sector)
    {
        if (sector.Sidedefs.Count < 3) return true;

        var lines = new HashSet<Linedef>(ReferenceEqualityComparer.Instance);
        foreach (var side in sector.Sidedefs)
            if (side.Line != null)
                lines.Add(side.Line);
        return lines.Count < 3;
    }

    private static void AddUnique<T>(List<T> items, T item)
    {
        if (!items.Contains(item)) items.Add(item);
    }

    // Average position of a set of vertices, or null when empty.
    private static Vector2D? Centroid(IEnumerable<Vertex> verts)
    {
        double sx = 0, sy = 0;
        int n = 0;
        foreach (var v in verts) { sx += v.Position.x; sy += v.Position.y; n++; }
        return n == 0 ? null : new Vector2D(sx / n, sy / n);
    }
}
