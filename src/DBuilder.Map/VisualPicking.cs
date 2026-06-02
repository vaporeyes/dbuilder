// ABOUTME: Raycast picking for 3D visual editing - finds the nearest sector floor/ceiling or sidedef wall hit by a ray.
// ABOUTME: Flat-plane model (FloorHeight/CeilHeight); slopes are approximated by their height at the sector. Pure geometry.

using System;
using DBuilder.Geometry;

namespace DBuilder.Map;

public enum VisualHitKind { Floor, Ceiling, Wall, Thing }

/// <summary>
/// A surface hit by a 3D ray: which kind, how far, where, the owning sector / linedef side, and the surface's
/// vertical extent (Bottom..Top; equal for a flat floor/ceiling) for drawing a highlight.
/// </summary>
public sealed record VisualHit(
    VisualHitKind Kind, double Distance, Vector3D Point, Sector? Sector, Linedef? Line, bool Front,
    double Bottom, double Top, SidedefPart Part = SidedefPart.None, Thing? Thing = null);

public sealed record VisualPickingTexture(
    int Width,
    int Height,
    Func<int, int, bool> AlphaTest,
    double ScaleX = 1.0,
    double ScaleY = 1.0);

public sealed record VisualPickingOptions(
    Func<Thing, (double radius, double height)>? ThingSize = null,
    Func<string, VisualPickingTexture?>? WallTexture = null,
    Func<string, VisualPickingTexture?>? FlatTexture = null,
    bool AlphaBasedTextureHighlighting = false);

public static class VisualPicking
{
    private const double Eps = 1e-6;

    /// <summary>
    /// Casts a ray from <paramref name="origin"/> along <paramref name="dir"/> and returns the nearest surface
    /// hit (floor/ceiling/wall), or null. Requires MapSet.BuildIndexes() (uses GetSectorAt for plane containment).
    /// </summary>
    public static VisualHit? Raycast(MapSet map, Vector3D origin, Vector3D dir,
        Func<Thing, (double radius, double height)>? thingSize = null)
        => Raycast(map, null, origin, dir, thingSize);

    /// <summary>
    /// Casts a ray using an optional blockmap to accelerate sector containment checks on large maps.
    /// </summary>
    public static VisualHit? Raycast(MapSet map, BlockMap? blockMap, Vector3D origin, Vector3D dir,
        Func<Thing, (double radius, double height)>? thingSize = null)
        => Raycast(map, blockMap, origin, dir, new VisualPickingOptions(ThingSize: thingSize));

    public static VisualHit? Raycast(MapSet map, Vector3D origin, Vector3D dir, VisualPickingOptions options)
        => Raycast(map, null, origin, dir, options);

    public static VisualHit? Raycast(MapSet map, BlockMap? blockMap, Vector3D origin, Vector3D dir, VisualPickingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        double len = Math.Sqrt(dir.x * dir.x + dir.y * dir.y + dir.z * dir.z);
        if (len < Eps) return null;
        dir = new Vector3D(dir.x / len, dir.y / len, dir.z / len);

        VisualHit? best = null;
        double bestDist = double.MaxValue;

        // Sector floors and ceilings as horizontal planes, accepted only when the hit (x,y) is in that sector.
        foreach (var s in map.Sectors)
        {
            if (s.Sidedefs.Count == 0) continue;
            TryPlane(map, blockMap, origin, dir, s, VisualHitKind.Floor, ref best, ref bestDist);
            TryPlane(map, blockMap, origin, dir, s, VisualHitKind.Ceiling, ref best, ref bestDist);
        }

        foreach ((Sector sector, List<ThreeDFloor> floors) in ThreeDFloors.Resolve(map))
        {
            if (sector.Sidedefs.Count == 0) continue;
            foreach (ThreeDFloor floor in floors)
            {
                if (floor.Alpha == 0 || floor.Top <= floor.Bottom) continue;
                TryThreeDFloorPlane(map, blockMap, origin, dir, sector, floor, top: true, options, ref best, ref bestDist);
                TryThreeDFloorPlane(map, blockMap, origin, dir, sector, floor, top: false, options, ref best, ref bestDist);
                foreach (Sidedef side in sector.Sidedefs)
                    TryThreeDFloorSide(origin, dir, side, floor, options, ref best, ref bestDist);
            }
        }

        // Sidedef walls as vertical quads along each linedef.
        foreach (var l in map.Linedefs)
        {
            var fs = l.Front?.Sector;
            var bs = l.Back?.Sector;
            var a = l.Start.Position;
            var b = l.End.Position;
            if (fs != null && bs == null)
                TryWall(origin, dir, l, fs.GetFloorZ(a), fs.GetFloorZ(b), fs.GetCeilZ(a), fs.GetCeilZ(b), SidedefPart.Middle, options, ref best, ref bestDist);
            else if (fs == null && bs != null)
                TryWall(origin, dir, l, bs.GetFloorZ(a), bs.GetFloorZ(b), bs.GetCeilZ(a), bs.GetCeilZ(b), SidedefPart.Middle, options, ref best, ref bestDist);
            else if (fs != null && bs != null)
            {
                // Lower step: between the two floors (per endpoint, so slopes are followed).
                double lbA = Math.Min(fs.GetFloorZ(a), bs.GetFloorZ(a)), lbB = Math.Min(fs.GetFloorZ(b), bs.GetFloorZ(b));
                double ltA = Math.Max(fs.GetFloorZ(a), bs.GetFloorZ(a)), ltB = Math.Max(fs.GetFloorZ(b), bs.GetFloorZ(b));
                if (ltA > lbA || ltB > lbB) TryWall(origin, dir, l, lbA, lbB, ltA, ltB, SidedefPart.Lower, options, ref best, ref bestDist);
                // Upper step: between the two ceilings.
                double ubA = Math.Min(fs.GetCeilZ(a), bs.GetCeilZ(a)), ubB = Math.Min(fs.GetCeilZ(b), bs.GetCeilZ(b));
                double utA = Math.Max(fs.GetCeilZ(a), bs.GetCeilZ(a)), utB = Math.Max(fs.GetCeilZ(b), bs.GetCeilZ(b));
                if (utA > ubA || utB > ubB) TryWall(origin, dir, l, ubA, ubB, utA, utB, SidedefPart.Upper, options, ref best, ref bestDist);

                double mbA = Math.Max(fs.GetFloorZ(a), bs.GetFloorZ(a)), mbB = Math.Max(fs.GetFloorZ(b), bs.GetFloorZ(b));
                double mtA = Math.Min(fs.GetCeilZ(a), bs.GetCeilZ(a)), mtB = Math.Min(fs.GetCeilZ(b), bs.GetCeilZ(b));
                if ((mtA > mbA || mtB > mbB) && (IsSet(l.Front?.MidTexture) || IsSet(l.Back?.MidTexture)))
                    TryWall(origin, dir, l, mbA, mbB, mtA, mtB, SidedefPart.Middle, options, ref best, ref bestDist, alphaTestMiddleTexture: true);
            }
        }

        // Things as upright bounding boxes (only when the caller supplies their radius/height from the config).
        if (options.ThingSize != null)
            foreach (var t in map.Things)
                TryThing(map, blockMap, origin, dir, t, options.ThingSize(t), ref best, ref bestDist);

        return best;
    }

    private static Sector? SectorAt(MapSet map, BlockMap? blockMap, Vector2D pos)
        => blockMap?.GetSectorAt(pos) ?? map.GetSectorAt(pos);

    private static void TryThing(MapSet map, BlockMap? blockMap, Vector3D o, Vector3D d, Thing t, (double radius, double height) size,
        ref VisualHit? best, ref double bestDist)
    {
        double r = size.radius > 0 ? size.radius : 16;
        double h = size.height > 0 ? size.height : 16;
        var sector = SectorAt(map, blockMap, t.Position);
        double floorZ = sector?.GetFloorZ(t.Position) ?? 0;
        double zb = floorZ + t.Height;
        double zt = zb + h;

        if (!RayAabb(o, d, t.Position.x - r, t.Position.y - r, zb, t.Position.x + r, t.Position.y + r, zt, out double tt)) return;
        if (tt <= Eps || tt >= bestDist) return;

        bestDist = tt;
        best = new VisualHit(VisualHitKind.Thing, tt, new Vector3D(o.x + d.x * tt, o.y + d.y * tt, o.z + d.z * tt),
            sector, null, true, zb, zt, SidedefPart.None, t);
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

    private static void TryPlane(MapSet map, BlockMap? blockMap, Vector3D o, Vector3D d, Sector s, VisualHitKind kind,
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
        if (!ReferenceEquals(SectorAt(map, blockMap, xy), s)) return;

        double zHit = floor ? s.GetFloorZ(xy) : s.GetCeilZ(xy);
        bestDist = t;
        best = new VisualHit(kind, t, new Vector3D(xy.x, xy.y, zHit), s, null, floor, zHit, zHit);
    }

    private static void TryThreeDFloorPlane(
        MapSet map,
        BlockMap? blockMap,
        Vector3D o,
        Vector3D d,
        Sector sector,
        ThreeDFloor floor,
        bool top,
        VisualPickingOptions options,
        ref VisualHit? best,
        ref double bestDist)
    {
        var here = new Vector2D(o.x, o.y);
        double zHere = top ? floor.Control.GetCeilZ(here) : floor.Control.GetFloorZ(here);
        if (top && o.z < zHere) return;
        if (!top && o.z > zHere) return;

        var step = new Vector2D(o.x + d.x, o.y + d.y);
        double g = (top ? floor.Control.GetCeilZ(step) : floor.Control.GetFloorZ(step)) - zHere;
        double denom = d.z - g;
        if (Math.Abs(denom) < Eps) return;

        double t = (zHere - o.z) / denom;
        if (t <= Eps || t >= bestDist) return;

        var xy = new Vector2D(o.x + d.x * t, o.y + d.y * t);
        if (!ReferenceEquals(SectorAt(map, blockMap, xy), sector)) return;
        if (!ThreeDFloorPixelIsOpaque(floor, xy, top, options)) return;

        double zHit = top ? floor.Control.GetCeilZ(xy) : floor.Control.GetFloorZ(xy);
        bestDist = t;
        best = new VisualHit(
            top ? VisualHitKind.Floor : VisualHitKind.Ceiling,
            t,
            new Vector3D(xy.x, xy.y, zHit),
            sector,
            null,
            top,
            zHit,
            zHit);
    }

    private static void TryThreeDFloorSide(
        Vector3D o,
        Vector3D d,
        Sidedef side,
        ThreeDFloor floor,
        VisualPickingOptions options,
        ref VisualHit? best,
        ref double bestDist)
    {
        var start = side.Line.Start.Position;
        var end = side.Line.End.Position;
        double startBottom = floor.Control.GetFloorZ(start);
        double endBottom = floor.Control.GetFloorZ(end);
        double startTop = floor.Control.GetCeilZ(start);
        double endTop = floor.Control.GetCeilZ(end);
        if (startTop <= startBottom && endTop <= endBottom) return;

        TryWall(o, d, side.Line, startBottom, endBottom, startTop, endTop, SidedefPart.Middle, options,
            ref best, ref bestDist, alphaTestTextureSide: side, alphaTestTextureName: floor.SideTexture);
    }

    // zBottom/zTop are the span heights at A and B; the hit's span is interpolated along the segment so the
    // wall follows sloped floors/ceilings.
    private static void TryWall(Vector3D o, Vector3D d, Linedef l, double zBotA, double zBotB, double zTopA, double zTopB,
        SidedefPart part, VisualPickingOptions options, ref VisualHit? best, ref double bestDist, bool alphaTestMiddleTexture = false,
        Sidedef? alphaTestTextureSide = null, string? alphaTestTextureName = null)
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
        var side = front ? l.Front : l.Back;
        if (alphaTestMiddleTexture && side != null && !MiddleTexturePixelIsOpaque(side, seg, z, bot, options))
            return;
        if (alphaTestTextureSide != null && !WallTexturePixelIsOpaque(alphaTestTextureSide, alphaTestTextureName, seg, z, bot, options))
            return;

        bestDist = u;
        best = new VisualHit(VisualHitKind.Wall, u, new Vector3D(o.x + d.x * u, o.y + d.y * u, z), sector, l, front, bot, top, part);
    }

    private static bool MiddleTexturePixelIsOpaque(Sidedef side, double seg, double z, double bottom, VisualPickingOptions options)
        => WallTexturePixelIsOpaque(side, side.MidTexture, seg, z, bottom, options);

    private static bool WallTexturePixelIsOpaque(Sidedef side, string? textureName, double seg, double z, double bottom, VisualPickingOptions options)
    {
        if (!options.AlphaBasedTextureHighlighting || options.WallTexture == null) return true;
        if (!IsSet(textureName)) return false;

        string textureKey = textureName!;
        VisualPickingTexture? texture = options.WallTexture(textureKey);
        if (texture == null || texture.Width <= 0 || texture.Height <= 0) return true;

        double sideSeg = side.IsFront ? seg : 1.0 - seg;
        double scaleX = NonZero(side.GetFloatField("scalex_mid", 1.0));
        double scaleY = NonZero(side.GetFloatField("scaley_mid", 1.0));
        double textureScaleX = NonZero(texture.ScaleX);
        double textureScaleY = NonZero(texture.ScaleY);
        double offsetX = side.OffsetX + side.GetFloatField("offsetx_mid", 0.0);

        int ox = Mod((int)Math.Floor(sideSeg * side.Line.Length * scaleX / textureScaleX + offsetX), texture.Width);
        int oy = Mod((int)Math.Ceiling(((z - bottom) * scaleY / textureScaleY) % texture.Height), texture.Height);
        int pixelY = Clamp(texture.Height - oy, 0, texture.Height - 1);

        return texture.AlphaTest(Clamp(ox, 0, texture.Width - 1), pixelY);
    }

    private static bool ThreeDFloorPixelIsOpaque(ThreeDFloor floor, Vector2D position, bool top, VisualPickingOptions options)
    {
        if (!options.AlphaBasedTextureHighlighting || options.FlatTexture == null) return true;

        string textureName = top ? floor.TopFlat : floor.BottomFlat;
        if (!IsSet(textureName)) return false;

        VisualPickingTexture? texture = options.FlatTexture(textureName);
        if (texture == null || texture.Width <= 0 || texture.Height <= 0) return true;

        Sector control = floor.Control;
        string suffix = top ? "ceiling" : "floor";
        double rotate = DegToRad(control.GetFloatField("rotation" + suffix, 0.0));
        var offset = new Vector2D(
            control.GetFloatField("xpanning" + suffix, 0.0),
            control.GetFloatField("ypanning" + suffix, 0.0));
        var scale = new Vector2D(
            NonZero(control.GetFloatField("xscale" + suffix, 1.0)),
            NonZero(control.GetFloatField("yscale" + suffix, 1.0)));

        Vector2D coord = position.GetRotated(rotate);
        coord.y = -coord.y;
        coord = new Vector2D(
            (coord.x + offset.x) * scale.x / NonZero(texture.ScaleX),
            (coord.y + offset.y) * scale.y / NonZero(texture.ScaleY));

        int ox = Mod((int)Math.Floor(coord.x), texture.Width);
        int oy = Mod((int)Math.Floor(coord.y), texture.Height);
        return texture.AlphaTest(Clamp(ox, 0, texture.Width - 1), Clamp(oy, 0, texture.Height - 1));
    }

    private static bool IsSet(string? texture)
        => !string.IsNullOrWhiteSpace(texture) && texture != "-";

    private static double NonZero(double value)
        => Math.Abs(value) < Eps ? 1.0 : value;

    private static int Mod(int value, int divisor)
    {
        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static int Clamp(int value, int min, int max)
        => value < min ? min : value > max ? max : value;

    private static double DegToRad(double degrees)
        => degrees * Math.PI / 180.0;
}
