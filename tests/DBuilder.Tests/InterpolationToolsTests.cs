// ABOUTME: InterpolationTools port verification tests.

using DBuilder.Geometry;

namespace DBuilder.Tests;

public class InterpolationToolsTests
{
    private const double Epsilon = 1e-9;

    [Fact]
    public void LinearHitsEndpoints()
    {
        Assert.Equal(0.0, InterpolationTools.Linear(0.0, 10.0, 0), Epsilon);
        Assert.Equal(10.0, InterpolationTools.Linear(0.0, 10.0, 1), Epsilon);
        Assert.Equal(5.0, InterpolationTools.Linear(0.0, 10.0, 0.5), Epsilon);
    }

    [Theory]
    [InlineData(InterpolationTools.Mode.LINEAR)]
    [InlineData(InterpolationTools.Mode.EASE_IN_SINE)]
    [InlineData(InterpolationTools.Mode.EASE_OUT_SINE)]
    [InlineData(InterpolationTools.Mode.EASE_IN_OUT_SINE)]
    public void AllModesPreserveEndpoints(InterpolationTools.Mode mode)
    {
        Assert.Equal(0.0, InterpolationTools.Interpolate(0.0, 10.0, 0, mode), 1e-6);
        Assert.Equal(10.0, InterpolationTools.Interpolate(0.0, 10.0, 1, mode), 1e-6);
    }

    [Fact]
    public void InterpolateColorMidpointAveragesARGB()
    {
        // Halfway between 0x00000000 and 0xffffffff should be ~(127, 127, 127, 127).
        uint mid = InterpolationTools.InterpolateColor(0x00000000u, 0xffffffffu, 0.5);
        byte a = (byte)((mid >> 24) & 0xff);
        byte r = (byte)((mid >> 16) & 0xff);
        byte g = (byte)((mid >> 8) & 0xff);
        byte b = (byte)(mid & 0xff);
        Assert.InRange(a, 126, 128);
        Assert.InRange(r, 126, 128);
        Assert.InRange(g, 126, 128);
        Assert.InRange(b, 126, 128);
    }
}
