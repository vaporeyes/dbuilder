// ABOUTME: Texture auto-alignment - propagates sidedef offsets across connected matching walls.
// ABOUTME: A focused port of UDB's auto-align; Doom offsets are per-sidedef, so this aligns wall sets seamlessly.

using System;
using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

public static class SidedefTextureAlignment
{
    /// <summary>
    /// Walks connected sidedefs sharing <paramref name="start"/>'s primary texture and sets each one's X offset
    /// so the texture continues seamlessly from the anchor wall. The start sidedef's offset is left unchanged.
    /// Returns the number of other sidedefs aligned.
    /// Requires MapSet.BuildIndexes() to have populated Vertex.Linedefs.
    /// </summary>
    public static int AutoAlignX(Sidedef start, int textureWidth)
    {
        if (textureWidth <= 0) textureWidth = 1; // guard against modulo/division by zero
        string tex = PrimaryTexture(start);

        var pending = new Queue<Sidedef>();
        var visited = new HashSet<Sidedef>(ReferenceEqualityComparer.Instance) { start };
        pending.Enqueue(start);

        while (pending.Count > 0)
        {
            Sidedef current = pending.Dequeue();
            AddNeighbors(current, tex, visited, pending, candidate =>
            {
                candidate.OffsetX = Mod(OffsetAtSharedVertex(current, candidate), textureWidth);
            });
        }

        return visited.Count - 1;
    }

    /// <summary>
    /// Walks the same connected wall set as <see cref="AutoAlignX"/> and sets each sidedef's Y (row) offset so
    /// a top-pegged texture stays continuous across ceiling-height changes. The start sidedef is the anchor.
    /// Returns the number of other sidedefs aligned. Requires BuildIndexes() for Vertex.Linedefs.
    /// </summary>
    public static int AutoAlignY(Sidedef start, int textureHeight)
    {
        if (textureHeight <= 0) textureHeight = 1;
        string tex = PrimaryTexture(start);

        var pending = new Queue<Sidedef>();
        var visited = new HashSet<Sidedef>(ReferenceEqualityComparer.Instance) { start };
        int startOffset = start.OffsetY;
        int startTop = TopReference(start);
        pending.Enqueue(start);

        while (pending.Count > 0)
        {
            Sidedef current = pending.Dequeue();
            AddNeighbors(current, tex, visited, pending, candidate =>
            {
                candidate.OffsetY = Mod(startOffset + startTop - TopReference(candidate), textureHeight);
            });
        }

        return visited.Count - 1;
    }

    // The vertical anchor for a top-pegged wall is its sector's ceiling height (0 when it has no sector).
    private static int TopReference(Sidedef sd) => sd.Sector?.CeilHeight ?? 0;

    // The primary texture used to decide chain membership: mid first (one-sided walls), then upper, then lower.
    public static string PrimaryTexture(Sidedef sd)
    {
        if (IsSet(sd.MidTexture)) return sd.MidTexture;
        if (IsSet(sd.HighTexture)) return sd.HighTexture;
        if (IsSet(sd.LowTexture)) return sd.LowTexture;
        return "-";
    }

    private static bool IsSet(string t) => !string.IsNullOrEmpty(t) && t != "-";

    private static double WallLength(Sidedef sd) => (WallTo(sd).Position - WallFrom(sd).Position).GetLength();

    private static Vertex WallFrom(Sidedef sd) => sd.IsFront ? sd.Line.Start : sd.Line.End;

    private static Vertex WallTo(Sidedef sd) => sd.IsFront ? sd.Line.End : sd.Line.Start;

    private static void AddNeighbors(
        Sidedef current,
        string texture,
        HashSet<Sidedef> visited,
        Queue<Sidedef> pending,
        Action<Sidedef> align)
    {
        AddNeighborsAtVertex(WallFrom(current), texture, visited, pending, align);
        AddNeighborsAtVertex(WallTo(current), texture, visited, pending, align);
    }

    private static void AddNeighborsAtVertex(
        Vertex vertex,
        string texture,
        HashSet<Sidedef> visited,
        Queue<Sidedef> pending,
        Action<Sidedef> align)
    {
        foreach (var line in vertex.Linedefs)
        {
            TryAddNeighbor(line.Front, vertex, texture, visited, pending, align);
            TryAddNeighbor(line.Back, vertex, texture, visited, pending, align);
        }
    }

    private static void TryAddNeighbor(
        Sidedef? candidate,
        Vertex shared,
        string texture,
        HashSet<Sidedef> visited,
        Queue<Sidedef> pending,
        Action<Sidedef> align)
    {
        if (candidate == null || !SameTex(PrimaryTexture(candidate), texture)) return;
        if (!ReferenceEquals(WallFrom(candidate), shared) && !ReferenceEquals(WallTo(candidate), shared)) return;
        if (!visited.Add(candidate)) return;

        align(candidate);
        pending.Enqueue(candidate);
    }

    private static int OffsetAtSharedVertex(Sidedef current, Sidedef candidate)
    {
        Vertex currentFrom = WallFrom(current);
        Vertex currentTo = WallTo(current);
        Vertex candidateFrom = WallFrom(candidate);
        Vertex candidateTo = WallTo(candidate);

        double boundaryOffset;
        if (ReferenceEquals(candidateFrom, currentFrom) || ReferenceEquals(candidateTo, currentFrom))
        {
            boundaryOffset = current.OffsetX;
        }
        else if (ReferenceEquals(candidateFrom, currentTo) || ReferenceEquals(candidateTo, currentTo))
        {
            boundaryOffset = current.OffsetX + WallLength(current);
        }
        else
        {
            boundaryOffset = candidate.OffsetX;
        }

        if (ReferenceEquals(candidateTo, currentFrom) || ReferenceEquals(candidateTo, currentTo))
            boundaryOffset -= WallLength(candidate);

        return (int)Math.Round(boundaryOffset);
    }

    private static bool SameTex(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static int Mod(int v, int m) => ((v % m) + m) % m;
}
