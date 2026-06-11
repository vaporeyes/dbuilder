// ABOUTME: Provides UDB-compatible three-channel floating-point rendering color values.
// ABOUTME: Preserves constructors, public fields, equality, and hash semantics from Color3.cs.

using System.Drawing;

namespace DBuilder.Rendering;

public struct Color3
{
    public Color3(float r, float g, float b)
    {
        Red = r;
        Green = g;
        Blue = b;
    }

    public Color3(Vector3f color)
    {
        Red = color.X;
        Green = color.Y;
        Blue = color.Z;
    }

    public Color3(Color color)
    {
        Red = color.R / 255.0f;
        Green = color.G / 255.0f;
        Blue = color.B / 255.0f;
    }

    public float Red;
    public float Green;
    public float Blue;

    public override readonly bool Equals(object? obj)
        => obj is Color3 color && this == color;

    public override readonly int GetHashCode()
        => Red.GetHashCode() + Green.GetHashCode() + Blue.GetHashCode();

    public static bool operator ==(Color3 left, Color3 right)
        => left.Red == right.Red && left.Green == right.Green && left.Blue == right.Blue;

    public static bool operator !=(Color3 left, Color3 right)
        => left.Red != right.Red || left.Green != right.Green || left.Blue != right.Blue;
}
