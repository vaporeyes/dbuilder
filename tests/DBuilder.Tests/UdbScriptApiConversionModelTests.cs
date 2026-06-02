// ABOUTME: Tests UDBScript API helper conversions for vectors and universal values.
// ABOUTME: Covers accepted input shapes and type mappings used by future script wrappers.

using System.Dynamic;
using System.Numerics;
using DBuilder.Geometry;
using DBuilder.IO;

namespace DBuilder.Tests;

public class UdbScriptApiConversionModelTests
{
    [Fact]
    public void ConvertsVectorInstancesAndWrappersToVector3D()
    {
        Assert.Equal(new Vector3D(1, 2, 0), UdbScriptApiConversionModel.GetVector3DFromObject(new Vector2D(1, 2)));
        Assert.Equal(new Vector3D(3, 4, 0), UdbScriptApiConversionModel.GetVector3DFromObject(new UdbScriptVector2DWrapper(3, 4)));
        Assert.Equal(new Vector3D(5, 6, 7), UdbScriptApiConversionModel.GetVector3DFromObject(new Vector3D(5, 6, 7)));
        Assert.Equal(new Vector3D(8, 9, 10), UdbScriptApiConversionModel.GetVector3DFromObject(new UdbScriptVector3DWrapper(8, 9, 10)));
    }

    [Fact]
    public void ConvertsNumericArraysToVector3D()
    {
        Assert.Equal(new Vector3D(1, 2, 0), UdbScriptApiConversionModel.GetVector3DFromObject(new object[] { 1.0, new BigInteger(2) }));
        Assert.Equal(new Vector3D(3, 4, 5), UdbScriptApiConversionModel.GetVector3DFromObject(new object[] { 3.0, 4.0, new BigInteger(5) }));

        UdbScriptVectorConversionException badElement = Assert.Throws<UdbScriptVectorConversionException>(
            () => UdbScriptApiConversionModel.GetVector3DFromObject(new object[] { 1.0, "bad" }));
        Assert.Equal("Values in array must be numbers.", badElement.Message);

        UdbScriptVectorConversionException badLength = Assert.Throws<UdbScriptVectorConversionException>(
            () => UdbScriptApiConversionModel.GetVector3DFromObject(new object[] { 1.0 }));
        Assert.Equal(UdbScriptApiConversionModel.VectorConversionFailureMessage, badLength.Message);
    }

    [Fact]
    public void ConvertsObjectMembersToVector3D()
    {
        dynamic expando = new ExpandoObject();
        expando.x = 1.5;
        expando.y = 2.0;
        expando.z = "3";

        Assert.Equal(new Vector3D(1.5, 2, 3), UdbScriptApiConversionModel.GetVector3DFromObject(expando));
        Assert.Equal(
            new Vector3D(4, 5, 0),
            UdbScriptApiConversionModel.GetVector3DFromObject(new Dictionary<string, object?> { ["x"] = 4.0, ["y"] = 5.0 }));

        UdbScriptVectorConversionException missingY = Assert.Throws<UdbScriptVectorConversionException>(
            () => UdbScriptApiConversionModel.GetVector3DFromObject(new Dictionary<string, object?> { ["x"] = 1.0 }));
        Assert.Equal(UdbScriptApiConversionModel.VectorConversionFailureMessage, missingY.Message);

        UdbScriptVectorConversionException badX = Assert.Throws<UdbScriptVectorConversionException>(
            () => UdbScriptApiConversionModel.GetVector3DFromObject(new Dictionary<string, object?> { ["x"] = "bad", ["y"] = 1.0 }));
        Assert.StartsWith("Can not convert 'x' property of data:", badX.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(UniversalType.Float, "1.5", 1.5)]
    [InlineData(UniversalType.AngleRadians, "2.5", 2.5)]
    [InlineData(UniversalType.Integer, "7", 7)]
    [InlineData(UniversalType.Color, "255", 255)]
    [InlineData(UniversalType.Boolean, "true", true)]
    [InlineData(UniversalType.String, 123, "123")]
    [InlineData(UniversalType.Texture, 456, "456")]
    public void ConvertsUniversalValuesToScriptPrimitiveValues(UniversalType type, object value, object expected)
    {
        object? converted = UdbScriptApiConversionModel.GetConvertedUniversalValue(new UdbScriptUniversalValue((int)type, value));

        Assert.Equal(expected, converted);
    }

    [Fact]
    public void MapsUniversalTypesToClrTypes()
    {
        Assert.Equal(typeof(double), UdbScriptApiConversionModel.GetTypeFromUniversalType((int)UniversalType.Float));
        Assert.Equal(typeof(double), UdbScriptApiConversionModel.GetTypeFromUniversalType((int)UniversalType.AngleDegreesFloat));
        Assert.Equal(typeof(int), UdbScriptApiConversionModel.GetTypeFromUniversalType((int)UniversalType.LinedefTag));
        Assert.Equal(typeof(int), UdbScriptApiConversionModel.GetTypeFromUniversalType((int)UniversalType.ThingType));
        Assert.Equal(typeof(bool), UdbScriptApiConversionModel.GetTypeFromUniversalType((int)UniversalType.Boolean));
        Assert.Equal(typeof(string), UdbScriptApiConversionModel.GetTypeFromUniversalType((int)UniversalType.Flat));
        Assert.Equal(typeof(string), UdbScriptApiConversionModel.GetTypeFromUniversalType((int)UniversalType.ThingClass));
        Assert.Null(UdbScriptApiConversionModel.GetTypeFromUniversalType((int)UniversalType.PolyobjectNumber));
    }
}
