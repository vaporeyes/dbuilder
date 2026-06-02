// ABOUTME: Tests UDBScript API helper conversions for vectors and universal values.
// ABOUTME: Covers accepted input shapes and type mappings used by future script wrappers.

using System.Dynamic;
using System.Numerics;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

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

    [Fact]
    public void Vector2DWrapperConstructsFromAcceptedVectorShapes()
    {
        var fromArray = new UdbScriptVector2DWrapper(new object[] { 3.0, 4.0 });
        var fromThreeD = new UdbScriptVector2DWrapper(new UdbScriptVector3DWrapper(5, 6, 7));

        Assert.Equal(new UdbScriptVector2DWrapper(3, 4), fromArray);
        Assert.Equal(new UdbScriptVector2DWrapper(5, 6), fromThreeD);
        Assert.Equal(3, fromArray.x);
        Assert.Equal(4, fromArray.y);
    }

    [Fact]
    public void Vector2DWrapperSupportsUdbArithmetic()
    {
        var vector = new UdbScriptVector2DWrapper(6, 3);

        Assert.Equal(new UdbScriptVector2DWrapper(8, 5), vector + 2.0);
        Assert.Equal(new UdbScriptVector2DWrapper(7, 5), vector + new object[] { 1.0, 2.0 });
        Assert.Equal(new UdbScriptVector2DWrapper(0.1, 0.05), 60.0 / vector);
        Assert.Equal("6, 3x", vector + "x");
        Assert.Equal("x6, 3", "x" + vector);
    }

    [Fact]
    public void Vector2DWrapperExposesUdbGeometryHelpers()
    {
        var vector = new UdbScriptVector2DWrapper(3, 4);

        Assert.Equal(11, UdbScriptVector2DWrapper.dotProduct(new UdbScriptVector2DWrapper(1, 2), new UdbScriptVector2DWrapper(3, 4)));
        Assert.Equal(new UdbScriptVector2DWrapper(-3, -4), UdbScriptVector2DWrapper.reversed(vector));
        Assert.Equal(new UdbScriptVector2DWrapper(8, 3), UdbScriptVector2DWrapper.crossProduct(vector, new object[] { 2.0, 1.0 }));
        Assert.Equal(5, vector.getLength());

        UdbScriptVector2DWrapper fromAngle = UdbScriptVector2DWrapper.fromAngle(0);
        Assert.Equal(0, fromAngle.X, 12);
        Assert.Equal(-1, fromAngle.Y, 12);
    }

    [Fact]
    public void Vector3DWrapperConstructsFromAcceptedVectorShapes()
    {
        var fromArray = new UdbScriptVector3DWrapper(new object[] { 3.0, 4.0, 5.0 });
        var fromTwoD = new UdbScriptVector3DWrapper(new UdbScriptVector2DWrapper(6, 7));

        Assert.Equal(new UdbScriptVector3DWrapper(3, 4, 5), fromArray);
        Assert.Equal(new UdbScriptVector3DWrapper(6, 7, 0), fromTwoD);
        Assert.Equal(3, fromArray.x);
        Assert.Equal(4, fromArray.y);
        Assert.Equal(5, fromArray.z);
    }

    [Fact]
    public void Vector3DWrapperSupportsUdbArithmetic()
    {
        var vector = new UdbScriptVector3DWrapper(6, 3, 2);

        Assert.Equal(new UdbScriptVector3DWrapper(8, 5, 4), vector + 2.0);
        Assert.Equal(new UdbScriptVector3DWrapper(7, 5, 5), vector + new object[] { 1.0, 2.0, 3.0 });
        Assert.Equal(new UdbScriptVector3DWrapper(4, 1, 0), 2.0 - vector);
        Assert.Equal(new UdbScriptVector3DWrapper(3, 1.5, 1), 2.0 / vector);
        Assert.Equal("6, 3, 2x", vector + "x");
    }

    [Fact]
    public void Vector3DWrapperExposesUdbGeometryHelpers()
    {
        var vector = new UdbScriptVector3DWrapper(2, 0, 0);

        Assert.Equal(32, UdbScriptVector3DWrapper.dotProduct(new UdbScriptVector3DWrapper(1, 2, 3), new UdbScriptVector3DWrapper(4, 5, 6)));
        Assert.Equal(new UdbScriptVector3DWrapper(0, 0, 1), UdbScriptVector3DWrapper.crossProduct(new object[] { 1.0, 0.0, 0.0 }, new object[] { 0.0, 1.0, 0.0 }));
        Assert.Equal(new UdbScriptVector3DWrapper(-2, 0, 0), UdbScriptVector3DWrapper.reversed(vector));
        Assert.Equal(new UdbScriptVector3DWrapper(6, 0, 0), vector.getScaled(3));
        Assert.False(vector.isNormalized());

        UdbScriptVector3DWrapper fromAngle = UdbScriptVector3DWrapper.fromAngleXYZ(0, 0);
        Assert.Equal(0, fromAngle.X, 12);
        Assert.Equal(-1, fromAngle.Y, 12);
        Assert.Equal(0, fromAngle.Z, 12);
    }

    [Fact]
    public void Line2DWrapperConstructsFromAcceptedVectorShapes()
    {
        var line = new UdbScriptLine2DWrapper(new object[] { 1.0, 2.0 }, new UdbScriptVector3DWrapper(3, 4, 5));

        Assert.Equal(new UdbScriptVector2DWrapper(1, 2), line.v1);
        Assert.Equal(new UdbScriptVector2DWrapper(3, 4), line.v2);
        Assert.Equal(new Line2D(new Vector2D(1, 2), new Vector2D(3, 4)), line.AsLine2D());
    }

    [Fact]
    public void Line2DWrapperExposesUdbStaticGeometryHelpers()
    {
        var horizontal = new UdbScriptLine2DWrapper(new object[] { 0.0, 0.0 }, new object[] { 10.0, 0.0 });
        var vertical = new UdbScriptLine2DWrapper(new object[] { 5.0, -5.0 }, new object[] { 5.0, 5.0 });

        Assert.True(UdbScriptLine2DWrapper.areIntersecting(horizontal, vertical));
        Assert.True(UdbScriptLine2DWrapper.areIntersecting(horizontal.v1, horizontal.v2, vertical.v1, vertical.v2));
        Assert.Equal(new UdbScriptVector2DWrapper(5, 0), UdbScriptLine2DWrapper.getIntersectionPoint(horizontal.v1, horizontal.v2, vertical.v1, vertical.v2));
        Assert.Equal(25, UdbScriptLine2DWrapper.getDistanceToLineSq(horizontal.v1, horizontal.v2, new object[] { 5.0, 5.0 }));
        Assert.Equal(5, UdbScriptLine2DWrapper.getDistanceToLine(horizontal.v1, horizontal.v2, new object[] { 5.0, 5.0 }));
        Assert.Equal(0.5, UdbScriptLine2DWrapper.getNearestOnLine(horizontal.v1, horizontal.v2, new object[] { 5.0, 5.0 }));
        Assert.Equal(new UdbScriptVector2DWrapper(2.5, 0), UdbScriptLine2DWrapper.getCoordinatesAt(horizontal.v1, horizontal.v2, 0.25));
    }

    [Fact]
    public void Line2DWrapperExposesUdbInstanceGeometryHelpers()
    {
        var line = new UdbScriptLine2DWrapper(new object[] { 0.0, 0.0 }, new object[] { 3.0, 4.0 });
        var crossing = new UdbScriptLine2DWrapper(new object[] { 0.0, 4.0 }, new object[] { 3.0, 0.0 });

        Assert.Equal(new UdbScriptVector2DWrapper(1.5, 2), line.getCoordinatesAt(0.5));
        Assert.Equal(5, line.getLength());
        Assert.Equal(Angle2D.RadToDeg(line.getAngleRad()), line.getAngle());
        Assert.Equal(new UdbScriptVector2DWrapper(-4, 3), line.getPerpendicular());
        Assert.True(line.isIntersecting(crossing));
        Assert.Equal(new UdbScriptVector2DWrapper(1.5, 2), line.getIntersectionPoint(crossing));
        Assert.True(line.getSideOfLine(new object[] { 0.0, 1.0 }) > 0);
        Assert.Equal("(0, 0) - (3, 4)", line.ToString());
    }

    [Fact]
    public void Line2DWrapperReturnsNanIntersectionPointWhenLinesDoNotIntersect()
    {
        var horizontal = new UdbScriptLine2DWrapper(new object[] { 0.0, 0.0 }, new object[] { 1.0, 0.0 });
        var vertical = new UdbScriptLine2DWrapper(new object[] { 2.0, 1.0 }, new object[] { 2.0, 2.0 });

        UdbScriptVector2DWrapper point = UdbScriptLine2DWrapper.getIntersectionPoint(horizontal.v1, horizontal.v2, vertical.v1, vertical.v2);

        Assert.False(UdbScriptLine2DWrapper.areIntersecting(horizontal, vertical));
        Assert.True(double.IsNaN(point.X));
        Assert.True(double.IsNaN(point.Y));
    }

    [Fact]
    public void Angle2DWrapperExposesUdbAngleConversions()
    {
        Assert.Equal(90, UdbScriptAngle2DWrapper.doomToReal(0));
        Assert.Equal(0, UdbScriptAngle2DWrapper.doomToReal(270));
        Assert.Equal(270, UdbScriptAngle2DWrapper.realToDoom(0));
        Assert.Equal(0, UdbScriptAngle2DWrapper.realToDoom(90));
        Assert.Equal(45, UdbScriptAngle2DWrapper.radToDeg(UdbScriptAngle2DWrapper.degToRad(45)), 12);
        Assert.Equal(5, UdbScriptAngle2DWrapper.normalized(365));
        Assert.Equal(355, UdbScriptAngle2DWrapper.normalized(-5));
        Assert.Equal(Angle2D.Normalized(-Angle2D.PIHALF), UdbScriptAngle2DWrapper.normalizedRad(-Angle2D.PIHALF));
    }

    [Fact]
    public void Angle2DWrapperExposesThreePointAngleHelpers()
    {
        object first = new object[] { 1.0, 0.0 };
        object second = new UdbScriptVector2DWrapper(0, 0);
        object third = new UdbScriptVector3DWrapper(0, 1, 5);

        double radians = UdbScriptAngle2DWrapper.getAngleRad(first, second, third);

        Assert.Equal(Angle2D.GetAngle(new Vector2D(1, 0), new Vector2D(0, 0), new Vector2D(0, 1)), radians);
        Assert.Equal(Angle2D.RadToDeg(radians), UdbScriptAngle2DWrapper.getAngle(first, second, third));
    }

    [Fact]
    public void PlaneWrapperConstructsFromNormalAndOffset()
    {
        var plane = new UdbScriptPlaneWrapper(new object[] { 0.0, 0.0, 1.0 }, -16);

        Assert.Equal(new UdbScriptVector3DWrapper(0, 0, 1), plane.normal);
        Assert.Equal(0, plane.a);
        Assert.Equal(0, plane.b);
        Assert.Equal(1, plane.c);
        Assert.Equal(-16, plane.offset);
        Assert.Equal(-16, plane.d);

        plane.d = -8;

        Assert.Equal(-8, plane.offset);
        Assert.Equal(-8, plane.AsPlane().Offset);
    }

    [Fact]
    public void PlaneWrapperConstructsFromThreePointsAndComputesGeometry()
    {
        var plane = new UdbScriptPlaneWrapper(
            new object[] { 0.0, 0.0, 0.0 },
            new object[] { 32.0, 0.0, 0.0 },
            new object[] { 32.0, 32.0, 16.0 },
            up: true);

        Assert.Equal(8, plane.getZ(new object[] { 16.0, 16.0 }));
        Assert.Equal(21.46625258399798, plane.distance(new object[] { 16.0, 16.0, 32.0 }), 12);
        Assert.Equal(new UdbScriptVector3DWrapper(16, 25.6, 12.8), plane.closestOnPlane(new object[] { 16.0, 16.0, 32.0 }));
    }

    [Fact]
    public void PlaneWrapperReportsLineIntersections()
    {
        var plane = new UdbScriptPlaneWrapper(new UdbScriptVector3DWrapper(0, 0, 1), 0);

        object[] hit = plane.getIntersection(new object[] { 0.0, 0.0, 32.0 }, new object[] { 0.0, 0.0, -32.0 });
        object[] miss = plane.getIntersection(new object[] { 0.0, 0.0, 32.0 }, new object[] { 32.0, 0.0, 32.0 });

        Assert.Equal(new object[] { true, 0.5 }, hit);
        Assert.False((bool)miss[0]);
        Assert.True(double.IsNaN((double)miss[1]));
    }

    [Fact]
    public void PlaneWrapperUsesPlaneEquality()
    {
        var first = new UdbScriptPlaneWrapper(new object[] { 0.0, 0.0, 1.0 }, -16);
        var second = new UdbScriptPlaneWrapper(new object[] { 0.0, 0.0, 1.0 }, -16);
        var third = new UdbScriptPlaneWrapper(new object[] { 0.0, 0.0, 1.0 }, -8);

        Assert.True(first == second);
        Assert.False(first != second);
        Assert.True(first.Equals(second));
        Assert.True(first != third);
    }

    [Fact]
    public void GameConfigurationWrapperExposesUdbConfigurationProperties()
    {
        GameConfiguration config = GameConfiguration.FromText("""
game = "Doom";
engine = "GZDoom";
localsidedeftextureoffsets = true;
""");

        var wrapper = new UdbScriptGameConfigurationWrapper(config);

        Assert.Equal("GZDoom", wrapper.engineName);
        Assert.True(wrapper.hasLocalSidedefTextureOffsets);
    }

    [Fact]
    public void GameConfigurationWrapperUsesParsedDefaultValues()
    {
        var wrapper = new UdbScriptGameConfigurationWrapper(GameConfiguration.FromText(""));

        Assert.Equal("", wrapper.engineName);
        Assert.False(wrapper.hasLocalSidedefTextureOffsets);
    }

    [Fact]
    public void VertexWrapperMutatesCoreProperties()
    {
        var vertex = new Vertex(new Vector2D(16, 32));
        var wrapper = new UdbScriptVertexWrapper(vertex);

        wrapper.position = new object[] { 64.0, 96.0 };
        wrapper.selected = true;
        wrapper.marked = true;
        wrapper.floorZ = 8.0;
        wrapper.ceilingZ = 24.0;

        Assert.Equal(new Vector2D(64, 96), vertex.Position);
        Assert.Equal(new UdbScriptVector2DWrapper(64, 96), wrapper.position);
        Assert.True(vertex.Selected);
        Assert.True(vertex.Marked);
        Assert.Equal(8.0, vertex.ZFloor);
        Assert.Equal(24.0, vertex.ZCeiling);
    }

    [Fact]
    public void VertexWrapperCopiesPropertiesAndUsesReferenceEquality()
    {
        var source = new Vertex(new Vector2D(1, 2))
        {
            Selected = true,
            Marked = true,
            Groups = 3,
            ZFloor = 4.0,
            ZCeiling = 5.0,
        };
        source.Fields["comment"] = "copied";
        var target = new Vertex(new Vector2D(8, 9));
        var sourceWrapper = new UdbScriptVertexWrapper(source);
        var targetWrapper = new UdbScriptVertexWrapper(target);

        sourceWrapper.copyPropertiesTo(targetWrapper);

        Assert.Equal(source.Position, target.Position);
        Assert.True(target.Selected);
        Assert.True(target.Marked);
        Assert.Equal(3, target.Groups);
        Assert.Equal(4.0, target.ZFloor);
        Assert.Equal(5.0, target.ZCeiling);
        Assert.Equal("copied", target.Fields["comment"]);
        Assert.True(sourceWrapper.Equals(new UdbScriptVertexWrapper(source)));
        Assert.False(sourceWrapper.Equals(targetWrapper));
    }

    [Fact]
    public void VertexWrapperReturnsConnectedNonDisposedLinedefs()
    {
        var vertex = new Vertex(new Vector2D(0, 0));
        var active = new Linedef(vertex, new Vertex(new Vector2D(16, 0)));
        var disposed = new Linedef(vertex, new Vertex(new Vector2D(0, 16))) { IsDisposed = true };
        vertex.Linedefs.Add(active);
        vertex.Linedefs.Add(disposed);
        var wrapper = new UdbScriptVertexWrapper(vertex);

        Linedef[] linedefs = wrapper.getLinedefs();

        Assert.Single(linedefs);
        Assert.Same(active, linedefs[0]);
    }

    [Fact]
    public void VertexWrapperRejectsDisposedVertexAccess()
    {
        var wrapper = new UdbScriptVertexWrapper(new Vertex { IsDisposed = true });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => wrapper.position);

        Assert.Equal("Vertex is disposed, the position member can not be accessed.", exception.Message);
    }

    [Fact]
    public void MapElementArgumentsWrapperMutatesThingArguments()
    {
        var thing = new Thing();
        thing.Args[0] = 1;
        thing.Args[1] = 2;
        var wrapper = new UdbScriptMapElementArgumentsWrapper(thing);

        wrapper[1] = 42;

        Assert.Equal(5, wrapper.length);
        Assert.Equal(1, wrapper[0]);
        Assert.Equal(42, thing.Args[1]);
        Assert.Equal(new[] { 1, 42, 0, 0, 0 }, wrapper.ToArray());
    }

    [Fact]
    public void MapElementArgumentsWrapperMutatesLinedefArguments()
    {
        var line = new Linedef();
        line.Args[2] = 7;
        var wrapper = new UdbScriptMapElementArgumentsWrapper(line);

        wrapper[2] = 9;
        wrapper[4] = 11;

        Assert.Equal(5, wrapper.length);
        Assert.Equal(9, line.Args[2]);
        Assert.Equal(11, wrapper[4]);
        Assert.Equal(new[] { 0, 0, 9, 0, 11 }, wrapper.ToArray());
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
