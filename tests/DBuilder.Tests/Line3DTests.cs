// ABOUTME: Line3D port verification tests.

using DBuilder.Geometry;

namespace DBuilder.Tests;

public class Line3DTests
{
    [Fact]
    public void DefaultColorAndArrowhead()
    {
        var l = new Line3D(new Vector3D(0, 0, 0), new Vector3D(1, 0, 0));
        Assert.Equal(Line3D.DefaultColor, l.Color);
        Assert.True(l.RenderArrowhead);
    }

    [Fact]
    public void DeltaIsEndMinusStart()
    {
        var l = new Line3D(new Vector3D(1, 2, 3), new Vector3D(4, 6, 8));
        Assert.Equal(new Vector3D(3, 4, 5), l.GetDelta());
    }

    [Fact]
    public void ColorCtorOverridesDefault()
    {
        var l = new Line3D(new Vector3D(0, 0, 0), new Vector3D(1, 0, 0), 0xff00ff00u);
        Assert.Equal(0xff00ff00u, l.Color);
    }
}
