// ABOUTME: Tests UDB-style Wavefront OBJ model loader behavior for 3D thing models.
// ABOUTME: Verifies coordinate conversion, mesh grouping, skins, surface-skin overrides, and parser errors.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class ObjModelLoaderTests
{
    [Fact]
    public void LoadsTriangleWithUdbCoordinateConversionAndAttributes()
    {
        const string text = """
v 1 2 3
v 4 5 6
v 7 8 9
vt 0.25 0.75
vt 0.5 0.25
vt 1 0
vn 0 0 1
f 1/1/1 2/2/1 3/3/1
""";

        ObjModelLoadResult result = ObjModelLoader.Load(text);

        Assert.Null(result.Errors);
        GzModelMesh mesh = Assert.Single(result.Meshes);
        Assert.Equal(new[] { 0, 1, 2 }, mesh.Indices);
        Assert.Equal(3, mesh.Vertices.Count);

        WorldVertex first = mesh.Vertices[0];
        Assert.Equal(-3.0f, first.x);
        Assert.Equal(-1.0f, first.y);
        Assert.Equal(2.0f, first.z);
        Assert.Equal(0.25f, first.u);
        Assert.Equal(0.25f, first.v);
        Assert.Equal(0.0f, first.nx);
        Assert.Equal(0.0f, first.ny);
        Assert.Equal(1.0f, first.nz);
        Assert.Equal(-9.0f, result.Bounds.MinX);
        Assert.Equal(-7.0f, result.Bounds.MinY);
        Assert.Equal(2.0f, result.Bounds.MinZ);
        Assert.Equal(-3.0f, result.Bounds.MaxX);
        Assert.Equal(-1.0f, result.Bounds.MaxY);
        Assert.Equal(8.0f, result.Bounds.MaxZ);
    }

    [Fact]
    public void SplitsQuadIntoTwoTriangles()
    {
        const string text = """
v 0 0 0
v 1 0 0
v 1 1 0
v 0 1 0
f 1 2 3 4
""";

        ObjModelLoadResult result = ObjModelLoader.Load(text);

        Assert.Null(result.Errors);
        GzModelMesh mesh = Assert.Single(result.Meshes);
        Assert.Equal(6, mesh.Vertices.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, mesh.Indices);
        Assert.Equal(0.0f, mesh.Vertices[3].x);
        Assert.Equal(0.0f, mesh.Vertices[3].y);
        Assert.Equal(0.0f, mesh.Vertices[3].z);
        Assert.Equal(0.0f, mesh.Vertices[4].x);
        Assert.Equal(-1.0f, mesh.Vertices[4].y);
        Assert.Equal(1.0f, mesh.Vertices[4].z);
    }

    [Fact]
    public void UsemtlCreatesMeshGroupsAndCapturesQuotedSkinNames()
    {
        const string text = """
usemtl "STONE"
v 0 0 0
v 1 0 0
v 0 1 0
f 1 2 3
usemtl METAL
f 1 3 2
""";

        ObjModelLoadResult result = ObjModelLoader.Load(text);

        Assert.Null(result.Errors);
        Assert.Equal(new[] { "STONE", "METAL" }, result.Skins);
        Assert.Equal(2, result.Meshes.Count);
        Assert.All(result.Meshes, mesh => Assert.Equal(3, mesh.Vertices.Count));
    }

    [Fact]
    public void SurfaceSkinsOverrideMaterialSkinsByIndex()
    {
        const string text = """
usemtl STONE
v 0 0 0
v 1 0 0
v 0 1 0
f 1 2 3
""";

        ObjModelLoadResult result = ObjModelLoader.Load(text, new Dictionary<int, string>
        {
            [0] = "models/override.png",
            [2] = "models/third.png",
        });

        Assert.Equal(new[] { "models/override.png", "", "models/third.png" }, result.Skins);
    }

    [Fact]
    public void ReportsUdbStyleParseErrors()
    {
        ObjModelLoadResult missingVertex = ObjModelLoader.Load("f 1 2 3");
        Assert.Equal("Error in line 1: vertex 1 does not exist", missingVertex.Errors);

        ObjModelLoadResult pentagon = ObjModelLoader.Load("""
v 0 0 0
v 1 0 0
v 1 1 0
v 0 1 0
v 0 0 1
f 1 2 3 4 5
""");
        Assert.Equal("Error in line 6: faces with more than 4 sides are not supported", pentagon.Errors);
    }
}
