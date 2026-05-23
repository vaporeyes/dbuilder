// ABOUTME: Tests cross-format flag translation when saving a map in a different format than it was loaded.
// ABOUTME: Verifies binary->UDMF fills named flags, UDMF->binary fills the bitfield, and the fill is non-destructive.

using System.IO;
using System.Linq;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class MapFormatConverterTests
{
    private const string Cfg = @"
linedefflagstranslation
{
    1 = ""blocking"";
    4 = ""twosided"";
}
thingflagstranslation
{
    8 = ""ambush"";
    16 = ""!single"";
}";

    private static MapSet MapWithLine(int lineFlags, int thingFlags)
    {
        var map = new MapSet();
        var v1 = new Vertex(new Vector2D(0, 0));
        var v2 = new Vertex(new Vector2D(64, 0));
        map.Vertices.Add(v1);
        map.Vertices.Add(v2);
        map.Linedefs.Add(new Linedef(v1, v2) { Flags = lineFlags });
        map.Things.Add(new Thing(new Vector2D(10, 10), 1) { Flags = thingFlags });
        return map;
    }

    [Fact]
    public void BinaryToUdmfFillsNamedFlags()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(1 | 4, 8); // blocking+twosided; ambush, single (16 clear)
        MapFormatConverter.Convert(map, MapFormat.Doom, MapFormat.Udmf, gc);

        Assert.Contains("blocking", map.Linedefs[0].UdmfFlags);
        Assert.Contains("twosided", map.Linedefs[0].UdmfFlags);
        Assert.Contains("ambush", map.Things[0].UdmfFlags);
        Assert.Contains("single", map.Things[0].UdmfFlags); // negated default present when bit clear
    }

    [Fact]
    public void BinaryToUdmfKeepsSourceBitfield()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(1, 8);
        MapFormatConverter.Convert(map, MapFormat.Doom, MapFormat.Udmf, gc);
        Assert.Equal(1, map.Linedefs[0].Flags); // source representation untouched
        Assert.Equal(8, map.Things[0].Flags);
    }

    [Fact]
    public void UdmfToBinaryFillsBitfield()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(0, 0);
        map.Linedefs[0].UdmfFlags.Add("twosided");
        map.Things[0].UdmfFlags.Add("ambush"); // single absent -> bit 16 set
        MapFormatConverter.Convert(map, MapFormat.Udmf, MapFormat.Doom, gc);

        Assert.Equal(4, map.Linedefs[0].Flags);
        Assert.Equal(8 | 16, map.Things[0].Flags);
    }

    [Fact]
    public void SameFormatIsNoOp()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(1, 8);
        MapFormatConverter.Convert(map, MapFormat.Doom, MapFormat.Doom, gc);
        Assert.Empty(map.Linedefs[0].UdmfFlags);
    }

    [Fact]
    public void NullConfigIsNoOp()
    {
        var map = MapWithLine(1, 8);
        MapFormatConverter.Convert(map, MapFormat.Doom, MapFormat.Udmf, null);
        Assert.Empty(map.Linedefs[0].UdmfFlags);
    }

    // A proper square sector map so the writers produce loadable output.
    private static MapSet SquareWithBlockingLine()
    {
        var map = new MapSet();
        var s = map.AddSector();
        var v = new[]
        {
            map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(0, 64)),
            map.AddVertex(new Vector2D(64, 64)), map.AddVertex(new Vector2D(64, 0)),
        };
        for (int i = 0; i < 4; i++)
        {
            var l = map.AddLinedef(v[i], v[(i + 1) % 4]);
            map.AddSidedef(l, true, s);
            if (i == 0) l.Flags = 1; // blocking
        }
        map.BuildIndexes();
        return map;
    }

    [Fact]
    public void DoomToUdmfSaveRoundTripsNamedFlags()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = SquareWithBlockingLine();
        MapFormatConverter.Convert(map, MapFormat.Doom, MapFormat.Udmf, gc);

        using var wad = new WAD(new MemoryStream());
        WadMaps.SaveMap(wad, "MAP01", map, MapFormat.Udmf, gc);

        var entry = WadMaps.Find(wad).First();
        Assert.Equal(MapFormat.Udmf, entry.Format);
        var reloaded = WadMaps.Load(wad, entry)!;
        Assert.Contains(reloaded.Linedefs, l => l.UdmfFlags.Contains("blocking"));
    }
}
