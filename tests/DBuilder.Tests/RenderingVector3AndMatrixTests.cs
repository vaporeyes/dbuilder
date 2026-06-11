// ABOUTME: Verifies UDB-compatible rendering Vector3f, Vector3i, and Matrix behavior.
// ABOUTME: Locks down constructors, transforms, Hermite, dot, cross, normalization, matrix factories, and multiplication.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class RenderingVector3AndMatrixTests
{
    [Fact]
    public void Vector3fConstructorsMatchUdbFieldAssignment()
    {
        var uniform = new Vector3f(3.5f);
        var fromVector2 = new Vector3f(new Vector2f(1.25f, -2.5f), 7.5f);
        var values = new Vector3f(1f, 2f, 3f);

        Assert.Equal(new Vector3f(3.5f, 3.5f, 3.5f), uniform);
        Assert.Equal(new Vector3f(1.25f, -2.5f, 7.5f), fromVector2);
        Assert.Equal(1f, values.X);
        Assert.Equal(2f, values.Y);
        Assert.Equal(3f, values.Z);
    }

    [Fact]
    public void Vector3fOperatorsMatchUdbArithmetic()
    {
        var left = new Vector3f(7f, -3f, 4f);
        var right = new Vector3f(2f, 5f, -6f);

        Assert.Equal(new Vector3f(14f, -6f, 8f), left * 2f);
        Assert.Equal(new Vector3f(14f, -6f, 8f), 2f * left);
        Assert.Equal(new Vector3f(9f, 2f, -2f), left + right);
        Assert.Equal(new Vector3f(5f, -8f, 10f), left - right);
        Assert.Equal(new Vector3f(-7f, 3f, -4f), -left);
        Assert.True(new Vector3f(1f, 2f, 3f) == new Vector3f(1f, 2f, 3f));
        Assert.True(new Vector3f(1f, 2f, 3f) != new Vector3f(1f, 2f, 4f));
    }

    [Fact]
    public void Vector3fMathHelpersMatchUdbSemantics()
    {
        var vector = new Vector3f(2f, 3f, 6f);
        var other = new Vector3f(5f, 7f, 11f);

        Assert.Equal(97f, Vector3f.Dot(vector, other));
        Assert.Equal(50f, Vector3f.DistanceSquared(vector, other));
        Assert.Equal(new Vector3f(-9f, 8f, -1f), Vector3f.Cross(vector, other));
        Assert.Equal(7f, vector.Length(), precision: 6);

        Vector3f normalized = Vector3f.Normalize(vector);
        Assert.Equal(2f / 7f, normalized.X, precision: 6);
        Assert.Equal(3f / 7f, normalized.Y, precision: 6);
        Assert.Equal(6f / 7f, normalized.Z, precision: 6);

        var zero = new Vector3f();
        zero.Normalize();
        Assert.Equal(new Vector3f(), zero);
    }

    [Fact]
    public void Vector3fHermiteMatchesUdbFormula()
    {
        var value1 = new Vector3f(1f, 2f, 3f);
        var tangent1 = new Vector3f(0.5f, 1.5f, -2f);
        var value2 = new Vector3f(9f, -4f, 5f);
        var tangent2 = new Vector3f(2f, -3f, 4f);

        Vector3f result = Vector3f.Hermite(value1, tangent1, value2, tangent2, 0.25f);

        Assert.Equal(2.2265625f, result.X, precision: 6);
        Assert.Equal(1.4140625f, result.Y, precision: 6);
        Assert.Equal(2.84375f, result.Z, precision: 6);
    }

    [Fact]
    public void Vector3fTransformMatchesUdbMatrixOrder()
    {
        Matrix transform = Matrix.Scaling(2f, 3f, 4f) * Matrix.Translation(5f, 6f, 7f);

        Vector4f result = Vector3f.Transform(new Vector3f(1f, 2f, 3f), transform);

        Assert.Equal(new Vector4f(7f, 12f, 19f, 1f), result);
    }

    [Fact]
    public void Vector3fEqualityAndHashMatchUdbSemantics()
    {
        var vector = new Vector3f(1f, 2f, 3f);

        Assert.True(vector.Equals(new Vector3f(1f, 2f, 3f)));
        Assert.False(vector.Equals(new Vector3f(1f, 2f, 4f)));
        Assert.False(vector.Equals("not a vector"));
        Assert.Equal(1f.GetHashCode() + 2f.GetHashCode() + 3f.GetHashCode(), vector.GetHashCode());
    }

    [Fact]
    public void Vector3iConstructorsAndOperatorsMatchUdbSemantics()
    {
        var uniform = new Vector3i(3);
        var fromVector2 = new Vector3i(new Vector2i(1, -2), 7);
        var left = new Vector3i(7, -3, 4);
        var right = new Vector3i(2, 5, -6);

        Assert.Equal(new Vector3i(3, 3, 3), uniform);
        Assert.Equal(new Vector3i(1, -2, 7), fromVector2);
        Assert.Equal(new Vector3i(9, 2, -2), left + right);
        Assert.Equal(new Vector3i(5, -8, 10), left - right);
        Assert.Equal(new Vector3i(-7, 3, -4), -left);
        Assert.True(new Vector3i(1, 2, 3) == new Vector3i(1, 2, 3));
        Assert.True(new Vector3i(1, 2, 3) != new Vector3i(1, 2, 4));
        Assert.Equal(1.GetHashCode() + 2.GetHashCode() + 3.GetHashCode(), new Vector3i(1, 2, 3).GetHashCode());
    }

    [Fact]
    public void MatrixFactoriesMatchUdbFieldLayout()
    {
        Matrix identity = Matrix.Identity;
        Matrix translation = Matrix.Translation(new Vector3f(5f, 6f, 7f));
        Matrix scaling = Matrix.Scaling(new Vector3f(2f, 3f, 4f));
        Matrix perspective = Matrix.PerspectiveFov((float)(Math.PI / 2.0), 2f, 1f, 9f);

        AssertMatrixDiagonal(identity, 1f, 1f, 1f, 1f);
        Assert.Equal(5f, translation.M41);
        Assert.Equal(6f, translation.M42);
        Assert.Equal(7f, translation.M43);
        AssertMatrixDiagonal(scaling, 2f, 3f, 4f, 1f);
        Assert.Equal(0.5f, perspective.M11, precision: 6);
        Assert.Equal(1f, perspective.M22, precision: 6);
        Assert.Equal(-1.25f, perspective.M33, precision: 6);
        Assert.Equal(-2.25f, perspective.M34, precision: 6);
        Assert.Equal(-1f, perspective.M43);
    }

    [Fact]
    public void MatrixRotationAndLookAtMatchUdbConventions()
    {
        Matrix rotationZ = Matrix.RotationZ((float)(Math.PI / 2.0));
        Matrix lookAt = Matrix.LookAt(new Vector3f(0f, 0f, -5f), new Vector3f(0f, 0f, 0f), new Vector3f(0f, 1f, 0f));

        Assert.Equal(0f, rotationZ.M11, precision: 6);
        Assert.Equal(1f, rotationZ.M12, precision: 6);
        Assert.Equal(-1f, rotationZ.M21, precision: 6);
        Assert.Equal(0f, rotationZ.M22, precision: 6);
        Assert.Equal(-1f, lookAt.M11, precision: 6);
        Assert.Equal(1f, lookAt.M22, precision: 6);
        Assert.Equal(-1f, lookAt.M33, precision: 6);
        Assert.Equal(-5f, lookAt.M43, precision: 6);
    }

    [Fact]
    public void MatrixEqualityAndHashMatchUdbSemantics()
    {
        Matrix left = Matrix.Translation(1f, 2f, 3f);
        Matrix right = Matrix.Translation(1f, 2f, 4f);

        Assert.True(left.Equals(Matrix.Translation(1f, 2f, 3f)));
        Assert.False(left.Equals(right));
        Assert.False(left.Equals("not a matrix"));
        Assert.True(left == Matrix.Translation(1f, 2f, 3f));
        Assert.True(left != right);
        Assert.Equal(
            left.M11.GetHashCode() + left.M12.GetHashCode() + left.M13.GetHashCode() + left.M14.GetHashCode()
            + left.M21.GetHashCode() + left.M22.GetHashCode() + left.M23.GetHashCode() + left.M24.GetHashCode()
            + left.M31.GetHashCode() + left.M32.GetHashCode() + left.M33.GetHashCode() + left.M34.GetHashCode()
            + left.M41.GetHashCode() + left.M42.GetHashCode() + left.M43.GetHashCode() + left.M44.GetHashCode(),
            left.GetHashCode());
    }

    private static void AssertMatrixDiagonal(Matrix matrix, float m11, float m22, float m33, float m44)
    {
        Assert.Equal(m11, matrix.M11);
        Assert.Equal(m22, matrix.M22);
        Assert.Equal(m33, matrix.M33);
        Assert.Equal(m44, matrix.M44);
    }
}
