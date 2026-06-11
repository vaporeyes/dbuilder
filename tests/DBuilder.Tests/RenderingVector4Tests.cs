// ABOUTME: Verifies UDB-compatible rendering Vector4f and Vector4i behavior.
// ABOUTME: Locks down constructors, operators, equality, hashing, dot, length, and normalization.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class RenderingVector4Tests
{
    [Fact]
    public void Vector4fConstructorsMatchUdbFieldAssignment()
    {
        var uniform = new Vector4f(3.5f);
        var fromVector2 = new Vector4f(new Vector2f(1.25f, -2.5f), 7.5f, -8.5f);
        var values = new Vector4f(1f, 2f, 3f, 4f);

        Assert.Equal(new Vector4f(3.5f, 3.5f, 3.5f, 3.5f), uniform);
        Assert.Equal(new Vector4f(1.25f, -2.5f, 7.5f, -8.5f), fromVector2);
        Assert.Equal(1f, values.X);
        Assert.Equal(2f, values.Y);
        Assert.Equal(3f, values.Z);
        Assert.Equal(4f, values.W);
    }

    [Fact]
    public void Vector4fOperatorsMatchUdbArithmetic()
    {
        var left = new Vector4f(7f, -3f, 4f, 10f);
        var right = new Vector4f(2f, 5f, -6f, 8f);

        Assert.Equal(new Vector4f(14f, -6f, 8f, 20f), left * 2f);
        Assert.Equal(new Vector4f(14f, -6f, 8f, 20f), 2f * left);
        Assert.Equal(new Vector4f(9f, 2f, -2f, 18f), left + right);
        Assert.Equal(new Vector4f(5f, -8f, 10f, 2f), left - right);
        Assert.Equal(new Vector4f(-7f, 3f, -4f, -10f), -left);
        Assert.True(new Vector4f(1f, 2f, 3f, 4f) == new Vector4f(1f, 2f, 3f, 4f));
        Assert.True(new Vector4f(1f, 2f, 3f, 4f) != new Vector4f(1f, 2f, 3f, 5f));
    }

    [Fact]
    public void Vector4fDotLengthAndNormalizeMatchUdbSemantics()
    {
        var vector = new Vector4f(2f, 3f, 6f, 1f);
        var other = new Vector4f(5f, 7f, 11f, 13f);

        Assert.Equal(110f, Vector4f.Dot(vector, other));
        Assert.Equal((float)Math.Sqrt(50f), vector.Length(), precision: 6);

        Vector4f normalized = Vector4f.Normalize(vector);
        Assert.Equal(2f / (float)Math.Sqrt(50f), normalized.X, precision: 6);
        Assert.Equal(3f / (float)Math.Sqrt(50f), normalized.Y, precision: 6);
        Assert.Equal(6f / (float)Math.Sqrt(50f), normalized.Z, precision: 6);
        Assert.Equal(1f / (float)Math.Sqrt(50f), normalized.W, precision: 6);

        var zero = new Vector4f();
        zero.Normalize();
        Assert.Equal(new Vector4f(), zero);
    }

    [Fact]
    public void Vector4fEqualityAndHashMatchUdbSemantics()
    {
        var vector = new Vector4f(1f, 2f, 3f, 4f);

        Assert.True(vector.Equals(new Vector4f(1f, 2f, 3f, 4f)));
        Assert.False(vector.Equals(new Vector4f(1f, 2f, 3f, 5f)));
        Assert.False(vector.Equals("not a vector"));
        Assert.Equal(
            1f.GetHashCode() + 2f.GetHashCode() + 3f.GetHashCode() + 4f.GetHashCode(),
            vector.GetHashCode());
    }

    [Fact]
    public void Vector4iConstructorsMatchUdbFieldAssignment()
    {
        var uniform = new Vector4i(3);
        var fromVector2 = new Vector4i(new Vector2i(1, -2), 7, -8);
        var values = new Vector4i(1, 2, 3, 4);

        Assert.Equal(new Vector4i(3, 3, 3, 3), uniform);
        Assert.Equal(new Vector4i(1, -2, 7, -8), fromVector2);
        Assert.Equal(1, values.X);
        Assert.Equal(2, values.Y);
        Assert.Equal(3, values.Z);
        Assert.Equal(4, values.W);
    }

    [Fact]
    public void Vector4iOperatorsMatchUdbArithmetic()
    {
        var left = new Vector4i(7, -3, 4, 10);
        var right = new Vector4i(2, 5, -6, 8);

        Assert.Equal(new Vector4i(9, 2, -2, 18), left + right);
        Assert.Equal(new Vector4i(5, -8, 10, 2), left - right);
        Assert.Equal(new Vector4i(-7, 3, -4, -10), -left);
        Assert.True(new Vector4i(1, 2, 3, 4) == new Vector4i(1, 2, 3, 4));
        Assert.True(new Vector4i(1, 2, 3, 4) != new Vector4i(1, 2, 3, 5));
    }

    [Fact]
    public void Vector4iEqualityAndHashMatchUdbSemantics()
    {
        var vector = new Vector4i(1, 2, 3, 4);

        Assert.True(vector.Equals(new Vector4i(1, 2, 3, 4)));
        Assert.False(vector.Equals(new Vector4i(1, 2, 3, 5)));
        Assert.False(vector.Equals("not a vector"));
        Assert.Equal(
            1.GetHashCode() + 2.GetHashCode() + 3.GetHashCode() + 4.GetHashCode(),
            vector.GetHashCode());
    }
}
