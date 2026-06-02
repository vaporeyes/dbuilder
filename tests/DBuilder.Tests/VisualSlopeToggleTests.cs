// ABOUTME: Tests UDB visual Toggle Slope action mutations for Plane_Align linedefs.
// ABOUTME: Covers wall assignment and floor or ceiling surface removal semantics.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VisualSlopeToggleTests
{
    [Fact]
    public void EmptySelectionReportsUdbWarning()
    {
        VisualSlopeToggleResult result = VisualSlopeToggle.Toggle([]);

        Assert.False(result.Changed);
        Assert.Equal(0, result.ChangedSurfaces);
        Assert.Equal(VisualSlopeToggle.EmptySelectionMessage, result.StatusMessage);
    }

    [Fact]
    public void LowerWallSelectionAssignsFloorPlaneAlignForTargetSide()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map);
        Sidedef side = sector.Sidedefs[0];

        VisualSlopeToggleResult result = VisualSlopeToggle.Toggle(
            [new VisualSlopeToggleTarget(Sidedef: side, Part: SidedefPart.Lower)]);

        Assert.True(result.Changed);
        Assert.Equal(1, result.ChangedSurfaces);
        Assert.Equal(VisualSlopeToggle.PlaneAlignAction, side.Line.Action);
        Assert.Equal(1, side.Line.Args[0]);
        Assert.Equal(0, side.Line.Args[1]);
    }

    [Fact]
    public void UpperWallSelectionAssignsCeilingPlaneAlignAndClearsExistingCeilingSource()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map);
        Sidedef oldSource = sector.Sidedefs[0];
        Sidedef newSource = sector.Sidedefs[1];
        oldSource.Line.Action = VisualSlopeToggle.PlaneAlignAction;
        oldSource.Line.Args[1] = 1;

        VisualSlopeToggleResult result = VisualSlopeToggle.Toggle(
            [new VisualSlopeToggleTarget(Sidedef: newSource, Part: SidedefPart.Upper)]);

        Assert.True(result.Changed);
        Assert.Equal(0, oldSource.Line.Action);
        Assert.Equal(1, oldSource.Line.Args[1]);
        Assert.Equal(VisualSlopeToggle.PlaneAlignAction, newSource.Line.Action);
        Assert.Equal(1, newSource.Line.Args[1]);
    }

    [Fact]
    public void FloorSurfaceSelectionRemovesFloorPlaneAlignOnly()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map);
        Sidedef side = sector.Sidedefs[0];
        side.Line.Action = VisualSlopeToggle.PlaneAlignAction;
        side.Line.Args[0] = 1;
        side.Line.Args[1] = 1;

        VisualSlopeToggleResult result = VisualSlopeToggle.Toggle(
            [new VisualSlopeToggleTarget(Sector: sector, Floor: true)]);

        Assert.True(result.Changed);
        Assert.Equal(VisualSlopeToggle.PlaneAlignAction, side.Line.Action);
        Assert.Equal(0, side.Line.Args[0]);
        Assert.Equal(1, side.Line.Args[1]);
    }

    [Fact]
    public void CeilingSurfaceSelectionRemovesActionWhenOnlyCeilingWasAligned()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map);
        Sidedef side = sector.Sidedefs[0];
        side.Line.Action = VisualSlopeToggle.PlaneAlignAction;
        side.Line.Args[1] = 1;

        VisualSlopeToggleResult result = VisualSlopeToggle.Toggle(
            [new VisualSlopeToggleTarget(Sector: sector, Ceiling: true)]);

        Assert.True(result.Changed);
        Assert.Equal(0, side.Line.Action);
        Assert.Equal(1, side.Line.Args[1]);
    }

    [Fact]
    public void MiddleWallSelectionDoesNotToggleSlope()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map);
        Sidedef side = sector.Sidedefs[0];

        VisualSlopeToggleResult result = VisualSlopeToggle.Toggle(
            [new VisualSlopeToggleTarget(Sidedef: side, Part: SidedefPart.Middle)]);

        Assert.False(result.Changed);
        Assert.Equal("Toggled Slope for 0 surfaces.", result.StatusMessage);
        Assert.Equal(0, side.Line.Action);
    }

    private static Sector AddSquareSector(MapSet map)
    {
        Sector sector = map.AddSector();
        Vertex v1 = map.AddVertex(new Vector2D(0, 0));
        Vertex v2 = map.AddVertex(new Vector2D(64, 0));
        Vertex v3 = map.AddVertex(new Vector2D(64, 64));
        Vertex v4 = map.AddVertex(new Vector2D(0, 64));

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
