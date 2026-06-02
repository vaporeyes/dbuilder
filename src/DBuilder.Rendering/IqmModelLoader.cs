// ABOUTME: Loads static Inter-Quake Model IQM geometry into UDB-style world vertices and material meshes.
// ABOUTME: Preserves UDB's IQM header validation, material names, vertex arrays, coordinate rotation, and errors.

using System.Text;

namespace DBuilder.Rendering;

public sealed record IqmModelLoadResult(
    IReadOnlyList<string> Skins,
    IReadOnlyList<GzModelMesh> Meshes,
    string? Errors,
    GzModelBounds Bounds);

public static class IqmModelLoader
{
    public static IqmModelLoadResult Load(byte[] bytes, int frame = 0)
    {
        using var stream = new MemoryStream(bytes);
        return Load(stream, frame);
    }

    public static IqmModelLoadResult Load(Stream stream, int frame = 0)
    {
        try
        {
            using var reader = new IqmReader(stream);
            if (!reader.ReadBytes(16).SequenceEqual(Encoding.ASCII.GetBytes("INTERQUAKEMODEL\0")))
                throw new InvalidDataException("Not an IQM file!");

            uint version = reader.ReadUInt32();
            if (version != 2)
                throw new InvalidDataException("Unsupported IQM version");

            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            uint textCount = reader.ReadUInt32();
            uint textOffset = reader.ReadUInt32();
            uint meshCount = reader.ReadUInt32();
            uint meshOffset = reader.ReadUInt32();
            uint vertexArrayCount = reader.ReadUInt32();
            uint vertexCount = reader.ReadUInt32();
            uint vertexArrayOffset = reader.ReadUInt32();
            uint triangleCount = reader.ReadUInt32();
            uint triangleOffset = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            uint frameCount = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();

            if (textCount == 0)
                throw new InvalidDataException("IQM model needs material names");

            reader.SeekTo(textOffset);
            byte[] text = reader.ReadBytes((int)textCount);
            text[^1] = 0;

            var meshes = new List<IqmMesh>((int)meshCount);
            reader.SeekTo(meshOffset);
            for (int i = 0; i < meshCount; i++)
            {
                meshes.Add(new IqmMesh(
                    reader.ReadName(text),
                    reader.ReadName(text),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32()));
            }

            var indices = new int[triangleCount * 3];
            reader.SeekTo(triangleOffset);
            for (int i = 0; i < indices.Length; i++)
                indices[i] = reader.ReadInt32();

            var vertexArrays = new List<IqmVertexArray>((int)vertexArrayCount);
            reader.SeekTo(vertexArrayOffset);
            for (int i = 0; i < vertexArrayCount; i++)
            {
                vertexArrays.Add(new IqmVertexArray(
                    (IqmVertexArrayType)reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    (IqmVertexArrayFormat)reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32()));
            }

            var vertices = new IqmVertex[vertexCount];
            foreach (IqmVertexArray vertexArray in vertexArrays)
            {
                reader.SeekTo(vertexArray.Offset);
                switch (vertexArray.Type)
                {
                    case IqmVertexArrayType.Position:
                        LoadPositions(reader, vertexArray, vertices);
                        break;
                    case IqmVertexArrayType.Texcoord:
                        LoadTexcoords(reader, vertexArray, vertices);
                        break;
                    case IqmVertexArrayType.Normal:
                        LoadNormals(reader, vertexArray, vertices);
                        break;
                }
            }

            uint effectiveFrameCount = frameCount == 0 ? 1u : frameCount;
            if (frame >= effectiveFrameCount)
                frame = 0;

            var worldVertices = new WorldVertex[vertexCount];
            var builder = new IqmModelBuilder();
            for (int i = 0; i < vertices.Length; i++)
            {
                IqmVertex vertex = vertices[i];
                var world = new WorldVertex
                {
                    x = vertex.Y,
                    y = -vertex.X,
                    z = vertex.Z,
                    nx = vertex.NormalX,
                    ny = vertex.NormalY,
                    nz = vertex.NormalZ,
                    u = vertex.U,
                    v = vertex.V,
                    c = -1,
                };
                worldVertices[i] = world;
                builder.UpdateBounds(world);
            }

            foreach (IqmMesh mesh in meshes)
            {
                var meshVertices = new WorldVertex[mesh.VertexCount];
                var meshIndices = new int[mesh.TriangleCount * 3];
                for (uint i = 0; i < mesh.VertexCount; i++)
                    meshVertices[i] = worldVertices[mesh.FirstVertex + i];

                uint firstIndex = mesh.FirstTriangle * 3;
                for (uint i = 0; i < mesh.TriangleCount * 3; i++)
                    meshIndices[i] = indices[firstIndex + i] - (int)mesh.FirstVertex;

                builder.AddMesh(meshVertices, meshIndices);
                builder.Skins.Add(mesh.Material);
            }

            return builder.Build();
        }
        catch (Exception ex)
        {
            return IqmModelBuilder.Empty(ex.Message);
        }
    }

    private static void LoadPositions(IqmReader reader, IqmVertexArray vertexArray, IqmVertex[] vertices)
    {
        if (vertexArray.Format != IqmVertexArrayFormat.Float || vertexArray.Size != 3)
            throw new InvalidDataException("Unsupported IQM_POSITION vertex format");

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].X = reader.ReadSingle();
            vertices[i].Y = reader.ReadSingle();
            vertices[i].Z = reader.ReadSingle();
        }
    }

    private static void LoadTexcoords(IqmReader reader, IqmVertexArray vertexArray, IqmVertex[] vertices)
    {
        if (vertexArray.Format != IqmVertexArrayFormat.Float || vertexArray.Size != 2)
            throw new InvalidDataException("Unsupported IQM_TEXCOORD vertex format");

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].U = reader.ReadSingle();
            vertices[i].V = reader.ReadSingle();
        }
    }

    private static void LoadNormals(IqmReader reader, IqmVertexArray vertexArray, IqmVertex[] vertices)
    {
        if (vertexArray.Format != IqmVertexArrayFormat.Float || vertexArray.Size != 3)
            throw new InvalidDataException("Unsupported IQM_NORMAL vertex format");

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].NormalX = reader.ReadSingle();
            vertices[i].NormalY = reader.ReadSingle();
            vertices[i].NormalZ = reader.ReadSingle();
        }
    }

    private sealed record IqmMesh(string Name, string Material, uint FirstVertex, uint VertexCount, uint FirstTriangle, uint TriangleCount);

    private sealed record IqmVertexArray(IqmVertexArrayType Type, uint Flags, IqmVertexArrayFormat Format, uint Size, uint Offset);

    private enum IqmVertexArrayType
    {
        Position = 0,
        Texcoord = 1,
        Normal = 2,
    }

    private enum IqmVertexArrayFormat
    {
        Float = 7,
    }

    private struct IqmVertex
    {
        public float X;
        public float Y;
        public float Z;
        public float NormalX;
        public float NormalY;
        public float NormalZ;
        public float U;
        public float V;
    }

    private sealed class IqmReader : BinaryReader
    {
        public IqmReader(Stream stream) : base(stream)
        {
        }

        public string ReadName(byte[] textBuffer)
        {
            uint nameOffset = ReadUInt32();
            if (nameOffset >= textBuffer.Length)
                throw new InvalidDataException("Name offset out of bounds");

            for (uint i = nameOffset; i < textBuffer.Length; i++)
                if (textBuffer[i] == 0)
                    return Encoding.ASCII.GetString(textBuffer, (int)nameOffset, (int)(i - nameOffset));

            throw new InvalidDataException("Name not null terminated");
        }

        public void SeekTo(uint offset)
            => BaseStream.Seek(offset, SeekOrigin.Begin);
    }

    private sealed class IqmModelBuilder
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

        public static IqmModelLoadResult Empty(string errors)
            => new(Array.Empty<string>(), Array.Empty<GzModelMesh>(), errors, GzModelBounds.Empty);

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

        public IqmModelLoadResult Build()
            => new(Skins.ToArray(), meshes.ToArray(), null, hasBounds ? new GzModelBounds(minX, minY, minZ, maxX, maxY, maxZ) : GzModelBounds.Empty);
    }
}
