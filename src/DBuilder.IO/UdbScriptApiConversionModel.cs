// ABOUTME: Models UDBScript API helper conversions for vectors and universal values.
// ABOUTME: Provides pure conversion targets for future script API wrapper execution.

using System.Collections;
using System.Dynamic;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed record UdbScriptHighlightedSidedefPart(Sidedef Sidedef, SidedefPart Part);

public sealed record UdbScriptHighlightedSectorSurface(Sector Sector, bool FloorHighlighted, bool CeilingHighlighted);

public sealed class UdbScriptVector2DWrapper : IEquatable<UdbScriptVector2DWrapper>
{
    private readonly Action<Vector2D>? changed;
    private double xValue;
    private double yValue;

    public UdbScriptVector2DWrapper(double X, double Y)
        : this(X, Y, null)
    {
    }

    public UdbScriptVector2DWrapper(double X, double Y, Action<Vector2D>? changed)
    {
        xValue = X;
        yValue = Y;
        this.changed = changed;
    }

    public double X
        => xValue;

    public double Y
        => yValue;

    public double x
    {
        get => xValue;
        set
        {
            xValue = value;
            NotifyChanged();
        }
    }

    public double y
    {
        get => yValue;
        set
        {
            yValue = value;
            NotifyChanged();
        }
    }

    public UdbScriptVector2DWrapper(object value)
        : this(ToVector2D(value).x, ToVector2D(value).y)
    {
    }

    public Vector2D AsVector2D()
        => new(X, Y);

    public static implicit operator UdbScriptVector3DWrapper(UdbScriptVector2DWrapper value)
        => new(value.X, value.Y, 0.0);

    public static object operator +(UdbScriptVector2DWrapper lhs, object rhs)
    {
        if (rhs is double number)
            return new UdbScriptVector2DWrapper(lhs.X + number, lhs.Y + number);

        if (TryVector2D(rhs, out Vector2D vector))
            return new UdbScriptVector2DWrapper(lhs.X + vector.x, lhs.Y + vector.y);

        return string.Concat(lhs, rhs);
    }

    public static object operator +(object lhs, UdbScriptVector2DWrapper rhs)
    {
        if (lhs is double number)
            return new UdbScriptVector2DWrapper(number + rhs.X, number + rhs.Y);

        if (TryVector2D(lhs, out Vector2D vector))
            return new UdbScriptVector2DWrapper(vector.x + rhs.X, vector.y + rhs.Y);

        return string.Concat(lhs, rhs);
    }

    public static object operator -(UdbScriptVector2DWrapper lhs, object rhs)
    {
        if (rhs is double number)
            return new UdbScriptVector2DWrapper(lhs.X - number, lhs.Y - number);

        Vector2D vector = ToVector2D(rhs);
        return new UdbScriptVector2DWrapper(lhs.X - vector.x, lhs.Y - vector.y);
    }

    public static object operator -(object lhs, UdbScriptVector2DWrapper rhs)
    {
        if (lhs is double number)
            return new UdbScriptVector2DWrapper(number - rhs.X, number - rhs.Y);

        Vector2D vector = ToVector2D(lhs);
        return new UdbScriptVector2DWrapper(vector.x - rhs.X, vector.y - rhs.Y);
    }

    public static UdbScriptVector2DWrapper operator -(UdbScriptVector2DWrapper value)
        => new(-value.X, -value.Y);

    public static object operator *(UdbScriptVector2DWrapper lhs, object rhs)
    {
        if (rhs is double number)
            return new UdbScriptVector2DWrapper(lhs.X * number, lhs.Y * number);

        Vector2D vector = ToVector2D(rhs);
        return new UdbScriptVector2DWrapper(lhs.X * vector.x, lhs.Y * vector.y);
    }

    public static object operator *(object lhs, UdbScriptVector2DWrapper rhs)
    {
        if (lhs is double number)
            return new UdbScriptVector2DWrapper(number * rhs.X, number * rhs.Y);

        Vector2D vector = ToVector2D(lhs);
        return new UdbScriptVector2DWrapper(vector.x * rhs.X, vector.y * rhs.Y);
    }

    public static object operator /(UdbScriptVector2DWrapper lhs, object rhs)
    {
        if (rhs is double number)
            return new UdbScriptVector2DWrapper(lhs.X / number, lhs.Y / number);

        Vector2D vector = ToVector2D(rhs);
        return new UdbScriptVector2DWrapper(lhs.X / vector.x, lhs.Y / vector.y);
    }

    public static object operator /(object lhs, UdbScriptVector2DWrapper rhs)
    {
        if (lhs is double number)
            return new UdbScriptVector2DWrapper(rhs.X / number, rhs.Y / number);

        Vector2D vector = ToVector2D(lhs);
        return new UdbScriptVector2DWrapper(vector.x / rhs.X, vector.y / rhs.Y);
    }

    public static bool operator ==(UdbScriptVector2DWrapper lhs, object rhs)
    {
        Vector2D vector = ToVector2D(rhs);
        return lhs.X == vector.x && lhs.Y == vector.y;
    }

    public static bool operator ==(object lhs, UdbScriptVector2DWrapper rhs)
    {
        Vector2D vector = ToVector2D(lhs);
        return vector.x == rhs.X && vector.y == rhs.Y;
    }

    public static bool operator !=(UdbScriptVector2DWrapper lhs, object rhs)
    {
        Vector2D vector = ToVector2D(rhs);
        return lhs.X != vector.x || lhs.Y != vector.y;
    }

    public static bool operator !=(object lhs, UdbScriptVector2DWrapper rhs)
    {
        Vector2D vector = ToVector2D(lhs);
        return vector.x != rhs.X || vector.y != rhs.Y;
    }

    public static double dotProduct(UdbScriptVector2DWrapper a, UdbScriptVector2DWrapper b)
        => a.X * b.X + a.Y * b.Y;

    public static UdbScriptVector2DWrapper crossProduct(object a, object b)
    {
        Vector2D first = ToVector2D(a);
        Vector2D second = ToVector2D(b);
        return new UdbScriptVector2DWrapper(first.y * second.x, first.x * second.y);
    }

    public static UdbScriptVector2DWrapper reflect(object value, object mirror)
    {
        Vector2D reflected = Vector2D.Reflect(ToVector2D(value), ToVector2D(mirror));
        return new UdbScriptVector2DWrapper(reflected.x, reflected.y);
    }

    public static UdbScriptVector2DWrapper reversed(object value)
    {
        Vector2D reversed = Vector2D.Reversed(ToVector2D(value));
        return new UdbScriptVector2DWrapper(reversed.x, reversed.y);
    }

    public static UdbScriptVector2DWrapper fromAngleRad(double angle)
    {
        Vector2D vector = Vector2D.FromAngle(angle);
        return new UdbScriptVector2DWrapper(vector.x, vector.y);
    }

    public static UdbScriptVector2DWrapper fromAngle(double angle)
        => fromAngleRad(Angle2D.DegToRad(angle));

    public static double getAngleRad(object a, object b)
        => Vector2D.GetAngle(ToVector2D(a), ToVector2D(b));

    public static double getAngle(object a, object b)
        => Angle2D.RadToDeg(getAngleRad(a, b));

    public static double getDistanceSq(object a, object b)
        => Vector2D.DistanceSq(ToVector2D(a), ToVector2D(b));

    public static double getDistance(object a, object b)
        => Vector2D.Distance(ToVector2D(a), ToVector2D(b));

    public UdbScriptVector2DWrapper getPerpendicular()
        => new(-Y, X);

    public UdbScriptVector2DWrapper getSign()
    {
        Vector2D vector = AsVector2D().GetSign();
        return new UdbScriptVector2DWrapper(vector.x, vector.y);
    }

    public double getAngleRad()
        => AsVector2D().GetAngle();

    public double getAngle()
        => Angle2D.RadToDeg(getAngleRad());

    public double getLength()
        => AsVector2D().GetLength();

    public double getLengthSq()
        => AsVector2D().GetLengthSq();

    public UdbScriptVector2DWrapper getNormal()
    {
        Vector2D vector = AsVector2D().GetNormal();
        return new UdbScriptVector2DWrapper(vector.x, vector.y);
    }

    public UdbScriptVector2DWrapper getTransformed(double offsetx, double offsety, double scalex, double scaley)
    {
        Vector2D vector = AsVector2D().GetTransformed(offsetx, offsety, scalex, scaley);
        return new UdbScriptVector2DWrapper(vector.x, vector.y);
    }

    public UdbScriptVector2DWrapper getInverseTransformed(double invoffsetx, double invoffsety, double invscalex, double invscaley)
    {
        Vector2D vector = AsVector2D().GetInvTransformed(invoffsetx, invoffsety, invscalex, invscaley);
        return new UdbScriptVector2DWrapper(vector.x, vector.y);
    }

    public UdbScriptVector2DWrapper getRotated(double theta)
        => getRotatedRad(Angle2D.DegToRad(theta));

    public UdbScriptVector2DWrapper getRotatedRad(double theta)
    {
        Vector2D vector = AsVector2D().GetRotated(theta);
        return new UdbScriptVector2DWrapper(vector.x, vector.y);
    }

    public bool isFinite()
        => AsVector2D().IsFinite();

    public override string ToString()
        => AsVector2D().ToString();

    public bool Equals(UdbScriptVector2DWrapper? other)
        => other is not null && X == other.X && Y == other.Y;

    public override bool Equals(object? obj)
        => obj is UdbScriptVector2DWrapper other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(X, Y);

    private void NotifyChanged()
        => changed?.Invoke(new Vector2D(X, Y));

    private static bool TryVector2D(object value, out Vector2D vector)
    {
        try
        {
            vector = UdbScriptApiConversionModel.GetVector3DFromObject(value);
            return true;
        }
        catch (UdbScriptVectorConversionException)
        {
            vector = default;
            return false;
        }
    }

    private static Vector2D ToVector2D(object value)
        => UdbScriptApiConversionModel.GetVector3DFromObject(value);
}

public sealed class UdbScriptVector3DWrapper : IEquatable<UdbScriptVector3DWrapper>
{
    private readonly Action<Vector3D>? changed;
    private double xValue;
    private double yValue;
    private double zValue;

    public UdbScriptVector3DWrapper(double X, double Y, double Z)
        : this(X, Y, Z, null)
    {
    }

    public UdbScriptVector3DWrapper(double X, double Y, double Z, Action<Vector3D>? changed)
    {
        xValue = X;
        yValue = Y;
        zValue = Z;
        this.changed = changed;
    }

    public double X
        => xValue;

    public double Y
        => yValue;

    public double Z
        => zValue;

    public double x
    {
        get => xValue;
        set
        {
            xValue = value;
            NotifyChanged();
        }
    }

    public double y
    {
        get => yValue;
        set
        {
            yValue = value;
            NotifyChanged();
        }
    }

    public double z
    {
        get => zValue;
        set
        {
            zValue = value;
            NotifyChanged();
        }
    }

    public UdbScriptVector3DWrapper(object value)
        : this(ToVector3D(value).x, ToVector3D(value).y, ToVector3D(value).z)
    {
    }

    public Vector3D AsVector3D()
        => new(X, Y, Z);

    public static implicit operator UdbScriptVector2DWrapper(UdbScriptVector3DWrapper value)
        => new(value.X, value.Y);

    public static object operator +(UdbScriptVector3DWrapper lhs, object rhs)
    {
        if (rhs is double number)
            return new UdbScriptVector3DWrapper(lhs.X + number, lhs.Y + number, lhs.Z + number);

        if (TryVector3D(rhs, out Vector3D vector))
            return new UdbScriptVector3DWrapper(lhs.X + vector.x, lhs.Y + vector.y, lhs.Z + vector.z);

        return string.Concat(lhs, rhs);
    }

    public static object operator +(object lhs, UdbScriptVector3DWrapper rhs)
    {
        if (lhs is double number)
            return new UdbScriptVector3DWrapper(number + rhs.X, number + rhs.Y, number + rhs.Z);

        if (TryVector3D(lhs, out Vector3D vector))
            return new UdbScriptVector3DWrapper(vector.x + rhs.X, vector.y + rhs.Y, vector.z + rhs.Z);

        return string.Concat(lhs, rhs);
    }

    public static object operator -(UdbScriptVector3DWrapper lhs, object rhs)
    {
        if (rhs is double number)
            return new UdbScriptVector3DWrapper(lhs.X - number, lhs.Y - number, lhs.Z - number);

        Vector3D vector = ToVector3D(rhs);
        return new UdbScriptVector3DWrapper(lhs.X - vector.x, lhs.Y - vector.y, lhs.Z - vector.z);
    }

    public static object operator -(object lhs, UdbScriptVector3DWrapper rhs)
    {
        if (lhs is double number)
            return new UdbScriptVector3DWrapper(rhs.X - number, rhs.Y - number, rhs.Z - number);

        Vector3D vector = ToVector3D(lhs);
        return new UdbScriptVector3DWrapper(vector.x - rhs.X, vector.y - rhs.Y, vector.z - rhs.Z);
    }

    public static object operator *(UdbScriptVector3DWrapper lhs, object rhs)
    {
        if (rhs is double number)
            return new UdbScriptVector3DWrapper(lhs.X * number, lhs.Y * number, lhs.Z * number);

        Vector3D vector = ToVector3D(rhs);
        return new UdbScriptVector3DWrapper(lhs.X * vector.x, lhs.Y * vector.y, lhs.Z * vector.z);
    }

    public static object operator *(object lhs, UdbScriptVector3DWrapper rhs)
    {
        if (lhs is double number)
            return new UdbScriptVector3DWrapper(rhs.X * number, rhs.Y * number, rhs.Z * number);

        Vector3D vector = ToVector3D(lhs);
        return new UdbScriptVector3DWrapper(vector.x * rhs.X, vector.y * rhs.Y, vector.z * rhs.Z);
    }

    public static object operator /(UdbScriptVector3DWrapper lhs, object rhs)
    {
        if (rhs is double number)
            return new UdbScriptVector3DWrapper(lhs.X / number, lhs.Y / number, lhs.Z / number);

        Vector3D vector = ToVector3D(rhs);
        return new UdbScriptVector3DWrapper(lhs.X / vector.x, lhs.Y / vector.y, lhs.Z / vector.z);
    }

    public static object operator /(object lhs, UdbScriptVector3DWrapper rhs)
    {
        if (lhs is double number)
            return new UdbScriptVector3DWrapper(rhs.X / number, rhs.Y / number, rhs.Z / number);

        Vector3D vector = ToVector3D(lhs);
        return new UdbScriptVector3DWrapper(vector.x / rhs.X, vector.y / rhs.Y, vector.z / rhs.Z);
    }

    public static bool operator ==(UdbScriptVector3DWrapper lhs, object rhs)
    {
        Vector3D vector = ToVector3D(rhs);
        return lhs.X == vector.x && lhs.Y == vector.y && lhs.Z == vector.z;
    }

    public static bool operator ==(object lhs, UdbScriptVector3DWrapper rhs)
    {
        Vector3D vector = ToVector3D(lhs);
        return vector.x == rhs.X && vector.y == rhs.Y && vector.z == rhs.Z;
    }

    public static bool operator !=(UdbScriptVector3DWrapper lhs, object rhs)
    {
        Vector3D vector = ToVector3D(rhs);
        return lhs.X != vector.x || lhs.Y != vector.y || lhs.Z != vector.z;
    }

    public static bool operator !=(object lhs, UdbScriptVector3DWrapper rhs)
    {
        Vector3D vector = ToVector3D(lhs);
        return vector.x != rhs.X || vector.y != rhs.Y || vector.z != rhs.Z;
    }

    public static double dotProduct(UdbScriptVector3DWrapper a, UdbScriptVector3DWrapper b)
        => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static UdbScriptVector3DWrapper crossProduct(object a, object b)
    {
        Vector3D first = ToVector3D(a);
        Vector3D second = ToVector3D(b);
        Vector3D vector = Vector3D.CrossProduct(first, second);
        return new UdbScriptVector3DWrapper(vector.x, vector.y, vector.z);
    }

    public static UdbScriptVector3DWrapper reflect(object value, object mirror)
    {
        Vector3D reflected = Vector3D.Reflect(ToVector3D(value), ToVector3D(mirror));
        return new UdbScriptVector3DWrapper(reflected.x, reflected.y, reflected.z);
    }

    public static UdbScriptVector3DWrapper reversed(object value)
    {
        Vector3D reversed = Vector3D.Reversed(ToVector3D(value));
        return new UdbScriptVector3DWrapper(reversed.x, reversed.y, reversed.z);
    }

    public static UdbScriptVector3DWrapper fromAngleXYRad(double angle)
    {
        Vector3D vector = Vector3D.FromAngleXY(angle);
        return new UdbScriptVector3DWrapper(vector.x, vector.y, vector.z);
    }

    public static UdbScriptVector3DWrapper fromAngleXY(double angle)
        => fromAngleXYRad(Angle2D.DegToRad(angle));

    public static UdbScriptVector3DWrapper fromAngleXYZRad(double anglexy, double anglez)
    {
        Vector3D vector = Vector3D.FromAngleXYZ(anglexy, anglez);
        return new UdbScriptVector3DWrapper(vector.x, vector.y, vector.z);
    }

    public static UdbScriptVector3DWrapper fromAngleXYZ(double anglexy, double anglez)
        => fromAngleXYZRad(Angle2D.DegToRad(anglexy), Angle2D.DegToRad(anglez));

    public double getAngleXYRad()
        => AsVector3D().GetAngleXY();

    public double getAngleXY()
        => Angle2D.RadToDeg(getAngleXYRad());

    public double getAngleZRad()
        => AsVector3D().GetAngleZ();

    public double getAngleZ()
        => Angle2D.RadToDeg(getAngleZRad());

    public double getLength()
        => AsVector3D().GetLength();

    public double getLengthSq()
        => AsVector3D().GetLengthSq();

    public UdbScriptVector3DWrapper getNormal()
    {
        Vector3D vector = AsVector3D().GetNormal();
        return new UdbScriptVector3DWrapper(vector.x, vector.y, vector.z);
    }

    public UdbScriptVector3DWrapper getScaled(double scale)
    {
        Vector3D vector = AsVector3D().GetScaled(scale);
        return new UdbScriptVector3DWrapper(vector.x, vector.y, vector.z);
    }

    public bool isNormalized()
        => AsVector3D().IsNormalized();

    public bool isFinite()
        => AsVector3D().IsFinite();

    public override string ToString()
        => AsVector3D().ToString();

    public bool Equals(UdbScriptVector3DWrapper? other)
        => other is not null && X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj)
        => obj is UdbScriptVector3DWrapper other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(X, Y, Z);

    private void NotifyChanged()
        => changed?.Invoke(new Vector3D(X, Y, Z));

    private static bool TryVector3D(object value, out Vector3D vector)
    {
        try
        {
            vector = UdbScriptApiConversionModel.GetVector3DFromObject(value);
            return true;
        }
        catch (UdbScriptVectorConversionException)
        {
            vector = default;
            return false;
        }
    }

    private static Vector3D ToVector3D(object value)
        => UdbScriptApiConversionModel.GetVector3DFromObject(value);
}

public sealed record UdbScriptLine2DWrapper(UdbScriptVector2DWrapper V1, UdbScriptVector2DWrapper V2)
{
    public UdbScriptVector2DWrapper v1 => V1;

    public UdbScriptVector2DWrapper v2 => V2;

    public UdbScriptLine2DWrapper(object v1, object v2)
        : this(new UdbScriptVector2DWrapper(v1), new UdbScriptVector2DWrapper(v2))
    {
    }

    public UdbScriptLine2DWrapper(Line2D line)
        : this(new UdbScriptVector2DWrapper(line.v1.x, line.v1.y), new UdbScriptVector2DWrapper(line.v2.x, line.v2.y))
    {
    }

    public Line2D AsLine2D()
        => new(V1.AsVector2D(), V2.AsVector2D());

    public static bool areIntersecting(UdbScriptLine2DWrapper line1, UdbScriptLine2DWrapper line2, bool bounded = true)
    {
        return Line2D.GetIntersection(
            line1.V1.AsVector2D(),
            line1.V2.AsVector2D(),
            line2.V1.X,
            line2.V1.Y,
            line2.V2.X,
            line2.V2.Y,
            out _,
            bounded);
    }

    public static bool areIntersecting(object a1, object a2, object b1, object b2, bool bounded = true)
    {
        Vector2D firstStart = ToVector2D(a1);
        Vector2D firstEnd = ToVector2D(a2);
        Vector2D secondStart = ToVector2D(b1);
        Vector2D secondEnd = ToVector2D(b2);

        return Line2D.GetIntersection(
            firstStart,
            firstEnd,
            secondStart.x,
            secondStart.y,
            secondEnd.x,
            secondEnd.y,
            out _,
            bounded);
    }

    public static UdbScriptVector2DWrapper getIntersectionPoint(object a1, object a2, object b1, object b2, bool bounded = true)
    {
        Vector2D firstStart = ToVector2D(a1);
        Vector2D firstEnd = ToVector2D(a2);
        Vector2D secondStart = ToVector2D(b1);
        Vector2D secondEnd = ToVector2D(b2);
        Vector2D point = Line2D.GetIntersectionPoint(new Line2D(firstStart, firstEnd), new Line2D(secondStart, secondEnd), bounded);

        return new UdbScriptVector2DWrapper(point.x, point.y);
    }

    public static double getSideOfLine(object v1, object v2, object p)
        => Line2D.GetSideOfLine(ToVector2D(v1), ToVector2D(v2), ToVector2D(p));

    public static double getDistanceToLine(object v1, object v2, object p, bool bounded = true)
        => Line2D.GetDistanceToLine(ToVector2D(v1), ToVector2D(v2), ToVector2D(p), bounded);

    public static double getDistanceToLineSq(object v1, object v2, object p, bool bounded = true)
        => Line2D.GetDistanceToLineSq(ToVector2D(v1), ToVector2D(v2), ToVector2D(p), bounded);

    public static double getNearestOnLine(object v1, object v2, object p)
        => Line2D.GetNearestOnLine(ToVector2D(v1), ToVector2D(v2), ToVector2D(p));

    public static UdbScriptVector2DWrapper getNearestPointOnLine(object v1, object v2, object p, bool bounded = true)
    {
        Vector2D point = Line2D.GetNearestPointOnLine(ToVector2D(v1), ToVector2D(v2), ToVector2D(p), bounded);
        return new UdbScriptVector2DWrapper(point.x, point.y);
    }

    public static UdbScriptVector2DWrapper getCoordinatesAt(object v1, object v2, double u)
    {
        Vector2D point = Line2D.GetCoordinatesAt(ToVector2D(v1), ToVector2D(v2), u);
        return new UdbScriptVector2DWrapper(point.x, point.y);
    }

    public UdbScriptVector2DWrapper getCoordinatesAt(double u)
    {
        Vector2D point = AsLine2D().GetCoordinatesAt(u);
        return new UdbScriptVector2DWrapper(point.x, point.y);
    }

    public double getLength()
        => AsLine2D().GetLength();

    public double getAngleRad()
        => AsLine2D().GetAngle();

    public double getAngle()
        => Angle2D.RadToDeg(getAngleRad());

    public UdbScriptVector2DWrapper getPerpendicular()
    {
        Vector2D perpendicular = AsLine2D().GetPerpendicular();
        return new UdbScriptVector2DWrapper(perpendicular.x, perpendicular.y);
    }

    public bool isIntersecting(UdbScriptLine2DWrapper ray, bool bounded = true)
    {
        return AsLine2D().GetIntersection(ray.V1.X, ray.V1.Y, ray.V2.X, ray.V2.Y, out _, bounded);
    }

    public bool isIntersecting(object a1, object a2, bool bounded = true)
    {
        Vector2D start = ToVector2D(a1);
        Vector2D end = ToVector2D(a2);
        return AsLine2D().GetIntersection(start.x, start.y, end.x, end.y, out _, bounded);
    }

    public UdbScriptVector2DWrapper getIntersectionPoint(object a1, object a2, bool bounded = true)
    {
        Vector2D start = ToVector2D(a1);
        Vector2D end = ToVector2D(a2);
        Line2D line = AsLine2D();
        line.GetIntersection(start.x, start.y, end.x, end.y, out double uRay, bounded);
        Vector2D point = line.GetCoordinatesAt(uRay);

        return new UdbScriptVector2DWrapper(point.x, point.y);
    }

    public UdbScriptVector2DWrapper getIntersectionPoint(UdbScriptLine2DWrapper ray, bool bounded = true)
    {
        Line2D line = AsLine2D();
        line.GetIntersection(ray.AsLine2D(), out double uRay, bounded);
        Vector2D point = line.GetCoordinatesAt(uRay);

        return new UdbScriptVector2DWrapper(point.x, point.y);
    }

    public double getSideOfLine(object p)
        => AsLine2D().GetSideOfLine(ToVector2D(p));

    public UdbScriptVector2DWrapper getNearestPointOnLine(object p, bool bounded = true)
    {
        Vector2D point = AsLine2D().GetNearestPointOnLine(ToVector2D(p), bounded);
        return new UdbScriptVector2DWrapper(point.x, point.y);
    }

    public override string ToString()
        => AsLine2D().ToString();

    private static Vector2D ToVector2D(object value)
        => UdbScriptApiConversionModel.GetVector3DFromObject(value);
}

public static class UdbScriptAngle2DWrapper
{
    public static double doomToReal(int doomangle)
        => normalized(doomangle + 90);

    public static double doomToRealRad(int doomangle)
        => Angle2D.DoomToReal(doomangle);

    public static int realToDoom(double realangle)
        => normalized((int)(realangle - 90));

    public static int realToDoomRad(double realangle)
        => Angle2D.RealToDoom(realangle);

    public static double radToDeg(double rad)
        => Angle2D.RadToDeg(rad);

    public static double degToRad(double deg)
        => Angle2D.DegToRad(deg);

    public static int normalized(int angle)
    {
        while (angle < 0) angle += 360;
        while (angle >= 360) angle -= 360;
        return angle;
    }

    public static double normalizedRad(double angle)
        => Angle2D.Normalized(angle);

    public static double getAngle(object p1, object p2, object p3)
        => Angle2D.RadToDeg(getAngleRad(p1, p2, p3));

    public static double getAngleRad(object p1, object p2, object p3)
    {
        Vector2D first = UdbScriptApiConversionModel.GetVector3DFromObject(p1);
        Vector2D second = UdbScriptApiConversionModel.GetVector3DFromObject(p2);
        Vector2D third = UdbScriptApiConversionModel.GetVector3DFromObject(p3);
        return Angle2D.GetAngle(first, second, third);
    }
}

public sealed class UdbScriptPlaneWrapper
{
    private DBuilder.Geometry.Plane plane;

    public UdbScriptPlaneWrapper(object normal, double offset)
    {
        plane = new DBuilder.Geometry.Plane(UdbScriptApiConversionModel.GetVector3DFromObject(normal), offset);
    }

    public UdbScriptPlaneWrapper(object p1, object p2, object p3, bool up)
    {
        plane = new DBuilder.Geometry.Plane(
            UdbScriptApiConversionModel.GetVector3DFromObject(p1),
            UdbScriptApiConversionModel.GetVector3DFromObject(p2),
            UdbScriptApiConversionModel.GetVector3DFromObject(p3),
            up);
    }

    public UdbScriptPlaneWrapper(DBuilder.Geometry.Plane plane)
    {
        this.plane = plane;
    }

    public UdbScriptVector3DWrapper normal
        => new(plane.Normal.x, plane.Normal.y, plane.Normal.z);

    public double offset
    {
        get => plane.Offset;
        set => plane.Offset = value;
    }

    public double a => plane.a;

    public double b => plane.b;

    public double c => plane.c;

    public double d
    {
        get => plane.d;
        set => plane.d = value;
    }

    public DBuilder.Geometry.Plane AsPlane()
        => plane;

    public object[] getIntersection(object from, object to)
    {
        Vector3D start = UdbScriptApiConversionModel.GetVector3DFromObject(from);
        Vector3D end = UdbScriptApiConversionModel.GetVector3DFromObject(to);
        double uRay = double.NaN;
        bool intersects = plane.GetIntersection(start, end, ref uRay);

        return new object[] { intersects, uRay };
    }

    public double distance(object p)
        => plane.Distance(UdbScriptApiConversionModel.GetVector3DFromObject(p));

    public UdbScriptVector3DWrapper closestOnPlane(object p)
    {
        Vector3D point = plane.ClosestOnPlane(UdbScriptApiConversionModel.GetVector3DFromObject(p));
        return new UdbScriptVector3DWrapper(point.x, point.y, point.z);
    }

    public double getZ(object p)
        => plane.GetZ(UdbScriptApiConversionModel.GetVector3DFromObject(p));

    public override bool Equals(object? obj)
        => obj is UdbScriptPlaneWrapper other && plane.Equals(other.plane);

    public override int GetHashCode()
        => plane.GetHashCode();

    public static bool operator ==(UdbScriptPlaneWrapper? a, UdbScriptPlaneWrapper? b)
        => ReferenceEquals(a, b) || (a is not null && b is not null && a.plane == b.plane);

    public static bool operator !=(UdbScriptPlaneWrapper? a, UdbScriptPlaneWrapper? b)
        => !(a == b);
}

public sealed class UdbScriptGameConfigurationWrapper
{
    private readonly GameConfiguration configuration;

    public UdbScriptGameConfigurationWrapper(GameConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public string engineName
        => configuration.EngineName;

    public bool hasLocalSidedefTextureOffsets
        => configuration.UseLocalSidedefTextureOffsets;
}

public sealed record UdbScriptImageInfo(
    string name,
    int width,
    int height,
    UdbScriptVector2DWrapper scale,
    bool isFlat);

public sealed class UdbScriptDataWrapper
{
    private readonly ResourceManager resources;

    public UdbScriptDataWrapper(ResourceManager resources)
    {
        this.resources = resources;
    }

    public string[] getTextureNames()
        => resources.GetTextureNames().ToArray();

    public bool textureExists(string name)
        => resources.GetTextureNames().Contains(name, StringComparer.OrdinalIgnoreCase);

    public UdbScriptImageInfo getTextureInfo(string name)
    {
        ImageData image = resources.GetWallTexture(name) ?? ImageData.CreateUnknown();
        return ImageInfo(name, image, isFlat: false);
    }

    public string[] getFlatNames()
        => resources.GetFlatNames().ToArray();

    public bool flatExists(string name)
        => resources.GetFlatNames().Contains(name, StringComparer.OrdinalIgnoreCase);

    public UdbScriptImageInfo getFlatInfo(string name)
    {
        ImageData image = resources.GetFlat(name) ?? ImageData.CreateUnknown();
        return ImageInfo(name, image, isFlat: true);
    }

    private static UdbScriptImageInfo ImageInfo(string name, ImageData image, bool isFlat)
        => new(
            name,
            image.Width,
            image.Height,
            new UdbScriptVector2DWrapper(image.ScaleX, image.ScaleY),
            isFlat);
}

public sealed class UdbScriptFieldsWrapper : IDictionary<string, object?>
{
    private readonly GameConfiguration? config;
    private readonly IFielded element;
    private readonly Thing? managedThing;

    public UdbScriptFieldsWrapper(IFielded element, Thing? managedThing = null, GameConfiguration? config = null)
    {
        this.element = element;
        this.managedThing = managedThing;
        this.config = config;
    }

    public object? this[string key]
    {
        get
        {
            if (element.Fields.TryGetValue(key, out object? value)) return value;
            return GetManagedThingField(key);
        }
        set => SetValue(key, value);
    }

    public ICollection<string> Keys
        => EnumerateFields().Select(item => item.Key).ToArray();

    public ICollection<object?> Values
        => EnumerateFields().Select(item => item.Value).ToArray();

    public int Count
        => Keys.Count;

    public bool IsReadOnly => false;

    public void Add(string key, object? value)
        => SetValue(key, value);

    public bool ContainsKey(string key)
        => element.Fields.ContainsKey(key) || GetManagedThingField(key) != null;

    public bool Remove(string key)
    {
        if (IsManagedThingScaleField(key) && ContainsKey(key))
        {
            SetManagedThingField(key, null);
            return true;
        }

        return element.Fields.Remove(key);
    }

    public bool TryGetValue(string key, out object? value)
    {
        if (element.Fields.TryGetValue(key, out object? fieldValue))
        {
            value = fieldValue;
            return true;
        }

        value = GetManagedThingField(key);
        if (value != null)
            return true;

        value = null;
        return false;
    }

    public void Add(KeyValuePair<string, object?> item)
        => SetValue(item.Key, item.Value);

    public void Clear()
        => element.Fields.Clear();

    public bool Contains(KeyValuePair<string, object?> item)
        => element.Fields.TryGetValue(item.Key, out object? value) && Equals(value, item.Value);

    public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
    {
        foreach (var item in this)
            array[arrayIndex++] = item;
    }

    public bool Remove(KeyValuePair<string, object?> item)
        => Contains(item) && element.Fields.Remove(item.Key);

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        foreach (var item in EnumerateFields())
            yield return item;
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    private void SetValue(string key, object? value)
    {
        ValidateFieldName(key);
        if (IsFlagField(key))
            throw new InvalidOperationException("You are trying to modify a flag through the UDMF fields. Please use the 'flags' property instead.");

        if (value == null)
        {
            if (IsManagedThingScaleField(key))
                SetManagedThingField(key, null);
            else
                element.Fields.Remove(key);
            return;
        }

        if (IsManagedThingScaleField(key))
        {
            SetManagedThingField(key, value);
            return;
        }

        element.Fields[key] = element.Fields.TryGetValue(key, out object? oldValue)
            ? ConvertExistingValue(key, value, oldValue)
            : ConvertNewValue(key, value);
    }

    private IEnumerable<KeyValuePair<string, object?>> EnumerateFields()
    {
        foreach (var item in element.Fields)
            yield return new KeyValuePair<string, object?>(item.Key, item.Value);

        if (managedThing == null)
            yield break;

        if (!element.Fields.ContainsKey("scalex") && managedThing.ScaleX != 1.0)
            yield return new KeyValuePair<string, object?>("scalex", managedThing.ScaleX);

        if (!element.Fields.ContainsKey("scaley") && managedThing.ScaleY != 1.0)
            yield return new KeyValuePair<string, object?>("scaley", managedThing.ScaleY);
    }

    private object? GetManagedThingField(string key)
    {
        if (managedThing == null)
            return null;

        return key switch
        {
            "scalex" when managedThing.ScaleX != 1.0 => managedThing.ScaleX,
            "scaley" when managedThing.ScaleY != 1.0 => managedThing.ScaleY,
            _ => null,
        };
    }

    private bool IsManagedThingScaleField(string key)
        => managedThing != null && (key == "scalex" || key == "scaley");

    private bool IsFlagField(string key)
    {
        bool active = element switch
        {
            Linedef linedef => linedef.UdmfFlags.Contains(key),
            Sidedef sidedef => sidedef.UdmfFlags.Contains(key),
            Sector sector => sector.UdmfFlags.Contains(key),
            Thing thing => thing.UdmfFlags.Contains(key),
            _ => false,
        };

        if (active || config == null)
            return active;

        return element switch
        {
            Linedef => config.LinedefFlags.Values.Contains(key, StringComparer.Ordinal),
            Sidedef => config.SidedefFlags.ContainsKey(key),
            Sector => config.SectorFlags.ContainsKey(key),
            Thing => config.ThingFlagKeys.Contains(key),
            _ => false,
        };
    }

    private void SetManagedThingField(string key, object? value)
    {
        if (managedThing == null)
            return;

        if (value == null)
        {
            element.Fields.Remove(key);
            if (key == "scalex")
                managedThing.SetScale(1.0, managedThing.ScaleY);
            else
                managedThing.SetScale(managedThing.ScaleX, 1.0);
            return;
        }

        object converted = ConvertValue(key, value);
        double scale = converted switch
        {
            int number => number,
            double number => number,
            _ => throw new InvalidOperationException("UDMF field '" + key + "' is of incompatible type for value " + value + "."),
        };

        element.Fields[key] = scale;
        if (key == "scalex")
            managedThing.SetScale(scale, managedThing.ScaleY);
        else
            managedThing.SetScale(managedThing.ScaleX, scale);
    }

    private static void ValidateFieldName(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException("UDMF field names can not be empty.");

        if (key != key.ToLowerInvariant())
            throw new InvalidOperationException("UDMF field names must be lowercase.");
    }

    private static object ConvertValue(string key, object value)
    {
        if (value is string or bool or int or double)
            return value;

        if (value is BigInteger big)
        {
            if (big > int.MaxValue)
                throw new InvalidOperationException("Value " + big + " for UDMF field \"" + key + "\" is too big. Maximum value is " + int.MaxValue + ".");

            if (big < int.MinValue)
                throw new InvalidOperationException("Value " + big + " for UDMF field \"" + key + "\" is too small. Minimum value is " + int.MinValue + ".");

            return (int)big;
        }

        if (value is UdbScriptUniversalValue universal)
            return UdbScriptApiConversionModel.GetConvertedUniversalValue(universal)
                ?? throw new InvalidOperationException("UDMF field '" + key + "' is of incompatible type for value " + value + ".");

        throw new InvalidOperationException("UDMF field '" + key + "' is of incompatible type for value " + value + ".");
    }

    private object ConvertNewValue(string key, object value)
    {
        UniversalFieldInfo? field = KnownUniversalField(key);
        return field == null
            ? ConvertValue(key, value)
            : ConvertKnownFieldValue(key, value, field);
    }

    private static object ConvertExistingValue(string key, object value, object oldValue)
    {
        object proposed = ConvertValue(key, value);
        return proposed switch
        {
            double number when oldValue is int => Convert.ToInt32(number),
            double number when oldValue is double => number,
            int number when oldValue is int => number,
            int number when oldValue is double => Convert.ToDouble(number),
            string text when oldValue is string => text,
            bool flag when oldValue is bool => flag,
            _ => throw new InvalidOperationException("UDMF field '" + key + "' is of incompatible type for value " + value + "."),
        };
    }

    private static object ConvertKnownFieldValue(string key, object value, UniversalFieldInfo field)
    {
        return value switch
        {
            double number when field.DefaultValue is double => number,
            double number when field.DefaultValue is int => Convert.ToInt32(number),
            int number when field.DefaultValue is double => Convert.ToDouble(number),
            int number when field.DefaultValue is int => number,
            string text when field.DefaultValue is string => text,
            bool flag when field.DefaultValue is bool => flag,
            _ => throw new InvalidOperationException("UDMF field '" + key + "' is of incompatible type for value " + value + "."),
        };
    }

    private UniversalFieldInfo? KnownUniversalField(string key)
    {
        if (config == null)
            return null;

        string? elementName = element switch
        {
            Vertex => "vertex",
            Linedef => "linedef",
            Sidedef => "sidedef",
            Sector => "sector",
            Thing => "thing",
            _ => null,
        };

        return elementName != null
            && config.UniversalFields.TryGetValue(elementName, out Dictionary<string, UniversalFieldInfo>? fields)
            && fields.TryGetValue(key, out UniversalFieldInfo? field)
                ? field
                : null;
    }
}

public sealed class UdbScriptFlagsWrapper : IDictionary<string, bool>
{
    private readonly HashSet<string>? flags;
    private readonly Func<int>? getNumericFlags;
    private readonly HashSet<string>? knownFlagNames;
    private readonly Action<int>? setNumericFlags;

    public UdbScriptFlagsWrapper(HashSet<string> flags, IEnumerable<string>? knownFlagNames = null)
    {
        this.flags = flags;
        this.knownFlagNames = knownFlagNames?.ToHashSet(StringComparer.Ordinal);
    }

    public UdbScriptFlagsWrapper(Func<int> getNumericFlags, Action<int> setNumericFlags)
    {
        this.getNumericFlags = getNumericFlags;
        this.setNumericFlags = setNumericFlags;
    }

    public bool this[string key]
    {
        get
        {
            if (flags != null) return flags.Contains(key);
            return TryParseFlagBit(key, out int bit) && (getNumericFlags!() & bit) != 0;
        }
        set
        {
            if (flags != null)
            {
                ValidateKnownFlagName(key);
                if (value) flags.Add(key);
                else flags.Remove(key);
                return;
            }

            int bit = ParseFlagBit(key);
            int numericFlags = getNumericFlags!();
            setNumericFlags!(value ? numericFlags | bit : numericFlags & ~bit);
        }
    }

    public ICollection<string> Keys
        => flags?.ToArray() ?? ActiveNumericFlagKeys();

    public ICollection<bool> Values
        => Keys.Select(_ => true).ToArray();

    public int Count
        => Keys.Count;

    public bool IsReadOnly
        => false;

    public void Add(string key, bool value)
        => this[key] = value;

    public bool ContainsKey(string key)
        => this[key];

    public bool Remove(string key)
    {
        bool contained = ContainsKey(key);
        if (contained) this[key] = false;
        return contained;
    }

    public bool TryGetValue(string key, out bool value)
    {
        value = ContainsKey(key);
        return value;
    }

    public void Add(KeyValuePair<string, bool> item)
        => Add(item.Key, item.Value);

    public void Clear()
    {
        if (flags != null) flags.Clear();
        else setNumericFlags!(0);
    }

    public bool Contains(KeyValuePair<string, bool> item)
        => item.Value && ContainsKey(item.Key);

    public void CopyTo(KeyValuePair<string, bool>[] array, int arrayIndex)
    {
        foreach (string flag in Keys)
            array[arrayIndex++] = new KeyValuePair<string, bool>(flag, true);
    }

    public bool Remove(KeyValuePair<string, bool> item)
        => item.Value && Remove(item.Key);

    public IEnumerator<KeyValuePair<string, bool>> GetEnumerator()
    {
        foreach (string flag in Keys)
            yield return new KeyValuePair<string, bool>(flag, true);
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    private string[] ActiveNumericFlagKeys()
    {
        int numericFlags = getNumericFlags!();
        var keys = new List<string>();
        for (int shift = 0; shift < 31; shift++)
        {
            int bit = 1 << shift;
            if ((numericFlags & bit) != 0) keys.Add(bit.ToString(CultureInfo.InvariantCulture));
        }

        return keys.ToArray();
    }

    private void ValidateKnownFlagName(string key)
    {
        if (knownFlagNames != null && !knownFlagNames.Contains(key))
            throw new InvalidOperationException("Flag name '" + key + "' is not valid.");
    }

    private static int ParseFlagBit(string key)
        => TryParseFlagBit(key, out int bit)
            ? bit
            : throw new InvalidOperationException("Flag name '" + key + "' is not valid.");

    private static bool TryParseFlagBit(string key, out int bit)
        => int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out bit) && bit > 0;
}

public sealed class UdbScriptVertexWrapper : IEquatable<UdbScriptVertexWrapper>
{
    private readonly GameConfiguration? config;
    private readonly Vertex vertex;
    private readonly GridSetup grid;
    private readonly MapSet? owner;

    public UdbScriptVertexWrapper(Vertex vertex, MapSet? owner = null, GridSetup? grid = null, GameConfiguration? config = null)
    {
        this.vertex = vertex;
        this.owner = owner;
        this.grid = grid ?? new GridSetup();
        this.config = config;
    }

    public Vertex Vertex
        => vertex;

    public int index
    {
        get
        {
            ThrowIfDisposed("index");
            return owner?.IndexOfVertex(vertex) ?? -1;
        }
    }

    public UdbScriptFieldsWrapper fields
    {
        get
        {
            ThrowIfDisposed("fields");
            return new UdbScriptFieldsWrapper(vertex, config: config);
        }
    }

    public object position
    {
        get
        {
            ThrowIfDisposed("position");
            return new UdbScriptVector2DWrapper(vertex.Position.x, vertex.Position.y, vertex.Move);
        }
        set
        {
            ThrowIfDisposed("position");
            Vector3D vector = UdbScriptApiConversionModel.GetVector3DFromObject(value);
            vertex.Move(new Vector2D(vector.x, vector.y));
        }
    }

    public bool selected
    {
        get
        {
            ThrowIfDisposed("selected");
            return vertex.Selected;
        }
        set
        {
            ThrowIfDisposed("selected");
            vertex.Selected = value;
        }
    }

    public bool marked
    {
        get
        {
            ThrowIfDisposed("marked");
            return vertex.Marked;
        }
        set
        {
            ThrowIfDisposed("marked");
            vertex.Marked = value;
        }
    }

    public double ceilingZ
    {
        get
        {
            ThrowIfDisposed("ceilingZ");
            return vertex.ZCeiling;
        }
        set
        {
            ThrowIfDisposed("ceilingZ");
            vertex.ZCeiling = value;
        }
    }

    public double floorZ
    {
        get
        {
            ThrowIfDisposed("floorZ");
            return vertex.ZFloor;
        }
        set
        {
            ThrowIfDisposed("floorZ");
            vertex.ZFloor = value;
        }
    }

    public UdbScriptLinedefWrapper[] getLinedefs()
    {
        ThrowIfDisposed("getLinedefs");
        return vertex.Linedefs
            .Where(line => !line.IsDisposed)
            .Select(line => new UdbScriptLinedefWrapper(line, owner, grid))
            .ToArray();
    }

    public double distanceToSq(object pos)
    {
        ThrowIfDisposed("distanceToSq");
        Vector3D point = UdbScriptApiConversionModel.GetVector3DFromObject(pos);
        return vertex.DistanceToSq(new Vector2D(point.x, point.y));
    }

    public double distanceTo(object pos)
    {
        ThrowIfDisposed("distanceTo");
        Vector3D point = UdbScriptApiConversionModel.GetVector3DFromObject(pos);
        return vertex.DistanceTo(new Vector2D(point.x, point.y));
    }

    public UdbScriptLinedefWrapper? nearestLinedef(object pos)
    {
        ThrowIfDisposed("nearestLinedef");
        Vector3D point = UdbScriptApiConversionModel.GetVector3DFromObject(pos);
        Linedef? line = vertex.NearestLinedef(new Vector2D(point.x, point.y));
        return line == null ? null : new UdbScriptLinedefWrapper(line, owner, grid);
    }

    public void snapToAccuracy()
        => snapToAccuracy(3);

    public void snapToAccuracy(int vertexDecimals, bool usePrecisePosition = true)
    {
        ThrowIfDisposed("snapToAccuracy");
        vertex.SnapToAccuracy(vertexDecimals, usePrecisePosition);
    }

    public void snapToGrid()
    {
        ThrowIfDisposed("snapToGrid");
        vertex.Move(grid.SnappedToGrid(vertex.Position));
    }

    public void copyPropertiesTo(UdbScriptVertexWrapper wrapper)
    {
        ThrowIfDisposed("copyPropertiesTo");
        vertex.CopyPropertiesTo(wrapper.vertex);
    }

    public void join(UdbScriptVertexWrapper other)
    {
        ThrowIfDisposed("join");
        if (other.vertex.IsDisposed)
            throw new InvalidOperationException("Vertex to join with is disposed, the join method can not be used.");

        if (owner != null)
            owner.JoinVertices(other.vertex, vertex);
        else
            vertex.IsDisposed = true;
    }

    public void delete()
    {
        if (vertex.IsDisposed)
            return;

        if (owner != null)
            owner.RemoveVertex(vertex);
        else
            vertex.IsDisposed = true;
    }

    public bool Equals(UdbScriptVertexWrapper? other)
        => other is not null && ReferenceEquals(vertex, other.vertex);

    public override bool Equals(object? obj)
        => obj is UdbScriptVertexWrapper other && Equals(other);

    public override int GetHashCode()
        => vertex.GetHashCode();

    public override string ToString()
        => vertex.ToString() ?? string.Empty;

    private void ThrowIfDisposed(string member)
    {
        if (vertex.IsDisposed)
            throw new InvalidOperationException("Vertex is disposed, the " + member + " member can not be accessed.");
    }
}

public sealed class UdbScriptLinedefWrapper : IEquatable<UdbScriptLinedefWrapper>
{
    private readonly GridSetup grid;
    private readonly object? highlightedObject;
    private readonly Linedef linedef;
    private readonly MapFormat mapFormat;
    private readonly MapSet? owner;
    private readonly GameConfiguration? config;
    private readonly UdbScriptMapElementArgumentsWrapper elementArgs;

    public UdbScriptLinedefWrapper(
        Linedef linedef,
        MapSet? owner = null,
        GridSetup? grid = null,
        object? highlightedObject = null,
        MapFormat mapFormat = MapFormat.Udmf,
        GameConfiguration? config = null)
    {
        this.linedef = linedef;
        this.owner = owner;
        this.grid = grid ?? new GridSetup();
        this.highlightedObject = highlightedObject;
        this.mapFormat = mapFormat;
        this.config = config;
        elementArgs = new UdbScriptMapElementArgumentsWrapper(linedef);
    }

    public Linedef Linedef
        => linedef;

    public int index
    {
        get
        {
            ThrowIfDisposed("index");
            return owner?.IndexOfLinedef(linedef) ?? -1;
        }
    }

    public UdbScriptFieldsWrapper fields
    {
        get
        {
            ThrowIfDisposed("fields");
            return new UdbScriptFieldsWrapper(linedef, config: config);
        }
    }

    public UdbScriptVertexWrapper start
    {
        get
        {
            ThrowIfDisposed("start");
            return new UdbScriptVertexWrapper(linedef.Start, owner, grid, config);
        }
    }

    public UdbScriptVertexWrapper end
    {
        get
        {
            ThrowIfDisposed("end");
            return new UdbScriptVertexWrapper(linedef.End, owner, grid, config);
        }
    }

    public UdbScriptLine2DWrapper line
    {
        get
        {
            ThrowIfDisposed("line");
            return new UdbScriptLine2DWrapper(linedef.Line);
        }
    }

    public UdbScriptSidedefWrapper? front
    {
        get
        {
            ThrowIfDisposed("front");
            return linedef.Front == null ? null : new UdbScriptSidedefWrapper(linedef.Front, owner, grid, highlightedObject, mapFormat, config);
        }
    }

    public UdbScriptSidedefWrapper? back
    {
        get
        {
            ThrowIfDisposed("back");
            return linedef.Back == null ? null : new UdbScriptSidedefWrapper(linedef.Back, owner, grid, highlightedObject, mapFormat, config);
        }
    }

    public bool selected
    {
        get
        {
            ThrowIfDisposed("selected");
            return linedef.Selected;
        }
        set
        {
            ThrowIfDisposed("selected");
            linedef.Selected = value;
        }
    }

    public bool marked
    {
        get
        {
            ThrowIfDisposed("marked");
            return linedef.Marked;
        }
        set
        {
            ThrowIfDisposed("marked");
            linedef.Marked = value;
        }
    }

    public int activate
    {
        get
        {
            ThrowIfDisposed("activate");
            return linedef.Activate;
        }
        set
        {
            ThrowIfDisposed("activate");
            linedef.Activate = value;
        }
    }

    public UdbScriptFlagsWrapper flags
    {
        get
        {
            ThrowIfDisposed("flags");
            return mapFormat == MapFormat.Udmf
                ? new UdbScriptFlagsWrapper(linedef.UdmfFlags, LinedefKnownFlagNames(config))
                : new UdbScriptFlagsWrapper(() => linedef.Flags, value => linedef.Flags = value);
        }
    }

    public UdbScriptMapElementArgumentsWrapper args
    {
        get
        {
            ThrowIfDisposed("args");
            return elementArgs;
        }
    }

    public int action
    {
        get
        {
            ThrowIfDisposed("action");
            return linedef.Action;
        }
        set
        {
            ThrowIfDisposed("action");
            linedef.Action = value;
        }
    }

    public int tag
    {
        get
        {
            ThrowIfDisposed("tag");
            return linedef.Tag;
        }
        set
        {
            ThrowIfDisposed("tag");
            linedef.Tag = value;
        }
    }

    public double lengthSq
    {
        get
        {
            ThrowIfDisposed("lengthSq");
            return linedef.LengthSq;
        }
    }

    public double length
    {
        get
        {
            ThrowIfDisposed("length");
            return linedef.Length;
        }
    }

    public double lengthInv
    {
        get
        {
            ThrowIfDisposed("lengthInv");
            return linedef.LengthInv;
        }
    }

    public int angle
    {
        get
        {
            ThrowIfDisposed("angle");
            return linedef.AngleDeg;
        }
    }

    public double angleRad
    {
        get
        {
            ThrowIfDisposed("angleRad");
            return linedef.Angle;
        }
    }

    public void copyPropertiesTo(UdbScriptLinedefWrapper wrapper)
    {
        ThrowIfDisposed("copyPropertiesTo");
        linedef.CopyPropertiesTo(wrapper.linedef);
    }

    public void clearFlags()
    {
        ThrowIfDisposed("clearFlags");
        linedef.UdmfFlags.Clear();
        linedef.Flags = 0;
    }

    public void flipVertices()
    {
        ThrowIfDisposed("flipVertices");
        linedef.FlipVertices();
    }

    public void flipSidedefs()
    {
        ThrowIfDisposed("flipSidedefs");
        linedef.FlipSidedefs();
    }

    public void flip()
    {
        ThrowIfDisposed("flip");
        linedef.FlipVertices();
        linedef.FlipSidedefs();
    }

    public UdbScriptVector2DWrapper getSidePoint(bool front)
    {
        ThrowIfDisposed("getSidePoint");
        Vector2D point = linedef.GetSidePoint(front);
        return new UdbScriptVector2DWrapper(point.x, point.y);
    }

    public UdbScriptVector2DWrapper getCenterPoint()
    {
        ThrowIfDisposed("getCenterPoint");
        Vector2D point = linedef.GetCenterPoint();
        return new UdbScriptVector2DWrapper(point.x, point.y);
    }

    public UdbScriptVector2DWrapper getCoordinatesAt(double u)
    {
        ThrowIfDisposed("getCoordinatesAt");
        Vector2D point = linedef.Line.GetCoordinatesAt(u);
        return new UdbScriptVector2DWrapper(point.x, point.y);
    }

    public void applySidedFlags()
    {
        ThrowIfDisposed("applySidedFlags");
        linedef.ApplySidedFlags();
    }

    public UdbScriptVector2DWrapper nearestOnLine(object pos)
    {
        ThrowIfDisposed("nearestOnLine");
        Vector3D point = UdbScriptApiConversionModel.GetVector3DFromObject(pos);
        Vector2D nearest = linedef.NearestOnLine(new Vector2D(point.x, point.y));
        return new UdbScriptVector2DWrapper(nearest.x, nearest.y);
    }

    public double safeDistanceToSq(object pos, bool bounded)
    {
        ThrowIfDisposed("safeDistanceToSq");
        Vector3D point = UdbScriptApiConversionModel.GetVector3DFromObject(pos);
        return linedef.SafeDistanceToSq(new Vector2D(point.x, point.y), bounded);
    }

    public double safeDistanceTo(object pos, bool bounded)
    {
        ThrowIfDisposed("safeDistanceTo");
        Vector3D point = UdbScriptApiConversionModel.GetVector3DFromObject(pos);
        return linedef.SafeDistanceTo(new Vector2D(point.x, point.y), bounded);
    }

    public double distanceToSq(object pos, bool bounded)
    {
        ThrowIfDisposed("distanceToSq");
        Vector3D point = UdbScriptApiConversionModel.GetVector3DFromObject(pos);
        return linedef.DistanceToSq(new Vector2D(point.x, point.y), bounded);
    }

    public double distanceTo(object pos, bool bounded)
    {
        ThrowIfDisposed("distanceTo");
        Vector3D point = UdbScriptApiConversionModel.GetVector3DFromObject(pos);
        return linedef.DistanceTo(new Vector2D(point.x, point.y), bounded);
    }

    public double sideOfLine(object pos)
    {
        ThrowIfDisposed("sideOfLine");
        Vector3D point = UdbScriptApiConversionModel.GetVector3DFromObject(pos);
        return linedef.SideOfLine(new Vector2D(point.x, point.y));
    }

    public UdbScriptLinedefWrapper split(object pos)
    {
        ThrowIfDisposed("split");
        Vertex vertex = pos is UdbScriptVertexWrapper wrapper
            ? wrapper.Vertex
            : CreateSplitVertex(pos);

        Linedef newLine;
        if (owner != null)
        {
            newLine = owner.SplitLinedefAt(linedef, vertex);
            owner.BuildIndexes();
        }
        else
        {
            newLine = SplitStandalone(linedef, vertex);
        }

        return new UdbScriptLinedefWrapper(newLine, owner, grid, highlightedObject, mapFormat, config);
    }

    public int[] getTags()
    {
        ThrowIfDisposed("getTags");
        return linedef.Tags.ToArray();
    }

    public bool addTag(int tag)
    {
        ThrowIfDisposed("addTag");
        if (linedef.Tags.Contains(tag))
            return false;

        linedef.Tags.Add(tag);
        linedef.Tags.Remove(0);
        return true;
    }

    public bool removeTag(int tag)
    {
        ThrowIfDisposed("removeTag");
        if (!linedef.Tags.Contains(tag))
            return false;

        if (linedef.Tags.Count == 1)
            linedef.Tag = 0;
        else
            linedef.Tags.Remove(tag);

        return true;
    }

    public void delete()
    {
        if (linedef.IsDisposed)
            return;

        if (owner != null)
            owner.RemoveLinedef(linedef);
        else
            linedef.IsDisposed = true;
    }

    public bool Equals(UdbScriptLinedefWrapper? other)
        => other is not null && ReferenceEquals(linedef, other.linedef);

    public override bool Equals(object? obj)
        => obj is UdbScriptLinedefWrapper other && Equals(other);

    public override int GetHashCode()
        => linedef.GetHashCode();

    public override string ToString()
        => linedef.ToString() ?? string.Empty;

    private void ThrowIfDisposed(string member)
    {
        if (linedef.IsDisposed)
            throw new InvalidOperationException("Linedef is disposed, the " + member + " member can not be accessed.");
    }

    private static IEnumerable<string>? LinedefKnownFlagNames(GameConfiguration? config)
    {
        if (config == null)
            return null;

        return config.LinedefFlags.Values.Concat(config.LinedefActivations.Select(activation => activation.Key));
    }

    private Vertex CreateSplitVertex(object pos)
    {
        Vector3D point = UdbScriptApiConversionModel.GetVector3DFromObject(pos);
        Vertex vertex = owner?.AddVertex(new Vector2D(point.x, point.y)) ?? new Vertex(new Vector2D(point.x, point.y));
        vertex.SnapToAccuracy(3);
        return vertex;
    }

    private static Linedef SplitStandalone(Linedef line, Vertex vertex)
    {
        double firstHalfLen = (vertex.Position - line.Start.Position).GetLength();
        Vertex oldEnd = line.End;
        var newLine = new Linedef(vertex, oldEnd);
        line.CopyPropertiesTo(newLine);
        line.SetEndVertex(vertex);

        if (line.Front != null)
        {
            var front = new Sidedef(newLine, isFront: true) { Sector = line.Front.Sector };
            line.Front.CopyPropertiesTo(front);
            front.OffsetX += (int)Math.Round(firstHalfLen);
            newLine.AttachFront(front);
        }

        if (line.Back != null)
        {
            var back = new Sidedef(newLine, isFront: false) { Sector = line.Back.Sector };
            line.Back.CopyPropertiesTo(back);
            newLine.AttachBack(back);
        }

        return newLine;
    }
}

public sealed class UdbScriptSidedefWrapper : IEquatable<UdbScriptSidedefWrapper>
{
    private readonly GridSetup grid;
    private readonly object? highlightedObject;
    private readonly MapFormat mapFormat;
    private readonly Sidedef sidedef;
    private readonly MapSet? owner;
    private readonly GameConfiguration? config;

    public UdbScriptSidedefWrapper(
        Sidedef sidedef,
        MapSet? owner = null,
        GridSetup? grid = null,
        object? highlightedObject = null,
        MapFormat mapFormat = MapFormat.Udmf,
        GameConfiguration? config = null)
    {
        this.sidedef = sidedef;
        this.owner = owner;
        this.grid = grid ?? new GridSetup();
        this.highlightedObject = highlightedObject;
        this.mapFormat = mapFormat;
        this.config = config;
    }

    public Sidedef Sidedef
        => sidedef;

    public int index
    {
        get
        {
            ThrowIfDisposed("index");
            return owner?.IndexOfSidedef(sidedef) ?? -1;
        }
    }

    public UdbScriptFieldsWrapper fields
    {
        get
        {
            ThrowIfDisposed("fields");
            return new UdbScriptFieldsWrapper(sidedef, config: config);
        }
    }

    public bool isFront
    {
        get
        {
            ThrowIfDisposed("isFront");
            return sidedef.IsFront;
        }
    }

    public UdbScriptLinedefWrapper line
    {
        get
        {
            ThrowIfDisposed("line");
            return new UdbScriptLinedefWrapper(sidedef.Line, owner, grid, highlightedObject, mapFormat, config);
        }
    }

    public UdbScriptSectorWrapper? sector
    {
        get
        {
            ThrowIfDisposed("sector");
            return sidedef.Sector == null ? null : new UdbScriptSectorWrapper(sidedef.Sector, owner, grid, highlightedObject, mapFormat, config);
        }
    }

    public UdbScriptSidedefWrapper? other
    {
        get
        {
            ThrowIfDisposed("other");
            return sidedef.Other == null ? null : new UdbScriptSidedefWrapper(sidedef.Other, owner, grid, highlightedObject, mapFormat, config);
        }
    }

    public double angle
    {
        get
        {
            ThrowIfDisposed("angle");
            return Angle2D.RadToDeg(sidedef.Angle);
        }
    }

    public double angleRad
    {
        get
        {
            ThrowIfDisposed("angleRad");
            return sidedef.Angle;
        }
    }

    public int offsetX
    {
        get
        {
            ThrowIfDisposed("offsetX");
            return sidedef.OffsetX;
        }
        set
        {
            ThrowIfDisposed("offsetX");
            sidedef.OffsetX = value;
        }
    }

    public int offsetY
    {
        get
        {
            ThrowIfDisposed("offsetY");
            return sidedef.OffsetY;
        }
        set
        {
            ThrowIfDisposed("offsetY");
            sidedef.OffsetY = value;
        }
    }

    public UdbScriptFlagsWrapper flags
    {
        get
        {
            ThrowIfDisposed("flags");
            return new UdbScriptFlagsWrapper(sidedef.UdmfFlags, config?.SidedefFlags.Keys);
        }
    }

    public string upperTexture
    {
        get
        {
            ThrowIfDisposed("upperTexture");
            return sidedef.HighTexture;
        }
        set
        {
            ThrowIfDisposed("upperTexture");
            sidedef.SetTextureHigh(value);
        }
    }

    public string middleTexture
    {
        get
        {
            ThrowIfDisposed("middleTexture");
            return sidedef.MidTexture;
        }
        set
        {
            ThrowIfDisposed("middleTexture");
            sidedef.SetTextureMid(value);
        }
    }

    public string lowerTexture
    {
        get
        {
            ThrowIfDisposed("lowerTexture");
            return sidedef.LowTexture;
        }
        set
        {
            ThrowIfDisposed("lowerTexture");
            sidedef.SetTextureLow(value);
        }
    }

    public bool upperSelected
    {
        get
        {
            ThrowIfDisposed("upperSelected");
            return sidedef.Line.Selected;
        }
    }

    public bool upperHighlighted
    {
        get
        {
            return IsHighlightedPart("upperHighlighted", SidedefPart.Upper);
        }
    }

    public bool middleSelected
    {
        get
        {
            ThrowIfDisposed("middleSelected");
            return sidedef.Line.Selected;
        }
    }

    public bool middleHighlighted
    {
        get
        {
            return IsHighlightedPart("middleHighlighted", SidedefPart.Middle);
        }
    }

    public bool lowerSelected
    {
        get
        {
            ThrowIfDisposed("lowerSelected");
            return sidedef.Line.Selected;
        }
    }

    public bool lowerHighlighted
    {
        get
        {
            return IsHighlightedPart("lowerHighlighted", SidedefPart.Lower);
        }
    }

    public void copyPropertiesTo(UdbScriptSidedefWrapper wrapper)
    {
        ThrowIfDisposed("copyPropertiesTo");
        sidedef.CopyPropertiesTo(wrapper.sidedef);
    }

    public UdbScriptVector2DWrapper getCenterPoint()
    {
        ThrowIfDisposed("getCenterPoint");
        Vector2D point = sidedef.Line.GetCenterPoint();
        return new UdbScriptVector2DWrapper(point.x, point.y);
    }

    public bool Equals(UdbScriptSidedefWrapper? other)
        => other is not null && ReferenceEquals(sidedef, other.sidedef);

    public override bool Equals(object? obj)
        => obj is UdbScriptSidedefWrapper other && Equals(other);

    public override int GetHashCode()
        => sidedef.GetHashCode();

    public override string ToString()
        => sidedef.ToString() ?? string.Empty;

    private void ThrowIfDisposed(string member)
    {
        if (sidedef.IsDisposed)
            throw new InvalidOperationException("Sidedef is disposed, the " + member + " member can not be accessed.");
    }

    private bool IsHighlightedPart(string member, SidedefPart part)
    {
        ThrowIfDisposed(member);
        return highlightedObject switch
        {
            UdbScriptHighlightedSidedefPart highlighted
                => ReferenceEquals(highlighted.Sidedef, sidedef) && highlighted.Part == part,
            Sidedef highlighted
                => ReferenceEquals(highlighted, sidedef),
            _ => false,
        };
    }
}

public sealed class UdbScriptSectorWrapper : IEquatable<UdbScriptSectorWrapper>
{
    private readonly GridSetup grid;
    private readonly object? highlightedObject;
    private readonly MapFormat mapFormat;
    private readonly Sector sector;
    private readonly MapSet? owner;
    private readonly GameConfiguration? config;

    public UdbScriptSectorWrapper(
        Sector sector,
        MapSet? owner = null,
        GridSetup? grid = null,
        object? highlightedObject = null,
        MapFormat mapFormat = MapFormat.Udmf,
        GameConfiguration? config = null)
    {
        this.sector = sector;
        this.owner = owner;
        this.grid = grid ?? new GridSetup();
        this.highlightedObject = highlightedObject;
        this.mapFormat = mapFormat;
        this.config = config;
    }

    public Sector Sector
        => sector;

    public UdbScriptFieldsWrapper fields
    {
        get
        {
            ThrowIfDisposed("fields");
            return new UdbScriptFieldsWrapper(sector, config: config);
        }
    }

    public int index
    {
        get
        {
            ThrowIfDisposed("index");
            return sector.Index;
        }
    }

    public int floorHeight
    {
        get
        {
            ThrowIfDisposed("floorHeight");
            return sector.FloorHeight;
        }
        set
        {
            ThrowIfDisposed("floorHeight");
            sector.FloorHeight = value;
        }
    }

    public int ceilingHeight
    {
        get
        {
            ThrowIfDisposed("ceilingHeight");
            return sector.CeilHeight;
        }
        set
        {
            ThrowIfDisposed("ceilingHeight");
            sector.CeilHeight = value;
        }
    }

    public string floorTexture
    {
        get
        {
            ThrowIfDisposed("floorTexture");
            return sector.FloorTexture;
        }
        set
        {
            ThrowIfDisposed("floorTexture");
            sector.SetFloorTexture(value);
        }
    }

    public string ceilingTexture
    {
        get
        {
            ThrowIfDisposed("ceilingTexture");
            return sector.CeilTexture;
        }
        set
        {
            ThrowIfDisposed("ceilingTexture");
            sector.SetCeilTexture(value);
        }
    }

    public bool selected
    {
        get
        {
            ThrowIfDisposed("selected");
            return sector.Selected;
        }
        set
        {
            ThrowIfDisposed("selected");
            sector.Selected = value;
            foreach (Sidedef side in sector.Sidedefs)
            {
                bool frontSelected = side.Line.Front?.Sector?.Selected ?? false;
                bool backSelected = side.Line.Back?.Sector?.Selected ?? false;
                side.Line.Selected = frontSelected || backSelected;
            }
        }
    }

    public bool floorSelected
    {
        get
        {
            ThrowIfDisposed("floorSelected");
            return sector.Selected;
        }
    }

    public bool floorHighlighted
    {
        get
        {
            return IsHighlightedSurface("floorHighlighted", floor: true);
        }
    }

    public bool ceilingSelected
    {
        get
        {
            ThrowIfDisposed("ceilingSelected");
            return sector.Selected;
        }
    }

    public bool ceilingHighlighted
    {
        get
        {
            return IsHighlightedSurface("ceilingHighlighted", floor: false);
        }
    }

    public bool marked
    {
        get
        {
            ThrowIfDisposed("marked");
            return sector.Marked;
        }
        set
        {
            ThrowIfDisposed("marked");
            sector.Marked = value;
        }
    }

    public UdbScriptFlagsWrapper flags
    {
        get
        {
            ThrowIfDisposed("flags");
            return new UdbScriptFlagsWrapper(sector.UdmfFlags, config?.SectorFlags.Keys);
        }
    }

    public int special
    {
        get
        {
            ThrowIfDisposed("special");
            return sector.Special;
        }
        set
        {
            ThrowIfDisposed("special");
            sector.Special = value;
        }
    }

    public int tag
    {
        get
        {
            ThrowIfDisposed("tag");
            return sector.Tag;
        }
        set
        {
            ThrowIfDisposed("tag");
            sector.Tag = value;
        }
    }

    public int brightness
    {
        get
        {
            ThrowIfDisposed("brightness");
            return sector.Brightness;
        }
        set
        {
            ThrowIfDisposed("brightness");
            sector.Brightness = value;
        }
    }

    public double floorSlopeOffset
    {
        get
        {
            ThrowIfDisposed("floorSlopeOffset");
            return sector.FloorSlopeOffset;
        }
        set
        {
            ThrowIfDisposed("floorSlopeOffset");
            sector.FloorSlopeOffset = value;
        }
    }

    public double ceilingSlopeOffset
    {
        get
        {
            ThrowIfDisposed("ceilingSlopeOffset");
            return sector.CeilSlopeOffset;
        }
        set
        {
            ThrowIfDisposed("ceilingSlopeOffset");
            sector.CeilSlopeOffset = value;
        }
    }

    public UdbScriptSidedefWrapper[] getSidedefs()
    {
        ThrowIfDisposed("getSidedefs");
        return sector.Sidedefs
            .Where(sidedef => !sidedef.IsDisposed)
            .Select(sidedef => new UdbScriptSidedefWrapper(sidedef, owner, grid, highlightedObject, mapFormat, config))
            .ToArray();
    }

    public UdbScriptVector2DWrapper[] getLabelPositions()
    {
        ThrowIfDisposed("getLabelPositions");
        return Tools.FindLabelPositions(sector)
            .Select(label => new UdbScriptVector2DWrapper(label.position.x, label.position.y))
            .ToArray();
    }

    public UdbScriptVector2DWrapper[][] getTriangles()
    {
        ThrowIfDisposed("getTriangles");
        Triangulation triangles = Triangulation.Create(sector);
        if (triangles.Vertices.Count % 3 != 0)
            throw new InvalidOperationException("Sector triangle vertices is not a multiple of 3.");

        var result = new UdbScriptVector2DWrapper[triangles.Vertices.Count / 3][];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new[]
            {
                new UdbScriptVector2DWrapper(triangles.Vertices[i * 3].x, triangles.Vertices[i * 3].y),
                new UdbScriptVector2DWrapper(triangles.Vertices[i * 3 + 1].x, triangles.Vertices[i * 3 + 1].y),
                new UdbScriptVector2DWrapper(triangles.Vertices[i * 3 + 2].x, triangles.Vertices[i * 3 + 2].y),
            };
        }

        return result;
    }

    public void clearFlags()
    {
        ThrowIfDisposed("clearFlags");
        sector.UdmfFlags.Clear();
    }

    public void copyPropertiesTo(UdbScriptSectorWrapper wrapper)
    {
        ThrowIfDisposed("copyPropertiesTo");
        sector.CopyPropertiesTo(wrapper.sector);
    }

    public bool intersect(object point)
    {
        ThrowIfDisposed("intersect");
        Vector3D vector = UdbScriptApiConversionModel.GetVector3DFromObject(point);
        return sector.Intersect(new Vector2D(vector.x, vector.y));
    }

    public UdbScriptVector3DWrapper getFloorSlope()
    {
        ThrowIfDisposed("getFloorSlope");
        Vector3D normal = sector.FloorSlope.GetNormal();
        return new UdbScriptVector3DWrapper(normal.x, normal.y, normal.z);
    }

    public void setFloorSlope(object normal)
    {
        ThrowIfDisposed("setFloorSlope");
        sector.FloorSlope = UdbScriptApiConversionModel.GetVector3DFromObject(normal);
    }

    public UdbScriptVector3DWrapper getCeilingSlope()
    {
        ThrowIfDisposed("getCeilingSlope");
        Vector3D normal = sector.CeilSlope.GetNormal();
        return new UdbScriptVector3DWrapper(normal.x, normal.y, normal.z);
    }

    public void setCeilingSlope(object normal)
    {
        ThrowIfDisposed("setCeilingSlope");
        sector.CeilSlope = UdbScriptApiConversionModel.GetVector3DFromObject(normal);
    }

    public int[] getTags()
    {
        ThrowIfDisposed("getTags");
        return sector.Tags.ToArray();
    }

    public bool addTag(int tag)
    {
        ThrowIfDisposed("addTag");
        if (sector.Tags.Contains(tag))
            return false;

        sector.Tags.Add(tag);
        sector.Tags.Remove(0);
        return true;
    }

    public bool removeTag(int tag)
    {
        ThrowIfDisposed("removeTag");
        if (!sector.Tags.Contains(tag))
            return false;

        if (sector.Tags.Count == 1)
            sector.Tag = 0;
        else
            sector.Tags.Remove(tag);

        return true;
    }

    public void join(UdbScriptSectorWrapper other)
    {
        ThrowIfDisposed("join");
        if (other.sector.IsDisposed)
            throw new InvalidOperationException("Sector to join with is disposed, the join method can not be used.");

        if (owner != null)
        {
            owner.JoinSectors(new[] { other.sector, sector });
            owner.BuildIndexes();
        }
        else
        {
            sector.IsDisposed = true;
        }
    }

    public void delete()
    {
        if (sector.IsDisposed)
            return;

        if (owner != null)
            owner.RemoveSector(sector);
        else
            sector.IsDisposed = true;
    }

    public bool Equals(UdbScriptSectorWrapper? other)
        => other is not null && ReferenceEquals(sector, other.sector);

    public override bool Equals(object? obj)
        => obj is UdbScriptSectorWrapper other && Equals(other);

    public override int GetHashCode()
        => sector.GetHashCode();

    public override string ToString()
        => sector.ToString() ?? string.Empty;

    private void ThrowIfDisposed(string member)
    {
        if (sector.IsDisposed)
            throw new InvalidOperationException("Sector is disposed, the " + member + " member can not be accessed.");
    }

    private bool IsHighlightedSurface(string member, bool floor)
    {
        ThrowIfDisposed(member);
        return highlightedObject switch
        {
            UdbScriptHighlightedSectorSurface highlighted
                => ReferenceEquals(highlighted.Sector, sector)
                    && (floor ? highlighted.FloorHighlighted : highlighted.CeilingHighlighted),
            Sector highlighted
                => ReferenceEquals(highlighted, sector),
            _ => false,
        };
    }
}

public sealed class UdbScriptThingWrapper : IEquatable<UdbScriptThingWrapper>
{
    private readonly GridSetup grid;
    private readonly object? highlightedObject;
    private readonly MapFormat mapFormat;
    private readonly Thing thing;
    private readonly MapSet? owner;
    private readonly GameConfiguration? config;
    private readonly UdbScriptMapElementArgumentsWrapper elementArgs;

    public UdbScriptThingWrapper(
        Thing thing,
        MapSet? owner = null,
        GridSetup? grid = null,
        object? highlightedObject = null,
        MapFormat mapFormat = MapFormat.Udmf,
        GameConfiguration? config = null)
    {
        this.thing = thing;
        this.owner = owner;
        this.grid = grid ?? new GridSetup();
        this.highlightedObject = highlightedObject;
        this.mapFormat = mapFormat;
        this.config = config;
        elementArgs = new UdbScriptMapElementArgumentsWrapper(thing);
    }

    public Thing Thing
        => thing;

    public int index
    {
        get
        {
            ThrowIfDisposed("index");
            return owner?.IndexOfThing(thing) ?? -1;
        }
    }

    public UdbScriptFieldsWrapper fields
    {
        get
        {
            ThrowIfDisposed("fields");
            return new UdbScriptFieldsWrapper(thing, thing, config);
        }
    }

    public int type
    {
        get
        {
            ThrowIfDisposed("type");
            return thing.Type;
        }
        set
        {
            ThrowIfDisposed("type");
            thing.Type = value;
        }
    }

    public int angle
    {
        get
        {
            ThrowIfDisposed("angle");
            return thing.Angle;
        }
        set
        {
            ThrowIfDisposed("angle");
            thing.Rotate(value);
        }
    }

    public double angleRad
    {
        get
        {
            ThrowIfDisposed("angleRad");
            return Angle2D.DoomToReal(thing.Angle);
        }
        set
        {
            ThrowIfDisposed("angleRad");
            thing.Rotate(value);
        }
    }

    public UdbScriptMapElementArgumentsWrapper args
    {
        get
        {
            ThrowIfDisposed("args");
            return elementArgs;
        }
    }

    public int action
    {
        get
        {
            ThrowIfDisposed("action");
            return thing.Action;
        }
        set
        {
            ThrowIfDisposed("action");
            thing.Action = value;
        }
    }

    public int tag
    {
        get
        {
            ThrowIfDisposed("tag");
            return thing.Tag;
        }
        set
        {
            ThrowIfDisposed("tag");
            thing.Tag = value;
        }
    }

    public bool selected
    {
        get
        {
            ThrowIfDisposed("selected");
            return thing.Selected;
        }
        set
        {
            ThrowIfDisposed("selected");
            thing.Selected = value;
        }
    }

    public bool marked
    {
        get
        {
            ThrowIfDisposed("marked");
            return thing.Marked;
        }
        set
        {
            ThrowIfDisposed("marked");
            thing.Marked = value;
        }
    }

    public UdbScriptFlagsWrapper flags
    {
        get
        {
            ThrowIfDisposed("flags");
            return mapFormat == MapFormat.Udmf
                ? new UdbScriptFlagsWrapper(thing.UdmfFlags, config?.ThingFlagKeys)
                : new UdbScriptFlagsWrapper(() => thing.Flags, value => thing.Flags = value);
        }
    }

    public object position
    {
        get
        {
            ThrowIfDisposed("position");
            return new UdbScriptVector3DWrapper(thing.Position.x, thing.Position.y, thing.Height, thing.Move);
        }
        set
        {
            ThrowIfDisposed("position");
            thing.Move(UdbScriptApiConversionModel.GetVector3DFromObject(value));
        }
    }

    public int pitch
    {
        get
        {
            ThrowIfDisposed("pitch");
            return thing.Pitch;
        }
        set
        {
            ThrowIfDisposed("pitch");
            thing.SetPitch(value);
        }
    }

    public int roll
    {
        get
        {
            ThrowIfDisposed("roll");
            return thing.Roll;
        }
        set
        {
            ThrowIfDisposed("roll");
            thing.SetRoll(value);
        }
    }

    public double scaleX
    {
        get
        {
            ThrowIfDisposed("scaleX");
            return thing.ScaleX;
        }
        set
        {
            ThrowIfDisposed("scaleX");
            thing.SetScale(value, thing.ScaleY);
        }
    }

    public double scaleY
    {
        get
        {
            ThrowIfDisposed("scaleY");
            return thing.ScaleY;
        }
        set
        {
            ThrowIfDisposed("scaleY");
            thing.SetScale(thing.ScaleX, value);
        }
    }

    public void copyPropertiesTo(UdbScriptThingWrapper wrapper)
    {
        ThrowIfDisposed("copyPropertiesTo");
        thing.CopyPropertiesTo(wrapper.thing);
    }

    public void clearFlags()
    {
        ThrowIfDisposed("clearFlags");
        thing.UdmfFlags.Clear();
        thing.Flags = 0;
    }

    public double distanceToSq(object pos)
    {
        ThrowIfDisposed("distanceToSq");
        Vector3D point = UdbScriptApiConversionModel.GetVector3DFromObject(pos);
        return thing.DistanceToSq(new Vector2D(point.x, point.y));
    }

    public double distanceTo(object pos)
    {
        ThrowIfDisposed("distanceTo");
        Vector3D point = UdbScriptApiConversionModel.GetVector3DFromObject(pos);
        return thing.DistanceTo(new Vector2D(point.x, point.y));
    }

    public void snapToAccuracy()
        => snapToAccuracy(3);

    public void snapToAccuracy(int vertexDecimals, bool usePrecisePosition = true)
    {
        ThrowIfDisposed("snapToAccuracy");
        thing.SnapToAccuracy(vertexDecimals, usePrecisePosition);
    }

    public void snapToGrid()
    {
        ThrowIfDisposed("snapToGrid");
        thing.Move(grid.SnappedToGrid(thing.Position));
    }

    public void delete()
    {
        if (thing.IsDisposed)
            return;

        if (owner != null)
            owner.RemoveThing(thing);
        else
            thing.IsDisposed = true;
    }

    public UdbScriptSectorWrapper? getSector()
    {
        ThrowIfDisposed("getSector");
        if (owner != null)
            thing.DetermineSector(owner);

        return thing.Sector == null ? null : new UdbScriptSectorWrapper(thing.Sector, owner, grid, highlightedObject, mapFormat, config);
    }

    public bool Equals(UdbScriptThingWrapper? other)
        => other is not null && ReferenceEquals(thing, other.thing);

    public override bool Equals(object? obj)
        => obj is UdbScriptThingWrapper other && Equals(other);

    public override int GetHashCode()
        => thing.GetHashCode();

    public override string ToString()
        => "Thing " + index;

    private void ThrowIfDisposed(string member)
    {
        if (thing.IsDisposed)
            throw new InvalidOperationException("Thing is disposed, the " + member + " member can not be accessed.");
    }
}

public abstract class UdbScriptBlockMapContentBase
{
    private readonly MapFormat mapFormat;
    private readonly MapSet? owner;
    private UdbScriptLinedefWrapper[]? wrappedLines;
    private UdbScriptThingWrapper[]? wrappedThings;
    private UdbScriptSectorWrapper[]? wrappedSectors;
    private UdbScriptVertexWrapper[]? wrappedVertices;

    protected UdbScriptBlockMapContentBase(MapSet? owner = null, MapFormat mapFormat = MapFormat.Doom)
    {
        this.owner = owner;
        this.mapFormat = mapFormat;
    }

    public abstract UdbScriptLinedefWrapper[] getLinedefs();
    public abstract UdbScriptThingWrapper[] getThings();
    public abstract UdbScriptSectorWrapper[] getSectors();
    public abstract UdbScriptVertexWrapper[] getVertices();

    protected UdbScriptLinedefWrapper[] GetWrappedLinedefs(IEnumerable<Linedef> lines)
        => wrappedLines ??= lines
            .Where(line => !line.IsDisposed)
            .Select(line => new UdbScriptLinedefWrapper(line, owner, mapFormat: mapFormat))
            .ToArray();

    protected UdbScriptThingWrapper[] GetWrappedThings(IEnumerable<Thing> things)
        => wrappedThings ??= things
            .Where(thing => !thing.IsDisposed)
            .Select(thing => new UdbScriptThingWrapper(thing, owner, mapFormat: mapFormat))
            .ToArray();

    protected UdbScriptSectorWrapper[] GetWrappedSectors(IEnumerable<Sector> sectors)
        => wrappedSectors ??= sectors
            .Where(sector => !sector.IsDisposed)
            .Select(sector => new UdbScriptSectorWrapper(sector, owner, mapFormat: mapFormat))
            .ToArray();

    protected UdbScriptVertexWrapper[] GetWrappedVertices(IEnumerable<Vertex> vertices)
        => wrappedVertices ??= vertices
            .Where(vertex => !vertex.IsDisposed)
            .Select(vertex => new UdbScriptVertexWrapper(vertex, owner))
            .ToArray();
}

public sealed class UdbScriptBlockEntryWrapper : UdbScriptBlockMapContentBase
{
    private readonly BlockMapCell entry;

    public UdbScriptBlockEntryWrapper(BlockMapCell entry, MapSet? owner = null)
        : this(entry, owner, MapFormat.Doom)
    {
    }

    public UdbScriptBlockEntryWrapper(BlockMapCell entry, MapSet? owner, MapFormat mapFormat)
        : base(owner, mapFormat)
    {
        this.entry = entry;
    }

    public BlockMapCell Entry => entry;

    public override UdbScriptLinedefWrapper[] getLinedefs()
        => GetWrappedLinedefs(entry.Lines);

    public override UdbScriptThingWrapper[] getThings()
        => GetWrappedThings(entry.Things);

    public override UdbScriptSectorWrapper[] getSectors()
        => GetWrappedSectors(entry.Sectors);

    public override UdbScriptVertexWrapper[] getVertices()
        => GetWrappedVertices(entry.Vertices);
}

public sealed class UdbScriptBlockMapQueryResult : UdbScriptBlockMapContentBase, IEnumerable<UdbScriptBlockEntryWrapper>
{
    private readonly IReadOnlyList<BlockMapCell> entries;
    private readonly MapFormat mapFormat;
    private readonly MapSet? owner;
    private UdbScriptBlockEntryWrapper[]? wrappedEntries;

    public UdbScriptBlockMapQueryResult(IEnumerable<BlockMapCell> entries, MapSet? owner = null)
        : this(entries, owner, MapFormat.Doom)
    {
    }

    public UdbScriptBlockMapQueryResult(IEnumerable<BlockMapCell> entries, MapSet? owner, MapFormat mapFormat)
        : base(owner, mapFormat)
    {
        this.entries = entries.ToArray();
        this.mapFormat = mapFormat;
        this.owner = owner;
    }

    public override UdbScriptLinedefWrapper[] getLinedefs()
        => GetWrappedLinedefs(entries.SelectMany(entry => entry.Lines).Distinct());

    public override UdbScriptThingWrapper[] getThings()
        => GetWrappedThings(entries.SelectMany(entry => entry.Things).Distinct());

    public override UdbScriptSectorWrapper[] getSectors()
        => GetWrappedSectors(entries.SelectMany(entry => entry.Sectors).Distinct());

    public override UdbScriptVertexWrapper[] getVertices()
        => GetWrappedVertices(entries.SelectMany(entry => entry.Vertices).Distinct());

    public IEnumerator<UdbScriptBlockEntryWrapper> GetEnumerator()
    {
        wrappedEntries ??= entries.Select(entry => new UdbScriptBlockEntryWrapper(entry, owner, mapFormat)).ToArray();
        return ((IEnumerable<UdbScriptBlockEntryWrapper>)wrappedEntries).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}

public sealed class UdbScriptBlockMapWrapper
{
    private readonly BlockMap blockMap;
    private readonly Dictionary<BlockMapCell, UdbScriptBlockEntryWrapper> blockEntries = new();
    private readonly MapSet map;
    private readonly MapFormat mapFormat;

    public UdbScriptBlockMapWrapper(MapSet map, double blockSize = 128.0, MapFormat mapFormat = MapFormat.Doom)
        : this(map, lines: true, things: true, sectors: true, vertices: true, blockSize, mapFormat)
    {
    }

    public UdbScriptBlockMapWrapper(
        MapSet map,
        bool lines,
        bool things,
        bool sectors,
        bool vertices,
        double blockSize = 128.0,
        MapFormat mapFormat = MapFormat.Doom)
    {
        this.map = map;
        this.mapFormat = mapFormat;
        blockMap = CreateBlockMap(map, lines, things, sectors, vertices, blockSize);
    }

    public UdbScriptBlockMapWrapper(
        MapSet map,
        IDictionary<string, object> options,
        double blockSize = 128.0,
        MapFormat mapFormat = MapFormat.Doom)
        : this(
            map,
            lines: IsOptionSet(options, "lines"),
            things: IsOptionSet(options, "things"),
            sectors: IsOptionSet(options, "sectors"),
            vertices: IsOptionSet(options, "vertices"),
            blockSize,
            mapFormat)
    {
    }

    public BlockMap BlockMap => blockMap;

    public UdbScriptBlockEntryWrapper getBlockAt(object pos)
    {
        Vector2D point = ToVector2D(pos);
        BlockMapCell entry = blockMap.GetBlockAt(point) ?? EmptyCell();
        if (!blockEntries.TryGetValue(entry, out UdbScriptBlockEntryWrapper? wrapper))
        {
            wrapper = new UdbScriptBlockEntryWrapper(entry, map, mapFormat);
            blockEntries[entry] = wrapper;
        }

        return wrapper;
    }

    public UdbScriptBlockMapQueryResult getLineBlocks(object v1, object v2)
        => new(blockMap.GetLineBlocks(ToVector2D(v1), ToVector2D(v2)), map, mapFormat);

    public UdbScriptBlockMapQueryResult getRectangleBlocks(int x, int y, int width, int height)
        => new(blockMap.GetBlocks(new RectangleF(x, y, width, height)), map, mapFormat);

    private static BlockMap CreateBlockMap(
        MapSet map,
        bool lines,
        bool things,
        bool sectors,
        bool vertices,
        double blockSize)
    {
        if (lines && things && sectors && vertices)
            return new BlockMap(map, blockSize);

        RectangleF area = CreateArea(map, things);
        var blockMap = new BlockMap(area, blockSize);

        if (lines) blockMap.AddLinedefs(map.Linedefs);
        if (things) blockMap.AddThings(map.Things);
        if (sectors) blockMap.AddSectors(map.Sectors);
        if (vertices) blockMap.AddVertices(map.Vertices);

        return blockMap;
    }

    private static RectangleF CreateArea(MapSet map, bool includeThings)
    {
        if (map.Vertices.Count == 0)
        {
            if (!includeThings || map.Things.Count == 0)
                return new RectangleF(0, 0, 1, 1);

            return MapSet.IncreaseArea(new RectangleF(0, 0, 1, 1), map.Things);
        }

        RectangleF area = MapSet.CreateArea(map.Vertices);
        return includeThings ? MapSet.IncreaseArea(area, map.Things) : area;
    }

    private static BlockMapCell EmptyCell()
        => new(
            Array.Empty<Linedef>(),
            Array.Empty<Thing>(),
            Array.Empty<Sector>(),
            Array.Empty<Vertex>());

    private static bool IsOptionSet(IDictionary<string, object>? options, string name)
        => options != null
            && options.TryGetValue(name, out object? value)
            && value is bool enabled
            && enabled;

    private static Vector2D ToVector2D(object value)
    {
        Vector3D vector = UdbScriptApiConversionModel.GetVector3DFromObject(value);
        return new Vector2D(vector.x, vector.y);
    }
}

public sealed class UdbScriptVisualCameraWrapper
{
    private readonly VisualCameraPose camera;

    public UdbScriptVisualCameraWrapper(VisualCameraPose camera)
    {
        this.camera = camera;
    }

    public UdbScriptVector3DWrapper position
        => new(camera.Position.x, camera.Position.y, camera.Position.z);

    public double angleXY
        => camera.Yaw;

    public double angleZ
        => camera.Pitch;
}

public sealed class UdbScriptMapWrapper
{
    public enum MergeGeometryMode
    {
        CLASSIC,
        MERGE,
        REPLACE,
    }

    private readonly GridSetup grid;
    private readonly object? highlightedObject;
    private readonly MapSet map;
    private readonly MapFormat mapFormat;
    private readonly GameConfiguration? config;
    private readonly Vector2D mouseMapPosition;
    private readonly VisualCameraPose visualCamera;

    public UdbScriptMapWrapper(
        MapSet map,
        GridSetup? grid = null,
        object? highlightedObject = null,
        MapFormat mapFormat = MapFormat.Doom,
        Vector2D? mousePosition = null,
        VisualCameraPose? visualCamera = null,
        GameConfiguration? config = null)
    {
        this.map = map;
        this.grid = grid ?? new GridSetup();
        this.highlightedObject = highlightedObject;
        this.mapFormat = mapFormat;
        mouseMapPosition = mousePosition ?? new Vector2D();
        this.visualCamera = visualCamera ?? new VisualCameraPose(new Vector3D(), 0.0, 0.0);
        this.config = config;
    }

    public MapSet Map
        => map;

    public bool isDoom
        => mapFormat == MapFormat.Doom;

    public bool isHexen
        => mapFormat == MapFormat.Hexen;

    public bool isUDMF
        => mapFormat == MapFormat.Udmf;

    public UdbScriptVector2DWrapper mousePosition
        => new(mouseMapPosition.x, mouseMapPosition.y);

    public UdbScriptVisualCameraWrapper camera
        => new(visualCamera);

    public UdbScriptVector2DWrapper snappedToGrid(object pos)
    {
        ThrowIfDisposed("snappedToGrid");
        Vector2D point = ToVector2D(pos);
        Vector2D snapped = grid.SnappedToGrid(point);
        return new UdbScriptVector2DWrapper(snapped.x, snapped.y);
    }

    public UdbScriptThingWrapper[] getThings()
    {
        ThrowIfDisposed("getThings");
        return map.Things
            .Where(thing => !thing.IsDisposed)
            .Select(thing => new UdbScriptThingWrapper(thing, map, grid, highlightedObject, mapFormat, config))
            .ToArray();
    }

    public UdbScriptSectorWrapper[] getSectors()
    {
        ThrowIfDisposed("getSectors");
        return map.Sectors
            .Where(sector => !sector.IsDisposed)
            .Select(sector => new UdbScriptSectorWrapper(sector, map, grid, highlightedObject, mapFormat, config))
            .ToArray();
    }

    public UdbScriptSidedefWrapper[] getSidedefs()
    {
        ThrowIfDisposed("getSidedefs");
        return map.Sidedefs
            .Where(sidedef => !sidedef.IsDisposed)
            .Select(sidedef => new UdbScriptSidedefWrapper(sidedef, map, grid, highlightedObject, mapFormat, config))
            .ToArray();
    }

    public UdbScriptLinedefWrapper[] getLinedefs()
    {
        ThrowIfDisposed("getLinedefs");
        return map.Linedefs
            .Where(linedef => !linedef.IsDisposed)
            .Select(linedef => new UdbScriptLinedefWrapper(linedef, map, grid, highlightedObject, mapFormat, config))
            .ToArray();
    }

    public UdbScriptVertexWrapper[] getVertices()
    {
        ThrowIfDisposed("getVertices");
        return map.Vertices
            .Where(vertex => !vertex.IsDisposed)
            .Select(vertex => new UdbScriptVertexWrapper(vertex, map, grid, config))
            .ToArray();
    }

    public int getNewTag(int[]? usedtags = null)
    {
        ThrowIfDisposed("getNewTag");
        if (config != null)
            return usedtags == null
                ? ConfiguredTagSearch.NextFreeTag(map, config)
                : ConfiguredTagSearch.NextFreeTag(map, config, usedtags);

        return usedtags == null ? map.GetNewTag() : map.GetNewTag(usedtags);
    }

    public int[] getMultipleNewTags(int count)
    {
        ThrowIfDisposed("getMultipleNewTags");
        if (config != null) return ConfiguredTagSearch.NextFreeTags(map, config, count).ToArray();
        return map.GetMultipleNewTags(count).ToArray();
    }

    public UdbScriptLinedefWrapper? nearestLinedef(object pos, double maxrange = double.NaN)
    {
        ThrowIfDisposed("nearestLinedef");
        Vector2D point = ToVector2D(pos);
        Linedef? nearest = double.IsNaN(maxrange)
            ? map.NearestLinedef(point)
            : map.NearestLinedefRange(point, maxrange);

        return nearest == null ? null : new UdbScriptLinedefWrapper(nearest, map, grid, highlightedObject, mapFormat, config);
    }

    public UdbScriptThingWrapper? nearestThing(object pos, double maxrange = double.NaN)
    {
        ThrowIfDisposed("nearestThing");
        Vector2D point = ToVector2D(pos);
        Thing? nearest = map.NearestThingSquareRange(point, double.IsNaN(maxrange) ? double.MaxValue : maxrange);

        return nearest == null ? null : new UdbScriptThingWrapper(nearest, map, grid, highlightedObject, mapFormat, config);
    }

    public UdbScriptVertexWrapper? nearestVertex(object pos, double maxrange = double.NaN)
    {
        ThrowIfDisposed("nearestVertex");
        Vector2D point = ToVector2D(pos);
        Vertex? nearest = map.NearestVertexSquareRange(point, double.IsNaN(maxrange) ? double.MaxValue : maxrange);

        return nearest == null ? null : new UdbScriptVertexWrapper(nearest, map, grid, config);
    }

    public UdbScriptSidedefWrapper? nearestSidedef(object pos)
    {
        ThrowIfDisposed("nearestSidedef");
        Sidedef? nearest = map.NearestSidedef(ToVector2D(pos));

        return nearest == null ? null : new UdbScriptSidedefWrapper(nearest, map, grid, highlightedObject, mapFormat, config);
    }

    public bool drawLines(object data)
    {
        ThrowIfDisposed("drawLines");
        if (data is not Array array)
            throw new InvalidOperationException("Data must be supplied as an array");

        var points = new List<Vector2D>(array.Length);
        foreach (object? item in array)
        {
            if (item == null)
                throw new InvalidOperationException(UdbScriptApiConversionModel.VectorConversionFailureMessage);

            points.Add(ToVector2D(item));
        }

        if (points.Count < 2)
            throw new InvalidOperationException("Array must have at least 2 values");

        map.ClearAllMarked(false);
        bool closed = points.Count > 2 && points[0] == points[^1];
        List<Vertex> vertices = AddDrawnVertices(points, closed);
        bool success = closed
            ? AddDrawnSectorLoop(vertices)
            : AddDrawnLinedefs(vertices);

        map.SnapAllToAccuracy(3);
        map.BuildIndexes();
        return success;
    }

    public void clearAllMarks(bool mark = false)
    {
        ThrowIfDisposed("clearAllMarks");
        map.ClearAllMarked(mark);
    }

    public void clearMarkedVertices(bool mark = false)
    {
        ThrowIfDisposed("clearMarkedVertices");
        map.ClearMarkedVertices(mark);
    }

    public void clearMarkedThings(bool mark = false)
    {
        ThrowIfDisposed("clearMarkedThings");
        map.ClearMarkedThings(mark);
    }

    public void clearMarkedLinedefs(bool mark = false)
    {
        ThrowIfDisposed("clearMarkedLinedefs");
        map.ClearMarkedLinedefs(mark);
    }

    public void clearMarkedSidedefs(bool mark = false)
    {
        ThrowIfDisposed("clearMarkedSidedefs");
        map.ClearMarkedSidedefs(mark);
    }

    public void clearMarkedSectors(bool mark = false)
    {
        ThrowIfDisposed("clearMarkedSectors");
        map.ClearMarkedSectors(mark);
    }

    public void clearMarkeLinedefs(bool mark = false)
        => clearMarkedLinedefs(mark);

    public void clearMarkeSidedefs(bool mark = false)
        => clearMarkedSidedefs(mark);

    public void clearMarkeSectors(bool mark = false)
        => clearMarkedSectors(mark);

    public void invertAllMarks()
    {
        ThrowIfDisposed("invertAllMarks");
        map.InvertAllMarked();
    }

    public void invertMarkedVertices()
    {
        ThrowIfDisposed("invertMarkedVertices");
        map.InvertMarkedVertices();
    }

    public void invertMarkedThings()
    {
        ThrowIfDisposed("invertMarkedThings");
        map.InvertMarkedThings();
    }

    public void invertMarkedLinedefs()
    {
        ThrowIfDisposed("invertMarkedLinedefs");
        map.InvertMarkedLinedefs();
    }

    public void invertMarkedSidedefs()
    {
        ThrowIfDisposed("invertMarkedSidedefs");
        map.InvertMarkedSidedefs();
    }

    public void invertMarkedSectors()
    {
        ThrowIfDisposed("invertMarkedSectors");
        map.InvertMarkedSectors();
    }

    public UdbScriptVertexWrapper[] getMarkedVertices(bool mark = true)
    {
        ThrowIfDisposed("getMarkedVertices");
        return map.GetMarkedVertices(mark)
            .Where(vertex => !vertex.IsDisposed)
            .Select(vertex => new UdbScriptVertexWrapper(vertex, map, grid, config))
            .ToArray();
    }

    public UdbScriptThingWrapper[] getMarkedThings(bool mark = true)
    {
        ThrowIfDisposed("getMarkedThings");
        return map.GetMarkedThings(mark)
            .Where(thing => !thing.IsDisposed)
            .Select(thing => new UdbScriptThingWrapper(thing, map, grid, highlightedObject, mapFormat, config))
            .ToArray();
    }

    public UdbScriptLinedefWrapper[] getMarkedLinedefs(bool mark = true)
    {
        ThrowIfDisposed("getMarkedLinedefs");
        return map.GetMarkedLinedefs(mark)
            .Where(linedef => !linedef.IsDisposed)
            .Select(linedef => new UdbScriptLinedefWrapper(linedef, map, grid, highlightedObject, mapFormat, config))
            .ToArray();
    }

    public UdbScriptSidedefWrapper[] getMarkedSidedefs(bool mark = true)
    {
        ThrowIfDisposed("getMarkedSidedefs");
        return map.GetMarkedSidedefs(mark)
            .Where(sidedef => !sidedef.IsDisposed)
            .Select(sidedef => new UdbScriptSidedefWrapper(sidedef, map, grid, highlightedObject, mapFormat, config))
            .ToArray();
    }

    public UdbScriptSectorWrapper[] getMarkedSectors(bool mark = true)
    {
        ThrowIfDisposed("getMarkedSectors");
        return map.GetMarkedSectors(mark)
            .Where(sector => !sector.IsDisposed)
            .Select(sector => new UdbScriptSectorWrapper(sector, map, grid, highlightedObject, mapFormat, config))
            .ToArray();
    }

    public void markSelectedVertices(bool mark = true)
    {
        ThrowIfDisposed("markSelectedVertices");
        map.MarkSelectedVertices(selected: true, mark);
    }

    public void markSelectedLinedefs(bool mark = true)
    {
        ThrowIfDisposed("markSelectedLinedefs");
        map.MarkSelectedLinedefs(selected: true, mark);
    }

    public void markSelectedSidedefs(bool mark = true)
    {
        ThrowIfDisposed("markSelectedSidedefs");
        map.MarkSelectedSidedefs(selected: true, mark);
    }

    public void markSelectedSectors(bool mark = true)
    {
        ThrowIfDisposed("markSelectedSectors");
        map.MarkSelectedSectors(selected: true, mark);
    }

    public void markSelectedThings(bool mark = true)
    {
        ThrowIfDisposed("markSelectedThings");
        map.MarkSelectedThings(selected: true, mark);
    }

    public UdbScriptVertexWrapper[] getSelectedVertices(bool selected = true)
    {
        ThrowIfDisposed("getSelectedVertices");
        return map.GetSelectedVertices(selected)
            .Where(vertex => !vertex.IsDisposed)
            .Select(vertex => new UdbScriptVertexWrapper(vertex, map, grid, config))
            .ToArray();
    }

    public UdbScriptVertexWrapper? getHighlightedVertex()
    {
        ThrowIfDisposed("getHighlightedVertex");
        return highlightedObject is Vertex vertex && !vertex.IsDisposed
            ? new UdbScriptVertexWrapper(vertex, map, grid, config)
            : null;
    }

    public UdbScriptVertexWrapper[] getSelectedOrHighlightedVertices()
    {
        ThrowIfDisposed("getSelectedOrHighlightedVertices");
        UdbScriptVertexWrapper[] selected = getSelectedVertices();
        if (selected.Length > 0) return selected;

        UdbScriptVertexWrapper? highlighted = getHighlightedVertex();
        return highlighted == null ? Array.Empty<UdbScriptVertexWrapper>() : new[] { highlighted };
    }

    public UdbScriptThingWrapper[] getSelectedThings(bool selected = true)
    {
        ThrowIfDisposed("getSelectedThings");
        return map.GetSelectedThings(selected)
            .Where(thing => !thing.IsDisposed)
            .Select(thing => new UdbScriptThingWrapper(thing, map, grid, highlightedObject, mapFormat, config))
            .ToArray();
    }

    public UdbScriptThingWrapper? getHighlightedThing()
    {
        ThrowIfDisposed("getHighlightedThing");
        return highlightedObject is Thing thing && !thing.IsDisposed
            ? new UdbScriptThingWrapper(thing, map, grid, highlightedObject, mapFormat, config)
            : null;
    }

    public UdbScriptThingWrapper[] getSelectedOrHighlightedThings()
    {
        ThrowIfDisposed("getSelectedOrHighlightedThings");
        UdbScriptThingWrapper[] selected = getSelectedThings();
        if (selected.Length > 0) return selected;

        UdbScriptThingWrapper? highlighted = getHighlightedThing();
        return highlighted == null ? Array.Empty<UdbScriptThingWrapper>() : new[] { highlighted };
    }

    public UdbScriptSectorWrapper[] getSelectedSectors(bool selected = true)
    {
        ThrowIfDisposed("getSelectedSectors");
        return map.GetSelectedSectors(selected)
            .Where(sector => !sector.IsDisposed)
            .Select(sector => new UdbScriptSectorWrapper(sector, map, grid, highlightedObject, mapFormat, config))
            .ToArray();
    }

    public UdbScriptSectorWrapper? getHighlightedSector()
    {
        ThrowIfDisposed("getHighlightedSector");
        Sector? sector = highlightedObject switch
        {
            Sector highlighted when !highlighted.IsDisposed => highlighted,
            UdbScriptHighlightedSectorSurface highlighted when !highlighted.Sector.IsDisposed => highlighted.Sector,
            _ => null,
        };

        return sector == null ? null : new UdbScriptSectorWrapper(sector, map, grid, highlightedObject, mapFormat, config);
    }

    public UdbScriptSectorWrapper[] getSelectedOrHighlightedSectors()
    {
        ThrowIfDisposed("getSelectedOrHighlightedSectors");
        UdbScriptSectorWrapper[] selected = getSelectedSectors();
        if (selected.Length > 0) return selected;

        UdbScriptSectorWrapper? highlighted = getHighlightedSector();
        return highlighted == null ? Array.Empty<UdbScriptSectorWrapper>() : new[] { highlighted };
    }

    public UdbScriptLinedefWrapper[] getSelectedLinedefs(bool selected = true)
    {
        ThrowIfDisposed("getSelectedLinedefs");
        return map.GetSelectedLinedefs(selected)
            .Where(linedef => !linedef.IsDisposed)
            .Select(linedef => new UdbScriptLinedefWrapper(linedef, map, grid, highlightedObject, mapFormat, config))
            .ToArray();
    }

    public UdbScriptSidedefWrapper[] getSelectedSidedefs(bool selected = true)
    {
        ThrowIfDisposed("getSelectedSidedefs");
        return map.GetSelectedSidedefs(selected)
            .Where(sidedef => !sidedef.IsDisposed)
            .Select(sidedef => new UdbScriptSidedefWrapper(sidedef, map, grid, highlightedObject, mapFormat, config))
            .ToArray();
    }

    public UdbScriptLinedefWrapper? getHighlightedLinedef()
    {
        ThrowIfDisposed("getHighlightedLinedef");
        Linedef? linedef = highlightedObject switch
        {
            Linedef line when !line.IsDisposed => line,
            Sidedef side when !side.IsDisposed && !side.Line.IsDisposed => side.Line,
            UdbScriptHighlightedSidedefPart highlighted
                when !highlighted.Sidedef.IsDisposed && !highlighted.Sidedef.Line.IsDisposed => highlighted.Sidedef.Line,
            _ => null,
        };

        return linedef == null ? null : new UdbScriptLinedefWrapper(linedef, map, grid, highlightedObject, mapFormat, config);
    }

    public UdbScriptSidedefWrapper? getHighlightedSidedef()
    {
        ThrowIfDisposed("getHighlightedSidedef");
        Sidedef? sidedef = highlightedObject switch
        {
            Sidedef side when !side.IsDisposed => side,
            UdbScriptHighlightedSidedefPart highlighted when !highlighted.Sidedef.IsDisposed => highlighted.Sidedef,
            _ => null,
        };

        return sidedef == null ? null : new UdbScriptSidedefWrapper(sidedef, map, grid, highlightedObject, mapFormat, config);
    }

    public UdbScriptSidedefWrapper[] getSelectedOrHighlightedSidedefs()
    {
        ThrowIfDisposed("getSelectedOrHighlightedSidedefs");
        UdbScriptSidedefWrapper[] selected = getSelectedSidedefs();
        if (selected.Length > 0) return selected;

        UdbScriptSidedefWrapper? highlighted = getHighlightedSidedef();
        return highlighted == null ? Array.Empty<UdbScriptSidedefWrapper>() : new[] { highlighted };
    }

    public UdbScriptLinedefWrapper[] getSelectedOrHighlightedLinedefs()
    {
        ThrowIfDisposed("getSelectedOrHighlightedLinedefs");
        UdbScriptLinedefWrapper[] selected = getSelectedLinedefs();
        if (selected.Length > 0) return selected;

        UdbScriptLinedefWrapper? highlighted = getHighlightedLinedef();
        return highlighted == null ? Array.Empty<UdbScriptLinedefWrapper>() : new[] { highlighted };
    }

    public UdbScriptSidedefWrapper[] getSidedefsFromSelectedLinedefs(bool selected = true)
    {
        ThrowIfDisposed("getSidedefsFromSelectedLinedefs");
        return map.GetSidedefsFromSelectedLinedefs(selected)
            .Where(sidedef => !sidedef.IsDisposed)
            .Select(sidedef => new UdbScriptSidedefWrapper(sidedef, map, grid, highlightedObject, mapFormat, config))
            .ToArray();
    }

    public UdbScriptSidedefWrapper[] getSidedefsFromSelectedOrHighlightedLinedefs()
    {
        ThrowIfDisposed("getSidedefsFromSelectedOrHighlightedLinedefs");
        UdbScriptSidedefWrapper[] selected = getSidedefsFromSelectedLinedefs();
        if (selected.Length > 0) return selected;

        Linedef? highlighted = highlightedObject switch
        {
            Linedef line when !line.IsDisposed => line,
            Sidedef side when !side.IsDisposed && !side.Line.IsDisposed => side.Line,
            UdbScriptHighlightedSidedefPart part
                when !part.Sidedef.IsDisposed && !part.Sidedef.Line.IsDisposed => part.Sidedef.Line,
            _ => null,
        };

        if (highlighted == null) return Array.Empty<UdbScriptSidedefWrapper>();

        return new[] { highlighted.Front, highlighted.Back }
            .Where(sidedef => sidedef != null && !sidedef.IsDisposed)
            .Select(sidedef => new UdbScriptSidedefWrapper(sidedef!, map, grid, highlightedObject, mapFormat, config))
            .ToArray();
    }

    public void clearAllSelected()
    {
        ThrowIfDisposed("clearAllSelected");
        map.ClearAllSelected();
    }

    public void clearSelectedVertices()
    {
        ThrowIfDisposed("clearSelectedVertices");
        map.ClearSelectedVertices();
    }

    public void clearSelectedThings()
    {
        ThrowIfDisposed("clearSelectedThings");
        map.ClearSelectedThings();
    }

    public void clearSelectedSectors()
    {
        ThrowIfDisposed("clearSelectedSectors");
        map.ClearSelectedSectors();
    }

    public void clearSelectedLinedefs()
    {
        ThrowIfDisposed("clearSelectedLinedefs");
        map.ClearSelectedLinedefs();
    }

    public void clearSelectedSidedefs()
    {
        ThrowIfDisposed("clearSelectedSidedefs");
        map.ClearSelectedSidedefs();
    }

    public void selectAllVertices()
    {
        ThrowIfDisposed("selectAllVertices");
        map.SelectAllVertices();
    }

    public void selectAllLinedefs()
    {
        ThrowIfDisposed("selectAllLinedefs");
        map.SelectAllLinedefs();
    }

    public void selectAllSidedefs()
    {
        ThrowIfDisposed("selectAllSidedefs");
        map.SelectAllSidedefs();
    }

    public void selectAllSectors()
    {
        ThrowIfDisposed("selectAllSectors");
        map.SelectAllSectors();
    }

    public void selectAllThings()
    {
        ThrowIfDisposed("selectAllThings");
        map.SelectAllThings();
    }

    public void invertSelectedVertices()
    {
        ThrowIfDisposed("invertSelectedVertices");
        map.InvertSelectedVertices();
    }

    public void invertSelectedLinedefs()
    {
        ThrowIfDisposed("invertSelectedLinedefs");
        map.InvertSelectedLinedefs();
    }

    public void invertSelectedSidedefs()
    {
        ThrowIfDisposed("invertSelectedSidedefs");
        map.InvertSelectedSidedefs();
    }

    public void invertSelectedSectors()
    {
        ThrowIfDisposed("invertSelectedSectors");
        map.InvertSelectedSectors();
    }

    public void invertSelectedThings()
    {
        ThrowIfDisposed("invertSelectedThings");
        map.InvertSelectedThings();
    }

    public void addSelectionToGroup(int groupIndex)
    {
        ThrowIfDisposed("addSelectionToGroup");
        map.AddSelectionToGroup(groupIndex);
    }

    public void clearGroup(int groupIndex)
    {
        ThrowIfDisposed("clearGroup");
        map.ClearGroup(MapSet.GroupMask(groupIndex));
    }

    public void selectVerticesByGroup(int groupIndex)
    {
        ThrowIfDisposed("selectVerticesByGroup");
        map.SelectVerticesByGroup(MapSet.GroupMask(groupIndex));
    }

    public void selectLinedefsByGroup(int groupIndex)
    {
        ThrowIfDisposed("selectLinedefsByGroup");
        map.SelectLinedefsByGroup(MapSet.GroupMask(groupIndex));
    }

    public void selectSectorsByGroup(int groupIndex)
    {
        ThrowIfDisposed("selectSectorsByGroup");
        map.SelectSectorsByGroup(MapSet.GroupMask(groupIndex));
    }

    public void selectThingsByGroup(int groupIndex)
    {
        ThrowIfDisposed("selectThingsByGroup");
        map.SelectThingsByGroup(MapSet.GroupMask(groupIndex));
    }

    public int moveSelectedVerticesBy(object delta)
    {
        ThrowIfDisposed("moveSelectedVerticesBy");
        return map.MoveSelectedVerticesBy(ToVector2D(delta));
    }

    public int moveSelectedThingsBy(object delta)
    {
        ThrowIfDisposed("moveSelectedThingsBy");
        return map.MoveSelectedThingsBy(ToVector2D(delta));
    }

    public int flipSelectedLinedefs()
    {
        ThrowIfDisposed("flipSelectedLinedefs");
        int flipped = map.FlipSelectedLinedefs();
        map.BuildIndexes();
        return flipped;
    }

    public int flipSelectedSidedefs()
    {
        ThrowIfDisposed("flipSelectedSidedefs");
        int flipped = map.FlipSelectedSidedefs();
        map.BuildIndexes();
        return flipped;
    }

    public UdbScriptVertexWrapper createVertex(object pos)
    {
        ThrowIfDisposed("createVertex");
        Vector2D point = ToVector2D(pos);
        return new UdbScriptVertexWrapper(map.AddVertex(point), map, grid, config);
    }

    public UdbScriptThingWrapper createThing(object pos, int type = 0)
    {
        ThrowIfDisposed("createThing");
        if (type < 0)
            throw new InvalidOperationException("Thing type can not be negative.");

        Vector3D point = UdbScriptApiConversionModel.GetVector3DFromObject(pos);
        Thing thing = map.AddThing(new Vector2D(point.x, point.y), type);
        thing.Height = point.z;
        ApplyCleanThingSettings(thing);
        thing.DetermineSector(map);
        return new UdbScriptThingWrapper(thing, map, grid, highlightedObject, mapFormat, config);
    }

    public void joinSectors(UdbScriptSectorWrapper[] sectors)
    {
        ThrowIfDisposed("joinSectors");
        map.JoinSectors(sectors.Select(sector => sector.Sector).ToArray());
        map.BuildIndexes();
    }

    public void mergeSectors(UdbScriptSectorWrapper[] sectors)
    {
        ThrowIfDisposed("mergeSectors");
        map.MergeSectors(sectors.Select(sector => sector.Sector).ToArray());
        map.BuildIndexes();
    }

    public bool stitchGeometry(MergeGeometryMode mergemode = MergeGeometryMode.CLASSIC)
    {
        ThrowIfDisposed("stitchGeometry");
        DBuilder.Map.MergeGeometryMode mode = mergemode switch
        {
            MergeGeometryMode.CLASSIC => DBuilder.Map.MergeGeometryMode.Classic,
            MergeGeometryMode.MERGE => DBuilder.Map.MergeGeometryMode.Merge,
            MergeGeometryMode.REPLACE => DBuilder.Map.MergeGeometryMode.Replace,
            _ => throw new InvalidOperationException("Unknown MergeGeometryMode value"),
        };

        GeometryStitchResult result = map.StitchSelectedGeometry(mode);
        map.BuildIndexes();
        return result.TotalChanges > 0;
    }

    public void snapAllToAccuracy()
        => snapAllToAccuracy(3);

    public void snapAllToAccuracy(bool usePrecisePosition)
        => snapAllToAccuracy(3, usePrecisePosition);

    public void snapAllToAccuracy(int vertexDecimals, bool usePrecisePosition = true)
    {
        ThrowIfDisposed("snapAllToAccuracy");
        map.SnapAllToAccuracy(vertexDecimals, usePrecisePosition);
    }

    private void ApplyCleanThingSettings(Thing thing)
    {
        if (config == null) return;

        thing.UdmfFlags.Clear();
        thing.Flags = 0;
        foreach (string flag in config.DefaultThingFlags)
        {
            if (mapFormat == MapFormat.Udmf)
                thing.SetFlag(flag, true);
            else if (int.TryParse(flag, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bit) && bit > 0)
                thing.Flags |= bit;
        }

        ThingTypeInfo? info = config.GetThing(thing.Type);
        if (info == null) return;

        for (int i = 0; i < thing.Args.Length && i < info.Args.Length; i++)
            thing.Args[i] = Convert.ToInt32(info.Args[i].DefaultValue, CultureInfo.InvariantCulture);

        if (!config.UniversalFields.TryGetValue("thing", out var fields)) return;
        foreach (string fieldName in info.AddUniversalFields)
        {
            if (!fields.TryGetValue(fieldName, out UniversalFieldInfo? field) || field.DefaultValue == null) continue;

            object? converted = UdbScriptApiConversionModel.GetConvertedUniversalValue(
                new UdbScriptUniversalValue(field.Type, field.DefaultValue));
            if (converted != null)
                thing.Fields[fieldName] = converted;
        }
    }

    private void ThrowIfDisposed(string member)
    {
        if (map.IsDisposed)
            throw new InvalidOperationException("Map is disposed, the " + member + " member can not be accessed.");
    }

    private static Vector2D ToVector2D(object value)
    {
        Vector3D vector = UdbScriptApiConversionModel.GetVector3DFromObject(value);
        return new Vector2D(vector.x, vector.y);
    }

    private List<Vertex> AddDrawnVertices(IReadOnlyList<Vector2D> points, bool closed)
    {
        int count = closed ? points.Count - 1 : points.Count;
        var vertices = new List<Vertex>(count);
        for (int i = 0; i < count; i++)
        {
            Vertex vertex = map.AddVertex(points[i]);
            vertex.Marked = true;
            vertices.Add(vertex);
        }

        return vertices;
    }

    private bool AddDrawnLinedefs(IReadOnlyList<Vertex> vertices)
    {
        if (vertices.Count < 2) return false;

        for (int i = 1; i < vertices.Count; i++)
        {
            Linedef line = map.AddLinedef(vertices[i - 1], vertices[i]);
            line.Marked = true;
            line.ApplySidedFlags();
        }

        return true;
    }

    private bool AddDrawnSectorLoop(IReadOnlyList<Vertex> vertices)
    {
        Sector? sector = Tools.MakeSectorFromLoop(map, vertices);
        if (sector == null) return false;

        sector.Marked = true;
        MarkDrawnLoopLinedefs(vertices);

        return true;
    }

    private void MarkDrawnLoopLinedefs(IReadOnlyList<Vertex> vertices)
    {
        for (int i = 0; i < vertices.Count; i++)
        {
            Vertex start = vertices[i];
            Vertex end = vertices[(i + 1) % vertices.Count];
            foreach (Linedef line in map.Linedefs)
            {
                bool matchesForward = ReferenceEquals(line.Start, start) && ReferenceEquals(line.End, end);
                bool matchesBackward = ReferenceEquals(line.Start, end) && ReferenceEquals(line.End, start);
                if (!matchesForward && !matchesBackward) continue;

                line.Marked = true;
                break;
            }
        }
    }
}

public sealed class UdbScriptMapElementArgumentsWrapper : IEnumerable<int>
{
    private readonly IHasArguments element;

    public UdbScriptMapElementArgumentsWrapper(IHasArguments element)
    {
        this.element = element;
    }

    public int this[int i]
    {
        get => element.Args[i];
        set => element.Args[i] = value;
    }

    public int length
        => element.Args.Length;

    public IEnumerator<int> GetEnumerator()
    {
        foreach (int value in element.Args)
            yield return value;
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}

public sealed record UdbScriptUniversalValue(int Type, object? Value);

public sealed class UdbScriptVectorConversionException : Exception
{
    public UdbScriptVectorConversionException(string message)
        : base(message)
    {
    }
}

public static class UdbScriptApiConversionModel
{
    public const string VectorConversionFailureMessage =
        "Data must be a Vector2D, Vector3D, an array of numbers, or an object with (x, y, z) members.";

    public static Vector3D GetVector3DFromObject(object data)
    {
        if (data is Vector2D vector2D)
            return vector2D;

        if (data is UdbScriptVector2DWrapper vector2DWrapper)
            return new Vector2D(vector2DWrapper.X, vector2DWrapper.Y);

        if (data is Vector3D vector3D)
            return vector3D;

        if (data is UdbScriptVector3DWrapper vector3DWrapper)
            return new Vector3D(vector3DWrapper.X, vector3DWrapper.Y, vector3DWrapper.Z);

        if (data.GetType().IsArray)
            return VectorFromArray(data);

        if (data is ExpandoObject expando)
            return VectorFromDictionary((IDictionary<string, object?>)expando);

        throw new UdbScriptVectorConversionException(VectorConversionFailureMessage);
    }

    public static object? GetConvertedUniversalValue(UdbScriptUniversalValue value)
    {
        return (UniversalType)value.Type switch
        {
            UniversalType.AngleRadians or UniversalType.AngleDegreesFloat or UniversalType.Float => Convert.ToDouble(value.Value),
            UniversalType.AngleDegrees or UniversalType.AngleByte or UniversalType.Color or UniversalType.EnumBits
                or UniversalType.EnumOption or UniversalType.Integer or UniversalType.LinedefTag
                or UniversalType.LinedefType or UniversalType.SectorEffect or UniversalType.SectorTag
                or UniversalType.ThingTag or UniversalType.ThingType => Convert.ToInt32(value.Value),
            UniversalType.Boolean => Convert.ToBoolean(value.Value),
            UniversalType.Flat or UniversalType.String or UniversalType.Texture or UniversalType.EnumStrings
                or UniversalType.ThingClass => Convert.ToString(value.Value),
            _ => null,
        };
    }

    public static Type? GetTypeFromUniversalType(int type)
    {
        return (UniversalType)type switch
        {
            UniversalType.AngleRadians or UniversalType.AngleDegreesFloat or UniversalType.Float => typeof(double),
            UniversalType.AngleDegrees or UniversalType.AngleByte or UniversalType.Color or UniversalType.EnumBits
                or UniversalType.EnumOption or UniversalType.Integer or UniversalType.LinedefTag
                or UniversalType.LinedefType or UniversalType.SectorEffect or UniversalType.SectorTag
                or UniversalType.ThingTag or UniversalType.ThingType => typeof(int),
            UniversalType.Boolean => typeof(bool),
            UniversalType.Flat or UniversalType.String or UniversalType.Texture or UniversalType.EnumStrings
                or UniversalType.ThingClass => typeof(string),
            _ => null,
        };
    }

    private static Vector3D VectorFromArray(object data)
    {
        if (data is not object[] values)
            throw new UdbScriptVectorConversionException(VectorConversionFailureMessage);

        var numbers = new List<double>();
        foreach (object? raw in values)
        {
            if (raw is double number)
            {
                numbers.Add(number);
            }
            else if (raw is BigInteger bigInteger)
            {
                numbers.Add((double)bigInteger);
            }
            else
            {
                throw new UdbScriptVectorConversionException("Values in array must be numbers.");
            }
        }

        return numbers.Count switch
        {
            2 => new Vector2D(numbers[0], numbers[1]),
            3 => new Vector3D(numbers[0], numbers[1], numbers[2]),
            _ => throw new UdbScriptVectorConversionException(VectorConversionFailureMessage),
        };
    }

    private static Vector3D VectorFromDictionary(IDictionary<string, object?> values)
    {
        double x = double.NaN;
        double y = double.NaN;
        double z = 0.0;

        if (values.ContainsKey("x"))
            x = ConvertMember("x", values["x"]);

        if (values.ContainsKey("y"))
            y = ConvertMember("y", values["y"]);

        if (values.ContainsKey("z"))
            z = ConvertMember("z", values["z"]);

        if (!double.IsNaN(x) && !double.IsNaN(y) && !double.IsNaN(z))
            return new Vector3D(x, y, z);

        throw new UdbScriptVectorConversionException(VectorConversionFailureMessage);
    }

    private static double ConvertMember(string name, object? value)
    {
        try
        {
            return Convert.ToDouble(value);
        }
        catch (Exception ex)
        {
            throw new UdbScriptVectorConversionException("Can not convert '" + name + "' property of data: " + ex.Message);
        }
    }
}
