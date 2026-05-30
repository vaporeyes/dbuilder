// ABOUTME: Static map health checker reporting common geometry/structure problems in a MapSet.
// ABOUTME: A focused subset of UDB's error checkers; works off raw lists so it needs no BuildIndexes() call.

using System;
using System.Collections.Generic;
using System.Globalization;
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
    // Context-aware checks (require a MapCheckContext):
    MissingTexture,
    UnknownTexture,
    UnusedTexture,
    MissingFlat,
    UnknownFlat,
    UnknownThingType,
    ObsoleteThingType,
    UnknownAction,
    UnknownSectorEffect,
    UnknownThingAction,
    OverlappingLinedefs,
    ShortLinedef,
    OffGridVertex,
    MissingActivation,
}

/// <summary>
/// Optional lookups that enable the resource/config-aware checks. Delegates are injected by the host (so this
/// project stays decoupled from resource/config code); a null delegate disables its check.
/// </summary>
public sealed class MapCheckContext
{
    /// <summary>Returns true when a wall-texture name resolves in the loaded resources.</summary>
    public Func<string, bool>? TextureExists { get; init; }
    /// <summary>Returns true when a flat name resolves in the loaded resources.</summary>
    public Func<string, bool>? FlatExists { get; init; }
    /// <summary>Returns true when a flat name is the configured sky flat marker.</summary>
    public Func<string, bool>? IsSkyFlat { get; init; }
    /// <summary>Returns true when a thing editor number is known to the game config.</summary>
    public Func<int, bool>? ThingTypeKnown { get; init; }
    /// <summary>Returns an obsolete warning for a known thing type, or null when the thing type is current.</summary>
    public Func<int, string?>? ThingObsoleteMessage { get; init; }
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
    /// <summary>Configured linedef flag that marks a line as double-sided.</summary>
    public string? DoubleSidedFlag { get; init; }
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

        CheckLinedefs(map, issues);
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
                    issues.Add(new MapIssue(MapIssueSeverity.Error, MapIssueKind.MissingTexture,
                        $"Linedef {index} ({which}) is one-sided but has no middle texture.") { Target = l, Focus = mid });
            }
            else
            {
                if (side.Sector != null && other.Sector != null)
                {
                    if ((other.Sector.CeilHeight < side.Sector.CeilHeight || ctx.ActionRequiresUpperTexture?.Invoke(l.Action) == true) &&
                        !IsSkyFlat(ctx, other.Sector.CeilTexture) &&
                        IsBlank(side.HighTexture))
                        issues.Add(new MapIssue(MapIssueSeverity.Error, MapIssueKind.MissingTexture,
                            $"Linedef {index} ({which}) needs an upper texture.") { Target = l, Focus = mid });
                    if (other.Sector.FloorHeight > side.Sector.FloorHeight && !IsSkyFlat(ctx, other.Sector.FloorTexture) && IsBlank(side.LowTexture))
                        issues.Add(new MapIssue(MapIssueSeverity.Error, MapIssueKind.MissingTexture,
                            $"Linedef {index} ({which}) needs a lower texture.") { Target = l, Focus = mid });
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
                        issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnknownTexture,
                            $"Linedef {index} ({which}) {slot} texture \"{name}\" is not found.") { Target = l, Focus = mid });

            if (!IsBlank(side.HighTexture) &&
                !side.HighRequired() &&
                ctx.ActionRequiresUpperTexture?.Invoke(l.Action) != true)
                issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnusedTexture,
                    $"Linedef {index} ({which}) upper texture \"{side.HighTexture}\" is not needed.") { Target = l, Focus = mid });

            if (!IsBlank(side.LowTexture) && !side.LowRequired())
                issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnusedTexture,
                    $"Linedef {index} ({which}) lower texture \"{side.LowTexture}\" is not needed.") { Target = l, Focus = mid });
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
                    issues.Add(new MapIssue(MapIssueSeverity.Error, MapIssueKind.MissingFlat,
                        $"Sector {i} has no {slot} flat.") { Target = s });
                else if (ctx.FlatExists != null && !ctx.FlatExists(name))
                    issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnknownFlat,
                        $"Sector {i} {slot} flat \"{name}\" is not found.") { Target = s });
            }
        }
    }

    private static void CheckThingsAndActions(MapSet map, MapCheckContext ctx, List<MapIssue> issues)
    {
        if (ctx.ThingTypeKnown != null)
            foreach (var t in map.Things)
                if (!ctx.ThingTypeKnown(t.Type))
                    issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnknownThingType,
                        $"Thing type {t.Type} is not in the game config.") { Target = t, Focus = t.Position });

        if (ctx.ThingObsoleteMessage != null)
            foreach (var t in map.Things)
            {
                string? message = ctx.ThingObsoleteMessage(t.Type);
                if (!string.IsNullOrWhiteSpace(message))
                    issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.ObsoleteThingType,
                        $"Thing type {t.Type} is obsolete: {message}") { Target = t, Focus = t.Position });
            }

        if (ctx.ActionKnown != null)
        {
            for (int i = 0; i < map.Linedefs.Count; i++)
            {
                var l = map.Linedefs[i];
                if (l.Action != 0 && !ctx.ActionKnown(l.Action))
                    issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnknownAction,
                        $"Linedef {i} action {l.Action} is not in the game config.")
                        { Target = l, Focus = new Vector2D((l.Start.Position.x + l.End.Position.x) * 0.5, (l.Start.Position.y + l.End.Position.y) * 0.5) });
            }

            if (ctx.CheckThingActions)
                for (int i = 0; i < map.Things.Count; i++)
                {
                    var t = map.Things[i];
                    if (t.Action != 0 && !ctx.ActionKnown(t.Action))
                        issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnknownThingAction,
                            $"Thing {i} action {t.Action} is not in the game config.") { Target = t, Focus = t.Position });
                }
        }

        if (ctx.SectorEffectKnown != null)
            for (int i = 0; i < map.Sectors.Count; i++)
            {
                var s = map.Sectors[i];
                if (s.Special != 0 && !ctx.SectorEffectKnown(s.Special))
                    issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnknownSectorEffect,
                        $"Sector {i} effect {s.Special} is not in the game config.") { Target = s });
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
            if (v.Position.x % ctx.GridSize != 0 || v.Position.y % ctx.GridSize != 0)
                issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.OffGridVertex,
                    $"Vertex {index[v]} is off the {ctx.GridSize}-unit grid.") { Target = v, Focus = v.Position });
    }

    private static bool IsBlank(string? tex) => string.IsNullOrEmpty(tex) || tex == "-";

    private static bool IsSkyFlat(MapCheckContext ctx, string? flat)
        => !IsBlank(flat) && ctx.IsSkyFlat?.Invoke(flat!) == true;

    private static void CheckLinedefs(MapSet map, List<MapIssue> issues)
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
                issues.Add(new MapIssue(MapIssueSeverity.Error, MapIssueKind.LinedefWithoutSidedefs,
                    $"Linedef {i} has no sidedefs.") { Target = l, Focus = mid });
            else if (l.Front == null)
                issues.Add(new MapIssue(MapIssueSeverity.Error, MapIssueKind.LinedefMissingFront,
                    $"Linedef {i} has only a back sidedef (a front sidedef is required).") { Target = l, Focus = mid });
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
                issues.Add(new MapIssue(MapIssueSeverity.Error, MapIssueKind.LinedefNotDoubleSided,
                    $"Linedef {i} is marked double-sided but has no back sidedef.") { Target = l, Focus = mid });
            else if (!markedDoubleSided && l.Back != null)
                issues.Add(new MapIssue(MapIssueSeverity.Error, MapIssueKind.LinedefNotSingleSided,
                    $"Linedef {i} has a back sidedef but is not marked double-sided.") { Target = l, Focus = mid });
        }
    }

    private static bool IsLineFlagSet(Linedef line, string? flag)
    {
        if (string.IsNullOrWhiteSpace(flag) || flag == "0") return false;
        if (int.TryParse(flag, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bit))
            return bit != 0 && (line.Flags & bit) != 0;
        return line.UdmfFlags.Contains(flag);
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
                var v0 = map.Vertices[list[0]];
                var p = v0.Position;
                issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.OverlappingVertices,
                    $"{list.Count} vertices overlap at ({p.x.ToString("0.###", CultureInfo.InvariantCulture)}, {p.y.ToString("0.###", CultureInfo.InvariantCulture)}).")
                    { Target = v0, Focus = p });
            }
    }

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

                issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.VertexOverlappingLinedef,
                    $"Vertex {vertexIndex[v]} overlaps linedef {lineIndex} without splitting it.")
                    { Target = v, Focus = v.Position });
            }
        }
    }

    private static void CheckUnusedVertices(MapSet map, Dictionary<Vertex, int> vertexIndex, List<MapIssue> issues)
    {
        var used = new HashSet<Vertex>(ReferenceEqualityComparer.Instance);
        foreach (var l in map.Linedefs) { used.Add(l.Start); used.Add(l.End); }
        foreach (var v in map.Vertices)
            if (!used.Contains(v))
                issues.Add(new MapIssue(MapIssueSeverity.Warning, MapIssueKind.UnusedVertex,
                    $"Vertex {vertexIndex[v]} is not used by any linedef.") { Target = v, Focus = v.Position });
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
        }
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
