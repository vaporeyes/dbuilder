// ABOUTME: 3D double-precision vector ported from UDB Source/Core/Geometry/Vector3D.cs.
// ABOUTME: Transform methods retargeted from UDB's renderer Matrix to System.Numerics.Matrix4x4; math preserved.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;
using System.Numerics;

namespace DBuilder.Geometry;

public struct Vector3D : IEquatable<Vector3D>
{
    private const double TINY_VALUE = 0.0000000001f;

    public double x;
    public double y;
    public double z;

    public Vector3D(double x, double y, double z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public Vector3D(Vector2D v)
    {
        this.x = v.x;
        this.y = v.y;
        this.z = 0f;
    }

    //mxd
    public Vector3D(Vector2D v, double z)
    {
        this.x = v.x;
        this.y = v.y;
        this.z = z;
    }

    // Conversion to Vector2D
    public static implicit operator Vector2D(Vector3D a) => new Vector2D(a);

    public static Vector3D operator +(Vector3D a, Vector3D b) => new Vector3D(a.x + b.x, a.y + b.y, a.z + b.z);
    public static Vector3D operator +(Vector3D a, double b) => new Vector3D(a.x + b, a.y + b, a.z + b);
    public static Vector3D operator +(double b, Vector3D a) => new Vector3D(a.x + b, a.y + b, a.z + b);

    public static Vector3D operator -(Vector3D a, Vector3D b) => new Vector3D(a.x - b.x, a.y - b.y, a.z - b.z);
    public static Vector3D operator -(Vector3D a, double b) => new Vector3D(a.x - b, a.y - b, a.z - b);
    public static Vector3D operator -(double a, Vector3D b) => new Vector3D(a - b.x, a - b.y, a - b.z);
    public static Vector3D operator -(Vector3D a) => new Vector3D(-a.x, -a.y, -a.z);

    public static Vector3D operator *(double s, Vector3D a) => new Vector3D(a.x * s, a.y * s, a.z * s);
    public static Vector3D operator *(Vector3D a, double s) => new Vector3D(a.x * s, a.y * s, a.z * s);
    public static Vector3D operator *(Vector3D a, Vector3D b) => new Vector3D(a.x * b.x, a.y * b.y, a.z * b.z);

    public static Vector3D operator /(double s, Vector3D a) => new Vector3D(a.x / s, a.y / s, a.z / s);
    public static Vector3D operator /(Vector3D a, double s) => new Vector3D(a.x / s, a.y / s, a.z / s);
    public static Vector3D operator /(Vector3D a, Vector3D b) => new Vector3D(a.x / b.x, a.y / b.y, a.z / b.z);

    public static bool operator ==(Vector3D a, Vector3D b) => (a.x == b.x) && (a.y == b.y) && (a.z == b.z);
    public static bool operator !=(Vector3D a, Vector3D b) => (a.x != b.x) || (a.y != b.y) || (a.z != b.z);

    public static Vector3D CrossProduct(Vector3D a, Vector3D b)
    {
        Vector3D result = new Vector3D();
        result.x = a.y * b.z - a.z * b.y;
        result.y = a.z * b.x - a.x * b.z;
        result.z = a.x * b.y - a.y * b.x;
        return result;
    }

    public static double DotProduct(Vector3D a, Vector3D b) => a.x * b.x + a.y * b.y + a.z * b.z;

    // This reflects the vector v over mirror m. Note that mirror m must be normalized.
    public static Vector3D Reflect(Vector3D v, Vector3D m)
    {
        double dp = Vector3D.DotProduct(v, m);
        Vector3D mv = new Vector3D();
        mv.x = -v.x + 2f * m.x * dp;
        mv.y = -v.y + 2f * m.y * dp;
        mv.z = -v.z + 2f * m.z * dp;
        return mv;
    }

    public static Vector3D Reversed(Vector3D v) => new Vector3D(-v.x, -v.y, -v.z);

    public static Vector3D FromAngleXY(double angle) => new Vector3D(Math.Sin(angle), -Math.Cos(angle), 0f);
    public static Vector3D FromAngleXY(double angle, double length) => FromAngleXY(angle) * length;

    public static Vector3D FromAngleXYZ(double anglexy, double anglez)
    {
        double ax = Math.Sin(anglexy) * Math.Cos(anglez);
        double ay = -Math.Cos(anglexy) * Math.Cos(anglez);
        double az = Math.Sin(anglez);
        return new Vector3D(ax, ay, az);
    }

    //mxd. Uses UDB's original element access (M11..M44 are read as M_row_col element values).
    // System.Numerics.Matrix4x4 has identically-named fields, so the math carries over unchanged.
    public static Vector3D Transform(Vector3D v, Matrix4x4 m)
    {
        return new Vector3D
        {
            x = m.M11 * v.x + m.M21 * v.y + m.M31 * v.z + m.M41,
            y = m.M12 * v.x + m.M22 * v.y + m.M32 * v.z + m.M42,
            z = m.M13 * v.x + m.M23 * v.y + m.M33 * v.z + m.M43,
        };
    }

    //mxd
    public static Vector3D Transform(double x, double y, double z, Matrix4x4 m)
    {
        return new Vector3D
        {
            x = m.M11 * x + m.M21 * y + m.M31 * z + m.M41,
            y = m.M12 * x + m.M22 * y + m.M32 * z + m.M42,
            z = m.M13 * x + m.M23 * y + m.M33 * z + m.M43,
        };
    }

    public double GetAngleXY()
    {
        return -Math.Atan2(-y, x) + Angle2D.PIHALF;
    }

    public double GetAngleZ()
    {
        Vector2D xy = new Vector2D(x, y);
        return Math.Atan2(xy.GetLength(), z) + Angle2D.PIHALF;
    }

    public double GetLength() => Math.Sqrt(x * x + y * y + z * z);
    public double GetLengthSq() => x * x + y * y + z * z;
    public double GetManhattanLength() => Math.Abs(x) + Math.Abs(y) + Math.Abs(z);

    public Vector3D GetNormal()
    {
        double lensq = this.GetLengthSq();
        if (lensq > TINY_VALUE)
        {
            double mul = 1f / Math.Sqrt(lensq);
            return new Vector3D(x * mul, y * mul, z * mul);
        }
        return new Vector3D(0f, 0f, 0f);
    }

    public Vector3D GetScaled(double s) => new Vector3D(x * s, y * s, z * s);
    public Vector3D GetFixedLength(double l) => this.GetNormal().GetScaled(l);

    public bool IsNormalized() => (Math.Abs(GetLengthSq() - 1.0f) < 0.0001f);

    public override string ToString() => x + ", " + y + ", " + z;

    public bool IsFinite() =>
        !double.IsNaN(x) && !double.IsNaN(y) && !double.IsNaN(z) &&
        !double.IsInfinity(x) && !double.IsInfinity(y) && !double.IsInfinity(z);

    public bool Equals(Vector3D other) => x == other.x && y == other.y && z == other.z;
    public override bool Equals(object? obj) => obj is Vector3D other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(x, y, z);
}
