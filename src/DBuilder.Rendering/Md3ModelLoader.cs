// ABOUTME: Loads Quake III MD3 model frames into UDB-style world vertices, indices, and skin groups.
// ABOUTME: Preserves UDB's MD3 header validation, frame selection, surface skins, normals, and errors.

using System.Text;

namespace DBuilder.Rendering;

public sealed record Md3ModelLoadResult(
    IReadOnlyList<string> Skins,
    IReadOnlyList<GzModelMesh> Meshes,
    string? Errors,
    GzModelBounds Bounds);

public static class Md3ModelLoader
{
    public static Md3ModelLoadResult Load(byte[] bytes, IReadOnlyDictionary<int, string>? surfaceSkins = null, int frame = 0)
    {
        using var stream = new MemoryStream(bytes);
        return Load(stream, surfaceSkins, frame);
    }

    public static Md3ModelLoadResult Load(Stream stream, IReadOnlyDictionary<int, string>? surfaceSkins = null, int frame = 0)
    {
        long start = stream.Position;
        using var reader = new BinaryReader(stream, Encoding.ASCII);
        var builder = new Md3ModelBuilder();

        string magic = ReadString(reader, 4);
        if (magic != "IDP3")
            return builder.Fail($"unknown header: expected \"IDP3\", but got \"{magic}\"");

        int modelVersion = reader.ReadInt32();
        if (modelVersion != 15)
            return builder.Fail("expected MD3 version 15, but got " + modelVersion);

        stream.Position += 76;
        int surfaceCount = reader.ReadInt32();
        stream.Position += 12;
        int surfaceOffset = reader.ReadInt32();
        stream.Position = start + surfaceOffset;

        var combinedIndices = new List<int>();
        var combinedVertices = new List<WorldVertex>();
        var groupedIndices = new Dictionary<string, List<List<int>>>(StringComparer.Ordinal);
        var groupedVertices = new Dictionary<string, List<WorldVertex>>(StringComparer.Ordinal);
        var groupedVertexCounts = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        bool useSkins = false;

        for (int surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
        {
            string skin = "";
            var surfaceIndices = new List<int>();
            var surfaceVertices = new List<WorldVertex>();
            string error = ReadSurface(builder, ref skin, reader, surfaceIndices, surfaceVertices, frame);
            if (!string.IsNullOrEmpty(error))
                return builder.Fail(error);

            if (surfaceSkins != null && surfaceSkins.TryGetValue(surfaceIndex, out string? overrideSkin))
                skin = overrideSkin;

            if (!string.IsNullOrEmpty(skin))
            {
                useSkins = true;
                if (!groupedIndices.TryGetValue(skin, out var skinIndices))
                {
                    skinIndices = new List<List<int>>();
                    groupedIndices[skin] = skinIndices;
                    groupedVertices[skin] = new List<WorldVertex>();
                    groupedVertexCounts[skin] = new List<int>();
                }

                skinIndices.Add(surfaceIndices);
                groupedVertices[skin].AddRange(surfaceVertices);
                groupedVertexCounts[skin].Add(surfaceVertices.Count);
            }
            else
            {
                int vertexOffset = combinedVertices.Count;
                combinedVertices.AddRange(surfaceVertices);
                foreach (int index in surfaceIndices)
                    combinedIndices.Add(index + vertexOffset);
            }
        }

        if (!useSkins)
        {
            builder.AddMesh(combinedVertices, combinedIndices);
            builder.Skins.Add("");
        }
        else
        {
            foreach (var group in groupedIndices)
            {
                var indices = new List<int>();
                int offset = 0;
                for (int i = 0; i < group.Value.Count; i++)
                {
                    if (i > 0)
                        offset += groupedVertexCounts[group.Key][i - 1];

                    foreach (int index in group.Value[i])
                        indices.Add(index + offset);
                }

                builder.AddMesh(groupedVertices[group.Key], indices);
                builder.Skins.Add(group.Key.ToLowerInvariant());
            }
        }

        return builder.Build();
    }

    private static string ReadSurface(
        Md3ModelBuilder builder,
        ref string skin,
        BinaryReader reader,
        List<int> indices,
        List<WorldVertex> vertices,
        int frame)
    {
        long start = reader.BaseStream.Position;
        string magic = ReadString(reader, 4);
        if (magic != "IDP3")
            return $"error while reading surface. Unknown header: expected \"IDP3\", but got \"{magic}\"";

        _ = ReadString(reader, 64);
        _ = reader.ReadInt32();
        int frameCount = reader.ReadInt32();
        _ = reader.ReadInt32();
        int vertexCount = reader.ReadInt32();
        int triangleCount = reader.ReadInt32();
        int triangleOffset = reader.ReadInt32();
        int shaderOffset = reader.ReadInt32();
        int stOffset = reader.ReadInt32();
        int normalOffset = reader.ReadInt32();
        int endOffset = reader.ReadInt32();

        if (frame < 0 || frame >= frameCount)
            return $"frame {frame} is outside of model's frame range [0..{frameCount - 1}]";

        reader.BaseStream.Position = start + triangleOffset;
        for (int i = 0; i < triangleCount * 3; i++)
            indices.Add(reader.ReadInt32());

        reader.BaseStream.Position = start + shaderOffset;
        skin = ReadString(reader, 64);

        reader.BaseStream.Position = start + stOffset;
        for (int i = 0; i < vertexCount; i++)
        {
            vertices.Add(new WorldVertex
            {
                c = -1,
                u = reader.ReadSingle(),
                v = reader.ReadSingle(),
            });
        }

        long frameVertexOffset = start + normalOffset + vertexCount * 8L * frame;
        reader.BaseStream.Position = frameVertexOffset;
        for (int i = 0; i < vertexCount; i++)
        {
            WorldVertex vertex = vertices[i];
            vertex.y = -(float)reader.ReadInt16() / 64.0f;
            vertex.x = (float)reader.ReadInt16() / 64.0f;
            vertex.z = (float)reader.ReadInt16() / 64.0f;
            builder.UpdateBounds(vertex);

            double lat = reader.ReadByte() * (2 * Math.PI) / 255.0;
            double lng = reader.ReadByte() * (2 * Math.PI) / 255.0;
            vertex.nx = (float)(Math.Sin(lng) * Math.Sin(lat));
            vertex.ny = -(float)(Math.Cos(lng) * Math.Sin(lat));
            vertex.nz = (float)Math.Cos(lat);
            vertices[i] = vertex;
        }

        reader.BaseStream.Position = start + endOffset;
        return "";
    }

    private static string ReadString(BinaryReader reader, int length)
    {
        string result = "";
        int i;
        for (i = 0; i < length; i++)
        {
            char c = reader.ReadChar();
            if (c == '\0')
            {
                i++;
                break;
            }

            result += c;
        }

        for (; i < length; i++)
            reader.ReadChar();

        return result;
    }

    private sealed class Md3ModelBuilder
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

        public Md3ModelLoadResult Fail(string errors)
            => Build(errors);

        public Md3ModelLoadResult Build(string? errors = null)
            => new(Skins.ToArray(), meshes.ToArray(), errors, hasBounds ? new GzModelBounds(minX, minY, minZ, maxX, maxY, maxZ) : GzModelBounds.Empty);
    }
}
