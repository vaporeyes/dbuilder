// ABOUTME: Represents UDB-style ARGB pixel colors used by rendering models.
// ABOUTME: Keeps channel packing and helper math independent from UI frameworks.
using System.Drawing;

namespace DBuilder.Rendering;

public readonly record struct PixelColor(byte A, byte R, byte G, byte B)
{
    public const float ByteToFloat = 0.00392156862745098f;
    public const int IntBlack = unchecked((int)0xFF000000);
    public const int IntWhite = unchecked((int)0xFFFFFFFF);
    public const int IntWhiteNoAlpha = 0x00FFFFFF;
    public const float BYTE_TO_FLOAT = ByteToFloat;
    public const int INT_BLACK = IntBlack;
    public const int INT_WHITE = IntWhite;
    public const int INT_WHITE_NO_ALPHA = IntWhiteNoAlpha;

    public static PixelColor Transparent { get; } = new(0, 0, 0, 0);

    public PixelColor(PixelColor color, byte alpha)
        : this(alpha, color.R, color.G, color.B)
    {
    }

    public byte a => A;
    public byte r => R;
    public byte g => G;
    public byte b => B;

    public static PixelColor FromArgb(int argb)
        => new(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));

    public static PixelColor FromColor(Color color)
        => new(color.A, color.R, color.G, color.B);

    public static PixelColor FromInt(int color)
        => FromColor(Color.FromArgb(color));

    public int ToArgb()
        => unchecked((int)((uint)A << 24 | (uint)R << 16 | (uint)G << 8 | B));

    public int ToInt()
        => ToArgb();

    public Color ToColor()
        => Color.FromArgb(ToArgb());

    public Color4 ToColorValue()
        => new(R * ByteToFloat, G * ByteToFloat, B * ByteToFloat, A * ByteToFloat);

    public Color4 ToColorValue(float withalpha)
        => new(R * ByteToFloat, G * ByteToFloat, B * ByteToFloat, withalpha);

    public PixelColor WithAlpha(byte alpha)
        => this with { A = alpha };

    public PixelColor ApplyAlpha()
        => new(255, (byte)(R * A / 255), (byte)(G * A / 255), (byte)(B * A / 255));

    public PixelColor Inverse()
        => new((byte)(255 - A), (byte)(255 - R), (byte)(255 - G), (byte)(255 - B));

    public PixelColor InverseKeepAlpha()
        => new(A, (byte)(255 - R), (byte)(255 - G), (byte)(255 - B));

    public int ToInversedColorRef()
        => R + (B << 16) + (G << 8);

    public static PixelColor Add(PixelColor left, PixelColor right)
        => new(
            (byte)Math.Min(left.A + right.A, 255),
            (byte)Math.Min(left.R + right.R, 255),
            (byte)Math.Min(left.G + right.G, 255),
            (byte)Math.Min(left.B + right.B, 255));

    public static PixelColor Subtract(PixelColor left, PixelColor right)
        => new(
            Math.Max(left.A, right.A),
            (byte)Math.Max(left.R - right.R, 0),
            (byte)Math.Max(left.G - right.G, 0),
            (byte)Math.Max(left.B - right.B, 0));

    public static PixelColor Modulate(PixelColor left, PixelColor right)
        => new(
            (byte)((left.A * ByteToFloat * right.A * ByteToFloat) * 255.0f),
            (byte)((left.R * ByteToFloat * right.R * ByteToFloat) * 255.0f),
            (byte)((left.G * ByteToFloat * right.G * ByteToFloat) * 255.0f),
            (byte)((left.B * ByteToFloat * right.B * ByteToFloat) * 255.0f));

    public static PixelColor Blend(PixelColor foreground, PixelColor background)
    {
        float alpha = foreground.A * ByteToFloat;
        return new PixelColor(
            (byte)(foreground.A * (1f - alpha) + alpha),
            (byte)(foreground.R * (1f - alpha) + background.R * alpha),
            (byte)(foreground.G * (1f - alpha) + background.G * alpha),
            (byte)(foreground.B * (1f - alpha) + background.B * alpha));
    }

    public override string ToString()
        => $"[A={A}, R={R}, G={G}, B={B}]";
}
