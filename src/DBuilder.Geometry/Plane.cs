// ABOUTME: Infinite 3D plane ported from UDB Source/Core/Geometry/Plane.cs.
// ABOUTME: Behavior preserved 1:1.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;

namespace DBuilder.Geometry;

public struct Plane : IEquatable<Plane>
{
    //
    // Plane definition:
    // A * x + B * y + C * z + D = 0
    //
    // A, B, C is the normal
    // D is the offset along the normal (negative)
    //
    private Vector3D normal;
    private double offset;

    public Vector3D Normal => normal;
    public double Offset { get => offset; set => offset = value; }
    public double a => normal.x;
    public double b => normal.y;
    public double c => normal.z;
    public double d { get => offset; set => offset = value; }

    public Plane(Vector3D normal, double offset)
    {
#if DEBUG
        if (!normal.IsNormalized())
            throw new NotSupportedException("Attempt to create a plane with a vector that is not normalized!");
#endif
        this.normal = normal;
        this.offset = offset;
    }

    public Plane(Vector3D normal, Vector3D position)
    {
#if DEBUG
        if (!normal.IsNormalized())
            throw new NotSupportedException("Attempt to create a plane with a vector that is not normalized!");
#endif
        this.normal = normal;
        this.offset = -Vector3D.DotProduct(normal, position);
    }

    public Plane(Vector3D p1, Vector3D p2, Vector3D p3, bool up)
    {
        this.normal = Vector3D.CrossProduct(p2 - p1, p3 - p1).GetNormal();

        if ((up && (this.normal.z < 0.0f)) || (!up && (this.normal.z > 0.0f)))
            this.normal = -this.normal;

        this.offset = -Vector3D.DotProduct(normal, p3);
    }

    //mxd
    public Plane(Vector3D center, double anglexy, double anglez, bool up)
    {
        Vector2D point = new Vector2D(center.x + Math.Cos(anglexy) * Math.Sin(anglez), center.y + Math.Sin(anglexy) * Math.Sin(anglez));
        Vector2D perpendicular = new Line2D(center, point).GetPerpendicular();

        Vector3D p2 = new Vector3D(point.x + perpendicular.x, point.y + perpendicular.y, center.z + Math.Cos(anglez));
        Vector3D p3 = new Vector3D(point.x - perpendicular.x, point.y - perpendicular.y, center.z + Math.Cos(anglez));

        this.normal = Vector3D.CrossProduct(p2 - center, p3 - center).GetNormal();

        if ((up && (this.normal.z < 0.0f)) || (!up && (this.normal.z > 0.0f)))
            this.normal = -this.normal;

        this.offset = -Vector3D.DotProduct(normal, p3);
    }

    /// <summary>
    /// Intersection with a line.
    /// See http://local.wasp.uwa.edu.au/~pbourke/geometry/planeline/
    /// </summary>
    public bool GetIntersection(Vector3D from, Vector3D to, ref double u_ray)
    {
        double w = Vector3D.DotProduct(normal, from - to);
        if (w != 0.0f)
        {
            double v = Vector3D.DotProduct(normal, from);
            u_ray = (offset + v) / w;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Smallest signed distance to the plane.
    /// &gt; 0 means the point lies on the front of the plane,
    /// &lt; 0 means behind.
    /// See http://mathworld.wolfram.com/Point-PlaneDistance.html
    /// </summary>
    public double Distance(Vector3D p) => Vector3D.DotProduct(normal, p) + offset;

    /// <summary>Closest point on the plane to a given point.</summary>
    public Vector3D ClosestOnPlane(Vector3D p) => p - normal * this.Distance(p);

    /// <summary>Z on the plane at (X, Y).</summary>
    public double GetZ(Vector2D pos) => (-offset - Vector2D.DotProduct(normal, pos)) / normal.z;

    /// <summary>Z on the plane at (X, Y).</summary>
    public double GetZ(double x, double y) => (-offset - (normal.x * x + normal.y * y)) / normal.z;

    public Plane GetInverted() => new Plane(-normal, -offset);

    public bool Equals(Plane other) => normal == other.normal && offset == other.offset;
    public override bool Equals(object? obj) => obj is Plane other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(normal, offset);

    public static bool operator ==(Plane a, Plane b) => (a.normal == b.normal) && (a.offset == b.offset);
    public static bool operator !=(Plane a, Plane b) => (a.normal != b.normal) || (a.offset != b.offset);
}
