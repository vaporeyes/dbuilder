// ABOUTME: Tests for static map-format save constraints across Doom, Hexen and UDMF.
// ABOUTME: Verifies unsupported fields and binary field ranges are reported before WAD mutation.

using System.IO;
using System.Linq;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class MapFormatConstraintsTests
{
    private static MapSet SquareMap()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var vertices = new[]
        {
            map.AddVertex(new Vector2D(0, 0)),
            map.AddVertex(new Vector2D(0, 64)),
            map.AddVertex(new Vector2D(64, 64)),
            map.AddVertex(new Vector2D(64, 0)),
        };

        for (int i = 0; i < vertices.Length; i++)
            map.AddSidedef(map.AddLinedef(vertices[i], vertices[(i + 1) % vertices.Length]), true, sector);

        map.AddThing(new Vector2D(16, 16), 3001);
        map.BuildIndexes();
        return map;
    }

    [Fact]
    public void DoomValidMapHasNoViolations()
    {
        var issues = MapFormatConstraints.Validate(SquareMap(), MapFormat.Doom);

        Assert.Empty(issues);
    }

    [Fact]
    public void DoomRejectsUnsupportedFieldsAndOutOfRangeCoordinates()
    {
        var map = SquareMap();
        map.Vertices[0].Position = new Vector2D(40000, 0);
        map.Linedefs[0].Args[0] = 1;
        map.Things[0].Action = 1;

        var fields = MapFormatConstraints.Validate(map, MapFormat.Doom).Select(v => v.Field).ToArray();

        Assert.Contains("vertices[0].x", fields);
        Assert.Contains("linedefs[0].arg0", fields);
        Assert.Contains("things[0].action", fields);
    }

    [Fact]
    public void BinaryThingCoordinatesValidateAgainstSerializedTruncation()
    {
        var map = SquareMap();
        map.Things[0].Position = new Vector2D(32767.9, -32768.9);
        map.Things[0].Height = 32767.9;

        var issues = MapFormatConstraints.Validate(map, MapFormat.Hexen);

        Assert.Empty(issues);
    }

    [Fact]
    public void BinaryThingCoordinatesRejectTruncatedValuesOutsideRange()
    {
        var map = SquareMap();
        map.Things[0].Position = new Vector2D(32768.1, -32769.1);
        map.Things[0].Height = 32768.1;

        var fields = MapFormatConstraints.Validate(map, MapFormat.Hexen).Select(v => v.Field).ToArray();

        Assert.Contains("things[0].x", fields);
        Assert.Contains("things[0].y", fields);
        Assert.Contains("things[0].height", fields);
    }

    [Fact]
    public void UdmfThingCoordinatesKeepRoundedValidation()
    {
        var map = SquareMap();
        map.Things[0].Position = new Vector2D(32767.9, 0);

        var violation = Assert.Single(MapFormatConstraints.Validate(map, MapFormat.Udmf));

        Assert.Equal("things[0].x", violation.Field);
    }

    [Fact]
    public void HexenAllowsActionArgsAndThingActionWithinByteRange()
    {
        var map = SquareMap();
        map.Linedefs[0].Action = 80;
        map.Linedefs[0].Args[0] = 255;
        map.Things[0].Action = 80;
        map.Things[0].Args[0] = 255;

        var issues = MapFormatConstraints.Validate(map, MapFormat.Hexen);

        Assert.Empty(issues);
    }

    [Fact]
    public void HexenRejectsArgsAboveByteRange()
    {
        var map = SquareMap();
        map.Linedefs[0].Args[0] = 256;
        map.Things[0].Args[0] = 256;

        var fields = MapFormatConstraints.Validate(map, MapFormat.Hexen).Select(v => v.Field).ToArray();

        Assert.Contains("linedefs[0].arg0", fields);
        Assert.Contains("things[0].arg0", fields);
    }

    [Fact]
    public void UdmfKeepsShortCoordinateBounds()
    {
        var map = SquareMap();
        map.Vertices[0].Position = new Vector2D(40000, 0);

        var violation = Assert.Single(MapFormatConstraints.Validate(map, MapFormat.Udmf));

        Assert.Equal("vertices[0].x", violation.Field);
    }

    [Fact]
    public void SaveMapThrowsBeforeMutatingWad()
    {
        using var wad = new WAD(new MemoryStream());
        DoomMapWriter.WriteMap(SquareMap(), wad, "MAP01", wad.Lumps.Count);
        var originalLumpCount = wad.Lumps.Count;

        var invalid = SquareMap();
        invalid.Linedefs[0].Args[0] = 1;

        var ex = Assert.Throws<InvalidDataException>(() => WadMaps.SaveMap(wad, "MAP01", invalid, MapFormat.Doom));

        Assert.Contains("linedefs[0].arg0", ex.Message);
        Assert.Equal(originalLumpCount, wad.Lumps.Count);
        Assert.NotNull(wad.FindLump("MAP01"));
        Assert.NotNull(wad.FindLump("LINEDEFS"));
    }
}
