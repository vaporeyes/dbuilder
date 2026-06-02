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
        wrapper.upperTexture = "STARTAN3";
        wrapper.middleTexture = "BRICK1";
        wrapper.lowerTexture = "STONE2";

        Assert.True(wrapper.isFront);
        Assert.Same(line, wrapper.line.Linedef);
        Assert.Same(back, wrapper.other?.Sidedef);
        Assert.Equal(Angle2D.RadToDeg(front.Angle), wrapper.angle);
        Assert.Equal(front.Angle, wrapper.angleRad);
        Assert.Equal(8, front.OffsetX);
        Assert.Equal(16, front.OffsetY);
        Assert.Equal("STARTAN3", front.HighTexture);
        Assert.Equal("BRICK1", front.MidTexture);
        Assert.Equal("STONE2", front.LowTexture);
        Assert.True(wrapper.flags["lightfog"]);
        Assert.True(wrapper.upperSelected);
        Assert.True(wrapper.middleSelected);
        Assert.True(wrapper.lowerSelected);
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
        wrapper.floorTexture = "FLOOR0_1";
        wrapper.ceilingTexture = "CEIL1_1";
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
        Assert.Equal("FLOOR0_1", sector.FloorTexture);
        Assert.Equal("CEIL1_1", sector.CeilTexture);
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
        sector.Tag = 1;
        var wrapper = new UdbScriptSectorWrapper(sector);

        wrapper.setFloorSlope(new object[] { 0.0, 3.0, 4.0 });
        wrapper.setCeilingSlope(new UdbScriptVector3DWrapper(0, -6, 8));
        bool added = wrapper.addTag(3);
        bool addedDuplicate = wrapper.addTag(3);
        bool removed = wrapper.removeTag(1);

        Assert.True(wrapper.intersect(new object[] { 32.0, 32.0 }));
        Assert.False(wrapper.intersect(new object[] { 96.0, 96.0 }));
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
    public void SectorWrapperRejectsDisposedSectorAccess()
    {
        var wrapper = new UdbScriptSectorWrapper(new Sector { IsDisposed = true });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => wrapper.floorHeight);

        Assert.Equal("Sector is disposed, the floorHeight member can not be accessed.", exception.Message);
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
        map.AddSidedef(line, isFront: true, sector);
        var thing = map.AddThing(new Vector2D(16, 16), 3001);
        first.Selected = true;
        line.Selected = true;
        sector.Selected = true;
        thing.Selected = true;
        var wrapper = new UdbScriptMapWrapper(map);

        wrapper.markSelectedVertices();
        wrapper.markSelectedLinedefs();
        wrapper.markSelectedSectors();
        wrapper.markSelectedThings();

        Assert.Same(first, Assert.Single(wrapper.getMarkedVertices()).Vertex);
        Assert.Same(line, Assert.Single(wrapper.getMarkedLinedefs()).Linedef);
        Assert.Same(sector, Assert.Single(wrapper.getMarkedSectors()).Sector);
        Assert.Same(thing, Assert.Single(wrapper.getMarkedThings()).Thing);

        wrapper.markSelectedVertices(mark: false);
        wrapper.markSelectedLinedefs(mark: false);
        wrapper.markSelectedSectors(mark: false);
        wrapper.markSelectedThings(mark: false);

        Assert.Empty(wrapper.getMarkedVertices());
        Assert.Empty(wrapper.getMarkedLinedefs());
        Assert.Empty(wrapper.getMarkedSectors());
        Assert.Empty(wrapper.getMarkedThings());
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
}
