// ABOUTME: Loads Unreal Engine 1 _a.3d and _d.3d model data into UDB-style meshes.
// ABOUTME: Preserves UDB's frame selection, packed and Deus Ex vertices, texture groups, UVs, and errors.

using System.Text;

namespace DBuilder.Rendering;

public sealed record UnrealModelLoadResult(
    IReadOnlyList<string> Skins,
    IReadOnlyList<GzModelMesh> Meshes,
    string? Errors,
    GzModelBounds Bounds);

public static class UnrealModelLoader
{
    public static UnrealModelLoadResult Load(
        byte[] animationBytes,
        byte[] dataBytes,
        int frame = 0,
        IReadOnlyDictionary<int, string>? skins = null)
    {
        using var animationStream = new MemoryStream(animationBytes);
        using var dataStream = new MemoryStream(dataBytes);
        return Load(animationStream, dataStream, frame, skins);
    }

    public static UnrealModelLoadResult Load(
        Stream animationStream,
        Stream dataStream,
        int frame = 0,
        IReadOnlyDictionary<int, string>? skins = null)
    {
        using var animationReader = new BinaryReader(animationStream, Encoding.ASCII);
        using var dataReader = new BinaryReader(dataStream, Encoding.ASCII);
        var builder = new UnrealModelBuilder();

        ushort polygonCount = dataReader.ReadUInt16();
        ushort vertexCount = dataReader.ReadUInt16();
        dataStream.Position += 44;
        long dataStart = dataStream.Position;

        ushort frameCount = animationReader.ReadUInt16();
        ushort frameSize = animationReader.ReadUInt16();
        long animationStart = animationStream.Position;

        if (frame < 0 || frame >= frameCount)
            return builder.Fail($"frame {frame} is outside of model's frame range [0..{frameCount - 1}]");

        bool isDeusEx = frameSize / vertexCount == 8;
        var vertices = new WorldVertex[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i] = isDeusEx
                ? ReadDeusExVertex(animationReader, animationStream, animationStart, frame, vertexCount, i)
                : ReadPackedVertex(animationReader, animationStream, animationStart, frame, vertexCount, i);
        }

        var polygons = new UnrealPolygon[polygonCount];
        for (int i = 0; i < polygonCount; i++)
        {
            dataStream.Position = dataStart + 16 * i;
            int a = dataReader.ReadInt16();
            int b = dataReader.ReadInt16();
            int c = dataReader.ReadInt16();
            if (a >= vertices.Length || a < 0 || b >= vertices.Length || b < 0 || c >= vertices.Length || c < 0)
                a = b = c = 0;

            int type = dataReader.ReadByte();
            dataStream.Position += 1;
            float s0 = dataReader.ReadByte() / 255f;
            float t0 = dataReader.ReadByte() / 255f;
            float s1 = dataReader.ReadByte() / 255f;
            float t1 = dataReader.ReadByte() / 255f;
            float s2 = dataReader.ReadByte() / 255f;
            float t2 = dataReader.ReadByte() / 255f;
            int textureNumber = dataReader.ReadByte();
            polygons[i] = new UnrealPolygon(a, b, c, type, textureNumber, s0, t0, s1, t1, s2, t2);
        }

        var polygonNormals = new UnrealVector[polygonCount];
        for (int i = 0; i < polygonCount; i++)
            polygonNormals[i] = CalculatePolygonNormal(polygons[i], vertices);

        var vertexNormals = new UnrealVector[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            var sum = new UnrealVector(0, 0, 0);
            int total = 0;
            for (int j = 0; j < polygons.Length; j++)
            {
                if (polygons[j].A != i && polygons[j].B != i && polygons[j].C != i) continue;
                sum = new UnrealVector(sum.X + polygonNormals[j].X, sum.Y + polygonNormals[j].Y, sum.Z + polygonNormals[j].Z);
                total++;
            }

            if (total > 0)
                vertexNormals[i] = new UnrealVector(-sum.X / total, -sum.Y / total, -sum.Z / total);
        }

        List<int> textureGroups = BuildTextureGroups(polygons);
        var textureGroupRemap = new Dictionary<int, int>();
        for (int i = 0; i < textureGroups.Count; i++)
            textureGroupRemap[textureGroups[i]] = i;

        if (skins == null)
        {
            AddPolygons(builder, polygons, vertices, vertexNormals, polygonNormals, _ => true);
            builder.Skins.Add("");
        }
        else
        {
            for (int groupIndex = 0; groupIndex < textureGroups.Count; groupIndex++)
            {
                int currentGroup = groupIndex;
                AddPolygons(builder, polygons, vertices, vertexNormals, polygonNormals, polygon => textureGroupRemap[polygon.TextureNumber] == currentGroup);
                builder.Skins.Add(skins.TryGetValue(groupIndex, out string? skin) ? skin.ToLowerInvariant() : string.Empty);
            }
        }

        return builder.Build();
    }

    private static WorldVertex ReadPackedVertex(BinaryReader reader, Stream stream, long start, int frame, int vertexCount, int index)
    {
        stream.Position = start + (index + frame * vertexCount) * 4;
        int packed = reader.ReadInt32();
        return new WorldVertex
        {
            y = -UnpackVertex(packed, 0),
            z = UnpackVertex(packed, 2),
            x = -UnpackVertex(packed, 1),
        };
    }

    private static WorldVertex ReadDeusExVertex(BinaryReader reader, Stream stream, long start, int frame, int vertexCount, int index)
    {
        stream.Position = start + (index + frame * vertexCount) * 8;
        int x = reader.ReadInt16();
        int y = reader.ReadInt16();
        int z = reader.ReadInt16();
        return new WorldVertex
        {
            y = -x,
            z = z,
            x = -y,
        };
    }

    private static List<int> BuildTextureGroups(IReadOnlyList<UnrealPolygon> polygons)
    {
        var groups = new List<int>();
        foreach (UnrealPolygon polygon in polygons)
        {
            if (groups.Contains(polygon.TextureNumber))
                continue;
            if (groups.Count == 0 || polygon.TextureNumber <= groups[0])
                groups.Insert(0, polygon.TextureNumber);
            else if (groups.Count == 0 || polygon.TextureNumber >= groups[^1])
                groups.Add(polygon.TextureNumber);
        }

        return groups;
    }

    private static void AddPolygons(
        UnrealModelBuilder builder,
        IReadOnlyList<UnrealPolygon> polygons,
        IReadOnlyList<WorldVertex> vertices,
        IReadOnlyList<UnrealVector> vertexNormals,
        IReadOnlyList<UnrealVector> polygonNormals,
        Func<UnrealPolygon, bool> include)
    {
        var meshVertices = new List<WorldVertex>();
        var meshIndices = new List<int>();

        for (int i = 0; i < polygons.Count; i++)
        {
            UnrealPolygon polygon = polygons[i];
            if ((polygon.Type & 0x08) != 0 || !include(polygon))
                continue;

            AddVertex(builder, meshVertices, meshIndices, vertices[polygon.A], polygon.S0, polygon.T0, NormalFor(polygon, polygonNormals[i], vertexNormals[polygon.A]));
            AddVertex(builder, meshVertices, meshIndices, vertices[polygon.B], polygon.S1, polygon.T1, NormalFor(polygon, polygonNormals[i], vertexNormals[polygon.B]));
            AddVertex(builder, meshVertices, meshIndices, vertices[polygon.C], polygon.S2, polygon.T2, NormalFor(polygon, polygonNormals[i], vertexNormals[polygon.C]));
        }

        builder.AddMesh(meshVertices, meshIndices);
    }

    private static void AddVertex(
        UnrealModelBuilder builder,
        List<WorldVertex> vertices,
        List<int> indices,
        WorldVertex source,
        float u,
        float v,
        UnrealVector normal)
    {
        source.u = u;
        source.v = v;
        source.nx = normal.X;
        source.ny = normal.Y;
        source.nz = normal.Z;
        builder.UpdateBounds(source);
        indices.Add(vertices.Count);
        vertices.Add(source);
    }

    private static UnrealVector NormalFor(UnrealPolygon polygon, UnrealVector polygonNormal, UnrealVector vertexNormal)
        => (polygon.Type & 0x20) != 0 ? polygonNormal : vertexNormal;

    private static UnrealVector CalculatePolygonNormal(UnrealPolygon polygon, IReadOnlyList<WorldVertex> vertices)
    {
        WorldVertex v0 = vertices[polygon.A];
        WorldVertex v1 = vertices[polygon.B];
        WorldVertex v2 = vertices[polygon.C];
        var first = new UnrealVector(v1.x - v0.x, v1.y - v0.y, v1.z - v0.z);
        var second = new UnrealVector(v2.x - v0.x, v2.y - v0.y, v2.z - v0.z);
        return Normalize(new UnrealVector(
            first.Y * second.Z - first.Z * second.Y,
            first.Z * second.X - first.X * second.Z,
            first.X * second.Y - first.Y * second.X));
    }

    private static UnrealVector Normalize(UnrealVector vector)
    {
        float length = MathF.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
        return length == 0 ? new UnrealVector(0, 0, 0) : new UnrealVector(vector.X / length, vector.Y / length, vector.Z / length);
    }

    private static int PadInt16(int value)
        => value > 32767 ? -(65536 - value) : value;

    private static float UnpackVertex(int packed, int component)
    {
        return component switch
        {
            0 => PadInt16((packed & 0x7ff) << 5) / 128f,
            1 => PadInt16((packed >> 11 & 0x7ff) << 5) / 128f,
            2 => PadInt16((packed >> 22 & 0x3ff) << 6) / 128f,
            _ => 0f,
        };
    }

    private sealed record UnrealPolygon(
        int A,
        int B,
        int C,
        int Type,
        int TextureNumber,
        float S0,
        float T0,
        float S1,
        float T1,
        float S2,
        float T2);

    private readonly record struct UnrealVector(float X, float Y, float Z);

    private sealed class UnrealModelBuilder
    {
        private readonly List<GzModelMesh> meshes = new();
        private bool hasBounds;
        private float minX;
        private float minY;
        private float minZ;
        private float maxX;
        private float maxY;
        private float maxZ;

        public List<string> Skins { get; } = new();

        public void AddMesh(IReadOnlyList<WorldVertex> vertices, IReadOnlyList<int> indices)
            => meshes.Add(new GzModelMesh(vertices.ToArray(), indices.ToArray()));

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

        public UnrealModelLoadResult Fail(string errors)
            => Build(errors);

        public UnrealModelLoadResult Build(string? errors = null)
            => new(Skins.ToArray(), meshes.ToArray(), errors, hasBounds ? new GzModelBounds(minX, minY, minZ, maxX, maxY, maxZ) : GzModelBounds.Empty);
    }
}
