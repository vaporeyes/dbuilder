// ABOUTME: Loads Quake II MD2 model frames into UDB-style world vertices and triangle indices.
// ABOUTME: Preserves UDB's header validation, frame selection, coordinate rotation, UV handling, and errors.

using System.Text;

namespace DBuilder.Rendering;

public sealed record Md2ModelLoadResult(
    IReadOnlyList<string> Skins,
    IReadOnlyList<GzModelMesh> Meshes,
    string? Errors,
    GzModelBounds Bounds);

public static class Md2ModelLoader
{
    public static Md2ModelLoadResult Load(byte[] bytes, int frame = 0, string frameName = "")
    {
        using var stream = new MemoryStream(bytes);
        return Load(stream, frame, frameName);
    }

    public static Md2ModelLoadResult Load(Stream stream, int frame = 0, string frameName = "")
    {
        long start = stream.Position;
        using var reader = new BinaryReader(stream, Encoding.ASCII);
        var builder = new Md2ModelBuilder();

        string magic = ReadString(reader, 4);
        if (magic != "IDP2")
            return builder.Fail($"unknown header: expected \"IDP2\", but got \"{magic}\"");

        int modelVersion = reader.ReadInt32();
        if (modelVersion != 8)
            return builder.Fail("expected MD3 version 15, but got " + modelVersion);

        int textureWidth = reader.ReadInt32();
        int textureHeight = reader.ReadInt32();
        int frameSize = reader.ReadInt32();
        stream.Position += 4;
        int vertexCount = reader.ReadInt32();
        int uvCount = reader.ReadInt32();
        int triangleCount = reader.ReadInt32();
        stream.Position += 4;
        int frameCount = reader.ReadInt32();

        if (frame < 0 || frame >= frameCount)
            return builder.Fail($"frame {frame} is outside of model's frame range [0..{frameCount - 1}]");

        stream.Position += 4;
        int uvOffset = reader.ReadInt32();
        int triangleOffset = reader.ReadInt32();
        int frameOffset = reader.ReadInt32();

        var triangleVertexIndices = new List<int>(triangleCount * 3);
        var triangleUvIndices = new List<int>(triangleCount * 3);
        var uvCoords = new List<(float U, float V)>(uvCount);
        var vertices = new List<WorldVertex>(vertexCount);

        stream.Position = start + triangleOffset;
        for (int i = 0; i < triangleCount; i++)
        {
            triangleVertexIndices.Add(reader.ReadUInt16());
            triangleVertexIndices.Add(reader.ReadUInt16());
            triangleVertexIndices.Add(reader.ReadUInt16());
            triangleUvIndices.Add(reader.ReadUInt16());
            triangleUvIndices.Add(reader.ReadUInt16());
            triangleUvIndices.Add(reader.ReadUInt16());
        }

        stream.Position = start + uvOffset;
        for (int i = 0; i < uvCount; i++)
            uvCoords.Add(((float)reader.ReadInt16() / textureWidth, (float)reader.ReadInt16() / textureHeight));

        if (!string.IsNullOrEmpty(frameName))
        {
            bool frameFound = false;
            for (int i = 0; i < frameCount; i++)
            {
                stream.Position = start + frameOffset + i * frameSize + 24;
                string currentFrameName = ReadString(reader, 16).ToLowerInvariant();
                if (currentFrameName == frameName)
                {
                    stream.Position -= 40;
                    frameFound = true;
                    break;
                }
            }

            if (!frameFound)
                return builder.Fail($"unable to find frame \"{frameName}\"!");
        }
        else
        {
            stream.Position = start + frameOffset + frame * frameSize;
        }

        float scaleX = reader.ReadSingle();
        float scaleY = reader.ReadSingle();
        float scaleZ = reader.ReadSingle();
        float translateX = reader.ReadSingle();
        float translateY = reader.ReadSingle();
        float translateZ = reader.ReadSingle();

        stream.Position += 16;

        for (int i = 0; i < vertexCount; i++)
        {
            float x = reader.ReadByte() * scaleX + translateX;
            float y = reader.ReadByte() * scaleY + translateY;
            float z = reader.ReadByte() * scaleZ + translateZ;
            vertices.Add(new WorldVertex
            {
                x = y,
                y = -x,
                z = z,
            });
            stream.Position += 1;
        }

        for (int i = 0; i < triangleVertexIndices.Count; i++)
        {
            int vertexIndex = triangleVertexIndices[i];
            WorldVertex vertex = vertices[vertexIndex];
            builder.UpdateBounds(vertex);

            var uv = uvCoords[triangleUvIndices[i]];
            if (vertex.c == -1 && (vertex.u != uv.U || vertex.v != uv.V))
            {
                vertices.Add(new WorldVertex
                {
                    x = vertex.x,
                    y = vertex.y,
                    z = vertex.z,
                    c = -1,
                    u = uv.U,
                    v = uv.V,
                });
                triangleVertexIndices[i] = vertices.Count - 1;
            }
            else
            {
                vertex.u = uv.U;
                vertex.v = uv.V;
                vertex.c = -1;
                vertices[vertexIndex] = vertex;
            }
        }

        builder.AddMesh(vertices, triangleVertexIndices);
        builder.Skins.Add("");
        return builder.Build();
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

    private sealed class Md2ModelBuilder
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

        public Md2ModelLoadResult Fail(string errors)
            => Build(errors);

        public Md2ModelLoadResult Build(string? errors = null)
            => new(Skins.ToArray(), meshes.ToArray(), errors, hasBounds ? new GzModelBounds(minX, minY, minZ, maxX, maxY, maxZ) : GzModelBounds.Empty);
    }
}
