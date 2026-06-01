// ABOUTME: UDB-style thing geometry helpers that need game-configuration thing metadata.
// ABOUTME: Keeps metadata-dependent calculations out of DBuilder.Map to avoid a reverse project dependency.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public static class ThingGeometryTools
{
    public static bool TryAlignThingToLine(
        Thing thing,
        Linedef line,
        ThingTypeInfo info,
        MapSet? map = null,
        int vertexDecimals = 3,
        bool usePrecisePosition = true,
        bool preserveHeight = true)
    {
        if (line.Back == null)
        {
            if (line.Front?.Sector != null && CanAlignThingTo(thing, info, line.Front.Sector))
            {
                AlignThingToLine(thing, line, front: true, info, map, vertexDecimals, usePrecisePosition, preserveHeight);
                return true;
            }

            return false;
        }

        if (line.Front == null)
        {
            if (line.Back.Sector != null && CanAlignThingTo(thing, info, line.Back.Sector))
            {
                AlignThingToLine(thing, line, front: false, info, map, vertexDecimals, usePrecisePosition, preserveHeight);
                return true;
            }

            return false;
        }

        double side = line.SideOfLine(thing.Position);
        if (side == 0.0)
        {
            thing.Rotate(ClampAngle(180 + line.AngleDeg));
            return true;
        }

        if (side < 0.0)
        {
            if ((HasMiddleTexture(line.Front) && line.Front.Sector != null && CanAlignThingTo(thing, info, line.Front.Sector))
                || (line.Front.Sector != null && line.Back.Sector != null && CanAlignThingTo(thing, info, line.Front.Sector, line.Back.Sector)))
            {
                AlignThingToLine(thing, line, front: true, info, map, vertexDecimals, usePrecisePosition, preserveHeight);
                return true;
            }

            return false;
        }

        if ((HasMiddleTexture(line.Back) && line.Back.Sector != null && CanAlignThingTo(thing, info, line.Back.Sector))
            || (line.Back.Sector != null && line.Front.Sector != null && CanAlignThingTo(thing, info, line.Back.Sector, line.Front.Sector)))
        {
            AlignThingToLine(thing, line, front: false, info, map, vertexDecimals, usePrecisePosition, preserveHeight);
            return true;
        }

        return false;
    }

    public static int GetThingAbsoluteZ(Thing thing, ThingTypeInfo info)
    {
        if (info.AbsoluteZ) return (int)thing.Height;

        if (thing.Sector != null)
        {
            if (info.Hangs) return (int)(thing.Sector.CeilHeight - thing.Height - info.Height);

            return (int)(thing.Sector.FloorHeight + thing.Height);
        }

        return (int)thing.Height;
    }

    private static bool CanAlignThingTo(Thing thing, ThingTypeInfo info, Sector front, Sector back)
    {
        int absz = GetThingAbsoluteZ(thing, info);
        int height = GetThingHeight(info);
        double thingBottom = info.Hangs ? absz - height : absz;
        double thingTop = thingBottom + height;

        if (front.FloorHeight < back.FloorHeight && Intersects(thingBottom, thingTop, front.FloorHeight, back.FloorHeight))
            return true;

        if (front.CeilHeight > back.CeilHeight && Intersects(thingBottom, thingTop, back.CeilHeight, front.CeilHeight))
            return true;

        return false;
    }

    private static bool CanAlignThingTo(Thing thing, ThingTypeInfo info, Sector sector)
    {
        int absz = GetThingAbsoluteZ(thing, info);
        int height = GetThingHeight(info);
        return Intersects(absz, absz + height, sector.FloorHeight, sector.CeilHeight);
    }

    private static void AlignThingToLine(
        Thing thing,
        Linedef line,
        bool front,
        ThingTypeInfo info,
        MapSet? map,
        int vertexDecimals,
        bool usePrecisePosition,
        bool preserveHeight)
    {
        Vector2D pos = line.NearestOnLine(thing.Position);
        Sector? initialSector = thing.Sector;
        double x = front ? pos.x - Math.Cos(line.Angle) : pos.x + Math.Cos(line.Angle);
        double y = front ? pos.y - Math.Sin(line.Angle) : pos.y + Math.Sin(line.Angle);

        thing.Move(new Vector2D(x, y));
        thing.SnapToAccuracy(vertexDecimals, usePrecisePosition);
        if (map != null) thing.DetermineSector(map);
        thing.Rotate(ClampAngle(front ? 180 + line.AngleDeg : line.AngleDeg));

        if (!preserveHeight || map == null || initialSector == null || thing.Sector == null || ReferenceEquals(initialSector, thing.Sector) || info.AbsoluteZ)
            return;

        if (info.Hangs && initialSector.CeilHeight != thing.Sector.CeilHeight)
        {
            thing.Move(thing.Position.x, thing.Position.y, thing.Height - (initialSector.CeilHeight - thing.Sector.CeilHeight));
            return;
        }

        if (initialSector.FloorHeight != thing.Sector.FloorHeight)
            thing.Move(thing.Position.x, thing.Position.y, thing.Height + (initialSector.FloorHeight - thing.Sector.FloorHeight));
    }

    private static int GetThingHeight(ThingTypeInfo info)
        => info.Height == 0 ? 1 : (int)info.Height;

    private static bool HasMiddleTexture(Sidedef side)
        => !string.IsNullOrEmpty(side.MidTexture) && side.MidTexture != "-";

    private static bool Intersects(double firstBottom, double firstTop, double secondBottom, double secondTop)
        => firstBottom < secondTop && secondBottom < firstTop;

    private static int ClampAngle(int angle)
    {
        int normalized = angle % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
}
