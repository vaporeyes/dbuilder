// ABOUTME: Verifies UDB-style visual slope handle meshes, placement, and pivots.
// ABOUTME: Keeps visual slope editing geometry testable before renderer integration.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VisualSlopeHandleTests
{
    [Fact]
    public void LocalMeshesMatchUdbVisualSlopeHandleGeometry()
    {
        VisualSlopeHandleVertex[] line = VisualSlopeHandles.LineMesh.Vertices.ToArray();
        VisualSlopeHandleVertex[] vertex = VisualSlopeHandles.VertexMesh.Vertices.ToArray();

        Assert.Equal(6, line.Length);
        Assert.Equal(new Vector3D(0, -8, 0.1), line[0].Position);
        Assert.Equal(VisualSlopeHandles.TransparentWhite, line[0].Color);
        Assert.Equal(new Vector3D(0, 0, 0.1), line[1].Position);
        Assert.Equal(VisualSlopeHandles.White, line[1].Color);
        Assert.Equal(new Vector3D(1, 0, 0.1), line[2].Position);
        Assert.Equal(VisualSlopeHandles.White, line[2].Color);
        Assert.Equal(new Vector3D(1, -8, 0.1), line[5].Position);
        Assert.Equal(VisualSlopeHandles.TransparentWhite, line[5].Color);

        Assert.Equal(3, vertex.Length);
        Assert.Equal(new Vector3D(0, 0, 0.1), vertex[0].Position);
        Assert.Equal(VisualSlopeHandles.White, vertex[0].Color);
        Assert.Equal(new Vector3D(4, -8, 0.1), vertex[1].Position);
        Assert.Equal(VisualSlopeHandles.TransparentWhite, vertex[1].Color);
        Assert.Equal(new Vector3D(-4, -8, 0.1), vertex[2].Position);
        Assert.Equal(VisualSlopeHandles.TransparentWhite, vertex[2].Color);
    }

    [Fact]
    public void PlacementMatchesUdbLinePlaneBasis()
    {
        var plane = new Plane(new Vector3D(0, 0, 1), 0);

        VisualSlopeHandlePlacement placement = VisualSlopeHandles.CreatePlacement(
            new Line2D(new Vector2D(0, 0), new Vector2D(64, 0)),
            plane);

        Assert.Equal(new Vector3D(0, 0, 0), placement.Origin);
        Assert.Equal(new Vector3D(1, 0, 0), placement.LineVector);
        Assert.Equal(new Vector3D(0, 1, 0), placement.PerpendicularVector);
        Assert.Equal(new Vector3D(0, 0, 1), placement.Normal);
        Assert.Equal(64, placement.Length);
    }

    [Fact]
    public void SidedefBaseLineUsesUdbUpAndSideInversionRules()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Vertex start = map.AddVertex(new Vector2D(0, 0));
        Vertex end = map.AddVertex(new Vector2D(64, 0));
        Linedef line = map.AddLinedef(start, end);
        Sidedef front = map.AddSidedef(line, true, sector);
        Sidedef back = map.AddSidedef(line, false, sector);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);

        Line2D frontUp = VisualSlopeHandles.GetSidedefBaseLine(front, level, up: true);
        Line2D backUp = VisualSlopeHandles.GetSidedefBaseLine(back, level, up: true);
        Line2D frontDown = VisualSlopeHandles.GetSidedefBaseLine(front, level, up: false);

        Assert.Equal(start.Position, frontUp.v1);
        Assert.Equal(end.Position, frontUp.v2);
        Assert.Equal(end.Position, backUp.v1);
        Assert.Equal(start.Position, backUp.v2);
        Assert.Equal(end.Position, frontDown.v1);
        Assert.Equal(start.Position, frontDown.v2);
    }

    [Fact]
    public void VertexAngleUsesSingleLineFallback()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Vertex start = map.AddVertex(new Vector2D(0, 0));
        Vertex end = map.AddVertex(new Vector2D(64, 0));
        Linedef line = map.AddLinedef(start, end);
        map.AddSidedef(line, true, sector);
        map.BuildIndexes();

        double floorAngle = VisualSlopeHandles.ComputeVertexAngle(start, sector, VisualSlopeLevelType.Floor);
        double ceilingAngle = VisualSlopeHandles.ComputeVertexAngle(start, sector, VisualSlopeLevelType.Ceiling);

        Assert.Equal(Angle2D.Normalized(line.Angle + Angle2D.PIHALF), floorAngle);
        Assert.Equal(floorAngle, ceilingAngle);
    }

    [Fact]
    public void SidedefHandleExposesCenterAndEndpointPivotPointsOnLevelPlane()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Vertex start = map.AddVertex(new Vector2D(0, 0));
        Vertex end = map.AddVertex(new Vector2D(64, 0));
        Linedef line = map.AddLinedef(start, end);
        Sidedef side = map.AddSidedef(line, true, sector);
        var level = new VisualSlopeLevel(
            sector,
            VisualSlopeLevelType.Floor,
            new Plane(new Vector3D(0, -0.7071067811865475, 0.7071067811865475), 0));

        VisualSlopeHandle handle = VisualSlopeHandles.CreateSidedef(side, level, up: true);

        Assert.Equal(VisualSlopeHandleKind.Line, handle.Kind);
        AssertVector(new Vector3D(32, 0, 0), handle.GetPivotPoint());
        Vector3D[] pivots = handle.GetPivotPoints().ToArray();
        Assert.Equal(2, pivots.Length);
        AssertVector(new Vector3D(0, 0, 0), pivots[0]);
        AssertVector(new Vector3D(64, 0, 0), pivots[1]);
    }

    [Fact]
    public void VertexHandlePivotUsesVertexPositionOnLevelPlane()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Vertex start = map.AddVertex(new Vector2D(0, 16));
        Vertex end = map.AddVertex(new Vector2D(64, 16));
        Linedef line = map.AddLinedef(start, end);
        map.AddSidedef(line, true, sector);
        map.BuildIndexes();
        var level = new VisualSlopeLevel(
            sector,
            VisualSlopeLevelType.Floor,
            new Plane(new Vector3D(0, -0.7071067811865475, 0.7071067811865475), 0));

        VisualSlopeHandle handle = VisualSlopeHandles.CreateVertex(start, sector, level);

        Assert.Equal(VisualSlopeHandleKind.Vertex, handle.Kind);
        AssertVector(new Vector3D(0, 16, 16), handle.GetPivotPoint());
    }

    private static void AssertVector(Vector3D expected, Vector3D actual, double precision = 1e-9)
    {
        Assert.Equal(expected.x, actual.x, precision);
        Assert.Equal(expected.y, actual.y, precision);
        Assert.Equal(expected.z, actual.z, precision);
    }
}
