// ABOUTME: Verifies UDB-compatible WorldVertex constructors and default field behavior.
// ABOUTME: Keeps ported rendering call sites aligned with Rendering/WorldVertex.cs.

using DBuilder.Geometry;
using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class WorldVertexTests
{
    [Fact]
    public void ConstructorWithCoordinatesColorAndUvMatchesUdbFieldAssignment()
    {
        var vertex = new WorldVertex(1.5f, -2.5f, 3.25f, unchecked((int)0xFF102030u), 0.125f, 0.875f);

        AssertWorldVertex(vertex, 1.5f, -2.5f, 3.25f, unchecked((int)0xFF102030u), 0.125f, 0.875f);
    }

    [Fact]
    public void ConstructorWithGeometryVectorsMatchesUdbFieldAssignment()
    {
        var vertex = new WorldVertex(
            new Vector3D(1.25, -2.5, 3.75),
            unchecked((int)0xFF405060u),
            new Vector2D(0.25, 0.75));

        AssertWorldVertex(vertex, 1.25f, -2.5f, 3.75f, unchecked((int)0xFF405060u), 0.25f, 0.75f);
    }

    [Fact]
    public void PositionOnlyConstructorUsesUdbDefaultColorAndUv()
    {
        var fromFloats = new WorldVertex(1.5f, -2.5f, 3.25f);
        var fromVector = new WorldVertex(new Vector3D(4.5, -5.5, 6.25));

        AssertWorldVertex(fromFloats, 1.5f, -2.5f, 3.25f, -1, 0.0f, 0.0f);
        AssertWorldVertex(fromVector, 4.5f, -5.5f, 6.25f, -1, 0.0f, 0.0f);
    }

    [Fact]
    public void StrideMatchesUdbWorldVertexLayout()
    {
        Assert.Equal(36, WorldVertex.Stride);
    }

    private static void AssertWorldVertex(WorldVertex vertex, float x, float y, float z, int c, float u, float v)
    {
        Assert.Equal(x, vertex.x);
        Assert.Equal(y, vertex.y);
        Assert.Equal(z, vertex.z);
        Assert.Equal(c, vertex.c);
        Assert.Equal(u, vertex.u);
        Assert.Equal(v, vertex.v);
        Assert.Equal(0.0f, vertex.nx);
        Assert.Equal(0.0f, vertex.ny);
        Assert.Equal(0.0f, vertex.nz);
    }
}
