// ABOUTME: Plane port verification tests.
// ABOUTME: Distance, GetZ, intersection, and inversion.

using DBuilder.Geometry;

namespace DBuilder.Tests;

public class PlaneTests
{
    private const double Epsilon = 1e-9;

    [Fact]
    public void DistanceFromOriginPlaneIsHeight()
    {
        // Plane: z = 0, normal = (0,0,1), offset = 0. Distance to (x, y, h) == h.
        var p = new Plane(new Vector3D(0, 0, 1), 0);
        Assert.Equal(5, p.Distance(new Vector3D(0, 0, 5)), Epsilon);
        Assert.Equal(-3, p.Distance(new Vector3D(0, 0, -3)), Epsilon);
    }

    [Fact]
    public void GetZOnHorizontalPlaneIsConstant()
    {
        // Plane offset by 10 along +Z: equation z = 10.
        var p = new Plane(new Vector3D(0, 0, 1), new Vector3D(0, 0, 10));
        Assert.Equal(10, p.GetZ(new Vector2D(123, -456)), Epsilon);
    }

    [Fact]
    public void ThreePointConstructorMakesPlaneThroughPoints()
    {
        var p1 = new Vector3D(0, 0, 5);
        var p2 = new Vector3D(1, 0, 5);
        var p3 = new Vector3D(0, 1, 5);
        var p = new Plane(p1, p2, p3, up: true);
        Assert.Equal(0, p.Distance(p1), 1e-6);
        Assert.Equal(0, p.Distance(p2), 1e-6);
        Assert.Equal(0, p.Distance(p3), 1e-6);
    }

    [Fact]
    public void GetIntersectionHitsHorizontalPlane()
    {
        // Plane z=0, ray from (0,0,5) to (0,0,-5): u should be 0.5.
        var p = new Plane(new Vector3D(0, 0, 1), 0);
        double u = 0;
        Assert.True(p.GetIntersection(new Vector3D(0, 0, 5), new Vector3D(0, 0, -5), ref u));
        Assert.Equal(0.5, u, 1e-9);
    }

    [Fact]
    public void GetIntersectionMissesParallelRay()
    {
        var p = new Plane(new Vector3D(0, 0, 1), 0);
        double u = 0;
        // Ray parallel to the plane at z=5 will never hit.
        Assert.False(p.GetIntersection(new Vector3D(0, 0, 5), new Vector3D(1, 0, 5), ref u));
    }

    [Fact]
    public void GetInvertedFlipsNormalAndOffset()
    {
        var p = new Plane(new Vector3D(0, 0, 1), 10);
        var inv = p.GetInverted();
        Assert.Equal(-p.a, inv.a);
        Assert.Equal(-p.b, inv.b);
        Assert.Equal(-p.c, inv.c);
        Assert.Equal(-p.d, inv.d);
    }
}
