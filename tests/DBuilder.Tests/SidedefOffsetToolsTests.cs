// ABOUTME: Verifies UDB-style sidedef vertical texture offset normalization.
// ABOUTME: Covers named and numeric peg flags, one-sided walls, two-sided walls, and sky ceilings.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SidedefOffsetToolsTests
{
    [Fact]
    public void TopOffsetUsesHighHeightUnlessUpperUnpegged()
    {
        var (_, front, _) = TwoSided(0, 128, 0, 64);
        var config = Config();

        Assert.Equal(-118, SidedefOffsetTools.GetTopOffsetY(front, 10, -2, fromNormalized: false, config));
        Assert.Equal(138, SidedefOffsetTools.GetTopOffsetY(front, 10, -2, fromNormalized: true, config));

        front.Line.SetFlag("dontpegtop", true);

        Assert.Equal(10, SidedefOffsetTools.GetTopOffsetY(front, 10, -2, fromNormalized: false, config));
    }

    [Fact]
    public void DisabledScaledTextureOffsetsIgnoreTextureScaleLikeUdb()
    {
        var (_, front, _) = TwoSided(0, 128, 32, 96);
        var config = Config(scaledTextureOffsets: false);

        Assert.Equal(-22, SidedefOffsetTools.GetTopOffsetY(front, 10, -2, fromNormalized: false, config));
        Assert.Equal(-22, SidedefOffsetTools.GetMiddleOffsetY(front, 10, 2, fromNormalized: false, config));
        Assert.Equal(-86, SidedefOffsetTools.GetBottomOffsetY(front, 10, 2, fromNormalized: false, config));
    }

    [Fact]
    public void MiddleOffsetUsesCeilingDeltaForTwoSidedWalls()
    {
        var (_, front, _) = TwoSided(0, 128, 0, 96);

        Assert.Equal(-22, SidedefOffsetTools.GetOffsetY(front, SidedefPart.Middle, 10, 1, fromNormalized: false, Config()));
    }

    [Fact]
    public void MiddleOffsetUsesFloorGapWhenLowerUnpegged()
    {
        var (_, front, _) = TwoSided(0, 128, 32, 96);
        front.Line.SetFlag("dontpegbottom", true);

        Assert.Equal(-86, SidedefOffsetTools.GetMiddleOffsetY(front, 10, 1, fromNormalized: false, Config()));
    }

    [Fact]
    public void OneSidedMiddleOffsetOnlyNormalizesWhenLowerUnpegged()
    {
        var (_, front) = OneSided(0, 128);
        var config = Config();

        Assert.Equal(10, SidedefOffsetTools.GetMiddleOffsetY(front, 10, 0.5, fromNormalized: false, config));

        front.Line.SetFlag("dontpegbottom", true);

        Assert.Equal(-54, SidedefOffsetTools.GetMiddleOffsetY(front, 10, 0.5, fromNormalized: false, config));
    }

    [Fact]
    public void BottomOffsetUsesCeilingToOtherFloorWithoutLowerUnpegged()
    {
        var (_, front, _) = TwoSided(0, 128, 32, 96);

        Assert.Equal(-86, SidedefOffsetTools.GetBottomOffsetY(front, 10, 1, fromNormalized: false, Config()));
    }

    [Fact]
    public void LowerUnpeggedBottomOffsetRequiresBothSkyCeilings()
    {
        var (_, front, back) = TwoSided(0, 128, 0, 96);
        front.Line.SetFlag("dontpegbottom", true);
        var config = Config();

        Assert.Equal(10, SidedefOffsetTools.GetBottomOffsetY(front, 10, 1, fromNormalized: false, config));

        front.Sector!.CeilTexture = "F_SKY1";
        back.Sector!.CeilTexture = "F_SKY1";

        Assert.Equal(-22, SidedefOffsetTools.GetBottomOffsetY(front, 10, 1, fromNormalized: false, config));
    }

    [Fact]
    public void NumericPegFlagStringsReadClassicFlagBits()
    {
        var (_, front) = OneSided(0, 128);
        front.Line.Flags = 16;

        Assert.Equal(-118, SidedefOffsetTools.GetMiddleOffsetY(front, 10, 1, fromNormalized: false, Config(lower: "16")));
    }

    private static GameConfiguration Config(string upper = "dontpegtop", string lower = "dontpegbottom", bool scaledTextureOffsets = true)
        => GameConfiguration.FromText($$"""
            upperunpeggedflag = "{{upper}}";
            lowerunpeggedflag = "{{lower}}";
            skyflatname = "F_SKY1";
            scaledtextureoffsets = {{scaledTextureOffsets.ToString().ToLowerInvariant()}};
            """);

    private static (MapSet Map, Sidedef Front, Sidedef Back) TwoSided(int frontFloor, int frontCeil, int backFloor, int backCeil)
    {
        var map = BaseMap();
        var frontSector = map.AddSector();
        frontSector.FloorHeight = frontFloor;
        frontSector.CeilHeight = frontCeil;
        var backSector = map.AddSector();
        backSector.FloorHeight = backFloor;
        backSector.CeilHeight = backCeil;
        var line = map.Linedefs[0];
        var front = map.AddSidedef(line, isFront: true, frontSector);
        var back = map.AddSidedef(line, isFront: false, backSector);
        map.BuildIndexes();
        return (map, front, back);
    }

    private static (MapSet Map, Sidedef Front) OneSided(int floor, int ceiling)
    {
        var map = BaseMap();
        var sector = map.AddSector();
        sector.FloorHeight = floor;
        sector.CeilHeight = ceiling;
        var front = map.AddSidedef(map.Linedefs[0], isFront: true, sector);
        map.BuildIndexes();
        return (map, front);
    }

    private static MapSet BaseMap()
    {
        var map = new MapSet();
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(128, 0));
        map.AddLinedef(start, end);
        return map;
    }
}
