// ABOUTME: Purpose-built UDMF text-map loader for the minimal Map skeleton.
// ABOUTME: Subset of UDB's UniversalStreamReader - just enough to fill a MapSet for visualization. Skips game-config validation, managed-field categorization, custom UDMF typing, and all error paths beyond parse failure.

/*
 * Inspired by UDB Source/Core/IO/UniversalStreamReader.cs but written from scratch against
 * the minimal Map skeleton. When Config and Types are ported the full UniversalStreamReader
 * can replace this loader.
 */

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public static class UdmfMapLoader
{
    /// <summary>
    /// Parses a UDMF source string into a populated MapSet. Returns null if parsing fails;
    /// inspect <paramref name="parser"/>'s error properties for details.
    /// </summary>
    public static MapSet? Load(string udmfText, out UniversalParser parser)
        => Load(udmfText, out parser, strictChecking: false);

    public static MapSet? Load(string udmfText, out UniversalParser parser, bool strictChecking)
    {
        parser = new UniversalParser { StrictChecking = strictChecking };
        if (!parser.InputConfiguration(udmfText)) return null;
        return Load(parser.Root);
    }

    /// <summary>Builds a MapSet from an already-parsed UniversalCollection (root of a UDMF document).</summary>
    public static MapSet Load(UniversalCollection root)
    {
        var map = new MapSet();

        // Top-level namespace and pass over the element collection.
        // Vertices come first because Linedefs index into them.
        var vertexEntries = new List<UniversalCollection>();
        var sectorEntries = new List<UniversalCollection>();
        var sidedefEntries = new List<UniversalCollection>();
        var linedefEntries = new List<UniversalCollection>();
        var thingEntries = new List<UniversalCollection>();

        foreach (var entry in root)
        {
            switch (entry.Key)
            {
                case "namespace" when entry.Value is string ns:
                    map.Namespace = ns;
                    break;
                case "vertex" when entry.Value is UniversalCollection v:
                    vertexEntries.Add(v);
                    break;
                case "sector" when entry.Value is UniversalCollection s:
                    sectorEntries.Add(s);
                    break;
                case "sidedef" when entry.Value is UniversalCollection sd:
                    sidedefEntries.Add(sd);
                    break;
                case "linedef" when entry.Value is UniversalCollection ld:
                    linedefEntries.Add(ld);
                    break;
                case "thing" when entry.Value is UniversalCollection t:
                    thingEntries.Add(t);
                    break;
                default:
                    CollectUnknownMapData(map, entry);
                    break;
            }
        }

        // Materialize in dependency order: vertices -> sectors -> sidedefs -> linedefs -> things.
        foreach (var v in vertexEntries) map.Vertices.Add(LoadVertex(v));
        foreach (var s in sectorEntries) map.Sectors.Add(LoadSector(s, map.Sectors.Count));
        foreach (var ld in linedefEntries)
        {
            var line = LoadLinedef(ld, map.Vertices, sidedefEntries, map.Sectors, map.Sidedefs);
            if (line != null) map.Linedefs.Add(line);
        }
        foreach (var t in thingEntries) map.Things.Add(LoadThing(t));

        map.BuildIndexes();
        return map;
    }

    private static Vertex LoadVertex(UniversalCollection c)
    {
        double x = GetDouble(c, "x");
        double y = GetDouble(c, "y");
        var v = new Vertex(new Vector2D(x, y))
        {
            ZCeiling = GetDouble(c, "zceiling", double.NaN),
            ZFloor = GetDouble(c, "zfloor", double.NaN),
        };
        CollectCustomFields(c, v.Fields, VertexManagedFields, preserveBoolFields: true);
        return v;
    }

    private static Sector LoadSector(UniversalCollection c, int index)
    {
        string floorTexture = GetString(c, "texturefloor", "-");
        string ceilTexture = GetString(c, "textureceiling", "-");
        var s = new Sector
        {
            Index = index,
            FloorHeight = GetInt(c, "heightfloor"),
            CeilHeight = GetInt(c, "heightceiling"),
            FloorTexture = floorTexture,
            CeilTexture = ceilTexture,
            LongFloorTexture = Lump.MakeLongName(floorTexture, useLongNames: true),
            LongCeilTexture = Lump.MakeLongName(ceilTexture, useLongNames: true),
            Brightness = GetInt(c, "lightlevel", 160),
            Special = GetInt(c, "special"),
            Tag = GetInt(c, "id"),
        };
        AppendMoreIds(c, s.Tags);

        // UDMF slope plane: normal = (a,b,c) normalized, offset = d (NaN when unset).
        var fslope = new Vector3D(GetDouble(c, "floorplane_a"), GetDouble(c, "floorplane_b"), GetDouble(c, "floorplane_c"));
        if (fslope.GetLengthSq() > 0)
        {
            s.FloorSlope = fslope.GetNormal();
            s.FloorSlopeOffset = GetDouble(c, "floorplane_d", double.NaN);
        }
        var cslope = new Vector3D(GetDouble(c, "ceilingplane_a"), GetDouble(c, "ceilingplane_b"), GetDouble(c, "ceilingplane_c"));
        if (cslope.GetLengthSq() > 0)
        {
            s.CeilSlope = cslope.GetNormal();
            s.CeilSlopeOffset = GetDouble(c, "ceilingplane_d", double.NaN);
        }

        foreach (var entry in c)
        {
            if (entry.Value is bool b && b && !SectorManagedFields.Contains(entry.Key))
                s.UdmfFlags.Add(entry.Key);
        }
        CollectCustomFields(c, s.Fields, SectorManagedFields, preserveBoolFields: true);
        return s;
    }

    private static Sidedef? LoadSidedef(UniversalCollection c, List<Sector> sectors)
    {
        int sectorIdx = GetInt(c, "sector");
        if (sectorIdx < 0 || sectorIdx >= sectors.Count) return null;

        string highTexture = GetString(c, "texturetop", "-");
        string midTexture = GetString(c, "texturemiddle", "-");
        string lowTexture = GetString(c, "texturebottom", "-");
        var sd = new Sidedef
        {
            // Line is back-filled when the owning linedef is loaded.
            Sector = sectors[sectorIdx],
            OffsetX = GetInt(c, "offsetx"),
            OffsetY = GetInt(c, "offsety"),
            HighTexture = highTexture,
            MidTexture = midTexture,
            LowTexture = lowTexture,
            LongHighTexture = Lump.MakeLongName(highTexture, useLongNames: true),
            LongMiddleTexture = Lump.MakeLongName(midTexture, useLongNames: true),
            LongLowTexture = Lump.MakeLongName(lowTexture, useLongNames: true),
        };
        foreach (var entry in c)
        {
            if (entry.Value is bool b && b && !SidedefManagedFields.Contains(entry.Key))
                sd.UdmfFlags.Add(entry.Key);
        }
        CollectCustomFields(c, sd.Fields, SidedefManagedFields, preserveBoolFields: true);
        return sd;
    }

    private static Linedef? LoadLinedef(
        UniversalCollection c,
        List<Vertex> verts,
        IReadOnlyList<UniversalCollection> sides,
        List<Sector> sectors,
        List<Sidedef> loadedSides)
    {
        int v1 = GetInt(c, "v1");
        int v2 = GetInt(c, "v2");
        int sideFront = GetInt(c, "sidefront", -1);
        int sideBack = GetInt(c, "sideback", -1);

        if (v1 < 0 || v1 >= verts.Count || v2 < 0 || v2 >= verts.Count) return null;

        var start = verts[v1];
        var end = verts[v2];
        if (Vector2D.ManhattanDistance(start.Position, end.Position) <= 0.0001) return null;

        var line = new Linedef(start, end)
        {
            Action = GetInt(c, "special"),
            Tag = GetInt(c, "id"),
        };
        for (int i = 0; i < line.Args.Length; i++) line.Args[i] = GetInt(c, $"arg{i}");
        AppendMoreIds(c, line.Tags);

        if (sideFront >= 0 && sideFront < sides.Count)
        {
            var side = LoadSidedef(sides[sideFront], sectors);
            if (side != null)
            {
                line.Front = side;
                line.Front.Line = line;
                line.Front.IsFront = true;
                loadedSides.Add(side);
            }
        }
        if (sideBack >= 0 && sideBack < sides.Count)
        {
            var side = LoadSidedef(sides[sideBack], sectors);
            if (side != null)
            {
                line.Back = side;
                line.Back.Line = line;
                line.Back.IsFront = false;
                loadedSides.Add(side);
            }
        }

        // Collect all bool-true keys other than the index/special/id fields as named UDMF flags.
        foreach (var entry in c)
        {
            if (entry.Value is bool b && b
                && entry.Key != "twosided" /* derived from sides */)
            {
                if (!IsLinedefIndexField(entry.Key))
                    line.UdmfFlags.Add(entry.Key);
            }
        }

        CollectCustomFields(c, line.Fields, LinedefManagedFields, preserveBoolFields: false);
        return line;
    }

    private static Thing LoadThing(UniversalCollection c)
    {
        var t = new Thing
        {
            Position = new Vector2D(GetDouble(c, "x"), GetDouble(c, "y")),
            Height = GetDouble(c, "height"),
            Type = GetInt(c, "type"),
            Angle = GetInt(c, "angle"),
            Pitch = GetInt(c, "pitch"),
            Roll = GetInt(c, "roll"),
            ScaleX = GetDouble(c, "scalex", 1.0),
            ScaleY = GetDouble(c, "scaley", 1.0),
            Action = GetInt(c, "special"),
            Tag = GetInt(c, "id"),
        };
        for (int i = 0; i < t.Args.Length; i++) t.Args[i] = GetInt(c, $"arg{i}");

        // "scale" is a uniform-scale shorthand: when present and nonzero it overrides scalex/scaley.
        double uniformScale = GetDouble(c, "scale");
        if (uniformScale != 0) { t.ScaleX = uniformScale; t.ScaleY = uniformScale; }

        foreach (var entry in c)
        {
            if (entry.Value is bool b && b && !IsThingIndexField(entry.Key))
                t.UdmfFlags.Add(entry.Key);
        }

        CollectCustomFields(c, t.Fields, ThingManagedFields, preserveBoolFields: false);
        return t;
    }

    // --- Field helpers ---

    private static void CollectMapField(MapSet map, UniversalEntry entry)
    {
        switch (entry.Value)
        {
            case bool b: map.Fields[entry.Key] = b; break;
            case int i: map.Fields[entry.Key] = i; break;
            case long l: map.Fields[entry.Key] = l; break;
            case double d: map.Fields[entry.Key] = d; break;
            case string s: map.Fields[entry.Key] = s; break;
        }
    }

    private static void CollectUnknownMapData(MapSet map, UniversalEntry entry)
    {
        if (entry.Value is UniversalCollection collection)
            map.UnknownUdmfData.Add(ConvertUnknownEntry(entry.Key, collection));
        else
            CollectMapField(map, entry);
    }

    private static UnknownUdmfEntry ConvertUnknownEntry(string key, UniversalCollection collection)
    {
        var children = new List<UnknownUdmfEntry>();
        foreach (var entry in collection)
        {
            if (entry.Value is UniversalCollection childCollection)
                children.Add(ConvertUnknownEntry(entry.Key, childCollection));
            else if (IsSupportedUnknownValue(entry.Value))
                children.Add(new UnknownUdmfEntry(entry.Key, entry.Value));
        }

        return new UnknownUdmfEntry(key, children);
    }

    private static bool IsSupportedUnknownValue(object value)
        => value is bool or int or long or double or float or string;

    // Keys handled by typed properties on each element. Anything else (and not a bool flag) is
    // preserved verbatim in the element's Fields dictionary so custom UDMF data survives round trip.
    private static readonly HashSet<string> VertexManagedFields = new(StringComparer.Ordinal)
        { "x", "y", "zfloor", "zceiling" };
    private static readonly HashSet<string> SectorManagedFields = new(StringComparer.Ordinal)
        { "heightfloor", "heightceiling", "texturefloor", "textureceiling", "lightlevel", "special", "id", "moreids",
          "floorplane_a", "floorplane_b", "floorplane_c", "floorplane_d",
          "ceilingplane_a", "ceilingplane_b", "ceilingplane_c", "ceilingplane_d" };
    private static readonly HashSet<string> SidedefManagedFields = new(StringComparer.Ordinal)
        { "sector", "offsetx", "offsety", "texturetop", "texturemiddle", "texturebottom" };
    private static readonly HashSet<string> LinedefManagedFields = new(StringComparer.Ordinal)
        { "v1", "v2", "sidefront", "sideback", "special", "id", "moreids", "arg0", "arg1", "arg2", "arg3", "arg4" };
    private static readonly HashSet<string> ThingManagedFields = new(StringComparer.Ordinal)
        { "x", "y", "height", "type", "angle", "pitch", "roll", "scalex", "scaley", "scale",
          "special", "id", "arg0", "arg1", "arg2", "arg3", "arg4" };

    // Appends unique nonzero ZDoom "moreids" tags onto a Tags list seeded with the primary id.
    // Tags[0] is the id read from the typed property, unless that primary id is zero and extras exist.
    private static void AppendMoreIds(UniversalCollection c, List<int> tags)
    {
        string more = GetString(c, "moreids", "");
        if (string.IsNullOrWhiteSpace(more)) return;
        foreach (var token in more.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int id)
                && id != 0
                && !tags.Contains(id))
            {
                tags.Add(id);
            }
        }

        if (tags.Count > 1 && tags[0] == 0) tags.RemoveAt(0);
    }

    // Copies non-managed entries into the destination Fields dictionary, normalizing the boxed value to one of
    // bool/int/double/string. Linedefs and things skip true bools here because unknown true bools are UDMF flags.
    private static void CollectCustomFields(UniversalCollection c, Dictionary<string, object> fields, HashSet<string> managed, bool preserveBoolFields)
    {
        foreach (var e in c)
        {
            if (managed.Contains(e.Key)) continue;
            switch (e.Value)
            {
                case bool b when preserveBoolFields: fields[e.Key] = b; break;
                case bool falseValue when !falseValue: fields[e.Key] = falseValue; break;
                case bool: continue;
                case int i: fields[e.Key] = i; break;
                case long l: fields[e.Key] = l; break;
                case double d: fields[e.Key] = d; break;
                case float f: fields[e.Key] = (double)f; break;
                case string s: fields[e.Key] = s; break;
            }
        }
    }

    private static bool IsLinedefIndexField(string key) => key switch
    {
        "v1" or "v2" or "sidefront" or "sideback" or "special" or "id"
            or "arg0" or "arg1" or "arg2" or "arg3" or "arg4" => true,
        _ => false,
    };

    private static bool IsThingIndexField(string key) => key switch
    {
        "x" or "y" or "height" or "type" or "angle" or "special" or "id"
            or "arg0" or "arg1" or "arg2" or "arg3" or "arg4" => true,
        _ => false,
    };

    private static int GetInt(UniversalCollection c, string key, int defaultValue = 0)
    {
        foreach (var e in c)
        {
            if (e.Key == key)
            {
                return e.Value switch
                {
                    int i => i,
                    long l => (int)l,
                    double d => (int)d,
                    float f => (int)f,
                    _ => defaultValue,
                };
            }
        }
        return defaultValue;
    }

    private static double GetDouble(UniversalCollection c, string key, double defaultValue = 0)
    {
        foreach (var e in c)
        {
            if (e.Key == key)
            {
                return e.Value switch
                {
                    double d => d,
                    float f => f,
                    int i => i,
                    long l => l,
                    _ => defaultValue,
                };
            }
        }
        return defaultValue;
    }

    private static string GetString(UniversalCollection c, string key, string defaultValue)
    {
        foreach (var e in c)
        {
            if (e.Key == key && e.Value is string s) return s;
        }
        return defaultValue;
    }
}
