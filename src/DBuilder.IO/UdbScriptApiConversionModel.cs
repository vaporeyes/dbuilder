// ABOUTME: Models UDBScript API helper conversions for vectors and universal values.
// ABOUTME: Provides pure conversion targets for future script API wrapper execution.

using System.Collections;
using System.Dynamic;
using System.Drawing;
using System.Numerics;
using DBuilder.Geometry;
using DBuilder.Map;

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

    public UdbScriptImageInfo? getTextureInfo(string name)
    {
        ImageData? image = resources.GetWallTexture(name);
        return image == null ? null : ImageInfo(name, image, isFlat: false);
    }

    public string[] getFlatNames()
        => resources.GetFlatNames().ToArray();

    public bool flatExists(string name)
        => resources.GetFlatNames().Contains(name, StringComparer.OrdinalIgnoreCase);

    public UdbScriptImageInfo? getFlatInfo(string name)
    {
        ImageData? image = resources.GetFlat(name);
        return image == null ? null : ImageInfo(name, image, isFlat: true);
    }

    private static UdbScriptImageInfo ImageInfo(string name, ImageData image, bool isFlat)
        => new(
            name,
            image.Width,
            image.Height,
            new UdbScriptVector2DWrapper(image.ScaleX, image.ScaleY),
            isFlat);
}

public sealed class UdbScriptVertexWrapper : IEquatable<UdbScriptVertexWrapper>
{
    private readonly Vertex vertex;

    public UdbScriptVertexWrapper(Vertex vertex)
    {
        this.vertex = vertex;
    }

    public Vertex Vertex
        => vertex;

    public object position
    {
        get
        {
            ThrowIfDisposed("position");
            return new UdbScriptVector2DWrapper(vertex.Position.x, vertex.Position.y);
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

    public Linedef[] getLinedefs()
    {
        ThrowIfDisposed("getLinedefs");
        return vertex.Linedefs.Where(line => !line.IsDisposed).ToArray();
    }

    public void copyPropertiesTo(UdbScriptVertexWrapper wrapper)
    {
        ThrowIfDisposed("copyPropertiesTo");
        vertex.CopyPropertiesTo(wrapper.vertex);
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
    private readonly Linedef linedef;
    private readonly UdbScriptMapElementArgumentsWrapper elementArgs;

    public UdbScriptLinedefWrapper(Linedef linedef)
    {
        this.linedef = linedef;
        elementArgs = new UdbScriptMapElementArgumentsWrapper(linedef);
    }

    public Linedef Linedef
        => linedef;

    public UdbScriptVertexWrapper start
    {
        get
        {
            ThrowIfDisposed("start");
            return new UdbScriptVertexWrapper(linedef.Start);
        }
    }

    public UdbScriptVertexWrapper end
    {
        get
        {
            ThrowIfDisposed("end");
            return new UdbScriptVertexWrapper(linedef.End);
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
            return linedef.Front == null ? null : new UdbScriptSidedefWrapper(linedef.Front);
        }
    }

    public UdbScriptSidedefWrapper? back
    {
        get
        {
            ThrowIfDisposed("back");
            return linedef.Back == null ? null : new UdbScriptSidedefWrapper(linedef.Back);
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

    public IReadOnlyDictionary<string, bool> flags
    {
        get
        {
            ThrowIfDisposed("flags");
            return linedef.UdmfFlags.ToDictionary(flag => flag, _ => true, StringComparer.OrdinalIgnoreCase);
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
}

public sealed class UdbScriptSidedefWrapper : IEquatable<UdbScriptSidedefWrapper>
{
    private readonly Sidedef sidedef;

    public UdbScriptSidedefWrapper(Sidedef sidedef)
    {
        this.sidedef = sidedef;
    }

    public Sidedef Sidedef
        => sidedef;

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
            return new UdbScriptLinedefWrapper(sidedef.Line);
        }
    }

    public UdbScriptSectorWrapper? sector
    {
        get
        {
            ThrowIfDisposed("sector");
            return sidedef.Sector == null ? null : new UdbScriptSectorWrapper(sidedef.Sector);
        }
    }

    public UdbScriptSidedefWrapper? other
    {
        get
        {
            ThrowIfDisposed("other");
            return sidedef.Other == null ? null : new UdbScriptSidedefWrapper(sidedef.Other);
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

    public IReadOnlyDictionary<string, bool> flags
    {
        get
        {
            ThrowIfDisposed("flags");
            return sidedef.UdmfFlags.ToDictionary(flag => flag, _ => true, StringComparer.OrdinalIgnoreCase);
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

    public bool middleSelected
    {
        get
        {
            ThrowIfDisposed("middleSelected");
            return sidedef.Line.Selected;
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

    public void copyPropertiesTo(UdbScriptSidedefWrapper wrapper)
    {
        ThrowIfDisposed("copyPropertiesTo");
        sidedef.CopyPropertiesTo(wrapper.sidedef);
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
}

public sealed class UdbScriptSectorWrapper : IEquatable<UdbScriptSectorWrapper>
{
    private readonly Sector sector;

    public UdbScriptSectorWrapper(Sector sector)
    {
        this.sector = sector;
    }

    public Sector Sector
        => sector;

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
                side.Line.Selected = value;
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

    public bool ceilingSelected
    {
        get
        {
            ThrowIfDisposed("ceilingSelected");
            return sector.Selected;
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

    public IReadOnlyDictionary<string, bool> flags
    {
        get
        {
            ThrowIfDisposed("flags");
            return sector.UdmfFlags.ToDictionary(flag => flag, _ => true, StringComparer.OrdinalIgnoreCase);
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
            .Select(sidedef => new UdbScriptSidedefWrapper(sidedef))
            .ToArray();
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
}

public sealed class UdbScriptThingWrapper : IEquatable<UdbScriptThingWrapper>
{
    private readonly Thing thing;
    private readonly UdbScriptMapElementArgumentsWrapper elementArgs;

    public UdbScriptThingWrapper(Thing thing)
    {
        this.thing = thing;
        elementArgs = new UdbScriptMapElementArgumentsWrapper(thing);
    }

    public Thing Thing
        => thing;

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

    public IReadOnlyDictionary<string, bool> flags
    {
        get
        {
            ThrowIfDisposed("flags");
            return thing.UdmfFlags.ToDictionary(flag => flag, _ => true, StringComparer.OrdinalIgnoreCase);
        }
    }

    public object position
    {
        get
        {
            ThrowIfDisposed("position");
            return new UdbScriptVector3DWrapper(thing.Position.x, thing.Position.y, thing.Height);
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

    public UdbScriptSectorWrapper? getSector()
    {
        ThrowIfDisposed("getSector");
        return thing.Sector == null ? null : new UdbScriptSectorWrapper(thing.Sector);
    }

    public bool Equals(UdbScriptThingWrapper? other)
        => other is not null && ReferenceEquals(thing, other.thing);

    public override bool Equals(object? obj)
        => obj is UdbScriptThingWrapper other && Equals(other);

    public override int GetHashCode()
        => thing.GetHashCode();

    public override string ToString()
        => "Thing " + thing.Type;

    private void ThrowIfDisposed(string member)
    {
        if (thing.IsDisposed)
            throw new InvalidOperationException("Thing is disposed, the " + member + " member can not be accessed.");
    }
}

public abstract class UdbScriptBlockMapContentBase
{
    public abstract UdbScriptLinedefWrapper[] getLinedefs();
    public abstract UdbScriptThingWrapper[] getThings();
    public abstract UdbScriptSectorWrapper[] getSectors();
    public abstract UdbScriptVertexWrapper[] getVertices();

    protected static UdbScriptLinedefWrapper[] WrapLinedefs(IEnumerable<Linedef> lines)
        => lines
            .Where(line => !line.IsDisposed)
            .Select(line => new UdbScriptLinedefWrapper(line))
            .ToArray();

    protected static UdbScriptThingWrapper[] WrapThings(IEnumerable<Thing> things)
        => things
            .Where(thing => !thing.IsDisposed)
            .Select(thing => new UdbScriptThingWrapper(thing))
            .ToArray();

    protected static UdbScriptSectorWrapper[] WrapSectors(IEnumerable<Sector> sectors)
        => sectors
            .Where(sector => !sector.IsDisposed)
            .Select(sector => new UdbScriptSectorWrapper(sector))
            .ToArray();

    protected static UdbScriptVertexWrapper[] WrapVertices(IEnumerable<Vertex> vertices)
        => vertices
            .Where(vertex => !vertex.IsDisposed)
            .Select(vertex => new UdbScriptVertexWrapper(vertex))
            .ToArray();
}

public sealed class UdbScriptBlockEntryWrapper : UdbScriptBlockMapContentBase
{
    private readonly BlockMapCell entry;

    public UdbScriptBlockEntryWrapper(BlockMapCell entry)
    {
        this.entry = entry;
    }

    public BlockMapCell Entry => entry;

    public override UdbScriptLinedefWrapper[] getLinedefs()
        => WrapLinedefs(entry.Lines);

    public override UdbScriptThingWrapper[] getThings()
        => WrapThings(entry.Things);

    public override UdbScriptSectorWrapper[] getSectors()
        => WrapSectors(entry.Sectors);

    public override UdbScriptVertexWrapper[] getVertices()
        => WrapVertices(entry.Vertices);
}

public sealed class UdbScriptBlockMapQueryResult : UdbScriptBlockMapContentBase, IEnumerable<UdbScriptBlockEntryWrapper>
{
    private readonly IReadOnlyList<BlockMapCell> entries;

    public UdbScriptBlockMapQueryResult(IEnumerable<BlockMapCell> entries)
    {
        this.entries = entries.ToArray();
    }

    public override UdbScriptLinedefWrapper[] getLinedefs()
        => WrapLinedefs(entries.SelectMany(entry => entry.Lines).Distinct());

    public override UdbScriptThingWrapper[] getThings()
        => WrapThings(entries.SelectMany(entry => entry.Things).Distinct());

    public override UdbScriptSectorWrapper[] getSectors()
        => WrapSectors(entries.SelectMany(entry => entry.Sectors).Distinct());

    public override UdbScriptVertexWrapper[] getVertices()
        => WrapVertices(entries.SelectMany(entry => entry.Vertices).Distinct());

    public IEnumerator<UdbScriptBlockEntryWrapper> GetEnumerator()
    {
        foreach (BlockMapCell entry in entries)
            yield return new UdbScriptBlockEntryWrapper(entry);
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}

public sealed class UdbScriptBlockMapWrapper
{
    private readonly BlockMap blockMap;

    public UdbScriptBlockMapWrapper(MapSet map, double blockSize = 128.0)
        : this(map, lines: true, things: true, sectors: true, vertices: true, blockSize)
    {
    }

    public UdbScriptBlockMapWrapper(
        MapSet map,
        bool lines,
        bool things,
        bool sectors,
        bool vertices,
        double blockSize = 128.0)
    {
        blockMap = CreateBlockMap(map, lines, things, sectors, vertices, blockSize);
    }

    public BlockMap BlockMap => blockMap;

    public UdbScriptBlockEntryWrapper getBlockAt(object pos)
    {
        Vector2D point = ToVector2D(pos);
        return new UdbScriptBlockEntryWrapper(blockMap.GetBlockAt(point) ?? EmptyCell());
    }

    public UdbScriptBlockMapQueryResult getLineBlocks(object v1, object v2)
        => new(blockMap.GetLineBlocks(ToVector2D(v1), ToVector2D(v2)));

    public UdbScriptBlockMapQueryResult getRectangleBlocks(int x, int y, int width, int height)
        => new(blockMap.GetBlocks(new RectangleF(x, y, width, height)));

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

    private static Vector2D ToVector2D(object value)
    {
        Vector3D vector = UdbScriptApiConversionModel.GetVector3DFromObject(value);
        return new Vector2D(vector.x, vector.y);
    }
}

public sealed class UdbScriptMapWrapper
{
    private readonly MapSet map;

    public UdbScriptMapWrapper(MapSet map)
    {
        this.map = map;
    }

    public MapSet Map
        => map;

    public UdbScriptThingWrapper[] getThings()
    {
        ThrowIfDisposed("getThings");
        return map.Things
            .Where(thing => !thing.IsDisposed)
            .Select(thing => new UdbScriptThingWrapper(thing))
            .ToArray();
    }

    public UdbScriptSectorWrapper[] getSectors()
    {
        ThrowIfDisposed("getSectors");
        return map.Sectors
            .Where(sector => !sector.IsDisposed)
            .Select(sector => new UdbScriptSectorWrapper(sector))
            .ToArray();
    }

    public UdbScriptSidedefWrapper[] getSidedefs()
    {
        ThrowIfDisposed("getSidedefs");
        return map.Sidedefs
            .Where(sidedef => !sidedef.IsDisposed)
            .Select(sidedef => new UdbScriptSidedefWrapper(sidedef))
            .ToArray();
    }

    public UdbScriptLinedefWrapper[] getLinedefs()
    {
        ThrowIfDisposed("getLinedefs");
        return map.Linedefs
            .Where(linedef => !linedef.IsDisposed)
            .Select(linedef => new UdbScriptLinedefWrapper(linedef))
            .ToArray();
    }

    public UdbScriptVertexWrapper[] getVertices()
    {
        ThrowIfDisposed("getVertices");
        return map.Vertices
            .Where(vertex => !vertex.IsDisposed)
            .Select(vertex => new UdbScriptVertexWrapper(vertex))
            .ToArray();
    }

    public int getNewTag(int[]? usedtags = null)
    {
        ThrowIfDisposed("getNewTag");
        return usedtags == null ? map.GetNewTag() : map.GetNewTag(usedtags);
    }

    public int[] getMultipleNewTags(int count)
    {
        ThrowIfDisposed("getMultipleNewTags");
        return map.GetMultipleNewTags(count).ToArray();
    }

    public UdbScriptLinedefWrapper? nearestLinedef(object pos, double maxrange = double.NaN)
    {
        ThrowIfDisposed("nearestLinedef");
        Vector2D point = ToVector2D(pos);
        Linedef? nearest = double.IsNaN(maxrange)
            ? map.NearestLinedef(point)
            : map.NearestLinedefRange(point, maxrange);

        return nearest == null ? null : new UdbScriptLinedefWrapper(nearest);
    }

    public UdbScriptThingWrapper? nearestThing(object pos, double maxrange = double.NaN)
    {
        ThrowIfDisposed("nearestThing");
        Vector2D point = ToVector2D(pos);
        Thing? nearest = map.NearestThingSquareRange(point, double.IsNaN(maxrange) ? double.MaxValue : maxrange);

        return nearest == null ? null : new UdbScriptThingWrapper(nearest);
    }

    public UdbScriptVertexWrapper? nearestVertex(object pos, double maxrange = double.NaN)
    {
        ThrowIfDisposed("nearestVertex");
        Vector2D point = ToVector2D(pos);
        Vertex? nearest = map.NearestVertexSquareRange(point, double.IsNaN(maxrange) ? double.MaxValue : maxrange);

        return nearest == null ? null : new UdbScriptVertexWrapper(nearest);
    }

    public UdbScriptSidedefWrapper? nearestSidedef(object pos)
    {
        ThrowIfDisposed("nearestSidedef");
        Sidedef? nearest = map.NearestSidedef(ToVector2D(pos));

        return nearest == null ? null : new UdbScriptSidedefWrapper(nearest);
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
            .Select(vertex => new UdbScriptVertexWrapper(vertex))
            .ToArray();
    }

    public UdbScriptThingWrapper[] getMarkedThings(bool mark = true)
    {
        ThrowIfDisposed("getMarkedThings");
        return map.GetMarkedThings(mark)
            .Where(thing => !thing.IsDisposed)
            .Select(thing => new UdbScriptThingWrapper(thing))
            .ToArray();
    }

    public UdbScriptLinedefWrapper[] getMarkedLinedefs(bool mark = true)
    {
        ThrowIfDisposed("getMarkedLinedefs");
        return map.GetMarkedLinedefs(mark)
            .Where(linedef => !linedef.IsDisposed)
            .Select(linedef => new UdbScriptLinedefWrapper(linedef))
            .ToArray();
    }

    public UdbScriptSidedefWrapper[] getMarkedSidedefs(bool mark = true)
    {
        ThrowIfDisposed("getMarkedSidedefs");
        return map.GetMarkedSidedefs(mark)
            .Where(sidedef => !sidedef.IsDisposed)
            .Select(sidedef => new UdbScriptSidedefWrapper(sidedef))
            .ToArray();
    }

    public UdbScriptSectorWrapper[] getMarkedSectors(bool mark = true)
    {
        ThrowIfDisposed("getMarkedSectors");
        return map.GetMarkedSectors(mark)
            .Where(sector => !sector.IsDisposed)
            .Select(sector => new UdbScriptSectorWrapper(sector))
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
            .Select(vertex => new UdbScriptVertexWrapper(vertex))
            .ToArray();
    }

    public UdbScriptThingWrapper[] getSelectedThings(bool selected = true)
    {
        ThrowIfDisposed("getSelectedThings");
        return map.GetSelectedThings(selected)
            .Where(thing => !thing.IsDisposed)
            .Select(thing => new UdbScriptThingWrapper(thing))
            .ToArray();
    }

    public UdbScriptSectorWrapper[] getSelectedSectors(bool selected = true)
    {
        ThrowIfDisposed("getSelectedSectors");
        return map.GetSelectedSectors(selected)
            .Where(sector => !sector.IsDisposed)
            .Select(sector => new UdbScriptSectorWrapper(sector))
            .ToArray();
    }

    public UdbScriptLinedefWrapper[] getSelectedLinedefs(bool selected = true)
    {
        ThrowIfDisposed("getSelectedLinedefs");
        return map.GetSelectedLinedefs(selected)
            .Where(linedef => !linedef.IsDisposed)
            .Select(linedef => new UdbScriptLinedefWrapper(linedef))
            .ToArray();
    }

    public UdbScriptSidedefWrapper[] getSidedefsFromSelectedLinedefs(bool selected = true)
    {
        ThrowIfDisposed("getSidedefsFromSelectedLinedefs");
        return map.GetSidedefsFromSelectedLinedefs(selected)
            .Where(sidedef => !sidedef.IsDisposed)
            .Select(sidedef => new UdbScriptSidedefWrapper(sidedef))
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

    public UdbScriptVertexWrapper createVertex(object pos)
    {
        ThrowIfDisposed("createVertex");
        Vector2D point = ToVector2D(pos);
        return new UdbScriptVertexWrapper(map.AddVertex(point));
    }

    public UdbScriptThingWrapper createThing(object pos, int type = 0)
    {
        ThrowIfDisposed("createThing");
        if (type < 0)
            throw new InvalidOperationException("Thing type can not be negative.");

        Vector3D point = UdbScriptApiConversionModel.GetVector3DFromObject(pos);
        Thing thing = map.AddThing(new Vector2D(point.x, point.y), type);
        thing.Height = point.z;
        thing.DetermineSector(map);
        return new UdbScriptThingWrapper(thing);
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
