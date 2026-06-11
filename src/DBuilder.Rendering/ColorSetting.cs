// ABOUTME: Provides UDB-compatible named color settings for rendering preferences.
// ABOUTME: Wraps PixelColor with System.Drawing.Color conversion and alpha helpers.

using System.Drawing;

namespace DBuilder.Rendering;

public sealed class ColorSetting : IEquatable<ColorSetting>
{
    private readonly string _name;
    private PixelColor _color;

    public ColorSetting(string name, PixelColor color)
    {
        _name = name;
        _color = color;
        GC.SuppressFinalize(this);
    }

    public Color Color
    {
        get => Color.FromArgb(_color.ToArgb());
        set => _color = PixelColor.FromArgb(value.ToArgb());
    }

    public PixelColor PixelColor
    {
        get => _color;
        set => _color = value;
    }

    public string Name => _name;

    public PixelColor WithAlpha(byte alpha)
        => _color.WithAlpha(alpha);

    public bool Equals(ColorSetting? other)
        => other is not null && _name == other._name;

    public override bool Equals(object? obj)
        => obj is ColorSetting other && Equals(other);

    public override int GetHashCode()
        => _name.GetHashCode();

    public static implicit operator PixelColor(ColorSetting setting)
        => setting._color;

    public static implicit operator Color(ColorSetting setting)
        => Color.FromArgb(setting._color.ToArgb());
}
