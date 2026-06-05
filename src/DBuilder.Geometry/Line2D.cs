// ABOUTME: 2D line segment ported from UDB Source/Core/Geometry/Line2D.cs.
// ABOUTME: Linedef ctor omitted until the Map module is ported; everything else preserved 1:1.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;
using System.Drawing;

namespace DBuilder.Geometry;

public struct Line2D
{
    // Coordinates
    public Vector2D v1;
    public Vector2D v2;

    public Line2D(Vector2D v1, Vector2D v2)
    {
        this.v1 = v1;
        this.v2 = v2;
    }

    public Line2D(Vector2D v1, double x2, double y2)
    {
        this.v1 = v1;
        this.v2 = new Vector2D(x2, y2);
    }

    public Line2D(double x1, double y1, Vector2D v2)
    {
        this.v1 = new Vector2D(x1, y1);
        this.v2 = v2;
    }

    public Line2D(double x1, double y1, double x2, double y2)
    {
        this.v1 = new Vector2D(x1, y1);
        this.v2 = new Vector2D(x2, y2);
    }

    // NOTE: UDB has `Line2D(Linedef)`. Reintroduce when the Map module is ported.

    public static double GetLength(double dx, double dy) => Math.Sqrt(GetLengthSq(dx, dy));
    public static double GetLengthSq(double dx, double dy) => dx * dx + dy * dy;

    public static Vector2D GetNormal(double dx, double dy) => new Vector2D(dx, dy).GetNormal();

    //mxd. This tests if given lines intersects
    public static bool GetIntersection(Line2D line1, Line2D line2)
    {
        return GetIntersection(line1.v1, line1.v2, line2.v1.x, line2.v1.y, line2.v2.x, line2.v2.y);
    }

    public static bool GetIntersection(Vector2D v1, Vector2D v2, double x3, double y3, double x4, double y4)
    {
        return GetIntersection(v1, v2, x3, y3, x4, y4, out _, out _);
    }

    public static bool GetIntersection(Vector2D v1, Vector2D v2, double x3, double y3, double x4, double y4, out double u_ray)
    {
        return GetIntersection(v1, v2, x3, y3, x4, y4, out u_ray, out _, true);
    }

    //mxd
    public static bool GetIntersection(Vector2D v1, Vector2D v2, double x3, double y3, double x4, double y4, out double u_ray, bool bounded)
    {
        return GetIntersection(v1, v2, x3, y3, x4, y4, out u_ray, out _, bounded);
    }

    //mxd. Gets intersection point between given lines
    public static Vector2D GetIntersectionPoint(Line2D line1, Line2D line2, bool bounded)
    {
        if (GetIntersection(line1.v1, line1.v2, line2.v1.x, line2.v1.y, line2.v2.x, line2.v2.y, out double u_ray, out _, bounded))
            return GetCoordinatesAt(line2.v1, line2.v2, u_ray);

        return new Vector2D(double.NaN, double.NaN);
    }

    public static bool GetIntersection(Vector2D v1, Vector2D v2, double x3, double y3, double x4, double y4, out double u_ray, out double u_line)
    {
        return GetIntersection(v1, v2, x3, y3, x4, y4, out u_ray, out u_line, true);
    }

    public static bool GetIntersection(Vector2D v1, Vector2D v2, double x3, double y3, double x4, double y4, out double u_ray, out double u_line, bool bounded)
    {
        // Calculate divider
        double div = (y4 - y3) * (v2.x - v1.x) - (x4 - x3) * (v2.y - v1.y);

        if (div != 0.0)
        {
            // Calculate the intersection distance from the line
            u_line = ((x4 - x3) * (v1.y - y3) - (y4 - y3) * (v1.x - x3)) / div;

            // Calculate the intersection distance from the ray
            u_ray = ((v2.x - v1.x) * (v1.y - y3) - (v2.y - v1.y) * (v1.x - x3)) / div;

            if (bounded && (u_ray < 0.0 || u_ray > 1.0 || u_line < 0.0 || u_line > 1.0)) return false; //mxd
            return true;
        }

        u_line = double.NaN;
        u_ray = double.NaN;
        return false;
    }

    // Side test: < 0 = front (right), > 0 = back (left), 0 = on the line
    public static double GetSideOfLine(Vector2D v1, Vector2D v2, Vector2D p)
    {
        return (p.y - v1.y) * (v2.x - v1.x) - (p.x - v1.x) * (v2.y - v1.y);
    }

    public static double GetDistanceToLine(Vector2D v1, Vector2D v2, Vector2D p, bool bounded)
    {
        return Math.Sqrt(GetDistanceToLineSq(v1, v2, p, bounded));
    }

    public static double GetDistanceToLineSq(Vector2D v1, Vector2D v2, Vector2D p, bool bounded)
    {
        double lengthSq = GetLengthSq(v2.x - v1.x, v2.y - v1.y);
        if (lengthSq == 0.0) return Vector2D.DistanceSq(v1, p);

        double u = ((p.x - v1.x) * (v2.x - v1.x) + (p.y - v1.y) * (v2.y - v1.y)) / lengthSq;

        if (bounded)
        {
            if (u < 0f) u = 0f; else if (u > 1f) u = 1f;
        }

        Vector2D i = v1 + u * (v2 - v1);

        double ldx = p.x - i.x;
        double ldy = p.y - i.y;
        return ldx * ldx + ldy * ldy;
    }

    public static double GetNearestOnLine(Vector2D v1, Vector2D v2, Vector2D p)
    {
        double lengthSq = GetLengthSq(v2.x - v1.x, v2.y - v1.y);
        if (lengthSq == 0.0) return 0.0;

        return ((p.x - v1.x) * (v2.x - v1.x) + (p.y - v1.y) * (v2.y - v1.y)) / lengthSq;
    }

    public static Vector2D GetNearestPointOnLine(Vector2D v1, Vector2D v2, Vector2D p, bool bounded)
    {
        double lengthSq = GetLengthSq(v2.x - v1.x, v2.y - v1.y);
        if (lengthSq == 0.0) return v1;

        double u = ((p.x - v1.x) * (v2.x - v1.x) + (p.y - v1.y) * (v2.y - v1.y)) / lengthSq;
        if (bounded)
        {
            if (u < 0.0) u = 0.0;
            else if (u > 1.0) u = 1.0;
        }

        return GetCoordinatesAt(v1, v2, u);
    }

    public static Vector2D GetCoordinatesAt(Vector2D v1, Vector2D v2, double u)
    {
        return new Vector2D(v1.x + u * (v2.x - v1.x), v1.y + u * (v2.y - v1.y));
    }

    private static bool IsEqualFloat(double a, double b) => Math.Abs(a - b) < 0.0001f;

    // Some random self-written algorithm instead of Cohen-Sutherland algorithm which used to hang up randomly
    public static Line2D ClipToRectangle(Line2D line, RectangleF rect, out bool intersects)
    {
        double rateXY = 0f;
        if (line.v2.y != line.v1.y)
        {
            double dx = line.v2.x - line.v1.x;
            double dy = line.v2.y - line.v1.y;
            rateXY = dx / dy;
        }

        double x1 = line.v1.x, y1 = line.v1.y;
        double x2 = line.v2.x, y2 = line.v2.y;

        for (int i = 0; i < 2; i++)
        {
            // check x1,y1
            if (y1 < rect.Top)    { x1 += (rect.Top - y1) * rateXY; y1 = rect.Top; }
            if (x1 < rect.Left)   { if (rateXY != 0) y1 += (rect.Left - x1) / rateXY; x1 = rect.Left; }
            // check x2,y2
            if (y2 < rect.Top)    { x2 += (rect.Top - y2) * rateXY; y2 = rect.Top; }
            if (x2 < rect.Left)   { if (rateXY != 0) y2 += (rect.Left - x2) / rateXY; x2 = rect.Left; }
            // check x1,y1
            if (y1 > rect.Bottom) { x1 -= (y1 - rect.Bottom) * rateXY; y1 = rect.Bottom; }
            if (x1 > rect.Right)  { if (rateXY != 0) y1 -= (x1 - rect.Right) / rateXY; x1 = rect.Right; }
            // check x2,y2
            if (y2 > rect.Bottom) { x2 -= (y2 - rect.Bottom) * rateXY; y2 = rect.Bottom; }
            if (x2 > rect.Right)  { if (rateXY != 0) y2 -= (x2 - rect.Right) / rateXY; x2 = rect.Right; }
        }

        if ((IsEqualFloat(x1, x2) && (IsEqualFloat(x1, rect.Left) || IsEqualFloat(x1, rect.Right)) ||
            (IsEqualFloat(y1, y2) && (IsEqualFloat(y1, rect.Bottom) || IsEqualFloat(y1, rect.Top)))))
        {
            intersects = false;
            return new Line2D();
        }

        intersects = true;
        return new Line2D(x1, y1, x2, y2);
    }

    // Perpendicular by simply making a normal
    public Vector2D GetPerpendicular()
    {
        Vector2D d = GetDelta();
        return new Vector2D(-d.y, d.x);
    }

    public double GetAngle()
    {
        Vector2D d = GetDelta();
        return -Math.Atan2(-d.y, d.x) + Angle2D.PIHALF;
    }

    public Vector2D GetDelta() => v2 - v1;

    public double GetLength() => Line2D.GetLength(v2.x - v1.x, v2.y - v1.y);
    public double GetLengthSq() => Line2D.GetLengthSq(v2.x - v1.x, v2.y - v1.y);

    public override string ToString() => "(" + v1 + ") - (" + v2 + ")";

    public bool GetIntersection(double x3, double y3, double x4, double y4)
        => Line2D.GetIntersection(v1, v2, x3, y3, x4, y4);

    public bool GetIntersection(double x3, double y3, double x4, double y4, out double u_ray)
        => Line2D.GetIntersection(v1, v2, x3, y3, x4, y4, out u_ray, true);

    public bool GetIntersection(double x3, double y3, double x4, double y4, out double u_ray, bool bounded)
        => Line2D.GetIntersection(v1, v2, x3, y3, x4, y4, out u_ray, bounded);

    public bool GetIntersection(double x3, double y3, double x4, double y4, out double u_ray, out double u_line)
        => Line2D.GetIntersection(v1, v2, x3, y3, x4, y4, out u_ray, out u_line);

    public bool GetIntersection(Line2D ray) => Line2D.GetIntersection(v1, v2, ray.v1.x, ray.v1.y, ray.v2.x, ray.v2.y);

    public bool GetIntersection(Line2D ray, out double u_ray)
        => Line2D.GetIntersection(v1, v2, ray.v1.x, ray.v1.y, ray.v2.x, ray.v2.y, out u_ray, true);

    public bool GetIntersection(Line2D ray, out double u_ray, bool bounded)
        => Line2D.GetIntersection(v1, v2, ray.v1.x, ray.v1.y, ray.v2.x, ray.v2.y, out u_ray, bounded);

    public bool GetIntersection(Line2D ray, out double u_ray, out double u_line)
        => Line2D.GetIntersection(v1, v2, ray.v1.x, ray.v1.y, ray.v2.x, ray.v2.y, out u_ray, out u_line);

    public double GetSideOfLine(Vector2D p) => Line2D.GetSideOfLine(v1, v2, p);

    public double GetDistanceToLine(Vector2D p, bool bounded) => Line2D.GetDistanceToLine(v1, v2, p, bounded);
    public double GetDistanceToLineSq(Vector2D p, bool bounded) => Line2D.GetDistanceToLineSq(v1, v2, p, bounded);

    public double GetNearestOnLine(Vector2D p) => Line2D.GetNearestOnLine(v1, v2, p);

    public Vector2D GetNearestPointOnLine(Vector2D p, bool bounded) => Line2D.GetNearestPointOnLine(v1, v2, p, bounded);

    public Vector2D GetCoordinatesAt(double u) => Line2D.GetCoordinatesAt(v1, v2, u);

    public Line2D GetTransformed(double offsetx, double offsety, double scalex, double scaley)
        => new Line2D(v1.GetTransformed(offsetx, offsety, scalex, scaley), v2.GetTransformed(offsetx, offsety, scalex, scaley));

    public Line2D GetInvTransformed(double invoffsetx, double invoffsety, double invscalex, double invscaley)
        => new Line2D(v1.GetInvTransformed(invoffsetx, invoffsety, invscalex, invscaley),
                      v2.GetInvTransformed(invoffsetx, invoffsety, invscalex, invscaley));
}
