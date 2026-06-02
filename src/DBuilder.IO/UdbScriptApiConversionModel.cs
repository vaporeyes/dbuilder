// ABOUTME: Models UDBScript API helper conversions for vectors and universal values.
// ABOUTME: Provides pure conversion targets for future script API wrapper execution.

using System.Collections;
using System.Dynamic;
using System.Numerics;
using DBuilder.Geometry;

namespace DBuilder.IO;

public sealed record UdbScriptVector2DWrapper(double X, double Y)
{
    public double x => X;

    public double y => Y;

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

public sealed record UdbScriptVector3DWrapper(double X, double Y, double Z)
{
    public double x => X;

    public double y => Y;

    public double z => Z;

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

        if (data is IReadOnlyDictionary<string, object?> dictionary)
            return VectorFromDictionary(dictionary);

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
        if (data is not IEnumerable enumerable)
            throw new UdbScriptVectorConversionException(VectorConversionFailureMessage);

        var values = new List<double>();
        foreach (object? raw in enumerable)
        {
            if (raw is double number)
            {
                values.Add(number);
            }
            else if (raw is BigInteger bigInteger)
            {
                values.Add((double)bigInteger);
            }
            else
            {
                throw new UdbScriptVectorConversionException("Values in array must be numbers.");
            }
        }

        return values.Count switch
        {
            2 => new Vector2D(values[0], values[1]),
            3 => new Vector3D(values[0], values[1], values[2]),
            _ => throw new UdbScriptVectorConversionException(VectorConversionFailureMessage),
        };
    }

    private static Vector3D VectorFromDictionary(IReadOnlyDictionary<string, object?> values)
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
