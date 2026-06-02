// ABOUTME: Tests UDB-style Unreal Engine 1 _a.3d and _d.3d model loading from binary data.
// ABOUTME: Verifies frame selection, packed and Deus Ex vertices, texture grouping, UVs, and loader errors.

using System.Text;
using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class UnrealModelLoaderTests
{
    [Fact]
    public void LoadsSelectedPackedFrameWithUdbCoordinatesUvsAndNormals()
    {
        byte[] animation = BuildPackedAnimation(new[]
        {
            new[] { (X: 0.0f, Y: 0.0f, Z: 0.0f), (X: 1.0f, Y: 0.0f, Z: 0.0f), (X: 0.0f, Y: 1.0f, Z: 0.0f) },
            new[] { (X: 0.0f, Y: 0.0f, Z: 0.0f), (X: 4.0f, Y: 0.0f, Z: 0.0f), (X: 0.0f, Y: 0.0f, Z: 2.0f) },
        });
        byte[] data = BuildData(
            vertexCount: 3,
            new[] { new UnrealPolySpec(0, 1, 2, Type: 0, TextureNumber: 3, Uv0: (0, 0), Uv1: (128, 64), Uv2: (255, 255)) });

        UnrealModelLoadResult result = UnrealModelLoader.Load(animation, data, frame: 1);

        Assert.Null(result.Errors);
        Assert.Equal(new[] { "" }, result.Skins);
        GzModelMesh mesh = Assert.Single(result.Meshes);
        Assert.Equal(new[] { 0, 1, 2 }, mesh.Indices);

        WorldVertex second = mesh.Vertices[1];
        Assert.Equal(4.0f, second.x);
        Assert.Equal(0.0f, second.y);
        Assert.Equal(0.0f, second.z);
        Assert.Equal(128.0f / 255.0f, second.u);
        Assert.Equal(64.0f / 255.0f, second.v);
        Assert.Equal(0.0f, second.nx);
        Assert.Equal(1.0f, second.ny);
        Assert.Equal(0.0f, second.nz);
        Assert.Equal(4.0f, result.Bounds.MaxX);
        Assert.Equal(2.0f, result.Bounds.MaxZ);
    }

    [Fact]
    public void LoadsDeusExFramesAndGroupsSkinsLikeUdb()
    {
        byte[] animation = BuildDeusExAnimation(new[]
        {
            new[] { (X: (short)0, Y: (short)0, Z: (short)0), (X: (short)-2, Y: (short)-4, Z: (short)8), (X: (short)-3, Y: (short)-6, Z: (short)9), (X: (short)-4, Y: (short)-8, Z: (short)10) },
        });
        byte[] data = BuildData(
            vertexCount: 4,
            new[]
            {
                new UnrealPolySpec(0, 1, 2, Type: 0x20, TextureNumber: 4, Uv0: (0, 0), Uv1: (64, 0), Uv2: (0, 64)),
                new UnrealPolySpec(0, 2, 3, Type: 0, TextureNumber: 7, Uv0: (0, 0), Uv1: (128, 0), Uv2: (0, 128)),
                new UnrealPolySpec(0, 3, 1, Type: 0x08, TextureNumber: 9, Uv0: (0, 0), Uv1: (255, 0), Uv2: (0, 255)),
            });

        UnrealModelLoadResult result = UnrealModelLoader.Load(animation, data, skins: new Dictionary<int, string>
        {
            [0] = "Textures/Body.PNG",
            [1] = "Textures/Head.PNG",
            [2] = "Textures/Hidden.PNG",
        });

        Assert.Null(result.Errors);
        Assert.Equal(new[] { "textures/body.png", "textures/head.png", "textures/hidden.png" }, result.Skins);
        Assert.Equal(3, result.Meshes.Count);
        Assert.Equal(3, result.Meshes[0].Vertices.Count);
        Assert.Equal(3, result.Meshes[1].Vertices.Count);
        Assert.Empty(result.Meshes[2].Vertices);

        WorldVertex first = result.Meshes[0].Vertices[1];
        Assert.Equal(4.0f, first.x);
        Assert.Equal(2.0f, first.y);
        Assert.Equal(8.0f, first.z);
        Assert.NotEqual(0.0f, first.nx);
    }

    [Fact]
    public void ReportsUdbStyleFrameErrorsAndCollapsesBrokenPolygons()
    {
        byte[] animation = BuildPackedAnimation(new[]
        {
            new[] { (X: 0.0f, Y: 0.0f, Z: 0.0f), (X: 1.0f, Y: 0.0f, Z: 0.0f), (X: 0.0f, Y: 1.0f, Z: 0.0f) },
        });
        byte[] data = BuildData(
            vertexCount: 3,
            new[] { new UnrealPolySpec(0, 20, 2, Type: 0, TextureNumber: 0, Uv0: (0, 0), Uv1: (0, 0), Uv2: (0, 0)) });

        Assert.Equal("frame 2 is outside of model's frame range [0..0]", UnrealModelLoader.Load(animation, data, frame: 2).Errors);

        UnrealModelLoadResult result = UnrealModelLoader.Load(animation, data);

        Assert.Null(result.Errors);
        GzModelMesh mesh = Assert.Single(result.Meshes);
        Assert.All(mesh.Vertices, vertex =>
        {
            Assert.Equal(0.0f, vertex.x);
            Assert.Equal(0.0f, vertex.y);
            Assert.Equal(0.0f, vertex.z);
        });
    }

    private static byte[] BuildPackedAnimation(IReadOnlyList<IReadOnlyList<(float X, float Y, float Z)>> frames)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII);
        writer.Write((ushort)frames.Count);
        writer.Write((ushort)(frames[0].Count * 4));
        foreach (var frame in frames)
            foreach (var vertex in frame)
                writer.Write(PackVertex(-vertex.Y, -vertex.X, vertex.Z));

        return stream.ToArray();
    }

    private static byte[] BuildDeusExAnimation(IReadOnlyList<IReadOnlyList<(short X, short Y, short Z)>> frames)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII);
        writer.Write((ushort)frames.Count);
        writer.Write((ushort)(frames[0].Count * 8));
        foreach (var frame in frames)
            foreach (var vertex in frame)
            {
                writer.Write(vertex.X);
                writer.Write(vertex.Y);
                writer.Write(vertex.Z);
                writer.Write((short)0);
            }

        return stream.ToArray();
    }

    private static byte[] BuildData(int vertexCount, IReadOnlyList<UnrealPolySpec> polygons)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII);
        writer.Write((ushort)polygons.Count);
        writer.Write((ushort)vertexCount);
        for (int i = 0; i < 44; i++)
            writer.Write((byte)0);

        foreach (UnrealPolySpec polygon in polygons)
        {
            writer.Write((short)polygon.A);
            writer.Write((short)polygon.B);
            writer.Write((short)polygon.C);
            writer.Write((byte)polygon.Type);
            writer.Write((byte)0);
            writer.Write((byte)polygon.Uv0.U);
            writer.Write((byte)polygon.Uv0.V);
            writer.Write((byte)polygon.Uv1.U);
            writer.Write((byte)polygon.Uv1.V);
            writer.Write((byte)polygon.Uv2.U);
            writer.Write((byte)polygon.Uv2.V);
            writer.Write((byte)polygon.TextureNumber);
            writer.Write((byte)0);
        }

        return stream.ToArray();
    }

    private static int PackVertex(float x, float y, float z)
    {
        int packedX = (int)MathF.Round(x * 4.0f) & 0x7ff;
        int packedY = (int)MathF.Round(y * 4.0f) & 0x7ff;
        int packedZ = (int)MathF.Round(z * 2.0f) & 0x3ff;
        return packedX | (packedY << 11) | (packedZ << 22);
    }

    private sealed record UnrealPolySpec(
        int A,
        int B,
        int C,
        int Type,
        int TextureNumber,
        (int U, int V) Uv0,
        (int U, int V) Uv1,
        (int U, int V) Uv2);
}
