// ABOUTME: Verifies UDB-compatible rendering Vector2f and Vector2i behavior.
// ABOUTME: Locks down constructors, operators, equality, hashing, and Hermite interpolation.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class RenderingVector2Tests
{
    [Fact]
    public void Vector2fConstructorsMatchUdbFieldAssignment()
    {
        var uniform = new Vector2f(3.5f);
        var pair = new Vector2f(1.25f, -2.5f);

        Assert.Equal(3.5f, uniform.X);
        Assert.Equal(3.5f, uniform.Y);
        Assert.Equal(1.25f, pair.X);
        Assert.Equal(-2.5f, pair.Y);
    }

    [Fact]
    public void Vector2fOperatorsMatchUdbArithmetic()
    {
        var left = new Vector2f(7f, -3f);
        var right = new Vector2f(2f, 5f);

        Assert.Equal(new Vector2f(9f, 2f), left + right);
        Assert.Equal(new Vector2f(5f, -8f), left - right);
        Assert.Equal(new Vector2f(-7f, 3f), -left);
        Assert.True(new Vector2f(1f, 2f) == new Vector2f(1f, 2f));
        Assert.True(new Vector2f(1f, 2f) != new Vector2f(1f, 3f));
    }

    [Fact]
    public void Vector2fHermiteMatchesUdbFormula()
    {
        var value1 = new Vector2f(1f, 2f);
        var tangent1 = new Vector2f(0.5f, 1.5f);
        var value2 = new Vector2f(9f, -4f);
        var tangent2 = new Vector2f(2f, -3f);

        Vector2f result = Vector2f.Hermite(value1, tangent1, value2, tangent2, 0.25f);

        Assert.Equal(2.2265625f, result.X, precision: 6);
        Assert.Equal(1.4140625f, result.Y, precision: 6);
    }

    [Fact]
    public void Vector2fEqualityAndHashMatchUdbSemantics()
    {
        var vector = new Vector2f(1f, 2f);

        Assert.True(vector.Equals(new Vector2f(1f, 2f)));
        Assert.False(vector.Equals(new Vector2f(1f, 3f)));
        Assert.False(vector.Equals("not a vector"));
        Assert.Equal(1f.GetHashCode() + 2f.GetHashCode(), vector.GetHashCode());
    }

    [Fact]
    public void Vector2iConstructorsMatchUdbFieldAssignment()
    {
        var uniform = new Vector2i(3);
        var pair = new Vector2i(1, -2);

        Assert.Equal(3, uniform.X);
        Assert.Equal(3, uniform.Y);
        Assert.Equal(1, pair.X);
        Assert.Equal(-2, pair.Y);
    }

    [Fact]
    public void Vector2iOperatorsMatchUdbArithmetic()
    {
        var left = new Vector2i(7, -3);
        var right = new Vector2i(2, 5);

        Assert.Equal(new Vector2i(9, 2), left + right);
        Assert.Equal(new Vector2i(5, -8), left - right);
        Assert.Equal(new Vector2i(-7, 3), -left);
        Assert.True(new Vector2i(1, 2) == new Vector2i(1, 2));
        Assert.True(new Vector2i(1, 2) != new Vector2i(1, 3));
    }

    [Fact]
    public void Vector2iEqualityAndHashMatchUdbSemantics()
    {
        var vector = new Vector2i(1, 2);

        Assert.True(vector.Equals(new Vector2i(1, 2)));
        Assert.False(vector.Equals(new Vector2i(1, 3)));
        Assert.False(vector.Equals("not a vector"));
        Assert.Equal(1.GetHashCode() + 2.GetHashCode(), vector.GetHashCode());
    }
}
