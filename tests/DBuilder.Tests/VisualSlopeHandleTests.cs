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

    [Fact]
    public void LineHandleHeightChangeAppliesFloorSlopeAroundPivotHandle()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        VisualSlopeHandle handle = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[0], level, up: true);
        VisualSlopeHandle pivot = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[2], level, up: true);

        VisualSlopeChangeResult result = VisualSlopeHandles.ChangeTargetHeight(handle, pivot, 16);

        Assert.Equal(VisualSlopeChangeResult.Changed, result);
        Assert.True(sector.HasFloorSlope);
        Assert.Equal(16, sector.GetFloorZ(new Vector2D(32, 0)), 1e-9);
        Assert.Equal(0, sector.GetFloorZ(new Vector2D(32, 64)), 1e-9);
    }

    [Fact]
    public void VertexHandleHeightChangeCanPivotAroundLineHandle()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        Vertex vertex = sector.Sidedefs[0].Line.Start;
        VisualSlopeHandle handle = VisualSlopeHandles.CreateVertex(vertex, sector, level);
        VisualSlopeHandle pivot = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[1], level, up: true);

        VisualSlopeChangeResult result = VisualSlopeHandles.ChangeTargetHeight(handle, pivot, 16);

        Assert.Equal(VisualSlopeChangeResult.Changed, result);
        Assert.True(sector.HasFloorSlope);
        Assert.Equal(16, sector.GetFloorZ(vertex.Position), 1e-9);
        Assert.Equal(0, sector.GetFloorZ(new Vector2D(64, 32)), 1e-9);
    }

    [Fact]
    public void VertexHandleHeightChangeCanPivotAroundVertexHandle()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        Vertex vertex = sector.Sidedefs[0].Line.Start;
        Vertex pivotVertex = sector.Sidedefs[0].Line.End;
        VisualSlopeHandle handle = VisualSlopeHandles.CreateVertex(vertex, sector, level);
        VisualSlopeHandle pivot = VisualSlopeHandles.CreateVertex(pivotVertex, sector, level);

        VisualSlopeChangeResult result = VisualSlopeHandles.ChangeTargetHeight(handle, pivot, 16);

        Assert.Equal(VisualSlopeChangeResult.Changed, result);
        Assert.True(sector.HasFloorSlope);
        Assert.Equal(16, sector.GetFloorZ(vertex.Position), 1e-9);
        Assert.Equal(0, sector.GetFloorZ(pivotVertex.Position), 1e-9);
    }

    [Fact]
    public void ApplySlopeResetsHorizontalFloorPlaneToHeight()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        sector.FloorSlope = new Vector3D(0, -0.7071067811865475, 0.7071067811865475);
        sector.FloorSlopeOffset = 0;

        VisualSlopeHandles.ApplySlope(
            VisualSlopeLevel.Floor(sector),
            new Plane(new Vector3D(0, 0, 1), -24));

        Assert.False(sector.HasFloorSlope);
        Assert.True(double.IsNaN(sector.FloorSlopeOffset));
        Assert.Equal(24, sector.FloorHeight);
    }

    [Fact]
    public void ApplySlopeInvertsCeilingPlane()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        Plane plane = new(new Vector3D(0, -0.7071067811865475, 0.7071067811865475), 0);

        VisualSlopeHandles.ApplySlope(VisualSlopeLevel.Ceiling(sector), plane);

        Assert.True(sector.HasCeilSlope);
        AssertVector(plane.GetInverted().Normal, sector.CeilSlope);
        Assert.Equal(plane.GetInverted().Offset, sector.CeilSlopeOffset, 1e-9);
    }

    [Fact]
    public void ChangeTargetHeightRejectsMissingSameAndVerticalPivot()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        VisualSlopeHandle handle = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[0], level, up: true);
        Linedef duplicate = map.AddLinedef(sector.Sidedefs[0].Line.Start, sector.Sidedefs[0].Line.End);
        Sidedef duplicateSide = map.AddSidedef(duplicate, true, sector);
        VisualSlopeHandle sameLinePivot = VisualSlopeHandles.CreateSidedef(duplicateSide, level, up: true);

        Assert.Equal(VisualSlopeChangeResult.MissingPivot, VisualSlopeHandles.ChangeTargetHeight(handle, null, 16));
        Assert.Equal(VisualSlopeChangeResult.SameAsPivot, VisualSlopeHandles.ChangeTargetHeight(handle, handle, 16));
        Assert.Equal(VisualSlopeChangeResult.VerticalPlane, VisualSlopeHandles.ChangeTargetHeight(handle, sameLinePivot, 16));
    }

    private static void AssertVector(Vector3D expected, Vector3D actual, double precision = 1e-9)
    {
        Assert.Equal(expected.x, actual.x, precision);
        Assert.Equal(expected.y, actual.y, precision);
        Assert.Equal(expected.z, actual.z, precision);
    }

    private static Sector AddSquareSector(MapSet map, double origin, double size)
    {
        Sector sector = map.AddSector();
        Vertex v1 = map.AddVertex(new Vector2D(origin, origin));
        Vertex v2 = map.AddVertex(new Vector2D(origin + size, origin));
        Vertex v3 = map.AddVertex(new Vector2D(origin + size, origin + size));
        Vertex v4 = map.AddVertex(new Vector2D(origin, origin + size));

        Linedef l1 = map.AddLinedef(v1, v2);
        Linedef l2 = map.AddLinedef(v2, v3);
        Linedef l3 = map.AddLinedef(v3, v4);
        Linedef l4 = map.AddLinedef(v4, v1);

        map.AddSidedef(l1, true, sector);
        map.AddSidedef(l2, true, sector);
        map.AddSidedef(l3, true, sector);
        map.AddSidedef(l4, true, sector);
        map.BuildIndexes();

        return sector;
    }
}
