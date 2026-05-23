// ABOUTME: Tests GZDoom 3D-floor resolution from Sector_3DFloor (160) control sectors to tagged target sectors.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ThreeDFloorsTests
{
    // A control sector carrying special 160 (arg0=tag) and a target sector tagged the same.
    private static (MapSet map, Sector control, Sector target) Setup(int tag, int alpha)
    {
        var map = new MapSet();
        var control = map.AddSector();
        control.FloorHeight = 0; control.CeilHeight = 32;
        control.FloorTexture = "FFLAT"; control.CeilTexture = "CFLAT"; control.Brightness = 200;

        var target = map.AddSector();
        target.Tag = tag;

        // A linedef inside the control sector with the 3D-floor special.
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(v0, v1);
        var sd = map.AddSidedef(line, true, control);
        sd.MidTexture = "SIDETEX";
        line.Action = ThreeDFloors.Sector3DFloorAction;
        line.Args[0] = tag;
        line.Args[1] = 1;       // solid type
        line.Args[3] = alpha;

        // Give the target a couple of sidedefs so it is a real sector (not required by Resolve).
        map.BuildIndexes();
        return (map, control, target);
    }

    [Fact]
    public void ResolvesSlabIntoTaggedSector()
    {
        var (map, control, target) = Setup(tag: 5, alpha: 160);
        var result = ThreeDFloors.Resolve(map);

        Assert.True(result.ContainsKey(target));
        var f = result[target][0];
        Assert.Same(control, f.Control);
        Assert.Equal(0, f.Bottom, 3);
        Assert.Equal(32, f.Top, 3);
        Assert.Equal(160, f.Alpha);
        Assert.Equal("CFLAT", f.TopFlat);     // control ceiling flat = slab top
        Assert.Equal("FFLAT", f.BottomFlat);  // control floor flat = slab bottom
        Assert.Equal("SIDETEX", f.SideTexture);
        Assert.Equal(200, f.Brightness);
    }

    [Fact]
    public void NonMatchingTagYieldsNothing()
    {
        var (map, _, target) = Setup(tag: 5, alpha: 255);
        target.Tag = 6; // no line targets tag 6
        map.BuildIndexes();
        Assert.Empty(ThreeDFloors.Resolve(map));
    }

    [Fact]
    public void NonSpecialLineIsIgnored()
    {
        var (map, _, _) = Setup(tag: 5, alpha: 255);
        foreach (var l in map.Linedefs) l.Action = 0; // remove the 3D-floor special
        Assert.Empty(ThreeDFloors.Resolve(map));
    }

    [Fact]
    public void AlphaIsClamped()
    {
        var (map, _, target) = Setup(tag: 7, alpha: 999);
        var f = ThreeDFloors.Resolve(map)[target][0];
        Assert.Equal(255, f.Alpha);
    }
}
