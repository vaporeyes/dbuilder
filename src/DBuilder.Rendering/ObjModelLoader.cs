// ABOUTME: Loads Wavefront OBJ model geometry into UDB-style world vertices and mesh groups.
// ABOUTME: Preserves UDB's OBJ coordinate conversion, quad splitting, material skins, and parse errors.

using System.Globalization;
using System.Text;

namespace DBuilder.Rendering;

public sealed record ObjModelLoadResult(
    IReadOnlyList<string> Skins,
    IReadOnlyList<ObjModelMesh> Meshes,
    string? Errors,
    ObjModelBounds Bounds);

public sealed record ObjModelMesh(IReadOnlyList<WorldVertex> Vertices, IReadOnlyList<int> Indices);

public sealed record ObjModelBounds(float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ)
{
    public static ObjModelBounds Empty { get; } = new(0, 0, 0, 0, 0, 0);
}

public static class ObjModelLoader
{
    public static ObjModelLoadResult Load(string text, IReadOnlyDictionary<int, string>? surfaceSkins = null)
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(text));
        return Load(stream, surfaceSkins);
    }

    public static ObjModelLoadResult Load(Stream stream, IReadOnlyDictionary<int, string>? surfaceSkins = null)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII);
        var result = new ObjModelBuilder();
        var vertices = new List<(float X, float Y, float Z)>();
        var normals = new List<(float X, float Y, float Z)>();
        var texcoords = new List<(float U, float V)>();
        var worldVertices = new List<WorldVertex>();
        var indices = new List<int>();

        string? line;
        int lineNumber = 1;
        while ((line = reader.ReadLine()) != null)
        {
            string[] fields = line.Trim().Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 0 || fields[0].Trim() == "#")
            {
                lineNumber++;
                continue;
            }

            string keyword = fields[0].Trim();
            string? payload = fields.Length == 2 ? fields[1].Trim() : null;

            switch (keyword)
            {
                case "v":
                    if (!TryParseVertex(payload, out var vertex, out string vertexMessage))
                        return result.Fail(lineNumber, vertexMessage);
                    vertices.Add(vertex);
                    break;

                case "vt":
                    if (!TryParseTextureCoords(payload, out var texcoord, out string texcoordMessage))
                        return result.Fail(lineNumber, texcoordMessage);
                    texcoords.Add(texcoord);
                    break;

                case "vn":
                    if (!TryParseNormal(payload, out var normal, out string normalMessage))
                        return result.Fail(lineNumber, normalMessage);
                    normals.Add(normal);
                    break;

                case "f":
                    if (!TryParseFace(payload, out var face, out var faceTexcoords, out var faceNormals, out string faceMessage))
                        return result.Fail(lineNumber, faceMessage);
                    if (!ValidateFace(lineNumber, face, vertices.Count, faceTexcoords, texcoords.Count, faceNormals, normals.Count, result, out var failure))
                        return failure;
                    AddFace(result, vertices, texcoords, normals, worldVertices, indices, face, faceTexcoords, faceNormals);
                    break;

                case "usemtl":
                    if (worldVertices.Count > 0)
                    {
                        result.AddMesh(worldVertices, indices);
                        worldVertices.Clear();
                        indices.Clear();
                    }

                    if (fields.Length >= 2)
                        result.Skins.Add(fields[1].Replace("\"", "", StringComparison.Ordinal));
                    break;

                case "":
                case "#":
                case "s":
                case "g":
                case "o":
                default:
                    break;
            }

            lineNumber++;
        }

        result.AddMesh(worldVertices, indices);
        ApplySurfaceSkins(result.Skins, surfaceSkins);
        return result.Build();
    }

    private static bool ValidateFace(
        int lineNumber,
        IReadOnlyList<int> face,
        int vertexCount,
        IReadOnlyList<int> texcoords,
        int texcoordCount,
        IReadOnlyList<int> normals,
        int normalCount,
        ObjModelBuilder result,
        out ObjModelLoadResult failure)
    {
        for (int i = 0; i < face.Count; i++)
            if (face[i] != -1 && face[i] >= vertexCount)
            {
                failure = result.Fail(lineNumber, $"vertex {face[i] + 1} does not exist");
                return false;
            }

        for (int i = 0; i < texcoords.Count; i++)
            if (texcoords[i] != -1 && texcoords[i] >= texcoordCount)
            {
                failure = result.Fail(lineNumber, $"texture coordinate {texcoords[i] + 1} does not exist");
                return false;
            }

        for (int i = 0; i < normals.Count; i++)
            if (normals[i] != -1 && normals[i] >= normalCount)
            {
                failure = result.Fail(lineNumber, $"vertex {normals[i] + 1} does not exist");
                return false;
            }

        failure = result.Build();
        return true;
    }

    private static void AddFace(
        ObjModelBuilder result,
        IReadOnlyList<(float X, float Y, float Z)> vertices,
        IReadOnlyList<(float U, float V)> texcoords,
        IReadOnlyList<(float X, float Y, float Z)> normals,
        List<WorldVertex> worldVertices,
        List<int> indices,
        IReadOnlyList<int> face,
        IReadOnlyList<int> faceTexcoords,
        IReadOnlyList<int> faceNormals)
    {
        int[] sequence = face.Count == 3 ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
        foreach (int sourceIndex in sequence)
        {
            var position = vertices[face[sourceIndex]];
            var vertex = new WorldVertex
            {
                x = position.X,
                y = position.Y,
                z = position.Z,
            };

            if (faceTexcoords[sourceIndex] != -1)
            {
                var texcoord = texcoords[faceTexcoords[sourceIndex]];
                vertex.u = texcoord.U;
                vertex.v = texcoord.V;
            }

            if (faceNormals[sourceIndex] != -1)
            {
                var normal = normals[faceNormals[sourceIndex]];
                vertex.nx = normal.X;
                vertex.ny = normal.Y;
                vertex.nz = normal.Z;
            }

            result.UpdateBounds(vertex);
            worldVertices.Add(vertex);
            indices.Add(indices.Count);
        }
    }

    private static void ApplySurfaceSkins(List<string> skins, IReadOnlyDictionary<int, string>? surfaceSkins)
    {
        if (surfaceSkins == null) return;
        foreach (var skin in surfaceSkins)
        {
            while (skins.Count <= skin.Key)
                skins.Add(string.Empty);

            skins[skin.Key] = skin.Value;
        }
    }

    private static bool TryParseVertex(string? payload, out (float X, float Y, float Z) vertex, out string message)
    {
        vertex = default;
        if (string.IsNullOrEmpty(payload))
        {
            message = "no arguments given";
            return false;
        }

        string[] fields = payload.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 3)
        {
            message = "too few arguments";
            return false;
        }

        if (!TryParseFloat(fields[0], out float x) || !TryParseFloat(fields[1], out float sourceY) || !TryParseFloat(fields[2], out float sourceZ))
        {
            message = "field is not a float";
            return false;
        }

        float doomY = -sourceZ;
        vertex = (doomY, -x, sourceY);
        message = "";
        return true;
    }

    private static bool TryParseTextureCoords(string? payload, out (float U, float V) texcoord, out string message)
    {
        texcoord = default;
        if (string.IsNullOrEmpty(payload))
        {
            message = "no arguments given";
            return false;
        }

        string[] fields = payload.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 2)
        {
            message = "too few arguments";
            return false;
        }

        if (!TryParseFloat(fields[0], out float u) || !TryParseFloat(fields[1], out float v))
        {
            message = "field is not a float";
            return false;
        }

        texcoord = (u, 1.0f - v);
        message = "";
        return true;
    }

    private static bool TryParseNormal(string? payload, out (float X, float Y, float Z) normal, out string message)
    {
        normal = default;
        if (string.IsNullOrEmpty(payload))
        {
            message = "no arguments given";
            return false;
        }

        string[] fields = payload.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 3)
        {
            message = "too few arguments";
            return false;
        }

        if (!TryParseFloat(fields[0], out float x) || !TryParseFloat(fields[1], out float y) || !TryParseFloat(fields[2], out float z))
        {
            message = "field is not a float";
            return false;
        }

        normal = (x, y, z);
        message = "";
        return true;
    }

    private static bool TryParseFace(
        string? payload,
        out List<int> face,
        out List<int> texcoords,
        out List<int> normals,
        out string message)
    {
        face = new List<int>();
        texcoords = new List<int>();
        normals = new List<int>();

        if (string.IsNullOrEmpty(payload))
        {
            message = "no arguments given";
            return false;
        }

        string[] fields = payload.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 3)
        {
            message = "too few arguments";
            return false;
        }

        if (fields.Length > 4)
        {
            message = "faces with more than 4 sides are not supported";
            return false;
        }

        foreach (string field in fields)
        {
            string[] vertexData = field.Split('/');
            if (!TryParseInt(vertexData[0], out int vertexIndex))
            {
                message = "field is not an integer";
                return false;
            }

            face.Add(vertexIndex - 1);

            if (vertexData.Length > 1 && vertexData[1] != "")
            {
                if (!TryParseInt(vertexData[1], out int texcoordIndex))
                {
                    message = "field is not an integer";
                    return false;
                }

                texcoords.Add(texcoordIndex - 1);
            }
            else
            {
                texcoords.Add(-1);
            }

            if (vertexData.Length > 2 && vertexData[2] != "")
            {
                if (!TryParseInt(vertexData[2], out int normalIndex))
                {
                    message = "field is not an integer";
                    return false;
                }

                normals.Add(normalIndex - 1);
            }
            else
            {
                normals.Add(-1);
            }
        }

        message = "";
        return true;
    }

    private static bool TryParseFloat(string value, out float result)
        => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static bool TryParseInt(string value, out int result)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private sealed class ObjModelBuilder
    {
        private readonly List<ObjModelMesh> meshes = new();
        private bool hasBounds;
        private float minX;
        private float minY;
        private float minZ;
        private float maxX;
        private float maxY;
        private float maxZ;

        public List<string> Skins { get; } = new();

        public void AddMesh(IReadOnlyList<WorldVertex> vertices, IReadOnlyList<int> indices)
            => meshes.Add(new ObjModelMesh(vertices.ToArray(), indices.ToArray()));

        public void UpdateBounds(WorldVertex vertex)
        {
            if (!hasBounds)
            {
                minX = maxX = vertex.x;
                minY = maxY = vertex.y;
                minZ = maxZ = vertex.z;
                hasBounds = true;
                return;
            }

            minX = Math.Min(minX, vertex.x);
            minY = Math.Min(minY, vertex.y);
            minZ = Math.Min(minZ, vertex.z);
            maxX = Math.Max(maxX, vertex.x);
            maxY = Math.Max(maxY, vertex.y);
            maxZ = Math.Max(maxZ, vertex.z);
        }

        public ObjModelLoadResult Fail(int lineNumber, string message)
            => Build($"Error in line {lineNumber}: {message}");

        public ObjModelLoadResult Build(string? errors = null)
            => new(Skins.ToArray(), meshes.ToArray(), errors, hasBounds ? new ObjModelBounds(minX, minY, minZ, maxX, maxY, maxZ) : ObjModelBounds.Empty);
    }
}
