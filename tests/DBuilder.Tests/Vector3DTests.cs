// ABOUTME: Vector3D port verification tests.
// ABOUTME: Locks down behavior preserved from UDB Source/Core/Geometry/Vector3D.cs.

using System.Numerics;
using DBuilder.Geometry;

namespace DBuilder.Tests;

public class Vector3DTests
{
    private const double Epsilon = 1e-12;

    [Fact]
    public void CrossProductMatchesRightHandRule()
    {
        var x = new Vector3D(1, 0, 0);
        var y = new Vector3D(0, 1, 0);
        var z = Vector3D.CrossProduct(x, y);
        Assert.Equal(new Vector3D(0, 0, 1), z);
    }

    [Fact]
    public void DotProduct()
    {
        var a = new Vector3D(1, 2, 3);
        var b = new Vector3D(4, -5, 6);
        Assert.Equal(12, Vector3D.DotProduct(a, b));
    }

    [Fact]
    public void LengthAndNormal()
    {
        var v = new Vector3D(0, 3, 4);
        Assert.Equal(25, v.GetLengthSq(), Epsilon);
        Assert.Equal(5, v.GetLength(), Epsilon);
        var n = v.GetNormal();
        Assert.Equal(1, n.GetLength(), Epsilon);
        Assert.True(n.IsNormalized());
    }

    [Fact]
    public void NormalizeZeroIsZero()
    {
        var v = new Vector3D(0, 0, 0).GetNormal();
        Assert.Equal(new Vector3D(0, 0, 0), v);
    }

    [Fact]
    public void TransformByIdentityIsIdentity()
    {
        var v = new Vector3D(1, 2, 3);
        var t = Vector3D.Transform(v, Matrix4x4.Identity);
        Assert.Equal(1, t.x, Epsilon);
        Assert.Equal(2, t.y, Epsilon);
        Assert.Equal(3, t.z, Epsilon);
    }

    [Fact]
    public void TransformByTranslation()
    {
        // System.Numerics convention: translation lives in M41/M42/M43 (row 4, last row).
        // UDB's Transform reads M41/M42/M43 as the additive offset, which matches.
        var v = new Vector3D(1, 2, 3);
        var m = Matrix4x4.CreateTranslation(10, 20, 30);
        var t = Vector3D.Transform(v, m);
        Assert.Equal(11, t.x, Epsilon);
        Assert.Equal(22, t.y, Epsilon);
        Assert.Equal(33, t.z, Epsilon);
    }

    [Fact]
    public void IsFiniteCatchesNaNAndInfinity()
    {
        Assert.True(new Vector3D(1, 2, 3).IsFinite());
        Assert.False(new Vector3D(double.NaN, 0, 0).IsFinite());
        Assert.False(new Vector3D(0, 0, double.NegativeInfinity).IsFinite());
    }

    [Fact]
    public void EqualityAndHashConsistent()
    {
        var a = new Vector3D(1, 2, 3);
        var b = new Vector3D(1, 2, 3);
        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ImplicitConversionToVector2DDropsZ()
    {
        Vector2D v2 = new Vector3D(5, 6, 7);
        Assert.Equal(5, v2.x);
        Assert.Equal(6, v2.y);
    }
}
