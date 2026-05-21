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

        return map;
    }

    private static Vertex LoadVertex(UniversalCollection c)
    {
        double x = GetDouble(c, "x");
        double y = GetDouble(c, "y");
        return new Vertex(new Vector2D(x, y));
    }

    private static Sector LoadSector(UniversalCollection c, int index)
    {
        return new Sector
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
    }

    private static Sidedef LoadSidedef(UniversalCollection c, List<Sector> sectors)
    {
        int sectorIdx = GetInt(c, "sector");
        return new Sidedef
        {
            // Line is back-filled when the owning linedef is loaded.
            Sector = (sectorIdx >= 0 && sectorIdx < sectors.Count) ? sectors[sectorIdx] : null,
            OffsetX = GetInt(c, "offsetx"),
            OffsetY = GetInt(c, "offsety"),
            HighTexture = GetString(c, "texturetop", "-"),
            MidTexture = GetString(c, "texturemiddle", "-"),
            LowTexture = GetString(c, "texturebottom", "-"),
        };
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
            Action = GetInt(c, "special"),
            Tag = GetInt(c, "id"),
        };

        foreach (var entry in c)
        {
            if (entry.Value is bool b && b && !IsThingIndexField(entry.Key))
                t.UdmfFlags.Add(entry.Key);
        }

        return t;
    }

    // --- Field helpers ---

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
