// ABOUTME: ProjectedFrustum2D port verification tests.

using DBuilder.Geometry;

namespace DBuilder.Tests;

public class ProjectedFrustum2DTests
{
    [Fact]
    public void FrustumBuildsFourLinesAndPositiveRadius()
    {
        var f = new ProjectedFrustum2D(new Vector2D(0, 0), xyangle: 0, zangle: 0, near: 10, far: 100, fov: (float)(System.Math.PI / 2));
        Assert.Equal(4, f.Lines.Length);
        Assert.True(f.Radius > 0);
    }

    [Fact]
    public void CircleInsideFrustumIntersects()
    {
        // UDB's FromAngle(a) returns (sin a, -cos a), so xyangle=0 points along -Y.
        // A circle deep inside the frustum body is at (0, -50).
        var f = new ProjectedFrustum2D(new Vector2D(0, 0), xyangle: 0, zangle: 0, near: 10, far: 100, fov: (float)(System.Math.PI / 2));
        Assert.True(f.IntersectCircle(new Vector2D(0, -50), 1));
    }

    [Fact]
    public void CircleBehindCameraDoesNotIntersect()
    {
        // xyangle=0 points along -Y (sin 0, -cos 0); behind the camera is +Y.
        var f = new ProjectedFrustum2D(new Vector2D(0, 0), xyangle: 0, zangle: 0, near: 10, far: 100, fov: (float)(System.Math.PI / 4));
        Assert.False(f.IntersectCircle(new Vector2D(0, 10000), 1));
    }
}
