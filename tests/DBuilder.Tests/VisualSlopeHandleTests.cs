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
    public void ToggleSelectionRejectsPivotHandlesLikeUdb()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        VisualSlopeHandle pivot = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[0], level, up: true) with { Pivot = true };
        VisualSlopeHandle other = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[1], level, up: true);

        VisualSlopeHandleStateResult result = VisualSlopeHandles.ToggleSelection(pivot, [pivot, other]);

        Assert.Equal(VisualSlopeHandles.CannotSelectPivotMessage, result.WarningMessage);
        Assert.False(result.Handles[0].Selected);
        Assert.True(result.Handles[0].Pivot);
        Assert.Same(other, result.Handles[1]);
    }

    [Fact]
    public void ToggleSelectionFlipsNonPivotSelectedStateLikeUdb()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        VisualSlopeHandle handle = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[0], level, up: true);

        VisualSlopeHandleStateResult selected = VisualSlopeHandles.ToggleSelection(handle, [handle]);
        VisualSlopeHandleStateResult deselected = VisualSlopeHandles.ToggleSelection(selected.Handles[0], selected.Handles);

        Assert.Null(selected.WarningMessage);
        Assert.True(selected.Handles[0].Selected);
        Assert.Null(deselected.WarningMessage);
        Assert.False(deselected.Handles[0].Selected);
    }

    [Fact]
    public void ToggleSelectionCanSelectAdjacentSameHeightVertexHandlesLikeUdb()
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

        VisualSlopeHandleStateResult result = VisualSlopeHandles.ToggleSelection(
            handle,
            [handle, adjacent, otherHeight],
            selectAdjacentVertexHandles: true);

        Assert.Null(result.WarningMessage);
        Assert.True(result.Handles[0].Selected);
        Assert.True(result.Handles[1].Selected);
        Assert.False(result.Handles[2].Selected);
    }

    [Fact]
    public void ToggleSelectionCanDeselectAdjacentSameHeightVertexHandlesLikeUdb()
    {
        var map = new MapSet();
        Sector first = AddSquareSector(map, 0, 64);
        Sector second = AddAdjacentSquareSector(map, first);
        Vertex shared = first.Sidedefs[1].Line.End;
        VisualSlopeLevel level = VisualSlopeLevel.Floor(first);
        VisualSlopeLevel adjacentLevel = VisualSlopeLevel.Floor(second);
        VisualSlopeHandle handle = VisualSlopeHandles.CreateVertex(shared, first, level) with { Selected = true };
        VisualSlopeHandle adjacent = VisualSlopeHandles.CreateVertex(shared, second, adjacentLevel) with { Selected = true };

        VisualSlopeHandleStateResult result = VisualSlopeHandles.ToggleSelection(
            handle,
            [handle, adjacent],
            selectAdjacentVertexHandles: true);

        Assert.Null(result.WarningMessage);
        Assert.False(result.Handles[0].Selected);
        Assert.False(result.Handles[1].Selected);
    }

    [Fact]
    public void ToggleSelectionRejectsPivotBeforeAdjacentVertexSelectionLikeUdb()
    {
        var map = new MapSet();
        Sector first = AddSquareSector(map, 0, 64);
        Sector second = AddAdjacentSquareSector(map, first);
        Vertex shared = first.Sidedefs[1].Line.End;
        VisualSlopeHandle pivot = VisualSlopeHandles.CreateVertex(shared, first, VisualSlopeLevel.Floor(first)) with { Pivot = true };
        VisualSlopeHandle adjacent = VisualSlopeHandles.CreateVertex(shared, second, VisualSlopeLevel.Floor(second));

        VisualSlopeHandleStateResult result = VisualSlopeHandles.ToggleSelection(
            pivot,
            [pivot, adjacent],
            selectAdjacentVertexHandles: true);

        Assert.Equal(VisualSlopeHandles.CannotSelectPivotMessage, result.WarningMessage);
        Assert.False(result.Handles[0].Selected);
        Assert.False(result.Handles[1].Selected);
    }

    [Fact]
    public void TogglePivotKeepsOnlyOnePivotLikeUdb()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        VisualSlopeHandle target = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[0], level, up: true);
        VisualSlopeHandle oldPivot = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[1], level, up: true) with { Pivot = true };
        VisualSlopeHandle smartPivot = VisualSlopeHandles.CreateVertex(sector.Sidedefs[2].Line.Start, sector, level) with { SmartPivot = true };

        VisualSlopeHandleStateResult result = VisualSlopeHandles.TogglePivot(target, [target, oldPivot, smartPivot]);

        Assert.Null(result.WarningMessage);
        Assert.True(result.Handles[0].Pivot);
        Assert.False(result.Handles[1].Pivot);
        Assert.True(result.Handles[2].SmartPivot);
    }

    [Fact]
    public void TogglePivotRejectsSelectedTargetAndClearsOtherPivotsLikeUdb()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        VisualSlopeHandle selected = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[0], level, up: true) with { Selected = true };
        VisualSlopeHandle oldPivot = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[1], level, up: true) with { Pivot = true };

        VisualSlopeHandleStateResult result = VisualSlopeHandles.TogglePivot(selected, [selected, oldPivot]);

        Assert.Equal(VisualSlopeHandles.CannotPivotSelectedMessage, result.WarningMessage);
        Assert.True(result.Handles[0].Selected);
        Assert.False(result.Handles[0].Pivot);
        Assert.False(result.Handles[1].Pivot);
    }

    [Fact]
    public void UsedHandlesKeepSelectedPivotAndSmartPivotStateLikeUdb()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        VisualSlopeHandle ordinary = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[0], level, up: true);
        VisualSlopeHandle selected = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[1], level, up: true) with { Selected = true };
        VisualSlopeHandle pivot = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[2], level, up: true) with { Pivot = true };
        VisualSlopeHandle smartPivot = VisualSlopeHandles.CreateVertex(
            sector.Sidedefs[3].Line.Start,
            sector,
            level) with { SmartPivot = true };

        IReadOnlyList<VisualSlopeHandle> used = VisualSlopeHandles.GetUsedHandles([ordinary, selected, pivot, smartPivot]);

        Assert.Equal(3, used.Count);
        Assert.DoesNotContain(ordinary, used);
        Assert.Contains(selected, used);
        Assert.Contains(pivot, used);
        Assert.Contains(smartPivot, used);
    }

    [Fact]
    public void UsedHandlesDropDeselectedNonPivotHandlesLikeUdb()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        VisualSlopeHandle handle = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[0], level, up: true) with { Selected = true };
        VisualSlopeHandleStateResult deselected = VisualSlopeHandles.ToggleSelection(handle, [handle]);

        IReadOnlyList<VisualSlopeHandle> used = VisualSlopeHandles.GetUsedHandles(deselected.Handles);

        Assert.Empty(used);
    }

    [Fact]
    public void UpdateTargetReplacesStaleSmartPivotAndKeepsNewTargetLikeUdb()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        VisualSlopeHandle oldTarget = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[1], level, up: true);
        VisualSlopeHandle staleSmartPivot = VisualSlopeHandles.CreateVertex(
            sector.Sidedefs[0].Line.Start,
            sector,
            level) with { SmartPivot = true };
        VisualSlopeHandle newTarget = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[0], level, up: true);
        VisualSlopeHandle expectedSmartPivot = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[2], level, up: true);

        VisualSlopeTargetStateResult result = VisualSlopeHandles.UpdateTarget(
            oldTarget,
            newTarget,
            [oldTarget, staleSmartPivot, newTarget, expectedSmartPivot]);

        VisualSlopeHandle staleResult = Assert.Single(result.Handles, handle => ReferenceEquals(handle.Vertex, staleSmartPivot.Vertex));
        Assert.False(staleResult.SmartPivot);
        Assert.NotNull(result.PickedHandle);
        Assert.NotNull(result.SmartPivotHandle);
        Assert.True(result.SmartPivotHandle.SmartPivot);
        Assert.Same(newTarget.Sidedef, result.PickedHandle.Sidedef);
        Assert.Same(expectedSmartPivot.Sidedef, result.SmartPivotHandle.Sidedef);
        Assert.Contains(result.PickedHandle, result.UsedHandles);
        Assert.Contains(result.SmartPivotHandle, result.UsedHandles);
        Assert.DoesNotContain(result.UsedHandles, handle => ReferenceEquals(handle.Sidedef, oldTarget.Sidedef));
        Assert.DoesNotContain(result.UsedHandles, handle => ReferenceEquals(handle.Vertex, staleSmartPivot.Vertex));
    }

    [Fact]
    public void UpdateTargetRetainsSelectedOldTargetAndClearsStaleSmartPivotLikeUdb()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        VisualSlopeHandle selectedOldTarget = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[0], level, up: true) with { Selected = true };
        VisualSlopeHandle staleSmartPivot = VisualSlopeHandles.CreateVertex(
            sector.Sidedefs[2].Line.Start,
            sector,
            level) with { SmartPivot = true };

        VisualSlopeTargetStateResult result = VisualSlopeHandles.UpdateTarget(
            selectedOldTarget,
            null,
            [selectedOldTarget, staleSmartPivot]);

        VisualSlopeHandle selectedResult = Assert.Single(result.UsedHandles);
        VisualSlopeHandle staleResult = Assert.Single(result.Handles, handle => ReferenceEquals(handle.Vertex, staleSmartPivot.Vertex));
        Assert.Same(selectedOldTarget.Sidedef, selectedResult.Sidedef);
        Assert.True(selectedResult.Selected);
        Assert.False(staleResult.SmartPivot);
        Assert.Null(result.PickedHandle);
        Assert.Null(result.SmartPivotHandle);
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
    public void HeightChangeIncludesHandleLevelWhenSelectedLevelsOmitItLikeUdb()
    {
        var map = new MapSet();
        Sector source = AddSquareSector(map, 0, 64);
        Sector selected = AddSquareSector(map, 0, 64);
        VisualSlopeLevel sourceLevel = VisualSlopeLevel.Floor(source);
        VisualSlopeLevel selectedLevel = VisualSlopeLevel.Floor(selected);
        VisualSlopeHandle handle = VisualSlopeHandles.CreateSidedef(source.Sidedefs[0], sourceLevel, up: true);
        VisualSlopeHandle pivot = VisualSlopeHandles.CreateSidedef(source.Sidedefs[2], sourceLevel, up: true);

        VisualSlopeChangeResult result = VisualSlopeHandles.ChangeTargetHeight(handle, pivot, 16, [selectedLevel]);

        Assert.Equal(VisualSlopeChangeResult.Changed, result);
        Assert.True(source.HasFloorSlope);
        Assert.True(selected.HasFloorSlope);
        Assert.Equal(16, source.GetFloorZ(new Vector2D(32, 0)), 1e-9);
        Assert.Equal(16, selected.GetFloorZ(new Vector2D(32, 0)), 1e-9);
    }

    [Fact]
    public void VertexHeightChangeFiltersDisposedSelectedLevelsBeforeAddingHandleLevel()
    {
        var map = new MapSet();
        Sector source = AddSquareSector(map, 0, 64);
        Sector disposed = AddSquareSector(map, 256, 64);
        disposed.IsDisposed = true;
        VisualSlopeLevel sourceLevel = VisualSlopeLevel.Floor(source);
        VisualSlopeLevel disposedLevel = VisualSlopeLevel.Floor(disposed);
        Vertex vertex = source.Sidedefs[0].Line.Start;
        VisualSlopeHandle handle = VisualSlopeHandles.CreateVertex(vertex, source, sourceLevel);
        VisualSlopeHandle pivot = VisualSlopeHandles.CreateSidedef(source.Sidedefs[1], sourceLevel, up: true);

        VisualSlopeChangeResult result = VisualSlopeHandles.ChangeTargetHeight(handle, pivot, 16, [disposedLevel]);

        Assert.Equal(VisualSlopeChangeResult.Changed, result);
        Assert.True(source.HasFloorSlope);
        Assert.False(disposed.HasFloorSlope);
        Assert.Equal(16, source.GetFloorZ(vertex.Position), 1e-9);
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
    public void SmartVertexPivotUsesSelectedVisualLevelsLikeUdb()
    {
        var map = new MapSet();
        Sector source = AddSquareSector(map, 0, 64);
        Sector selected = AddSquareSector(map, 256, 64);
        VisualSlopeLevel sourceLevel = VisualSlopeLevel.Floor(source);
        VisualSlopeLevel selectedLevel = VisualSlopeLevel.Floor(selected);
        VisualSlopeHandle handle = VisualSlopeHandles.CreateVertex(source.Sidedefs[0].Line.Start, source, sourceLevel);
        VisualSlopeHandle sameSector = VisualSlopeHandles.CreateVertex(source.Sidedefs[0].Line.End, source, sourceLevel);
        VisualSlopeHandle selectedNear = VisualSlopeHandles.CreateVertex(selected.Sidedefs[0].Line.Start, selected, selectedLevel);
        VisualSlopeHandle selectedFar = VisualSlopeHandles.CreateVertex(selected.Sidedefs[2].Line.Start, selected, selectedLevel);

        VisualSlopeHandle? pivot = VisualSlopeHandles.GetSmartVertexPivot(
            handle,
            [handle, sameSector, selectedNear, selectedFar],
            selectedLevels: [selectedLevel]);

        Assert.Same(selectedFar, pivot);
    }

    [Fact]
    public void SmartSidedefPivotUsesClosestAngleThenFarthestSameLevelLineHandle()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        VisualSlopeHandle handle = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[0], level, up: true);
        VisualSlopeHandle perpendicular = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[1], level, up: true);
        VisualSlopeHandle parallel = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[2], level, up: true);
        VisualSlopeHandle otherParallel = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[3], level, up: true);

        VisualSlopeHandle? pivot = VisualSlopeHandles.GetSmartSidedefPivot(
            handle,
            [handle, perpendicular, parallel, otherParallel]);

        Assert.Same(parallel, pivot);
    }

    [Fact]
    public void SmartSidedefPivotCanUseOppositeTriangularVertexHandle()
    {
        var map = new MapSet();
        Sector sector = AddTriangleSector(map);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        VisualSlopeHandle handle = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[0], level, up: true);
        VisualSlopeHandle nearVertex = VisualSlopeHandles.CreateVertex(sector.Sidedefs[0].Line.Start, sector, level);
        VisualSlopeHandle oppositeVertex = VisualSlopeHandles.CreateVertex(sector.Sidedefs[1].Line.End, sector, level);

        VisualSlopeHandle? pivot = VisualSlopeHandles.GetSmartSidedefPivot(
            handle,
            [handle, nearVertex, oppositeVertex],
            useOppositeVertexHandle: true);

        Assert.Same(oppositeVertex, pivot);
    }

    [Fact]
    public void SmartSidedefPivotUsesSelectedVisualLevelsLikeUdb()
    {
        var map = new MapSet();
        Sector source = AddSquareSector(map, 0, 64);
        Sector selected = AddSquareSector(map, 256, 64);
        VisualSlopeLevel sourceLevel = VisualSlopeLevel.Floor(source);
        VisualSlopeLevel selectedLevel = VisualSlopeLevel.Floor(selected);
        VisualSlopeHandle handle = VisualSlopeHandles.CreateSidedef(source.Sidedefs[0], sourceLevel, up: true);
        VisualSlopeHandle sameSector = VisualSlopeHandles.CreateSidedef(source.Sidedefs[2], sourceLevel, up: true);
        VisualSlopeHandle selectedPerpendicular = VisualSlopeHandles.CreateSidedef(selected.Sidedefs[1], selectedLevel, up: true);
        VisualSlopeHandle selectedParallel = VisualSlopeHandles.CreateSidedef(selected.Sidedefs[2], selectedLevel, up: true);

        VisualSlopeHandle? pivot = VisualSlopeHandles.GetSmartSidedefPivot(
            handle,
            [handle, sameSector, selectedPerpendicular, selectedParallel],
            selectedLevels: [selectedLevel]);

        Assert.Same(selectedParallel, pivot);
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

    [Fact]
    public void ApplySlopeBetweenHandlesUsesFirstHandleLineAndSecondHandleCenter()
    {
        var map = new MapSet();
        Sector target = AddSquareSector(map, 0, 64);
        VisualSlopeHandle sourceHandle = AddLineHandle(map, new Vector2D(0, 64), new Vector2D(64, 64), 16);
        VisualSlopeHandle pivotHandle = AddLineHandle(map, new Vector2D(0, 0), new Vector2D(64, 0), 0);

        VisualSlopeBetweenHandlesApplyResult result = VisualSlopeHandles.ApplySlopeBetweenHandles(
            [VisualSlopeLevel.Floor(target)],
            [sourceHandle, pivotHandle]);

        Assert.Equal(VisualSlopeBetweenHandlesResult.Changed, result.Result);
        Assert.Equal(1, result.ChangedLevels);
        Assert.Equal("Sloped between slope handles.", result.StatusMessage);
        Assert.True(target.HasFloorSlope);
        Assert.Equal(16, target.GetFloorZ(new Vector2D(32, 64)), 1e-9);
        Assert.Equal(0, target.GetFloorZ(new Vector2D(32, 0)), 1e-9);
    }

    [Fact]
    public void ApplySlopeBetweenHandlesValidatesSelectionAndHandlePairLikeUdb()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        VisualSlopeHandle lineHandle = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[0], level, up: true);
        VisualSlopeHandle vertexHandle = VisualSlopeHandles.CreateVertex(sector.Sidedefs[0].Line.Start, sector, level);

        VisualSlopeBetweenHandlesApplyResult noSelection = VisualSlopeHandles.ApplySlopeBetweenHandles(
            [],
            [lineHandle, lineHandle]);
        VisualSlopeBetweenHandlesApplyResult oneHandle = VisualSlopeHandles.ApplySlopeBetweenHandles(
            [level],
            [lineHandle]);
        VisualSlopeBetweenHandlesApplyResult vertex = VisualSlopeHandles.ApplySlopeBetweenHandles(
            [level],
            [lineHandle, vertexHandle]);

        Assert.Equal(VisualSlopeBetweenHandlesResult.MissingSelectedLevels, noSelection.Result);
        Assert.Equal(VisualSlopeHandles.MissingSelectedLevelsMessage, noSelection.StatusMessage);
        Assert.Equal(VisualSlopeBetweenHandlesResult.MissingHandlePair, oneHandle.Result);
        Assert.Equal(VisualSlopeHandles.MissingHandlePairMessage, oneHandle.StatusMessage);
        Assert.Equal(VisualSlopeBetweenHandlesResult.UnsupportedHandleKind, vertex.Result);
        Assert.Equal(VisualSlopeHandles.UnsupportedHandleKindMessage, vertex.StatusMessage);
    }

    [Fact]
    public void ApplyArchBetweenHandlesUsesUdbDefaultThetaAndOffset()
    {
        var map = new MapSet();
        Sector left = AddRectSector(map, 0, 0, 50, 10);
        Sector right = AddRectSector(map, 50, 0, 100, 10);
        VisualSlopeHandle handle1 = AddLineHandle(map, new Vector2D(-10, 0), new Vector2D(10, 0), 128);
        VisualSlopeHandle handle2 = AddLineHandle(map, new Vector2D(90, 0), new Vector2D(110, 0), 128);

        VisualSlopeBetweenHandlesApplyResult result = VisualSlopeHandles.ApplyArchBetweenHandles(
            [VisualSlopeLevel.Floor(left), VisualSlopeLevel.Floor(right)],
            [handle1, handle2]);

        Assert.Equal(VisualSlopeBetweenHandlesResult.Changed, result.Result);
        Assert.Equal(2, result.ChangedLevels);
        Assert.Equal("Arched between slope handles.", result.StatusMessage);
        Assert.True(left.HasFloorSlope);
        Assert.True(right.HasFloorSlope);
        Assert.Equal(128.0, left.GetFloorZ(new Vector2D(0, 5)), 2);
        Assert.Equal(178.0, left.GetFloorZ(new Vector2D(50, 5)), 1);
        Assert.Equal(128.0, right.GetFloorZ(new Vector2D(100, 5)), 1);
    }

    [Fact]
    public void ApplyArchBetweenHandlesCanApplyCeilingLevels()
    {
        var map = new MapSet();
        Sector target = AddRectSector(map, 0, 0, 50, 10);
        Sector second = AddRectSector(map, 50, 0, 100, 10);
        VisualSlopeHandle handle1 = AddLineHandle(map, new Vector2D(-10, 0), new Vector2D(10, 0), 256, ceiling: true);
        VisualSlopeHandle handle2 = AddLineHandle(map, new Vector2D(90, 0), new Vector2D(110, 0), 256, ceiling: true);

        VisualSlopeBetweenHandlesApplyResult result = VisualSlopeHandles.ApplyArchBetweenHandles(
            [VisualSlopeLevel.Ceiling(target), VisualSlopeLevel.Ceiling(second)],
            [handle1, handle2],
            heightOffset: 16);

        Assert.Equal(VisualSlopeBetweenHandlesResult.Changed, result.Result);
        Assert.Equal(2, result.ChangedLevels);
        Assert.True(target.HasCeilSlope);
        Assert.False(target.HasFloorSlope);
        Assert.Equal(272.0, target.GetCeilZ(new Vector2D(0, 5)), 2);
        Assert.Equal(322.0, target.GetCeilZ(new Vector2D(50, 5)), 1);
    }

    [Fact]
    public void ApplyArchBetweenHandlesRequiresTwoSelectedLevels()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        VisualSlopeLevel level = VisualSlopeLevel.Floor(sector);
        VisualSlopeHandle handle = VisualSlopeHandles.CreateSidedef(sector.Sidedefs[0], level, up: true);

        VisualSlopeBetweenHandlesApplyResult result = VisualSlopeHandles.ApplyArchBetweenHandles(
            [level],
            [handle, handle]);

        Assert.Equal(VisualSlopeBetweenHandlesResult.MissingSelectedLevels, result.Result);
        Assert.Equal(VisualSlopeHandles.MissingArchSelectedLevelsMessage, result.StatusMessage);
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

    private static Sector AddRectSector(MapSet map, double left, double top, double right, double bottom)
    {
        Sector sector = map.AddSector();
        sector.FloorHeight = 0;
        sector.CeilHeight = 128;

        Vertex a = map.AddVertex(new Vector2D(left, top));
        Vertex b = map.AddVertex(new Vector2D(right, top));
        Vertex c = map.AddVertex(new Vector2D(right, bottom));
        Vertex d = map.AddVertex(new Vector2D(left, bottom));

        map.AddSidedef(map.AddLinedef(a, b), true, sector);
        map.AddSidedef(map.AddLinedef(b, c), true, sector);
        map.AddSidedef(map.AddLinedef(c, d), true, sector);
        map.AddSidedef(map.AddLinedef(d, a), true, sector);
        map.BuildIndexes();
        return sector;
    }

    private static VisualSlopeHandle AddLineHandle(MapSet map, Vector2D start, Vector2D end, int height, bool ceiling = false)
    {
        Sector sector = map.AddSector();
        sector.FloorHeight = height;
        sector.CeilHeight = height;
        Linedef line = map.AddLinedef(map.AddVertex(start), map.AddVertex(end));
        Sidedef side = map.AddSidedef(line, true, sector);
        var level = new VisualSlopeLevel(
            sector,
            ceiling ? VisualSlopeLevelType.Ceiling : VisualSlopeLevelType.Floor,
            new Plane(new Vector3D(0, 0, 1), -height));
        map.BuildIndexes();
        return VisualSlopeHandles.CreateSidedef(side, level, up: true);
    }
}
