// ABOUTME: Binary clipboard format for copy/paste of map element subsets - writer + reader pair, BinaryWriter-based.
// ABOUTME: Wire shape mirrors UDB's ClipboardStreamReader/Writer but trimmed to the fields the current Map skeleton exposes.

/*
 * Inspired by UDB Source/Core/IO/ClipboardStreamReader.cs + ClipboardStreamWriter.cs.  The
 * original carries more fields (per-vertex z-floor/ceiling, sector slopes, multi-tags,
 * thing pitch/roll/scale, custom UDMF fields).  This port keeps the same overall block
 * order so the format can be extended in place when the Map model grows in Tier 2.
 *
 * Wire layout (all little-endian, UDB-style length-prefixed strings as int32 char count + UTF-8 chars):
 *
 *   header:    int32 numVerts, int32 numSectors, int32 numLinedefs, int32 numThings
 *   vertices:  int32 count; per vertex: double x, double y, zceiling, zfloor; customFields
 *   sectors:   int32 count; per sector: int32 special, hfloor, hceil, bright; tags;
 *                                       str floortex, str ceiltex;
 *                                       double floorSlopeOffset, fSlope.x/y/z,
 *                                              ceilSlopeOffset, cSlope.x/y/z;
 *                                       int32 udmfFlagCount; (str, bool) pairs; customFields
 *   sidedefs:  int32 count; per sidedef: int32 offx, offy, sectorId; str hi, mid, lo;
 *                                       int32 udmfFlagCount; (str, bool) pairs; customFields
 *   linedefs:  int32 count; per linedef: int32 v1, v2, sidefront, sideback, action,
 *                                       args[5], flags; tags;
 *                                       int32 udmfFlagCount; (str, bool) pairs; customFields
 *   things:    int32 count; per thing: int32 tag; double x, y, height;
 *                                      int32 angle, pitch, roll; double scalex, scaley;
 *                                      int32 type, action, args[5], flags;
 *                                      int32 udmfFlagCount; (str, bool) pairs; customFields
 *
 *   tags:         int32 count; int32 tag per entry
 *   customFields: int32 count; per field: str key, int32 declared UniversalType,
 *                 int32 primitive UniversalType, then value. The private
 *                 !dbuilder_ignored_error_checks string field stores ignored map-check kinds.
 */

using System.Collections.Generic;
using System.IO;
using System.Text;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public static class ClipboardStreamWriter
{
    private const string IgnoredErrorChecksField = "!dbuilder_ignored_error_checks";

    /// <summary>Writes the entire map to <paramref name="stream"/> in clipboard binary format.</summary>
    public static void Write(MapSet map, Stream stream)
    {
        Write(map.Vertices, map.Linedefs, map.Sidedefs, map.Sectors, map.Things, stream);
    }

    internal static void WriteSnapshot(MapSet map, Stream stream)
        => Write(map.Vertices, map.Linedefs, map.Sidedefs, map.Sectors, map.Things, stream, includeGroups: true);

    /// <summary>
    /// Writes the given element subsets to <paramref name="stream"/>.  Callers are responsible for closing the subset
    /// under references (a linedef referencing a vertex not in <paramref name="vertices"/> will encode v1/v2 as 0).
    /// </summary>
    public static void Write(
        IReadOnlyList<Vertex> vertices,
        IReadOnlyList<Linedef> linedefs,
        IReadOnlyList<Sidedef> sidedefs,
        IReadOnlyList<Sector> sectors,
        IReadOnlyList<Thing> things,
        Stream stream)
        => Write(vertices, linedefs, sidedefs, sectors, things, stream, includeGroups: false);

    private static void Write(
        IReadOnlyList<Vertex> vertices,
        IReadOnlyList<Linedef> linedefs,
        IReadOnlyList<Sidedef> sidedefs,
        IReadOnlyList<Sector> sectors,
        IReadOnlyList<Thing> things,
        Stream stream,
        bool includeGroups)
    {
        var vertexIds  = BuildIndex(vertices);
        var sidedefIds = BuildIndex(sidedefs);
        var sectorIds  = BuildIndex(sectors);

        var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        // Header: counts of each top-level element block (for early max-bounds checks on read).
        w.Write(vertices.Count);
        w.Write(sectors.Count);
        w.Write(linedefs.Count);
        w.Write(things.Count);

        WriteVertices(vertices, w, includeGroups);
        WriteSectors(sectors, w, includeGroups);
        WriteSidedefs(sidedefs, w, sectorIds);
        WriteLinedefs(linedefs, w, sidedefIds, vertexIds, includeGroups);
        WriteThings(things, w, includeGroups);
        w.Flush();
    }

    private static void WriteVertices(IReadOnlyList<Vertex> verts, BinaryWriter w, bool includeGroups)
    {
        w.Write(verts.Count);
        foreach (var v in verts)
        {
            w.Write(v.Position.x);
            w.Write(v.Position.y);
            w.Write(v.ZCeiling);
            w.Write(v.ZFloor);
            if (includeGroups) w.Write(v.Groups);
            WriteCustomFields(w, v.Fields, v.IgnoredErrorChecks);
        }
    }

    private static void WriteSectors(IReadOnlyList<Sector> sectors, BinaryWriter w, bool includeGroups)
    {
        w.Write(sectors.Count);
        foreach (var s in sectors)
        {
            w.Write(s.Special);
            w.Write(s.FloorHeight);
            w.Write(s.CeilHeight);
            w.Write(s.Brightness);
            WriteTags(w, s.Tags);
            WriteString(w, s.FloorTexture);
            WriteString(w, s.CeilTexture);
            w.Write(s.FloorSlopeOffset);
            w.Write(s.FloorSlope.x); w.Write(s.FloorSlope.y); w.Write(s.FloorSlope.z);
            w.Write(s.CeilSlopeOffset);
            w.Write(s.CeilSlope.x); w.Write(s.CeilSlope.y); w.Write(s.CeilSlope.z);
            if (includeGroups) w.Write(s.Groups);
            WriteUdmfFlagSet(w, s.UdmfFlags);
            WriteCustomFields(w, s.Fields, s.IgnoredErrorChecks);
        }
    }

    private static void WriteSidedefs(IReadOnlyList<Sidedef> sides, BinaryWriter w, Dictionary<Sector, int> sectorIds)
    {
        w.Write(sides.Count);
        foreach (var sd in sides)
        {
            w.Write(sd.OffsetX);
            w.Write(sd.OffsetY);
            int sectorIdx = sd.Sector != null && sectorIds.TryGetValue(sd.Sector, out int si) ? si : -1;
            w.Write(sectorIdx);
            WriteString(w, sd.HighTexture);
            WriteString(w, sd.MidTexture);
            WriteString(w, sd.LowTexture);
            WriteUdmfFlagSet(w, sd.UdmfFlags);
            WriteCustomFields(w, sd.Fields, sd.IgnoredErrorChecks);
        }
    }

    private static void WriteLinedefs(IReadOnlyList<Linedef> lines, BinaryWriter w,
                                      Dictionary<Sidedef, int> sidedefIds, Dictionary<Vertex, int> vertexIds,
                                      bool includeGroups)
    {
        w.Write(lines.Count);
        foreach (var l in lines)
        {
            w.Write(vertexIds.TryGetValue(l.Start, out int v1i) ? v1i : 0);
            w.Write(vertexIds.TryGetValue(l.End,   out int v2i) ? v2i : 0);
            w.Write(l.Front != null && sidedefIds.TryGetValue(l.Front, out int srI) ? srI : -1);
            w.Write(l.Back  != null && sidedefIds.TryGetValue(l.Back,  out int slI) ? slI : -1);
            w.Write(l.Action);
            for (int i = 0; i < l.Args.Length; i++) w.Write(l.Args[i]);
            w.Write(l.Flags);
            WriteTags(w, l.Tags);
            if (includeGroups) w.Write(l.Groups);
            WriteUdmfFlagSet(w, l.UdmfFlags);
            WriteCustomFields(w, l.Fields, l.IgnoredErrorChecks);
        }
    }

    private static void WriteThings(IReadOnlyList<Thing> things, BinaryWriter w, bool includeGroups)
    {
        w.Write(things.Count);
        foreach (var t in things)
        {
            w.Write(t.Tag);
            w.Write(t.Position.x);
            w.Write(t.Position.y);
            w.Write(t.Height);
            w.Write(t.Angle);
            w.Write(t.Pitch);
            w.Write(t.Roll);
            w.Write(t.ScaleX);
            w.Write(t.ScaleY);
            w.Write(t.Type);
            w.Write(t.Action);
            for (int i = 0; i < t.Args.Length; i++) w.Write(t.Args[i]);
            w.Write(t.Flags);
            if (includeGroups) w.Write(t.Groups);
            WriteUdmfFlagSet(w, t.UdmfFlags);
            WriteCustomFields(w, t.Fields, t.IgnoredErrorChecks);
        }
    }

    private static void WriteTags(BinaryWriter w, List<int> tags)
    {
        w.Write(tags.Count);
        foreach (int t in tags) w.Write(t);
    }

    private static void WriteCustomFields(
        BinaryWriter w,
        Dictionary<string, object> fields,
        HashSet<MapIssueKind>? ignoredChecks = null)
    {
        bool writeIgnoredChecks = ignoredChecks is { Count: > 0 } && !fields.ContainsKey(IgnoredErrorChecksField);
        w.Write(fields.Count + (writeIgnoredChecks ? 1 : 0));
        foreach (var kv in fields)
            WriteCustomField(w, kv.Key, kv.Value);

        if (writeIgnoredChecks)
            WriteCustomField(w, IgnoredErrorChecksField, string.Join("\n", ignoredChecks!.OrderBy(check => check.ToString())));
    }

    private static void WriteCustomField(BinaryWriter w, string key, object value)
    {
        WriteString(w, key);
        var primitiveType = PrimitiveUniversalType(value);
        w.Write((int)primitiveType);
        w.Write((int)primitiveType);
        switch (value)
        {
            case bool b: w.Write(b); break;
            case double d: w.Write(d); break;
            case float f: w.Write((double)f); break;
            case int i: w.Write(i); break;
            case long l: w.Write(System.Convert.ToInt32(l)); break;
            case string s: WriteString(w, s); break;
            default: w.Write(System.Convert.ToInt32(value)); break;
        }
    }

    private static UniversalType PrimitiveUniversalType(object value) => value switch
    {
        bool => UniversalType.Boolean,
        double or float => UniversalType.Float,
        string => UniversalType.String,
        _ => UniversalType.Integer,
    };

    private static void WriteUdmfFlagSet(BinaryWriter w, HashSet<string> flags)
    {
        w.Write(flags.Count);
        foreach (var f in flags)
        {
            WriteString(w, f);
            w.Write(true);
        }
    }

    private static void WriteString(BinaryWriter w, string s)
    {
        s ??= string.Empty;
        w.Write(s.Length);
        w.Write(s.ToCharArray());
    }

    private static Dictionary<T, int> BuildIndex<T>(IReadOnlyList<T> list) where T : class
    {
        var dict = new Dictionary<T, int>(list.Count, ReferenceEqualityComparer.Instance);
        for (int i = 0; i < list.Count; i++) dict[list[i]] = i;
        return dict;
    }
}

public static class ClipboardStreamReader
{
    private const string IgnoredErrorChecksField = "!dbuilder_ignored_error_checks";

    /// <summary>
    /// Reads clipboard-format bytes from <paramref name="stream"/> and appends them to <paramref name="map"/>.
    /// New elements are appended at the end of each MapSet list; existing elements are untouched.
    /// Returns the indices into the destination lists of the newly-added elements so paste callers
    /// can position / select them.
    /// </summary>
    public static PasteResult Read(MapSet map, Stream stream)
        => Read(map, stream, includeGroups: false);

    internal static PasteResult ReadSnapshot(MapSet map, Stream stream)
        => Read(map, stream, includeGroups: true);

    private static PasteResult Read(MapSet map, Stream stream, bool includeGroups)
    {
        var r = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        // Header counts - currently advisory (bounds-check happens at the per-block count too).
        _ = r.ReadInt32(); _ = r.ReadInt32(); _ = r.ReadInt32(); _ = r.ReadInt32();

        int firstVertex  = map.Vertices.Count;
        int firstSector  = map.Sectors.Count;
        int firstSidedef = map.Sidedefs.Count;
        int firstLinedef = map.Linedefs.Count;
        int firstThing   = map.Things.Count;

        var newVerts   = ReadVertices(map, r, includeGroups);
        var newSectors = ReadSectors(map, r, includeGroups);
        var newSidedefs = ReadSidedefs(map, r, newSectors);
        ReadLinedefs(map, r, newVerts, newSidedefs, includeGroups);
        RemoveUnattachedSidedefs(map, firstSidedef);
        ReadThings(map, r, includeGroups);

        map.BuildIndexes();

        return new PasteResult(firstVertex, firstSector, firstSidedef, firstLinedef, firstThing,
                               map.Vertices.Count - firstVertex,
                               map.Sectors.Count  - firstSector,
                               map.Sidedefs.Count - firstSidedef,
                               map.Linedefs.Count - firstLinedef,
                               map.Things.Count   - firstThing);
    }

    private static List<Vertex> ReadVertices(MapSet map, BinaryReader r, bool includeGroups)
    {
        int count = r.ReadInt32();
        var list = new List<Vertex>(count);
        for (int i = 0; i < count; i++)
        {
            double x = r.ReadDouble();
            double y = r.ReadDouble();
            var v = new Vertex(new Vector2D(x, y))
            {
                ZCeiling = r.ReadDouble(),
                ZFloor = r.ReadDouble(),
            };
            if (includeGroups) v.Groups = r.ReadInt32();
            ReadCustomFields(r, v.Fields, v.IgnoredErrorChecks);
            map.Vertices.Add(v);
            list.Add(v);
        }
        return list;
    }

    private static List<Sector> ReadSectors(MapSet map, BinaryReader r, bool includeGroups)
    {
        int count = r.ReadInt32();
        var list = new List<Sector>(count);
        for (int i = 0; i < count; i++)
        {
            int special   = r.ReadInt32();
            int hfloor    = r.ReadInt32();
            int hceil     = r.ReadInt32();
            int bright    = r.ReadInt32();
            var tags      = ReadTags(r);
            string tfloor = ReadString(r);
            string tceil  = ReadString(r);
            double foffset = r.ReadDouble();
            var fslope = new Vector3D(r.ReadDouble(), r.ReadDouble(), r.ReadDouble());
            double coffset = r.ReadDouble();
            var cslope = new Vector3D(r.ReadDouble(), r.ReadDouble(), r.ReadDouble());

            var s = new Sector
            {
                Index = map.Sectors.Count,
                Special = special,
                FloorHeight = hfloor,
                CeilHeight = hceil,
                Brightness = bright,
                FloorTexture = tfloor,
                CeilTexture = tceil,
                FloorSlopeOffset = foffset,
                FloorSlope = fslope,
                CeilSlopeOffset = coffset,
                CeilSlope = cslope,
            };
            if (includeGroups) s.Groups = r.ReadInt32();
            s.Tags.AddRange(tags);
            ReadUdmfFlagSet(r, s.UdmfFlags);
            ReadCustomFields(r, s.Fields, s.IgnoredErrorChecks);
            map.Sectors.Add(s);
            list.Add(s);
        }
        return list;
    }

    private static List<Sidedef> ReadSidedefs(MapSet map, BinaryReader r, List<Sector> newSectors)
    {
        int count = r.ReadInt32();
        var list = new List<Sidedef>(count);
        for (int i = 0; i < count; i++)
        {
            int offx = r.ReadInt32();
            int offy = r.ReadInt32();
            int secId = r.ReadInt32();
            string hi  = ReadString(r);
            string mid = ReadString(r);
            string lo  = ReadString(r);

            var sd = new Sidedef
            {
                OffsetX = offx,
                OffsetY = offy,
                HighTexture = hi,
                MidTexture = mid,
                LowTexture = lo,
                Sector = (secId >= 0 && secId < newSectors.Count) ? newSectors[secId] : null,
            };
            ReadUdmfFlagSet(r, sd.UdmfFlags);
            ReadCustomFields(r, sd.Fields, sd.IgnoredErrorChecks);
            map.Sidedefs.Add(sd);
            list.Add(sd);
        }
        return list;
    }

    private static void ReadLinedefs(MapSet map, BinaryReader r, List<Vertex> newVerts, List<Sidedef> newSidedefs, bool includeGroups)
    {
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            int v1 = r.ReadInt32();
            int v2 = r.ReadInt32();
            int sf = r.ReadInt32();
            int sb = r.ReadInt32();
            int action = r.ReadInt32();
            int a0 = r.ReadInt32(), a1 = r.ReadInt32(), a2 = r.ReadInt32(), a3 = r.ReadInt32(), a4 = r.ReadInt32();
            int flags = r.ReadInt32();
            var tags = ReadTags(r);
            int groups = includeGroups ? r.ReadInt32() : 0;

            var udmfFlags = new HashSet<string>(System.StringComparer.Ordinal);
            ReadUdmfFlagSet(r, udmfFlags);
            var fields = new Dictionary<string, object>(System.StringComparer.Ordinal);
            var ignoredChecks = new HashSet<MapIssueKind>();
            ReadCustomFields(r, fields, ignoredChecks);

            if (!TryGetValidLinedefVertices(newVerts, v1, v2, out var start, out var end))
                continue;

            var l = new Linedef(start, end)
            {
                Action = action,
                Flags = flags,
                Groups = groups,
            };
            l.Tags.AddRange(tags);
            l.Args[0] = a0; l.Args[1] = a1; l.Args[2] = a2; l.Args[3] = a3; l.Args[4] = a4;

            if (sf >= 0 && sf < newSidedefs.Count)
            {
                var side = newSidedefs[sf];
                if (side.Sector != null)
                {
                    l.Front = side;
                    l.Front.Line = l;
                    l.Front.IsFront = true;
                }
            }
            if (sb >= 0 && sb < newSidedefs.Count)
            {
                var side = newSidedefs[sb];
                if (side.Sector != null)
                {
                    l.Back = side;
                    l.Back.Line = l;
                    l.Back.IsFront = false;
                }
            }

            foreach (var flag in udmfFlags) l.UdmfFlags.Add(flag);
            foreach (var field in fields) l.Fields[field.Key] = field.Value;
            foreach (var check in ignoredChecks) l.IgnoredErrorChecks.Add(check);
            map.Linedefs.Add(l);
        }
    }

    private static bool TryGetValidLinedefVertices(List<Vertex> verts, int startIndex, int endIndex, out Vertex start, out Vertex end)
    {
        start = null!;
        end = null!;
        if (startIndex < 0 || startIndex >= verts.Count || endIndex < 0 || endIndex >= verts.Count)
            return false;

        start = verts[startIndex];
        end = verts[endIndex];
        return Vector2D.ManhattanDistance(start.Position, end.Position) > 0.0001;
    }

    private static void RemoveUnattachedSidedefs(MapSet map, int firstSidedef)
    {
        for (int i = map.Sidedefs.Count - 1; i >= firstSidedef; i--)
        {
            var side = map.Sidedefs[i];
            if (side.Line == null || side.Sector == null) map.Sidedefs.RemoveAt(i);
        }
    }

    private static void ReadThings(MapSet map, BinaryReader r, bool includeGroups)
    {
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            int tag = r.ReadInt32();
            double x = r.ReadDouble();
            double y = r.ReadDouble();
            double h = r.ReadDouble();
            int angle = r.ReadInt32();
            int pitch = r.ReadInt32();
            int roll = r.ReadInt32();
            double scaleX = r.ReadDouble();
            double scaleY = r.ReadDouble();
            int type = r.ReadInt32();
            int action = r.ReadInt32();
            int a0 = r.ReadInt32(), a1 = r.ReadInt32(), a2 = r.ReadInt32(), a3 = r.ReadInt32(), a4 = r.ReadInt32();
            int flags = r.ReadInt32();
            int groups = includeGroups ? r.ReadInt32() : 0;

            var t = new Thing
            {
                Tag = tag,
                Position = new Vector2D(x, y),
                Height = h,
                Angle = angle,
                Pitch = pitch,
                Roll = roll,
                ScaleX = scaleX,
                ScaleY = scaleY,
                Type = type,
                Action = action,
                Flags = flags,
                Groups = groups,
            };
            t.Args[0] = a0; t.Args[1] = a1; t.Args[2] = a2; t.Args[3] = a3; t.Args[4] = a4;
            ReadUdmfFlagSet(r, t.UdmfFlags);
            ReadCustomFields(r, t.Fields, t.IgnoredErrorChecks);
            map.Things.Add(t);
        }
    }

    private static void ReadUdmfFlagSet(BinaryReader r, HashSet<string> target)
    {
        int n = r.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            string key = ReadString(r);
            bool val = r.ReadBoolean();
            if (val) target.Add(key);
        }
    }

    private static List<int> ReadTags(BinaryReader r)
    {
        int n = r.ReadInt32();
        var tags = new List<int>(n);
        for (int i = 0; i < n; i++) tags.Add(r.ReadInt32());
        return tags;
    }

    private static void ReadCustomFields(
        BinaryReader r,
        Dictionary<string, object> target,
        HashSet<MapIssueKind>? ignoredChecks = null)
    {
        int n = r.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            string key = ReadString(r);
            _ = r.ReadInt32();
            var primitiveType = (UniversalType)r.ReadInt32();
            object value = primitiveType switch
            {
                UniversalType.Integer => r.ReadInt32(),
                UniversalType.Float => r.ReadDouble(),
                UniversalType.Boolean => r.ReadBoolean(),
                UniversalType.String => ReadString(r),
                _ => throw new IOException($"Unknown clipboard field primitive type {primitiveType} for key \"{key}\""),
            };
            if (ignoredChecks != null && key == IgnoredErrorChecksField && value is string ignoredText)
            {
                ReadIgnoredChecks(ignoredText, ignoredChecks);
                continue;
            }

            target[key] = value;
        }
    }

    private static void ReadIgnoredChecks(string value, HashSet<MapIssueKind> target)
    {
        foreach (string raw in value.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse(raw, out MapIssueKind kind))
                target.Add(kind);
        }
    }

    private static string ReadString(BinaryReader r)
    {
        int len = r.ReadInt32();
        if (len <= 0) return string.Empty;
        return new string(r.ReadChars(len));
    }
}

/// <summary>Describes the slice of a MapSet that ClipboardStreamReader.Read just appended.</summary>
public readonly record struct PasteResult(
    int FirstVertex, int FirstSector, int FirstSidedef, int FirstLinedef, int FirstThing,
    int VertexCount, int SectorCount, int SidedefCount, int LinedefCount, int ThingCount);
