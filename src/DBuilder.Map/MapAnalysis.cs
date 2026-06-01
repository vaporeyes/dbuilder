// ABOUTME: Static map health checker reporting common geometry/structure problems in a MapSet.
// ABOUTME: A focused subset of UDB's error checkers; works off raw lists so it needs no BuildIndexes() call.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DBuilder.Geometry;

namespace DBuilder.Map;

public enum MapIssueSeverity { Warning, Error }

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

/// <summary>
/// Optional lookups that enable the resource/config-aware checks. Delegates are injected by the host (so this
/// project stays decoupled from resource/config code); a null delegate disables its check.
/// </summary>
public sealed class MapCheckContext
{
    public MapIssueFixOptions FixOptions { get; init; } = new();
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
    /// <summary>Returns an obsolete warning for a known thing type, or null when the thing type is current.</summary>
    public Func<int, string?>? ThingObsoleteMessage { get; init; }
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
    /// <summary>Returns true when a sector effect number is known (incl. generalized) to the game config.</summary>
    public Func<int, bool>? SectorEffectKnown { get; init; }
    /// <summary>Enable Hexen/UDMF thing action checks.</summary>
    public bool CheckThingActions { get; init; }
    /// <summary>Returns true when an action deliberately uses unresolved names in the given texture slot.</summary>
    public Func<int, SidedefPart, bool>? IgnoreUnknownTexture { get; init; }
    /// <summary>Returns true when a linedef action forces an upper texture even without a height gap.</summary>
    public Func<int, bool>? ActionRequiresUpperTexture { get; init; }
    /// <summary>Returns true when a linedef action requires an activation flag.</summary>
    public Func<int, bool>? ActionRequiresActivation { get; init; }
    /// <summary>UDMF linedef flags that activate an action; non-trigger flags are excluded.</summary>
    public IReadOnlySet<string>? TriggerActivationFlags { get; init; }
    /// <summary>Enable UDMF-only missing activation checks.</summary>
    public bool CheckMissingActivations { get; init; }
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
    /// <summary>Grid size for the off-grid vertex check; 0 disables it.</summary>
    public int GridSize { get; init; }
    /// <summary>Linedefs shorter than this (but non-zero) are flagged. Default 8.</summary>
    public double ShortLinedefLength { get; init; } = 8;
}

/// <summary>A single detected map problem with a human-readable message and optional navigation hints.</summary>
public sealed record MapIssue(MapIssueSeverity Severity, MapIssueKind Kind, string Message)
{
    /// <summary>The offending element, so the editor can select it (null when the issue has no single element).</summary>
    public ISelectable? Target { get; init; }

    /// <summary>A representative world location to center the view on (null when unknown).</summary>
    public Vector2D? Focus { get; init; }

    public IReadOnlyList<MapIssueFix> Fixes { get; init; } = Array.Empty<MapIssueFix>();
}

public static class MapAnalysis
{
    /// <summary>Scans the map and returns all detected issues (empty list when clean).</summary>
    public static IReadOnlyList<MapIssue> Check(MapSet map) => Check(map, null);

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
        CheckSectors(map, issues);

        if (ctx != null)
        {
            CheckTextures(map, ctx, issues);
            CheckFlats(map, ctx, issues);
            CheckThingsAndActions(map, ctx, issues);
            CheckPolyobjects(map, ctx, issues);
            CheckTextureAlignment(map, ctx, issues);
            CheckOverlappingLinedefs(map, issues);
            CheckShortLinedefs(map, ctx, issues);
            CheckOffGridVertices(map, ctx, vertexIndex, issues);
        }

        return issues;
    }

    // A two-sided line needs an upper/lower texture where its sector is taller/lower than the neighbor; a
    // one-sided line needs a middle texture. Flags required-but-absent ("-") slots and unresolved names.
    private static void CheckTextures(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
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
                    issues.Add(MissingTextureIssue(l, side, SidedefPart.Middle, ctx.FixOptions,
                        $"Linedef {index} ({which}) is one-sided but has no middle texture.", mid));
            }
            else
            {
                if (side.Sector != null && other.Sector != null)
                {
                    if ((other.Sector.CeilHeight < side.Sector.CeilHeight || ctx.ActionRequiresUpperTexture?.Invoke(l.Action) == true) &&
                        !IsSkyFlat(ctx, other.Sector.CeilTexture) &&
                        IsBlank(side.HighTexture))
                        issues.Add(MissingTextureIssue(l, side, SidedefPart.Upper, ctx.FixOptions,
                            $"Linedef {index} ({which}) needs an upper texture.", mid));
                    if (other.Sector.FloorHeight > side.Sector.FloorHeight && !IsSkyFlat(ctx, other.Sector.FloorTexture) && IsBlank(side.LowTexture))
                        issues.Add(MissingTextureIssue(l, side, SidedefPart.Lower, ctx.FixOptions,
                            $"Linedef {index} ({which}) needs a lower texture.", mid));
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
                        issues.Add(UnknownTextureIssue(l, side, part, ctx.FixOptions,
                            $"Linedef {index} ({which}) {slot} texture \"{name}\" is not found.", mid));

            if (!IsBlank(side.HighTexture) &&
                !side.HighRequired() &&
                ctx.ActionRequiresUpperTexture?.Invoke(l.Action) != true)
                issues.Add(UnusedTextureIssue(l, side, SidedefPart.Upper,
                    $"Linedef {index} ({which}) upper texture \"{side.HighTexture}\" is not needed.", mid));

            if (!IsBlank(side.LowTexture) && !side.LowRequired())
                issues.Add(UnusedTextureIssue(l, side, SidedefPart.Lower,
                    $"Linedef {index} ({which}) lower texture \"{side.LowTexture}\" is not needed.", mid));
        }
    }

    private static void CheckFlats(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
        for (int i = 0; i < map.Sectors.Count; i++)
        {
            var s = map.Sectors[i];
            foreach (var (slot, name) in new[] { ("floor", s.FloorTexture), ("ceiling", s.CeilTexture) })
            {
                if (IsBlank(name))
                    issues.Add(MissingFlatIssue(s, slot == "ceiling", ctx.FixOptions,
                        $"Sector {i} has no {slot} flat."));
                else if (ctx.FlatExists != null && !ctx.FlatExists(name))
                    issues.Add(UnknownFlatIssue(s, slot == "ceiling", ctx.FixOptions,
                        $"Sector {i} {slot} flat \"{name}\" is not found."));
            }
        }
    }

    private static void CheckThingsAndActions(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
        if (ctx.ThingTypeKnown != null)
            foreach (var t in map.Things)
                if (!ctx.ThingTypeKnown(t.Type))
                    issues.Add(DeleteThingIssue(MapIssueKind.UnknownThingType, t,
                        $"Thing type {t.Type} is not in the game config."));

        if (ctx.ThingObsoleteMessage != null)
            foreach (var t in map.Things)
            {
                string? message = ctx.ThingObsoleteMessage(t.Type);
                if (!string.IsNullOrWhiteSpace(message))
                    issues.Add(DeleteThingIssue(MapIssueKind.ObsoleteThingType, t,
                        $"Thing type {t.Type} is obsolete: {message}"));
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
                    issues.Add(UnknownLinedefActionIssue(l,
                        $"Linedef {i} action {l.Action} is not in the game config."));
            }

            if (ctx.CheckThingActions)
                for (int i = 0; i < map.Things.Count; i++)
                {
                    var t = map.Things[i];
                    if (t.Action != 0 && !ctx.ActionKnown(t.Action))
                        issues.Add(UnknownThingActionIssue(t,
                            $"Thing {i} action {t.Action} is not in the game config."));
                }
        }

        if (ctx.SectorEffectKnown != null)
            for (int i = 0; i < map.Sectors.Count; i++)
            {
                var s = map.Sectors[i];
                if (s.Special != 0 && !ctx.SectorEffectKnown(s.Special))
                    issues.Add(UnknownSectorEffectIssue(s,
                        $"Sector {i} effect {s.Special} is not in the game config."));
            }

        if (ctx.CheckMissingActivations && ctx.ActionRequiresActivation != null && ctx.TriggerActivationFlags != null)
            for (int i = 0; i < map.Linedefs.Count; i++)
            {
                var l = map.Linedefs[i];
                if (l.Action == 0 || !ctx.ActionRequiresActivation(l.Action)) continue;
                bool hasActivation = false;
                foreach (var flag in ctx.TriggerActivationFlags)
                    if (l.UdmfFlags.Contains(flag)) { hasActivation = true; break; }

                if (!hasActivation)
                    issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.MissingActivation,
                        $"Linedef {i} has an action with no activation.")
                        { Target = l, Focus = new Vector2D((l.Start.Position.x + l.End.Position.x) * 0.5, (l.Start.Position.y + l.End.Position.y) * 0.5) });
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
                    issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnknownLinedefScript,
                        $"Linedef {i} references unknown ACS script name \"{scriptName}\".")
                        { Target = line, Focus = LinedefMidpoint(line) });
            }
            else if (ctx.ScriptNumberExists != null && !ctx.ScriptNumberExists(line.Args[0]))
            {
                issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnknownLinedefScript,
                    $"Linedef {i} references unknown ACS script number \"{line.Args[0]}\".")
                    { Target = line, Focus = LinedefMidpoint(line) });
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
                    issues.Add(DeleteThingIssue(MapIssueKind.UnknownThingScript, thing,
                        $"Thing {i} references unknown ACS script name \"{scriptName}\"."));
            }
            else if (ctx.ScriptNumberExists != null && !ctx.ScriptNumberExists(thing.Args[0]))
            {
                issues.Add(DeleteThingIssue(MapIssueKind.UnknownThingScript, thing,
                    $"Thing {i} references unknown ACS script number \"{thing.Args[0]}\"."));
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
                $"Thing {i} type {t.Type} is unused: {string.Join(" ", warnings)}"));
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

    private static MapIssue UnknownLinedefActionIssue(Linedef line, string message)
        => new(MapIssueSeverity.Warning, MapIssueKind.UnknownAction, message)
        {
            Target = line,
            Focus = LinedefMidpoint(line),
            Fixes = new[]
            {
                new MapIssueFix("Remove Action", map =>
                {
                    if (!map.Linedefs.Contains(line)) return false;
                    line.Action = 0;
                    return true;
                }),
            },
        };

    private static MapIssue UnknownThingActionIssue(Thing thing, string message)
        => new(MapIssueSeverity.Warning, MapIssueKind.UnknownThingAction, message)
        {
            Target = thing,
            Focus = thing.Position,
            Fixes = new[]
            {
                new MapIssueFix("Remove Action", map =>
                {
                    if (!map.Things.Contains(thing)) return false;
                    thing.Action = 0;
                    return true;
                }),
            },
        };

    private static MapIssue UnknownSectorEffectIssue(Sector sector, string message)
        => new(MapIssueSeverity.Warning, MapIssueKind.UnknownSectorEffect, message)
        {
            Target = sector,
            Fixes = new[]
            {
                new MapIssueFix("Remove Effect", map =>
                {
                    if (!map.Sectors.Contains(sector)) return false;
                    sector.Special = 0;
                    return true;
                }),
            },
        };

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
                issues.Add(DeleteThingIssue(MapIssueKind.ThingOutsideMap, t,
                    $"Thing {i} type {t.Type} is outside the map at {t.Position.x.ToString("0.###", CultureInfo.InvariantCulture)}, {t.Position.y.ToString("0.###", CultureInfo.InvariantCulture)}."));
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

            if (PointInRect(left, right, top, bottom, l.Start.Position) ||
                PointInRect(left, right, top, bottom, l.End.Position) ||
                SegmentIntersectsRect(l.Start.Position, l.End.Position, left, right, top, bottom))
            {
                stuck = true;
                issues.Add(DeleteThingIssue(MapIssueKind.ThingStuckInLinedef, thing,
                    $"Thing {thingIndex} type {thing.Type} is stuck in linedef {lineIndex}."));
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
                $"Thing {thingIndex} type {thing.Type} is stuck in thing {otherIndex} type {other.Type}."));
        }

        return stuck;
    }

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
        => new(MapIssueSeverity.Warning, kind, message)
        {
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
            },
        };

    private static MapIssue UnusedThingIssue(Thing thing, IReadOnlyList<string> defaultFlags, string message)
        => new(MapIssueSeverity.Warning, MapIssueKind.UnusedThing, message)
        {
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
            Target = first,
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

    private static bool PointInRect(double left, double right, double top, double bottom, Vector2D p)
        => p.x >= Math.Min(left, right) && p.x <= Math.Max(left, right) &&
           p.y >= Math.Min(top, bottom) && p.y <= Math.Max(top, bottom);

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
                    AddPolyLineIssue(issues, linesByNumber.Value, $"\"{group.Key}\" action targets non-existing Polyobject Start Spot ({linesByNumber.Key}).");

        if (polyLines.TryGetValue(polyobjStartLine, out var startLines))
            foreach (var linesByNumber in startLines)
            {
                if (linesByNumber.Value.Count > 1)
                    AddPolyLineIssue(issues, linesByNumber.Value, $"Several \"{polyobjStartLine}\" actions have the same Polyobject Number assigned ({linesByNumber.Key}).");

                foreach (var line in linesByNumber.Value)
                    CheckMirror(linesByNumber.Key, line.Line.Args[1], line, polyobjStartLine);
            }

        if (polyLines.TryGetValue(polyobjExplicitLine, out var explicitLines))
            foreach (var linesByNumber in explicitLines)
                foreach (var line in linesByNumber.Value)
                    CheckMirror(linesByNumber.Key, line.Line.Args[2], line, polyobjExplicitLine);

        foreach (var group in anchors)
        {
            if (!startSpots.ContainsKey(group.Key))
                AddPolyThingIssue(issues, group.Value, $"Polyobject {(group.Value.Count > 1 ? "Anchors target" : "Anchor targets")} non-existing Polyobject Start Spot ({group.Key}).");
            if (group.Value.Count > 1)
                AddPolyThingIssue(issues, group.Value, $"Several Polyobject Anchors target the same Polyobject Start Spot ({group.Key}).");
        }

        foreach (var group in startSpots)
        {
            if (!anchors.ContainsKey(group.Key))
                AddPolyThingIssue(issues, group.Value, $"Polyobject Start {(group.Value.Count > 1 ? "Spots are not targeted" : "Spot " + group.Key.ToString(CultureInfo.InvariantCulture) + " is not targeted")} by any Polyobject Anchor.");
            if (group.Value.Count > 1)
                AddPolyThingIssue(issues, group.Value, $"Several Polyobject Start Spots have the same Polyobject number ({group.Key}).");
        }

        void CheckMirror(int polyNumber, int mirrorNumber, (int Index, Linedef Line) line, string actionId)
        {
            if (mirrorNumber <= 0) return;
            if (!startSpots.ContainsKey(mirrorNumber))
                AddPolyLineIssue(issues, new[] { line }, $"\"{actionId}\" action has non-existing Mirror Polyobject Number assigned ({mirrorNumber}).");
            if (mirrorNumber == polyNumber)
                AddPolyLineIssue(issues, new[] { line }, $"\"{actionId}\" action has the same Polyobject and Mirror Polyobject numbers assigned ({mirrorNumber}).");
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
        var first = lines.FirstOrDefault();
        if (first.Line == null) return;
        issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.InvalidPolyobject, message)
        {
            Target = first.Line,
            Focus = new Vector2D((first.Line.Start.Position.x + first.Line.End.Position.x) * 0.5, (first.Line.Start.Position.y + first.Line.End.Position.y) * 0.5),
        });
    }

    private static void AddPolyThingIssue(List<MapIssue> issues, IEnumerable<(int Index, Thing Thing)> things, string message)
    {
        var first = things.FirstOrDefault();
        if (first.Thing == null) return;
        issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.InvalidPolyobject, message)
        {
            Target = first.Thing,
            Focus = first.Thing.Position,
        });
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
            string texture = SidedefTextureAlignment.PrimaryTexture(side);
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
                $"Texture \"{texture}\" is not aligned on linedefs {sourceIndex} ({(side.IsFront ? "front" : "back")}) and {targetIndex} ({(target.IsFront ? "front" : "back")}).")
                { Target = side.Line, Focus = LinedefMidpoint(side.Line) });
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
            if (other.Front != null && ReferenceEquals(other.Start, forward) && SameTexture(SidedefTextureAlignment.PrimaryTexture(other.Front), texture))
                return other.Front;
            if (other.Back != null && ReferenceEquals(other.End, forward) && SameTexture(SidedefTextureAlignment.PrimaryTexture(other.Back), texture))
                return other.Back;
        }

        return null;
    }

    private static int TopReference(Sidedef side) => side.Sector?.CeilHeight ?? 0;

    private static bool SameTexture(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static int Mod(int value, int modulus)
        => ((value % modulus) + modulus) % modulus;

    // Two linedefs sharing both endpoints or crossing through their interiors overlap; report each extra one once.
    private static void CheckOverlappingLinedefs(MapSet map, List<MapIssue> issues)
    {
        var seen = new HashSet<(long, long, long, long)>();
        for (int i = 0; i < map.Linedefs.Count; i++)
        {
            var l = map.Linedefs[i];
            var a = Key(l.Start.Position);
            var b = Key(l.End.Position);
            var key = Compare(a, b) <= 0 ? (a.Item1, a.Item2, b.Item1, b.Item2) : (b.Item1, b.Item2, a.Item1, a.Item2);
            if (!seen.Add(key))
                issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.OverlappingLinedefs,
                    $"Linedef {i} overlaps another linedef (same endpoints).")
                    { Target = l, Focus = new Vector2D((l.Start.Position.x + l.End.Position.x) * 0.5, (l.Start.Position.y + l.End.Position.y) * 0.5) });

            for (int j = 0; j < i; j++)
            {
                var other = map.Linedefs[j];
                if (!l.Line.GetIntersection(other.Line, out double uLine, out double uOther)) continue;
                if (uLine <= 0.0 || uLine >= 1.0 || uOther <= 0.0 || uOther >= 1.0) continue;
                if (ReferencesSameSectorOnAllSides(l, other)) continue;

                issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.OverlappingLinedefs,
                    $"Linedef {i} crosses linedef {j}.")
                    { Target = l, Focus = l.Line.GetCoordinatesAt(uLine) });
                break;
            }
        }

        static (long, long) Key(Vector2D p) => ((long)Math.Round(p.x * 1000), (long)Math.Round(p.y * 1000));
        static int Compare((long, long) a, (long, long) b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : a.Item2.CompareTo(b.Item2);
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
        for (int i = 0; i < map.Linedefs.Count; i++)
        {
            var l = map.Linedefs[i];
            double len = (l.End.Position - l.Start.Position).GetLength();
            if (len > 1e-4 && len < ctx.ShortLinedefLength)
                issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.ShortLinedef,
                    $"Linedef {i} is very short ({len:0.##} units).")
                    { Target = l, Focus = new Vector2D((l.Start.Position.x + l.End.Position.x) * 0.5, (l.Start.Position.y + l.End.Position.y) * 0.5) });
        }
    }

    private static void CheckOffGridVertices(MapSet map, MapCheckContext ctx, Dictionary<Vertex, int> index, List<MapIssue> issues)
    {
        if (ctx.GridSize <= 0) return;
        foreach (var v in map.Vertices)
            if (!IsOnGrid(v.Position.x, ctx.GridSize) || !IsOnGrid(v.Position.y, ctx.GridSize))
                issues.Add(OffGridVertexIssue(v, ctx.GridSize,
                    $"Vertex {index[v]} is off the {ctx.GridSize}-unit grid."));
    }

    private static bool IsOnGrid(double value, int gridSize)
        => Math.Abs(value - Math.Round(value / gridSize) * gridSize) < 1e-9;

    private static MapIssue OffGridVertexIssue(Vertex vertex, int gridSize, string message)
        => new(MapIssueSeverity.Warning, MapIssueKind.OffGridVertex, message)
        {
            Target = vertex,
            Focus = vertex.Position,
            Fixes = new[]
            {
                new MapIssueFix("Align Vertex", map =>
                {
                    if (!map.Vertices.Contains(vertex)) return false;
                    vertex.Move(
                        Math.Round(vertex.Position.x / gridSize) * gridSize,
                        Math.Round(vertex.Position.y / gridSize) * gridSize);
                    map.BuildIndexes();
                    return true;
                }),
            },
        };

    private static MapIssue MissingTextureIssue(
        Linedef line,
        Sidedef side,
        SidedefPart part,
        MapIssueFixOptions options,
        string message,
        Vector2D focus)
        => new(MapIssueSeverity.Error, MapIssueKind.MissingTexture, message)
        {
            Target = line,
            Focus = focus,
            Fixes = new[]
            {
                new MapIssueFix("Add Default Texture", map =>
                {
                    if (!map.Sidedefs.Contains(side)) return false;
                    side.SetTexture(part, DefaultTexture(part, options));
                    return true;
                }),
            },
        };

    private static MapIssue UnusedTextureIssue(
        Linedef line,
        Sidedef side,
        SidedefPart part,
        string message,
        Vector2D focus)
        => new(MapIssueSeverity.Warning, MapIssueKind.UnusedTexture, message)
        {
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
        MapIssueFixOptions options,
        string message,
        Vector2D focus)
        => new(MapIssueSeverity.Warning, MapIssueKind.UnknownTexture, message)
        {
            Target = line,
            Focus = focus,
            Fixes = new[]
            {
                new MapIssueFix("Remove Texture", map =>
                {
                    if (!map.Sidedefs.Contains(side)) return false;
                    side.SetTexture(part, "-");
                    return true;
                }),
                new MapIssueFix("Add Default Texture", map =>
                {
                    if (!map.Sidedefs.Contains(side)) return false;
                    side.SetTexture(part, DefaultTexture(part, options));
                    return true;
                }),
            },
        };

    private static MapIssue MissingFlatIssue(Sector sector, bool ceiling, MapIssueFixOptions options, string message)
        => new(MapIssueSeverity.Error, MapIssueKind.MissingFlat, message)
        {
            Target = sector,
            Fixes = new[]
            {
                new MapIssueFix("Add Default Flat", map =>
                {
                    if (!map.Sectors.Contains(sector)) return false;
                    if (ceiling) sector.SetCeilTexture(options.DefaultCeilingTexture);
                    else sector.SetFloorTexture(options.DefaultFloorTexture);
                    return true;
                }),
            },
        };

    private static MapIssue UnknownFlatIssue(Sector sector, bool ceiling, MapIssueFixOptions options, string message)
        => new(MapIssueSeverity.Warning, MapIssueKind.UnknownFlat, message)
        {
            Target = sector,
            Fixes = new[]
            {
                new MapIssueFix("Add Default Flat", map =>
                {
                    if (!map.Sectors.Contains(sector)) return false;
                    if (ceiling) sector.SetCeilTexture(options.DefaultCeilingTexture);
                    else sector.SetFloorTexture(options.DefaultFloorTexture);
                    return true;
                }),
            },
        };

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
                    $"Linedef {i} has no sidedefs.", mid));
            else if (l.Front == null)
                issues.Add(MissingFrontIssue(map, l, ctx?.DoubleSidedFlag,
                    $"Linedef {i} has only a back sidedef (a front sidedef is required).", mid));
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
                    $"Linedef {i} is marked double-sided but has no back sidedef.", mid));
            else if (!markedDoubleSided && l.Back != null)
                issues.Add(LineNotSingleSidedIssue(l, ctx.DoubleSidedFlag,
                    $"Linedef {i} has a back sidedef but is not marked double-sided.", mid));
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
            ? $"Map width and height exceed the safe boundary of {ctx.SafeBoundary} map units."
            : tooWide
                ? $"Map width exceeds the safe boundary of {ctx.SafeBoundary} map units."
                : $"Map height exceeds the safe boundary of {ctx.SafeBoundary} map units.";
        issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.MapTooBig, message)
        {
            Focus = new Vector2D((minX + maxX) * 0.5, (minY + maxY) * 0.5),
        });
    }

    private static void CheckOverlappingVertices(MapSet map, List<MapIssue> issues)
    {
        // Group by quantized position; any cell with more than one vertex is a stack of coincident points.
        var buckets = new Dictionary<(long, long), List<int>>();
        for (int i = 0; i < map.Vertices.Count; i++)
        {
            var p = map.Vertices[i].Position;
            var key = ((long)Math.Round(p.x * 1000.0), (long)Math.Round(p.y * 1000.0));
            if (!buckets.TryGetValue(key, out var list)) { list = new List<int>(); buckets[key] = list; }
            list.Add(i);
        }
        foreach (var (_, list) in buckets)
            if (list.Count > 1)
            {
                var vertices = list.Select(i => map.Vertices[i]).ToArray();
                var p = vertices[0].Position;
                issues.Add(OverlappingVerticesIssue(vertices,
                    $"{vertices.Length} vertices overlap at ({p.x.ToString("0.###", CultureInfo.InvariantCulture)}, {p.y.ToString("0.###", CultureInfo.InvariantCulture)})."));
            }
    }

    private static MapIssue OverlappingVerticesIssue(Vertex[] vertices, string message)
        => new(MapIssueSeverity.Warning, MapIssueKind.OverlappingVertices, message)
        {
            Target = vertices[0],
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
                    $"Vertex {vertexIndex[v]} overlaps linedef {lineIndex} without splitting it."));
            }
        }
    }

    private static MapIssue VertexOverlappingLinedefIssue(Vertex vertex, Linedef line, string message)
        => new(MapIssueSeverity.Warning, MapIssueKind.VertexOverlappingLinedef, message)
        {
            Target = vertex,
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
                    $"Vertex {vertexIndex[v]} is not used by any linedef.")
                {
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
        // Which sectors are referenced by a sidedef, and the per-sector edge degree of each vertex.
        var referenced = new HashSet<Sector>(ReferenceEqualityComparer.Instance);
        var degrees = new Dictionary<Sector, Dictionary<Vertex, int>>(ReferenceEqualityComparer.Instance);

        foreach (var sd in map.Sidedefs)
        {
            if (sd.Sector == null || sd.Line == null) continue;
            referenced.Add(sd.Sector);
            if (!degrees.TryGetValue(sd.Sector, out var dv))
            {
                dv = new Dictionary<Vertex, int>(ReferenceEqualityComparer.Instance);
                degrees[sd.Sector] = dv;
            }
            Bump(dv, sd.Line.Start);
            Bump(dv, sd.Line.End);
        }

        for (int i = 0; i < map.Sectors.Count; i++)
        {
            var s = map.Sectors[i];
            if (!referenced.Contains(s))
            {
                issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.EmptySector,
                    $"Sector {i} has no sidedefs.") { Target = s });
                continue;
            }
            // A closed boundary visits every vertex an even number of times; an odd degree means a gap.
            bool unclosed = false;
            foreach (var (_, count) in degrees[s])
                if ((count & 1) != 0) { unclosed = true; break; }
            if (unclosed)
                issues.Add(new MapIssue(MapIssueSeverity.Error, MapIssueKind.UnclosedSector,
                    $"Sector {i} is not closed (a boundary vertex has an odd number of edges).")
                    { Target = s, Focus = Centroid(degrees[s].Keys) });
            else if (IsInvalidSector(s))
                issues.Add(new MapIssue(MapIssueSeverity.Error, MapIssueKind.InvalidSector,
                    $"Sector {i} is invalid (it has fewer than 3 sidedefs or linedefs).")
                    { Target = s, Focus = Centroid(degrees[s].Keys) });
        }
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

    private static void Bump(Dictionary<Vertex, int> d, Vertex v)
        => d[v] = d.TryGetValue(v, out int c) ? c + 1 : 1;

    // Average position of a set of vertices, or null when empty.
    private static Vector2D? Centroid(IEnumerable<Vertex> verts)
    {
        double sx = 0, sy = 0;
        int n = 0;
        foreach (var v in verts) { sx += v.Position.x; sy += v.Position.y; n++; }
        return n == 0 ? null : new Vector2D(sx / n, sy / n);
    }
}
