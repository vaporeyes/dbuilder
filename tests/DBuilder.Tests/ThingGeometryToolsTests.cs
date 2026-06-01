// ABOUTME: Tests UDB-style thing absolute-Z calculation against sector-relative thing metadata.
// ABOUTME: Covers absolute, floor-relative, ceiling-hanging, and no-sector fallbacks.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ThingGeometryToolsTests
{
    [Fact]
    public void AlignThingToOneSidedFrontWallMovesToLineAndRotates()
    {
        var (_, line, front) = OneSidedLine(0, 128);
        var thing = new Thing(new Vector2D(64, -16), 1) { Sector = front };
        var info = new ThingTypeInfo { Height = 16 };

        bool aligned = ThingGeometryTools.TryAlignThingToLine(thing, line, info);

        Assert.True(aligned);
        Assert.Equal(64, thing.Position.x);
        Assert.Equal(-1, thing.Position.y);
        Assert.Equal(270, thing.Angle);
    }

    [Fact]
    public void AlignThingToOneSidedWallRequiresVerticalOverlap()
    {
        var (_, line, front) = OneSidedLine(0, 16);
        var thing = new Thing(new Vector2D(64, -16), 1) { Height = 32, Sector = front };
        var info = new ThingTypeInfo { Height = 16 };

        bool aligned = ThingGeometryTools.TryAlignThingToLine(thing, line, info);

        Assert.False(aligned);
        Assert.Equal(new Vector2D(64, -16), thing.Position);
        Assert.Equal(0, thing.Angle);
    }

    [Fact]
    public void AlignThingToTwoSidedWallUsesLowerHeightGap()
    {
        var (_, line, front, _) = TwoSidedLine(0, 128, 32, 128);
        var thing = new Thing(new Vector2D(64, -16), 1) { Sector = front };
        var info = new ThingTypeInfo { Height = 16 };

        bool aligned = ThingGeometryTools.TryAlignThingToLine(thing, line, info);

        Assert.True(aligned);
        Assert.Equal(new Vector2D(64, -1), thing.Position);
        Assert.Equal(270, thing.Angle);
    }

    [Fact]
    public void AlignThingAlreadyOnTwoSidedLineOnlyRotates()
    {
        var (_, line, front, _) = TwoSidedLine(0, 128, 0, 128);
        var thing = new Thing(new Vector2D(64, 0), 1) { Sector = front };
        var info = new ThingTypeInfo { Height = 16 };

        bool aligned = ThingGeometryTools.TryAlignThingToLine(thing, line, info);

        Assert.True(aligned);
        Assert.Equal(new Vector2D(64, 0), thing.Position);
        Assert.Equal(270, thing.Angle);
    }

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

    private static (MapSet Map, Linedef Line, Sector Front) OneSidedLine(int floor, int ceiling)
    {
        var map = BaseLineMap();
        var sector = map.AddSector();
        sector.FloorHeight = floor;
        sector.CeilHeight = ceiling;
        map.AddSidedef(map.Linedefs[0], isFront: true, sector);
        map.BuildIndexes();
        return (map, map.Linedefs[0], sector);
    }

    private static (MapSet Map, Linedef Line, Sector Front, Sector Back) TwoSidedLine(
        int frontFloor,
        int frontCeil,
        int backFloor,
        int backCeil)
    {
        var map = BaseLineMap();
        var front = map.AddSector();
        front.FloorHeight = frontFloor;
        front.CeilHeight = frontCeil;
        var back = map.AddSector();
        back.FloorHeight = backFloor;
        back.CeilHeight = backCeil;
        var line = map.Linedefs[0];
        map.AddSidedef(line, isFront: true, front);
        map.AddSidedef(line, isFront: false, back);
        map.BuildIndexes();
        return (map, line, front, back);
    }

    private static MapSet BaseLineMap()
    {
        var map = new MapSet();
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(128, 0));
        map.AddLinedef(start, end);
        return map;
    }
}
