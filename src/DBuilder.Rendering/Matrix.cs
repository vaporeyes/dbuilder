// ABOUTME: Provides UDB-compatible 4x4 rendering matrix math for vector transforms.
// ABOUTME: Preserves field layout, factory methods, multiplication, projection, look-at, equality, and hashing.

using System.Runtime.InteropServices;

namespace DBuilder.Rendering;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Matrix
{
    public float M11, M12, M13, M14;
    public float M21, M22, M23, M24;
    public float M31, M32, M33, M34;
    public float M41, M42, M43, M44;

    public static Matrix Null
    {
        get
        {
            Matrix matrix = new();
            matrix.M11 = 0.0f;
            matrix.M12 = 0.0f;
            matrix.M13 = 0.0f;
            matrix.M14 = 0.0f;
            matrix.M21 = 0.0f;
            matrix.M22 = 0.0f;
            matrix.M23 = 0.0f;
            matrix.M24 = 0.0f;
            matrix.M31 = 0.0f;
            matrix.M32 = 0.0f;
            matrix.M33 = 0.0f;
            matrix.M34 = 0.0f;
            matrix.M41 = 0.0f;
            matrix.M42 = 0.0f;
            matrix.M43 = 0.0f;
            matrix.M44 = 0.0f;
            return matrix;
        }
    }

    public static Matrix Identity
    {
        get
        {
            Matrix matrix = Null;
            matrix.M11 = 1.0f;
            matrix.M22 = 1.0f;
            matrix.M33 = 1.0f;
            matrix.M44 = 1.0f;
            return matrix;
        }
    }

    public static Matrix Translation(Vector3f vector)
    {
        Matrix matrix = Identity;
        matrix.M41 = vector.X;
        matrix.M42 = vector.Y;
        matrix.M43 = vector.Z;
        return matrix;
    }

    public static Matrix Translation(float x, float y, float z)
    {
        Matrix matrix = Identity;
        matrix.M41 = x;
        matrix.M42 = y;
        matrix.M43 = z;
        return matrix;
    }

    public static Matrix RotationX(float angle)
    {
        float cos = (float)Math.Cos(angle);
        float sin = (float)Math.Sin(angle);

        Matrix result = Null;
        result.M11 = 1.0f;
        result.M22 = cos;
        result.M23 = sin;
        result.M32 = -sin;
        result.M33 = cos;
        result.M44 = 1.0f;
        return result;
    }

    public static Matrix RotationY(float angle)
    {
        float cos = (float)Math.Cos(angle);
        float sin = (float)Math.Sin(angle);

        Matrix result = Null;
        result.M11 = cos;
        result.M13 = -sin;
        result.M22 = 1.0f;
        result.M31 = sin;
        result.M33 = cos;
        result.M44 = 1.0f;
        return result;
    }

    public static Matrix RotationZ(float angle)
    {
        float cos = (float)Math.Cos(angle);
        float sin = (float)Math.Sin(angle);

        Matrix result = Null;
        result.M11 = cos;
        result.M12 = sin;
        result.M21 = -sin;
        result.M22 = cos;
        result.M33 = 1.0f;
        result.M44 = 1.0f;
        return result;
    }

    public static Matrix Scaling(float x, float y, float z)
    {
        Matrix result = Null;
        result.M11 = x;
        result.M22 = y;
        result.M33 = z;
        result.M44 = 1.0f;
        return result;
    }

    public static Matrix Scaling(Vector3f vector)
    {
        Matrix result = Null;
        result.M11 = vector.X;
        result.M22 = vector.Y;
        result.M33 = vector.Z;
        result.M44 = 1.0f;
        return result;
    }

    public static Matrix Multiply(Matrix left, Matrix right)
    {
        Matrix result = new();
        result.M11 = (left.M11 * right.M11) + (left.M12 * right.M21) + (left.M13 * right.M31) + (left.M14 * right.M41);
        result.M12 = (left.M11 * right.M12) + (left.M12 * right.M22) + (left.M13 * right.M32) + (left.M14 * right.M42);
        result.M13 = (left.M11 * right.M13) + (left.M12 * right.M23) + (left.M13 * right.M33) + (left.M14 * right.M43);
        result.M14 = (left.M11 * right.M14) + (left.M12 * right.M24) + (left.M13 * right.M34) + (left.M14 * right.M44);
        result.M21 = (left.M21 * right.M11) + (left.M22 * right.M21) + (left.M23 * right.M31) + (left.M24 * right.M41);
        result.M22 = (left.M21 * right.M12) + (left.M22 * right.M22) + (left.M23 * right.M32) + (left.M24 * right.M42);
        result.M23 = (left.M21 * right.M13) + (left.M22 * right.M23) + (left.M23 * right.M33) + (left.M24 * right.M43);
        result.M24 = (left.M21 * right.M14) + (left.M22 * right.M24) + (left.M23 * right.M34) + (left.M24 * right.M44);
        result.M31 = (left.M31 * right.M11) + (left.M32 * right.M21) + (left.M33 * right.M31) + (left.M34 * right.M41);
        result.M32 = (left.M31 * right.M12) + (left.M32 * right.M22) + (left.M33 * right.M32) + (left.M34 * right.M42);
        result.M33 = (left.M31 * right.M13) + (left.M32 * right.M23) + (left.M33 * right.M33) + (left.M34 * right.M43);
        result.M34 = (left.M31 * right.M14) + (left.M32 * right.M24) + (left.M33 * right.M34) + (left.M34 * right.M44);
        result.M41 = (left.M41 * right.M11) + (left.M42 * right.M21) + (left.M43 * right.M31) + (left.M44 * right.M41);
        result.M42 = (left.M41 * right.M12) + (left.M42 * right.M22) + (left.M43 * right.M32) + (left.M44 * right.M42);
        result.M43 = (left.M41 * right.M13) + (left.M42 * right.M23) + (left.M43 * right.M33) + (left.M44 * right.M43);
        result.M44 = (left.M41 * right.M14) + (left.M42 * right.M24) + (left.M43 * right.M34) + (left.M44 * right.M44);
        return result;
    }

    public static Matrix LookAt(Vector3f eye, Vector3f target, Vector3f up)
    {
        Vector3f zaxis = Vector3f.Normalize(target - eye);
        Vector3f xaxis = Vector3f.Normalize(Vector3f.Cross(up, zaxis));
        Vector3f yaxis = Vector3f.Cross(zaxis, xaxis);

        Matrix result = Null;
        result.M11 = -xaxis.X;
        result.M12 = yaxis.X;
        result.M13 = -zaxis.X;
        result.M21 = -xaxis.Y;
        result.M22 = yaxis.Y;
        result.M23 = -zaxis.Y;
        result.M31 = -xaxis.Z;
        result.M32 = yaxis.Z;
        result.M33 = -zaxis.Z;
        result.M44 = 1.0f;
        return Translation(-eye) * result;
    }

    public static Matrix PerspectiveFov(float fov, float aspect, float znear, float zfar)
    {
        float f = (float)(1.0 / Math.Tan(fov * 0.5f));

        Matrix result = Null;
        result.M11 = f / aspect;
        result.M22 = f;
        result.M33 = (zfar + znear) / (znear - zfar);
        result.M34 = 2.0f * zfar * znear / (znear - zfar);
        result.M43 = -1.0f;
        return result;
    }

    public override readonly bool Equals(object? obj)
        => obj is Matrix matrix && this == matrix;

    public override readonly int GetHashCode()
        => M11.GetHashCode() + M12.GetHashCode() + M13.GetHashCode() + M14.GetHashCode()
            + M21.GetHashCode() + M22.GetHashCode() + M23.GetHashCode() + M24.GetHashCode()
            + M31.GetHashCode() + M32.GetHashCode() + M33.GetHashCode() + M34.GetHashCode()
            + M41.GetHashCode() + M42.GetHashCode() + M43.GetHashCode() + M44.GetHashCode();

    public static Matrix operator *(Matrix left, Matrix right)
        => Multiply(left, right);

    public static bool operator ==(Matrix left, Matrix right)
        => left.M11 == right.M11 && left.M12 == right.M12 && left.M13 == right.M13 && left.M14 == right.M14
            && left.M21 == right.M21 && left.M22 == right.M22 && left.M23 == right.M23 && left.M24 == right.M24
            && left.M31 == right.M31 && left.M32 == right.M32 && left.M33 == right.M33 && left.M34 == right.M34
            && left.M41 == right.M41 && left.M42 == right.M42 && left.M43 == right.M43 && left.M44 == right.M44;

    public static bool operator !=(Matrix left, Matrix right)
        => left.M11 != right.M11 || left.M12 != right.M12 || left.M13 != right.M13 || left.M14 != right.M14
            || left.M21 != right.M21 || left.M22 != right.M22 || left.M23 != right.M23 || left.M24 != right.M24
            || left.M31 != right.M31 || left.M32 != right.M32 || left.M33 != right.M33 || left.M34 != right.M34
            || left.M41 != right.M41 || left.M42 != right.M42 || left.M43 != right.M43 || left.M44 != right.M44;
}
