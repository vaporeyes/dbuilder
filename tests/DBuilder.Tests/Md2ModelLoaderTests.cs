// ABOUTME: Tests UDB-style MD2 model loading from binary model data.
// ABOUTME: Verifies frame selection, coordinate rotation, UV duplication, skin output, and loader errors.

using System.Text;
using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class Md2ModelLoaderTests
{
    [Fact]
    public void LoadsSelectedFrameWithUdbCoordinateRotationAndUvs()
    {
        byte[] md2 = BuildMd2(
            vertices: new[]
            {
                (X: (byte)1, Y: (byte)2, Z: (byte)3),
                (X: (byte)4, Y: (byte)5, Z: (byte)6),
                (X: (byte)7, Y: (byte)8, Z: (byte)9),
            },
            uvs: new[]
            {
                (U: (short)0, V: (short)0),
                (U: (short)32, V: (short)64),
                (U: (short)64, V: (short)128),
            },
            triangles: new[] { ((ushort)0, (ushort)1, (ushort)2, (ushort)0, (ushort)1, (ushort)2) },
            frameNames: new[] { "idle", "run" });

        Md2ModelLoadResult result = Md2ModelLoader.Load(md2, frame: 1);

        Assert.Null(result.Errors);
        Assert.Equal(new[] { "" }, result.Skins);
        GzModelMesh mesh = Assert.Single(result.Meshes);
        Assert.Equal(new[] { 0, 1, 2 }, mesh.Indices);
        WorldVertex first = mesh.Vertices[0];
        Assert.Equal(2.0f, first.x);
        Assert.Equal(-1.0f, first.y);
        Assert.Equal(3.0f, first.z);
        Assert.Equal(0.0f, first.u);
        Assert.Equal(0.0f, first.v);
        Assert.Equal(8.0f, result.Bounds.MaxX);
        Assert.Equal(-1.0f, result.Bounds.MaxY);
        Assert.Equal(9.0f, result.Bounds.MaxZ);
        Assert.Equal(0.5f, mesh.Vertices[1].u);
        Assert.Equal(0.5f, mesh.Vertices[1].v);
    }

    [Fact]
    public void SelectsFrameByLowercaseName()
    {
        byte[] md2 = BuildMd2(
            vertices: new[]
            {
                (X: (byte)1, Y: (byte)2, Z: (byte)3),
                (X: (byte)4, Y: (byte)5, Z: (byte)6),
                (X: (byte)7, Y: (byte)8, Z: (byte)9),
            },
            uvs: new[] { ((short)0, (short)0), ((short)0, (short)0), ((short)0, (short)0) },
            triangles: new[] { ((ushort)0, (ushort)1, (ushort)2, (ushort)0, (ushort)1, (ushort)2) },
            frameNames: new[] { "idle", "run" },
            frameOffset: 10);

        Md2ModelLoadResult result = Md2ModelLoader.Load(md2, frameName: "run");

        Assert.Null(result.Errors);
        WorldVertex first = Assert.Single(result.Meshes).Vertices[0];
        Assert.Equal(12.0f, first.x);
        Assert.Equal(-11.0f, first.y);
        Assert.Equal(13.0f, first.z);
    }

    [Fact]
    public void DuplicatesVerticesWhenSameSourceVertexUsesDifferentUvs()
    {
        byte[] md2 = BuildMd2(
            vertices: new[]
            {
                (X: (byte)1, Y: (byte)2, Z: (byte)3),
                (X: (byte)4, Y: (byte)5, Z: (byte)6),
                (X: (byte)7, Y: (byte)8, Z: (byte)9),
            },
            uvs: new[]
            {
                (U: (short)0, V: (short)0),
                (U: (short)32, V: (short)32),
                (U: (short)64, V: (short)64),
                (U: (short)16, V: (short)16),
            },
            triangles: new[]
            {
                ((ushort)0, (ushort)1, (ushort)2, (ushort)0, (ushort)1, (ushort)2),
                ((ushort)0, (ushort)2, (ushort)1, (ushort)3, (ushort)2, (ushort)1),
            },
            frameNames: new[] { "idle" });

        Md2ModelLoadResult result = Md2ModelLoader.Load(md2);

        Assert.Null(result.Errors);
        GzModelMesh mesh = Assert.Single(result.Meshes);
        Assert.Equal(4, mesh.Vertices.Count);
        Assert.Equal(3, mesh.Indices[3]);
        Assert.Equal(0.25f, mesh.Vertices[3].u);
        Assert.Equal(0.125f, mesh.Vertices[3].v);
    }

    [Fact]
    public void ReportsUdbStyleHeaderFrameAndNamedFrameErrors()
    {
        byte[] md2 = BuildMd2(
            vertices: new[]
            {
                (X: (byte)1, Y: (byte)2, Z: (byte)3),
                (X: (byte)4, Y: (byte)5, Z: (byte)6),
                (X: (byte)7, Y: (byte)8, Z: (byte)9),
            },
            uvs: new[] { ((short)0, (short)0), ((short)0, (short)0), ((short)0, (short)0) },
            triangles: new[] { ((ushort)0, (ushort)1, (ushort)2, (ushort)0, (ushort)1, (ushort)2) },
            frameNames: new[] { "idle" });

        byte[] badMagic = (byte[])md2.Clone();
        badMagic[3] = (byte)'3';
        Assert.Equal("unknown header: expected \"IDP2\", but got \"IDP3\"", Md2ModelLoader.Load(badMagic).Errors);

        byte[] badVersion = (byte[])md2.Clone();
        BitConverter.GetBytes(7).CopyTo(badVersion, 4);
        Assert.Equal("expected MD3 version 15, but got 7", Md2ModelLoader.Load(badVersion).Errors);

        Assert.Equal("frame 2 is outside of model's frame range [0..0]", Md2ModelLoader.Load(md2, frame: 2).Errors);
        Assert.Equal("unable to find frame \"run\"!", Md2ModelLoader.Load(md2, frameName: "run").Errors);
    }

    private static byte[] BuildMd2(
        IReadOnlyList<(byte X, byte Y, byte Z)> vertices,
        IReadOnlyList<(short U, short V)> uvs,
        IReadOnlyList<(ushort A, ushort B, ushort C, ushort Ua, ushort Ub, ushort Uc)> triangles,
        IReadOnlyList<string> frameNames,
        int frameOffset = 0)
    {
        const int textureWidth = 64;
        const int textureHeight = 128;
        int frameSize = 40 + vertices.Count * 4;
        int uvOffset = 68;
        int triangleOffset = uvOffset + uvs.Count * 4;
        int frameDataOffset = triangleOffset + triangles.Count * 12;
        int endOffset = frameDataOffset + frameNames.Count * frameSize;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII);

        writer.Write(Encoding.ASCII.GetBytes("IDP2"));
        writer.Write(8);
        writer.Write(textureWidth);
        writer.Write(textureHeight);
        writer.Write(frameSize);
        writer.Write(0);
        writer.Write(vertices.Count);
        writer.Write(uvs.Count);
        writer.Write(triangles.Count);
        writer.Write(0);
        writer.Write(frameNames.Count);
        writer.Write(68);
        writer.Write(uvOffset);
        writer.Write(triangleOffset);
        writer.Write(frameDataOffset);
        writer.Write(0);
        writer.Write(endOffset);

        foreach (var uv in uvs)
        {
            writer.Write(uv.U);
            writer.Write(uv.V);
        }

        foreach (var triangle in triangles)
        {
            writer.Write(triangle.A);
            writer.Write(triangle.B);
            writer.Write(triangle.C);
            writer.Write(triangle.Ua);
            writer.Write(triangle.Ub);
            writer.Write(triangle.Uc);
        }

        for (int frame = 0; frame < frameNames.Count; frame++)
        {
            writer.Write(1.0f);
            writer.Write(1.0f);
            writer.Write(1.0f);
            writer.Write((float)(frame * frameOffset));
            writer.Write((float)(frame * frameOffset));
            writer.Write((float)(frame * frameOffset));
            WriteFixedString(writer, frameNames[frame], 16);
            foreach (var vertex in vertices)
            {
                writer.Write(vertex.X);
                writer.Write(vertex.Y);
                writer.Write(vertex.Z);
                writer.Write((byte)0);
            }
        }

        return stream.ToArray();
    }

    private static void WriteFixedString(BinaryWriter writer, string value, int length)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        writer.Write(bytes, 0, Math.Min(bytes.Length, length));
        for (int i = bytes.Length; i < length; i++)
            writer.Write((byte)0);
    }
}
