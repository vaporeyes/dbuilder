// ABOUTME: Tests UDBScript API helper conversions for vectors and universal values.
// ABOUTME: Covers accepted input shapes and type mappings used by future script wrappers.

using System.Dynamic;
using System.IO;
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

        UdbScriptVectorConversionException typedArray = Assert.Throws<UdbScriptVectorConversionException>(
            () => UdbScriptApiConversionModel.GetVector3DFromObject(new double[] { 1.0, 2.0 }));
        Assert.Equal(UdbScriptApiConversionModel.VectorConversionFailureMessage, typedArray.Message);
    }

    [Fact]
    public void ConvertsObjectMembersToVector3D()
    {
        dynamic expando = new ExpandoObject();
        expando.x = 1.5;
        expando.y = 2.0;
        expando.z = "3";

        Assert.Equal(new Vector3D(1.5, 2, 3), UdbScriptApiConversionModel.GetVector3DFromObject(expando));
        UdbScriptVectorConversionException dictionary = Assert.Throws<UdbScriptVectorConversionException>(
            () => UdbScriptApiConversionModel.GetVector3DFromObject(new Dictionary<string, object?> { ["x"] = 4.0, ["y"] = 5.0 }));
        Assert.Equal(UdbScriptApiConversionModel.VectorConversionFailureMessage, dictionary.Message);

        dynamic missing = new ExpandoObject();
        missing.x = 1.0;
        UdbScriptVectorConversionException missingY = Assert.Throws<UdbScriptVectorConversionException>(
            () => UdbScriptApiConversionModel.GetVector3DFromObject(missing));
        Assert.Equal(UdbScriptApiConversionModel.VectorConversionFailureMessage, missingY.Message);

        dynamic bad = new ExpandoObject();
        bad.x = "bad";
        bad.y = 1.0;
        UdbScriptVectorConversionException badX = Assert.Throws<UdbScriptVectorConversionException>(
            () => UdbScriptApiConversionModel.GetVector3DFromObject(bad));
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

        fromArray.x = 8;
        fromArray.y = 9;
        Assert.Equal(new UdbScriptVector2DWrapper(8, 9), fromArray);
        Assert.Equal(8, fromArray.X);
        Assert.Equal(9, fromArray.Y);
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
    public void Vector2DWrapperSupportsUdbVectorLikeEquality()
    {
        var vector = new UdbScriptVector2DWrapper(6, 3);

        Assert.True(vector == new object[] { 6.0, 3.0 });
        Assert.True(new object[] { 6.0, 3.0 } == vector);
        Assert.False(vector == new object[] { 6.0, 4.0 });
        Assert.True(vector != new object[] { 6.0, 4.0 });
        Assert.True((object)new UdbScriptVector3DWrapper(6, 3, 9) == vector);
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

        fromArray.x = 8;
        fromArray.y = 9;
        fromArray.z = 10;
        Assert.Equal(new UdbScriptVector3DWrapper(8, 9, 10), fromArray);
        Assert.Equal(8, fromArray.X);
        Assert.Equal(9, fromArray.Y);
        Assert.Equal(10, fromArray.Z);
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
    public void Vector3DWrapperSupportsUdbVectorLikeEquality()
    {
        var vector = new UdbScriptVector3DWrapper(6, 3, 2);

        Assert.True(vector == new object[] { 6.0, 3.0, 2.0 });
        Assert.True(new object[] { 6.0, 3.0, 2.0 } == vector);
        Assert.False(vector == new object[] { 6.0, 3.0, 1.0 });
        Assert.True(vector != new object[] { 6.0, 3.0, 1.0 });
        Assert.True((object)new UdbScriptVector2DWrapper(6, 3) != vector);
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
        Assert.Equal(new UdbScriptVector2DWrapper(10, 0), UdbScriptLine2DWrapper.getNearestPointOnLine(horizontal.v1, horizontal.v2, new object[] { 15.0, 5.0 }));
        Assert.Equal(new UdbScriptVector2DWrapper(15, 0), UdbScriptLine2DWrapper.getNearestPointOnLine(horizontal.v1, horizontal.v2, new object[] { 15.0, 5.0 }, bounded: false));
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
        Assert.Equal(new UdbScriptVector2DWrapper(1.56, 2.08), line.getNearestPointOnLine(new object[] { 3.0, 1.0 }));
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
    public void PositionVectorCoordinateMutationUpdatesParentElements()
    {
        var vertex = new Vertex(new Vector2D(1, 2));
        var vertexPosition = (UdbScriptVector2DWrapper)new UdbScriptVertexWrapper(vertex).position;

        vertexPosition.x = 5;
        vertexPosition.y = 6;

        var thing = new Thing(new Vector2D(1, 2), 3001) { Height = 3 };
        var thingPosition = (UdbScriptVector3DWrapper)new UdbScriptThingWrapper(thing).position;

        thingPosition.x = 7;
        thingPosition.y = 8;
        thingPosition.z = 9;

        Assert.Equal(new Vector2D(5, 6), vertex.Position);
        Assert.Equal(new Vector2D(7, 8), thing.Position);
        Assert.Equal(9, thing.Height);
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

        UdbScriptLinedefWrapper[] linedefs = wrapper.getLinedefs();

        Assert.Single(linedefs);
        Assert.Same(active, linedefs[0].Linedef);
    }

    [Fact]
    public void VertexWrapperReturnsDistanceAndNearestLinedef()
    {
        var vertex = new Vertex(new Vector2D(0, 0));
        var horizontal = new Linedef(vertex, new Vertex(new Vector2D(64, 0)));
        var vertical = new Linedef(vertex, new Vertex(new Vector2D(0, 64)));
        vertex.Linedefs.Add(horizontal);
        vertex.Linedefs.Add(vertical);
        var wrapper = new UdbScriptVertexWrapper(vertex);

        Assert.Equal(25.0, wrapper.distanceToSq(new object[] { 3.0, 4.0 }));
        Assert.Equal(5.0, wrapper.distanceTo(new UdbScriptVector2DWrapper(3, 4)));
        UdbScriptLinedefWrapper? nearest = wrapper.nearestLinedef(new object[] { 4.0, 20.0 });

        Assert.NotNull(nearest);
        Assert.Same(vertical, nearest.Linedef);
    }

    [Fact]
    public void VertexWrapperSnapsToAccuracy()
    {
        var vertex = new Vertex(new Vector2D(1.2345, 6.789));
        var wrapper = new UdbScriptVertexWrapper(vertex);

        wrapper.snapToAccuracy();

        Assert.Equal(new Vector2D(1.234, 6.789), vertex.Position);

        wrapper.position = new object[] { 1.2345, 6.789 };
        wrapper.snapToAccuracy(2);

        Assert.Equal(new Vector2D(1.23, 6.79), vertex.Position);

        wrapper.position = new object[] { 1.6, 6.4 };
        wrapper.snapToAccuracy(2, usePrecisePosition: false);

        Assert.Equal(new Vector2D(2, 6), vertex.Position);
    }

    [Fact]
    public void VertexWrapperDeletesStandaloneVertex()
    {
        var vertex = new Vertex(new Vector2D(1, 2));
        var wrapper = new UdbScriptVertexWrapper(vertex);

        wrapper.delete();
        wrapper.delete();

        Assert.True(vertex.IsDisposed);
    }

    [Fact]
    public void MapOwnedVertexWrappersDeleteAndJoinVertices()
    {
        var map = new MapSet();
        var wrapper = new UdbScriptMapWrapper(map);
        UdbScriptVertexWrapper keep = wrapper.createVertex(new object[] { 0.0, 0.0 });
        UdbScriptVertexWrapper remove = wrapper.createVertex(new object[] { 32.0, 0.0 });
        Vertex end = map.AddVertex(new Vector2D(64, 0));
        Linedef line = map.AddLinedef(remove.Vertex, end);

        remove.join(keep);

        Assert.DoesNotContain(remove.Vertex, map.Vertices);
        Assert.Same(keep.Vertex, line.Start);
        Assert.Same(end, line.End);
        Assert.True(remove.Vertex.IsDisposed);

        UdbScriptVertexWrapper vertex = wrapper.createVertex(new object[] { 64.0, 0.0 });
        Linedef attached = map.AddLinedef(keep.Vertex, vertex.Vertex);

        vertex.delete();
        vertex.delete();

        Assert.DoesNotContain(vertex.Vertex, map.Vertices);
        Assert.DoesNotContain(attached, map.Linedefs);
        Assert.True(vertex.Vertex.IsDisposed);
        Assert.True(attached.IsDisposed);
    }

    [Fact]
    public void MapElementWrappersExposeUdbMapIndexes()
    {
        var map = new MapSet();
        Vertex firstVertex = map.AddVertex(new Vector2D(0, 0));
        Vertex secondVertex = map.AddVertex(new Vector2D(16, 0));
        Vertex thirdVertex = map.AddVertex(new Vector2D(32, 0));
        Linedef firstLine = map.AddLinedef(firstVertex, secondVertex);
        Linedef secondLine = map.AddLinedef(secondVertex, thirdVertex);
        Sector sector = map.AddSector();
        map.AddSidedef(firstLine, isFront: true, sector);
        Sidedef secondSide = map.AddSidedef(secondLine, isFront: true, sector);
        map.AddThing(new Vector2D(8, 8), 1);
        Thing secondThing = map.AddThing(new Vector2D(24, 8), 2);

        Assert.Equal(1, new UdbScriptVertexWrapper(secondVertex, map).index);
        Assert.Equal(1, new UdbScriptLinedefWrapper(secondLine, map).index);
        Assert.Equal(1, new UdbScriptSidedefWrapper(secondSide, map).index);
        Assert.Equal(1, new UdbScriptThingWrapper(secondThing, map).index);
        Assert.Equal("Thing 1", new UdbScriptThingWrapper(secondThing, map).ToString());
        Assert.Equal(-1, new UdbScriptVertexWrapper(new Vertex()).index);
        Assert.Equal(-1, new UdbScriptLinedefWrapper(new Linedef(firstVertex, secondVertex)).index);
        Assert.Equal(-1, new UdbScriptSidedefWrapper(new Sidedef()).index);
        Assert.Equal(-1, new UdbScriptThingWrapper(new Thing()).index);
        Assert.Equal("Thing -1", new UdbScriptThingWrapper(new Thing()).ToString());
    }

    [Fact]
    public void MapElementFlagWrappersMutateNamedFlags()
    {
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(16, 0)));
        var side = new Sidedef(line, isFront: true);
        var sector = new Sector();
        var thing = new Thing(new Vector2D(0, 0), 3001);

        UdbScriptFlagsWrapper lineFlags = new UdbScriptLinedefWrapper(line).flags;
        UdbScriptFlagsWrapper sideFlags = new UdbScriptSidedefWrapper(side).flags;
        UdbScriptFlagsWrapper sectorFlags = new UdbScriptSectorWrapper(sector).flags;
        UdbScriptFlagsWrapper thingFlags = new UdbScriptThingWrapper(thing).flags;

        lineFlags["blocking"] = true;
        sideFlags["lightfog"] = true;
        sectorFlags["secret"] = true;
        thingFlags["ambush"] = true;
        lineFlags["blocking"] = false;
        sideFlags.Remove("lightfog");
        sectorFlags.Clear();

        Assert.False(line.IsFlagSet("blocking"));
        Assert.False(side.IsFlagSet("lightfog"));
        Assert.False(sector.IsFlagSet("secret"));
        Assert.True(thing.IsFlagSet("ambush"));
        Assert.True(thingFlags.TryGetValue("ambush", out bool ambush));
        Assert.True(ambush);
        Assert.Contains(new KeyValuePair<string, bool>("ambush", true), thingFlags);
    }

    [Fact]
    public void VertexWrapperRejectsDisposedVertexAccess()
    {
        var wrapper = new UdbScriptVertexWrapper(new Vertex { IsDisposed = true });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => wrapper.position);

        Assert.Equal("Vertex is disposed, the position member can not be accessed.", exception.Message);
    }

    [Fact]
    public void LinedefWrapperExposesCorePropertiesAndArguments()
    {
        var start = new Vertex(new Vector2D(0, 0));
        var end = new Vertex(new Vector2D(3, 4));
        var line = new Linedef(start, end);
        var front = new Sidedef { IsFront = true };
        line.AttachFront(front);
        line.SetFlag("blocking", true);
        var wrapper = new UdbScriptLinedefWrapper(line);

        wrapper.selected = true;
        wrapper.marked = true;
        wrapper.activate = 7;
        wrapper.action = 80;
        wrapper.tag = 12;
        wrapper.args[2] = 99;

        Assert.Same(start, wrapper.start.Vertex);
        Assert.Same(end, wrapper.end.Vertex);
        Assert.Same(front, wrapper.front?.Sidedef);
        Assert.Null(wrapper.back);
        Assert.Equal(new UdbScriptLine2DWrapper(new UdbScriptVector2DWrapper(0, 0), new UdbScriptVector2DWrapper(3, 4)), wrapper.line);
        Assert.True(line.Selected);
        Assert.True(line.Marked);
        Assert.Equal(7, line.Activate);
        Assert.Equal(80, line.Action);
        Assert.Equal(12, line.Tag);
        Assert.Equal(99, line.Args[2]);
        Assert.Equal(25, wrapper.lengthSq);
        Assert.Equal(5, wrapper.length);
        Assert.Equal(0.2, wrapper.lengthInv);
        Assert.Equal(line.AngleDeg, wrapper.angle);
        Assert.Equal(line.Angle, wrapper.angleRad);
        Assert.True(wrapper.flags["blocking"]);
    }

    [Fact]
    public void LinedefWrapperExposesGeometryHelpers()
    {
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(10, 0)));
        var wrapper = new UdbScriptLinedefWrapper(line);

        Assert.Equal(new UdbScriptVector2DWrapper(5, 0), wrapper.getCenterPoint());
        Assert.Equal(new UdbScriptVector2DWrapper(2.5, 0), wrapper.getCoordinatesAt(0.25));
        Assert.Equal(new UdbScriptVector2DWrapper(5, 0), wrapper.nearestOnLine(new object[] { 5.0, 3.0 }));
        Assert.Equal(9, wrapper.distanceToSq(new object[] { 5.0, 3.0 }, bounded: true));
        Assert.Equal(3, wrapper.distanceTo(new object[] { 5.0, 3.0 }, bounded: true));
        Assert.Equal(line.SafeDistanceToSq(new Vector2D(5, 3), bounded: true), wrapper.safeDistanceToSq(new object[] { 5.0, 3.0 }, bounded: true));
        Assert.Equal(line.SafeDistanceTo(new Vector2D(5, 3), bounded: true), wrapper.safeDistanceTo(new object[] { 5.0, 3.0 }, bounded: true));
        Assert.Equal(line.SideOfLine(new Vector2D(5, 3)), wrapper.sideOfLine(new object[] { 5.0, 3.0 }));
        Assert.Equal(line.GetSidePoint(front: true).x, wrapper.getSidePoint(front: true).x);
        Assert.Equal(line.GetSidePoint(front: true).y, wrapper.getSidePoint(front: true).y);
    }

    [Fact]
    public void LinedefWrapperAppliesSidedFlags()
    {
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(10, 0)))
        {
            Flags = Linedef.TwoSidedFlagBit,
        };
        line.AttachFront(new Sidedef { IsFront = true });
        var wrapper = new UdbScriptLinedefWrapper(line);

        wrapper.applySidedFlags();

        Assert.True(line.IsFlagSet("blocking"));
        Assert.False(line.IsFlagSet("twosided"));
        Assert.Equal(Linedef.BlockingFlagBit, line.Flags & (Linedef.BlockingFlagBit | Linedef.TwoSidedFlagBit));

        line.AttachBack(new Sidedef { IsFront = false });
        wrapper.applySidedFlags();

        Assert.False(line.IsFlagSet("blocking"));
        Assert.True(line.IsFlagSet("twosided"));
        Assert.Equal(Linedef.TwoSidedFlagBit, line.Flags & (Linedef.BlockingFlagBit | Linedef.TwoSidedFlagBit));
    }

    [Fact]
    public void LinedefWrapperCopiesPropertiesAndClearsFlags()
    {
        var source = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(8, 0)))
        {
            Selected = true,
            Marked = true,
            Groups = 5,
            Flags = 64,
            Activate = 2,
            Action = 80,
        };
        source.Tag = 9;
        source.Args[1] = 3;
        source.SetFlag("blocking", true);
        source.Fields["comment"] = "copied";
        var target = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(0, 8)));
        var sourceWrapper = new UdbScriptLinedefWrapper(source);
        var targetWrapper = new UdbScriptLinedefWrapper(target);

        sourceWrapper.copyPropertiesTo(targetWrapper);
        sourceWrapper.clearFlags();

        Assert.True(target.Selected);
        Assert.True(target.Marked);
        Assert.Equal(5, target.Groups);
        Assert.Equal(64, target.Flags);
        Assert.Equal(2, target.Activate);
        Assert.Equal(80, target.Action);
        Assert.Equal(9, target.Tag);
        Assert.Equal(3, target.Args[1]);
        Assert.True(target.IsFlagSet("blocking"));
        Assert.Equal("copied", target.Fields["comment"]);
        Assert.Empty(source.UdmfFlags);
        Assert.Equal(0, source.Flags);
        Assert.True(sourceWrapper.Equals(new UdbScriptLinedefWrapper(source)));
        Assert.False(sourceWrapper.Equals(targetWrapper));
    }

    [Fact]
    public void LinedefWrapperExposesMultiTagHelpers()
    {
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(8, 0)));
        line.Tag = 1;
        var wrapper = new UdbScriptLinedefWrapper(line);

        bool added = wrapper.addTag(3);
        bool duplicate = wrapper.addTag(3);
        bool removed = wrapper.removeTag(1);
        bool missing = wrapper.removeTag(9);

        Assert.True(added);
        Assert.False(duplicate);
        Assert.True(removed);
        Assert.False(missing);
        Assert.Equal(new[] { 3 }, wrapper.getTags());

        Assert.True(wrapper.removeTag(3));
        Assert.Equal(new[] { 0 }, wrapper.getTags());
        Assert.Equal(0, line.Tag);
    }

    [Fact]
    public void LinedefWrapperFlipsVerticesAndSidedefs()
    {
        var start = new Vertex(new Vector2D(0, 0));
        var end = new Vertex(new Vector2D(16, 0));
        var line = new Linedef(start, end);
        var front = new Sidedef { IsFront = true };
        var back = new Sidedef { IsFront = false };
        line.AttachFront(front);
        line.AttachBack(back);
        var wrapper = new UdbScriptLinedefWrapper(line);

        wrapper.flipVertices();

        Assert.Same(end, line.Start);
        Assert.Same(start, line.End);

        wrapper.flipSidedefs();

        Assert.Same(back, line.Front);
        Assert.Same(front, line.Back);
        Assert.True(back.IsFront);
        Assert.False(front.IsFront);
    }

    [Fact]
    public void LinedefWrapperRejectsDisposedLinedefAccess()
    {
        var wrapper = new UdbScriptLinedefWrapper(new Linedef
        {
            IsDisposed = true,
        });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => wrapper.length);

        Assert.Equal("Linedef is disposed, the length member can not be accessed.", exception.Message);
    }

    [Fact]
    public void LinedefWrapperDeletesStandaloneLinedef()
    {
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(64, 0)));
        var wrapper = new UdbScriptLinedefWrapper(line);

        wrapper.delete();
        wrapper.delete();

        Assert.True(line.IsDisposed);
    }

    [Fact]
    public void LinedefWrapperSplitsStandaloneLinedef()
    {
        var start = new Vertex(new Vector2D(0, 0));
        var end = new Vertex(new Vector2D(64, 0));
        var line = new Linedef(start, end) { Action = 80, Tag = 7 };
        var front = new Sidedef(line, isFront: true) { OffsetX = 4 };
        front.SetTextureMid("STARTAN3");
        line.AttachFront(front);
        var wrapper = new UdbScriptLinedefWrapper(line);

        UdbScriptLinedefWrapper created = wrapper.split(new object[] { 16.12345, 0.0 });

        Assert.Equal(new Vector2D(16.123, 0), line.End.Position);
        Assert.Same(line.End, created.Linedef.Start);
        Assert.Same(end, created.Linedef.End);
        Assert.Equal(80, created.Linedef.Action);
        Assert.Equal(7, created.Linedef.Tag);
        Assert.NotNull(created.Linedef.Front);
        Assert.Equal("STARTAN3", created.Linedef.Front!.MidTexture);
        Assert.Equal(20, created.Linedef.Front.OffsetX);
    }

    [Fact]
    public void MapOwnedLinedefWrappersSnapNewSplitVerticesLikeUdbScript()
    {
        var map = new MapSet();
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(start, end);
        var wrapper = new UdbScriptLinedefWrapper(line, map);

        UdbScriptLinedefWrapper created = wrapper.split(new object[] { 16.12345, 0.00049 });

        Vertex split = line.End;
        Assert.Contains(split, map.Vertices);
        Assert.Equal(new Vector2D(16.123, 0), split.Position);
        Assert.Same(split, created.Linedef.Start);
    }

    [Fact]
    public void MapOwnedLinedefWrappersDeleteLinedefsAndSidedefs()
    {
        var map = new MapSet();
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(start, end);
        var sector = map.AddSector();
        Sidedef side = map.AddSidedef(line, isFront: true, sector);
        var wrapper = new UdbScriptMapWrapper(map);
        UdbScriptLinedefWrapper lineWrapper = Assert.Single(wrapper.getLinedefs());

        lineWrapper.delete();
        lineWrapper.delete();

        Assert.DoesNotContain(line, map.Linedefs);
        Assert.DoesNotContain(side, map.Sidedefs);
        Assert.True(line.IsDisposed);
        Assert.True(side.IsDisposed);
    }

    [Fact]
    public void MapOwnedLinedefWrappersSplitLinedefsByVertex()
    {
        var map = new MapSet();
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(64, 0));
        var split = map.AddVertex(new Vector2D(16, 0));
        var line = map.AddLinedef(start, end);
        line.Action = 80;
        line.Tag = 7;
        var sector = map.AddSector();
        Sidedef side = map.AddSidedef(line, isFront: true, sector);
        side.OffsetX = 4;
        side.SetTextureMid("STARTAN3");
        map.BuildIndexes();
        var wrapper = new UdbScriptMapWrapper(map);
        UdbScriptLinedefWrapper lineWrapper = Assert.Single(wrapper.getLinedefs());

        UdbScriptLinedefWrapper created = lineWrapper.split(new UdbScriptVertexWrapper(split, map));

        Assert.Equal(2, map.Linedefs.Count);
        Assert.Contains(created.Linedef, map.Linedefs);
        Assert.Same(split, line.End);
        Assert.Same(split, created.Linedef.Start);
        Assert.Same(end, created.Linedef.End);
        Assert.Equal(80, created.Linedef.Action);
        Assert.Equal(7, created.Linedef.Tag);
        Assert.NotNull(created.Linedef.Front);
        Assert.Equal("STARTAN3", created.Linedef.Front!.MidTexture);
        Assert.Contains(created.Linedef.Front, map.Sidedefs);
        Assert.Contains(created.Linedef.Front, sector.Sidedefs);
    }

    [Fact]
    public void SidedefWrapperExposesCoreProperties()
    {
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(16, 0)));
        var front = new Sidedef();
        var back = new Sidedef();
        line.AttachFront(front);
        line.AttachBack(back);
        front.SetFlag("lightfog", true);
        line.Selected = true;
        var wrapper = new UdbScriptSidedefWrapper(front);

        wrapper.offsetX = 8;
        wrapper.offsetY = 16;
        wrapper.upperTexture = "UPPERSCRIPT";
        wrapper.middleTexture = "MIDDLESCRIPT";
        wrapper.lowerTexture = "LOWERSCRIPT";

        Assert.True(wrapper.isFront);
        Assert.Same(line, wrapper.line.Linedef);
        Assert.Same(back, wrapper.other?.Sidedef);
        Assert.Equal(Angle2D.RadToDeg(front.Angle), wrapper.angle);
        Assert.Equal(front.Angle, wrapper.angleRad);
        Assert.Equal(8, front.OffsetX);
        Assert.Equal(16, front.OffsetY);
        Assert.Equal("UPPERSCRIPT", front.HighTexture);
        Assert.Equal("MIDDLESCRIPT", front.MidTexture);
        Assert.Equal("LOWERSCRIPT", front.LowTexture);
        Assert.Equal(Lump.MakeLongName("UPPERSCRIPT", useLongNames: true), front.LongHighTexture);
        Assert.Equal(Lump.MakeLongName("MIDDLESCRIPT", useLongNames: true), front.LongMiddleTexture);
        Assert.Equal(Lump.MakeLongName("LOWERSCRIPT", useLongNames: true), front.LongLowTexture);
        Assert.True(wrapper.flags["lightfog"]);
        Assert.True(wrapper.upperSelected);
        Assert.True(wrapper.middleSelected);
        Assert.True(wrapper.lowerSelected);
        Assert.Equal(new UdbScriptVector2DWrapper(8, 0), wrapper.getCenterPoint());
        Assert.Equal(
            new UdbScriptVector2DWrapper(line.GetSidePoint(front: true).x, line.GetSidePoint(front: true).y),
            wrapper.getSidePoint(front: true));
        Assert.Equal(new UdbScriptVector2DWrapper(4, 0), wrapper.getCoordinatesAt(0.25));
    }

    [Fact]
    public void SidedefWrapperCopiesPropertiesAndUsesReferenceEquality()
    {
        var sourceLine = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(8, 0)));
        var source = new Sidedef();
        sourceLine.AttachFront(source);
        source.Selected = true;
        source.Marked = true;
        source.OffsetX = 12;
        source.OffsetY = 24;
        source.SetTextureHigh("UPPER");
        source.SetTextureMid("MID");
        source.SetTextureLow("LOWER");
        source.SetFlag("clipmidtex", true);
        source.Fields["comment"] = "copied";
        var targetLine = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(0, 8)));
        var target = new Sidedef();
        targetLine.AttachFront(target);
        var sourceWrapper = new UdbScriptSidedefWrapper(source);
        var targetWrapper = new UdbScriptSidedefWrapper(target);

        sourceWrapper.copyPropertiesTo(targetWrapper);

        Assert.True(target.Selected);
        Assert.True(target.Marked);
        Assert.Equal(12, target.OffsetX);
        Assert.Equal(24, target.OffsetY);
        Assert.Equal("UPPER", target.HighTexture);
        Assert.Equal("MID", target.MidTexture);
        Assert.Equal("LOWER", target.LowTexture);
        Assert.True(target.IsFlagSet("clipmidtex"));
        Assert.Equal("copied", target.Fields["comment"]);
        Assert.True(sourceWrapper.Equals(new UdbScriptSidedefWrapper(source)));
        Assert.False(sourceWrapper.Equals(targetWrapper));
    }

    [Fact]
    public void SidedefWrapperRejectsDisposedSidedefAccess()
    {
        var wrapper = new UdbScriptSidedefWrapper(new Sidedef { IsDisposed = true });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => wrapper.offsetX);

        Assert.Equal("Sidedef is disposed, the offsetX member can not be accessed.", exception.Message);
    }

    [Fact]
    public void SectorWrapperExposesCorePropertiesAndSidedefs()
    {
        Sector sector = CreateSquareSector();
        sector.Index = 4;
        sector.SetFlag("secret", true);
        var wrapper = new UdbScriptSectorWrapper(sector);

        wrapper.floorHeight = 8;
        wrapper.ceilingHeight = 128;
        wrapper.floorTexture = "FLOORSCRIPT";
        wrapper.ceilingTexture = "CEILINGSCRIPT";
        wrapper.selected = true;
        wrapper.marked = true;
        wrapper.special = 9;
        wrapper.tag = 12;
        wrapper.brightness = 192;
        wrapper.floorSlopeOffset = -8.0;
        wrapper.ceilingSlopeOffset = 16.0;

        Assert.Equal(4, wrapper.index);
        Assert.Equal(8, sector.FloorHeight);
        Assert.Equal(128, sector.CeilHeight);
        Assert.Equal("FLOORSCRIPT", sector.FloorTexture);
        Assert.Equal("CEILINGSCRIPT", sector.CeilTexture);
        Assert.Equal(Lump.MakeLongName("FLOORSCRIPT", useLongNames: true), sector.LongFloorTexture);
        Assert.Equal(Lump.MakeLongName("CEILINGSCRIPT", useLongNames: true), sector.LongCeilTexture);
        Assert.True(sector.Selected);
        Assert.True(sector.Marked);
        Assert.All(sector.Sidedefs, side => Assert.True(side.Line.Selected));
        Assert.True(wrapper.floorSelected);
        Assert.True(wrapper.ceilingSelected);
        Assert.Equal(9, sector.Special);
        Assert.Equal(12, sector.Tag);
        Assert.Equal(192, sector.Brightness);
        Assert.Equal(-8.0, sector.FloorSlopeOffset);
        Assert.Equal(16.0, sector.CeilSlopeOffset);
        Assert.True(wrapper.flags["secret"]);
        Assert.Equal(4, wrapper.getSidedefs().Length);
        Assert.Same(sector, wrapper.getSidedefs()[0].sector?.Sector);
    }

    [Fact]
    public void SectorWrapperSelectionKeepsSharedLineSelectedWhenAdjacentSectorSelected()
    {
        var (_, first, second, shared) = CreateTwoSharedSectors();
        var firstWrapper = new UdbScriptSectorWrapper(first);
        var secondWrapper = new UdbScriptSectorWrapper(second);

        firstWrapper.selected = true;
        secondWrapper.selected = true;
        firstWrapper.selected = false;

        Assert.False(first.Selected);
        Assert.True(second.Selected);
        Assert.True(shared.Selected);
    }

    [Fact]
    public void SectorWrapperCopiesPropertiesAndClearsFlags()
    {
        Sector source = CreateSquareSector();
        source.Selected = true;
        source.Marked = true;
        source.Groups = 3;
        source.FloorHeight = 12;
        source.CeilHeight = 120;
        source.SetFloorTexture("FLOOR");
        source.SetCeilTexture("CEIL");
        source.Brightness = 144;
        source.Special = 7;
        source.Tag = 5;
        source.FloorSlope = new Vector3D(0, 1, 1);
        source.FloorSlopeOffset = -16.0;
        source.CeilSlope = new Vector3D(0, -1, 1);
        source.CeilSlopeOffset = 32.0;
        source.SetFlag("secret", true);
        source.Fields["comment"] = "copied";
        Sector target = CreateSquareSector();
        var sourceWrapper = new UdbScriptSectorWrapper(source);
        var targetWrapper = new UdbScriptSectorWrapper(target);

        sourceWrapper.copyPropertiesTo(targetWrapper);
        sourceWrapper.clearFlags();

        Assert.True(target.Selected);
        Assert.True(target.Marked);
        Assert.Equal(3, target.Groups);
        Assert.Equal(12, target.FloorHeight);
        Assert.Equal(120, target.CeilHeight);
        Assert.Equal("FLOOR", target.FloorTexture);
        Assert.Equal("CEIL", target.CeilTexture);
        Assert.Equal(144, target.Brightness);
        Assert.Equal(7, target.Special);
        Assert.Equal(5, target.Tag);
        Assert.Equal(new Vector3D(0, 1, 1), target.FloorSlope);
        Assert.Equal(-16.0, target.FloorSlopeOffset);
        Assert.Equal(new Vector3D(0, -1, 1), target.CeilSlope);
        Assert.Equal(32.0, target.CeilSlopeOffset);
        Assert.True(target.IsFlagSet("secret"));
        Assert.Equal("copied", target.Fields["comment"]);
        Assert.Empty(source.UdmfFlags);
        Assert.True(sourceWrapper.Equals(new UdbScriptSectorWrapper(source)));
        Assert.False(sourceWrapper.Equals(targetWrapper));
    }

    [Fact]
    public void SectorWrapperExposesGeometrySlopesAndTagHelpers()
    {
        Sector sector = CreateSquareSector();
        sector.FloorHeight = 8;
        sector.CeilHeight = 128;
        sector.Tag = 1;
        var wrapper = new UdbScriptSectorWrapper(sector);

        Assert.Equal(8, wrapper.getFloorZ(new object[] { 32.0, 32.0 }));
        Assert.Equal(128, wrapper.getCeilingZ(new UdbScriptVector2DWrapper(32, 32)));

        wrapper.setFloorSlope(new object[] { 0.0, 0.6, 0.8 });
        wrapper.setCeilingSlope(new UdbScriptVector3DWrapper(0, -0.6, 0.8));
        wrapper.floorSlopeOffset = -8.0;
        wrapper.ceilingSlopeOffset = -128.0;
        bool added = wrapper.addTag(3);
        bool addedDuplicate = wrapper.addTag(3);
        bool removed = wrapper.removeTag(1);

        Assert.True(wrapper.intersect(new object[] { 32.0, 32.0 }));
        Assert.False(wrapper.intersect(new object[] { 96.0, 96.0 }));
        Assert.Equal(sector.GetFloorZ(new Vector2D(32, 32)), wrapper.getFloorZ(new object[] { 32.0, 32.0 }));
        Assert.Equal(sector.GetCeilZ(new Vector2D(32, 32)), wrapper.getCeilingZ(new object[] { 32.0, 32.0 }));
        UdbScriptVector3DWrapper floorSlope = wrapper.getFloorSlope();
        UdbScriptVector3DWrapper ceilingSlope = wrapper.getCeilingSlope();
        Assert.Equal(0, floorSlope.x, 12);
        Assert.Equal(0.6, floorSlope.y, 12);
        Assert.Equal(0.8, floorSlope.z, 12);
        Assert.Equal(0, ceilingSlope.x, 12);
        Assert.Equal(-0.6, ceilingSlope.y, 12);
        Assert.Equal(0.8, ceilingSlope.z, 12);
        Assert.True(added);
        Assert.False(addedDuplicate);
        Assert.True(removed);
        Assert.Equal(new[] { 3 }, wrapper.getTags());
    }

    [Fact]
    public void SectorWrapperReturnsLabelPositions()
    {
        Sector sector = CreateSquareSector();
        var wrapper = new UdbScriptSectorWrapper(sector);
        LabelPositionInfo expected = Assert.Single(Tools.FindLabelPositions(sector));

        UdbScriptVector2DWrapper label = Assert.Single(wrapper.getLabelPositions());

        Assert.Equal(new UdbScriptVector2DWrapper(expected.position.x, expected.position.y), label);
    }

    [Fact]
    public void SectorWrapperReturnsTriangles()
    {
        Sector sector = CreateSquareSector();
        var wrapper = new UdbScriptSectorWrapper(sector);
        Triangulation expected = Triangulation.Create(sector);

        UdbScriptVector2DWrapper[][] triangles = wrapper.getTriangles();

        Assert.Equal(expected.Vertices.Count / 3, triangles.Length);
        Assert.All(triangles, triangle => Assert.Equal(3, triangle.Length));
        for (int i = 0; i < expected.Vertices.Count; i++)
        {
            UdbScriptVector2DWrapper actual = triangles[i / 3][i % 3];
            Assert.Equal(new UdbScriptVector2DWrapper(expected.Vertices[i].x, expected.Vertices[i].y), actual);
        }
    }

    [Fact]
    public void SectorWrapperRejectsDisposedSectorAccess()
    {
        var wrapper = new UdbScriptSectorWrapper(new Sector { IsDisposed = true });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => wrapper.floorHeight);

        Assert.Equal("Sector is disposed, the floorHeight member can not be accessed.", exception.Message);
    }

    [Fact]
    public void SectorWrapperDeletesStandaloneSector()
    {
        var sector = new Sector();
        var wrapper = new UdbScriptSectorWrapper(sector);

        wrapper.delete();
        wrapper.delete();

        Assert.True(sector.IsDisposed);
    }

    [Fact]
    public void MapOwnedSectorWrappersDeleteAndJoinSectors()
    {
        var map = new MapSet();
        var keep = map.AddSector();
        var remove = map.AddSector();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        Sidedef side = map.AddSidedef(line, isFront: true, remove);
        var wrapper = new UdbScriptMapWrapper(map);

        wrapper.getSectors().First(sector => ReferenceEquals(sector.Sector, remove))
            .join(wrapper.getSectors().First(sector => ReferenceEquals(sector.Sector, keep)));

        Assert.DoesNotContain(remove, map.Sectors);
        Assert.Same(keep, side.Sector);
        Assert.True(remove.IsDisposed);

        UdbScriptSectorWrapper keepWrapper = wrapper.getSectors().First(sector => ReferenceEquals(sector.Sector, keep));
        keepWrapper.delete();
        keepWrapper.delete();

        Assert.DoesNotContain(keep, map.Sectors);
        Assert.DoesNotContain(side, map.Sidedefs);
        Assert.True(keep.IsDisposed);
        Assert.True(side.IsDisposed);
        Assert.DoesNotContain(line, map.Linedefs);
        Assert.True(line.IsDisposed);
    }

    [Fact]
    public void MapOwnedSectorWrapperDeleteFlipsRemainingBackSideAndRepairsTextures()
    {
        var map = new MapSet();
        var remove = map.AddSector();
        var keep = map.AddSector();
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(start, end);
        Sidedef removedSide = map.AddSidedef(line, isFront: true, remove);
        Sidedef keptSide = map.AddSidedef(line, isFront: false, keep);
        keptSide.HighTexture = "STONE";
        keptSide.LongHighTexture = 1234;
        map.BuildIndexes();

        new UdbScriptSectorWrapper(remove, map).delete();

        Assert.DoesNotContain(remove, map.Sectors);
        Assert.True(remove.IsDisposed);
        Assert.DoesNotContain(removedSide, map.Sidedefs);
        Assert.True(removedSide.IsDisposed);
        Assert.Contains(line, map.Linedefs);
        Assert.Same(end, line.Start);
        Assert.Same(start, line.End);
        Assert.Same(keptSide, line.Front);
        Assert.Null(line.Back);
        Assert.True(keptSide.IsFront);
        Assert.True(keptSide.MiddleRequired());
        Assert.Equal("STONE", keptSide.MidTexture);
        Assert.Equal(1234, keptSide.LongMiddleTexture);
        Assert.Equal("-", keptSide.HighTexture);
        Assert.Equal(MapSet.EmptyLongName, keptSide.LongHighTexture);
        Assert.True(line.IsFlagSet("blocking"));
        Assert.False(line.IsFlagSet("twosided"));
    }

    [Fact]
    public void MapOwnedSectorWrapperDeleteRemovesOrphanedLines()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        Sidedef side = map.AddSidedef(line, isFront: true, sector);
        map.BuildIndexes();

        new UdbScriptSectorWrapper(sector, map).delete();

        Assert.DoesNotContain(sector, map.Sectors);
        Assert.DoesNotContain(side, map.Sidedefs);
        Assert.DoesNotContain(line, map.Linedefs);
        Assert.True(sector.IsDisposed);
        Assert.True(side.IsDisposed);
        Assert.True(line.IsDisposed);
    }

    [Fact]
    public void ThingWrapperExposesCorePropertiesAndArguments()
    {
        var sector = new Sector { Tag = 99 };
        var thing = new Thing(new Vector2D(16, 32), 3001, 90)
        {
            Sector = sector,
        };
        thing.SetFlag("ambush", true);
        var wrapper = new UdbScriptThingWrapper(thing);

        wrapper.type = 3002;
        wrapper.angle = 180;
        wrapper.action = 80;
        wrapper.tag = 7;
        wrapper.selected = true;
        wrapper.marked = true;
        wrapper.args[3] = 44;
        wrapper.position = new object[] { 64.0, 96.0, 12.0 };
        wrapper.pitch = 450;
        wrapper.roll = -90;
        wrapper.scaleX = 1.5;
        wrapper.scaleY = 0.75;

        Assert.Equal(3002, thing.Type);
        Assert.Equal(180, thing.Angle);
        Assert.Equal(80, thing.Action);
        Assert.Equal(7, thing.Tag);
        Assert.True(thing.Selected);
        Assert.True(thing.Marked);
        Assert.Equal(44, thing.Args[3]);
        Assert.Equal(new Vector2D(64, 96), thing.Position);
        Assert.Equal(12.0, thing.Height);
        Assert.Equal(90, thing.Pitch);
        Assert.Equal(270, thing.Roll);
        Assert.Equal(1.5, thing.ScaleX);
        Assert.Equal(0.75, thing.ScaleY);
        Assert.True(wrapper.flags["ambush"]);
        Assert.Same(sector, wrapper.getSector()?.Sector);
        Assert.Equal(new UdbScriptVector3DWrapper(64, 96, 12), wrapper.position);
    }

    [Fact]
    public void ThingWrapperFieldsExposeAndApplyManagedScaleFields()
    {
        var thing = new Thing(new Vector2D(1, 2), 3001);
        thing.SetScale(1.5, 0.75);
        var fields = new UdbScriptThingWrapper(thing).fields;

        Assert.True(fields.ContainsKey("scalex"));
        Assert.True(fields.ContainsKey("scaley"));
        Assert.Equal(1.5, fields["scalex"]);
        Assert.Equal(0.75, fields["scaley"]);

        fields["scalex"] = 2.0;
        fields["scaley"] = null;

        Assert.Equal(2.0, thing.ScaleX);
        Assert.Equal(1.0, thing.ScaleY);
        Assert.Equal(2.0, thing.Fields["scalex"]);
        Assert.False(thing.Fields.ContainsKey("scaley"));
        Assert.False(fields.ContainsKey("scaley"));
    }

    [Fact]
    public void ThingWrapperGetSectorDeterminesContainingSector()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        Vertex start = map.AddVertex(new Vector2D(0, 0));
        Vertex end = map.AddVertex(new Vector2D(64, 0));
        Linedef line = map.AddLinedef(start, end);
        map.AddSidedef(line, isFront: true, sector);
        Thing thing = map.AddThing(new Vector2D(32, -8), 3001);
        map.BuildIndexes();
        var wrapper = new UdbScriptThingWrapper(thing, map);

        UdbScriptSectorWrapper? detected = wrapper.getSector();

        Assert.Same(sector, detected?.Sector);
        Assert.Same(sector, thing.Sector);
    }

    [Fact]
    public void ThingWrapperExposesAngleAndDistanceHelpers()
    {
        var thing = new Thing(new Vector2D(3, 4), 1);
        var wrapper = new UdbScriptThingWrapper(thing);

        wrapper.angleRad = 0;

        Assert.Equal(Angle2D.RealToDoom(0), thing.Angle);
        Assert.Equal(Angle2D.DoomToReal(thing.Angle), wrapper.angleRad);
        Assert.Equal(25, wrapper.distanceToSq(new object[] { 0.0, 0.0 }));
        Assert.Equal(5, wrapper.distanceTo(new UdbScriptVector2DWrapper(0, 0)));
    }

    [Fact]
    public void ThingWrapperSnapsToAccuracy()
    {
        var thing = new Thing(new Vector2D(1.2345, 6.789), 1)
        {
            Height = 3.456,
        };
        var wrapper = new UdbScriptThingWrapper(thing);

        wrapper.snapToAccuracy();

        Assert.Equal(new Vector2D(1.234, 6.789), thing.Position);
        Assert.Equal(3.456, thing.Height);

        wrapper.position = new UdbScriptVector3DWrapper(1.2345, 6.789, 3.456);
        wrapper.snapToAccuracy(2);

        Assert.Equal(new Vector2D(1.23, 6.79), thing.Position);
        Assert.Equal(3.46, thing.Height);

        wrapper.position = new UdbScriptVector3DWrapper(1.6, 6.4, 3.5);
        wrapper.snapToAccuracy(2, usePrecisePosition: false);

        Assert.Equal(new Vector2D(2, 6), thing.Position);
        Assert.Equal(4, thing.Height);
    }

    [Fact]
    public void ThingWrapperDeletesStandaloneThing()
    {
        var thing = new Thing(new Vector2D(1, 2), 3001);
        var wrapper = new UdbScriptThingWrapper(thing);

        wrapper.delete();
        wrapper.delete();

        Assert.True(thing.IsDisposed);
    }

    [Fact]
    public void ThingWrapperCopiesPropertiesAndClearsFlags()
    {
        var source = new Thing(new Vector2D(1, 2), 3001, 90)
        {
            Selected = true,
            Marked = true,
            Groups = 3,
            Height = 4.0,
            Pitch = 5,
            Roll = 6,
            ScaleX = 1.25,
            ScaleY = 0.5,
            Size = 16,
            FixedSize = true,
            Flags = 32,
            Tag = 7,
            Action = 80,
        };
        source.Args[2] = 11;
        source.SetFlag("ambush", true);
        source.Fields["comment"] = "copied";
        var target = new Thing();
        var sourceWrapper = new UdbScriptThingWrapper(source);
        var targetWrapper = new UdbScriptThingWrapper(target);

        sourceWrapper.copyPropertiesTo(targetWrapper);
        sourceWrapper.clearFlags();

        Assert.Equal(source.Position, target.Position);
        Assert.True(target.Selected);
        Assert.True(target.Marked);
        Assert.Equal(3, target.Groups);
        Assert.Equal(4.0, target.Height);
        Assert.Equal(3001, target.Type);
        Assert.Equal(90, target.Angle);
        Assert.Equal(5, target.Pitch);
        Assert.Equal(6, target.Roll);
        Assert.Equal(1.25, target.ScaleX);
        Assert.Equal(0.5, target.ScaleY);
        Assert.Equal(16, target.Size);
        Assert.True(target.FixedSize);
        Assert.Equal(32, target.Flags);
        Assert.Equal(7, target.Tag);
        Assert.Equal(80, target.Action);
        Assert.Equal(11, target.Args[2]);
        Assert.True(target.IsFlagSet("ambush"));
        Assert.Equal("copied", target.Fields["comment"]);
        Assert.Empty(source.UdmfFlags);
        Assert.Equal(0, source.Flags);
        Assert.True(sourceWrapper.Equals(new UdbScriptThingWrapper(source)));
        Assert.False(sourceWrapper.Equals(targetWrapper));
    }

    [Fact]
    public void ThingWrapperRejectsDisposedThingAccess()
    {
        var wrapper = new UdbScriptThingWrapper(new Thing { IsDisposed = true });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => wrapper.type);

        Assert.Equal("Thing is disposed, the type member can not be accessed.", exception.Message);
    }

    [Fact]
    public void MapWrapperReturnsNonDisposedCoreElements()
    {
        var map = new MapSet();
        var vertex = map.AddVertex(new Vector2D(0, 0));
        map.Vertices.Add(new Vertex { IsDisposed = true });
        var line = map.AddLinedef(vertex, map.AddVertex(new Vector2D(64, 0)));
        map.Linedefs.Add(new Linedef { IsDisposed = true });
        var sector = map.AddSector();
        var side = map.AddSidedef(line, isFront: true, sector);
        map.Sidedefs.Add(new Sidedef { IsDisposed = true });
        map.Sectors.Add(new Sector { IsDisposed = true });
        var thing = map.AddThing(new Vector2D(32, 32), 3001);
        map.Things.Add(new Thing { IsDisposed = true });
        var wrapper = new UdbScriptMapWrapper(map);

        UdbScriptVertexWrapper[] vertices = wrapper.getVertices();
        UdbScriptLinedefWrapper[] linedefs = wrapper.getLinedefs();
        UdbScriptSidedefWrapper[] sidedefs = wrapper.getSidedefs();
        UdbScriptSectorWrapper[] sectors = wrapper.getSectors();
        UdbScriptThingWrapper[] things = wrapper.getThings();

        Assert.Equal(2, vertices.Length);
        Assert.Same(vertex, vertices[0].Vertex);
        Assert.Same(line, Assert.Single(linedefs).Linedef);
        Assert.Same(side, Assert.Single(sidedefs).Sidedef);
        Assert.Same(sector, Assert.Single(sectors).Sector);
        Assert.Same(thing, Assert.Single(things).Thing);
    }

    [Fact]
    public void MapWrapperRejectsDisposedMapAccess()
    {
        var map = new MapSet();
        map.Dispose();
        var wrapper = new UdbScriptMapWrapper(map);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => wrapper.getVertices());

        Assert.Equal("Map is disposed, the getVertices member can not be accessed.", exception.Message);
    }

    [Fact]
    public void MapWrapperExposesTagAllocationHelpers()
    {
        var map = new MapSet();
        map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0))).Tag = 1;
        map.AddSector().Tag = 3;
        map.AddThing(new Vector2D(32, 32), 3001).Tag = 4;
        var wrapper = new UdbScriptMapWrapper(map);

        Assert.Equal(2, wrapper.getNewTag());
        Assert.Equal(5, wrapper.getNewTag(new[] { 2 }));
        Assert.Equal(new[] { 2, 5, 6 }, wrapper.getMultipleNewTags(3));
    }

    [Fact]
    public void MapWrapperTagAllocationSkipsConfiguredActionArgsLikeUdbScript()
    {
        var config = GameConfiguration.FromText("""
            formatinterface = "UniversalMapSetIO";
            linedeftypes
            {
                tags
                {
                    80
                    {
                        title = "Tag args";
                        arg0 { type = 13; }
                        arg1 { type = 14; }
                    }
                }
            }
            """);
        var map = new MapSet();
        map.AddThing(new Vector2D(32, 32), 3001).Tag = 1;
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        line.Action = 80;
        line.Args[0] = 2;
        var thing = map.AddThing(new Vector2D(48, 48), 3002);
        thing.Action = 80;
        thing.Args[1] = 3;
        var wrapper = new UdbScriptMapWrapper(map, config: config);

        Assert.Equal(4, wrapper.getNewTag());
        Assert.Equal(5, wrapper.getNewTag(new[] { 4 }));
        Assert.Equal(new[] { 4, 5, 6 }, wrapper.getMultipleNewTags(3));
    }

    [Fact]
    public void MapWrapperExposesNearestElementHelpers()
    {
        var map = new MapSet();
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(100, 0));
        var otherVertex = map.AddVertex(new Vector2D(400, 400));
        var line = map.AddLinedef(start, end);
        var sector = map.AddSector();
        var front = map.AddSidedef(line, isFront: true, sector);
        var back = map.AddSidedef(line, isFront: false, sector);
        var nearThing = map.AddThing(new Vector2D(8, -8), 3001);
        map.AddThing(new Vector2D(300, 300), 3002);
        var wrapper = new UdbScriptMapWrapper(map);

        Assert.Same(start, wrapper.nearestVertex(new object[] { 3.0, -2.0 })?.Vertex);
        Assert.Same(otherVertex, wrapper.nearestVertex(new object[] { 390.0, 390.0 })?.Vertex);
        Assert.Same(line, wrapper.nearestLinedef(new UdbScriptVector2DWrapper(50, 12))?.Linedef);
        Assert.Same(nearThing, wrapper.nearestThing(new UdbScriptVector3DWrapper(9, -9, 64))?.Thing);
        Assert.Same(front, wrapper.nearestSidedef(new object[] { 50.0, -8.0 })?.Sidedef);
        Assert.Same(back, wrapper.nearestSidedef(new object[] { 50.0, 8.0 })?.Sidedef);
        Assert.Null(wrapper.nearestVertex(new object[] { 390.0, 390.0 }, maxrange: 4));
        Assert.Null(wrapper.nearestLinedef(new object[] { 50.0, 20.0 }, maxrange: 4));
        Assert.Null(wrapper.nearestThing(new object[] { 50.0, 50.0 }, maxrange: 4));
    }

    [Fact]
    public void MapWrapperExposesMarkClearInvertAndQueryHelpers()
    {
        var map = new MapSet();
        var vertex = map.AddVertex(new Vector2D(0, 0));
        var unmarkedVertex = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(vertex, unmarkedVertex);
        var sector = map.AddSector();
        var side = map.AddSidedef(line, isFront: true, sector);
        var thing = map.AddThing(new Vector2D(16, 16), 3001);
        vertex.Marked = true;
        line.Marked = true;
        side.Marked = true;
        sector.Marked = true;
        thing.Marked = true;
        var wrapper = new UdbScriptMapWrapper(map);

        Assert.Same(vertex, Assert.Single(wrapper.getMarkedVertices()).Vertex);
        Assert.Same(unmarkedVertex, Assert.Single(wrapper.getMarkedVertices(mark: false)).Vertex);
        Assert.Same(line, Assert.Single(wrapper.getMarkedLinedefs()).Linedef);
        Assert.Same(side, Assert.Single(wrapper.getMarkedSidedefs()).Sidedef);
        Assert.Same(sector, Assert.Single(wrapper.getMarkedSectors()).Sector);
        Assert.Same(thing, Assert.Single(wrapper.getMarkedThings()).Thing);

        wrapper.clearAllMarks(mark: false);

        Assert.Empty(wrapper.getMarkedVertices());
        Assert.Empty(wrapper.getMarkedLinedefs());
        Assert.Empty(wrapper.getMarkedSidedefs());
        Assert.Empty(wrapper.getMarkedSectors());
        Assert.Empty(wrapper.getMarkedThings());

        wrapper.invertAllMarks();

        Assert.Equal(2, wrapper.getMarkedVertices().Length);
        Assert.Same(line, Assert.Single(wrapper.getMarkedLinedefs()).Linedef);
        Assert.Same(side, Assert.Single(wrapper.getMarkedSidedefs()).Sidedef);
        Assert.Same(sector, Assert.Single(wrapper.getMarkedSectors()).Sector);
        Assert.Same(thing, Assert.Single(wrapper.getMarkedThings()).Thing);

        wrapper.clearMarkedVertices();
        wrapper.clearMarkeLinedefs();
        wrapper.clearMarkeSidedefs();
        wrapper.clearMarkeSectors();
        wrapper.clearMarkedThings();

        Assert.Empty(wrapper.getMarkedVertices());
        Assert.Empty(wrapper.getMarkedLinedefs());
        Assert.Empty(wrapper.getMarkedSidedefs());
        Assert.Empty(wrapper.getMarkedSectors());
        Assert.Empty(wrapper.getMarkedThings());
    }

    [Fact]
    public void MapWrapperMarksSelectedElements()
    {
        var map = new MapSet();
        var first = map.AddVertex(new Vector2D(0, 0));
        var second = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(first, second);
        var sector = map.AddSector();
        var side = map.AddSidedef(line, isFront: true, sector);
        var thing = map.AddThing(new Vector2D(16, 16), 3001);
        first.Selected = true;
        line.Selected = true;
        side.Selected = true;
        sector.Selected = true;
        thing.Selected = true;
        var wrapper = new UdbScriptMapWrapper(map);

        wrapper.markSelectedVertices();
        wrapper.markSelectedLinedefs();
        wrapper.markSelectedSidedefs();
        wrapper.markSelectedSectors();
        wrapper.markSelectedThings();

        Assert.Same(first, Assert.Single(wrapper.getMarkedVertices()).Vertex);
        Assert.Same(line, Assert.Single(wrapper.getMarkedLinedefs()).Linedef);
        Assert.Same(side, Assert.Single(wrapper.getMarkedSidedefs()).Sidedef);
        Assert.Same(sector, Assert.Single(wrapper.getMarkedSectors()).Sector);
        Assert.Same(thing, Assert.Single(wrapper.getMarkedThings()).Thing);

        wrapper.markSelectedVertices(mark: false);
        wrapper.markSelectedLinedefs(mark: false);
        wrapper.markSelectedSidedefs(mark: false);
        wrapper.markSelectedSectors(mark: false);
        wrapper.markSelectedThings(mark: false);

        Assert.Empty(wrapper.getMarkedVertices());
        Assert.Empty(wrapper.getMarkedLinedefs());
        Assert.Empty(wrapper.getMarkedSidedefs());
        Assert.Empty(wrapper.getMarkedSectors());
        Assert.Empty(wrapper.getMarkedThings());
    }

    [Fact]
    public void MapWrapperExposesSelectedElementHelpers()
    {
        var map = new MapSet();
        var first = map.AddVertex(new Vector2D(0, 0));
        var second = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(first, second);
        var sector = map.AddSector();
        var side = map.AddSidedef(line, isFront: true, sector);
        var unselectedThing = map.AddThing(new Vector2D(16, 16), 3001);
        var selectedThing = map.AddThing(new Vector2D(32, 32), 3002);
        first.Selected = true;
        line.Selected = true;
        side.Selected = true;
        sector.Selected = true;
        selectedThing.Selected = true;
        var wrapper = new UdbScriptMapWrapper(map);

        Assert.Same(first, Assert.Single(wrapper.getSelectedVertices()).Vertex);
        Assert.Same(second, Assert.Single(wrapper.getSelectedVertices(selected: false)).Vertex);
        Assert.Same(line, Assert.Single(wrapper.getSelectedLinedefs()).Linedef);
        Assert.Same(side, Assert.Single(wrapper.getSelectedSidedefs()).Sidedef);
        Assert.Same(side, Assert.Single(wrapper.getSidedefsFromSelectedLinedefs()).Sidedef);
        Assert.Same(sector, Assert.Single(wrapper.getSelectedSectors()).Sector);
        Assert.Same(selectedThing, Assert.Single(wrapper.getSelectedThings()).Thing);
        Assert.Same(unselectedThing, Assert.Single(wrapper.getSelectedThings(selected: false)).Thing);
    }

    [Fact]
    public void MapWrapperClearsSelectedElements()
    {
        var map = new MapSet();
        var first = map.AddVertex(new Vector2D(0, 0));
        var second = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(first, second);
        var sector = map.AddSector();
        var side = map.AddSidedef(line, isFront: true, sector);
        var thing = map.AddThing(new Vector2D(16, 16), 3001);
        first.Selected = true;
        line.Selected = true;
        side.Selected = true;
        sector.Selected = true;
        thing.Selected = true;
        var wrapper = new UdbScriptMapWrapper(map);

        wrapper.clearSelectedVertices();
        wrapper.clearSelectedLinedefs();
        wrapper.clearSelectedSidedefs();
        wrapper.clearSelectedSectors();
        wrapper.clearSelectedThings();

        Assert.Empty(wrapper.getSelectedVertices());
        Assert.Empty(wrapper.getSelectedLinedefs());
        Assert.Empty(wrapper.getSelectedSidedefs());
        Assert.Empty(wrapper.getSelectedSectors());
        Assert.Empty(wrapper.getSelectedThings());

        first.Selected = true;
        line.Selected = true;
        side.Selected = true;
        sector.Selected = true;
        thing.Selected = true;

        wrapper.clearAllSelected();

        Assert.Empty(wrapper.getSelectedVertices());
        Assert.Empty(wrapper.getSelectedLinedefs());
        Assert.Empty(map.GetSelectedSidedefs());
        Assert.Empty(wrapper.getSelectedSectors());
        Assert.Empty(wrapper.getSelectedThings());
    }

    [Fact]
    public void MapWrapperSelectsAllAndInvertsSelectedElements()
    {
        var map = new MapSet();
        var firstVertex = map.AddVertex(new Vector2D(0, 0));
        var secondVertex = map.AddVertex(new Vector2D(64, 0));
        var firstLine = map.AddLinedef(firstVertex, secondVertex);
        var secondLine = map.AddLinedef(map.AddVertex(new Vector2D(128, 0)), map.AddVertex(new Vector2D(192, 0)));
        var firstSector = map.AddSector();
        var secondSector = map.AddSector();
        var firstSide = map.AddSidedef(firstLine, isFront: true, firstSector);
        var secondSide = map.AddSidedef(secondLine, isFront: true, secondSector);
        var firstThing = map.AddThing(new Vector2D(16, 16), 3001);
        var secondThing = map.AddThing(new Vector2D(32, 32), 3002);
        var wrapper = new UdbScriptMapWrapper(map);

        wrapper.selectAllVertices();
        wrapper.selectAllLinedefs();
        wrapper.selectAllSidedefs();
        wrapper.selectAllSectors();
        wrapper.selectAllThings();

        Assert.Equal(4, wrapper.getSelectedVertices().Length);
        Assert.Equal(2, wrapper.getSelectedLinedefs().Length);
        Assert.Equal(2, wrapper.getSelectedSidedefs().Length);
        Assert.Equal(2, wrapper.getSelectedSectors().Length);
        Assert.Equal(2, wrapper.getSelectedThings().Length);

        firstVertex.Selected = true;
        secondVertex.Selected = false;
        firstLine.Selected = true;
        secondLine.Selected = false;
        firstSide.Selected = true;
        secondSide.Selected = false;
        firstSector.Selected = true;
        secondSector.Selected = false;
        firstThing.Selected = true;
        secondThing.Selected = false;

        wrapper.invertSelectedVertices();
        wrapper.invertSelectedLinedefs();
        wrapper.invertSelectedSidedefs();
        wrapper.invertSelectedSectors();
        wrapper.invertSelectedThings();

        Assert.Same(secondVertex, Assert.Single(wrapper.getSelectedVertices()).Vertex);
        Assert.Same(secondLine, Assert.Single(wrapper.getSelectedLinedefs()).Linedef);
        Assert.Same(secondSide, Assert.Single(wrapper.getSelectedSidedefs()).Sidedef);
        Assert.Same(secondSector, Assert.Single(wrapper.getSelectedSectors()).Sector);
        Assert.Same(secondThing, Assert.Single(wrapper.getSelectedThings()).Thing);
    }

    [Fact]
    public void MapWrapperExposesSelectionGroupOperations()
    {
        var map = new MapSet();
        var selectedVertex = map.AddVertex(new Vector2D(0, 0));
        var unselectedVertex = map.AddVertex(new Vector2D(64, 0));
        var selectedLine = map.AddLinedef(selectedVertex, unselectedVertex);
        var unselectedLine = map.AddLinedef(map.AddVertex(new Vector2D(128, 0)), map.AddVertex(new Vector2D(192, 0)));
        var selectedSector = map.AddSector();
        var unselectedSector = map.AddSector();
        var selectedThing = map.AddThing(new Vector2D(16, 16), 3001);
        var unselectedThing = map.AddThing(new Vector2D(32, 32), 3002);
        selectedVertex.Selected = true;
        selectedLine.Selected = true;
        selectedSector.Selected = true;
        selectedThing.Selected = true;
        var wrapper = new UdbScriptMapWrapper(map);

        wrapper.addSelectionToGroup(2);
        wrapper.clearAllSelected();
        wrapper.selectVerticesByGroup(2);
        wrapper.selectLinedefsByGroup(2);
        wrapper.selectSectorsByGroup(2);
        wrapper.selectThingsByGroup(2);

        Assert.Same(selectedVertex, Assert.Single(wrapper.getSelectedVertices()).Vertex);
        Assert.Same(selectedLine, Assert.Single(wrapper.getSelectedLinedefs()).Linedef);
        Assert.Same(selectedSector, Assert.Single(wrapper.getSelectedSectors()).Sector);
        Assert.Same(selectedThing, Assert.Single(wrapper.getSelectedThings()).Thing);
        Assert.False(unselectedVertex.Selected);
        Assert.False(unselectedLine.Selected);
        Assert.False(unselectedSector.Selected);
        Assert.False(unselectedThing.Selected);

        wrapper.clearGroup(2);
        wrapper.selectVerticesByGroup(2);
        wrapper.selectLinedefsByGroup(2);
        wrapper.selectSectorsByGroup(2);
        wrapper.selectThingsByGroup(2);

        Assert.Empty(wrapper.getSelectedVertices());
        Assert.Empty(wrapper.getSelectedLinedefs());
        Assert.Empty(wrapper.getSelectedSectors());
        Assert.Empty(wrapper.getSelectedThings());
    }

    [Fact]
    public void MapWrapperMovesSelectedVerticesAndThings()
    {
        var map = new MapSet();
        var selectedVertex = map.AddVertex(new Vector2D(0, 0));
        var unselectedVertex = map.AddVertex(new Vector2D(64, 0));
        var selectedThing = map.AddThing(new Vector2D(16, 16), 3001);
        var unselectedThing = map.AddThing(new Vector2D(32, 32), 3002);
        selectedVertex.Selected = true;
        selectedThing.Selected = true;
        var wrapper = new UdbScriptMapWrapper(map);

        int movedVertices = wrapper.moveSelectedVerticesBy(new object[] { 8.0, -4.0 });
        int movedThings = wrapper.moveSelectedThingsBy(new UdbScriptVector2DWrapper(-2, 6));

        Assert.Equal(1, movedVertices);
        Assert.Equal(1, movedThings);
        Assert.Equal(new Vector2D(8, -4), selectedVertex.Position);
        Assert.Equal(new Vector2D(64, 0), unselectedVertex.Position);
        Assert.Equal(new Vector2D(14, 22), selectedThing.Position);
        Assert.Equal(new Vector2D(32, 32), unselectedThing.Position);
    }

    [Fact]
    public void MapWrapperFlipsSelectedLinedefsAndSidedefs()
    {
        var map = new MapSet();
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(start, end);
        var frontSector = map.AddSector();
        var backSector = map.AddSector();
        var front = map.AddSidedef(line, isFront: true, frontSector);
        var back = map.AddSidedef(line, isFront: false, backSector);
        line.Selected = true;
        var wrapper = new UdbScriptMapWrapper(map);

        int flippedLines = wrapper.flipSelectedLinedefs();

        Assert.Equal(1, flippedLines);
        Assert.Same(end, line.Start);
        Assert.Same(start, line.End);
        Assert.Same(back, line.Front);
        Assert.Same(front, line.Back);

        int flippedSides = wrapper.flipSelectedSidedefs();

        Assert.Equal(1, flippedSides);
        Assert.Same(front, line.Front);
        Assert.Same(back, line.Back);
        Assert.Same(end, line.Start);
        Assert.Same(start, line.End);
    }

    [Fact]
    public void MapWrapperCreatesVerticesAndThings()
    {
        var map = new MapSet();
        var wrapper = new UdbScriptMapWrapper(map);

        UdbScriptVertexWrapper vertex = wrapper.createVertex(new object[] { 16.0, 32.0 });
        UdbScriptThingWrapper thing = wrapper.createThing(new UdbScriptVector3DWrapper(64, 96, 12), type: 3001);

        Assert.Same(vertex.Vertex, Assert.Single(map.Vertices));
        Assert.Equal(new Vector2D(16, 32), vertex.Vertex.Position);
        Assert.Same(thing.Thing, Assert.Single(map.Things));
        Assert.Equal(new Vector2D(64, 96), thing.Thing.Position);
        Assert.Equal(12, thing.Thing.Height);
        Assert.Equal(3001, thing.Thing.Type);
        Assert.Equal(new UdbScriptVector3DWrapper(64, 96, 12), thing.position);
    }

    [Fact]
    public void MapWrapperCreateThingAppliesConfiguredCleanThingSettingsLikeUdbScript()
    {
        var config = GameConfiguration.FromText("""
            thingflags
            {
                skill1 = "Skill 1";
                ambush = "Ambush";
            }
            defaultthingflags
            {
                skill1;
                ambush;
            }
            thingtypes
            {
                monsters
                {
                    title = "Monsters";
                    3001
                    {
                        title = "Imp";
                        arg0 { default = 7; }
                        arg2 { default = 12; }
                    }
                }
            }
            """);
        var map = new MapSet();
        var wrapper = new UdbScriptMapWrapper(map, mapFormat: MapFormat.Udmf, config: config);

        UdbScriptThingWrapper thing = wrapper.createThing(new UdbScriptVector3DWrapper(64, 96, 12), type: 3001);

        Assert.Equal(3001, thing.Thing.Type);
        Assert.Equal(new Vector2D(64, 96), thing.Thing.Position);
        Assert.Equal(12, thing.Thing.Height);
        Assert.True(thing.Thing.IsFlagSet("skill1"));
        Assert.True(thing.Thing.IsFlagSet("ambush"));
        Assert.Equal(new[] { 7, 0, 12, 0, 0 }, thing.Thing.Args);
    }

    [Fact]
    public void MapWrapperCreateThingAppliesConfiguredNumericFlagsLikeUdbScript()
    {
        var config = GameConfiguration.FromText("""
            thingflags
            {
                1 = "Easy";
                2 = "Medium";
            }
            defaultthingflags
            {
                1;
                2;
            }
            thingtypes
            {
                monsters
                {
                    title = "Monsters";
                    3001 { title = "Imp"; }
                }
            }
            """);
        var map = new MapSet();
        var wrapper = new UdbScriptMapWrapper(map, mapFormat: MapFormat.Doom, config: config);

        UdbScriptThingWrapper thing = wrapper.createThing(new object[] { 64.0, 96.0 }, type: 3001);

        Assert.Equal(3, thing.Thing.Flags);
        Assert.True(thing.flags["1"]);
        Assert.True(thing.flags["2"]);
        Assert.Empty(thing.Thing.UdmfFlags);
    }

    [Fact]
    public void MapWrapperCreateThingAppliesActorUserVariableDefaultsLikeUdbScript()
    {
        var config = GameConfiguration.FromText("");
        var actor = new ActorInfo { ClassName = "DefaultUserVarThing" };
        actor.UserVariables["user_score"] = new ActorUserVariable("user_score", UniversalType.Integer, 11);
        actor.UserVariables["user_speed"] = new ActorUserVariable("user_speed", UniversalType.Float, 1.5);
        actor.UserVariables["user_enabled"] = new ActorUserVariable("user_enabled", UniversalType.Boolean, false);
        actor.UserVariables["user_label"] = new ActorUserVariable("user_label", UniversalType.String, "active");
        actor.UserVariables["user_unset"] = new ActorUserVariable("user_unset", UniversalType.Integer);
        config.MergeActors(new[] { actor }, new Dictionary<int, string> { [3001] = "DefaultUserVarThing" });
        var map = new MapSet();
        var wrapper = new UdbScriptMapWrapper(map, mapFormat: MapFormat.Udmf, config: config);

        UdbScriptThingWrapper thing = wrapper.createThing(new object[] { 64.0, 96.0 }, type: 3001);

        Assert.Equal(11, thing.Thing.Fields["user_score"]);
        Assert.Equal(1.5, thing.Thing.Fields["user_speed"]);
        Assert.Equal(false, thing.Thing.Fields["user_enabled"]);
        Assert.Equal("active", thing.Thing.Fields["user_label"]);
        Assert.False(thing.Thing.Fields.ContainsKey("user_unset"));
    }

    [Fact]
    public void MapOwnedThingWrappersDeleteThings()
    {
        var map = new MapSet();
        var wrapper = new UdbScriptMapWrapper(map);
        UdbScriptThingWrapper created = wrapper.createThing(new object[] { 8.0, 16.0 }, type: 3001);

        created.delete();
        created.delete();

        Assert.Empty(map.Things);
        Assert.True(created.Thing.IsDisposed);

        var remaining = map.AddThing(new Vector2D(32, 32), 3002);
        UdbScriptThingWrapper nearest = wrapper.nearestThing(new object[] { 32.0, 32.0 })!;

        nearest.delete();

        Assert.DoesNotContain(remaining, map.Things);
        Assert.Empty(wrapper.getThings());
    }

    [Fact]
    public void MapWrapperRejectsInvalidCreationAccess()
    {
        var wrapper = new UdbScriptMapWrapper(new MapSet());

        InvalidOperationException negativeType = Assert.Throws<InvalidOperationException>(
            () => wrapper.createThing(new object[] { 0.0, 0.0 }, type: -1));

        Assert.Equal("Thing type can not be negative.", negativeType.Message);
    }

    [Fact]
    public void MapWrapperSnapsAllElementsToAccuracy()
    {
        var map = new MapSet();
        var vertex = map.AddVertex(new Vector2D(1.2345, 6.789));
        var thing = map.AddThing(new Vector2D(3.456, 9.876), 1);
        thing.Height = 5.4321;
        var wrapper = new UdbScriptMapWrapper(map);

        wrapper.snapAllToAccuracy();

        Assert.Equal(new Vector2D(1.234, 6.789), vertex.Position);
        Assert.Equal(new Vector2D(3.456, 9.876), thing.Position);
        Assert.Equal(5.432, thing.Height);

        vertex.Move(1.2345, 6.789);
        thing.Move(new Vector2D(3.456, 9.876));
        thing.Height = 5.4321;
        wrapper.snapAllToAccuracy(2);

        Assert.Equal(new Vector2D(1.23, 6.79), vertex.Position);
        Assert.Equal(new Vector2D(3.46, 9.88), thing.Position);
        Assert.Equal(5.43, thing.Height);

        vertex.Move(1.6, 6.4);
        thing.Move(new Vector2D(3.4, 9.6));
        thing.Height = 5.6;
        wrapper.snapAllToAccuracy(2, usePrecisePosition: false);

        Assert.Equal(new Vector2D(2, 6), vertex.Position);
        Assert.Equal(new Vector2D(3, 10), thing.Position);
        Assert.Equal(6, thing.Height);

        vertex.Move(1.6, 6.4);
        thing.Move(new Vector2D(3.4, 9.6));
        thing.Height = 5.6;
        wrapper.snapAllToAccuracy(false);

        Assert.Equal(new Vector2D(2, 6), vertex.Position);
        Assert.Equal(new Vector2D(3, 10), thing.Position);
        Assert.Equal(6, thing.Height);
    }

    [Fact]
    public void MapWrapperSnapsPointToCurrentGrid()
    {
        var grid = new GridSetup();
        grid.SetGridSize(10);
        grid.SetGridOrigin(100, 50);
        grid.SetGridRotation(Angle2D.PIHALF);
        var wrapper = new UdbScriptMapWrapper(new MapSet(), grid);

        UdbScriptVector2DWrapper snapped = wrapper.snappedToGrid(new object[] { 100.0, 61.0 });

        Assert.Equal(100, snapped.x, 6);
        Assert.Equal(60, snapped.y, 6);
    }

    [Fact]
    public void VertexAndThingWrappersSnapToCurrentGrid()
    {
        var map = new MapSet();
        Vertex start = map.AddVertex(new Vector2D(13, 27));
        Vertex end = map.AddVertex(new Vector2D(51, 9));
        map.AddLinedef(start, end);
        Thing thing = map.AddThing(new Vector2D(24, 36), 3001);
        thing.Height = 7.5;
        var grid = new GridSetup();
        grid.SetGridSize(16);
        var wrapper = new UdbScriptMapWrapper(map, grid);

        wrapper.getVertices()[0].snapToGrid();
        wrapper.getThings()[0].snapToGrid();
        wrapper.getLinedefs()[0].end.snapToGrid();

        Assert.Equal(new Vector2D(16, 32), start.Position);
        Assert.Equal(new Vector2D(48, 16), end.Position);
        Assert.Equal(new Vector2D(32, 32), thing.Position);
        Assert.Equal(7.5, thing.Height);
    }

    [Fact]
    public void MapWrapperDrawLinesCreatesOpenPath()
    {
        var map = new MapSet();
        var wrapper = new UdbScriptMapWrapper(map);

        bool success = wrapper.drawLines(new object[]
        {
            new object[] { 0.0, 0.0 },
            new UdbScriptVector2DWrapper(64, 0),
            new object[] { 64.0, 64.0 },
        });

        Assert.True(success);
        Assert.Equal(3, map.Vertices.Count);
        Assert.Equal(2, map.Linedefs.Count);
        Assert.Empty(map.Sectors);
        Assert.All(map.Vertices, vertex => Assert.True(vertex.Marked));
        Assert.All(map.Linedefs, line => Assert.True(line.Marked));
    }

    [Fact]
    public void MapWrapperDrawLinesCreatesClosedSectorLoop()
    {
        var map = new MapSet();
        var wrapper = new UdbScriptMapWrapper(map);

        bool success = wrapper.drawLines(new object[]
        {
            new object[] { 0.0, 0.0 },
            new object[] { 64.0, 0.0 },
            new object[] { 64.0, 64.0 },
            new object[] { 0.0, 64.0 },
            new object[] { 0.0, 0.0 },
        });

        Sector sector = Assert.Single(map.Sectors);
        Assert.True(success);
        Assert.Equal(4, map.Vertices.Count);
        Assert.Equal(4, map.Linedefs.Count);
        Assert.Equal(4, sector.Sidedefs.Count);
        Assert.True(sector.Marked);
        Assert.All(map.Vertices, vertex => Assert.True(vertex.Marked));
        Assert.All(map.Linedefs, line => Assert.True(line.Marked));
    }

    [Fact]
    public void CommonUdbScriptMapEditingSnippetUsesMapWrappersTogether()
    {
        var map = new MapSet();
        var wrapper = new UdbScriptMapWrapper(map);

        bool success = wrapper.drawLines(new object[]
        {
            new object[] { 0.0, 0.0 },
            new UdbScriptVector2DWrapper(64, 0),
            new object[] { 64.0, 64.0 },
            new object[] { 0.0, 64.0 },
            new object[] { 0.0, 0.0 },
        });
        int tag = wrapper.getNewTag();
        UdbScriptSectorWrapper sector = Assert.Single(wrapper.getMarkedSectors());
        UdbScriptLinedefWrapper[] outline = wrapper.getMarkedLinedefs();

        sector.addTag(tag);
        foreach (UdbScriptLinedefWrapper line in outline)
        {
            line.addTag(tag);
            line.selected = true;
        }

        wrapper.markSelectedLinedefs();
        UdbScriptThingWrapper thing = wrapper.createThing(new UdbScriptVector3DWrapper(32, 32, 8), type: 3001);
        thing.selected = true;
        int movedThings = wrapper.moveSelectedThingsBy(new object[] { 16.0, -8.0 });

        Assert.True(success);
        Assert.Equal(4, wrapper.getVertices().Length);
        Assert.Equal(4, wrapper.getLinedefs().Length);
        Assert.Single(wrapper.getSectors());
        Assert.Single(wrapper.getThings());
        Assert.Equal(new[] { tag }, sector.getTags());
        Assert.All(outline, line => Assert.Contains(tag, line.getTags()));
        Assert.Equal(4, wrapper.getSelectedLinedefs().Length);
        Assert.Equal(4, wrapper.getMarkedLinedefs().Length);
        Assert.Equal(1, movedThings);
        Assert.Equal(new UdbScriptVector3DWrapper(48, 24, 8), thing.position);
        Assert.Same(thing.Thing, wrapper.nearestThing(new object[] { 48.0, 24.0 })!.Thing);
        UdbScriptLinedefWrapper nearestLine = wrapper.nearestLinedef(new object[] { 32.0, 0.0 })!;
        Assert.Contains(outline, line => ReferenceEquals(line.Linedef, nearestLine.Linedef));
    }

    [Fact]
    public void MapWrapperDrawLinesRejectsInvalidInput()
    {
        var wrapper = new UdbScriptMapWrapper(new MapSet());

        InvalidOperationException nonArray = Assert.Throws<InvalidOperationException>(
            () => wrapper.drawLines("bad"));
        InvalidOperationException tooShort = Assert.Throws<InvalidOperationException>(
            () => wrapper.drawLines(Array.Empty<object>()));

        Assert.Equal("Data must be supplied as an array", nonArray.Message);
        Assert.Equal("Array must have at least 2 values", tooShort.Message);
    }

    [Fact]
    public void MapWrapperExposesHighlightedElements()
    {
        var (map, first, _, shared) = CreateTwoSharedSectors();
        var vertex = shared.Start;
        var thing = map.AddThing(new Vector2D(16, 16), 3001);

        Assert.Same(vertex, new UdbScriptMapWrapper(map, highlightedObject: vertex).getHighlightedVertex()!.Vertex);
        Assert.Same(thing, new UdbScriptMapWrapper(map, highlightedObject: thing).getHighlightedThing()!.Thing);
        Assert.Same(first, new UdbScriptMapWrapper(map, highlightedObject: first).getHighlightedSector()!.Sector);
        Assert.Same(shared, new UdbScriptMapWrapper(map, highlightedObject: shared).getHighlightedLinedef()!.Linedef);
        Assert.Same(shared, new UdbScriptMapWrapper(map, highlightedObject: shared.Front).getHighlightedLinedef()!.Linedef);
        Assert.Same(shared.Front, new UdbScriptMapWrapper(map, highlightedObject: shared.Front).getHighlightedSidedef()!.Sidedef);
        Assert.Null(new UdbScriptMapWrapper(map, highlightedObject: shared).getHighlightedSidedef());
    }

    [Fact]
    public void MapWrapperThreadsHighlightedSurfaceStateToNestedWrappers()
    {
        var (map, first, _, shared) = CreateTwoSharedSectors();
        Sidedef side = shared.Front!;
        var sidePart = new UdbScriptHighlightedSidedefPart(side, SidedefPart.Middle);
        var sectorSurface = new UdbScriptHighlightedSectorSurface(first, FloorHighlighted: true, CeilingHighlighted: false);

        UdbScriptSidedefWrapper sideWrapper = Assert.Single(
            new UdbScriptMapWrapper(map, highlightedObject: sidePart).getSidedefs(),
            wrapper => ReferenceEquals(side, wrapper.Sidedef));
        UdbScriptSectorWrapper sectorWrapper = Assert.Single(
            new UdbScriptMapWrapper(map, highlightedObject: sectorSurface).getSectors(),
            wrapper => ReferenceEquals(first, wrapper.Sector));

        Assert.False(sideWrapper.upperHighlighted);
        Assert.True(sideWrapper.middleHighlighted);
        Assert.False(sideWrapper.lowerHighlighted);
        Assert.Same(shared, new UdbScriptMapWrapper(map, highlightedObject: sidePart).getHighlightedLinedef()!.Linedef);
        Assert.True(new UdbScriptMapWrapper(map, highlightedObject: sidePart).getHighlightedSidedef()!.middleHighlighted);
        Assert.True(new UdbScriptSidedefWrapper(side, highlightedObject: side).upperHighlighted);
        Assert.True(new UdbScriptSidedefWrapper(side, highlightedObject: side).middleHighlighted);
        Assert.True(new UdbScriptSidedefWrapper(side, highlightedObject: side).lowerHighlighted);
        Assert.True(sectorWrapper.floorHighlighted);
        Assert.False(sectorWrapper.ceilingHighlighted);
        Assert.Same(first, new UdbScriptMapWrapper(map, highlightedObject: sectorSurface).getHighlightedSector()!.Sector);
        Assert.True(new UdbScriptSectorWrapper(first, highlightedObject: first).floorHighlighted);
        Assert.True(new UdbScriptSectorWrapper(first, highlightedObject: first).ceilingHighlighted);
    }

    [Fact]
    public void MapWrapperSelectedOrHighlightedUsesSelectedElementsFirst()
    {
        var map = new MapSet();
        var selectedVertex = map.AddVertex(new Vector2D(0, 0));
        var highlightedVertex = map.AddVertex(new Vector2D(64, 0));
        selectedVertex.Selected = true;
        var selectedThing = map.AddThing(new Vector2D(0, 32), 3001);
        var highlightedThing = map.AddThing(new Vector2D(64, 32), 3002);
        selectedThing.Selected = true;
        var selectedSector = map.AddSector();
        var highlightedSector = map.AddSector();
        selectedSector.Selected = true;
        var selectedLine = map.AddLinedef(map.AddVertex(new Vector2D(0, 64)), map.AddVertex(new Vector2D(32, 64)));
        var highlightedLine = map.AddLinedef(map.AddVertex(new Vector2D(64, 64)), map.AddVertex(new Vector2D(96, 64)));
        selectedLine.Selected = true;
        var selectedSide = map.AddSidedef(selectedLine, isFront: true, selectedSector);
        var highlightedSide = map.AddSidedef(highlightedLine, isFront: true, highlightedSector);
        selectedSide.Selected = true;
        var wrapper = new UdbScriptMapWrapper(map, highlightedObject: highlightedVertex);

        Assert.Same(selectedVertex, Assert.Single(wrapper.getSelectedOrHighlightedVertices()).Vertex);
        Assert.Same(selectedThing, Assert.Single(new UdbScriptMapWrapper(map, highlightedObject: highlightedThing).getSelectedOrHighlightedThings()).Thing);
        Assert.Same(selectedSector, Assert.Single(new UdbScriptMapWrapper(map, highlightedObject: highlightedSector).getSelectedOrHighlightedSectors()).Sector);
        Assert.Same(selectedLine, Assert.Single(new UdbScriptMapWrapper(map, highlightedObject: highlightedLine).getSelectedOrHighlightedLinedefs()).Linedef);
        Assert.Same(selectedSide, Assert.Single(new UdbScriptMapWrapper(map, highlightedObject: highlightedSide).getSelectedOrHighlightedSidedefs()).Sidedef);
    }

    [Fact]
    public void MapWrapperSelectedOrHighlightedFallsBackToHighlightedElements()
    {
        var (map, first, _, shared) = CreateTwoSharedSectors();
        var vertex = shared.Start;
        var thing = map.AddThing(new Vector2D(16, 16), 3001);

        Assert.Same(vertex, Assert.Single(new UdbScriptMapWrapper(map, highlightedObject: vertex).getSelectedOrHighlightedVertices()).Vertex);
        Assert.Same(thing, Assert.Single(new UdbScriptMapWrapper(map, highlightedObject: thing).getSelectedOrHighlightedThings()).Thing);
        Assert.Same(first, Assert.Single(new UdbScriptMapWrapper(map, highlightedObject: first).getSelectedOrHighlightedSectors()).Sector);
        Assert.Same(shared, Assert.Single(new UdbScriptMapWrapper(map, highlightedObject: shared).getSelectedOrHighlightedLinedefs()).Linedef);
        Assert.Same(shared.Front, Assert.Single(new UdbScriptMapWrapper(map, highlightedObject: shared.Front).getSelectedOrHighlightedSidedefs()).Sidedef);
    }

    [Fact]
    public void MapWrapperGetsSidedefsFromSelectedOrHighlightedLinedefs()
    {
        var (map, _, _, shared) = CreateTwoSharedSectors();
        UdbScriptSidedefWrapper[] highlighted = new UdbScriptMapWrapper(map, highlightedObject: shared)
            .getSidedefsFromSelectedOrHighlightedLinedefs();

        Assert.Contains(highlighted, wrapper => ReferenceEquals(shared.Front, wrapper.Sidedef));
        Assert.Contains(highlighted, wrapper => ReferenceEquals(shared.Back, wrapper.Sidedef));

        shared.Selected = true;
        UdbScriptSidedefWrapper[] selected = new UdbScriptMapWrapper(map)
            .getSidedefsFromSelectedOrHighlightedLinedefs();

        Assert.Equal(2, selected.Length);
    }

    [Fact]
    public void MapWrapperExposesMapFormatFlags()
    {
        var doom = new UdbScriptMapWrapper(new MapSet());
        var hexen = new UdbScriptMapWrapper(new MapSet(), mapFormat: MapFormat.Hexen);
        var udmf = new UdbScriptMapWrapper(new MapSet(), mapFormat: MapFormat.Udmf);

        Assert.True(doom.isDoom);
        Assert.False(doom.isHexen);
        Assert.False(doom.isUDMF);
        Assert.False(hexen.isDoom);
        Assert.True(hexen.isHexen);
        Assert.False(hexen.isUDMF);
        Assert.False(udmf.isDoom);
        Assert.False(udmf.isHexen);
        Assert.True(udmf.isUDMF);
    }

    [Fact]
    public void MapWrapperUsesNumericFlagsForClassicLinedefsAndThings()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        var thing = map.AddThing(new Vector2D(16, 16), 3001);
        var wrapper = new UdbScriptMapWrapper(map, mapFormat: MapFormat.Hexen);

        UdbScriptFlagsWrapper lineFlags = Assert.Single(wrapper.getLinedefs()).flags;
        UdbScriptFlagsWrapper thingFlags = Assert.Single(wrapper.getThings()).flags;

        lineFlags["64"] = true;
        thingFlags["8"] = true;

        Assert.Equal(64, line.Flags);
        Assert.Equal(8, thing.Flags);
        Assert.False(line.IsFlagSet("64"));
        Assert.False(thing.IsFlagSet("8"));
        Assert.True(lineFlags["64"]);
        Assert.True(thingFlags.TryGetValue("8", out bool ambush));
        Assert.True(ambush);
        Assert.Contains(new KeyValuePair<string, bool>("64", true), lineFlags);

        lineFlags["64"] = false;
        thingFlags.Clear();

        Assert.Equal(0, line.Flags);
        Assert.Equal(0, thing.Flags);
    }

    [Fact]
    public void MapWrapperUsesNumericFlagsThroughNestedClassicLinedefPaths()
    {
        var (map, first, _, shared) = CreateTwoSharedSectors();
        var wrapper = new UdbScriptMapWrapper(map, mapFormat: MapFormat.Hexen);

        UdbScriptSidedefWrapper lineSide = Assert.IsType<UdbScriptSidedefWrapper>(Assert.Single(wrapper.getLinedefs()).front);
        lineSide.line.flags["32"] = true;

        Assert.Equal(32, shared.Flags);

        shared.Flags = 0;
        UdbScriptSidedefWrapper mapSide = Assert.Single(wrapper.getSidedefs(), side => ReferenceEquals(shared.Front, side.Sidedef));
        mapSide.line.flags["64"] = true;

        Assert.Equal(64, shared.Flags);
        Assert.False(shared.IsFlagSet("64"));

        shared.Flags = 0;
        UdbScriptSectorWrapper sector = Assert.Single(wrapper.getSectors(), sector => ReferenceEquals(first, sector.Sector));
        UdbScriptSidedefWrapper sectorSide = Assert.Single(sector.getSidedefs());
        sectorSide.line.flags["128"] = true;

        Assert.Equal(128, shared.Flags);
        Assert.False(shared.IsFlagSet("128"));
    }

    [Fact]
    public void MapWrapperRejectsNamedClassicFlags()
    {
        var map = new MapSet();
        map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        var wrapper = new UdbScriptMapWrapper(map, mapFormat: MapFormat.Doom);
        UdbScriptFlagsWrapper flags = Assert.Single(wrapper.getLinedefs()).flags;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => flags["blocking"] = true);

        Assert.Equal("Flag name 'blocking' is not valid.", exception.Message);
    }

    [Fact]
    public void MapWrapperExposesMousePosition()
    {
        var wrapper = new UdbScriptMapWrapper(new MapSet(), mousePosition: new Vector2D(12, -34));

        Assert.Equal(12, wrapper.mousePosition.x);
        Assert.Equal(-34, wrapper.mousePosition.y);
    }

    [Fact]
    public void VisualCameraWrapperExposesPose()
    {
        var wrapper = new UdbScriptVisualCameraWrapper(
            new VisualCameraPose(new Vector3D(1, 2, 3), 0.5, -0.25));

        Assert.Equal(new UdbScriptVector3DWrapper(1, 2, 3), wrapper.position);
        Assert.Equal(0.5, wrapper.angleXY);
        Assert.Equal(-0.25, wrapper.angleZ);
    }

    [Fact]
    public void MapWrapperExposesVisualCamera()
    {
        var pose = new VisualCameraPose(new Vector3D(64, 96, 32), 1.25, -0.5);
        var wrapper = new UdbScriptMapWrapper(new MapSet(), visualCamera: pose);

        Assert.Equal(new UdbScriptVector3DWrapper(64, 96, 32), wrapper.camera.position);
        Assert.Equal(1.25, wrapper.camera.angleXY);
        Assert.Equal(-0.5, wrapper.camera.angleZ);
    }

    [Fact]
    public void MapWrapperJoinsSectors()
    {
        var (map, first, second, shared) = CreateTwoSharedSectors();
        var wrapper = new UdbScriptMapWrapper(map);

        wrapper.joinSectors(new[] { new UdbScriptSectorWrapper(first), new UdbScriptSectorWrapper(second) });

        Sector remaining = Assert.Single(map.Sectors);
        Assert.Same(first, remaining);
        Assert.Same(first, shared.Front!.Sector);
        Assert.Same(first, shared.Back!.Sector);
        Assert.Single(map.Linedefs);
    }

    [Fact]
    public void MapWrapperMergesSectors()
    {
        var (map, first, second, _) = CreateTwoSharedSectors();
        var wrapper = new UdbScriptMapWrapper(map);

        wrapper.mergeSectors(new[] { new UdbScriptSectorWrapper(first), new UdbScriptSectorWrapper(second) });

        Assert.Same(first, Assert.Single(map.Sectors));
        Assert.Empty(map.Linedefs);
        Assert.Empty(map.Vertices);
    }

    [Fact]
    public void MapWrapperStitchesSelectedGeometry()
    {
        var map = new MapSet();
        var fixedVertex = map.AddVertex(new Vector2D(0, 0));
        var movingVertex = map.AddVertex(new Vector2D(0.25, 0));
        movingVertex.Selected = true;
        var wrapper = new UdbScriptMapWrapper(map);

        bool changed = wrapper.stitchGeometry();

        Assert.True(changed);
        Assert.Equal(new Vector2D(0, 0), movingVertex.Position);
        Assert.Contains(movingVertex, map.Vertices);
        Assert.DoesNotContain(fixedVertex, map.Vertices);
    }

    [Fact]
    public void MapWrapperRejectsUnknownStitchMode()
    {
        var wrapper = new UdbScriptMapWrapper(new MapSet());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => wrapper.stitchGeometry((UdbScriptMapWrapper.MergeGeometryMode)99));

        Assert.Equal("Unknown MergeGeometryMode value", exception.Message);
    }

    [Fact]
    public void BlockMapWrapperReturnsBlockEntryElements()
    {
        var (map, line, sector, thing, vertex) = CreateBlockMapFixture(64);
        var wrapper = new UdbScriptBlockMapWrapper(map);

        UdbScriptBlockEntryWrapper block = wrapper.getBlockAt(new object[] { 32.0, 32.0 });

        Assert.Contains(block.getLinedefs(), item => ReferenceEquals(line, item.Linedef));
        Assert.Same(sector, Assert.Single(block.getSectors()).Sector);
        Assert.Same(thing, Assert.Single(block.getThings()).Thing);
        Assert.Contains(block.getVertices(), item => ReferenceEquals(vertex, item.Vertex));
        Assert.Equal(map.IndexOfLinedef(line), block.getLinedefs().First(item => ReferenceEquals(line, item.Linedef)).index);
        Assert.Equal(map.IndexOfSector(sector), Assert.Single(block.getSectors()).index);
        Assert.Equal(map.IndexOfThing(thing), Assert.Single(block.getThings()).index);
        Assert.Equal(map.IndexOfVertex(vertex), block.getVertices().First(item => ReferenceEquals(vertex, item.Vertex)).index);
    }

    [Fact]
    public void BlockMapContentWrappersUseOwningMapForMutations()
    {
        var (map, line, _, thing, _) = CreateBlockMapFixture(64);
        var wrapper = new UdbScriptBlockMapWrapper(map);
        UdbScriptBlockEntryWrapper block = wrapper.getBlockAt(new object[] { 32.0, 32.0 });

        block.getThings().Single().delete();
        UdbScriptLinedefWrapper lineWrapper = block.getLinedefs().First(item => ReferenceEquals(line, item.Linedef));
        lineWrapper.delete();

        Assert.DoesNotContain(thing, map.Things);
        Assert.DoesNotContain(line, map.Linedefs);
        Assert.True(thing.IsDisposed);
        Assert.True(line.IsDisposed);
    }

    [Fact]
    public void BlockMapWrapperCachesBlockEntryAndContentWrappers()
    {
        var (map, line, _, _, _) = CreateBlockMapFixture(64);
        var wrapper = new UdbScriptBlockMapWrapper(map);

        UdbScriptBlockEntryWrapper firstBlock = wrapper.getBlockAt(new object[] { 32.0, 32.0 });
        UdbScriptBlockEntryWrapper secondBlock = wrapper.getBlockAt(new UdbScriptVector2DWrapper(32, 32));
        UdbScriptLinedefWrapper[] firstLines = firstBlock.getLinedefs();
        UdbScriptLinedefWrapper[] secondLines = firstBlock.getLinedefs();
        UdbScriptBlockMapQueryResult result = wrapper.getLineBlocks(new object[] { 0.0, 0.0 }, new object[] { 64.0, 0.0 });
        UdbScriptBlockEntryWrapper firstQueryBlock = result.First();
        UdbScriptBlockEntryWrapper secondQueryBlock = result.First();

        Assert.Same(firstBlock, secondBlock);
        Assert.Same(firstLines, secondLines);
        Assert.Same(firstLines[0], secondLines[0]);
        Assert.Same(line, firstLines[0].Linedef);
        Assert.Same(firstQueryBlock, secondQueryBlock);
        Assert.Same(result.getLinedefs(), result.getLinedefs());
    }

    [Fact]
    public void BlockMapWrapperHonorsElementTypeSelection()
    {
        var (map, _, _, thing, _) = CreateBlockMapFixture(64);
        var wrapper = new UdbScriptBlockMapWrapper(
            map,
            lines: false,
            things: true,
            sectors: false,
            vertices: false);

        UdbScriptBlockEntryWrapper block = wrapper.getBlockAt(new UdbScriptVector2DWrapper(32, 32));

        Assert.Empty(block.getLinedefs());
        Assert.Same(thing, Assert.Single(block.getThings()).Thing);
        Assert.Empty(block.getSectors());
        Assert.Empty(block.getVertices());
    }

    [Fact]
    public void BlockMapContentWrappersUseClassicFlagFormat()
    {
        var (map, line, _, thing, _) = CreateBlockMapFixture(64);
        var wrapper = new UdbScriptBlockMapWrapper(map, mapFormat: MapFormat.Hexen);
        UdbScriptBlockEntryWrapper block = wrapper.getBlockAt(new UdbScriptVector2DWrapper(32, 32));

        UdbScriptFlagsWrapper lineFlags = block.getLinedefs().First(item => ReferenceEquals(line, item.Linedef)).flags;
        UdbScriptFlagsWrapper thingFlags = Assert.Single(block.getThings()).flags;

        lineFlags["64"] = true;
        thingFlags["8"] = true;

        Assert.Equal(64, line.Flags);
        Assert.Equal(8, thing.Flags);
        Assert.False(line.IsFlagSet("64"));
        Assert.False(thing.IsFlagSet("8"));
    }

    [Fact]
    public void BlockMapWrapperHonorsOptionsObjectSelection()
    {
        var (map, _, sector, _, vertex) = CreateBlockMapFixture(64);
        var wrapper = new UdbScriptBlockMapWrapper(
            map,
            new Dictionary<string, object>
            {
                ["lines"] = false,
                ["things"] = false,
                ["sectors"] = true,
                ["vertices"] = true,
            });

        UdbScriptBlockEntryWrapper block = wrapper.getBlockAt(new UdbScriptVector2DWrapper(32, 32));

        Assert.Empty(block.getLinedefs());
        Assert.Empty(block.getThings());
        Assert.Same(sector, Assert.Single(block.getSectors()).Sector);
        Assert.Contains(block.getVertices(), item => ReferenceEquals(vertex, item.Vertex));
    }

    [Fact]
    public void BlockMapQueryResultReturnsUniqueElementsAndEnumeratesBlocks()
    {
        var map = new MapSet();
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(256, 0));
        var line = map.AddLinedef(start, end);
        var wrapper = new UdbScriptBlockMapWrapper(map);

        UdbScriptBlockMapQueryResult result = wrapper.getLineBlocks(new object[] { 0.0, 0.0 }, new object[] { 256.0, 0.0 });

        Assert.True(result.Count() > 1);
        Assert.Same(line, Assert.Single(result.getLinedefs()).Linedef);
        Assert.Equal(2, result.getVertices().Length);
    }

    [Fact]
    public void BlockMapWrapperReturnsRectangleQueryResult()
    {
        var (map, _, sector, _, _) = CreateBlockMapFixture(64);
        var wrapper = new UdbScriptBlockMapWrapper(map);

        UdbScriptBlockMapQueryResult result = wrapper.getRectangleBlocks(0, 0, 32, 32);

        Assert.Same(sector, Assert.Single(result.getSectors()).Sector);
    }

    [Fact]
    public void DataWrapperExposesTextureAndFlatNamesAndInfo()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("textures/WALLA.png", TestArtifacts.Png(2, 3, TestArtifacts.SolidRgba(2, 3, 10, 20, 30, 255))),
            ("flats/FLOORA.png", TestArtifacts.Png(4, 5, TestArtifacts.SolidRgba(4, 5, 40, 50, 60, 255))));
        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);
            var wrapper = new UdbScriptDataWrapper(resources);

            Assert.Contains("WALLA", wrapper.getTextureNames());
            Assert.Contains("FLOORA", wrapper.getFlatNames());
            Assert.True(wrapper.textureExists("walla"));
            Assert.False(wrapper.textureExists("missing"));
            Assert.True(wrapper.flatExists("floora"));
            Assert.False(wrapper.flatExists("missing"));

            UdbScriptImageInfo? texture = wrapper.getTextureInfo("WALLA");
            UdbScriptImageInfo? flat = wrapper.getFlatInfo("FLOORA");
            Assert.NotNull(texture);
            Assert.NotNull(flat);

            Assert.Equal("WALLA", texture!.name);
            Assert.Equal(2, texture.width);
            Assert.Equal(3, texture.height);
            Assert.Equal(new UdbScriptVector2DWrapper(1, 1), texture.scale);
            Assert.False(texture.isFlat);
            Assert.Equal("FLOORA", flat!.name);
            Assert.Equal(4, flat.width);
            Assert.Equal(5, flat.height);
            Assert.True(flat.isFlat);

            UdbScriptImageInfo missingTexture = wrapper.getTextureInfo("MISSING");
            UdbScriptImageInfo missingFlat = wrapper.getFlatInfo("MISSING");
            Assert.Equal("MISSING", missingTexture.name);
            Assert.Equal(64, missingTexture.width);
            Assert.Equal(64, missingTexture.height);
            Assert.False(missingTexture.isFlat);
            Assert.Equal("MISSING", missingFlat.name);
            Assert.Equal(64, missingFlat.width);
            Assert.Equal(64, missingFlat.height);
            Assert.True(missingFlat.isFlat);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void MapElementWrappersExposeMutableCustomFields()
    {
        var vertex = new Vertex(new Vector2D(1, 2));
        var line = new Linedef(vertex, new Vertex(new Vector2D(3, 4)));
        var sector = new Sector();
        var side = new Sidedef(line, isFront: true);
        var thing = new Thing(new Vector2D(5, 6), 3001);

        new UdbScriptVertexWrapper(vertex).fields["user_vertex"] = "v";
        new UdbScriptLinedefWrapper(line).fields["user_line"] = 1.5;
        new UdbScriptSidedefWrapper(side).fields["user_side"] = true;
        new UdbScriptSectorWrapper(sector).fields["user_sector"] = new BigInteger(42);
        new UdbScriptThingWrapper(thing).fields["user_thing"] = new UdbScriptUniversalValue((int)UniversalType.String, 7);

        Assert.Equal("v", vertex.Fields["user_vertex"]);
        Assert.Equal(1.5, line.Fields["user_line"]);
        Assert.Equal(true, side.Fields["user_side"]);
        Assert.Equal(42, sector.Fields["user_sector"]);
        Assert.Equal("7", thing.Fields["user_thing"]);

        var fields = new UdbScriptThingWrapper(thing).fields;
        Assert.True(fields.ContainsKey("user_thing"));
        Assert.Equal("7", fields["user_thing"]);

        fields["user_thing"] = null;

        Assert.False(thing.Fields.ContainsKey("user_thing"));
    }

    [Fact]
    public void MapElementFieldsUseConfiguredKnownFieldDefaultTypesLikeUdb()
    {
        var config = GameConfiguration.FromText("""
            universalfields
            {
                vertex
                {
                    user_vertex_int { type = 0; default = 0; }
                }
                linedef
                {
                    user_line_float { type = 1; default = 0.0; }
                }
                sidedef
                {
                    user_side_text { type = 2; default = ""; }
                }
                sector
                {
                    user_sector_bool { type = 3; default = false; }
                }
                thing
                {
                    user_thing_int { type = 0; default = 0; }
                    user_thing_otherint { type = 0; default = 0; }
                }
            }
            """);
        var vertex = new Vertex(new Vector2D(1, 2));
        var line = new Linedef(vertex, new Vertex(new Vector2D(3, 4)));
        var side = new Sidedef(line, isFront: true);
        var sector = new Sector();
        var thing = new Thing();

        new UdbScriptVertexWrapper(vertex, config: config).fields["user_vertex_int"] = 2.6;
        new UdbScriptLinedefWrapper(line, config: config).fields["user_line_float"] = 3;
        new UdbScriptSidedefWrapper(side, config: config).fields["user_side_text"] = "side";
        new UdbScriptSectorWrapper(sector, config: config).fields["user_sector_bool"] = true;
        UdbScriptFieldsWrapper thingFields = new UdbScriptThingWrapper(thing, config: config).fields;
        thingFields["user_thing_int"] = 7.1;

        Assert.Equal(3, vertex.Fields["user_vertex_int"]);
        Assert.IsType<int>(vertex.Fields["user_vertex_int"]);
        Assert.Equal(3.0, line.Fields["user_line_float"]);
        Assert.IsType<double>(line.Fields["user_line_float"]);
        Assert.Equal("side", side.Fields["user_side_text"]);
        Assert.Equal(true, sector.Fields["user_sector_bool"]);
        Assert.Equal(7, thing.Fields["user_thing_int"]);
        Assert.IsType<int>(thing.Fields["user_thing_int"]);

        InvalidOperationException badType = Assert.Throws<InvalidOperationException>(
            () => thingFields["user_thing_otherint"] = new BigInteger(8));

        Assert.StartsWith("UDMF field 'user_thing_otherint' is of incompatible type", badType.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapElementFieldsPreserveExistingNumericFieldTypesLikeUdb()
    {
        var thing = new Thing();
        thing.Fields["user_int"] = 2;
        thing.Fields["user_double"] = 2.5;
        thing.Fields["user_string"] = "old";
        thing.Fields["user_bool"] = false;
        var fields = new UdbScriptThingWrapper(thing).fields;

        fields["user_int"] = 2.6;
        fields["user_double"] = 3;
        fields["user_string"] = "new";
        fields["user_bool"] = true;

        Assert.Equal(3, thing.Fields["user_int"]);
        Assert.IsType<int>(thing.Fields["user_int"]);
        Assert.Equal(3.0, thing.Fields["user_double"]);
        Assert.IsType<double>(thing.Fields["user_double"]);
        Assert.Equal("new", thing.Fields["user_string"]);
        Assert.Equal(true, thing.Fields["user_bool"]);

        InvalidOperationException badType = Assert.Throws<InvalidOperationException>(() => fields["user_int"] = "bad");

        Assert.StartsWith("UDMF field 'user_int' is of incompatible type", badType.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapElementFieldsRejectInvalidNamesAndValues()
    {
        var fields = new UdbScriptThingWrapper(new Thing()).fields;

        InvalidOperationException uppercase = Assert.Throws<InvalidOperationException>(() => fields["User"] = 1);
        InvalidOperationException empty = Assert.Throws<InvalidOperationException>(() => fields[""] = 1);
        InvalidOperationException tooBig = Assert.Throws<InvalidOperationException>(() => fields["user_big"] = new BigInteger(int.MaxValue) + 1);
        InvalidOperationException badType = Assert.Throws<InvalidOperationException>(() => fields["user_bad"] = new object());

        Assert.Equal("UDMF field names must be lowercase.", uppercase.Message);
        Assert.Equal("UDMF field names can not be empty.", empty.Message);
        Assert.StartsWith("Value 2147483648 for UDMF field \"user_big\" is too big.", tooBig.Message, StringComparison.Ordinal);
        Assert.StartsWith("UDMF field 'user_bad' is of incompatible type", badType.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapElementFieldsRejectActiveFlagNames()
    {
        var line = new Linedef();
        var side = new Sidedef(line, isFront: true);
        var sector = new Sector();
        var thing = new Thing();
        line.SetFlag("blocksound", true);
        side.SetFlag("lightfog", true);
        sector.SetFlag("noattack", true);
        thing.SetFlag("ambush", true);

        InvalidOperationException lineException = Assert.Throws<InvalidOperationException>(
            () => new UdbScriptLinedefWrapper(line).fields["blocksound"] = false);
        InvalidOperationException sideException = Assert.Throws<InvalidOperationException>(
            () => new UdbScriptSidedefWrapper(side).fields["lightfog"] = false);
        InvalidOperationException sectorException = Assert.Throws<InvalidOperationException>(
            () => new UdbScriptSectorWrapper(sector).fields["noattack"] = false);
        InvalidOperationException thingException = Assert.Throws<InvalidOperationException>(
            () => new UdbScriptThingWrapper(thing).fields["ambush"] = false);

        const string expected = "You are trying to modify a flag through the UDMF fields. Please use the 'flags' property instead.";
        Assert.Equal(expected, lineException.Message);
        Assert.Equal(expected, sideException.Message);
        Assert.Equal(expected, sectorException.Message);
        Assert.Equal(expected, thingException.Message);
    }

    [Fact]
    public void MapElementFieldsRejectConfiguredInactiveFlagNamesLikeUdb()
    {
        var config = GameConfiguration.FromText("""
            linedefflags
            {
                1 = "blocking";
            }
            sidedefflags
            {
                lightfog = "Light fog";
            }
            sectorflags
            {
                noattack = "No attack";
            }
            thingflags
            {
                ambush = "Ambush";
            }
            """);
        var line = new Linedef();
        var side = new Sidedef(line, isFront: true);
        var sector = new Sector();
        var thing = new Thing();

        InvalidOperationException lineException = Assert.Throws<InvalidOperationException>(
            () => new UdbScriptLinedefWrapper(line, config: config).fields["blocking"] = false);
        InvalidOperationException sideException = Assert.Throws<InvalidOperationException>(
            () => new UdbScriptSidedefWrapper(side, config: config).fields["lightfog"] = false);
        InvalidOperationException sectorException = Assert.Throws<InvalidOperationException>(
            () => new UdbScriptSectorWrapper(sector, config: config).fields["noattack"] = false);
        InvalidOperationException thingException = Assert.Throws<InvalidOperationException>(
            () => new UdbScriptThingWrapper(thing, config: config).fields["ambush"] = false);

        const string expected = "You are trying to modify a flag through the UDMF fields. Please use the 'flags' property instead.";
        Assert.Equal(expected, lineException.Message);
        Assert.Equal(expected, sideException.Message);
        Assert.Equal(expected, sectorException.Message);
        Assert.Equal(expected, thingException.Message);
        Assert.Empty(line.Fields);
        Assert.Empty(side.Fields);
        Assert.Empty(sector.Fields);
        Assert.Empty(thing.Fields);
    }

    [Fact]
    public void MapElementFlagsRejectUnknownConfiguredFlagNamesLikeUdb()
    {
        var config = GameConfiguration.FromText("""
            linedefflags
            {
                1 = "blocking";
            }
            linedefactivations
            {
                playercross = "Player Crosses";
            }
            sidedefflags
            {
                lightfog = "Light fog";
            }
            sectorflags
            {
                noattack = "No attack";
            }
            thingflags
            {
                ambush = "Ambush";
            }
            """);
        var line = new Linedef();
        var side = new Sidedef(line, isFront: true);
        var sector = new Sector();
        var thing = new Thing();

        UdbScriptFlagsWrapper lineFlags = new UdbScriptLinedefWrapper(line, config: config).flags;
        UdbScriptFlagsWrapper sideFlags = new UdbScriptSidedefWrapper(side, config: config).flags;
        UdbScriptFlagsWrapper sectorFlags = new UdbScriptSectorWrapper(sector, config: config).flags;
        UdbScriptFlagsWrapper thingFlags = new UdbScriptThingWrapper(thing, config: config).flags;

        lineFlags["blocking"] = true;
        lineFlags["playercross"] = true;
        sideFlags["lightfog"] = true;
        sectorFlags["noattack"] = true;
        thingFlags["ambush"] = true;

        Assert.True(line.IsFlagSet("blocking"));
        Assert.True(line.IsFlagSet("playercross"));
        Assert.True(side.IsFlagSet("lightfog"));
        Assert.True(sector.IsFlagSet("noattack"));
        Assert.True(thing.IsFlagSet("ambush"));

        InvalidOperationException lineException = Assert.Throws<InvalidOperationException>(() => lineFlags["unknown"] = true);
        InvalidOperationException sideException = Assert.Throws<InvalidOperationException>(() => sideFlags["unknown"] = true);
        InvalidOperationException sectorException = Assert.Throws<InvalidOperationException>(() => sectorFlags["unknown"] = true);
        InvalidOperationException thingException = Assert.Throws<InvalidOperationException>(() => thingFlags["unknown"] = true);

        const string expected = "Flag name 'unknown' is not valid.";
        Assert.Equal(expected, lineException.Message);
        Assert.Equal(expected, sideException.Message);
        Assert.Equal(expected, sectorException.Message);
        Assert.Equal(expected, thingException.Message);
    }

    [Fact]
    public void MapElementWrappersRejectDisposedFieldsAccess()
    {
        var wrapper = new UdbScriptVertexWrapper(new Vertex { IsDisposed = true });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => wrapper.fields);

        Assert.Equal("Vertex is disposed, the fields member can not be accessed.", exception.Message);
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

    private static Sector CreateSquareSector()
    {
        var sector = new Sector();
        var vertices = new[]
        {
            new Vertex(new Vector2D(0, 0)),
            new Vertex(new Vector2D(64, 0)),
            new Vertex(new Vector2D(64, 64)),
            new Vertex(new Vector2D(0, 64)),
        };

        for (int i = 0; i < vertices.Length; i++)
        {
            var line = new Linedef(vertices[i], vertices[(i + 1) % vertices.Length]);
            var side = new Sidedef();
            line.AttachFront(side);
            side.SetSector(sector);
            sector.Sidedefs.Add(side);
        }

        sector.UpdateBBox();
        return sector;
    }

    private static (MapSet Map, Sector First, Sector Second, Linedef Shared) CreateTwoSharedSectors()
    {
        var map = new MapSet();
        var first = map.AddSector();
        var second = map.AddSector();
        var start = map.AddVertex(new Vector2D(64, 0));
        var end = map.AddVertex(new Vector2D(64, 64));
        var shared = map.AddLinedef(start, end);
        map.AddSidedef(shared, isFront: true, first);
        map.AddSidedef(shared, isFront: false, second);
        map.BuildIndexes();
        return (map, first, second, shared);
    }

    private static (MapSet Map, Linedef Line, Sector Sector, Thing Thing, Vertex Vertex) CreateBlockMapFixture(int size)
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var vertices = new[]
        {
            map.AddVertex(new Vector2D(0, 0)),
            map.AddVertex(new Vector2D(size, 0)),
            map.AddVertex(new Vector2D(size, size)),
            map.AddVertex(new Vector2D(0, size)),
        };

        Linedef? firstLine = null;
        for (int i = 0; i < vertices.Length; i++)
        {
            var line = map.AddLinedef(vertices[i], vertices[(i + 1) % vertices.Length]);
            map.AddSidedef(line, isFront: true, sector);
            firstLine ??= line;
        }

        var thing = map.AddThing(new Vector2D(size / 2.0, size / 2.0), 3001);
        map.BuildIndexes();
        return (map, firstLine!, sector, thing, vertices[0]);
    }
}
