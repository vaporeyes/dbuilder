// ABOUTME: Provides UDB-compatible 4D float and integer vector structs for rendering code.
// ABOUTME: Preserves constructors, operators, equality, hash, dot, length, and normalization semantics.

namespace DBuilder.Rendering;

public struct Vector4f
{
    public Vector4f(float value)
    {
        X = value;
        Y = value;
        Z = value;
        W = value;
    }

    public Vector4f(Vector2f xy, float z, float w)
    {
        X = xy.X;
        Y = xy.Y;
        Z = z;
        W = w;
    }

    public Vector4f(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public float X;
    public float Y;
    public float Z;
    public float W;

    public static float Dot(Vector4f a, Vector4f b)
        => a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

    public readonly float Length()
        => (float)Math.Sqrt(Dot(this, this));

    public static Vector4f Normalize(Vector4f vector)
    {
        vector.Normalize();
        return vector;
    }

    public void Normalize()
    {
        float len = Length();
        if (len > 0.0f)
        {
            X /= len;
            Y /= len;
            Z /= len;
            W /= len;
        }
    }

    public override readonly bool Equals(object? obj)
        => obj is Vector4f vector && this == vector;

    public override readonly int GetHashCode()
        => X.GetHashCode() + Y.GetHashCode() + Z.GetHashCode() + W.GetHashCode();

    public static Vector4f operator *(Vector4f vector, float scalar)
        => new(vector.X * scalar, vector.Y * scalar, vector.Z * scalar, vector.W * scalar);

    public static Vector4f operator *(float scalar, Vector4f vector)
        => new(vector.X * scalar, vector.Y * scalar, vector.Z * scalar, vector.W * scalar);

    public static Vector4f operator +(Vector4f left, Vector4f right)
        => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z, left.W + right.W);

    public static Vector4f operator -(Vector4f left, Vector4f right)
        => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z, left.W - right.W);

    public static Vector4f operator -(Vector4f vector)
        => new(-vector.X, -vector.Y, -vector.Z, -vector.W);

    public static bool operator ==(Vector4f left, Vector4f right)
        => left.X == right.X && left.Y == right.Y && left.Z == right.Z && left.W == right.W;

    public static bool operator !=(Vector4f left, Vector4f right)
        => left.X != right.X || left.Y != right.Y || left.Z != right.Z || left.W != right.W;
}

public struct Vector4i
{
    public Vector4i(int value)
    {
        X = value;
        Y = value;
        Z = value;
        W = value;
    }

    public Vector4i(Vector2i xy, int z, int w)
    {
        X = xy.X;
        Y = xy.Y;
        Z = z;
        W = w;
    }

    public Vector4i(int x, int y, int z, int w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public int X;
    public int Y;
    public int Z;
    public int W;

    public override readonly bool Equals(object? obj)
        => obj is Vector4i vector && this == vector;

    public override readonly int GetHashCode()
        => X.GetHashCode() + Y.GetHashCode() + Z.GetHashCode() + W.GetHashCode();

    public static Vector4i operator +(Vector4i left, Vector4i right)
        => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z, left.W + right.W);

    public static Vector4i operator -(Vector4i left, Vector4i right)
        => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z, left.W - right.W);

    public static Vector4i operator -(Vector4i vector)
        => new(-vector.X, -vector.Y, -vector.Z, -vector.W);

    public static bool operator ==(Vector4i left, Vector4i right)
        => left.X == right.X && left.Y == right.Y && left.Z == right.Z && left.W == right.W;

    public static bool operator !=(Vector4i left, Vector4i right)
        => left.X != right.X || left.Y != right.Y || left.Z != right.Z || left.W != right.W;
}
