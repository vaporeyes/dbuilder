// ABOUTME: Tests UDB-style static IQM model loading from binary model data.
// ABOUTME: Verifies material meshes, coordinate rotation, vertex arrays, local indices, and loader errors.

using System.Text;
using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class IqmModelLoaderTests
{
    [Fact]
    public void LoadsStaticMeshesWithUdbCoordinateRotationAndMaterials()
    {
        byte[] iqm = BuildIqm(
            vertices: new[]
            {
                (X: 1.0f, Y: 2.0f, Z: 3.0f),
                (X: 4.0f, Y: 5.0f, Z: 6.0f),
                (X: 7.0f, Y: 8.0f, Z: 9.0f),
                (X: 10.0f, Y: 11.0f, Z: 12.0f),
                (X: 13.0f, Y: 14.0f, Z: 15.0f),
                (X: 16.0f, Y: 17.0f, Z: 18.0f),
            },
            texcoords: new[]
            {
                (0.0f, 0.0f), (0.5f, 0.0f), (1.0f, 0.0f),
                (0.0f, 1.0f), (0.5f, 1.0f), (1.0f, 1.0f),
            },
            normals: new[]
            {
                (0.0f, 0.0f, 1.0f), (0.0f, 0.0f, 1.0f), (0.0f, 0.0f, 1.0f),
                (0.0f, 1.0f, 0.0f), (0.0f, 1.0f, 0.0f), (0.0f, 1.0f, 0.0f),
            },
            triangles: new[] { (0, 1, 2), (3, 4, 5) },
            meshes: new[]
            {
                new IqmMeshSpec("body", "textures/body.png", 0, 3, 0, 1),
                new IqmMeshSpec("head", "textures/head.png", 3, 3, 1, 1),
            });

        IqmModelLoadResult result = IqmModelLoader.Load(iqm);

        Assert.Null(result.Errors);
        Assert.Equal(new[] { "textures/body.png", "textures/head.png" }, result.Skins);
        Assert.Equal(2, result.Meshes.Count);
        GzModelMesh firstMesh = result.Meshes[0];
        Assert.Equal(new[] { 0, 1, 2 }, firstMesh.Indices);
        WorldVertex first = firstMesh.Vertices[0];
        Assert.Equal(2.0f, first.x);
        Assert.Equal(-1.0f, first.y);
        Assert.Equal(3.0f, first.z);
        Assert.Equal(0.0f, first.u);
        Assert.Equal(0.0f, first.v);
        Assert.Equal(0.0f, first.nx);
        Assert.Equal(0.0f, first.ny);
        Assert.Equal(1.0f, first.nz);
        Assert.Equal(17.0f, result.Bounds.MaxX);
        Assert.Equal(-1.0f, result.Bounds.MaxY);
        Assert.Equal(18.0f, result.Bounds.MaxZ);
        Assert.Equal(new[] { 0, 1, 2 }, result.Meshes[1].Indices);
    }

    [Fact]
    public void ReportsUdbStyleHeaderMaterialAndVertexArrayErrors()
    {
        byte[] iqm = BuildIqm(
            vertices: new[] { (1.0f, 2.0f, 3.0f), (4.0f, 5.0f, 6.0f), (7.0f, 8.0f, 9.0f) },
            texcoords: new[] { (0.0f, 0.0f), (0.5f, 0.0f), (1.0f, 0.0f) },
            normals: new[] { (0.0f, 0.0f, 1.0f), (0.0f, 0.0f, 1.0f), (0.0f, 0.0f, 1.0f) },
            triangles: new[] { (0, 1, 2) },
            meshes: new[] { new IqmMeshSpec("body", "textures/body.png", 0, 3, 0, 1) });

        byte[] badMagic = (byte[])iqm.Clone();
        badMagic[0] = (byte)'X';
        Assert.Equal("Not an IQM file!", IqmModelLoader.Load(badMagic).Errors);

        byte[] badVersion = (byte[])iqm.Clone();
        BitConverter.GetBytes(1u).CopyTo(badVersion, 16);
        Assert.Equal("Unsupported IQM version", IqmModelLoader.Load(badVersion).Errors);

        byte[] missingText = BuildIqm(
            vertices: new[] { (1.0f, 2.0f, 3.0f) },
            texcoords: new[] { (0.0f, 0.0f) },
            normals: new[] { (0.0f, 0.0f, 1.0f) },
            triangles: Array.Empty<(int, int, int)>(),
            meshes: Array.Empty<IqmMeshSpec>(),
            includeText: false);
        Assert.Equal("IQM model needs material names", IqmModelLoader.Load(missingText).Errors);

        byte[] badTexcoordFormat = BuildIqm(
            vertices: new[] { (1.0f, 2.0f, 3.0f), (4.0f, 5.0f, 6.0f), (7.0f, 8.0f, 9.0f) },
            texcoords: new[] { (0.0f, 0.0f), (0.5f, 0.0f), (1.0f, 0.0f) },
            normals: new[] { (0.0f, 0.0f, 1.0f), (0.0f, 0.0f, 1.0f), (0.0f, 0.0f, 1.0f) },
            triangles: new[] { (0, 1, 2) },
            meshes: new[] { new IqmMeshSpec("body", "textures/body.png", 0, 3, 0, 1) },
            texcoordSize: 3);
        Assert.Equal("Unsupported IQM_TEXCOORD vertex format", IqmModelLoader.Load(badTexcoordFormat).Errors);
    }

    private static byte[] BuildIqm(
        IReadOnlyList<(float X, float Y, float Z)> vertices,
        IReadOnlyList<(float U, float V)> texcoords,
        IReadOnlyList<(float X, float Y, float Z)> normals,
        IReadOnlyList<(int A, int B, int C)> triangles,
        IReadOnlyList<IqmMeshSpec> meshes,
        bool includeText = true,
        uint texcoordSize = 2)
    {
        const int headerSize = 124;
        var names = new Dictionary<string, uint>(StringComparer.Ordinal);
        using var text = new MemoryStream();

        uint NameOffset(string name)
        {
            if (!includeText) return 0;
            if (names.TryGetValue(name, out uint offset)) return offset;
            offset = (uint)text.Position;
            byte[] bytes = Encoding.ASCII.GetBytes(name);
            text.Write(bytes);
            text.WriteByte(0);
            names[name] = offset;
            return offset;
        }

        foreach (IqmMeshSpec mesh in meshes)
        {
            _ = NameOffset(mesh.Name);
            _ = NameOffset(mesh.Material);
        }

        if (includeText && text.Length == 0)
            text.WriteByte(0);

        byte[] textBytes = text.ToArray();
        uint textCount = includeText ? (uint)textBytes.Length : 0;
        uint textOffset = headerSize;
        uint meshOffset = textOffset + textCount;
        uint vertexArrayOffset = meshOffset + (uint)meshes.Count * 24;
        uint triangleOffset = vertexArrayOffset + 3 * 20;
        uint positionOffset = triangleOffset + (uint)triangles.Count * 12;
        uint texcoordOffset = positionOffset + (uint)vertices.Count * 12;
        uint normalOffset = texcoordOffset + (uint)texcoords.Count * 8;
        uint fileSize = normalOffset + (uint)normals.Count * 12;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII);

        writer.Write(Encoding.ASCII.GetBytes("INTERQUAKEMODEL\0"));
        writer.Write(2u);
        writer.Write(fileSize);
        writer.Write(0u);
        writer.Write(textCount);
        writer.Write(textOffset);
        writer.Write((uint)meshes.Count);
        writer.Write(meshOffset);
        writer.Write(3u);
        writer.Write((uint)vertices.Count);
        writer.Write(vertexArrayOffset);
        writer.Write((uint)triangles.Count);
        writer.Write(triangleOffset);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);

        if (includeText)
            writer.Write(textBytes);

        foreach (IqmMeshSpec mesh in meshes)
        {
            writer.Write(NameOffset(mesh.Name));
            writer.Write(NameOffset(mesh.Material));
            writer.Write(mesh.FirstVertex);
            writer.Write(mesh.VertexCount);
            writer.Write(mesh.FirstTriangle);
            writer.Write(mesh.TriangleCount);
        }

        WriteVertexArray(writer, 0u, 7u, 3u, positionOffset);
        WriteVertexArray(writer, 1u, 7u, texcoordSize, texcoordOffset);
        WriteVertexArray(writer, 2u, 7u, 3u, normalOffset);

        foreach (var triangle in triangles)
        {
            writer.Write(triangle.A);
            writer.Write(triangle.B);
            writer.Write(triangle.C);
        }

        foreach (var vertex in vertices)
        {
            writer.Write(vertex.X);
            writer.Write(vertex.Y);
            writer.Write(vertex.Z);
        }

        foreach (var texcoord in texcoords)
        {
            writer.Write(texcoord.U);
            writer.Write(texcoord.V);
        }

        foreach (var normal in normals)
        {
            writer.Write(normal.X);
            writer.Write(normal.Y);
            writer.Write(normal.Z);
        }

        return stream.ToArray();
    }

    private static void WriteVertexArray(BinaryWriter writer, uint type, uint format, uint size, uint offset)
    {
        writer.Write(type);
        writer.Write(0u);
        writer.Write(format);
        writer.Write(size);
        writer.Write(offset);
    }

    private sealed record IqmMeshSpec(string Name, string Material, uint FirstVertex, uint VertexCount, uint FirstTriangle, uint TriangleCount);
}
