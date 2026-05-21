// ABOUTME: 2D double-precision vector ported from UDB Source/Core/Geometry/Vector2D.cs.
// ABOUTME: Behavior preserved 1:1; only namespace and file-scoping changed.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;

namespace DBuilder.Geometry;

public struct Vector2D : IEquatable<Vector2D>
{
    private const double TINY_VALUE = 0.0000000001f;

    // Coordinates
    public double x;
    public double y;

    public Vector2D(double x, double y)
    {
        this.x = x;
        this.y = y;
    }

    public Vector2D(Vector3D v)
    {
        this.x = v.x;
        this.y = v.y;
    }

    // Conversion to Vector3D
    public static implicit operator Vector3D(Vector2D a) => new Vector3D(a);

    public static Vector2D operator +(Vector2D a, Vector2D b) => new Vector2D(a.x + b.x, a.y + b.y);
    public static Vector2D operator +(double a, Vector2D b) => new Vector2D(a + b.x, a + b.y);
    public static Vector2D operator +(Vector2D a, double b) => new Vector2D(a.x + b, a.y + b);

    public static Vector2D operator -(Vector2D a, Vector2D b) => new Vector2D(a.x - b.x, a.y - b.y);
    public static Vector2D operator -(Vector2D a, double b) => new Vector2D(a.x - b, a.y - b);
    public static Vector2D operator -(double a, Vector2D b) => new Vector2D(a - b.x, a - b.y);
    public static Vector2D operator -(Vector2D a) => new Vector2D(-a.x, -a.y);

    public static Vector2D operator *(double s, Vector2D a) => new Vector2D(a.x * s, a.y * s);
    public static Vector2D operator *(Vector2D a, double s) => new Vector2D(a.x * s, a.y * s);
    public static Vector2D operator *(Vector2D a, Vector2D b) => new Vector2D(a.x * b.x, a.y * b.y);

    public static Vector2D operator /(double s, Vector2D a) => new Vector2D(a.x / s, a.y / s);
    public static Vector2D operator /(Vector2D a, double s) => new Vector2D(a.x / s, a.y / s);
    public static Vector2D operator /(Vector2D a, Vector2D b) => new Vector2D(a.x / b.x, a.y / b.y);

    public static double DotProduct(Vector2D a, Vector2D b) => a.x * b.x + a.y * b.y;

    // NOTE: this is UDB's original "cross product" for Vector2D, which is not the conventional
    // 2D cross. Preserved verbatim for compatibility with existing call sites.
    public static Vector2D CrossProduct(Vector2D a, Vector2D b)
    {
        Vector2D result = new Vector2D();
        result.x = a.y * b.x;
        result.y = a.x * b.y;
        return result;
    }

    public static bool operator ==(Vector2D a, Vector2D b) => (a.x == b.x) && (a.y == b.y);
    public static bool operator !=(Vector2D a, Vector2D b) => (a.x != b.x) || (a.y != b.y);

    // This reflects the vector v over mirror m. Note that mirror m must be normalized.
    // R = V - 2 * M * (M dot V)
    public static Vector2D Reflect(Vector2D v, Vector2D m)
    {
        double dp = Vector2D.DotProduct(m, v);
        Vector2D mv = new Vector2D();
        mv.x = v.x - (2f * m.x * dp);
        mv.y = v.y - (2f * m.y * dp);
        return mv;
    }

    public static Vector2D Reversed(Vector2D v) => new Vector2D(-v.x, -v.y);

    // This returns a vector from an angle
    public static Vector2D FromAngle(double angle) => new Vector2D(Math.Sin(angle), -Math.Cos(angle));

    // This returns a vector from an angle with a given length
    public static Vector2D FromAngle(double angle, double length) => FromAngle(angle) * length;

    // This calculates the angle
    public static double GetAngle(Vector2D a, Vector2D b)
    {
        return -Math.Atan2(-(a.y - b.y), (a.x - b.x)) + Angle2D.PIHALF;
    }

    public static double DistanceSq(Vector2D a, Vector2D b)
    {
        Vector2D d = a - b;
        return d.GetLengthSq();
    }

    public static double Distance(Vector2D a, Vector2D b)
    {
        Vector2D d = a - b;
        return d.GetLength();
    }

    public static double ManhattanDistance(Vector2D a, Vector2D b)
    {
        Vector2D d = a - b;
        return Math.Abs(d.x) + Math.Abs(d.y);
    }

    // Perpendicular by simply making a normal
    public Vector2D GetPerpendicular() => new Vector2D(-y, x);

    public Vector2D GetSign() => new Vector2D(Math.Sign(x), Math.Sign(y));

    public double GetAngle()
    {
        //mxd. Make sure the angle is in [0 .. PI2] range
        double angle = -Math.Atan2(-y, x) + Angle2D.PIHALF;
        if (angle < 0f) angle += Angle2D.PI2;
        return angle;
    }

    public double GetLength() => Math.Sqrt(x * x + y * y);
    public double GetLengthSq() => x * x + y * y;
    public double GetManhattanLength() => Math.Abs(x) + Math.Abs(y);

    public Vector2D GetNormal()
    {
        double lensq = this.GetLengthSq();
        if (lensq > TINY_VALUE)
        {
            double mul = 1f / Math.Sqrt(lensq);
            return new Vector2D(x * mul, y * mul);
        }
        return new Vector2D(0f, 0f);
    }

    public Vector2D GetScaled(double s) => new Vector2D(x * s, y * s);

    public Vector2D GetFixedLength(double l) => this.GetNormal().GetScaled(l);

    public override string ToString() => x + ", " + y;

    public Vector2D GetTransformed(double offsetx, double offsety, double scalex, double scaley)
    {
        return new Vector2D((x + offsetx) * scalex, (y + offsety) * scaley);
    }

    public Vector2D GetInvTransformed(double invoffsetx, double invoffsety, double invscalex, double invscaley)
    {
        return new Vector2D((x * invscalex) + invoffsetx, (y * invscaley) + invoffsety);
    }

    public Vector2D GetRotated(double theta)
    {
        double cos = Math.Cos(theta);
        double sin = Math.Sin(theta);
        double rx = cos * x - sin * y;
        double ry = sin * x + cos * y;
        return new Vector2D(rx, ry);
    }

    public bool IsFinite() => !double.IsNaN(x) && !double.IsNaN(y) && !double.IsInfinity(x) && !double.IsInfinity(y);

    public bool Equals(Vector2D other) => x == other.x && y == other.y;
    public override bool Equals(object? obj) => obj is Vector2D other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(x, y);
}
