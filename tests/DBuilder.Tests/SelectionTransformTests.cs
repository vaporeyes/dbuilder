// ABOUTME: Tests SelectionTransform flip/rotate/scale over selected vertices and things (positions + thing angles).
// ABOUTME: Uses a small selection with a known bounding box so the center is predictable.

using System.Collections.Generic;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SelectionTransformTests
{
    // Two selected vertices spanning x[0..100], y[0..40] -> center (50, 20); one selected thing facing east.
    private static (MapSet map, Vertex a, Vertex b, Thing t) Selection()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 40));
        a.Selected = true;
        b.Selected = true;
        var t = map.AddThing(new Vector2D(0, 0), 1);
        t.Angle = 0; // east
        t.Selected = true;
        return (map, a, b, t);
    }

    [Fact]
    public void FlipHorizontalMirrorsXAndAngle()
    {
        var (map, a, b, t) = Selection();
        Assert.True(SelectionTransform.Apply(map, SelectionTransform.Op.FlipHorizontal));
        Assert.Equal(100, a.Position.x, 3); // 0 -> 2*50-0
        Assert.Equal(0, b.Position.x, 3);   // 100 -> 0
        Assert.Equal(0, a.Position.y, 3);   // y unchanged
        Assert.Equal(180, t.Angle);         // east -> west
    }

    [Fact]
    public void FlipVerticalMirrorsYAndAngle()
    {
        var (map, a, b, t) = Selection();
        SelectionTransform.Apply(map, SelectionTransform.Op.FlipVertical);
        Assert.Equal(40, a.Position.y, 3); // 0 -> 2*20-0
        Assert.Equal(0, b.Position.y, 3);  // 40 -> 0
        Assert.Equal(0, t.Angle);          // east mirrored vertically stays east (-0)
    }

    [Fact]
    public void RotateCCWMovesAndTurnsThing()
    {
        var (map, _, _, t) = Selection();
        // thing at (0,0), center (50,20): CCW -> (cx-(y-cy), cy+(x-cx)) = (50-(0-20), 20+(0-50)) = (70, -30)
        SelectionTransform.Apply(map, SelectionTransform.Op.RotateCCW);
        Assert.Equal(70, t.Position.x, 3);
        Assert.Equal(-30, t.Position.y, 3);
        Assert.Equal(90, t.Angle); // east -> north
    }

    [Fact]
    public void RotateCWTurnsThingClockwise()
    {
        var (map, _, _, t) = Selection();
        SelectionTransform.Apply(map, SelectionTransform.Op.RotateCW);
        Assert.Equal(270, t.Angle); // east -> south (0 - 90 normalized)
    }

    [Fact]
    public void ScaleAboutCenter()
    {
        var (map, a, b, _) = Selection();
        SelectionTransform.Scale(map, 2.0);
        // center (50,20): a (0,0) -> (50 + (0-50)*2, 20 + (0-20)*2) = (-50, -20)
        Assert.Equal(-50, a.Position.x, 3);
        Assert.Equal(-20, a.Position.y, 3);
        Assert.Equal(150, b.Position.x, 3); // 50 + (100-50)*2
    }

    [Fact]
    public void RotateArbitraryAngleMovesSelectionAndTurnsThing()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(10, 0));
        a.Selected = true;
        b.Selected = true;
        var thing = map.AddThing(new Vector2D(10, 0), 1);
        thing.Angle = 0;
        thing.Selected = true;

        Assert.True(SelectionTransform.Rotate(map, Angle2D.DegToRad(45)));

        Assert.Equal(8.536, b.Position.x, 3);
        Assert.Equal(3.536, b.Position.y, 3);
        Assert.Equal(45, thing.Angle);
    }

    [Fact]
    public void RotateCanUseUdbGridSnap()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(10, 0));
        a.Selected = true;
        b.Selected = true;

        SelectionTransform.Rotate(map, Angle2D.DegToRad(44), snapToUdbGrid: true);

        Assert.Equal(8.536, b.Position.x, 3);
        Assert.Equal(3.536, b.Position.y, 3);
    }

    [Fact]
    public void NoSelectionReturnsFalse()
    {
        var map = new MapSet();
        map.AddVertex(new Vector2D(0, 0)); // not selected
        Assert.False(SelectionTransform.Apply(map, SelectionTransform.Op.FlipHorizontal));
    }

    [Fact]
    public void SelectedGeometryVerticesIncludesLinedefEndpoints()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(64, 0));
        var l = map.AddLinedef(a, b);
        l.Selected = true;
        var set = map.SelectedGeometryVertices();
        Assert.Contains(a, set);
        Assert.Contains(b, set);
    }
}
