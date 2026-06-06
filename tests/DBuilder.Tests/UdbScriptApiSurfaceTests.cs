// ABOUTME: Verifies DBuilder's UDBScript wrapper method surface against upstream script declarations.
// ABOUTME: Keeps common documented UDBScript API entry points from regressing while runtime parity grows.

using System.Reflection;
using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class UdbScriptApiSurfaceTests
{
    [Theory]
    [MemberData(nameof(InstanceMethodSurface))]
    public void DocumentedInstanceMethodsExist(Type type, string methodName)
    {
        Assert.Contains(
            type.GetMethods(BindingFlags.Instance | BindingFlags.Public),
            method => method.Name == methodName);
    }

    [Theory]
    [MemberData(nameof(StaticMethodSurface))]
    public void DocumentedStaticMethodsExist(Type type, string methodName)
    {
        Assert.Contains(
            type.GetMethods(BindingFlags.Static | BindingFlags.Public),
            method => method.Name == methodName);
    }

    public static TheoryData<Type, string> InstanceMethodSurface()
    {
        var data = new TheoryData<Type, string>();

        Add(data, typeof(UdbScriptBlockEntryWrapper),
            "getLinedefs", "getThings", "getSectors", "getVertices");
        Add(data, typeof(UdbScriptBlockMapQueryResult),
            "getLinedefs", "getThings", "getSectors", "getVertices");
        Add(data, typeof(UdbScriptBlockMapWrapper),
            "getBlockAt", "getLineBlocks", "getRectangleBlocks");
        Add(data, typeof(UdbScriptDataWrapper),
            "getTextureNames", "textureExists", "getTextureInfo",
            "getFlatNames", "flatExists", "getFlatInfo");
        Add(data, typeof(UdbScriptLine2DWrapper),
            "getCoordinatesAt", "getLength", "getAngleRad", "getAngle", "getPerpendicular",
            "isIntersecting", "getIntersectionPoint", "getSideOfLine", "getNearestPointOnLine");
        Add(data, typeof(UdbScriptLinedefWrapper),
            "copyPropertiesTo", "clearFlags", "flipVertices", "flipSidedefs", "flip",
            "getSidePoint", "getCenterPoint", "getCoordinatesAt", "applySidedFlags", "nearestOnLine",
            "safeDistanceToSq", "safeDistanceTo", "distanceToSq", "distanceTo",
            "sideOfLine", "split", "delete", "getTags", "addTag", "removeTag");
        Add(data, typeof(UdbScriptMapWrapper),
            "snappedToGrid", "getThings", "getSectors", "getSidedefs", "getLinedefs",
            "getVertices", "stitchGeometry", "snapAllToAccuracy", "getNewTag",
            "getMultipleNewTags", "nearestLinedef", "nearestThing", "nearestVertex",
            "nearestSidedef", "drawLines", "clearAllMarks", "clearMarkedVertices",
            "clearMarkedThings", "clearMarkedLinedefs", "clearMarkedSidedefs",
            "clearMarkedSectors", "clearMarkeLinedefs", "clearMarkeSidedefs",
            "clearMarkeSectors", "invertAllMarks", "invertMarkedVertices",
            "invertMarkedThings", "invertMarkedLinedefs", "invertMarkedSidedefs",
            "invertMarkedSectors", "getMarkedVertices", "getMarkedThings",
            "getMarkedLinedefs", "getMarkedSidedefs", "getMarkedSectors",
            "markSelectedVertices", "markSelectedLinedefs", "markSelectedSectors",
            "markSelectedThings", "getSelectedVertices", "getHighlightedVertex",
            "getSelectedOrHighlightedVertices", "getSelectedThings", "getHighlightedThing",
            "getSelectedOrHighlightedThings", "getSelectedSectors", "getHighlightedSector",
            "getSelectedOrHighlightedSectors", "getSelectedLinedefs", "getHighlightedLinedef",
            "getSelectedOrHighlightedLinedefs", "getSidedefsFromSelectedLinedefs",
            "getSidedefsFromSelectedOrHighlightedLinedefs", "clearAllSelected",
            "clearSelectedVertices", "clearSelectedThings", "clearSelectedSectors",
            "createVertex", "createThing", "joinSectors", "mergeSectors");
        Add(data, typeof(UdbScriptPlaneWrapper),
            "getIntersection", "distance", "closestOnPlane", "getZ");
        Add(data, typeof(UdbScriptSectorWrapper),
            "getSidedefs", "clearFlags", "copyPropertiesTo", "intersect", "getFloorZ",
            "getCeilingZ", "join", "delete",
            "getTriangles", "getFloorSlope", "setFloorSlope", "getCeilingSlope",
            "setCeilingSlope", "getLabelPositions", "getTags", "addTag", "removeTag");
        Add(data, typeof(UdbScriptSidedefWrapper),
            "copyPropertiesTo", "getCenterPoint", "getSidePoint", "getCoordinatesAt");
        Add(data, typeof(UdbScriptThingWrapper),
            "copyPropertiesTo", "clearFlags", "snapToGrid", "snapToAccuracy",
            "distanceToSq", "distanceTo", "delete", "getSector");
        Add(data, typeof(UdbScriptVector2DWrapper),
            "getPerpendicular", "getSign", "getAngleRad", "getAngle", "getLength",
            "getLengthSq", "getNormal", "getTransformed", "getInverseTransformed",
            "getRotated", "getRotatedRad", "isFinite");
        Add(data, typeof(UdbScriptVector3DWrapper),
            "getAngleXYRad", "getAngleXY", "getAngleZRad", "getAngleZ", "getLength",
            "getLengthSq", "getNormal", "getScaled", "isNormalized", "isFinite");
        Add(data, typeof(UdbScriptVertexWrapper),
            "getLinedefs", "copyPropertiesTo", "distanceToSq", "distanceTo",
            "nearestLinedef", "snapToAccuracy", "snapToGrid", "join", "delete");

        return data;
    }

    public static TheoryData<Type, string> StaticMethodSurface()
    {
        var data = new TheoryData<Type, string>();

        Add(data, typeof(UdbScriptAngle2DWrapper),
            "doomToReal", "doomToRealRad", "realToDoom", "realToDoomRad",
            "radToDeg", "degToRad", "normalized", "normalizedRad",
            "getAngle", "getAngleRad");
        Add(data, typeof(UdbScriptLine2DWrapper),
            "areIntersecting", "getIntersectionPoint", "getSideOfLine",
            "getDistanceToLine", "getDistanceToLineSq", "getNearestOnLine",
            "getNearestPointOnLine", "getCoordinatesAt");
        Add(data, typeof(UdbScriptVector2DWrapper),
            "dotProduct", "crossProduct", "reflect", "reversed", "fromAngleRad",
            "fromAngle", "getAngleRad", "getAngle", "getDistanceSq", "getDistance");
        Add(data, typeof(UdbScriptVector3DWrapper),
            "dotProduct", "crossProduct", "reflect", "reversed", "fromAngleXYRad",
            "fromAngleXY", "fromAngleXYZRad", "fromAngleXYZ");

        return data;
    }

    private static void Add(TheoryData<Type, string> data, Type type, params string[] methodNames)
    {
        foreach (string methodName in methodNames)
            data.Add(type, methodName);
    }
}
