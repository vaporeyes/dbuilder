// ABOUTME: Tests UDB visual Reset Plane Slope action semantics for selected floor and ceiling planes.
// ABOUTME: Ensures reset clears explicit UDMF slope planes without changing sector heights.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VisualSlopeResetTests
{
    [Fact]
    public void EmptySelectionReportsUdbWarning()
    {
        VisualSlopeResetResult result = VisualSlopeReset.Reset([]);

        Assert.False(result.Changed);
        Assert.Equal(0, result.ChangedSurfaces);
        Assert.Equal(VisualSlopeReset.EmptySelectionMessage, result.StatusMessage);
    }

    [Fact]
    public void FloorSelectionClearsFloorSlopeOnly()
    {
        var sector = SlopedSector();

        VisualSlopeResetResult result = VisualSlopeReset.Reset(
            [new VisualSlopeResetTarget(sector, Ceiling: false)]);

        Assert.True(result.Changed);
        Assert.Equal(1, result.Floors);
        Assert.Equal(0, result.Ceilings);
        Assert.Equal("1 floor slopes reset.", result.StatusMessage);
        Assert.False(sector.HasFloorSlope);
        Assert.True(double.IsNaN(sector.FloorSlopeOffset));
        Assert.True(sector.HasCeilSlope);
        Assert.Equal(32, sector.FloorHeight);
    }

    [Fact]
    public void CeilingSelectionClearsCeilingSlopeOnly()
    {
        var sector = SlopedSector();

        VisualSlopeResetResult result = VisualSlopeReset.Reset(
            [new VisualSlopeResetTarget(sector, Ceiling: true)]);

        Assert.True(result.Changed);
        Assert.Equal(0, result.Floors);
        Assert.Equal(1, result.Ceilings);
        Assert.Equal("1 ceiling slopes reset.", result.StatusMessage);
        Assert.True(sector.HasFloorSlope);
        Assert.False(sector.HasCeilSlope);
        Assert.True(double.IsNaN(sector.CeilSlopeOffset));
        Assert.Equal(128, sector.CeilHeight);
    }

    [Fact]
    public void FloorAndCeilingSelectionReportsPlaneCount()
    {
        var sector = SlopedSector();

        VisualSlopeResetResult result = VisualSlopeReset.Reset(
        [
            new VisualSlopeResetTarget(sector, Ceiling: false),
            new VisualSlopeResetTarget(sector, Ceiling: true),
        ]);

        Assert.True(result.Changed);
        Assert.Equal(2, result.ChangedSurfaces);
        Assert.Equal("2 plane slopes reset.", result.StatusMessage);
        Assert.False(sector.HasFloorSlope);
        Assert.False(sector.HasCeilSlope);
    }

    [Fact]
    public void DuplicateTargetsOnlyResetOnce()
    {
        var sector = SlopedSector();

        VisualSlopeResetResult result = VisualSlopeReset.Reset(
        [
            new VisualSlopeResetTarget(sector, Ceiling: false),
            new VisualSlopeResetTarget(sector, Ceiling: false),
        ]);

        Assert.Equal(1, result.ChangedSurfaces);
        Assert.Equal("1 floor slopes reset.", result.StatusMessage);
    }

    private static Sector SlopedSector()
        => new()
        {
            FloorHeight = 32,
            CeilHeight = 128,
            FloorSlope = new Vector3D(0, -0.5, 1),
            FloorSlopeOffset = -32,
            CeilSlope = new Vector3D(0, 0.5, -1),
            CeilSlopeOffset = 128,
        };
}
