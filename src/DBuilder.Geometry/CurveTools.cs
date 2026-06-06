// ABOUTME: Bezier curve utilities ported from UDB Source/Core/Geometry/CurveTools.cs.
// ABOUTME: Pure math; behavior preserved 1:1.

/*
 * mxd. Ported from Cubic Bezier curve tools by Andy Woodruff (http://cartogrammar.com/source/CubicBezier.as)
 */

using System;
using System.Collections.Generic;

namespace DBuilder.Geometry;

public static class CurveTools
{
    // "default" values: z = 0.5, angleFactor = 0.75; if targetSegmentLength <= 0, will return lines
    public static Curve CurveThroughPoints(List<Vector2D> points, float z, float angleFactor, int targetSegmentLength)
    {
        Curve result = new Curve();

        // First calculate all the curve control points
        // None of this junk will do any good if there are only two points
        if (points.Count > 2 && targetSegmentLength > 0)
        {
            List<List<Vector2D>> controlPts = new List<List<Vector2D>>();   // Two control points (of a cubic Bezier curve) for each point

            // Make sure z is between 0 and 1 (too messy otherwise)
            if (z <= 0) z = 0.1f;
            else if (z > 1) z = 1;

            // Make sure angleFactor is between 0 and 1
            if (angleFactor < 0) angleFactor = 0;
            else if (angleFactor > 1) angleFactor = 1;

            int firstPt = 1;
            int lastPt = points.Count - 1;

            // Check if this is a closed line (the first and last points are the same)
            if (points[0].x == points[points.Count - 1].x && points[0].y == points[points.Count - 1].y)
            {
                firstPt = 0;
                lastPt = points.Count;
            }
            else
            {
                controlPts.Add(new List<Vector2D>()); // dummy entry
            }

            for (int i = firstPt; i < lastPt; i++)
            {
                // The previous, current, and next points
                Vector2D p0 = (i - 1 < 0) ? points[points.Count - 2] : points[i - 1];
                Vector2D p1 = points[i];
                Vector2D p2 = (i + 1 == points.Count) ? points[1] : points[i + 1];

                double a = Vector2D.Distance(p0, p1);
                if (a < 0.001) a = 0.001f;
                double b = Vector2D.Distance(p1, p2);
                if (b < 0.001) b = 0.001f;
                double c = Vector2D.Distance(p0, p2);
                if (c < 0.001) c = 0.001f;

                double cos = (b * b + a * a - c * c) / (2 * b * a);
                if (cos < -1) cos = -1;
                else if (cos > 1) cos = 1;

                double C = Math.Acos(cos);

                Vector2D aPt = new Vector2D(p0.x - p1.x, p0.y - p1.y);
                Vector2D bPt = new Vector2D(p1.x, p1.y);
                Vector2D cPt = new Vector2D(p2.x - p1.x, p2.y - p1.y);

                if (a > b) aPt = aPt.GetNormal() * b;
                else if (b > a) cPt = cPt.GetNormal() * a;

                aPt += p1;
                cPt += p1;

                double ax = bPt.x - aPt.x;
                double ay = bPt.y - aPt.y;
                double bx = bPt.x - cPt.x;
                double by = bPt.y - cPt.y;
                double rx = ax + bx;
                double ry = ay + by;

                // Correct for three points in a line by finding the angle between just two of them
                if (rx == 0 && ry == 0)
                {
                    rx = -bx;
                    ry = by;
                }

                // Switch rx and ry when y or x difference is 0
                if (ay == 0 && by == 0)
                {
                    rx = 0;
                    ry = 1;
                }
                else if (ax == 0 && bx == 0)
                {
                    rx = 1;
                    ry = 0;
                }

                double theta = Math.Atan2(ry, rx);

                double controlDist = Math.Min(a, b) * z;
                double controlScaleFactor = C / Angle2D.PI;
                controlDist *= ((1 - angleFactor) + angleFactor * controlScaleFactor);
                double controlAngle = theta + Angle2D.PIHALF;

                Vector2D controlPoint2 = new Vector2D(controlDist, 0);
                Vector2D controlPoint1 = new Vector2D(controlDist, 0);
                controlPoint2 = controlPoint2.GetRotated(controlAngle);
                controlPoint1 = controlPoint1.GetRotated(controlAngle + Angle2D.PI);

                controlPoint1 += p1;
                controlPoint2 += p1;

                if (Vector2D.Distance(controlPoint2, p2) > Vector2D.Distance(controlPoint1, p2))
                    controlPts.Add(new List<Vector2D> { controlPoint2, controlPoint1 });
                else
                    controlPts.Add(new List<Vector2D> { controlPoint1, controlPoint2 });
            }

            // Quadratic Bezier from the first to second points if line not closed.
            if (firstPt == 1)
            {
                double length = (points[1] - points[0]).GetLength();
                int numSteps = Math.Max(1, (int)Math.Round(length / targetSegmentLength));
                CurveSegment segment = new CurveSegment();
                segment.Start = points[0];
                segment.CPMid = controlPts[1][0];
                segment.End = points[1];
                CreateQuadraticCurve(segment, numSteps);

                result.Segments.Add(segment);
            }

            // Cubic Bezier curves through the penultimate point, or through the last point if closed.
            for (int i = firstPt; i < lastPt - 1; i++)
            {
                double length = (points[i + 1] - points[i]).GetLength();
                int numSteps = Math.Max(1, (int)Math.Round(length / targetSegmentLength));

                CurveSegment segment = new CurveSegment();
                segment.CPStart = controlPts[i][1];
                segment.CPEnd = controlPts[i + 1][0];
                segment.Start = points[i];
                segment.End = points[i + 1];
                CreateCubicCurve(segment, numSteps);

                result.Segments.Add(segment);
            }

            // Last quadratic Bezier if not closed.
            if (lastPt == points.Count - 1)
            {
                double length = (points[lastPt] - points[lastPt - 1]).GetLength();
                int numSteps = Math.Max(1, (int)Math.Round(length / targetSegmentLength));

                CurveSegment segment = new CurveSegment();
                segment.Start = points[lastPt - 1];
                segment.CPMid = controlPts[lastPt - 1][1];
                segment.End = points[lastPt];
                CreateQuadraticCurve(segment, numSteps);

                result.Segments.Add(segment);
            }
        }
        else if (points.Count >= 2)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                CurveSegment segment = new CurveSegment();
                segment.Start = points[i];
                segment.End = points[i + 1];
                segment.Points = new[] { segment.Start, segment.End };
                result.Segments.Add(segment);
            }
        }

        result.UpdateShape();
        return result;
    }

    public static void CreateQuadraticCurve(CurveSegment segment, int steps)
    {
        segment.CurveType = CurveSegmentType.QUADRATIC;
        segment.Points = GetQuadraticCurve(segment.Start, segment.CPMid, segment.End, steps)!;
    }

    // 3-point quadratic Bezier
    public static Vector2D[]? GetQuadraticCurve(Vector2D p1, Vector2D p2, Vector2D p3, int steps)
    {
        if (steps < 0) return null;
        if (steps == 0) return new[] { p1 };

        int totalSteps = steps + 1;
        Vector2D[] points = new Vector2D[totalSteps];
        double step = 1f / steps;
        double curStep = 0f;

        for (int i = 0; i < totalSteps; i++)
        {
            points[i] = GetPointOnQuadraticCurve(p1, p2, p3, curStep);
            curStep += step;
        }
        return points;
    }

    public static void CreateCubicCurve(CurveSegment segment, int steps)
    {
        segment.CurveType = CurveSegmentType.CUBIC;
        segment.Points = GetCubicCurve(segment.Start, segment.End, segment.CPStart, segment.CPEnd, steps)!;
    }

    // 4-point cubic Bezier
    public static Vector2D[]? GetCubicCurve(Vector2D p1, Vector2D p2, Vector2D cp1, Vector2D cp2, int steps)
    {
        if (steps < 0) return null;
        if (steps == 0) return new[] { p1 };

        int totalSteps = steps + 1;
        Vector2D[] points = new Vector2D[totalSteps];
        double step = 1f / steps;
        double curStep = 0f;

        for (int i = 0; i < totalSteps; i++)
        {
            points[i] = GetPointOnCubicCurve(p1, p2, cp1, cp2, curStep);
            curStep += step;
        }
        return points;
    }

    public static Vector2D GetPointOnCurve(CurveSegment segment, double delta) => segment.CurveType switch
    {
        CurveSegmentType.QUADRATIC => GetPointOnQuadraticCurve(segment.Start, segment.CPMid, segment.End, delta),
        CurveSegmentType.CUBIC     => GetPointOnCubicCurve(segment.Start, segment.End, segment.CPStart, segment.CPEnd, delta),
        CurveSegmentType.LINE      => GetPointOnLine(segment.Start, segment.End, delta),
        _ => throw new Exception("GetPointOnCurve: got unknown curve type: " + segment.CurveType),
    };

    public static Vector2D GetPointOnQuadraticCurve(Vector2D p1, Vector2D p2, Vector2D p3, double delta)
    {
        double invDelta = 1f - delta;
        double m1 = invDelta * invDelta;
        double m2 = 2 * invDelta * delta;
        double m3 = delta * delta;
        double px = (m1 * p1.x + m2 * p2.x + m3 * p3.x);
        double py = (m1 * p1.y + m2 * p2.y + m3 * p3.y);
        return new Vector2D(px, py);
    }

    public static Vector2D GetPointOnCubicCurve(Vector2D p1, Vector2D p2, Vector2D cp1, Vector2D cp2, double delta)
    {
        double invDelta = 1f - delta;
        double m1 = invDelta * invDelta * invDelta;
        double m2 = 3 * delta * invDelta * invDelta;
        double m3 = 3 * delta * delta * invDelta;
        double m4 = delta * delta * delta;
        double px = (m1 * p1.x + m2 * cp1.x + m3 * cp2.x + m4 * p2.x);
        double py = (m1 * p1.y + m2 * cp1.y + m3 * cp2.y + m4 * p2.y);
        return new Vector2D(px, py);
    }

    public static Vector2D HermiteSpline(Vector2D p1, Vector2D t1, Vector2D p2, Vector2D t2, float u)
    {
        double u2 = u * u;
        double u3 = u2 * u;
        double h1 = 2 * u3 - 3 * u2 + 1;
        double h2 = -2 * u3 + 3 * u2;
        double h3 = u3 - 2 * u2 + u;
        double h4 = u3 - u2;
        return h1 * p1 + h2 * p2 + h3 * t1 + h4 * t2;
    }

    public static Vector3D HermiteSpline(Vector3D p1, Vector3D t1, Vector3D p2, Vector3D t2, float u)
    {
        double u2 = u * u;
        double u3 = u2 * u;
        double h1 = 2 * u3 - 3 * u2 + 1;
        double h2 = -2 * u3 + 3 * u2;
        double h3 = u3 - 2 * u2 + u;
        double h4 = u3 - u2;
        return h1 * p1 + h2 * p2 + h3 * t1 + h4 * t2;
    }

    // basically 2-point bezier
    public static Vector2D GetPointOnLine(Vector2D p1, Vector2D p2, double delta)
    {
        return new Vector2D((int)((1f - delta) * p1.x + delta * p2.x), (int)((1f - delta) * p1.y + delta * p2.y));
    }
}

public class Curve
{
    public List<CurveSegment> Segments;
    public List<Vector2D> Shape = new();

    public Curve()
    {
        Segments = new List<CurveSegment>();
    }

    public void UpdateShape()
    {
        Shape = new List<Vector2D>();
        foreach (CurveSegment segment in Segments)
        {
            foreach (Vector2D point in segment.Points)
            {
                if (Shape.Count == 0 || point != Shape[Shape.Count - 1])
                    Shape.Add(point);
            }
        }
    }
}

public class CurveSegment
{
    public Vector2D[] Points = Array.Empty<Vector2D>();
    public Vector2D Start;
    public Vector2D End;
    public Vector2D CPStart;
    public Vector2D CPMid;
    public Vector2D CPEnd;
    public CurveSegmentType CurveType;
}

public enum CurveSegmentType
{
    LINE,
    QUADRATIC,
    CUBIC,
}
