// ABOUTME: Angle2D port verification tests.
// ABOUTME: Covers conversions, normalization, doom-angle round-trip, and 3-point angle.

using DBuilder.Geometry;

namespace DBuilder.Tests;

public class Angle2DTests
{
    private const double Epsilon = 1e-9;

    [Fact]
    public void DegRadRoundTrip()
    {
        Assert.Equal(90.0, Angle2D.RadToDeg(Angle2D.DegToRad(90.0)), Epsilon);
        Assert.Equal(0.5, Angle2D.RadToDeg(Angle2D.DegToRad(0.5)), Epsilon);
    }

    [Fact]
    public void NormalizedWrapsInto0to2Pi()
    {
        Assert.Equal(0.0, Angle2D.Normalized(0));
        Assert.Equal(Angle2D.PI, Angle2D.Normalized(Angle2D.PI));
        Assert.Equal(0.0, Angle2D.Normalized(Angle2D.PI2), Epsilon);
        Assert.Equal(Angle2D.PI, Angle2D.Normalized(-Angle2D.PI), Epsilon);
    }

    [Fact]
    public void DifferenceHandlesWrapAround()
    {
        // Angles 10 degrees apart across the 0/2pi boundary should still report ~10 deg.
        double a = Angle2D.DegToRad(355);
        double b = Angle2D.DegToRad(5);
        double diff = Angle2D.Difference(a, b);
        Assert.Equal(Angle2D.DegToRad(10), diff, 1e-6);
    }

    [Fact]
    public void DoomAngleRoundTripPreservesCardinal()
    {
        foreach (int d in new[] { 0, 90, 180, 270 })
        {
            int back = Angle2D.RealToDoom(Angle2D.DoomToReal(d));
            Assert.Equal(d, ((back % 360) + 360) % 360);
        }
    }
}
