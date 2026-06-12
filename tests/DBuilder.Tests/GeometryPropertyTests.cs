// ABOUTME: Deterministic property-style tests for core geometry operations.
// ABOUTME: Covers vector, line, and plane invariants across many sampled inputs.

using DBuilder.Geometry;

namespace DBuilder.Tests;

public sealed class GeometryPropertyTests
{
    private const double Epsilon = 1e-9;

    [Fact]
    public void VectorNormalizationPreservesDirectionAndUnitLength()
    {
        var random = new Random(0x5EED);

        for (int i = 0; i < 256; i++)
        {
            Vector2D source = NonZeroVector2D(random);
            Vector2D normal = source.GetNormal();
            Vector2D perpendicular = source.GetPerpendicular();

            Assert.Equal(1, normal.GetLength(), Epsilon);
            Assert.True(Vector2D.DotProduct(source, normal) > 0);
            Assert.Equal(0, Vector2D.DotProduct(source, perpendicular), 1e-7);
        }
    }

    [Fact]
    public void VectorTransformRoundTripsForNonZeroScales()
    {
        var random = new Random(0xC0FFEE);

        for (int i = 0; i < 256; i++)
        {
            Vector2D source = MakeVector2D(random);
            double offsetx = Range(random, -1024, 1024);
            double offsety = Range(random, -1024, 1024);
            double scalex = NonZero(random, -32, 32);
            double scaley = NonZero(random, -32, 32);

            Vector2D transformed = source.GetTransformed(offsetx, offsety, scalex, scaley);
            Vector2D roundTripped = transformed.GetInvTransformed(-offsetx, -offsety, 1 / scalex, 1 / scaley);

            Assert.Equal(source.x, roundTripped.x, 1e-8);
            Assert.Equal(source.y, roundTripped.y, 1e-8);
        }
    }

    [Fact]
    public void LineProjectionIsOnLineAndMinimizesDistance()
    {
        var random = new Random(0xBEEF);

        for (int i = 0; i < 256; i++)
        {
            Vector2D start = MakeVector2D(random);
            Vector2D end = start + NonZeroVector2D(random);
            Vector2D point = MakeVector2D(random);

            double u = Line2D.GetNearestOnLine(start, end, point);
            Vector2D unbounded = Line2D.GetNearestPointOnLine(start, end, point, bounded: false);
            Vector2D atU = Line2D.GetCoordinatesAt(start, end, u);

            Assert.Equal(atU.x, unbounded.x, 1e-8);
            Assert.Equal(atU.y, unbounded.y, 1e-8);
            Assert.Equal(Vector2D.DistanceSq(point, unbounded), Line2D.GetDistanceToLineSq(start, end, point, bounded: false), 1e-7);

            Vector2D bounded = Line2D.GetNearestPointOnLine(start, end, point, bounded: true);
            double boundedU = Math.Clamp(u, 0, 1);
            Vector2D boundedAtU = Line2D.GetCoordinatesAt(start, end, boundedU);

            Assert.Equal(boundedAtU.x, bounded.x, 1e-8);
            Assert.Equal(boundedAtU.y, bounded.y, 1e-8);
            Assert.Equal(Vector2D.DistanceSq(point, bounded), Line2D.GetDistanceToLineSq(start, end, point, bounded: true), 1e-7);
        }
    }

    [Fact]
    public void PlaneClosestPointLiesOnPlaneAlongNormal()
    {
        var random = new Random(0xFACE);

        for (int i = 0; i < 256; i++)
        {
            Vector3D normal = NonZeroVector3D(random).GetNormal();
            Vector3D pointOnPlane = MakeVector3D(random);
            var plane = new Plane(normal, pointOnPlane);
            Vector3D point = MakeVector3D(random);

            double distance = plane.Distance(point);
            Vector3D closest = plane.ClosestOnPlane(point);
            Vector3D expected = point - normal * distance;

            Assert.Equal(0, plane.Distance(closest), 1e-8);
            Assert.Equal(expected.x, closest.x, 1e-8);
            Assert.Equal(expected.y, closest.y, 1e-8);
            Assert.Equal(expected.z, closest.z, 1e-8);
        }
    }

    private static Vector2D MakeVector2D(Random random)
        => new(Range(random, -4096, 4096), Range(random, -4096, 4096));

    private static Vector3D MakeVector3D(Random random)
        => new(Range(random, -4096, 4096), Range(random, -4096, 4096), Range(random, -4096, 4096));

    private static Vector2D NonZeroVector2D(Random random)
    {
        Vector2D vector;
        do
        {
            vector = MakeVector2D(random);
        }
        while (vector.GetLengthSq() < 1e-8);

        return vector;
    }

    private static Vector3D NonZeroVector3D(Random random)
    {
        Vector3D vector;
        do
        {
            vector = MakeVector3D(random);
        }
        while (vector.GetLengthSq() < 1e-8);

        return vector;
    }

    private static double NonZero(Random random, double min, double max)
    {
        double value;
        do
        {
            value = Range(random, min, max);
        }
        while (Math.Abs(value) < 1e-6);

        return value;
    }

    private static double Range(Random random, double min, double max)
        => min + random.NextDouble() * (max - min);
}
