// ABOUTME: Provides UDB-compatible four-channel floating-point rendering color values.
// ABOUTME: Preserves ARGB packing, vector conversion, arithmetic, equality, and hash semantics.

using System.Drawing;

namespace DBuilder.Rendering;

public struct Color4
{
    public Color4(int argb)
    {
        uint value = (uint)argb;
        Alpha = ((value >> 24) & 0xff) / 255.0f;
        Red = ((value >> 16) & 0xff) / 255.0f;
        Green = ((value >> 8) & 0xff) / 255.0f;
        Blue = (value & 0xff) / 255.0f;
    }

    public Color4(float r, float g, float b, float a)
    {
        Red = r;
        Green = g;
        Blue = b;
        Alpha = a;
    }

    public Color4(Vector4f color)
    {
        Red = color.X;
        Green = color.Y;
        Blue = color.Z;
        Alpha = color.W;
    }

    public Color4(Color color)
    {
        Red = color.R / 255.0f;
        Green = color.G / 255.0f;
        Blue = color.B / 255.0f;
        Alpha = color.A / 255.0f;
    }

    public float Red;
    public float Green;
    public float Blue;
    public float Alpha;

    public readonly int ToArgb()
    {
        uint r = (uint)Math.Max(Math.Min(Red * 255.0f, 255.0f), 0.0f);
        uint g = (uint)Math.Max(Math.Min(Green * 255.0f, 255.0f), 0.0f);
        uint b = (uint)Math.Max(Math.Min(Blue * 255.0f, 255.0f), 0.0f);
        uint a = (uint)Math.Max(Math.Min(Alpha * 255.0f, 255.0f), 0.0f);
        return (int)((a << 24) | (r << 16) | (g << 8) | b);
    }

    public readonly Color ToColor()
        => Color.FromArgb(ToArgb());

    public readonly Vector4f ToVector()
        => new(Red, Green, Blue, Alpha);

    public override readonly bool Equals(object? obj)
        => obj is Color4 color && this == color;

    public override readonly int GetHashCode()
        => Red.GetHashCode() + Green.GetHashCode() + Blue.GetHashCode() + Alpha.GetHashCode();

    public static Color4 operator +(Color4 left, Color4 right)
        => new(left.Red + right.Red, left.Green + right.Green, left.Blue + right.Blue, left.Alpha + right.Alpha);

    public static Color4 operator -(Color4 left, Color4 right)
        => new(left.Red - right.Red, left.Green - right.Green, left.Blue - right.Blue, left.Alpha - right.Alpha);

    public static Color4 operator -(Color4 color)
        => new(-color.Red, -color.Green, -color.Blue, -color.Alpha);

    public static bool operator ==(Color4 left, Color4 right)
        => left.Red == right.Red && left.Green == right.Green && left.Blue == right.Blue && left.Alpha == right.Alpha;

    public static bool operator !=(Color4 left, Color4 right)
        => left.Red != right.Red || left.Green != right.Green || left.Blue != right.Blue || left.Alpha != right.Alpha;
}
