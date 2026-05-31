// ABOUTME: Generates vertex loops for shape-draw tools (rectangle, ellipse / regular N-gon) inscribed in a bounding box.
// ABOUTME: Pure geometry; the caller materializes the loop into a sector.

using System;
using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

public sealed record DrawShapePlan(IReadOnlyList<Vector2D> Points, int EffectiveBevelWidth, string HintText);

public static class ShapeGenerator
{
    /// <summary>A counter-clockwise rectangle loop spanning the bounding box of the two corners (empty if degenerate).</summary>
    public static List<Vector2D> Rectangle(Vector2D a, Vector2D b)
    {
        var (minX, minY, maxX, maxY) = Bounds(a, b);
        if (maxX - minX < 1e-6 || maxY - minY < 1e-6) return new List<Vector2D>();
        return new List<Vector2D>
        {
            new(minX, minY), new(maxX, minY), new(maxX, maxY), new(minX, maxY),
        };
    }

    /// <summary>
    /// A counter-clockwise ellipse (regular N-gon, sides &gt;= 3) inscribed in the bounding box of the two corners.
    /// Returns empty for a degenerate box.
    /// </summary>
    public static List<Vector2D> Ellipse(Vector2D a, Vector2D b, int sides)
    {
        if (sides < 3) sides = 3;
        var (minX, minY, maxX, maxY) = Bounds(a, b);
        double rx = (maxX - minX) * 0.5, ry = (maxY - minY) * 0.5;
        if (rx < 1e-6 || ry < 1e-6) return new List<Vector2D>();
        double cx = (minX + maxX) * 0.5, cy = (minY + maxY) * 0.5;

        var loop = new List<Vector2D>(sides);
        for (int i = 0; i < sides; i++)
        {
            double t = 2.0 * Math.PI * i / sides;
            loop.Add(new Vector2D(cx + rx * Math.Cos(t), cy + ry * Math.Sin(t)));
        }
        return loop;
    }

    public static DrawShapePlan UdbRectangle(Vector2D start, Vector2D end, DrawRectangleModeSettings settings)
    {
        if (start == end) return new DrawShapePlan(Array.Empty<Vector2D>(), 0, RectangleHint(settings));
        if (end.x == start.x || end.y == start.y)
            return new DrawShapePlan([start, end], 0, RectangleHint(settings));

        if (settings.BevelWidth == 0)
        {
            return new DrawShapePlan(
                [start, new Vector2D(start.x, end.y), end, new Vector2D(end.x, start.y), start],
                0,
                RectangleHint(settings));
        }

        int width = (int)(end.x - start.x);
        int height = (int)(end.y - start.y);
        int effectiveBevelWidth = Math.Min(Math.Abs(settings.BevelWidth), Math.Min(Math.Abs(width), Math.Abs(height)) / 2);
        bool reverse = false;
        if (settings.BevelWidth < 0)
        {
            effectiveBevelWidth *= -1;
            reverse = true;
        }

        var shape = new List<Vector2D>();
        shape.AddRange(RectangleCornerPoints(settings, start, effectiveBevelWidth, effectiveBevelWidth, !reverse));
        shape.AddRange(RectangleCornerPoints(settings, new Vector2D(end.x, start.y), -effectiveBevelWidth, effectiveBevelWidth, reverse));
        shape.AddRange(RectangleCornerPoints(settings, end, -effectiveBevelWidth, -effectiveBevelWidth, !reverse));
        shape.AddRange(RectangleCornerPoints(settings, new Vector2D(start.x, end.y), effectiveBevelWidth, -effectiveBevelWidth, reverse));
        shape.Add(shape[0]);
        return new DrawShapePlan(shape, effectiveBevelWidth, RectangleHint(settings));
    }

    public static DrawShapePlan UdbEllipse(Vector2D start, Vector2D end, DrawEllipseModeSettings settings)
    {
        if (end.x == start.x && end.y == start.y) return new DrawShapePlan(Array.Empty<Vector2D>(), 0, EllipseHint(settings));
        if (end.x == start.x || end.y == start.y) return new DrawShapePlan([start, end], 0, EllipseHint(settings));

        int width = (int)(end.x - start.x);
        int height = (int)(end.y - start.y);
        int effectiveBevelWidth;
        if (settings.Subdivisions < 6)
            effectiveBevelWidth = 0;
        else if (settings.BevelWidth < 0)
            effectiveBevelWidth = -Math.Min(Math.Abs(settings.BevelWidth), Math.Min(Math.Abs(width), Math.Abs(height)) / 2) + 1;
        else
            effectiveBevelWidth = settings.BevelWidth;

        var shape = new Vector2D[settings.Subdivisions + 1];
        bool doBevel = false;
        double halfWidth = width / 2.0;
        double halfHeight = height / 2.0;
        Vector2D center = new(start.x + halfWidth, start.y + halfHeight);
        double currentAngle = Angle2D.DegToRad(settings.Angle);
        double angleStep = -Angle2D.PI / settings.Subdivisions * 2;

        for (int i = 0; i < settings.Subdivisions; i++)
        {
            double radiusX = halfWidth + (doBevel ? effectiveBevelWidth : 0);
            double radiusY = halfHeight + (doBevel ? effectiveBevelWidth : 0);
            shape[i] = new Vector2D(
                center.x - Math.Sin(currentAngle) * radiusX,
                center.y - Math.Cos(currentAngle) * radiusY);
            doBevel = !doBevel;
            currentAngle += angleStep;
        }

        shape[settings.Subdivisions] = shape[0];
        FitEllipseToBounds(shape, start, end, center);
        return new DrawShapePlan(shape, effectiveBevelWidth, EllipseHint(settings));
    }

    private static (double minX, double minY, double maxX, double maxY) Bounds(Vector2D a, Vector2D b)
        => (Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Max(a.x, b.x), Math.Max(a.y, b.y));

    private static Vector2D[] RectangleCornerPoints(DrawRectangleModeSettings settings, Vector2D startPoint, int bevelWidth, int bevelHeight, bool reverse)
    {
        Vector2D center = settings.BevelWidth > 0
            ? new Vector2D(startPoint.x + bevelWidth, startPoint.y + bevelHeight)
            : startPoint;
        double currentAngle = Angle2D.PI;
        int steps = settings.Subdivisions + 2;
        var points = new Vector2D[steps];
        double stepAngle = Angle2D.PIHALF / (settings.Subdivisions + 1);

        for (int i = 0; i < steps; i++)
        {
            points[i] = new Vector2D(
                center.x + Math.Sin(currentAngle) * bevelWidth,
                center.y + Math.Cos(currentAngle) * bevelHeight);
            currentAngle += stepAngle;
        }

        if (reverse) Array.Reverse(points);
        return points;
    }

    private static void FitEllipseToBounds(Vector2D[] shape, Vector2D start, Vector2D end, Vector2D center)
    {
        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        foreach (Vector2D vertex in shape)
        {
            if (vertex.x < minX) minX = vertex.x;
            if (vertex.x > maxX) maxX = vertex.x;
            if (vertex.y < minY) minY = vertex.y;
            if (vertex.y > maxY) maxY = vertex.y;
        }

        double scaleX = 1.0;
        double scaleY = 1.0;
        if (minX != start.x || maxX != end.x) scaleX = (end.x - start.x) / (maxX - minX);
        if (minY != start.y) scaleY = (end.y - start.y) / (maxY - minY);

        if (scaleX != 1.0 || scaleY != 1.0)
        {
            for (int i = 0; i < shape.Length; i++)
            {
                shape[i].x = (center.x - shape[i].x) * scaleX + center.x;
                shape[i].y = (center.y - shape[i].y) * scaleY + center.y;
            }
        }

        minX = double.MaxValue;
        minY = double.MaxValue;
        foreach (Vector2D vertex in shape)
        {
            if (vertex.x < minX) minX = vertex.x;
            if (vertex.y < minY) minY = vertex.y;
        }

        Vector2D offset = new();
        if (minX != start.x) offset.x = start.x - minX;
        if (minY != start.y) offset.y = start.y - minY;
        if (offset.x == 0.0 && offset.y == 0.0) return;

        for (int i = 0; i < shape.Length; i++) shape[i] += offset;
    }

    private static string RectangleHint(DrawRectangleModeSettings settings)
    {
        var parts = new List<string>();
        if (settings.BevelWidth != 0) parts.Add("BVL: " + settings.BevelWidth);
        if (settings.Subdivisions != 0) parts.Add("SUB: " + settings.Subdivisions);
        return string.Join("; ", parts);
    }

    private static string EllipseHint(DrawEllipseModeSettings settings)
    {
        var parts = new List<string>();
        if (settings.BevelWidth != 0) parts.Add("BVL: " + settings.BevelWidth);
        if (settings.Subdivisions != 0) parts.Add("VERTS: " + settings.Subdivisions);
        if (settings.Angle != 0) parts.Add("ANGLE: " + settings.Angle);
        return string.Join("; ", parts);
    }
}
