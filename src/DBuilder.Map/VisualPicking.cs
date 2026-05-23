// ABOUTME: Raycast picking for 3D visual editing - finds the nearest sector floor/ceiling or sidedef wall hit by a ray.
// ABOUTME: Flat-plane model (FloorHeight/CeilHeight); slopes are approximated by their height at the sector. Pure geometry.

using System;
using DBuilder.Geometry;

namespace DBuilder.Map;

public enum VisualHitKind { Floor, Ceiling, Wall }

/// <summary>Which texture slot a wall hit corresponds to (None for floor/ceiling hits).</summary>
public enum WallPart { None, Upper, Middle, Lower }

/// <summary>
/// A surface hit by a 3D ray: which kind, how far, where, the owning sector / linedef side, and the surface's
/// vertical extent (Bottom..Top; equal for a flat floor/ceiling) for drawing a highlight.
/// </summary>
public sealed record VisualHit(
    VisualHitKind Kind, double Distance, Vector3D Point, Sector? Sector, Linedef? Line, bool Front,
    double Bottom, double Top, WallPart Part = WallPart.None);

public static class VisualPicking
{
    private const double Eps = 1e-6;

    /// <summary>
    /// Casts a ray from <paramref name="origin"/> along <paramref name="dir"/> and returns the nearest surface
    /// hit (floor/ceiling/wall), or null. Requires MapSet.BuildIndexes() (uses GetSectorAt for plane containment).
    /// </summary>
    public static VisualHit? Raycast(MapSet map, Vector3D origin, Vector3D dir)
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
            TryPlane(map, origin, dir, s, s.FloorHeight, VisualHitKind.Floor, ref best, ref bestDist);
            TryPlane(map, origin, dir, s, s.CeilHeight, VisualHitKind.Ceiling, ref best, ref bestDist);
        }

        // Sidedef walls as vertical quads along each linedef.
        foreach (var l in map.Linedefs)
        {
            var fs = l.Front?.Sector;
            var bs = l.Back?.Sector;
            if (fs != null && bs == null)
                TryWall(origin, dir, l, fs.FloorHeight, fs.CeilHeight, WallPart.Middle, ref best, ref bestDist);
            else if (fs == null && bs != null)
                TryWall(origin, dir, l, bs.FloorHeight, bs.CeilHeight, WallPart.Middle, ref best, ref bestDist);
            else if (fs != null && bs != null)
            {
                int loBot = Math.Min(fs.FloorHeight, bs.FloorHeight);
                int loTop = Math.Max(fs.FloorHeight, bs.FloorHeight);
                if (loTop > loBot) TryWall(origin, dir, l, loBot, loTop, WallPart.Lower, ref best, ref bestDist); // lower step
                int hiBot = Math.Min(fs.CeilHeight, bs.CeilHeight);
                int hiTop = Math.Max(fs.CeilHeight, bs.CeilHeight);
                if (hiTop > hiBot) TryWall(origin, dir, l, hiBot, hiTop, WallPart.Upper, ref best, ref bestDist); // upper step
            }
        }

        return best;
    }

    private static void TryPlane(MapSet map, Vector3D o, Vector3D d, Sector s, double z, VisualHitKind kind,
        ref VisualHit? best, ref double bestDist)
    {
        if (Math.Abs(d.z) < Eps) return;
        // A floor is visible from above; a ceiling from below.
        if (kind == VisualHitKind.Floor && o.z < z) return;
        if (kind == VisualHitKind.Ceiling && o.z > z) return;

        double t = (z - o.z) / d.z;
        if (t <= Eps || t >= bestDist) return;

        var xy = new Vector2D(o.x + d.x * t, o.y + d.y * t);
        if (!ReferenceEquals(map.GetSectorAt(xy), s)) return;

        bestDist = t;
        best = new VisualHit(kind, t, new Vector3D(xy.x, xy.y, z), s, null, kind == VisualHitKind.Floor, z, z);
    }

    private static void TryWall(Vector3D o, Vector3D d, Linedef l, double zBottom, double zTop, WallPart part,
        ref VisualHit? best, ref double bestDist)
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

        double z = o.z + d.z * u;
        if (z < zBottom || z > zTop) return;

        // The visible side is the one facing the camera (front when the origin is on the right of start->end).
        bool front = Line2D.GetSideOfLine(a, b, new Vector2D(o.x, o.y)) < 0;
        var sector = front ? l.Front?.Sector : l.Back?.Sector;

        bestDist = u;
        best = new VisualHit(VisualHitKind.Wall, u, new Vector3D(o.x + d.x * u, o.y + d.y * u, z), sector, l, front, zBottom, zTop, part);
    }
}
