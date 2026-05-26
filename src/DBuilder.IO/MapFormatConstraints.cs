// ABOUTME: Static map-format constraints for Doom, Hexen and UDMF map saves.
// ABOUTME: Mirrors UDB's MapSetIO limits so callers can reject values before binary fields overflow.

using DBuilder.Map;

namespace DBuilder.IO;

public sealed record MapFormatViolation(string Field, int Value, int Min, int Max);

public static class MapFormatConstraints
{
    public static IReadOnlyList<MapFormatViolation> Validate(MapSet map, MapFormat format)
    {
        var limits = Limits.For(format);
        var issues = new List<MapFormatViolation>();

        Check(issues, "vertices.count", map.Vertices.Count, 0, limits.MaxVertices);
        Check(issues, "linedefs.count", map.Linedefs.Count, 0, limits.MaxLinedefs);
        Check(issues, "sidedefs.count", map.Sidedefs.Count, 0, limits.MaxSidedefs);
        Check(issues, "sectors.count", map.Sectors.Count, 0, limits.MaxSectors);
        Check(issues, "things.count", map.Things.Count, 0, limits.MaxThings);

        for (int i = 0; i < map.Vertices.Count; i++)
        {
            Check(issues, $"vertices[{i}].x", RoundToInt(map.Vertices[i].Position.x), limits.MinCoordinate, limits.MaxCoordinate);
            Check(issues, $"vertices[{i}].y", RoundToInt(map.Vertices[i].Position.y), limits.MinCoordinate, limits.MaxCoordinate);
        }

        for (int i = 0; i < map.Sidedefs.Count; i++)
        {
            Check(issues, $"sidedefs[{i}].offsetx", map.Sidedefs[i].OffsetX, limits.MinTextureOffset, limits.MaxTextureOffset);
            Check(issues, $"sidedefs[{i}].offsety", map.Sidedefs[i].OffsetY, limits.MinTextureOffset, limits.MaxTextureOffset);
        }

        for (int i = 0; i < map.Sectors.Count; i++)
        {
            var sector = map.Sectors[i];
            Check(issues, $"sectors[{i}].floorheight", sector.FloorHeight, limits.MinCoordinate, limits.MaxCoordinate);
            Check(issues, $"sectors[{i}].ceilheight", sector.CeilHeight, limits.MinCoordinate, limits.MaxCoordinate);
            Check(issues, $"sectors[{i}].brightness", sector.Brightness, limits.MinBrightness, limits.MaxBrightness);
            Check(issues, $"sectors[{i}].special", sector.Special, limits.MinEffect, limits.MaxEffect);
            Check(issues, $"sectors[{i}].tag", sector.Tag, limits.MinTag, limits.MaxTag);
        }

        for (int i = 0; i < map.Linedefs.Count; i++)
        {
            var line = map.Linedefs[i];
            Check(issues, $"linedefs[{i}].flags", line.Flags, 0, limits.MaxLinedefFlags);
            Check(issues, $"linedefs[{i}].action", line.Action, limits.MinAction, limits.MaxAction);
            Check(issues, $"linedefs[{i}].tag", line.Tag, limits.MinLinedefTag, limits.MaxLinedefTag);
            for (int arg = 0; arg < line.Args.Length; arg++)
                Check(issues, $"linedefs[{i}].arg{arg}", line.Args[arg], limits.MinArgument, limits.MaxArgument);
        }

        for (int i = 0; i < map.Things.Count; i++)
        {
            var thing = map.Things[i];
            Check(issues, $"things[{i}].x", RoundToInt(thing.Position.x), limits.MinCoordinate, limits.MaxCoordinate);
            Check(issues, $"things[{i}].y", RoundToInt(thing.Position.y), limits.MinCoordinate, limits.MaxCoordinate);
            Check(issues, $"things[{i}].height", RoundToInt(thing.Height), limits.MinThingHeight, limits.MaxThingHeight);
            Check(issues, $"things[{i}].angle", thing.Angle, limits.MinThingAngle, limits.MaxThingAngle);
            Check(issues, $"things[{i}].type", thing.Type, limits.MinThingType, limits.MaxThingType);
            Check(issues, $"things[{i}].flags", thing.Flags, 0, limits.MaxThingFlags);
            Check(issues, $"things[{i}].tag", thing.Tag, limits.MinThingTag, limits.MaxThingTag);
            Check(issues, $"things[{i}].action", thing.Action, limits.MinThingAction, limits.MaxThingAction);
            for (int arg = 0; arg < thing.Args.Length; arg++)
                Check(issues, $"things[{i}].arg{arg}", thing.Args[arg], limits.MinThingArgument, limits.MaxThingArgument);
        }

        return issues;
    }

    public static void ThrowIfInvalid(MapSet map, MapFormat format)
    {
        var issues = Validate(map, format);
        if (issues.Count == 0) return;

        var first = issues[0];
        throw new InvalidDataException(
            $"Map cannot be saved as {format}: {first.Field} value {first.Value} is outside {first.Min}..{first.Max}.");
    }

    private static void Check(List<MapFormatViolation> issues, string field, int value, int min, int max)
    {
        if (value < min || value > max) issues.Add(new MapFormatViolation(field, value, min, max));
    }

    private static int RoundToInt(double value)
    {
        if (value < int.MinValue) return int.MinValue;
        if (value > int.MaxValue) return int.MaxValue;
        return (int)System.Math.Round(value);
    }

    private sealed record Limits(
        int MaxVertices,
        int MaxLinedefs,
        int MaxSidedefs,
        int MaxSectors,
        int MaxThings,
        int MinCoordinate,
        int MaxCoordinate,
        int MinTextureOffset,
        int MaxTextureOffset,
        int MinTag,
        int MaxTag,
        int MinAction,
        int MaxAction,
        int MinArgument,
        int MaxArgument,
        int MinEffect,
        int MaxEffect,
        int MinBrightness,
        int MaxBrightness,
        int MinThingType,
        int MaxThingType,
        int MinThingAngle,
        int MaxThingAngle,
        int MaxLinedefFlags,
        int MaxThingFlags,
        int MinLinedefTag,
        int MaxLinedefTag,
        int MinThingTag,
        int MaxThingTag,
        int MinThingAction,
        int MaxThingAction,
        int MinThingArgument,
        int MaxThingArgument,
        int MinThingHeight,
        int MaxThingHeight)
    {
        public static Limits For(MapFormat format) => format switch
        {
            MapFormat.Udmf => new Limits(
                int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue,
                short.MinValue, short.MaxValue,
                int.MinValue, int.MaxValue,
                int.MinValue, int.MaxValue,
                int.MinValue, int.MaxValue,
                int.MinValue, int.MaxValue,
                int.MinValue, int.MaxValue,
                int.MinValue, int.MaxValue,
                int.MinValue, int.MaxValue,
                int.MinValue, int.MaxValue,
                int.MaxValue, int.MaxValue,
                int.MinValue, int.MaxValue,
                int.MinValue, int.MaxValue,
                int.MinValue, int.MaxValue,
                int.MinValue, int.MaxValue,
                int.MinValue, int.MaxValue),
            MapFormat.Hexen => new Limits(
                ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, int.MaxValue,
                short.MinValue, short.MaxValue,
                short.MinValue, short.MaxValue,
                ushort.MinValue, ushort.MaxValue,
                byte.MinValue, byte.MaxValue,
                byte.MinValue, byte.MaxValue,
                ushort.MinValue, ushort.MaxValue,
                short.MinValue, short.MaxValue,
                short.MinValue, short.MaxValue,
                short.MinValue, short.MaxValue,
                ushort.MaxValue, ushort.MaxValue,
                ushort.MinValue, ushort.MaxValue,
                ushort.MinValue, ushort.MaxValue,
                byte.MinValue, byte.MaxValue,
                byte.MinValue, byte.MaxValue,
                short.MinValue, short.MaxValue),
            _ => new Limits(
                ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, int.MaxValue,
                short.MinValue, short.MaxValue,
                short.MinValue, short.MaxValue,
                ushort.MinValue, ushort.MaxValue,
                ushort.MinValue, ushort.MaxValue,
                0, 0,
                ushort.MinValue, ushort.MaxValue,
                short.MinValue, short.MaxValue,
                short.MinValue, short.MaxValue,
                short.MinValue, short.MaxValue,
                ushort.MaxValue, ushort.MaxValue,
                ushort.MinValue, ushort.MaxValue,
                0, 0,
                0, 0,
                0, 0,
                0, 0),
        };
    }
}
