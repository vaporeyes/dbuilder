// ABOUTME: Tests UDB-style Wavefront OBJ terrain import behavior.
// ABOUTME: Verifies axis conversion, face filtering, and triangular map geometry creation.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ObjTerrainImporterTests
{
    [Fact]
    public void ParseUsesUdbAxisScaleAndReversesFaceWinding()
    {
        const string text = """
v 1 2 3
v 4 5 6
v 7 8 10
f 1/1/1 2/2/2 3/3/3
""";

        ObjTerrainParseResult result = ObjTerrainImporter.Parse(text, scale: 2, ObjTerrainUpAxis.Y);

        Assert.True(result.Success);
        Assert.Equal(new Vector3D(2, -6, 4), result.Geometry.Vertices[0]);
        Assert.Equal(new Vector3D(8, -12, 10), result.Geometry.Vertices[1]);
        Assert.Equal(new Vector3D(14, -20, 16), result.Geometry.Vertices[2]);
        ObjTerrainFace face = Assert.Single(result.Geometry.Faces);
        Assert.Equal(result.Geometry.Vertices[2], face.V1);
        Assert.Equal(result.Geometry.Vertices[1], face.V2);
        Assert.Equal(result.Geometry.Vertices[0], face.V3);
        Assert.Equal(4, result.Geometry.MinZ);
        Assert.Equal(16, result.Geometry.MaxZ);
    }

    [Fact]
    public void ParseSkipsDuplicateAndVerticalFaces()
    {
        const string text = """
v 0 0 0
v 0 1 0
v 0 1 1
v 1 0 1
f 1 1 2
f 1 2 3
f 1 2 4
""";

        ObjTerrainParseResult result = ObjTerrainImporter.Parse(text, axis: ObjTerrainUpAxis.Z);

        Assert.True(result.Success);
        ObjTerrainFace face = Assert.Single(result.Geometry.Faces);
        Assert.Equal(result.Geometry.Vertices[3], face.V1);
    }

    [Fact]
    public void ParseReportsNonTriangularAndInvalidFaceDefinitions()
    {
        const string text = """
v 0 0 0
v 1 0 0
v 0 1 1
f 1 2 3 1
f 1 missing 3
""";

        ObjTerrainParseResult result = ObjTerrainImporter.Parse(text);

        Assert.False(result.Success);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains("only triangular faces", result.Errors[0]);
    }

    [Fact]
    public void BuildMapGeometryCreatesSelectedTriangularSectorsAndSharedEdges()
    {
        const string text = """
v 0 0 0
v 64 0 0
v 64 64 16
v 0 64 16
f 1 2 3
f 1 3 4
""";
        ObjTerrainGeometry geometry = ObjTerrainImporter.Parse(text, axis: ObjTerrainUpAxis.Z).Geometry;
        var map = new MapSet();

        ObjTerrainImportResult result = ObjTerrainImporter.BuildMapGeometry(
            map,
            geometry,
            new ObjTerrainImportOptions(DefaultBrightness: 144, DefaultFloorTexture: "GRASS", DefaultCeilingTexture: "F_SKY1", DefaultWallTexture: "ROCK"));

        Assert.Equal(4, result.VerticesCreated);
        Assert.Equal(5, result.LinedefsCreated);
        Assert.Equal(6, result.SidedefsCreated);
        Assert.Equal(2, result.SectorsCreated);
        Assert.Equal(0, result.ThingsCreated);
        Assert.All(map.Sectors, sector =>
        {
            Assert.True(sector.Selected);
            Assert.Equal(144, sector.Brightness);
            Assert.Equal("GRASS", sector.FloorTexture);
            Assert.Equal("F_SKY1", sector.CeilTexture);
        });
        Assert.Contains(map.Linedefs, line => line.Front != null && line.Back != null && (line.Flags & Linedef.TwoSidedFlagBit) != 0);
        Assert.Contains(map.Linedefs, line => line.Back == null && (line.Flags & Linedef.BlockingFlagBit) != 0);
        Assert.All(map.Sidedefs, side => Assert.Equal("ROCK", side.LowTexture));
    }

    [Fact]
    public void BuildMapGeometryStoresVertexHeightsAndOptionalHeightThings()
    {
        const string text = """
v 0 0 0
v 64 0 0
v 0 64 32
f 1 2 3
""";
        ObjTerrainGeometry geometry = ObjTerrainImporter.Parse(text, axis: ObjTerrainUpAxis.Z).Geometry;
        var map = new MapSet();

        ObjTerrainImportResult result = ObjTerrainImporter.BuildMapGeometry(
            map,
            geometry,
            new ObjTerrainImportOptions(UseVertexHeights: true, CreateVertexHeightThings: true));

        Assert.Equal(3, result.VerticesCreated);
        Assert.Equal(3, result.ThingsCreated);
        Assert.Contains(map.Vertices, vertex => vertex.ZFloor == 32);
        Assert.All(map.Sidedefs, side => Assert.Equal("-", side.LowTexture));
        Assert.All(map.Things, thing => Assert.Equal(ObjTerrainImporter.VertexHeightThingType, thing.Type));
        Assert.Contains(map.Things, thing => thing.Height == 32);
    }
}
