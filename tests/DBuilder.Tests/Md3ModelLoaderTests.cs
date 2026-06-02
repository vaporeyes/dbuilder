// ABOUTME: Tests UDB-style MD3 model loading from binary model data.
// ABOUTME: Verifies surface parsing, frame selection, shader skins, surface overrides, normals, and errors.

using System.Text;
using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class Md3ModelLoaderTests
{
    [Fact]
    public void LoadsSelectedFrameWithUdbCoordinatesNormalsAndUvs()
    {
        byte[] md3 = BuildMd3(new[]
        {
            new Md3SurfaceSpec(
                "body",
                "textures/body.png",
                new[] { (0, 1, 2) },
                new[] { (0.25f, 0.75f), (0.5f, 0.25f), (1.0f, 0.0f) },
                new[]
                {
                    new[] { (X: (short)64, Y: (short)128, Z: (short)192, Lat: (byte)0, Lng: (byte)0), (X: (short)256, Y: (short)320, Z: (short)384, Lat: (byte)0, Lng: (byte)0), (X: (short)448, Y: (short)512, Z: (short)576, Lat: (byte)0, Lng: (byte)0) },
                    new[] { (X: (short)128, Y: (short)192, Z: (short)256, Lat: (byte)0, Lng: (byte)0), (X: (short)320, Y: (short)384, Z: (short)448, Lat: (byte)0, Lng: (byte)0), (X: (short)512, Y: (short)576, Z: (short)640, Lat: (byte)0, Lng: (byte)0) },
                })
        });

        Md3ModelLoadResult result = Md3ModelLoader.Load(md3, frame: 1);

        Assert.Null(result.Errors);
        Assert.Equal(new[] { "textures/body.png" }, result.Skins);
        GzModelMesh mesh = Assert.Single(result.Meshes);
        Assert.Equal(new[] { 0, 1, 2 }, mesh.Indices);
        WorldVertex first = mesh.Vertices[0];
        Assert.Equal(3.0f, first.x);
        Assert.Equal(-2.0f, first.y);
        Assert.Equal(4.0f, first.z);
        Assert.Equal(0.25f, first.u);
        Assert.Equal(0.75f, first.v);
        Assert.Equal(0.0f, first.nx);
        Assert.Equal(0.0f, first.ny);
        Assert.Equal(1.0f, first.nz);
        Assert.Equal(9.0f, result.Bounds.MaxX);
        Assert.Equal(-2.0f, result.Bounds.MaxY);
        Assert.Equal(10.0f, result.Bounds.MaxZ);
    }

    [Fact]
    public void SurfaceSkinsOverrideShaderSkinsBySurfaceIndex()
    {
        byte[] md3 = BuildMd3(new[]
        {
            SimpleSurface("body", "textures/body.png"),
            SimpleSurface("head", "textures/head.png"),
        });

        Md3ModelLoadResult result = Md3ModelLoader.Load(md3, new Dictionary<int, string>
        {
            [1] = "models/head_alt.png",
        });

        Assert.Null(result.Errors);
        Assert.Equal(new[] { "textures/body.png", "models/head_alt.png" }, result.Skins);
        Assert.Equal(2, result.Meshes.Count);
    }

    [Fact]
    public void GroupsSurfacesThatUseTheSameSkin()
    {
        byte[] md3 = BuildMd3(new[]
        {
            SimpleSurface("body", "textures/shared.png"),
            SimpleSurface("head", "textures/shared.png"),
        });

        Md3ModelLoadResult result = Md3ModelLoader.Load(md3);

        Assert.Null(result.Errors);
        Assert.Equal(new[] { "textures/shared.png" }, result.Skins);
        GzModelMesh mesh = Assert.Single(result.Meshes);
        Assert.Equal(6, mesh.Vertices.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, mesh.Indices);
    }

    [Fact]
    public void ReportsUdbStyleHeaderFrameAndSurfaceErrors()
    {
        byte[] md3 = BuildMd3(new[] { SimpleSurface("body", "textures/body.png") });

        byte[] badMagic = (byte[])md3.Clone();
        badMagic[3] = (byte)'2';
        Assert.Equal("unknown header: expected \"IDP3\", but got \"IDP2\"", Md3ModelLoader.Load(badMagic).Errors);

        byte[] badVersion = (byte[])md3.Clone();
        BitConverter.GetBytes(14).CopyTo(badVersion, 4);
        Assert.Equal("expected MD3 version 15, but got 14", Md3ModelLoader.Load(badVersion).Errors);

        Assert.Equal("frame 2 is outside of model's frame range [0..0]", Md3ModelLoader.Load(md3, frame: 2).Errors);

        byte[] badSurface = (byte[])md3.Clone();
        badSurface[108 + 3] = (byte)'2';
        Assert.Equal("error while reading surface. Unknown header: expected \"IDP3\", but got \"IDP2\"", Md3ModelLoader.Load(badSurface).Errors);
    }

    private static Md3SurfaceSpec SimpleSurface(string name, string skin)
        => new(
            name,
            skin,
            new[] { (0, 1, 2) },
            new[] { (0.0f, 0.0f), (0.5f, 0.5f), (1.0f, 1.0f) },
            new[]
            {
                new[] { (X: (short)64, Y: (short)128, Z: (short)192, Lat: (byte)0, Lng: (byte)0), (X: (short)256, Y: (short)320, Z: (short)384, Lat: (byte)0, Lng: (byte)0), (X: (short)448, Y: (short)512, Z: (short)576, Lat: (byte)0, Lng: (byte)0) },
            });

    private static byte[] BuildMd3(IReadOnlyList<Md3SurfaceSpec> surfaces)
    {
        const int headerSize = 108;
        int surfaceBytes = surfaces.Sum(SurfaceSize);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII);

        writer.Write(Encoding.ASCII.GetBytes("IDP3"));
        writer.Write(15);
        WriteFixedString(writer, "test", 64);
        writer.Write(0);
        writer.Write(surfaces.Max(surface => surface.Frames.Count));
        writer.Write(0);
        writer.Write(surfaces.Count);
        writer.Write(0);
        writer.Write(headerSize);
        writer.Write(headerSize);
        writer.Write(headerSize);
        writer.Write(headerSize + surfaceBytes);

        foreach (Md3SurfaceSpec surface in surfaces)
            WriteSurface(writer, surface);

        return stream.ToArray();
    }

    private static void WriteSurface(BinaryWriter writer, Md3SurfaceSpec surface)
    {
        const int surfaceHeaderSize = 108;
        int triangleOffset = surfaceHeaderSize;
        int shaderOffset = triangleOffset + surface.Triangles.Count * 12;
        int stOffset = shaderOffset + 68;
        int normalOffset = stOffset + surface.TextureCoordinates.Count * 8;
        int endOffset = normalOffset + surface.Frames.Count * surface.TextureCoordinates.Count * 8;

        writer.Write(Encoding.ASCII.GetBytes("IDP3"));
        WriteFixedString(writer, surface.Name, 64);
        writer.Write(0);
        writer.Write(surface.Frames.Count);
        writer.Write(1);
        writer.Write(surface.TextureCoordinates.Count);
        writer.Write(surface.Triangles.Count);
        writer.Write(triangleOffset);
        writer.Write(shaderOffset);
        writer.Write(stOffset);
        writer.Write(normalOffset);
        writer.Write(endOffset);

        foreach (var triangle in surface.Triangles)
        {
            writer.Write(triangle.A);
            writer.Write(triangle.B);
            writer.Write(triangle.C);
        }

        WriteFixedString(writer, surface.Skin, 64);
        writer.Write(0);

        foreach (var textureCoordinate in surface.TextureCoordinates)
        {
            writer.Write(textureCoordinate.U);
            writer.Write(textureCoordinate.V);
        }

        foreach (var frame in surface.Frames)
            foreach (var vertex in frame)
            {
                writer.Write(vertex.X);
                writer.Write(vertex.Y);
                writer.Write(vertex.Z);
                writer.Write(vertex.Lat);
                writer.Write(vertex.Lng);
            }
    }

    private static int SurfaceSize(Md3SurfaceSpec surface)
        => 108 + surface.Triangles.Count * 12 + 68 + surface.TextureCoordinates.Count * 8 + surface.Frames.Count * surface.TextureCoordinates.Count * 8;

    private static void WriteFixedString(BinaryWriter writer, string value, int length)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        writer.Write(bytes, 0, Math.Min(bytes.Length, length));
        for (int i = bytes.Length; i < length; i++)
            writer.Write((byte)0);
    }

    private sealed record Md3SurfaceSpec(
        string Name,
        string Skin,
        IReadOnlyList<(int A, int B, int C)> Triangles,
        IReadOnlyList<(float U, float V)> TextureCoordinates,
        IReadOnlyList<IReadOnlyList<(short X, short Y, short Z, byte Lat, byte Lng)>> Frames);
}
