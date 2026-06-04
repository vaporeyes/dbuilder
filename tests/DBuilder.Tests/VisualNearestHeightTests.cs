// ABOUTME: Tests UDB-style visual raise/lower-to-nearest height behavior.
// ABOUTME: Covers adjacent sector height resolution and within-selection alignment.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VisualNearestHeightTests
{
    [Fact]
    public void RaisesFloorToNearestAdjacentHeight()
    {
        Sector sector = Sector(0, 128);
        Sector neighbor = Sector(24, 96);
        LinkAdjacent(sector, neighbor);

        VisualNearestHeightResult result = VisualNearestHeight.Apply([FloorHit(sector)], raise: true, withinSelection: false);

        Assert.Equal(1, result.ChangedSurfaces);
        Assert.Equal(24, sector.FloorHeight);
    }

    [Fact]
    public void LowersCeilingToNearestAdjacentHeight()
    {
        Sector sector = Sector(0, 128);
        Sector neighbor = Sector(24, 96);
        LinkAdjacent(sector, neighbor);

        VisualNearestHeightResult result = VisualNearestHeight.Apply([CeilingHit(sector)], raise: false, withinSelection: false);

        Assert.Equal(1, result.ChangedSurfaces);
        Assert.Equal(96, sector.CeilHeight);
    }

    [Fact]
    public void RaisesSelectedFloorsWithinSelection()
    {
        Sector low = Sector(0, 128);
        Sector high = Sector(32, 128);

        VisualNearestHeightResult result = VisualNearestHeight.Apply(
            [FloorHit(low), FloorHit(high)],
            raise: true,
            withinSelection: true);

        Assert.Equal(1, result.ChangedSurfaces);
        Assert.Equal(32, low.FloorHeight);
        Assert.Equal(32, high.FloorHeight);
    }

    [Fact]
    public void LowersSelectedCeilingsWithinSelection()
    {
        Sector high = Sector(0, 128);
        Sector low = Sector(0, 96);

        VisualNearestHeightResult result = VisualNearestHeight.Apply(
            [CeilingHit(high), CeilingHit(low)],
            raise: false,
            withinSelection: true);

        Assert.Equal(1, result.ChangedSurfaces);
        Assert.Equal(96, high.CeilHeight);
        Assert.Equal(96, low.CeilHeight);
    }

    [Fact]
    public void WithinSelectionRequiresAtLeastTwoSelectedFloors()
    {
        Sector sector = Sector(0, 128);

        VisualNearestHeightResult result = VisualNearestHeight.Apply(
            [FloorHit(sector)],
            raise: true,
            withinSelection: true);

        Assert.Equal(0, result.ChangedSurfaces);
        Assert.Equal("Can't do: at least 2 selected floors are required!", result.Message);
        Assert.Equal(0, sector.FloorHeight);
    }

    [Fact]
    public void WithinSelectionReportsCombinedFloorAndCeilingRequirement()
    {
        Sector floor = Sector(0, 128);
        Sector ceiling = Sector(16, 96);

        VisualNearestHeightResult result = VisualNearestHeight.Apply(
            [FloorHit(floor), CeilingHit(ceiling)],
            raise: true,
            withinSelection: true);

        Assert.Equal(0, result.ChangedSurfaces);
        Assert.Equal("Can't do: at least 2 selected floors and ceilings are required!", result.Message);
        Assert.Equal(0, floor.FloorHeight);
        Assert.Equal(96, ceiling.CeilHeight);
    }

    [Fact]
    public void RaiseWithinSelectionRejectsFloorAboveLowestCeiling()
    {
        Sector blocked = Sector(0, 8);
        Sector high = Sector(16, 32);

        VisualNearestHeightResult result = VisualNearestHeight.Apply(
            [FloorHit(blocked), FloorHit(high)],
            raise: true,
            withinSelection: true);

        Assert.Equal(0, result.ChangedSurfaces);
        Assert.Equal(VisualNearestHeight.LowestCeilingBelowHighestFloorMessage, result.Message);
        Assert.Equal(0, blocked.FloorHeight);
        Assert.Equal(16, high.FloorHeight);
    }

    [Fact]
    public void LowerWithinSelectionRejectsCeilingBelowHighestFloor()
    {
        Sector low = Sector(0, 8);
        Sector blocked = Sector(16, 32);

        VisualNearestHeightResult result = VisualNearestHeight.Apply(
            [CeilingHit(low), CeilingHit(blocked)],
            raise: false,
            withinSelection: true);

        Assert.Equal(0, result.ChangedSurfaces);
        Assert.Equal(VisualNearestHeight.LowestCeilingBelowHighestFloorMessage, result.Message);
        Assert.Equal(8, low.CeilHeight);
        Assert.Equal(32, blocked.CeilHeight);
    }

    [Fact]
    public void RaisesAndLowersThingWithinContainingSector()
    {
        Sector sector = Sector(8, 72);
        var thing = new Thing(new Vector2D(0, 0), 1) { Sector = sector };

        VisualNearestHeight.Apply([ThingHit(thing)], raise: true, withinSelection: false);
        Assert.Equal(64, thing.Height);

        VisualNearestHeight.Apply([ThingHit(thing)], raise: false, withinSelection: false);
        Assert.Equal(0, thing.Height);
    }

    private static Sector Sector(int floor, int ceiling)
        => new() { FloorHeight = floor, CeilHeight = ceiling };

    private static void LinkAdjacent(Sector first, Sector second)
    {
        var line = new Linedef();
        var front = new Sidedef(line, isFront: true) { Sector = first };
        var back = new Sidedef(line, isFront: false) { Sector = second };
        line.AttachFront(front);
        line.AttachBack(back);
        front.Other = back;
        back.Other = front;
        first.Sidedefs.Add(front);
        second.Sidedefs.Add(back);
    }

    private static VisualHit FloorHit(Sector sector)
        => new(VisualHitKind.Floor, 1, new Vector3D(0, 0, sector.FloorHeight), sector, null, true, 0, 0);

    private static VisualHit CeilingHit(Sector sector)
        => new(VisualHitKind.Ceiling, 1, new Vector3D(0, 0, sector.CeilHeight), sector, null, true, 0, 0);

    private static VisualHit ThingHit(Thing thing)
        => new(VisualHitKind.Thing, 1, new Vector3D(thing.Position, thing.Height), null, null, true, 0, 0, Thing: thing);
}
