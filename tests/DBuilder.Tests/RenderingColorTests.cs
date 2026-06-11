// ABOUTME: Verifies UDB-compatible Color3 and Color4 rendering color behavior.
// ABOUTME: Locks down constructors, ARGB packing, vector conversion, arithmetic, equality, and hashing.

using System.Drawing;
using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class RenderingColorTests
{
    [Fact]
    public void Color3ConstructorsMatchUdbFieldAssignment()
    {
        var floats = new Color3(0.1f, 0.2f, 0.3f);
        var vector = new Color3(new Vector3f(0.4f, 0.5f, 0.6f));
        var drawing = new Color3(Color.FromArgb(128, 64, 32, 16));

        Assert.Equal(new Color3(0.1f, 0.2f, 0.3f), floats);
        Assert.Equal(new Color3(0.4f, 0.5f, 0.6f), vector);
        Assert.Equal(64f / 255.0f, drawing.Red, precision: 6);
        Assert.Equal(32f / 255.0f, drawing.Green, precision: 6);
        Assert.Equal(16f / 255.0f, drawing.Blue, precision: 6);
    }

    [Fact]
    public void Color3EqualityAndHashMatchUdbSemantics()
    {
        var color = new Color3(0.1f, 0.2f, 0.3f);

        Assert.True(color.Equals(new Color3(0.1f, 0.2f, 0.3f)));
        Assert.False(color.Equals(new Color3(0.1f, 0.2f, 0.4f)));
        Assert.False(color.Equals("not a color"));
        Assert.True(new Color3(0.1f, 0.2f, 0.3f) == new Color3(0.1f, 0.2f, 0.3f));
        Assert.True(new Color3(0.1f, 0.2f, 0.3f) != new Color3(0.1f, 0.2f, 0.4f));
        Assert.Equal(0.1f.GetHashCode() + 0.2f.GetHashCode() + 0.3f.GetHashCode(), color.GetHashCode());
    }

    [Fact]
    public void Color4ConstructorsMatchUdbFieldAssignment()
    {
        var argb = new Color4(unchecked((int)0x80402010u));
        var floats = new Color4(0.1f, 0.2f, 0.3f, 0.4f);
        var vector = new Color4(new Vector4f(0.5f, 0.6f, 0.7f, 0.8f));
        var drawing = new Color4(Color.FromArgb(128, 64, 32, 16));

        Assert.Equal(64f / 255.0f, argb.Red, precision: 6);
        Assert.Equal(32f / 255.0f, argb.Green, precision: 6);
        Assert.Equal(16f / 255.0f, argb.Blue, precision: 6);
        Assert.Equal(128f / 255.0f, argb.Alpha, precision: 6);
        Assert.Equal(new Color4(0.1f, 0.2f, 0.3f, 0.4f), floats);
        Assert.Equal(new Color4(0.5f, 0.6f, 0.7f, 0.8f), vector);
        Assert.Equal(64f / 255.0f, drawing.Red, precision: 6);
        Assert.Equal(32f / 255.0f, drawing.Green, precision: 6);
        Assert.Equal(16f / 255.0f, drawing.Blue, precision: 6);
        Assert.Equal(128f / 255.0f, drawing.Alpha, precision: 6);
    }

    [Fact]
    public void Color4ArgbConversionClampsAndTruncatesLikeUdb()
    {
        var color = new Color4(1.2f, 0.5f, -0.1f, 0.25f);

        Assert.Equal(unchecked((int)0x3FFF7F00u), color.ToArgb());
        Assert.Equal(Color.FromArgb(unchecked((int)0x3FFF7F00u)), color.ToColor());
        Assert.Equal(new Vector4f(1.2f, 0.5f, -0.1f, 0.25f), color.ToVector());
    }

    [Fact]
    public void Color4OperatorsMatchUdbArithmetic()
    {
        var left = new Color4(0.7f, -0.3f, 0.4f, 1.0f);
        var right = new Color4(0.2f, 0.5f, -0.6f, 0.8f);

        AssertColor(left + right, 0.9f, 0.2f, -0.2f, 1.8f);
        AssertColor(left - right, 0.5f, -0.8f, 1.0f, 0.2f);
        AssertColor(-left, -0.7f, 0.3f, -0.4f, -1.0f);
    }

    [Fact]
    public void Color4EqualityAndHashMatchUdbSemantics()
    {
        var color = new Color4(0.1f, 0.2f, 0.3f, 0.4f);

        Assert.True(color.Equals(new Color4(0.1f, 0.2f, 0.3f, 0.4f)));
        Assert.False(color.Equals(new Color4(0.1f, 0.2f, 0.3f, 0.5f)));
        Assert.False(color.Equals("not a color"));
        Assert.True(new Color4(0.1f, 0.2f, 0.3f, 0.4f) == new Color4(0.1f, 0.2f, 0.3f, 0.4f));
        Assert.True(new Color4(0.1f, 0.2f, 0.3f, 0.4f) != new Color4(0.1f, 0.2f, 0.3f, 0.5f));
        Assert.Equal(
            0.1f.GetHashCode() + 0.2f.GetHashCode() + 0.3f.GetHashCode() + 0.4f.GetHashCode(),
            color.GetHashCode());
    }

    private static void AssertColor(Color4 color, float red, float green, float blue, float alpha)
    {
        Assert.Equal(red, color.Red, precision: 6);
        Assert.Equal(green, color.Green, precision: 6);
        Assert.Equal(blue, color.Blue, precision: 6);
        Assert.Equal(alpha, color.Alpha, precision: 6);
    }
}
