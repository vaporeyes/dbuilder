// ABOUTME: Computes sector floor/ceiling slope planes from the Plane_Align (181) linedef special and bakes them in.
// ABOUTME: Mirrors ZDoom P_AlignPlane: the aligned plane meets the neighbor at the line and tilts to the far vertex.

using System;
using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

public static class SlopeEffects
{
    /// <summary>The Hexen/ZDoom Plane_Align linedef special number.</summary>
    public const int PlaneAlignAction = 181;

    /// <summary>
    /// Applies every Plane_Align special to slope the indicated sector's floor (arg0) and/or ceiling (arg1):
    /// 1 = slope the front sector, 2 = slope the back sector. Returns the number of slope planes set.
    /// Requires <see cref="MapSet.BuildIndexes"/> so each sector knows its sidedefs.
    /// </summary>
    public static int ApplyPlaneAlign(MapSet map, int action = PlaneAlignAction)
    {
        int count = 0;
        foreach (var l in map.Linedefs)
        {
            if (l.Action != action || l.Front?.Sector == null || l.Back?.Sector == null) continue;
            count += AlignOne(l, l.Front.Sector, l.Back.Sector, l.Args[0], floor: true);
            count += AlignOne(l, l.Front.Sector, l.Back.Sector, l.Args[1], floor: false);
        }
        return count;
    }

    private static int AlignOne(Linedef l, Sector front, Sector back, int arg, bool floor)
    {
        Sector sloped, other;
        if (arg == 1) { sloped = front; other = back; }
        else if (arg == 2) { sloped = back; other = front; }
        else return 0;

        double srcH = floor ? sloped.FloorHeight : sloped.CeilHeight;   // the sloped sector's own height (at the far vertex)
        double dstH = floor ? other.FloorHeight : other.CeilHeight;     // the neighbor's height (at the line)
        if (srcH == dstH) return 0;                                     // equal heights -> flat, nothing to slope

        var far = FarthestVertex(sloped, l.Start.Position, l.End.Position, out double dist);
        if (far == null || dist < 1e-3) return 0;

        var p1 = new Vector3D(l.Start.Position.x, l.Start.Position.y, dstH);
        var p2 = new Vector3D(l.End.Position.x, l.End.Position.y, dstH);
        var p3 = new Vector3D(far.Position.x, far.Position.y, srcH);
        var plane = new Plane(p1, p2, p3, up: floor); // floors face up, ceilings face down

        if (floor) { sloped.FloorSlope = plane.Normal; sloped.FloorSlopeOffset = plane.Offset; }
        else { sloped.CeilSlope = plane.Normal; sloped.CeilSlopeOffset = plane.Offset; }
        return 1;
    }

    // The vertex of the sector with the greatest perpendicular distance from the line (the slope's high/low edge).
    private static Vertex? FarthestVertex(Sector sec, Vector2D a, Vector2D b, out double maxDist)
    {
        maxDist = 0;
        Vertex? best = null;
        double dx = b.x - a.x, dy = b.y - a.y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-9) return null;

        var seen = new HashSet<Vertex>(ReferenceEqualityComparer.Instance);
        foreach (var sd in sec.Sidedefs)
        {
            if (sd.Line == null) continue;
            foreach (var v in new[] { sd.Line.Start, sd.Line.End })
            {
                if (!seen.Add(v)) continue;
                double d = Math.Abs(dx * (a.y - v.Position.y) - dy * (a.x - v.Position.x)) / len;
                if (d > maxDist) { maxDist = d; best = v; }
            }
        }
        return best;
    }
}
