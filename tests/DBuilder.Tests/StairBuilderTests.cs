// ABOUTME: Tests StairBuilder stepping floor heights and UDB-style option application.
// ABOUTME: Covers independent height bases, flat changes, wall texture filling, and unpegged flags.

using System.Collections.Generic;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class StairBuilderTests
{
    private static List<Sector> Sectors(int count)
    {
        var map = new MapSet();
        var list = new List<Sector>();
        for (int i = 0; i < count; i++)
        {
            var s = map.AddSector();
            s.FloorHeight = 0;
            s.CeilHeight = 128;
            list.Add(s);
        }
        return list;
    }

    [Fact]
    public void StepsFloorHeightsInOrder()
    {
        var s = Sectors(3);
        StairBuilder.Apply(s, startFloor: 0, step: 16, moveCeiling: false);
        Assert.Equal(0, s[0].FloorHeight);
        Assert.Equal(16, s[1].FloorHeight);
        Assert.Equal(32, s[2].FloorHeight);
    }

    [Fact]
    public void MoveCeilingPreservesHeadroom()
    {
        var s = Sectors(3); // each room is 128 tall
        StairBuilder.Apply(s, startFloor: 0, step: 16, moveCeiling: true);
        Assert.Equal(128, s[0].CeilHeight);
        Assert.Equal(144, s[1].CeilHeight); // floor 16 + 128
        Assert.Equal(160, s[2].CeilHeight); // floor 32 + 128
        Assert.Equal(128, s[2].CeilHeight - s[2].FloorHeight); // headroom preserved
    }

    [Fact]
    public void WithoutMoveCeilingTheCeilingStays()
    {
        var s = Sectors(2);
        StairBuilder.Apply(s, startFloor: 64, step: 24, moveCeiling: false);
        Assert.Equal(64, s[0].FloorHeight);
        Assert.Equal(88, s[1].FloorHeight);
        Assert.Equal(128, s[1].CeilHeight); // unchanged
    }

    [Fact]
    public void NegativeStepDescends()
    {
        var s = Sectors(3);
        StairBuilder.Apply(s, startFloor: 0, step: -8, moveCeiling: false);
        Assert.Equal(-8, s[1].FloorHeight);
        Assert.Equal(-16, s[2].FloorHeight);
    }

    [Fact]
    public void IndependentCeilingStepMatchesUdbHeightModification()
    {
        var s = Sectors(3);

        StairBuilder.Apply(s, startFloor: 16, floorStep: 8, applyCeiling: true, startCeiling: 128, ceilingStep: 4);

        Assert.Equal(new[] { 16, 24, 32 }, s.ConvertAll(sector => sector.FloorHeight));
        Assert.Equal(new[] { 128, 132, 136 }, s.ConvertAll(sector => sector.CeilHeight));
    }

    [Fact]
    public void IndependentCeilingStepCanLeaveCeilingsUnchanged()
    {
        var s = Sectors(2);

        StairBuilder.Apply(s, startFloor: 32, floorStep: 16, applyCeiling: false, startCeiling: 256, ceilingStep: 32);

        Assert.Equal(32, s[0].FloorHeight);
        Assert.Equal(48, s[1].FloorHeight);
        Assert.Equal(128, s[0].CeilHeight);
        Assert.Equal(128, s[1].CeilHeight);
    }

    [Fact]
    public void OptionsUseOneBasedStepCountersWithGlobalBaseHeights()
    {
        var s = Sectors(3);

        StairBuilder.Apply(s, new StairBuilderOptions
        {
            FloorBase = 0,
            FloorStep = 8,
            ApplyCeilingHeight = true,
            CeilingBase = 128,
            CeilingStep = 4
        });

        Assert.Equal(new[] { 8, 16, 24 }, s.ConvertAll(sector => sector.FloorHeight));
        Assert.Equal(new[] { 132, 136, 140 }, s.ConvertAll(sector => sector.CeilHeight));
    }

    [Fact]
    public void OptionsCanUseDistinctExistingBaseHeights()
    {
        var s = Sectors(2);
        s[0].FloorHeight = 16;
        s[1].FloorHeight = 48;
        s[0].CeilHeight = 128;
        s[1].CeilHeight = 192;

        StairBuilder.Apply(s, new StairBuilderOptions
        {
            FloorStep = 8,
            ApplyCeilingHeight = true,
            CeilingStep = -4,
            DistinctBaseHeights = true
        });

        Assert.Equal(new[] { 24, 64 }, s.ConvertAll(sector => sector.FloorHeight));
        Assert.Equal(new[] { 124, 184 }, s.ConvertAll(sector => sector.CeilHeight));
    }

    [Fact]
    public void OptionsApplyFloorAndCeilingFlats()
    {
        var s = Sectors(2);

        StairBuilder.Apply(s, new StairBuilderOptions
        {
            FloorStep = 0,
            ApplyFloorTexture = true,
            FloorTexture = "STEP1",
            ApplyCeilingTexture = true,
            CeilingTexture = "CEIL1"
        });

        Assert.All(s, sector => Assert.Equal("STEP1", sector.FloorTexture));
        Assert.All(s, sector => Assert.Equal("CEIL1", sector.CeilTexture));
    }

    [Fact]
    public void OptionsFillRequiredUpperLowerTexturesAndSetUnpeggedFlags()
    {
        var map = new MapSet();
        Sector front = map.AddSector();
        Sector back = map.AddSector();
        front.FloorHeight = 0;
        front.CeilHeight = 128;
        back.FloorHeight = 16;
        back.CeilHeight = 96;
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        Sidedef frontSide = map.AddSidedef(line, true, front);
        Sidedef backSide = map.AddSidedef(line, false, back);
        map.BuildIndexes();

        StairBuilder.Apply(new[] { front }, new StairBuilderOptions
        {
            FloorStep = 0,
            ApplyUpperTexture = true,
            UpperTexture = "UPPER",
            UpperUnpegged = true,
            ApplyLowerTexture = true,
            LowerTexture = "LOWER",
            LowerUnpegged = true
        });

        Assert.Equal("UPPER", frontSide.HighTexture);
        Assert.Equal("-", backSide.HighTexture);
        Assert.Equal("LOWER", frontSide.LowTexture);
        Assert.Equal("-", backSide.LowTexture);
        Assert.Equal(StairBuilder.DefaultUpperUnpeggedBit | StairBuilder.DefaultLowerUnpeggedBit, line.Flags);
    }

    [Fact]
    public void OptionsFillRequiredMiddleTextureOnOneSidedLines()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        Sidedef side = map.AddSidedef(line, true, sector);
        map.BuildIndexes();

        StairBuilder.Apply(new[] { sector }, new StairBuilderOptions
        {
            FloorStep = 0,
            ApplyMiddleTexture = true,
            MiddleTexture = "MID"
        });

        Assert.Equal("MID", side.MidTexture);
    }

    [Fact]
    public void StraightLinePlanBuildsFrontSideLoopsLikeUdb()
    {
        var map = new MapSet();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));

        IReadOnlyList<StairBuilderSectorPlan> plan = StairBuilder.PlanStraightSectorsFromLines(
            new[] { line },
            new StairBuilderStraightOptions
            {
                NumberOfSectors = 2,
                SectorDepth = 32,
                SideFront = true
            });

        Assert.Equal(2, plan.Count);
        Assert.Equal(
            new[] { new Vector2D(0, 0), new Vector2D(0, -32), new Vector2D(64, -32), new Vector2D(64, 0), new Vector2D(0, 0) },
            plan[0].Vertices);
        Assert.Equal(
            new[] { new Vector2D(0, -32), new Vector2D(0, -64), new Vector2D(64, -64), new Vector2D(64, -32), new Vector2D(0, -32) },
            plan[1].Vertices);
    }

    [Fact]
    public void StraightLinePlanBuildsBackSideLoopsAndAppliesSpacing()
    {
        var map = new MapSet();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));

        IReadOnlyList<StairBuilderSectorPlan> plan = StairBuilder.PlanStraightSectorsFromLines(
            new[] { line },
            new StairBuilderStraightOptions
            {
                NumberOfSectors = 2,
                SectorDepth = 32,
                Spacing = 8,
                SideFront = false
            });

        Assert.Equal(
            new[] { new Vector2D(0, 40), new Vector2D(0, 72), new Vector2D(64, 72), new Vector2D(64, 40), new Vector2D(0, 40) },
            plan[1].Vertices);
    }

    [Fact]
    public void StraightLinePlanCreatesIndependentLoopsForEachSelectedLine()
    {
        var map = new MapSet();
        Linedef first = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        Linedef second = map.AddLinedef(map.AddVertex(new Vector2D(128, 0)), map.AddVertex(new Vector2D(128, 64)));

        IReadOnlyList<StairBuilderSectorPlan> plan = StairBuilder.PlanStraightSectorsFromLines(
            new[] { first, second },
            new StairBuilderStraightOptions
            {
                NumberOfSectors = 1,
                SectorDepth = 16,
                SideFront = true
            });

        Assert.Equal(2, plan.Count);
        Assert.Equal(
            new[] { new Vector2D(0, 0), new Vector2D(0, -16), new Vector2D(64, -16), new Vector2D(64, 0), new Vector2D(0, 0) },
            plan[0].Vertices);
        Assert.Equal(
            new[] { new Vector2D(128, 0), new Vector2D(144, 0), new Vector2D(144, 64), new Vector2D(128, 64), new Vector2D(128, 0) },
            plan[1].Vertices);
    }
}
