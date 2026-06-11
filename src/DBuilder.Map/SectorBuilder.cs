// ABOUTME: Builds a sector from an ordered loop of vertices - the core geometry op behind a draw-room tool.
// ABOUTME: Reuses existing linedefs between consecutive vertices, creates the rest, and assigns the interior-facing sidedef.

/*
 * A focused subset of UDB's drawing pipeline. UDB's SectorBuilder traces an unknown boundary from a point
 * (Tools.FindPotentialSectorAt); here the caller supplies the closed polygon (the typical output of a draw
 * tool), and we wire up the linedefs/sidedefs so the new sector's front faces inward.
 *
 * Doom convention: the front sidedef is on the right of a linedef walked start->end. We normalize the loop to
 * clockwise winding (in math y-up, where interior is then on the right) so the interior-facing side is the
 * front when our travel matches the line's own direction, and the back when it runs opposite.
 */

using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

public static class SectorBuilder
{
    /// <summary>
    /// Creates a sector bounded by the given ordered, closed vertex loop. Existing linedefs between consecutive
    /// vertices are reused; missing ones are created. The interior-facing sidedef of each edge is assigned to the
    /// new sector (created if absent). Optionally copies properties from <paramref name="copyFrom"/>.
    /// Returns the new sector, or null if the loop has fewer than 3 vertices. Call BuildIndexes() afterward.
    /// </summary>
    public static Sector? CreateSector(MapSet map, IReadOnlyList<Vertex> loop, Sector? copyFrom = null)
    {
        if (loop.Count < 3) return null;

        var verts = new List<Vertex>(loop);
        // Normalize to clockwise winding (negative signed area in y-up) so front faces the interior.
        if (SignedArea(verts) > 0) verts.Reverse();

        int n = verts.Count;
        var sides = new List<LinedefSide>(n);
        for (int i = 0; i < n; i++)
        {
            var v1 = verts[i];
            var v2 = verts[(i + 1) % n];
            if (ReferenceEquals(v1, v2)) continue;

            var line = FindLinedef(map, v1, v2) ?? map.AddLinedef(v1, v2);
            // Interior is on the right of v1->v2. The right side is the line's front when our travel matches
            // the line's start->end, otherwise its back.
            bool useFront = ReferenceEquals(line.Start, v1);
            sides.Add(new LinedefSide(line, useFront));
        }

        var sector = map.AddSector();
        sector.Marked = true;
        CopySectorProperties(copyFrom ?? FindCopySector(sides), sector);

        foreach (LinedefSide side in sides)
            AssignSide(map, side.Line, side.Front, sector);

        return sector;
    }

    /// <summary>
    /// Assigns the given traced linedef sides (from Tools.FindClosestPath) to a new sector: each side's sidedef
    /// is created (or reassigned) to the sector. Returns the new sector, or null for an empty path. Call
    /// BuildIndexes() afterward.
    /// </summary>
    public static Sector? CreateSectorFromSides(MapSet map, IReadOnlyList<LinedefSide> sides, Sector? copyFrom = null)
    {
        if (sides.Count == 0) return null;
        var sector = map.AddSector();
        sector.Marked = true;
        CopySectorProperties(copyFrom ?? FindCopySector(sides), sector);

        foreach (var ls in sides)
        {
            AssignSide(map, ls.Line, ls.Front, sector);
        }
        return sector;
    }

    private static Sector? FindCopySector(IReadOnlyList<LinedefSide> sides)
    {
        Sector? copyFrom = null;
        foreach (LinedefSide side in sides)
        {
            if (side.Line.Front?.Sector != null)
            {
                copyFrom = side.Line.Front.Sector;
                if (side.Front) break;
            }

            if (side.Line.Back?.Sector != null)
            {
                copyFrom = side.Line.Back.Sector;
                if (!side.Front) break;
            }
        }

        return copyFrom;
    }

    private static void AssignSide(MapSet map, Linedef line, bool front, Sector sector)
    {
        bool wasSingleSided = line.Front == null || line.Back == null;
        var side = front ? line.Front : line.Back;
        if (side != null)
        {
            side.Sector = sector;
            ApplySidedFlagsIfSidednessChanged(line, wasSingleSided);
            return;
        }

        side = map.AddSidedef(line, front, sector);
        side.Marked = true;
        Sidedef? other = side.Other ?? (front ? line.Back : line.Front);
        if (other == null) return;

        other.CopyPropertiesTo(side);
        side.Line = line;
        side.IsFront = front;
        side.Sector = sector;
        side.Marked = true;
        other.Marked = true;
        ApplySidedFlagsIfSidednessChanged(line, wasSingleSided);
    }

    private static void ApplySidedFlagsIfSidednessChanged(Linedef line, bool wasSingleSided)
    {
        bool isSingleSided = line.Front == null || line.Back == null;
        if (wasSingleSided == isSingleSided) return;

        line.ApplySidedFlags();
    }

    private static double SignedArea(IReadOnlyList<Vertex> verts)
    {
        double sum = 0;
        for (int i = 0; i < verts.Count; i++)
        {
            var a = verts[i].Position;
            var b = verts[(i + 1) % verts.Count].Position;
            sum += a.x * b.y - b.x * a.y;
        }
        return sum * 0.5;
    }

    private static Linedef? FindLinedef(MapSet map, Vertex a, Vertex b)
    {
        foreach (var l in map.Linedefs)
        {
            if ((ReferenceEquals(l.Start, a) && ReferenceEquals(l.End, b)) ||
                (ReferenceEquals(l.Start, b) && ReferenceEquals(l.End, a)))
                return l;
        }
        return null;
    }

    private static void CopySectorProperties(Sector? src, Sector dst)
    {
        if (src == null) return;

        dst.Selected = src.Selected;
        dst.Groups = src.Groups;
        dst.FloorHeight = src.FloorHeight;
        dst.CeilHeight = src.CeilHeight;
        dst.FloorTexture = src.FloorTexture;
        dst.CeilTexture = src.CeilTexture;
        dst.LongFloorTexture = src.LongFloorTexture;
        dst.LongCeilTexture = src.LongCeilTexture;
        dst.Brightness = src.Brightness;
        dst.Special = src.Special;
        dst.FloorSlope = src.FloorSlope;
        dst.FloorSlopeOffset = src.FloorSlopeOffset;
        dst.CeilSlope = src.CeilSlope;
        dst.CeilSlopeOffset = src.CeilSlopeOffset;
        dst.Tags.Clear();
        dst.Tags.AddRange(src.Tags);
        dst.UdmfFlags.Clear();
        foreach (var flag in src.UdmfFlags) dst.UdmfFlags.Add(flag);
        dst.IgnoredErrorChecks.Clear();
        foreach (var check in src.IgnoredErrorChecks) dst.IgnoredErrorChecks.Add(check);
        dst.Fields.Clear();
        foreach (var kv in src.Fields) dst.Fields[kv.Key] = kv.Value;
    }
}
