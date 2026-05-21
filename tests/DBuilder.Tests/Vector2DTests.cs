// ABOUTME: Vector2D port verification tests.
// ABOUTME: Locks down behavior preserved from UDB Source/Core/Geometry/Vector2D.cs.

using DBuilder.Geometry;

namespace DBuilder.Tests;

public class Vector2DTests
{
    private const double Epsilon = 1e-12;

    [Fact]
    public void AddSubtract()
    {
        var a = new Vector2D(3, 4);
        var b = new Vector2D(1, 2);
        Assert.Equal(new Vector2D(4, 6), a + b);
        Assert.Equal(new Vector2D(2, 2), a - b);
        Assert.Equal(new Vector2D(-3, -4), -a);
    }

    [Fact]
    public void ScalarOps()
    {
        var a = new Vector2D(2, 4);
        Assert.Equal(new Vector2D(4, 8), a * 2);
        Assert.Equal(new Vector2D(4, 8), 2 * a);
        Assert.Equal(new Vector2D(1, 2), a / 2);
    }

    [Fact]
    public void DotProduct()
    {
        var a = new Vector2D(3, 4);
        var b = new Vector2D(2, 1);
        Assert.Equal(10, Vector2D.DotProduct(a, b));
    }

    [Fact]
    public void LengthAndLengthSq()
    {
        var v = new Vector2D(3, 4);
        Assert.Equal(25, v.GetLengthSq(), Epsilon);
        Assert.Equal(5, v.GetLength(), Epsilon);
        Assert.Equal(7, v.GetManhattanLength(), Epsilon);
    }

    [Fact]
    public void Normalize()
    {
        var v = new Vector2D(3, 4).GetNormal();
        Assert.Equal(1, v.GetLength(), Epsilon);
        Assert.Equal(0.6, v.x, Epsilon);
        Assert.Equal(0.8, v.y, Epsilon);
    }

    [Fact]
    public void NormalizeZeroIsZero()
    {
        // UDB returns (0,0) when the vector is effectively zero rather than NaN.
        var v = new Vector2D(0, 0).GetNormal();
        Assert.Equal(0, v.x);
        Assert.Equal(0, v.y);
    }

    [Fact]
    public void Perpendicular()
    {
        var v = new Vector2D(1, 0).GetPerpendicular();
        Assert.Equal(new Vector2D(0, 1), v);
    }

    [Fact]
    public void RotatePiOverTwo()
    {
        var v = new Vector2D(1, 0).GetRotated(Angle2D.PIHALF);
        Assert.Equal(0, v.x, 1e-9);
        Assert.Equal(1, v.y, 1e-9);
    }

    [Fact]
    public void Reflect()
    {
        // Reflecting (1, -1) over the X axis normal (0, 1) yields (1, 1).
        var v = new Vector2D(1, -1);
        var m = new Vector2D(0, 1);
        var r = Vector2D.Reflect(v, m);
        Assert.Equal(1, r.x, Epsilon);
        Assert.Equal(1, r.y, Epsilon);
    }

    [Fact]
    public void DistanceMatchesPythagoras()
    {
        var a = new Vector2D(0, 0);
        var b = new Vector2D(3, 4);
        Assert.Equal(5, Vector2D.Distance(a, b), Epsilon);
        Assert.Equal(25, Vector2D.DistanceSq(a, b), Epsilon);
        Assert.Equal(7, Vector2D.ManhattanDistance(a, b), Epsilon);
    }

    [Fact]
    public void IsFiniteCatchesNaNAndInfinity()
    {
        Assert.True(new Vector2D(1, 2).IsFinite());
        Assert.False(new Vector2D(double.NaN, 0).IsFinite());
        Assert.False(new Vector2D(0, double.PositiveInfinity).IsFinite());
    }

    [Fact]
    public void EqualityAndHashConsistent()
    {
        var a = new Vector2D(1.5, -2.25);
        var b = new Vector2D(1.5, -2.25);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ImplicitConversionToVector3D()
    {
        Vector3D v = new Vector2D(2, 3);
        Assert.Equal(2, v.x);
        Assert.Equal(3, v.y);
        Assert.Equal(0, v.z);
    }
}
