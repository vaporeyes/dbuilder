// ABOUTME: Generates vertex loops for shape-draw tools (rectangle, ellipse / regular N-gon) inscribed in a bounding box.
// ABOUTME: Pure geometry; the caller materializes the loop into a sector.

using System;
using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

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

    private static (double minX, double minY, double maxX, double maxY) Bounds(Vector2D a, Vector2D b)
        => (Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Max(a.x, b.x), Math.Max(a.y, b.y));
}
