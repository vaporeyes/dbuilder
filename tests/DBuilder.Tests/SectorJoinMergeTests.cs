// ABOUTME: Tests MapSet.JoinSectors (reassign sidedefs, drop extra sectors) and MergeSectors (also delete internal walls).
// ABOUTME: Builds two sectors sharing a two-sided linedef.

using System.Collections.Generic;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SectorJoinMergeTests
{
    // Two sectors A and B sharing one two-sided linedef (front -> A, back -> B).
    private static (MapSet map, Sector a, Sector b, Linedef shared) TwoSharedSectors()
    {
        var map = new MapSet();
        var a = map.AddSector();
        var b = map.AddSector();
        var p0 = map.AddVertex(new Vector2D(64, 0));
        var p1 = map.AddVertex(new Vector2D(64, 64));
        var shared = map.AddLinedef(p0, p1);
        map.AddSidedef(shared, true, a);
        map.AddSidedef(shared, false, b);
        map.BuildIndexes();
        return (map, a, b, shared);
    }

    [Fact]
    public void JoinReassignsSidedefsAndDropsSector()
    {
        var (map, a, b, shared) = TwoSharedSectors();
        var keep = map.JoinSectors(new List<Sector> { a, b });
        map.BuildIndexes();

        Assert.Same(a, keep);
        Assert.Single(map.Sectors);
        Assert.Same(a, shared.Front!.Sector);
        Assert.Same(a, shared.Back!.Sector); // back was B, now reassigned to A
        Assert.Single(map.Linedefs);          // join keeps geometry
    }

    [Fact]
    public void MergeDeletesInternalWallAndUnusedVertices()
    {
        var (map, a, b, _) = TwoSharedSectors();
        var keep = map.MergeSectors(new List<Sector> { a, b });
        map.BuildIndexes();

        Assert.Same(a, keep);
        Assert.Single(map.Sectors);
        Assert.Empty(map.Linedefs);  // the shared wall became internal and was removed
        Assert.Empty(map.Vertices);  // its endpoints became unused
    }

    [Fact]
    public void JoinWithFewerThanTwoReturnsNull()
    {
        var (map, a, _, _) = TwoSharedSectors();
        Assert.Null(map.JoinSectors(new List<Sector> { a }));
        Assert.Equal(2, map.Sectors.Count);
    }

    [Fact]
    public void JoinReindexesRemainingSectors()
    {
        var map = new MapSet();
        var a = map.AddSector(); // Index 0
        var b = map.AddSector(); // Index 1
        var c = map.AddSector(); // Index 2
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(0, 64));
        foreach (var s in new[] { a, b, c }) map.AddSidedef(map.AddLinedef(v0, v1), true, s);
        map.JoinSectors(new List<Sector> { a, b }); // remove b
        Assert.Equal(2, map.Sectors.Count);
        Assert.Equal(0, map.Sectors[0].Index);
        Assert.Equal(1, map.Sectors[1].Index); // c reindexed from 2 -> 1
    }
}
