// ABOUTME: Data-level Wavefront OBJ terrain importer ported from UDB BuilderEffects.
// ABOUTME: Parses triangular OBJ faces and builds selected triangular map sectors without UI dependencies.

using System.Globalization;
using DBuilder.Geometry;

namespace DBuilder.Map;

public enum ObjTerrainUpAxis
{
    Y,
    Z,
    X
}

public readonly record struct ObjTerrainFace(Vector3D V1, Vector3D V2, Vector3D V3);

public sealed class ObjTerrainGeometry
{
    public List<Vector3D> Vertices { get; } = new();
    public List<ObjTerrainFace> Faces { get; } = new();
    public int MinZ { get; internal set; } = int.MaxValue;
    public int MaxZ { get; internal set; } = int.MinValue;
}

public sealed record ObjTerrainParseResult(ObjTerrainGeometry Geometry, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;
}

public readonly record struct ObjTerrainImportOptions(
    int DefaultBrightness = 160,
    string DefaultFloorTexture = "FLOOR0_1",
    string DefaultCeilingTexture = "F_SKY1",
    string DefaultWallTexture = "STARTAN3",
    bool UseVertexHeights = false,
    bool CreateVertexHeightThings = false,
    int VertexHeightThingType = ObjTerrainImporter.VertexHeightThingType);

public readonly record struct ObjTerrainImportResult(
    int VerticesCreated,
    int LinedefsCreated,
    int SidedefsCreated,
    int SectorsCreated,
    int ThingsCreated);

public static class ObjTerrainImporter
{
    public const int VertexHeightThingType = 1504;

    public static ObjTerrainParseResult Parse(string text, double scale = 1.0, ObjTerrainUpAxis axis = ObjTerrainUpAxis.Y)
    {
        var geometry = new ObjTerrainGeometry();
        var errors = new List<string>();
        string[] lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
        double verticalAngle = Math.Round(Angle2D.PI, 3);

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];
            int lineNumber = lineIndex + 1;
            if (line.StartsWith("v ", StringComparison.Ordinal))
            {
                ParseVertex(line, lineNumber, scale, axis, geometry, errors);
            }
            else if (line.StartsWith("f ", StringComparison.Ordinal))
            {
                ParseFace(line, lineNumber, geometry, errors, verticalAngle);
            }
        }

        return new ObjTerrainParseResult(geometry, errors);
    }

    public static ObjTerrainImportResult BuildMapGeometry(MapSet map, ObjTerrainGeometry geometry, ObjTerrainImportOptions options)
    {
        if (geometry.Vertices.Count < 3 || geometry.Faces.Count == 0)
            return new ObjTerrainImportResult(0, 0, 0, 0, 0);

        int maxZ = geometry.MaxZ + (geometry.MaxZ - geometry.MinZ) / 2;
        var vertices = new Dictionary<Vector3D, Vertex>();
        var lines = new Dictionary<EdgeKey, Linedef>();
        int startingVertexCount = map.Vertices.Count;
        int startingLineCount = map.Linedefs.Count;
        int startingSideCount = map.Sidedefs.Count;
        int startingSectorCount = map.Sectors.Count;
        int startingThingCount = map.Things.Count;

        foreach (ObjTerrainFace face in geometry.Faces)
        {
            Sector sector = map.AddSector();
            sector.Selected = true;
            sector.FloorHeight = (int)Math.Round((face.V1.z + face.V2.z + face.V3.z) / 3.0);
            sector.CeilHeight = maxZ;
            sector.Brightness = options.DefaultBrightness;
            sector.SetCeilTexture(options.DefaultCeilingTexture);
            sector.SetFloorTexture(options.DefaultFloorTexture);

            AddSide(map, vertices, lines, sector, face.V1, face.V2, options);
            AddSide(map, vertices, lines, sector, face.V2, face.V3, options);
            AddSide(map, vertices, lines, sector, face.V3, face.V1, options);
        }

        if (options.UseVertexHeights && options.CreateVertexHeightThings)
        {
            foreach (Vector3D position in vertices.Keys)
            {
                Thing thing = map.AddThing(new Vector2D(position.x, position.y), options.VertexHeightThingType);
                thing.Height = position.z;
                thing.Selected = true;
            }
        }

        map.BuildIndexes();
        return new ObjTerrainImportResult(
            map.Vertices.Count - startingVertexCount,
            map.Linedefs.Count - startingLineCount,
            map.Sidedefs.Count - startingSideCount,
            map.Sectors.Count - startingSectorCount,
            map.Things.Count - startingThingCount);
    }

    private static void ParseVertex(
        string line,
        int lineNumber,
        double scale,
        ObjTerrainUpAxis axis,
        ObjTerrainGeometry geometry,
        List<string> errors)
    {
        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
            || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double y)
            || !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double z))
        {
            errors.Add($"Failed to parse vertex definition at line {lineNumber}.");
            return;
        }

        Vector3D vertex = ConvertVertex(x, y, z, scale, axis);
        geometry.MaxZ = Math.Max(geometry.MaxZ, (int)vertex.z);
        geometry.MinZ = Math.Min(geometry.MinZ, (int)vertex.z);
        geometry.Vertices.Add(vertex);
    }

    private static void ParseFace(
        string line,
        int lineNumber,
        ObjTerrainGeometry geometry,
        List<string> errors,
        double verticalAngle)
    {
        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
        {
            errors.Add($"Failed to parse face definition at line {lineNumber}: only triangular faces are supported.");
            return;
        }

        if (!TryReadVertexIndex(parts[1], out int v1)
            || !TryReadVertexIndex(parts[2], out int v2)
            || !TryReadVertexIndex(parts[3], out int v3)
            || !HasVertex(geometry, v1)
            || !HasVertex(geometry, v2)
            || !HasVertex(geometry, v3))
        {
            errors.Add($"Failed to parse face definition at line {lineNumber}.");
            return;
        }

        Vector3D first = geometry.Vertices[v1 - 1];
        Vector3D second = geometry.Vertices[v2 - 1];
        Vector3D third = geometry.Vertices[v3 - 1];
        if (first == second || first == third || second == third) return;

        Plane plane = new(first, second, third, true);
        if (Math.Round(plane.Normal.GetAngleZ(), 3) == verticalAngle) return;

        geometry.Faces.Add(new ObjTerrainFace(third, second, first));
    }

    private static Vector3D ConvertVertex(double x, double y, double z, double scale, ObjTerrainUpAxis axis)
        => axis switch
        {
            ObjTerrainUpAxis.Z => new Vector3D(
                (int)Math.Round(-x * scale),
                (int)Math.Round(-y * scale),
                (int)Math.Round(z * scale)),
            ObjTerrainUpAxis.X => new Vector3D(
                (int)Math.Round(-y * scale),
                (int)Math.Round(-z * scale),
                (int)Math.Round(x * scale)),
            _ => new Vector3D(
                (int)Math.Round(x * scale),
                (int)Math.Round(-z * scale),
                (int)Math.Round(y * scale)),
        };

    private static bool TryReadVertexIndex(string definition, out int index)
    {
        int slash = definition.IndexOf("/", StringComparison.Ordinal);
        if (slash != -1) definition = definition[..slash];
        return int.TryParse(definition, NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
    }

    private static bool HasVertex(ObjTerrainGeometry geometry, int oneBasedIndex)
        => oneBasedIndex > 0 && oneBasedIndex <= geometry.Vertices.Count;

    private static void AddSide(
        MapSet map,
        Dictionary<Vector3D, Vertex> vertices,
        Dictionary<EdgeKey, Linedef> lines,
        Sector sector,
        Vector3D from,
        Vector3D to,
        ObjTerrainImportOptions options)
    {
        Vertex start = GetVertex(map, vertices, from, options.UseVertexHeights);
        Vertex end = GetVertex(map, vertices, to, options.UseVertexHeights);
        EdgeKey edge = new(from, to);

        if (!lines.TryGetValue(edge, out Linedef? line))
        {
            line = map.AddLinedef(start, end);
            line.Selected = true;
            Sidedef side = map.AddSidedef(line, true, sector);
            if (!options.UseVertexHeights) side.SetTextureLow(options.DefaultWallTexture);
            lines.Add(edge, line);
            return;
        }

        Sidedef back = map.AddSidedef(line, false, sector);
        if (!options.UseVertexHeights) back.SetTextureLow(options.DefaultWallTexture);
    }

    private static Vertex GetVertex(MapSet map, Dictionary<Vector3D, Vertex> vertices, Vector3D position, bool useVertexHeights)
    {
        if (vertices.TryGetValue(position, out Vertex? vertex)) return vertex;

        vertex = map.AddVertex(new Vector2D(position.x, position.y));
        if (useVertexHeights) vertex.ZFloor = position.z;
        vertices.Add(position, vertex);
        return vertex;
    }

    private readonly record struct EdgeKey
    {
        public readonly Vector3D A;
        public readonly Vector3D B;

        public EdgeKey(Vector3D first, Vector3D second)
        {
            if (ComesBefore(first, second))
            {
                A = first;
                B = second;
            }
            else
            {
                A = second;
                B = first;
            }
        }

        private static bool ComesBefore(Vector3D left, Vector3D right)
        {
            int x = left.x.CompareTo(right.x);
            if (x != 0) return x < 0;
            int y = left.y.CompareTo(right.y);
            if (y != 0) return y < 0;
            return left.z.CompareTo(right.z) <= 0;
        }
    }
}
