// ABOUTME: Provides UDB-compatible 2D float and integer vector structs for rendering code.
// ABOUTME: Preserves constructors, operators, equality, hash, and Hermite interpolation semantics.

namespace DBuilder.Rendering;

public struct Vector2f
{
    public Vector2f(float value)
    {
        X = value;
        Y = value;
    }

    public Vector2f(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X;
    public float Y;

    public static Vector2f Hermite(Vector2f value1, Vector2f tangent1, Vector2f value2, Vector2f tangent2, float amount)
    {
        float squared = amount * amount;
        float cubed = amount * squared;
        float part1 = ((2.0f * cubed) - (3.0f * squared)) + 1.0f;
        float part2 = (-2.0f * cubed) + (3.0f * squared);
        float part3 = (cubed - (2.0f * squared)) + amount;
        float part4 = cubed - squared;

        return new Vector2f(
            (((value1.X * part1) + (value2.X * part2)) + (tangent1.X * part3)) + (tangent2.X * part4),
            (((value1.Y * part1) + (value2.Y * part2)) + (tangent1.Y * part3)) + (tangent2.Y * part4));
    }

    public override readonly bool Equals(object? obj)
        => obj is Vector2f vector && this == vector;

    public override readonly int GetHashCode()
        => X.GetHashCode() + Y.GetHashCode();

    public static Vector2f operator +(Vector2f left, Vector2f right)
        => new(left.X + right.X, left.Y + right.Y);

    public static Vector2f operator -(Vector2f left, Vector2f right)
        => new(left.X - right.X, left.Y - right.Y);

    public static Vector2f operator -(Vector2f vector)
        => new(-vector.X, -vector.Y);

    public static bool operator ==(Vector2f left, Vector2f right)
        => left.X == right.X && left.Y == right.Y;

    public static bool operator !=(Vector2f left, Vector2f right)
        => left.X != right.X || left.Y != right.Y;
}

public struct Vector2i
{
    public Vector2i(int value)
    {
        X = value;
        Y = value;
    }

    public Vector2i(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X;
    public int Y;

    public override readonly bool Equals(object? obj)
        => obj is Vector2i vector && this == vector;

    public override readonly int GetHashCode()
        => X.GetHashCode() + Y.GetHashCode();

    public static Vector2i operator +(Vector2i left, Vector2i right)
        => new(left.X + right.X, left.Y + right.Y);

    public static Vector2i operator -(Vector2i left, Vector2i right)
        => new(left.X - right.X, left.Y - right.Y);

    public static Vector2i operator -(Vector2i vector)
        => new(-vector.X, -vector.Y);

    public static bool operator ==(Vector2i left, Vector2i right)
        => left.X == right.X && left.Y == right.Y;

    public static bool operator !=(Vector2i left, Vector2i right)
        => left.X != right.X || left.Y != right.Y;
}
