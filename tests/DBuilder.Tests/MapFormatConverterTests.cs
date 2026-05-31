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

    [Theory]
    [InlineData(1, 3, true)]
    [InlineData(5, 4, true)]
    [InlineData(181, 2, true)]
    [InlineData(215, 0, true)]
    [InlineData(222, 0, false)]
    public void HexenTagArgActionsConvertToUdmfTag(int action, int tagArg, bool clearsArg)
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(0, 0);
        var line = map.Linedefs[0];
        line.Action = action;
        line.Args[tagArg] = 77;

        MapFormatConverter.Convert(map, MapFormat.Hexen, MapFormat.Udmf, gc);

        Assert.Equal(77, line.Tag);
        Assert.Equal(clearsArg ? 0 : 77, line.Args[tagArg]);
    }

    [Fact]
    public void HexenTranslucentLineConvertsLineIdAndFlagsToUdmf()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(0, 0);
        var line = map.Linedefs[0];
        line.Action = 208;
        line.Args[0] = 33;
        line.Args[3] = 2 | 16;

        MapFormatConverter.Convert(map, MapFormat.Hexen, MapFormat.Udmf, gc);

        Assert.Equal(33, line.Tag);
        Assert.Equal(0, line.Args[3]);
        Assert.Contains("jumpover", line.UdmfFlags);
        Assert.Contains("wrapmidtex", line.UdmfFlags);
    }

    [Fact]
    public void HexenSector3DFloorUsesArg4AsLineIdWhenFlagIsSet()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(0, 0);
        var line = map.Linedefs[0];
        line.Action = 160;
        line.Args[1] = 8;
        line.Args[4] = 12;

        MapFormatConverter.Convert(map, MapFormat.Hexen, MapFormat.Udmf, gc);

        Assert.Equal(12, line.Tag);
        Assert.Equal(0, line.Args[1]);
        Assert.Equal(0, line.Args[4]);
    }

    [Fact]
    public void HexenSector3DFloorCombinesHighTagIntoArg0WhenLineIdFlagIsClear()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(0, 0);
        var line = map.Linedefs[0];
        line.Action = 160;
        line.Args[0] = 10;
        line.Args[4] = 2;

        MapFormatConverter.Convert(map, MapFormat.Hexen, MapFormat.Udmf, gc);

        Assert.Equal(522, line.Args[0]);
        Assert.Equal(0, line.Args[4]);
        Assert.Equal(0, line.Tag);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(5, 4)]
    [InlineData(181, 2)]
    [InlineData(215, 0)]
    [InlineData(222, 0)]
    public void UdmfTagArgActionsConvertToHexenArgsAndClearTag(int action, int tagArg)
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(0, 0);
        var line = map.Linedefs[0];
        line.Action = action;
        line.Tag = 77;

        MapFormatConverter.Convert(map, MapFormat.Udmf, MapFormat.Hexen, gc);

        Assert.Equal(77, line.Args[tagArg]);
        Assert.Equal(0, line.Tag);
    }

    [Fact]
    public void UdmfTranslucentLineConvertsLineIdAndFlagsToHexenArgs()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(0, 0);
        var line = map.Linedefs[0];
        line.Action = 208;
        line.Tag = 33;
        line.UdmfFlags.Add("jumpover");
        line.UdmfFlags.Add("wrapmidtex");

        MapFormatConverter.Convert(map, MapFormat.Udmf, MapFormat.Hexen, gc);

        Assert.Equal(33, line.Args[0]);
        Assert.Equal(2 | 16, line.Args[3]);
        Assert.Equal(0, line.Tag);
    }

    [Fact]
    public void UdmfSector3DFloorSplitsLargeArg0ForHexen()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(0, 0);
        var line = map.Linedefs[0];
        line.Action = 160;
        line.Args[0] = 522;
        line.Tag = 99;

        MapFormatConverter.Convert(map, MapFormat.Udmf, MapFormat.Hexen, gc);

        Assert.Equal(10, line.Args[0]);
        Assert.Equal(2, line.Args[4]);
        Assert.Equal(0, line.Tag);
    }

    [Fact]
    public void UdmfSector3DFloorMovesLineIdToArg4ForHexen()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(0, 0);
        var line = map.Linedefs[0];
        line.Action = 160;
        line.Args[0] = 10;
        line.Tag = 12;

        MapFormatConverter.Convert(map, MapFormat.Udmf, MapFormat.Hexen, gc);

        Assert.Equal(12, line.Args[4]);
        Assert.Equal(8, line.Args[1]);
        Assert.Equal(0, line.Tag);
    }

    [Fact]
    public void LargeUdmfLineIdWithoutActionConvertsToHexenLineSetIdentification()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = MapWithLine(0, 0);
        var line = map.Linedefs[0];
        line.Tag = 556;
        line.UdmfFlags.Add("zoneboundary");
        line.UdmfFlags.Add("clipmidtex");

        MapFormatConverter.Convert(map, MapFormat.Udmf, MapFormat.Hexen, gc);

        Assert.Equal(121, line.Action);
        Assert.Equal(44, line.Args[0]);
        Assert.Equal(2, line.Args[4]);
        Assert.Equal(1 | 8, line.Args[1]);
        Assert.Equal(0, line.Tag);
    }

    [Fact]
    public void UdmfToBinaryClearsUdmfOnlyElementData()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var map = SquareWithBlockingLine();
        var vertex = map.Vertices[0];
        var line = map.Linedefs[0];
        var sector = map.Sectors[0];
        var sidedef = map.Sidedefs[0];
        var thing = new Thing(new Vector2D(10, 10), 1)
        {
            Pitch = 10,
            Roll = 20,
            ScaleX = 2.0,
            ScaleY = 0.5,
        };
        map.Things.Add(thing);
        vertex.ZCeiling = 96.0;
        vertex.ZFloor = -16.0;
        vertex.Fields["comment"] = "slope anchor";
        line.UdmfFlags.Add("blocking");
        line.Fields["renderstyle"] = "add";
        sidedef.UdmfFlags.Add("lightabsolute");
        sidedef.MidTexture = "STARTAN";
        sidedef.OffsetX = 4;
        sidedef.OffsetY = 8;
        sidedef.Fields["offsetx_mid"] = 3.0;
        sidedef.Fields["offsety_mid"] = -2.0;
        sector.UdmfFlags.Add("secret");
        sector.Fields["lightcolor"] = 255;
        sector.FloorSlope = new Vector3D(0, 1, 1);
        sector.FloorSlopeOffset = -32.0;
        sector.CeilSlope = new Vector3D(0, -1, 1);
        sector.CeilSlopeOffset = 64.0;
        var virtualSector = map.AddSector();
        virtualSector.Fields[MapSet.VirtualSectorField] = 0;
        virtualSector.Fields["lightcolor"] = 128;
        thing.UdmfFlags.Add("ambush");
        thing.Fields["conversation"] = 3;

        MapFormatConverter.Convert(map, MapFormat.Udmf, MapFormat.Hexen, gc);

        Assert.Equal(1, line.Flags);
        Assert.Equal(8 | 16, thing.Flags);
        Assert.True(double.IsNaN(vertex.ZCeiling));
        Assert.True(double.IsNaN(vertex.ZFloor));
        Assert.Empty(vertex.Fields);
        Assert.Empty(line.UdmfFlags);
        Assert.Empty(line.Fields);
        Assert.Empty(sidedef.UdmfFlags);
        Assert.Empty(sidedef.Fields);
        Assert.Equal(7, sidedef.OffsetX);
        Assert.Equal(6, sidedef.OffsetY);
        Assert.Empty(sector.UdmfFlags);
        Assert.Empty(sector.Fields);
        Assert.Equal(new[] { MapSet.VirtualSectorField }, virtualSector.Fields.Keys);
        Assert.Equal(0, sector.FloorSlope.GetLengthSq());
        Assert.True(double.IsNaN(sector.FloorSlopeOffset));
        Assert.Equal(0, sector.CeilSlope.GetLengthSq());
        Assert.True(double.IsNaN(sector.CeilSlopeOffset));
        Assert.Empty(thing.UdmfFlags);
        Assert.Empty(thing.Fields);
        Assert.Equal(0, thing.Pitch);
        Assert.Equal(0, thing.Roll);
        Assert.Equal(1.0, thing.ScaleX);
        Assert.Equal(1.0, thing.ScaleY);
    }
}
