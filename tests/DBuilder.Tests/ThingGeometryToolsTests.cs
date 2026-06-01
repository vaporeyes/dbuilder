// ABOUTME: Tests UDB-style thing absolute-Z calculation against sector-relative thing metadata.
// ABOUTME: Covers absolute, floor-relative, ceiling-hanging, and no-sector fallbacks.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ThingGeometryToolsTests
{
    [Fact]
    public void AlignSelectedThingsToNearestWallsSkipsNonAlignableThings()
    {
        GameConfiguration config = ThingConfig();
        var (map, line, front) = OneSidedLine(0, 128);
        var alignable = map.AddThing(new Vector2D(64, -16), 31010);
        var normal = map.AddThing(new Vector2D(96, -16), 2);
        alignable.Selected = true;
        normal.Selected = true;
        alignable.Sector = front;
        normal.Sector = front;

        ThingWallAlignmentResult result = ThingWallAlignment.AlignSelectedToNearestWalls(map, config);

        Assert.Equal(2, result.SelectedCount);
        Assert.Equal(1, result.EligibleCount);
        Assert.Equal(1, result.AlignedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(new Vector2D(64, -1), alignable.Position);
        Assert.Equal(270, alignable.Angle);
        Assert.Equal(new Vector2D(96, -16), normal.Position);
        Assert.Equal(0, normal.Angle);
        Assert.Equal("Aligned a thing.", result.Message);
        Assert.Same(line, map.NearestLinedef(alignable.Position));
    }

    [Fact]
    public void AlignSelectedThingsToNearestWallsTriesNextLineWhenNearestCannotAlign()
    {
        GameConfiguration config = ThingConfig();
        var map = new MapSet();
        var low = map.AddSector();
        low.FloorHeight = 0;
        low.CeilHeight = 8;
        var tall = map.AddSector();
        tall.FloorHeight = 0;
        tall.CeilHeight = 128;
        var nearA = map.AddVertex(new Vector2D(0, 0));
        var nearB = map.AddVertex(new Vector2D(128, 0));
        var farA = map.AddVertex(new Vector2D(0, 16));
        var farB = map.AddVertex(new Vector2D(128, 16));
        var near = map.AddLinedef(nearA, nearB);
        var far = map.AddLinedef(farA, farB);
        map.AddSidedef(near, isFront: true, low);
        map.AddSidedef(far, isFront: true, tall);
        var thing = map.AddThing(new Vector2D(64, -4), 31009);
        thing.Height = 32;
        thing.Selected = true;
        thing.Sector = tall;
        map.BuildIndexes();

        ThingWallAlignmentResult result = ThingWallAlignment.AlignSelectedToNearestWalls(map, config);

        Assert.Equal(1, result.AlignedCount);
        Assert.Equal(new Vector2D(64, 15), thing.Position);
        Assert.Equal(270, thing.Angle);
        Assert.Same(far, map.NearestLinedef(thing.Position));
    }

    [Fact]
    public void AlignExplicitThingsToNearestWallsIgnoresOtherSelectedThings()
    {
        GameConfiguration config = ThingConfig();
        var (map, _, front) = OneSidedLine(0, 128);
        var visualThing = map.AddThing(new Vector2D(64, -16), 31010);
        var otherSelected = map.AddThing(new Vector2D(96, -16), 31010);
        visualThing.Sector = front;
        otherSelected.Sector = front;
        otherSelected.Selected = true;

        ThingWallAlignmentResult result = ThingWallAlignment.AlignThingsToNearestWalls(map, config, new[] { visualThing });

        Assert.Equal(1, result.SelectedCount);
        Assert.Equal(1, result.EligibleCount);
        Assert.Equal(1, result.AlignedCount);
        Assert.Equal(new Vector2D(64, -1), visualThing.Position);
        Assert.Equal(new Vector2D(96, -16), otherSelected.Position);
    }

    [Fact]
    public void AlignSelectedThingsToNearestWallsReportsNoEligibleSelection()
    {
        var config = GameConfiguration.FromText("""
            thingtypes { 1 { title = "Normal sprite"; renderstyle = "normal"; } }
            """);
        var (map, _, front) = OneSidedLine(0, 128);
        var thing = map.AddThing(new Vector2D(64, -16), 1);
        thing.Selected = true;
        thing.Sector = front;

        ThingWallAlignmentResult result = ThingWallAlignment.AlignSelectedToNearestWalls(map, config);

        Assert.Equal(1, result.SelectedCount);
        Assert.Equal(0, result.EligibleCount);
        Assert.Equal(0, result.AlignedCount);
        Assert.Equal("This action only works for models or things with FLATSPRITE/WALLSPRITE flags!", result.Message);
        Assert.Equal(new Vector2D(64, -16), thing.Position);
    }

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

    private static GameConfiguration ThingConfig()
    {
        const string text = @"
ACTOR WallSpriteThing 31009
{
    +WALLSPRITE
    Height 16
    States { Spawn: WSPR A -1 stop }
}

ACTOR FlatSpriteThing 31010
{
    +FLATSPRITE
    Height 16
    States { Spawn: FSPR A -1 stop }
}";
        var config = GameConfiguration.FromText("""
            thingtypes
            {
                test
                {
                    title = "Test";
                    2 { title = "Normal sprite"; height = 16; }
                }
            }
            """);
        config.MergeActors(DecorateParser.Parse(text));
        return config;
    }

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
