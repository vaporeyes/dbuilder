// ABOUTME: Tests UDB-style thing absolute-Z calculation against sector-relative thing metadata.
// ABOUTME: Covers absolute, floor-relative, ceiling-hanging, and no-sector fallbacks.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ThingGeometryToolsTests
{
    [Fact]
    public void AbsoluteZUsesThingHeightDirectly()
    {
        var thing = new Thing(new Vector2D(0, 0), 1) { Height = 42, Sector = Sector(0, 128) };
        var info = new ThingTypeInfo { AbsoluteZ = true, Height = 16 };

        Assert.Equal(42, ThingGeometryTools.GetThingAbsoluteZ(thing, info));
    }

    [Fact]
    public void FloorRelativeThingAddsSectorFloor()
    {
        var thing = new Thing(new Vector2D(0, 0), 1) { Height = 8, Sector = Sector(24, 128) };
        var info = new ThingTypeInfo { Height = 16 };

        Assert.Equal(32, ThingGeometryTools.GetThingAbsoluteZ(thing, info));
    }

    [Fact]
    public void HangingThingSubtractsOffsetAndThingHeightFromCeiling()
    {
        var thing = new Thing(new Vector2D(0, 0), 1) { Height = 12, Sector = Sector(0, 128) };
        var info = new ThingTypeInfo { Hangs = true, Height = 56 };

        Assert.Equal(60, ThingGeometryTools.GetThingAbsoluteZ(thing, info));
    }

    [Fact]
    public void MissingSectorFallsBackToThingHeight()
    {
        var thing = new Thing(new Vector2D(0, 0), 1) { Height = 19 };
        var info = new ThingTypeInfo { Height = 16 };

        Assert.Equal(19, ThingGeometryTools.GetThingAbsoluteZ(thing, info));
    }

    private static Sector Sector(int floor, int ceiling)
        => new() { FloorHeight = floor, CeilHeight = ceiling };
}
