// ABOUTME: Tests Plane_Align slope baking - the sloped sector meets its neighbor at the line and tilts to the far edge.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SlopeEffectsTests
{
    // Left square (front, floor 0) and right square (back, floor 64) sharing the edge x=64.
    // The shared linedef carries Plane_Align with the given args.
    private static (MapSet map, Sector front, Sector back, Linedef shared) TwoSquares(int arg0, int arg1)
    {
        var map = new MapSet();
        var front = map.AddSector(); front.FloorHeight = 0; front.CeilHeight = 128;
        var back = map.AddSector(); back.FloorHeight = 64; back.CeilHeight = 200;

        // Left square vertices.
        var l0 = map.AddVertex(new Vector2D(0, 0));
        var l1 = map.AddVertex(new Vector2D(0, 64));
        var s0 = map.AddVertex(new Vector2D(64, 0));   // shared edge bottom
        var s1 = map.AddVertex(new Vector2D(64, 64));  // shared edge top
        var r0 = map.AddVertex(new Vector2D(128, 0));
        var r1 = map.AddVertex(new Vector2D(128, 64));

        // Left square (front) - 3 outer edges + the shared edge.
        map.AddSidedef(map.AddLinedef(l0, l1), true, front);
        map.AddSidedef(map.AddLinedef(l1, s1), true, front);
        map.AddSidedef(map.AddLinedef(s0, l0), true, front);
        var shared = map.AddLinedef(s0, s1);           // shared edge, runs (64,0)->(64,64)
        map.AddSidedef(shared, true, front);
        map.AddSidedef(shared, false, back);
        shared.Action = SlopeEffects.PlaneAlignAction;
        shared.Args[0] = arg0;
        shared.Args[1] = arg1;

        // Right square (back) - 3 outer edges (shared edge already has its back side).
        map.AddSidedef(map.AddLinedef(s1, r1), true, back);
        map.AddSidedef(map.AddLinedef(r1, r0), true, back);
        map.AddSidedef(map.AddLinedef(r0, s0), true, back);

        map.BuildIndexes();
        return (map, front, back, shared);
    }

    [Fact]
    public void SlopesFrontFloorToMeetBackAtLine()
    {
        var (map, front, _, _) = TwoSquares(arg0: 1, arg1: 0); // slope front floor
        int n = SlopeEffects.ApplyPlaneAlign(map);
        Assert.Equal(1, n);
        Assert.True(front.HasFloorSlope);

        // At the shared line (x=64) the front floor rises to the back's height (64); at the far edge (x=0) it is 0.
        Assert.Equal(64, front.GetFloorZ(new Vector2D(64, 32)), 2);
        Assert.Equal(0, front.GetFloorZ(new Vector2D(0, 32)), 2);
        Assert.Equal(32, front.GetFloorZ(new Vector2D(32, 32)), 2); // linear ramp midway
    }

    [Fact]
    public void NoSlopeWhenHeightsEqual()
    {
        var (map, front, back, _) = TwoSquares(arg0: 1, arg1: 0);
        back.FloorHeight = 0; // same as front
        Assert.Equal(0, SlopeEffects.ApplyPlaneAlign(map));
        Assert.False(front.HasFloorSlope);
    }

    [Fact]
    public void ArgZeroAlignsNeither()
    {
        var (map, front, back, _) = TwoSquares(arg0: 0, arg1: 0);
        Assert.Equal(0, SlopeEffects.ApplyPlaneAlign(map));
        Assert.False(front.HasFloorSlope);
        Assert.False(back.HasFloorSlope);
    }

    [Fact]
    public void CeilingArgSlopesCeiling()
    {
        var (map, front, _, _) = TwoSquares(arg0: 0, arg1: 1); // slope front ceiling
        int n = SlopeEffects.ApplyPlaneAlign(map);
        Assert.Equal(1, n);
        Assert.True(front.HasCeilSlope);
        Assert.False(front.HasFloorSlope);
        Assert.Equal(200, front.GetCeilZ(new Vector2D(64, 32)), 2); // meets back ceiling (200) at the line
        Assert.Equal(128, front.GetCeilZ(new Vector2D(0, 32)), 2);  // own ceiling (128) at the far edge
    }
}
