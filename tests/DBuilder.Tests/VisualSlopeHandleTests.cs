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
    public void SmartVertexPivotUsesFarthestSameLevelVertexHandle()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        VisualSlopeHandle handle = VisualSlopeHandles.CreateVertex(sector.Sidedefs[0].Line.Start, sector, level);
        VisualSlopeHandle near = VisualSlopeHandles.CreateVertex(sector.Sidedefs[0].Line.End, sector, level);
        VisualSlopeHandle far = VisualSlopeHandles.CreateVertex(sector.Sidedefs[2].Line.Start, sector, level);

        VisualSlopeHandle? pivot = VisualSlopeHandles.GetSmartVertexPivot(handle, [handle, near, far]);

        Assert.Same(far, pivot);
    }

    [Fact]
    public void SmartVertexPivotCanUseOppositeTriangularLineHandle()
    {
        var map = new MapSet();
        Sector sector = AddTriangleSector(map);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        Vertex vertex = sector.Sidedefs[0].Line.Start;
        VisualSlopeHandle handle = VisualSlopeHandles.CreateVertex(vertex, sector, level);
        VisualSlopeHandle near = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[0], level, up: true);
        VisualSlopeHandle opposite = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[1], level, up: true);

        VisualSlopeHandle? pivot = VisualSlopeHandles.GetSmartVertexPivot(
            handle,
            [handle, near, opposite],
            useOppositeLineHandle: true);

        Assert.Same(opposite, pivot);
    }

    [Fact]
    public void AdjacentVertexSlopeHandlesMatchSameVertexAndRoundedHeight()
    {
        var map = new MapSet();
        Sector first = AddSquareSector(map, 0, 64);
        Sector second = AddAdjacentSquareSector(map, first);
        Vertex shared = first.Sidedefs[1].Line.End;
        var firstLevel = new VisualSlopeLevel(
            first,
            VisualSlopeLevelType.Floor,
            new Plane(new Vector3D(0, 0, 1), 0));
        var secondLevel = new VisualSlopeLevel(
            second,
            VisualSlopeLevelType.Floor,
            new Plane(new Vector3D(0, 0, 1), -0.000001));
        var otherHeightLevel = new VisualSlopeLevel(
            second,
            VisualSlopeLevelType.Floor,
            new Plane(new Vector3D(0, 0, 1), -16));
        VisualSlopeHandle handle = VisualSlopeHandles.CreateVertex(shared, first, firstLevel);
        VisualSlopeHandle adjacent = VisualSlopeHandles.CreateVertex(shared, second, secondLevel);
        VisualSlopeHandle otherHeight = VisualSlopeHandles.CreateVertex(shared, second, otherHeightLevel);

        IReadOnlyList<VisualSlopeHandle> selected = VisualSlopeHandles.GetAdjacentVertexSlopeHandles(
            handle,
            [handle, adjacent, otherHeight]);

        VisualSlopeHandle result = Assert.Single(selected);
        Assert.Same(adjacent, result);
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

    private static Sector AddTriangleSector(MapSet map)
    {
        Sector sector = map.AddSector();
        Vertex v1 = map.AddVertex(new Vector2D(0, 0));
        Vertex v2 = map.AddVertex(new Vector2D(64, 0));
        Vertex v3 = map.AddVertex(new Vector2D(0, 64));

        Linedef l1 = map.AddLinedef(v1, v2);
        Linedef l2 = map.AddLinedef(v2, v3);
        Linedef l3 = map.AddLinedef(v3, v1);

        map.AddSidedef(l1, true, sector);
        map.AddSidedef(l2, true, sector);
        map.AddSidedef(l3, true, sector);
        map.BuildIndexes();

        return sector;
    }

    private static Sector AddAdjacentSquareSector(MapSet map, Sector first)
    {
        Sector sector = map.AddSector();
        Vertex v1 = first.Sidedefs[1].Line.End;
        Vertex v2 = first.Sidedefs[1].Line.Start;
        Vertex v3 = map.AddVertex(new Vector2D(128, 64));
        Vertex v4 = map.AddVertex(new Vector2D(128, 0));

        Linedef l1 = first.Sidedefs[1].Line;
        Linedef l2 = map.AddLinedef(v2, v4);
        Linedef l3 = map.AddLinedef(v4, v3);
        Linedef l4 = map.AddLinedef(v3, v1);

        map.AddSidedef(l1, false, sector);
        map.AddSidedef(l2, true, sector);
        map.AddSidedef(l3, true, sector);
        map.AddSidedef(l4, true, sector);
        map.BuildIndexes();

        return sector;
    }
}
