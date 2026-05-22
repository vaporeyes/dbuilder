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
    {
        parser = new UniversalParser { StrictChecking = false };
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
            }
        }

        // Materialize in dependency order: vertices -> sectors -> sidedefs -> linedefs -> things.
        foreach (var v in vertexEntries) map.Vertices.Add(LoadVertex(v));
        foreach (var s in sectorEntries) map.Sectors.Add(LoadSector(s, map.Sectors.Count));
        foreach (var sd in sidedefEntries) map.Sidedefs.Add(LoadSidedef(sd, map.Sectors));
        foreach (var ld in linedefEntries) map.Linedefs.Add(LoadLinedef(ld, map.Vertices, map.Sidedefs));
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
        CollectCustomFields(c, v.Fields, VertexManagedFields);
        return v;
    }

    private static Sector LoadSector(UniversalCollection c, int index)
    {
        var s = new Sector
        {
            Index = index,
            FloorHeight = GetInt(c, "heightfloor"),
            CeilHeight = GetInt(c, "heightceiling"),
            FloorTexture = GetString(c, "texturefloor", "-"),
            CeilTexture = GetString(c, "textureceiling", "-"),
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

        CollectCustomFields(c, s.Fields, SectorManagedFields);
        return s;
    }

    private static Sidedef LoadSidedef(UniversalCollection c, List<Sector> sectors)
    {
        int sectorIdx = GetInt(c, "sector");
        var sd = new Sidedef
        {
            // Line is back-filled when the owning linedef is loaded.
            Sector = (sectorIdx >= 0 && sectorIdx < sectors.Count) ? sectors[sectorIdx] : null,
            OffsetX = GetInt(c, "offsetx"),
            OffsetY = GetInt(c, "offsety"),
            HighTexture = GetString(c, "texturetop", "-"),
            MidTexture = GetString(c, "texturemiddle", "-"),
            LowTexture = GetString(c, "texturebottom", "-"),
        };
        CollectCustomFields(c, sd.Fields, SidedefManagedFields);
        return sd;
    }

    private static Linedef LoadLinedef(UniversalCollection c, List<Vertex> verts, List<Sidedef> sides)
    {
        int v1 = GetInt(c, "v1");
        int v2 = GetInt(c, "v2");
        int sideFront = GetInt(c, "sidefront", -1);
        int sideBack = GetInt(c, "sideback", -1);

        // Index sanity: clamp into range and skip if invalid - leaves the linedef pointing at the
        // first vertex which is wrong-but-not-crashy; the caller can flag missing data later.
        var start = (v1 >= 0 && v1 < verts.Count) ? verts[v1] : verts[0];
        var end = (v2 >= 0 && v2 < verts.Count) ? verts[v2] : verts[0];

        var line = new Linedef(start, end)
        {
            Action = GetInt(c, "special"),
            Tag = GetInt(c, "id"),
        };
        for (int i = 0; i < line.Args.Length; i++) line.Args[i] = GetInt(c, $"arg{i}");
        AppendMoreIds(c, line.Tags);

        if (sideFront >= 0 && sideFront < sides.Count)
        {
            line.Front = sides[sideFront];
            line.Front.Line = line;
            line.Front.IsFront = true;
        }
        if (sideBack >= 0 && sideBack < sides.Count)
        {
            line.Back = sides[sideBack];
            line.Back.Line = line;
            line.Back.IsFront = false;
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

        CollectCustomFields(c, line.Fields, LinedefManagedFields);
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

        CollectCustomFields(c, t.Fields, ThingManagedFields);
        return t;
    }

    // --- Field helpers ---

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

    // Appends ZDoom "moreids" (space-separated extra tags) onto a Tags list seeded with the primary id.
    // Tags[0] is the id read from the typed property; moreids contributes Tags[1..].
    private static void AppendMoreIds(UniversalCollection c, List<int> tags)
    {
        string more = GetString(c, "moreids", "");
        if (string.IsNullOrWhiteSpace(more)) return;
        foreach (var token in more.Split(new[] { ' ', '\t', ',' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int id))
                tags.Add(id);
        }
    }

    // Copies non-managed, non-bool entries into the destination Fields dictionary, normalizing the boxed
    // value to one of int/double/string. Bool entries are skipped here - they are captured as UdmfFlags.
    private static void CollectCustomFields(UniversalCollection c, Dictionary<string, object> fields, HashSet<string> managed)
    {
        foreach (var e in c)
        {
            if (managed.Contains(e.Key)) continue;
            switch (e.Value)
            {
                case bool: continue; // handled as a named flag
                case int i: fields[e.Key] = i; break;
                case long l: fields[e.Key] = (int)l; break;
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
