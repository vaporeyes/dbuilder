// ABOUTME: Quick coverage for the plain-data structs DrawnVertex and LabelPositionInfo.

using DBuilder.Geometry;

namespace DBuilder.Tests;

public class DrawnVertexAndLabelTests
{
    [Fact]
    public void DrawnVertexFieldsRoundTrip()
    {
        var v = new DrawnVertex { pos = new Vector2D(1, 2), stitch = true, stitchline = false };
        Assert.Equal(new Vector2D(1, 2), v.pos);
        Assert.True(v.stitch);
        Assert.False(v.stitchline);
    }

    [Fact]
    public void LabelPositionInfoCtorRecordsArgs()
    {
        var l = new LabelPositionInfo(new Vector2D(5, 6), 12.5);
        Assert.Equal(new Vector2D(5, 6), l.position);
        Assert.Equal(12.5, l.radius);
    }
}
