// ABOUTME: CurveTools port verification tests.
// ABOUTME: Covers Bezier generation and curve-through-points behavior.

using DBuilder.Geometry;

namespace DBuilder.Tests;

public class CurveToolsTests
{
    private const double Epsilon = 1e-9;

    [Fact]
    public void QuadraticBezierEndpointsMatchInputs()
    {
        var p1 = new Vector2D(0, 0);
        var p2 = new Vector2D(5, 10);
        var p3 = new Vector2D(10, 0);
        Assert.Equal(p1, CurveTools.GetPointOnQuadraticCurve(p1, p2, p3, 0));
        Assert.Equal(p3, CurveTools.GetPointOnQuadraticCurve(p1, p2, p3, 1));
    }

    [Fact]
    public void CubicBezierEndpointsMatchInputs()
    {
        // Note UDB's GetCubicCurve signature: (start, end, cp1, cp2).
        var p1 = new Vector2D(0, 0);
        var p2 = new Vector2D(10, 0);
        var cp1 = new Vector2D(3, 5);
        var cp2 = new Vector2D(7, 5);
        Assert.Equal(p1, CurveTools.GetPointOnCubicCurve(p1, p2, cp1, cp2, 0));
        Assert.Equal(p2, CurveTools.GetPointOnCubicCurve(p1, p2, cp1, cp2, 1));
    }

    [Fact]
    public void GetQuadraticCurveProducesRequestedStepCount()
    {
        var pts = CurveTools.GetQuadraticCurve(new Vector2D(0, 0), new Vector2D(5, 10), new Vector2D(10, 0), 10);
        Assert.NotNull(pts);
        Assert.Equal(11, pts!.Length); // steps + 1
    }

    [Fact]
    public void GetQuadraticCurveWithZeroStepsReturnsStartPoint()
    {
        var p1 = new Vector2D(0, 0);

        var pts = CurveTools.GetQuadraticCurve(p1, new Vector2D(5, 10), new Vector2D(10, 0), 0);

        Assert.NotNull(pts);
        Assert.Equal(new[] { p1 }, pts!);
    }

    [Fact]
    public void GetCubicCurveProducesRequestedStepCount()
    {
        var pts = CurveTools.GetCubicCurve(new Vector2D(0, 0), new Vector2D(10, 0), new Vector2D(3, 5), new Vector2D(7, 5), 20);
        Assert.NotNull(pts);
        Assert.Equal(21, pts!.Length);
    }

    [Fact]
    public void GetCubicCurveWithZeroStepsReturnsStartPoint()
    {
        var p1 = new Vector2D(0, 0);

        var pts = CurveTools.GetCubicCurve(p1, new Vector2D(10, 0), new Vector2D(3, 5), new Vector2D(7, 5), 0);

        Assert.NotNull(pts);
        Assert.Equal(new[] { p1 }, pts!);
    }

    [Fact]
    public void CurveThroughPointsTwoPointFallbackProducesLineSegments()
    {
        var curve = CurveTools.CurveThroughPoints(new() { new Vector2D(0, 0), new Vector2D(10, 0) }, z: 0.5f, angleFactor: 0.75f, targetSegmentLength: 5);
        // Fewer than 3 input points falls into the line-segment branch.
        Assert.Single(curve.Segments);
        Assert.Equal(2, curve.Segments[0].Points.Length);
    }

    [Fact]
    public void CurveThroughPointsThreeInputsBuildsSegments()
    {
        var pts = new List<Vector2D>
        {
            new(0, 0),
            new(5, 10),
            new(10, 0),
        };
        var curve = CurveTools.CurveThroughPoints(pts, z: 0.5f, angleFactor: 0.75f, targetSegmentLength: 5);
        Assert.True(curve.Segments.Count >= 1);
        Assert.NotEmpty(curve.Shape);
    }

    [Fact]
    public void HermiteSplineVector2DMatchesEndpoints()
    {
        var p1 = new Vector2D(0, 0);
        var p2 = new Vector2D(10, 2);
        var t1 = new Vector2D(4, 8);
        var t2 = new Vector2D(-2, 6);

        Assert.Equal(p1, CurveTools.HermiteSpline(p1, t1, p2, t2, 0));
        Assert.Equal(p2, CurveTools.HermiteSpline(p1, t1, p2, t2, 1));
    }

    [Fact]
    public void HermiteSplineVector2DInterpolatesWithTangents()
    {
        var result = CurveTools.HermiteSpline(
            new Vector2D(0, 0),
            new Vector2D(4, 8),
            new Vector2D(10, 2),
            new Vector2D(-2, 6),
            0.25f);

        Assert.InRange(result.x, 2.21875 - Epsilon, 2.21875 + Epsilon);
        Assert.InRange(result.y, 1.15625 - Epsilon, 1.15625 + Epsilon);
    }

    [Fact]
    public void HermiteSplineVector3DMatchesEndpoints()
    {
        var p1 = new Vector3D(1, 2, 3);
        var p2 = new Vector3D(9, 6, 0);
        var t1 = new Vector3D(4, -2, 1);
        var t2 = new Vector3D(-3, 5, 2);

        Assert.Equal(p1, CurveTools.HermiteSpline(p1, t1, p2, t2, 0));
        Assert.Equal(p2, CurveTools.HermiteSpline(p1, t1, p2, t2, 1));
    }

    [Fact]
    public void HermiteSplineVector3DInterpolatesWithTangents()
    {
        var result = CurveTools.HermiteSpline(
            new Vector3D(1, 2, 3),
            new Vector3D(4, -2, 1),
            new Vector3D(9, 6, 0),
            new Vector3D(-3, 5, 2),
            0.5f);

        Assert.InRange(result.x, 5.875 - Epsilon, 5.875 + Epsilon);
        Assert.InRange(result.y, 3.125 - Epsilon, 3.125 + Epsilon);
        Assert.InRange(result.z, 1.375 - Epsilon, 1.375 + Epsilon);
    }
}
