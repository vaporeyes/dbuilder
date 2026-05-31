// ABOUTME: Tests UDB-style BuilderModes Make Door mutations on map sectors.
// ABOUTME: Covers door textures, tags, action specials, offsets, flags, and outward line facing.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class MakeDoorToolTests
{
    [Fact]
    public void ApplyCreatesDoorFromSelectedSector()
    {
        var (map, sector, trackSide, doorSide, outsideSide) = DoorMap();
        sector.FloorHeight = 16;
        sector.CeilHeight = 128;
        sector.FloorTexture = "OLD-FLOOR";
        sector.CeilTexture = "OLD-CEIL";
        trackSide.OffsetX = 12;
        trackSide.OffsetY = 24;
        doorSide.OffsetX = 32;
        doorSide.OffsetY = 48;
        outsideSide.OffsetX = 64;
        outsideSide.OffsetY = 96;

        var options = new MakeDoorOptions
        {
            DoorTexture = "BIGDOOR2",
            TrackTexture = "DOORTRAK",
            FloorTexture = "STEP1",
            CeilingTexture = "CEIL5_1",
            ResetOffsets = true,
            ApplyActionSpecials = true,
            ApplyTag = false,
            Action = 202,
            Activate = 1024,
            Args = new[] { 0, -1, 150, 34, 1 },
            Flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["playeruse"] = true,
                ["monsteractivate"] = false,
            },
        };

        MakeDoorResult result = MakeDoorTool.Apply(map, new[] { sector }, options);

        Assert.Equal(new MakeDoorResult(1, 1, 1), result);
        Assert.Equal(sector.FloorHeight, sector.CeilHeight);
        Assert.Equal("STEP1", sector.FloorTexture);
        Assert.Equal("CEIL5_1", sector.CeilTexture);
        Assert.Equal(1, sector.Tag);

        Assert.Equal("-", trackSide.HighTexture);
        Assert.Equal("DOORTRAK", trackSide.MidTexture);
        Assert.Equal("-", trackSide.LowTexture);
        Assert.False(trackSide.Line.IsFlagSet("upperunpegged"));
        Assert.True(trackSide.Line.IsFlagSet("lowerunpegged"));

        Assert.Equal("BIGDOOR2", outsideSide.HighTexture);
        Assert.Equal(202, doorSide.Line.Action);
        Assert.Equal(1024, doorSide.Line.Activate);
        Assert.Equal(new[] { 0, 1, 150, 34, 1 }, doorSide.Line.Args);
        Assert.True(doorSide.Line.IsFlagSet("playeruse"));
        Assert.False(doorSide.Line.IsFlagSet("monsteractivate"));
        Assert.False(doorSide.Line.IsFlagSet("upperunpegged"));
        Assert.False(doorSide.Line.IsFlagSet("lowerunpegged"));

        Assert.Equal(0, trackSide.OffsetX);
        Assert.Equal(0, trackSide.OffsetY);
        Assert.Equal(0, doorSide.OffsetX);
        Assert.Equal(0, doorSide.OffsetY);
        Assert.Equal(0, outsideSide.OffsetX);
        Assert.Equal(0, outsideSide.OffsetY);

        Assert.False(doorSide.IsFront);
        Assert.Same(outsideSide, doorSide.Line.Front);
        Assert.Same(doorSide, doorSide.Line.Back);
    }

    [Fact]
    public void ApplyCanLeaveActionsTagsAndOffsetsUntouchedWhenDisabled()
    {
        var (map, sector, _, doorSide, outsideSide) = DoorMap();
        sector.Tag = 9;
        doorSide.Line.Action = 80;
        doorSide.Line.Activate = 1;
        doorSide.Line.Args[0] = 7;
        doorSide.OffsetX = 16;
        outsideSide.OffsetY = 32;

        var options = new MakeDoorOptions
        {
            DoorTexture = "",
            CeilingTexture = "",
            ResetOffsets = false,
            ApplyActionSpecials = false,
            ApplyTag = false,
            Args = new[] { 2, 3, 4, 5, 6 },
        };

        MakeDoorTool.Apply(map, new[] { sector }, options);

        Assert.Equal(9, sector.Tag);
        Assert.Equal("OUTSIDE-HIGH", outsideSide.HighTexture);
        Assert.Equal("OLD-CEIL", sector.CeilTexture);
        Assert.Equal(80, doorSide.Line.Action);
        Assert.Equal(1, doorSide.Line.Activate);
        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, doorSide.Line.Args);
        Assert.Equal(16, doorSide.OffsetX);
        Assert.Equal(32, outsideSide.OffsetY);
    }

    private static (MapSet Map, Sector Sector, Sidedef TrackSide, Sidedef DoorSide, Sidedef OutsideSide) DoorMap()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        sector.CeilTexture = "OLD-CEIL";
        Sector outside = map.AddSector();

        Vertex a = map.AddVertex(new Vector2D(0, 0));
        Vertex b = map.AddVertex(new Vector2D(128, 0));
        Vertex c = map.AddVertex(new Vector2D(128, 128));
        Vertex d = map.AddVertex(new Vector2D(0, 128));

        Linedef oneSided = map.AddLinedef(a, b);
        Sidedef trackSide = map.AddSidedef(oneSided, true, sector);
        trackSide.HighTexture = "OLD-HIGH";
        trackSide.MidTexture = "OLD-MID";
        trackSide.LowTexture = "OLD-LOW";

        Linedef twoSided = map.AddLinedef(c, d);
        Sidedef doorSide = map.AddSidedef(twoSided, true, sector);
        Sidedef outsideSide = map.AddSidedef(twoSided, false, outside);
        outsideSide.HighTexture = "OUTSIDE-HIGH";

        map.BuildIndexes();
        return (map, sector, trackSide, doorSide, outsideSide);
    }
}
