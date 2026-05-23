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

    // A single big square sector with one slope thing at its center.
    private static (MapSet map, Sector sec, Thing thing) SquareWithThing(int thingType, int vangleDeg)
    {
        var map = new MapSet();
        var s = map.AddSector(); s.FloorHeight = 0; s.CeilHeight = 256;
        var v = new[]
        {
            map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(0, 256)),
            map.AddVertex(new Vector2D(256, 256)), map.AddVertex(new Vector2D(256, 0)),
        };
        for (int i = 0; i < 4; i++) map.AddSidedef(map.AddLinedef(v[i], v[(i + 1) % 4]), true, s);
        var t = map.AddThing(new Vector2D(128, 128), thingType);
        t.Args[0] = vangleDeg;
        map.BuildIndexes();
        return (map, s, t);
    }

    [Fact]
    public void FloorSlopeThingSlopesItsSector()
    {
        var (map, s, _) = SquareWithThing(SlopeEffects.FloorSlopeThing, vangleDeg: 45);
        int n = SlopeEffects.ApplyThingSlopes(map);
        Assert.Equal(1, n);
        Assert.True(s.HasFloorSlope);
        // The plane is tilted, so the floor height is not constant across the sector.
        double a = s.GetFloorZ(new Vector2D(40, 40));
        double b = s.GetFloorZ(new Vector2D(220, 220));
        Assert.True(System.Math.Abs(a - b) > 1, $"expected a tilted floor, got {a} vs {b}");
    }

    [Fact]
    public void CeilingSlopeThingSlopesCeiling()
    {
        var (map, s, _) = SquareWithThing(SlopeEffects.CeilingSlopeThing, vangleDeg: 30);
        Assert.Equal(1, SlopeEffects.ApplyThingSlopes(map));
        Assert.True(s.HasCeilSlope);
        Assert.False(s.HasFloorSlope);
    }

    [Fact]
    public void VerticalSlopeThingIsIgnored()
    {
        var (map, s, _) = SquareWithThing(SlopeEffects.FloorSlopeThing, vangleDeg: 0); // sin(0)=0 -> degenerate
        Assert.Equal(0, SlopeEffects.ApplyThingSlopes(map));
        Assert.False(s.HasFloorSlope);
    }

    [Fact]
    public void ApplyAllCombinesLineAndThingSlopes()
    {
        var (map, _, _, _) = TwoSquares(arg0: 1, arg1: 0); // one Plane_Align floor slope
        // add a ceiling slope thing inside the front square (which spans x[0..64], y[0..64])
        var t = map.AddThing(new Vector2D(20, 20), SlopeEffects.CeilingSlopeThing);
        t.Args[0] = 45;
        map.BuildIndexes();
        Assert.Equal(2, SlopeEffects.ApplyAll(map));
    }
}
