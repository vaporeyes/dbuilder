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
    OverlappingVertices,
    UnusedVertex,
    EmptySector,
    UnclosedSector,
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
    public static IReadOnlyList<MapIssue> Check(MapSet map)
    {
        var issues = new List<MapIssue>();

        var vertexIndex = new Dictionary<Vertex, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < map.Vertices.Count; i++) vertexIndex[map.Vertices[i]] = i;

        CheckLinedefs(map, issues);
        CheckOverlappingVertices(map, issues);
        CheckUnusedVertices(map, vertexIndex, issues);
        CheckSectors(map, issues);

        return issues;
    }

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
