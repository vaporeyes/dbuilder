// ABOUTME: Horizontal texture auto-alignment - propagates a sidedef's X offset along a connected run of walls.
// ABOUTME: A focused port of UDB's auto-align; Doom's X offset is per-sidedef, so this aligns the whole chain seamlessly.

using System;
using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

public static class SidedefTextureAlignment
{
    /// <summary>
    /// Walks the forward chain of connected sidedefs sharing <paramref name="start"/>'s primary texture and sets
    /// each one's X offset so the texture continues seamlessly from the previous wall. The start sidedef's offset
    /// is the anchor and is left unchanged. Returns the number of downstream sidedefs aligned.
    /// Requires MapSet.BuildIndexes() to have populated Vertex.Linedefs.
    /// </summary>
    public static int AutoAlignX(Sidedef start, int textureWidth)
    {
        if (textureWidth <= 0) textureWidth = 1; // guard against modulo/division by zero
        string tex = PrimaryTexture(start);

        int aligned = 0;
        var visited = new HashSet<Sidedef>(ReferenceEqualityComparer.Instance) { start };
        Sidedef prev = start;
        int offset = start.OffsetX;
        Sidedef? next = NextSidedef(start, tex);

        while (next != null && visited.Add(next))
        {
            offset += (int)Math.Round(WallLength(prev));
            next.OffsetX = Mod(offset, textureWidth);
            aligned++;
            prev = next;
            next = NextSidedef(next, tex);
        }
        return aligned;
    }

    /// <summary>
    /// Walks the same forward chain as <see cref="AutoAlignX"/> and sets each sidedef's Y (row) offset so a
    /// top-pegged texture stays continuous across ceiling-height changes. The start sidedef is the anchor.
    /// Returns the number of downstream sidedefs aligned. Requires BuildIndexes() for Vertex.Linedefs.
    /// </summary>
    public static int AutoAlignY(Sidedef start, int textureHeight)
    {
        if (textureHeight <= 0) textureHeight = 1;
        string tex = PrimaryTexture(start);

        int aligned = 0;
        var visited = new HashSet<Sidedef>(ReferenceEqualityComparer.Instance) { start };
        int offset = start.OffsetY;
        int top = TopReference(start);
        Sidedef? next = NextSidedef(start, tex);

        while (next != null && visited.Add(next))
        {
            int nextTop = TopReference(next);
            // Continuity: top_prev - z + off_prev == top_next - z + off_next, so off_next = off_prev + (top_prev - top_next).
            offset += top - nextTop;
            next.OffsetY = Mod(offset, textureHeight);
            aligned++;
            top = nextTop;
            next = NextSidedef(next, tex);
        }
        return aligned;
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

    private static double WallLength(Sidedef sd)
        => (sd.Line.End.Position - sd.Line.Start.Position).GetLength();

    // The next sidedef whose wall begins at this sidedef's forward vertex and shares the texture.
    private static Sidedef? NextSidedef(Sidedef sd, string tex)
    {
        var line = sd.Line;
        // The wall runs Start->End for a front side and End->Start for a back side; advance off its far vertex.
        Vertex forward = sd.IsFront ? line.End : line.Start;

        foreach (var o in forward.Linedefs)
        {
            if (ReferenceEquals(o, line)) continue;
            // A front side begins at o.Start; a back side begins at o.End. Continue where one begins at forward.
            if (o.Front != null && ReferenceEquals(o.Start, forward) && SameTex(PrimaryTexture(o.Front), tex)) return o.Front;
            if (o.Back != null && ReferenceEquals(o.End, forward) && SameTex(PrimaryTexture(o.Back), tex)) return o.Back;
        }
        return null;
    }

    private static bool SameTex(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static int Mod(int v, int m) => ((v % m) + m) % m;
}
