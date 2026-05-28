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

    private const string HexenCfg = @"
thingflagstranslation
{
    8 = ""ambush"";
    256 = ""single"";
    512 = ""coop"";
    1024 = ""dm"";
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

    [Fact]
    public void DoomToHexenTranslatesThingSingleFlagThroughUdmf()
    {
        var doom = GameConfiguration.FromText(Cfg);
        var hexen = GameConfiguration.FromText(HexenCfg);
        var map = MapWithLine(0, 8); // ambush set, Doom single-player exclusion bit clear

        MapFormatConverter.Convert(map, MapFormat.Doom, MapFormat.Hexen, doom, hexen);

        Assert.Equal(8 | 256, map.Things[0].Flags);
        Assert.Contains("single", map.Things[0].UdmfFlags);
    }

    [Fact]
    public void DoomToHexenKeepsSingleClearWhenDoomExcludesSinglePlayer()
    {
        var doom = GameConfiguration.FromText(Cfg);
        var hexen = GameConfiguration.FromText(HexenCfg);
        var map = MapWithLine(0, 8 | 16); // Doom bit 16 means not single-player

        MapFormatConverter.Convert(map, MapFormat.Doom, MapFormat.Hexen, doom, hexen);

        Assert.Equal(8, map.Things[0].Flags);
        Assert.DoesNotContain("single", map.Things[0].UdmfFlags);
    }

    [Fact]
    public void ConvertingToDoomClearsLinedefAndThingArgs()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(0, 0);
        map.Linedefs[0].Args[0] = 7;
        map.Linedefs[0].Args[4] = 9;
        map.Things[0].Args[1] = 3;
        map.Things[0].Args[2] = 5;

        MapFormatConverter.Convert(map, MapFormat.Hexen, MapFormat.Doom, gc);

        Assert.All(map.Linedefs[0].Args, arg => Assert.Equal(0, arg));
        Assert.All(map.Things[0].Args, arg => Assert.Equal(0, arg));
    }

    [Fact]
    public void ConvertingToDoomClearsArgsWithoutConfig()
    {
        var map = MapWithLine(0, 0);
        map.Linedefs[0].Args[0] = 7;
        map.Things[0].Args[0] = 3;

        MapFormatConverter.Convert(map, MapFormat.Udmf, MapFormat.Doom, null);

        Assert.All(map.Linedefs[0].Args, arg => Assert.Equal(0, arg));
        Assert.All(map.Things[0].Args, arg => Assert.Equal(0, arg));
    }

    [Fact]
    public void HexenLineSetIdentificationConvertsToUdmfTagAndFlags()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(0, 0);
        var line = map.Linedefs[0];
        line.Action = 121;
        line.Args[0] = 44;
        line.Args[1] = 1 | 8 | 128;
        line.Args[4] = 2;

        MapFormatConverter.Convert(map, MapFormat.Hexen, MapFormat.Udmf, gc);

        Assert.Equal(556, line.Tag);
        Assert.Equal(0, line.Action);
        Assert.All(line.Args, arg => Assert.Equal(0, arg));
        Assert.Contains("zoneboundary", line.UdmfFlags);
        Assert.Contains("clipmidtex", line.UdmfFlags);
        Assert.Contains("firstsideonly", line.UdmfFlags);
    }

    [Fact]
    public void HexenLineSetIdentificationKeepsTranslatedConfigFlags()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(1, 0);
        var line = map.Linedefs[0];
        line.Action = 121;
        line.Args[0] = 1;

        MapFormatConverter.Convert(map, MapFormat.Hexen, MapFormat.Udmf, gc);

        Assert.Contains("blocking", line.UdmfFlags);
        Assert.Equal(1, line.Tag);
    }
}
