// ABOUTME: Tests Doom-style sound propagation: free flow, level-2 across one sound-block line, and stop at the second.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SoundPropagationTests
{
    private const int Block = SoundPropagation.DefaultSoundBlockBit; // 64

    // A linear chain of sectors; each adjacent pair shares one two-sided linedef. blockAfter[i]==true marks
    // the line between sector i and i+1 as sound-blocking. Returns the sectors in order.
    private static (MapSet map, Sector[] sectors) Chain(int count, bool[] blockBetween)
    {
        var map = new MapSet();
        var sectors = new Sector[count];
        for (int i = 0; i < count; i++)
        {
            sectors[i] = map.AddSector();
            sectors[i].FloorHeight = 0;
            sectors[i].CeilHeight = 128;
        }

        for (int i = 0; i < count - 1; i++)
        {
            var a = map.AddVertex(new Vector2D(i * 64, 0));
            var b = map.AddVertex(new Vector2D(i * 64, 64));
            var line = map.AddLinedef(a, b);
            map.AddSidedef(line, true, sectors[i]);
            map.AddSidedef(line, false, sectors[i + 1]);
            if (blockBetween[i]) line.Flags |= Block;
        }
        map.BuildIndexes();
        return (map, sectors);
    }

    [Fact]
    public void SoundFlowsFreelyThroughOpenLines()
    {
        var (map, s) = Chain(3, new[] { false, false });
        var reach = SoundPropagation.Reachable(map, s[0]);
        Assert.Equal(3, reach.Count);
        Assert.Equal(1, reach[s[0]]);
        Assert.Equal(1, reach[s[1]]);
        Assert.Equal(1, reach[s[2]]);
    }

    [Fact]
    public void CrossingOneBlockLineIsLevelTwo()
    {
        var (map, s) = Chain(3, new[] { false, true }); // block between s1 and s2
        var reach = SoundPropagation.Reachable(map, s[0]);
        Assert.Equal(1, reach[s[0]]);
        Assert.Equal(1, reach[s[1]]);
        Assert.Equal(2, reach[s[2]]); // reached only by crossing the block line
    }

    [Fact]
    public void SecondBlockLineStopsSound()
    {
        var (map, s) = Chain(4, new[] { false, true, true }); // blocks at s1|s2 and s2|s3
        var reach = SoundPropagation.Reachable(map, s[0]);
        Assert.True(reach.ContainsKey(s[2])); // reachable across the first block (level 2)
        Assert.False(reach.ContainsKey(s[3])); // the second block line stops it
    }

    [Fact]
    public void StartingPastABlockHearsBackwardAtLevelOne()
    {
        var (map, s) = Chain(3, new[] { true, false }); // block between s0 and s1
        var reach = SoundPropagation.Reachable(map, s[1]);
        Assert.Equal(1, reach[s[1]]);
        Assert.Equal(1, reach[s[2]]);      // open line
        Assert.Equal(2, reach[s[0]]);      // across the block line
    }

    [Fact]
    public void ClosedDoorHeightBlocksSound()
    {
        var (map, s) = Chain(3, new[] { false, false });
        s[0].FloorHeight = 0;
        s[0].CeilHeight = 64;
        s[1].FloorHeight = 64;
        s[1].CeilHeight = 128;
        s[2].FloorHeight = 0;
        s[2].CeilHeight = 128;

        var reach = SoundPropagation.Reachable(map, s[0]);

        Assert.True(reach.ContainsKey(s[0]));
        Assert.False(reach.ContainsKey(s[1]));
        Assert.False(reach.ContainsKey(s[2]));
    }

    [Fact]
    public void InvalidSectorHeightBlocksSound()
    {
        var (map, s) = Chain(2, new[] { false });
        s[0].FloorHeight = 64;
        s[0].CeilHeight = 64;
        s[1].FloorHeight = 0;
        s[1].CeilHeight = 128;

        var reach = SoundPropagation.Reachable(map, s[0]);

        Assert.True(SoundPropagation.IsBlockedByHeight(map.Linedefs[0]));
        Assert.Single(reach);
        Assert.True(reach.ContainsKey(s[0]));
    }
}
