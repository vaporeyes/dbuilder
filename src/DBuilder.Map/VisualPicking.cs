// ABOUTME: Raycast picking for 3D visual editing - finds the nearest sector floor/ceiling or sidedef wall hit by a ray.
// ABOUTME: Flat-plane model (FloorHeight/CeilHeight); slopes are approximated by their height at the sector. Pure geometry.

using System;
using DBuilder.Geometry;

namespace DBuilder.Map;

public enum VisualHitKind { Floor, Ceiling, Wall, Thing }

/// <summary>Which texture slot a wall hit corresponds to (None for floor/ceiling hits).</summary>
public enum WallPart { None, Upper, Middle, Lower }

/// <summary>
/// A surface hit by a 3D ray: which kind, how far, where, the owning sector / linedef side, and the surface's
/// vertical extent (Bottom..Top; equal for a flat floor/ceiling) for drawing a highlight.
/// </summary>
public sealed record VisualHit(
    VisualHitKind Kind, double Distance, Vector3D Point, Sector? Sector, Linedef? Line, bool Front,
    double Bottom, double Top, WallPart Part = WallPart.None, Thing? Thing = null);

public static class VisualPicking
{
    private const double Eps = 1e-6;

    /// <summary>
    /// Casts a ray from <paramref name="origin"/> along <paramref name="dir"/> and returns the nearest surface
    /// hit (floor/ceiling/wall), or null. Requires MapSet.BuildIndexes() (uses GetSectorAt for plane containment).
    /// </summary>
    public static VisualHit? Raycast(MapSet map, Vector3D origin, Vector3D dir,
        Func<Thing, (double radius, double height)>? thingSize = null)
    {
        double len = Math.Sqrt(dir.x * dir.x + dir.y * dir.y + dir.z * dir.z);
        if (len < Eps) return null;
        dir = new Vector3D(dir.x / len, dir.y / len, dir.z / len);

        VisualHit? best = null;
        double bestDist = double.MaxValue;

        // Sector floors and ceilings as horizontal planes, accepted only when the hit (x,y) is in that sector.
        foreach (var s in map.Sectors)
        {
            if (s.Sidedefs.Count == 0) continue;
            TryPlane(map, origin, dir, s, VisualHitKind.Floor, ref best, ref bestDist);
            TryPlane(map, origin, dir, s, VisualHitKind.Ceiling, ref best, ref bestDist);
        }

        // Sidedef walls as vertical quads along each linedef.
        foreach (var l in map.Linedefs)
        {
            var fs = l.Front?.Sector;
            var bs = l.Back?.Sector;
            var a = l.Start.Position;
            var b = l.End.Position;
            if (fs != null && bs == null)
                TryWall(origin, dir, l, fs.GetFloorZ(a), fs.GetFloorZ(b), fs.GetCeilZ(a), fs.GetCeilZ(b), WallPart.Middle, ref best, ref bestDist);
            else if (fs == null && bs != null)
                TryWall(origin, dir, l, bs.GetFloorZ(a), bs.GetFloorZ(b), bs.GetCeilZ(a), bs.GetCeilZ(b), WallPart.Middle, ref best, ref bestDist);
            else if (fs != null && bs != null)
            {
                // Lower step: between the two floors (per endpoint, so slopes are followed).
                double lbA = Math.Min(fs.GetFloorZ(a), bs.GetFloorZ(a)), lbB = Math.Min(fs.GetFloorZ(b), bs.GetFloorZ(b));
                double ltA = Math.Max(fs.GetFloorZ(a), bs.GetFloorZ(a)), ltB = Math.Max(fs.GetFloorZ(b), bs.GetFloorZ(b));
                if (ltA > lbA || ltB > lbB) TryWall(origin, dir, l, lbA, lbB, ltA, ltB, WallPart.Lower, ref best, ref bestDist);
                // Upper step: between the two ceilings.
                double ubA = Math.Min(fs.GetCeilZ(a), bs.GetCeilZ(a)), ubB = Math.Min(fs.GetCeilZ(b), bs.GetCeilZ(b));
                double utA = Math.Max(fs.GetCeilZ(a), bs.GetCeilZ(a)), utB = Math.Max(fs.GetCeilZ(b), bs.GetCeilZ(b));
                if (utA > ubA || utB > ubB) TryWall(origin, dir, l, ubA, ubB, utA, utB, WallPart.Upper, ref best, ref bestDist);
            }
        }

        // Things as upright bounding boxes (only when the caller supplies their radius/height from the config).
        if (thingSize != null)
            foreach (var t in map.Things)
                TryThing(map, origin, dir, t, thingSize(t), ref best, ref bestDist);

        return best;
    }

    private static void TryThing(MapSet map, Vector3D o, Vector3D d, Thing t, (double radius, double height) size,
        ref VisualHit? best, ref double bestDist)
    {
        double r = size.radius > 0 ? size.radius : 16;
        double h = size.height > 0 ? size.height : 16;
        double floorZ = map.GetSectorAt(t.Position)?.GetFloorZ(t.Position) ?? 0;
        double zb = floorZ + t.Height;
        double zt = zb + h;

        if (!RayAabb(o, d, t.Position.x - r, t.Position.y - r, zb, t.Position.x + r, t.Position.y + r, zt, out double tt)) return;
        if (tt <= Eps || tt >= bestDist) return;

        bestDist = tt;
        best = new VisualHit(VisualHitKind.Thing, tt, new Vector3D(o.x + d.x * tt, o.y + d.y * tt, o.z + d.z * tt),
            map.GetSectorAt(t.Position), null, true, zb, zt, WallPart.None, t);
    }

    // Slab-method ray vs axis-aligned box; returns the nearest forward entry distance.
    private static bool RayAabb(Vector3D o, Vector3D d,
        double minX, double minY, double minZ, double maxX, double maxY, double maxZ, out double t)
    {
        t = 0;
        double tmin = double.NegativeInfinity, tmax = double.PositiveInfinity;
        if (!Slab(o.x, d.x, minX, maxX, ref tmin, ref tmax)) return false;
        if (!Slab(o.y, d.y, minY, maxY, ref tmin, ref tmax)) return false;
        if (!Slab(o.z, d.z, minZ, maxZ, ref tmin, ref tmax)) return false;
        if (tmax < Eps) return false;
        t = tmin > Eps ? tmin : tmax; // box entry, or exit when the origin is inside
        return t > Eps;

        static bool Slab(double o1, double d1, double lo, double hi, ref double tmin, ref double tmax)
        {
            if (Math.Abs(d1) < Eps) return o1 >= lo && o1 <= hi; // parallel: must already be within the slab
            double t1 = (lo - o1) / d1, t2 = (hi - o1) / d1;
            if (t1 > t2) (t1, t2) = (t2, t1);
            if (t1 > tmin) tmin = t1;
            if (t2 < tmax) tmax = t2;
            return tmin <= tmax;
        }
    }

    private static void TryPlane(MapSet map, Vector3D o, Vector3D d, Sector s, VisualHitKind kind,
        ref VisualHit? best, ref double bestDist)
    {
        bool floor = kind == VisualHitKind.Floor;
        var here = new Vector2D(o.x, o.y);
        double zHere = floor ? s.GetFloorZ(here) : s.GetCeilZ(here);

        // A floor is visible from above; a ceiling from below.
        if (floor && o.z < zHere) return;
        if (!floor && o.z > zHere) return;

        // For a (possibly sloped, but planar) surface z = a*x + b*y + c, a unit step along the ray's xy changes
        // the surface height by g; solving o.z + t*d.z = surface(o.xy + t*d.xy) gives t below.
        var step = new Vector2D(o.x + d.x, o.y + d.y);
        double g = (floor ? s.GetFloorZ(step) : s.GetCeilZ(step)) - zHere;
        double denom = d.z - g;
        if (Math.Abs(denom) < Eps) return;

        double t = (zHere - o.z) / denom;
        if (t <= Eps || t >= bestDist) return;

        var xy = new Vector2D(o.x + d.x * t, o.y + d.y * t);
        if (!ReferenceEquals(map.GetSectorAt(xy), s)) return;

        double zHit = floor ? s.GetFloorZ(xy) : s.GetCeilZ(xy);
        bestDist = t;
        best = new VisualHit(kind, t, new Vector3D(xy.x, xy.y, zHit), s, null, floor, zHit, zHit);
    }

    // zBottom/zTop are the span heights at A and B; the hit's span is interpolated along the segment so the
    // wall follows sloped floors/ceilings.
    private static void TryWall(Vector3D o, Vector3D d, Linedef l, double zBotA, double zBotB, double zTopA, double zTopB,
        WallPart part, ref VisualHit? best, ref double bestDist)
    {
        var a = l.Start.Position;
        var b = l.End.Position;
        double sx = b.x - a.x, sy = b.y - a.y;
        double denom = d.x * sy - d.y * sx;
        if (Math.Abs(denom) < Eps) return; // ray parallel to the wall

        double dx = a.x - o.x, dy = a.y - o.y;
        double u = (dx * sy - dy * sx) / denom; // distance along the (normalized) ray
        double seg = (dx * d.y - dy * d.x) / denom; // position along the wall segment
        if (u <= Eps || u >= bestDist || seg < 0 || seg > 1) return;

        double bot = zBotA + (zBotB - zBotA) * seg;
        double top = zTopA + (zTopB - zTopA) * seg;
        double z = o.z + d.z * u;
        if (z < bot || z > top) return;

        // The visible side is the one facing the camera (front when the origin is on the right of start->end).
        bool front = Line2D.GetSideOfLine(a, b, new Vector2D(o.x, o.y)) < 0;
        var sector = front ? l.Front?.Sector : l.Back?.Sector;

        bestDist = u;
        best = new VisualHit(VisualHitKind.Wall, u, new Vector3D(o.x + d.x * u, o.y + d.y * u, z), sector, l, front, bot, top, part);
    }
}
