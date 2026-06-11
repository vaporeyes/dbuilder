// ABOUTME: Verifies UDB FlatQuad vertex, UV, color, and primitive behavior.
// ABOUTME: Keeps immediate flat quad rendering helpers compatible with Rendering/FlatQuad.cs.

using System.Drawing;
using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class FlatQuadTests
{
    [Fact]
    public void TriangleListUsesUdbVertexAndUvOrder()
    {
        var quad = new FlatQuad(PrimitiveType.TriangleList, 1f, 2f, 11f, 22f, 0.1f, 0.2f, 0.8f, 0.9f);

        Assert.Equal(PrimitiveType.TriangleList, quad.Type);
        Assert.Equal(6, quad.Vertices.Length);
        AssertVertex(quad.Vertices[0], 1f, 2f, 0.1f, 0.2f);
        AssertVertex(quad.Vertices[1], 11f, 2f, 0.8f, 0.2f);
        AssertVertex(quad.Vertices[2], 1f, 22f, 0.1f, 0.9f);
        AssertVertex(quad.Vertices[3], 1f, 22f, 0.1f, 0.9f);
        AssertVertex(quad.Vertices[4], 11f, 2f, 0.8f, 0.2f);
        AssertVertex(quad.Vertices[5], 11f, 22f, 0.8f, 0.9f);
        Assert.All(quad.Vertices, vertex => Assert.Equal(-1, vertex.c));
    }

    [Fact]
    public void TriangleStripUsesUdbVertexAndUvOrder()
    {
        var quad = new FlatQuad(PrimitiveType.TriangleStrip, 1f, 2f, 11f, 22f);

        Assert.Equal(PrimitiveType.TriangleStrip, quad.Type);
        Assert.Equal(4, quad.Vertices.Length);
        AssertVertex(quad.Vertices[0], 1f, 2f, 0f, 0f);
        AssertVertex(quad.Vertices[1], 11f, 2f, 1f, 0f);
        AssertVertex(quad.Vertices[2], 1f, 22f, 0f, 1f);
        AssertVertex(quad.Vertices[3], 11f, 22f, 1f, 1f);
        Assert.All(quad.Vertices, vertex => Assert.Equal(-1, vertex.c));
    }

    [Fact]
    public void TextureSizeConstructorAppliesUdbOnePixelInset()
    {
        var quad = new FlatQuad(PrimitiveType.TriangleStrip, 1f, 2f, 11f, 22f, textureWidth: 100f, textureHeight: 200f);

        AssertVertex(quad.Vertices[0], 1f, 2f, 0.01f, 0.005f);
        AssertVertex(quad.Vertices[1], 11f, 2f, 0.99f, 0.005f);
        AssertVertex(quad.Vertices[2], 1f, 22f, 0.01f, 0.995f);
        AssertVertex(quad.Vertices[3], 11f, 22f, 0.99f, 0.995f);
    }

    [Fact]
    public void RectangleConstructorUsesRectangleEdgesAndExplicitUv()
    {
        var quad = new FlatQuad(PrimitiveType.TriangleStrip, new RectangleF(3f, 4f, 10f, 20f), 0.2f, 0.3f, 0.7f, 0.8f);

        AssertVertex(quad.Vertices[0], 3f, 4f, 0.2f, 0.3f);
        AssertVertex(quad.Vertices[1], 13f, 4f, 0.7f, 0.3f);
        AssertVertex(quad.Vertices[2], 3f, 24f, 0.2f, 0.8f);
        AssertVertex(quad.Vertices[3], 13f, 24f, 0.7f, 0.8f);
    }

    [Fact]
    public void SetColorsAppliesUniformColorToEveryVertex()
    {
        var quad = new FlatQuad(PrimitiveType.TriangleList, 1f, 2f, 11f, 22f);

        quad.SetColors(0x10203040);

        Assert.All(quad.Vertices, vertex => Assert.Equal(0x10203040, vertex.c));
    }

    [Fact]
    public void SetCornerColorsUsesUdbTriangleListMapping()
    {
        var quad = new FlatQuad(PrimitiveType.TriangleList, 1f, 2f, 11f, 22f);

        quad.SetColors(1, 2, 3, 4);

        Assert.Equal(new[] { 1, 2, 3, 3, 2, 4 }, quad.Vertices.Select(vertex => vertex.c));
    }

    [Fact]
    public void SetCornerColorsUsesUdbTriangleStripMapping()
    {
        var quad = new FlatQuad(PrimitiveType.TriangleStrip, 1f, 2f, 11f, 22f);

        quad.SetColors(1, 2, 3, 4);

        Assert.Equal(new[] { 1, 2, 3, 4 }, quad.Vertices.Select(vertex => vertex.c));
    }

    [Fact]
    public void UnsupportedPrimitiveTypeThrowsUdbMessage()
    {
        var ex = Assert.Throws<NotSupportedException>(() => new FlatQuad(PrimitiveType.LineList, 1f, 2f, 11f, 22f));

        Assert.Equal("Unsupported PrimitiveType", ex.Message);
    }

    private static void AssertVertex(FlatVertex vertex, float x, float y, float u, float v)
    {
        Assert.Equal(x, vertex.x);
        Assert.Equal(y, vertex.y);
        Assert.Equal(u, vertex.u, precision: 6);
        Assert.Equal(v, vertex.v, precision: 6);
    }
}
