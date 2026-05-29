// ABOUTME: Tests for MapSet box queries used by rubber-band selection (vertices/things/linedefs/sectors in a rect).
// ABOUTME: Verifies inclusion rules: points inside, linedefs fully inside, sectors fully enclosed.

using DBuilder.Geometry;
using DBuilder.Map;
using System.Drawing;

namespace DBuilder.Tests;

public class MapSetBoxQueryTests
{
    [Fact]
    public void VerticesInsideBoxOnly()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(10, 10));   // inside
        var b = map.AddVertex(new Vector2D(50, 50));   // inside
        map.AddVertex(new Vector2D(200, 200));         // outside
        map.BuildIndexes();

        var inside = map.GetVerticesInBox(0, 0, 100, 100);
        Assert.Equal(2, inside.Count);
        Assert.Contains(a, inside);
        Assert.Contains(b, inside);
    }

    [Fact]
    public void ThingsInsideBoxOnly()
    {
        var map = new MapSet();
        var t1 = map.AddThing(new Vector2D(5, 5), 1);
        map.AddThing(new Vector2D(-5, 5), 1); // outside (negative x)
        map.BuildIndexes();

        var inside = map.GetThingsInBox(0, 0, 100, 100);
        Assert.Single(inside);
        Assert.Contains(t1, inside);
    }

    [Fact]
    public void LinedefRequiresBothEndpointsInside()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(10, 10));
        var b = map.AddVertex(new Vector2D(90, 90));
        var c = map.AddVertex(new Vector2D(300, 300));
        var inside = map.AddLinedef(a, b);   // both inside
        map.AddLinedef(b, c);                // straddles the boundary
        map.BuildIndexes();

        var hit = map.GetLinedefsInBox(0, 0, 100, 100);
        Assert.Single(hit);
        Assert.Contains(inside, hit);
    }

    [Fact]
    public void SectorRequiresAllBoundaryVerticesInside()
    {
        var map = new MapSet();
        var inSec = map.AddSector();
        var outSec = map.AddSector();
        // Small square fully inside the box.
        var v = new[]
        {
            map.AddVertex(new Vector2D(10, 10)), map.AddVertex(new Vector2D(10, 40)),
            map.AddVertex(new Vector2D(40, 40)), map.AddVertex(new Vector2D(40, 10)),
        };
        for (int i = 0; i < 4; i++) map.AddSidedef(map.AddLinedef(v[i], v[(i + 1) % 4]), true, inSec);
        // Square partially outside the box.
        var w = new[]
        {
            map.AddVertex(new Vector2D(80, 80)), map.AddVertex(new Vector2D(80, 200)),
            map.AddVertex(new Vector2D(200, 200)), map.AddVertex(new Vector2D(200, 80)),
        };
        for (int i = 0; i < 4; i++) map.AddSidedef(map.AddLinedef(w[i], w[(i + 1) % 4]), true, outSec);
        map.BuildIndexes();

        var hit = map.GetSectorsInBox(0, 0, 100, 100);
        Assert.Single(hit);
        Assert.Contains(inSec, hit);
    }

    [Fact]
    public void InclusiveBoundsAndDegenerateBox()
    {
        var map = new MapSet();
        var corner = map.AddVertex(new Vector2D(100, 100)); // exactly on the max corner
        map.BuildIndexes();
        Assert.Contains(corner, map.GetVerticesInBox(0, 0, 100, 100));
        // A zero-area box catches a point sitting exactly on it.
        Assert.Contains(corner, map.GetVerticesInBox(100, 100, 100, 100));
    }

    [Fact]
    public void AreaHelpersCreateAndIncreaseBounds()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(-16, 8));
        var b = map.AddVertex(new Vector2D(24, 32));
        var c = map.AddVertex(new Vector2D(64, -4));
        var line = map.AddLinedef(b, c);
        var thing = map.AddThing(new Vector2D(80, 96), 3001);

        var area = MapSet.CreateArea(new[] { a, b });
        Assert.Equal(new RectangleF(-16, 8, 40, 24), area);

        area = MapSet.IncreaseArea(area, new[] { line });
        Assert.Equal(new RectangleF(-16, -4, 80, 36), area);

        area = MapSet.IncreaseArea(area, new[] { thing });
        Assert.Equal(new RectangleF(-16, -4, 96, 100), area);

        area = MapSet.IncreaseArea(area, new Vector2D(-32, 128));
        Assert.Equal(new RectangleF(-32, -4, 112, 132), area);
    }

    [Fact]
    public void FilterByAreaUsesCohenSutherlandLineRejectionAndInclusiveVertices()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(-10, 50));
        var b = map.AddVertex(new Vector2D(110, 50));
        var c = map.AddVertex(new Vector2D(-10, -10));
        var d = map.AddVertex(new Vector2D(-20, -20));
        var e = map.AddVertex(new Vector2D(0, 0));
        var f = map.AddVertex(new Vector2D(100, 100));
        var crossing = map.AddLinedef(a, b);
        var outside = map.AddLinedef(c, d);
        var area = new RectangleF(0, 0, 100, 100);

        Assert.Equal(0x05, MapSet.GetCSFieldBits(c.Position, area));
        Assert.Equal(new[] { crossing }, MapSet.FilterByArea(new[] { crossing, outside }, ref area));
        Assert.Equal(new[] { e, f }, MapSet.FilterByArea(new[] { a, b, c, d, e, f }, ref area));
    }
}
