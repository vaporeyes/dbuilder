// ABOUTME: Tests StairBuilder stepping floor heights across sectors, with and without ceiling movement.

using System.Collections.Generic;
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
}
