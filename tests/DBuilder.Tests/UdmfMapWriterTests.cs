// ABOUTME: Round-trip tests for UdmfMapWriter - load UDMF source through UdmfMapLoader, write, re-load, assert structural equality.
// ABOUTME: Also verifies default-value omission, args/UdmfFlags emission, and TEXTMAP/ENDMAP lump structure.

using System.IO;
using System.Linq;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class UdmfMapWriterTests
{
    private const string SimpleRoom = """
        namespace = "Doom";

        vertex { x = 0;   y = 0;   }
        vertex { x = 256; y = 0;   }
        vertex { x = 256; y = 256; }
        vertex { x = 0;   y = 256; }

        sector { heightfloor = 0; heightceiling = 128; texturefloor = "FLOOR1"; textureceiling = "CEIL1"; lightlevel = 192; }

        sidedef { sector = 0; texturemiddle = "STARTAN"; }
        sidedef { sector = 0; texturemiddle = "STARTAN"; }
        sidedef { sector = 0; texturemiddle = "STARTAN"; }
        sidedef { sector = 0; texturemiddle = "STARTAN"; }

        linedef { v1 = 0; v2 = 1; sidefront = 0; blocking = true; }
        linedef { v1 = 1; v2 = 2; sidefront = 1; blocking = true; }
        linedef { v1 = 2; v2 = 3; sidefront = 2; blocking = true; }
        linedef { v1 = 3; v2 = 0; sidefront = 3; blocking = true; }

        thing { x = 128.0; y = 128.0; angle = 90; type = 1;  skill1 = true; skill2 = true; skill3 = true; }
        thing { x = 64.0;  y = 64.0;  angle = 0;  type = 2014; }
        """;

    [Fact]
    public void RoundTripPreservesCountsAndNamespace()
    {
        var map = UdmfMapLoader.Load(SimpleRoom, out _)!;
        var written = UdmfMapWriter.Write(map);
        var reloaded = UdmfMapLoader.Load(written, out var parser);

        Assert.NotNull(reloaded);
        Assert.Equal(0, parser.ErrorResult);
        Assert.Equal("Doom", reloaded!.Namespace);
        Assert.Equal(map.Vertices.Count, reloaded.Vertices.Count);
        Assert.Equal(map.Sectors.Count,  reloaded.Sectors.Count);
        Assert.Equal(map.Sidedefs.Count, reloaded.Sidedefs.Count);
        Assert.Equal(map.Linedefs.Count, reloaded.Linedefs.Count);
        Assert.Equal(map.Things.Count,   reloaded.Things.Count);
    }

    [Fact]
    public void WritesElementsInUdbOrder()
    {
        var map = UdmfMapLoader.Load(SimpleRoom, out _)!;
        var written = UdmfMapWriter.Write(map);

        int vertex = written.IndexOf("vertex // 0", StringComparison.Ordinal);
        int linedef = written.IndexOf("linedef // 0", StringComparison.Ordinal);
        int sidedef = written.IndexOf("sidedef // 0", StringComparison.Ordinal);
        int sector = written.IndexOf("sector // 0", StringComparison.Ordinal);
        int thing = written.IndexOf("thing // 0", StringComparison.Ordinal);

        Assert.True(vertex >= 0);
        Assert.True(vertex < linedef);
        Assert.True(linedef < sidedef);
        Assert.True(sidedef < sector);
        Assert.True(sector < thing);
    }

    [Fact]
    public void WritesUdbStyleCrlfLineEndings()
    {
        var map = UdmfMapLoader.Load(SimpleRoom, out _)!;
        var written = UdmfMapWriter.Write(map);

        Assert.Contains("\r\nvertex // 0\r\n", written);
        Assert.DoesNotContain('\n', written.Replace("\r\n", "", StringComparison.Ordinal));
    }

    [Fact]
    public void WritesFieldsInUdbOrder()
    {
        var map = new MapSet { Namespace = "ZDoom" };
        var vertex = new Vertex(new Vector2D(0, 0)) { ZCeiling = 64, ZFloor = -16 };
        map.Vertices.Add(vertex);
        map.Vertices.Add(new Vertex(new Vector2D(128, 0)));

        var sector = new Sector { Index = 0, FloorTexture = "FLOOR", CeilTexture = "CEIL" };
        map.Sectors.Add(sector);

        var side = new Sidedef
        {
            Sector = sector,
            OffsetX = 8,
            OffsetY = 16,
            HighTexture = "TOP",
            LowTexture = "BOT",
            MidTexture = "MID",
        };
        map.Sidedefs.Add(side);

        var line = new Linedef(map.Vertices[0], map.Vertices[1]) { Front = side, Tag = 99, Action = 80 };
        side.Line = line;
        line.Args[0] = 7;
        map.Linedefs.Add(line);

        map.Things.Add(new Thing { Position = new Vector2D(32, 48), Type = 3001, Tag = 12, Action = 80 });

        var written = UdmfMapWriter.Write(map);

        AssertInOrder(Block(written, "vertex // 0"), "x = 0.0;", "y = 0.0;", "zceiling = 64.0;", "zfloor = -16.0;");
        AssertInOrder(Block(written, "linedef // 0"), "id = 99;", "v1 = 0;", "v2 = 1;", "sidefront = 0;", "sideback = -1;", "special = 80;", "arg0 = 7;");
        AssertInOrder(Block(written, "sidedef // 0"), "offsetx = 8;", "offsety = 16;", "texturetop = \"TOP\";", "texturebottom = \"BOT\";", "texturemiddle = \"MID\";", "sector = 0;");
        AssertInOrder(Block(written, "thing // 0"), "id = 12;", "x = 32.0;", "y = 48.0;", "angle = 0;", "type = 3001;", "special = 80;");
    }

    [Fact]
    public void RoundTripPreservesTopology()
    {
        var map = UdmfMapLoader.Load(SimpleRoom, out _)!;
        var written = UdmfMapWriter.Write(map);
        var r = UdmfMapLoader.Load(written, out _)!;

        for (int i = 0; i < map.Vertices.Count; i++)
            Assert.Equal(map.Vertices[i].Position, r.Vertices[i].Position);

        for (int i = 0; i < map.Linedefs.Count; i++)
        {
            var lo = map.Linedefs[i];
            var lr = r.Linedefs[i];
            Assert.Same(r.Vertices[map.Vertices.IndexOf(lo.Start)], lr.Start);
            Assert.Same(r.Vertices[map.Vertices.IndexOf(lo.End)],   lr.End);
            Assert.Equal(lo.UdmfFlags.OrderBy(s => s), lr.UdmfFlags.OrderBy(s => s));
        }

        map.Sidedefs[0].UdmfFlags.Add("lightabsolute");
        var withSidedefFlag = UdmfMapLoader.Load(UdmfMapWriter.Write(map), out _)!;
        Assert.Contains("lightabsolute", withSidedefFlag.Sidedefs[0].UdmfFlags);

        var s0 = map.Sectors[0];
        var sr = r.Sectors[0];
        Assert.Equal(s0.FloorHeight,  sr.FloorHeight);
        Assert.Equal(s0.CeilHeight,   sr.CeilHeight);
        Assert.Equal(s0.FloorTexture, sr.FloorTexture);
        Assert.Equal(s0.CeilTexture,  sr.CeilTexture);
        Assert.Equal(s0.Brightness,   sr.Brightness);

        map.Sectors[0].UdmfFlags.Add("secret");
        var withSectorFlag = UdmfMapLoader.Load(UdmfMapWriter.Write(map), out _)!;
        Assert.Contains("secret", withSectorFlag.Sectors[0].UdmfFlags);

        for (int i = 0; i < map.Things.Count; i++)
        {
            Assert.Equal(map.Things[i].Position, r.Things[i].Position);
            Assert.Equal(map.Things[i].Type,     r.Things[i].Type);
            Assert.Equal(map.Things[i].Angle,    r.Things[i].Angle);
        }
    }

    [Fact]
    public void SectorCoreFieldsAreAlwaysEmittedLikeUdb()
    {
        var map = new MapSet { Namespace = "Doom" };
        map.Sectors.Add(new Sector { Index = 0, FloorTexture = "-", CeilTexture = "-", CeilHeight = 0, Brightness = 160 });

        var text = UdmfMapWriter.Write(map);
        Assert.Contains("heightfloor = 0;", text);
        Assert.Contains("heightceiling = 0;", text);
        Assert.Contains("texturefloor = \"-\";", text);
        Assert.Contains("textureceiling = \"-\";", text);
        Assert.Contains("lightlevel = 160;", text);
        Assert.DoesNotContain("special", text);
    }

    [Fact]
    public void ArgsAreEmittedForLinedef()
    {
        var map = new MapSet { Namespace = "Hexen" };
        map.Vertices.Add(new Vertex(new Vector2D(0, 0)));
        map.Vertices.Add(new Vertex(new Vector2D(10, 0)));
        var l = new Linedef(map.Vertices[0], map.Vertices[1]) { Action = 80 };
        l.Args[0] = 7; l.Args[2] = 13;
        map.Linedefs.Add(l);

        var text = UdmfMapWriter.Write(map);
        Assert.Contains("arg0 = 7;", text);
        Assert.Contains("arg2 = 13;", text);
        Assert.DoesNotContain("arg1 = 0;", text);
        Assert.DoesNotContain("arg3 = 0;", text);
        Assert.Contains("special = 80;", text);
    }

    [Fact]
    public void ArgsAreEmittedForThing()
    {
        var map = new MapSet { Namespace = "Hexen" };
        var t = new Thing { Position = new Vector2D(50, 50), Type = 9001, Tag = 42, Action = 80 };
        t.Args[1] = 100;
        map.Things.Add(t);

        var text = UdmfMapWriter.Write(map);
        Assert.Contains("arg1 = 100;", text);
        Assert.Contains("id = 42;", text);
        Assert.Contains("special = 80;", text);
    }

    [Fact]
    public void ThingAngleIsAlwaysEmittedLikeUdb()
    {
        var map = new MapSet { Namespace = "Doom" };
        map.Things.Add(new Thing { Position = new Vector2D(16, 32), Type = 1, Angle = 0 });

        var text = UdmfMapWriter.Write(map);

        Assert.Contains("angle = 0;", text);
    }

    [Fact]
    public void FractionalCoordinatesPreserveDecimalPoint()
    {
        // The parser distinguishes float from int by the decimal point - integer-valued doubles need ".0".
        var map = new MapSet { Namespace = "Doom" };
        map.Vertices.Add(new Vertex(new Vector2D(128, 64)));
        var text = UdmfMapWriter.Write(map);
        Assert.Contains("x = 128.0;", text);
        Assert.Contains("y = 64.0;", text);
    }

    [Fact]
    public void DoubleValuesUseUdbUniversalParserFormat()
    {
        var map = new MapSet { Namespace = "Doom" };
        map.Vertices.Add(new Vertex(new Vector2D(0.000000123456789, 1.234567890123456)));
        map.Things.Add(new Thing { Position = new Vector2D(1000000000000000d, -0.000000123456789), Type = 1 });

        var text = UdmfMapWriter.Write(map);

        Assert.Contains("x = 0.000000123456789;", text);
        Assert.Contains("y = 1.23456789012346;", text);
        Assert.Contains("x = 1000000000000000.0;", text);
        Assert.Contains("y = -0.000000123456789;", text);
        Assert.DoesNotContain("E", text, StringComparison.Ordinal);
        Assert.DoesNotContain("e-", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FloatValuesUseUdbUniversalParserFormat()
    {
        var map = new MapSet { Namespace = "Doom" };
        map.Fields["map_float"] = 1.25f;
        map.Vertices.Add(new Vertex(new Vector2D(0, 0)));
        map.Vertices[0].Fields["vertex_float"] = 0.125f;
        map.UnknownUdmfData.Add(new UnknownUdmfEntry("unknown_float", 2.5f));

        var text = UdmfMapWriter.Write(map);

        Assert.Contains("map_float = 1.250;", text);
        Assert.Contains("vertex_float = 0.125;", text);
        Assert.Contains("unknown_float = 2.500;", text);
    }

    [Fact]
    public void TwoSidedLineRoundTripsBothSidedefs()
    {
        const string udmf = """
            namespace = "Doom";
            vertex { x = 0; y = 0; }
            vertex { x = 100; y = 0; }
            sector { heightfloor = 0; heightceiling = 64; texturefloor = "A"; textureceiling = "B"; lightlevel = 160; }
            sector { heightfloor = 0; heightceiling = 64; texturefloor = "A"; textureceiling = "B"; lightlevel = 160; }
            sidedef { sector = 0; }
            sidedef { sector = 1; }
            linedef { v1 = 0; v2 = 1; sidefront = 0; sideback = 1; twosided = true; }
            """;
        var map = UdmfMapLoader.Load(udmf, out _)!;
        var written = UdmfMapWriter.Write(map);
        var r = UdmfMapLoader.Load(written, out _)!;

        var l = r.Linedefs[0];
        Assert.NotNull(l.Front);
        Assert.NotNull(l.Back);
        Assert.Same(r.Sectors[0], l.Front!.Sector);
        Assert.Same(r.Sectors[1], l.Back!.Sector);
    }

    [Fact]
    public void EmitsMissingSidedefReferencesLikeUdb()
    {
        var map = new MapSet { Namespace = "Doom" };
        map.Vertices.Add(new Vertex(new Vector2D(0, 0)));
        map.Vertices.Add(new Vertex(new Vector2D(10, 0)));
        map.Linedefs.Add(new Linedef(map.Vertices[0], map.Vertices[1]));

        var text = UdmfMapWriter.Write(map);

        Assert.Contains("sidefront = -1;", text);
        Assert.Contains("sideback = -1;", text);
    }

    [Fact]
    public void MoreIdsPreserveZeroAndDuplicateExtraTagsLikeUdb()
    {
        var map = new MapSet { Namespace = "ZDoom" };
        var sector = new Sector { Index = 0, FloorTexture = "A", CeilTexture = "B" };
        sector.Tags.Clear();
        sector.Tags.AddRange([5, 0, 5, 7, 7, 8]);
        map.Sectors.Add(sector);
        map.Vertices.Add(new Vertex(new Vector2D(0, 0)));
        map.Vertices.Add(new Vertex(new Vector2D(64, 0)));
        var line = new Linedef(map.Vertices[0], map.Vertices[1]) { Tag = 11 };
        line.Tags.AddRange([0, 11, 12, 12, 13]);
        map.Linedefs.Add(line);

        var text = UdmfMapWriter.Write(map);

        Assert.Contains("id = 5;", Block(text, "sector // 0"));
        Assert.Contains("moreids = \"0 5 7 7 8\";", Block(text, "sector // 0"));
        Assert.Contains("id = 11;", Block(text, "linedef // 0"));
        Assert.Contains("moreids = \"0 11 12 12 13\";", Block(text, "linedef // 0"));
    }

    [Fact]
    public void WriteMapEmitsMarkerTextmapAndEndmap()
    {
        var map = UdmfMapLoader.Load(SimpleRoom, out _)!;
        var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            UdmfMapWriter.WriteMap(map, wad, "MAP01", 0);
        }
        ms.Position = 0;
        using var rwad = new WAD(ms, openreadonly: true);

        Assert.Equal("MAP01",   rwad.Lumps[0].Name);
        Assert.Equal("TEXTMAP", rwad.Lumps[1].Name);
        Assert.Equal("ENDMAP",  rwad.Lumps[2].Name);
        Assert.True(rwad.Lumps[1].Length > 0);
        Assert.Equal(0, rwad.Lumps[0].Length);
        Assert.Equal(0, rwad.Lumps[2].Length);
    }

    [Fact]
    public void SaveToWadFileAndReload()
    {
        // Mirrors the viewer's Ctrl+S save: write UDMF into a file-backed WAD, then reopen and reload.
        var map = UdmfMapLoader.Load(SimpleRoom, out _)!;
        map.Sectors[0].Fields["lightcolor"] = 255; // a custom field to confirm fidelity through the file

        string path = Path.Combine(Path.GetTempPath(), "dbuilder_savetest_" + System.Guid.NewGuid().ToString("N") + ".wad");
        try
        {
            using (var wad = new WAD(path, openreadonly: false))
            {
                UdmfMapWriter.WriteMap(map, wad, "MAP01", 0);
            }
            Assert.True(File.Exists(path));

            using var reopened = new WAD(path, openreadonly: true);
            Assert.True(HexenOrUdmfHasTextmap(reopened));
            var r = UdmfMapLoader.Load(System.Text.Encoding.ASCII.GetString(reopened.FindLump("TEXTMAP")!.Stream.ReadAllBytes()), out var parser)!;
            Assert.Equal(0, parser.ErrorResult);
            Assert.Equal(map.Vertices.Count, r.Vertices.Count);
            Assert.Equal(map.Linedefs.Count, r.Linedefs.Count);
            Assert.Equal(255, (int)r.Sectors[0].Fields["lightcolor"]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static bool HexenOrUdmfHasTextmap(WAD wad) => wad.FindLump("TEXTMAP") != null;

    [Fact]
    public void WriteMapThrowsOnReadOnlyWad()
    {
        var map = UdmfMapLoader.Load(SimpleRoom, out _)!;
        var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            wad.Insert("DUMMY", 0, 0);
            wad.WriteHeaders();
        }
        ms.Position = 0;
        using var rwad = new WAD(ms, openreadonly: true);
        Assert.Throws<IOException>(() => UdmfMapWriter.WriteMap(map, rwad, "MAP01", 0));
    }

    [Fact]
    public void EmptyNamespaceDefaultsToDoom()
    {
        var map = new MapSet { Namespace = "" };
        var text = UdmfMapWriter.Write(map);
        Assert.Contains("namespace = \"Doom\";", text);
    }

    [Fact]
    public void CustomFieldsRoundTripThroughLoadWriteLoad()
    {
        const string udmf = """
            namespace = "ZDoom";
            vertex { x = 0.0; y = 0.0; user_weight = 12.5; locked = true; }
            vertex { x = 64.0; y = 0.0; }
            sector { heightfloor = 0; heightceiling = 64; texturefloor = "A"; textureceiling = "B"; lightlevel = 160; lightcolor = 16711680; xscalefloor = 2.0; comment = "lava pit"; secretmarked = true; }
            sidedef { sector = 0; scalex_mid = 1.5; wraps_mid = true; }
            linedef { v1 = 0; v2 = 1; sidefront = 0; }
            thing { x = 10.0; y = 10.0; type = 1; health = 200; nicename = "boss"; }
            """;
        var map = UdmfMapLoader.Load(udmf, out _)!;

        // Custom fields captured on load.
        Assert.Equal(12.5, (double)map.Vertices[0].Fields["user_weight"]);
        Assert.True((bool)map.Vertices[0].Fields["locked"]);
        Assert.Equal(16711680, (int)map.Sectors[0].Fields["lightcolor"]);
        Assert.Equal(2.0, (double)map.Sectors[0].Fields["xscalefloor"]);
        Assert.Equal("lava pit", (string)map.Sectors[0].Fields["comment"]);
        Assert.True((bool)map.Sectors[0].Fields["secretmarked"]);
        Assert.Equal(1.5, (double)map.Sidedefs[0].Fields["scalex_mid"]);
        Assert.True((bool)map.Sidedefs[0].Fields["wraps_mid"]);
        Assert.Equal(200, (int)map.Things[0].Fields["health"]);
        Assert.Equal("boss", (string)map.Things[0].Fields["nicename"]);

        // Survive a write -> reload cycle.
        var written = UdmfMapWriter.Write(map);
        var r = UdmfMapLoader.Load(written, out var parser)!;
        Assert.Equal(0, parser.ErrorResult);
        Assert.Equal(12.5, (double)r.Vertices[0].Fields["user_weight"]);
        Assert.True((bool)r.Vertices[0].Fields["locked"]);
        Assert.Equal(16711680, (int)r.Sectors[0].Fields["lightcolor"]);
        Assert.Equal(2.0, (double)r.Sectors[0].Fields["xscalefloor"]);
        Assert.Equal("lava pit", (string)r.Sectors[0].Fields["comment"]);
        Assert.True((bool)r.Sectors[0].Fields["secretmarked"]);
        Assert.Equal(1.5, (double)r.Sidedefs[0].Fields["scalex_mid"]);
        Assert.True((bool)r.Sidedefs[0].Fields["wraps_mid"]);
        Assert.Equal(200, (int)r.Things[0].Fields["health"]);
        Assert.Equal("boss", (string)r.Things[0].Fields["nicename"]);
    }

    [Fact]
    public void LongCustomFieldsRoundTripThroughLoadWriteLoad()
    {
        const long largeValue = 4294967296L;
        const string udmf = """
            namespace = "ZDoom";
            vertex { x = 0.0; y = 0.0; user_large = 4294967296; }
            sector { heightfloor = 0; heightceiling = 64; texturefloor = "A"; textureceiling = "B"; lightlevel = 160; user_large = 4294967296; }
            """;

        var map = UdmfMapLoader.Load(udmf, out _)!;

        Assert.Equal(largeValue, Assert.IsType<long>(map.Vertices[0].Fields["user_large"]));
        Assert.Equal(largeValue, Assert.IsType<long>(map.Sectors[0].Fields["user_large"]));

        var written = UdmfMapWriter.Write(map);
        Assert.Contains("user_large = 4294967296;", written);

        var reloaded = UdmfMapLoader.Load(written, out var parser)!;
        Assert.Equal(0, parser.ErrorResult);
        Assert.Equal(largeValue, Assert.IsType<long>(reloaded.Vertices[0].Fields["user_large"]));
        Assert.Equal(largeValue, Assert.IsType<long>(reloaded.Sectors[0].Fields["user_large"]));
    }

    [Fact]
    public void LongMapFieldsRoundTripThroughLoadWriteLoad()
    {
        const long largeValue = 4294967296L;
        const string udmf = """
            namespace = "ZDoom";
            user_large = 4294967296;
            """;

        var map = UdmfMapLoader.Load(udmf, out _)!;

        Assert.Equal(largeValue, Assert.IsType<long>(map.Fields["user_large"]));

        var written = UdmfMapWriter.Write(map);
        Assert.Contains("user_large = 4294967296;", written);

        var reloaded = UdmfMapLoader.Load(written, out var parser)!;
        Assert.Equal(0, parser.ErrorResult);
        Assert.Equal(largeValue, Assert.IsType<long>(reloaded.Fields["user_large"]));
    }

    [Fact]
    public void UnsignedIntegerCustomFieldsAreWrittenAsUdmfNumbers()
    {
        var map = new MapSet { Namespace = "ZDoom" };
        map.Fields["map_byte"] = (byte)7;
        map.Fields["map_short"] = (ushort)513;
        map.Fields["map_uint"] = uint.MaxValue;
        map.Vertices.Add(new Vertex(new Vector2D(0, 0)));
        map.Vertices[0].Fields["vertex_sbyte"] = (sbyte)-8;
        map.Vertices[0].Fields["vertex_ushort"] = (ushort)1024;
        map.Sectors.Add(new Sector { Index = 0, FloorTexture = "A", CeilTexture = "B" });
        map.Sectors[0].Fields["sector_uint"] = 4000000000u;

        string written = UdmfMapWriter.Write(map);

        Assert.Contains("map_byte = 7;", written);
        Assert.Contains("map_short = 513;", written);
        Assert.Contains("map_uint = 4294967295;", written);
        Assert.Contains("vertex_sbyte = -8;", written);
        Assert.Contains("vertex_ushort = 1024;", written);
        Assert.Contains("sector_uint = 4000000000;", written);

        MapSet reloaded = UdmfMapLoader.Load(written, out UniversalParser parser)!;
        Assert.Equal(0, parser.ErrorResult);
        Assert.Equal(7, Assert.IsType<int>(reloaded.Fields["map_byte"]));
        Assert.Equal(513, Assert.IsType<int>(reloaded.Fields["map_short"]));
        Assert.Equal((long)uint.MaxValue, Assert.IsType<long>(reloaded.Fields["map_uint"]));
        Assert.Equal(-8, Assert.IsType<int>(reloaded.Vertices[0].Fields["vertex_sbyte"]));
        Assert.Equal(1024, Assert.IsType<int>(reloaded.Vertices[0].Fields["vertex_ushort"]));
        Assert.Equal(4000000000L, Assert.IsType<long>(reloaded.Sectors[0].Fields["sector_uint"]));
    }

    [Fact]
    public void UnsignedIntegerUnknownFieldsAreWrittenAsUdmfNumbers()
    {
        var map = new MapSet { Namespace = "ZDoom" };
        map.UnknownUdmfData.Add(new UnknownUdmfEntry("editorstate", new List<UnknownUdmfEntry>
        {
            new("byte_value", (byte)9),
            new("uint_value", uint.MaxValue),
            new("ulong_value", (ulong)long.MaxValue),
        }));

        string written = UdmfMapWriter.Write(map);

        Assert.Contains("byte_value = 9;", written);
        Assert.Contains("uint_value = 4294967295;", written);
        Assert.Contains("ulong_value = 9223372036854775807;", written);

        MapSet reloaded = UdmfMapLoader.Load(written, out UniversalParser parser)!;
        Assert.Equal(0, parser.ErrorResult);
        UnknownUdmfEntry state = Assert.Single(reloaded.UnknownUdmfData);
        Assert.Equal(9, Assert.IsType<int>(state.Children[0].Value));
        Assert.Equal((long)uint.MaxValue, Assert.IsType<long>(state.Children[1].Value));
        Assert.Equal(long.MaxValue, Assert.IsType<long>(state.Children[2].Value));
    }

    [Fact]
    public void FalseBooleanLinedefAndThingCustomFieldsRoundTrip()
    {
        const string udmf = """
            namespace = "ZDoom";
            vertex { x = 0.0; y = 0.0; }
            vertex { x = 64.0; y = 0.0; }
            linedef { v1 = 0; v2 = 1; sidefront = -1; sideback = -1; user_enabled = false; }
            thing { x = 8.0; y = 16.0; type = 3001; user_enabled = false; }
            """;

        var map = UdmfMapLoader.Load(udmf, out _)!;

        Assert.False(Assert.IsType<bool>(map.Linedefs[0].Fields["user_enabled"]));
        Assert.False(Assert.IsType<bool>(map.Things[0].Fields["user_enabled"]));

        var written = UdmfMapWriter.Write(map);
        Assert.Contains("user_enabled = false;", written);

        var reloaded = UdmfMapLoader.Load(written, out var parser)!;
        Assert.Equal(0, parser.ErrorResult);
        Assert.False(Assert.IsType<bool>(reloaded.Linedefs[0].Fields["user_enabled"]));
        Assert.False(Assert.IsType<bool>(reloaded.Things[0].Fields["user_enabled"]));
    }

    [Fact]
    public void ManagedFieldsNotDuplicatedIntoCustomFields()
    {
        var map = UdmfMapLoader.Load(SimpleRoom, out _)!;
        // Standard typed keys must not leak into the custom Fields bag.
        Assert.DoesNotContain("x", map.Vertices[0].Fields.Keys);
        Assert.DoesNotContain("heightfloor", map.Sectors[0].Fields.Keys);
        Assert.DoesNotContain("sector", map.Sidedefs[0].Fields.Keys);
        Assert.DoesNotContain("v1", map.Linedefs[0].Fields.Keys);
        Assert.DoesNotContain("type", map.Things[0].Fields.Keys);
        // Bool flags are captured as UdmfFlags, not custom Fields.
        Assert.DoesNotContain("blocking", map.Linedefs[0].Fields.Keys);
    }

    [Fact]
    public void ThingPitchRollScaleRoundTrip()
    {
        const string udmf = """
            namespace = "ZDoom";
            thing { x = 0.0; y = 0.0; type = 1; pitch = 30; roll = 45; scalex = 2.0; scaley = 0.5; }
            thing { x = 8.0; y = 8.0; type = 2; }
            """;
        var map = UdmfMapLoader.Load(udmf, out _)!;

        var t = map.Things[0];
        Assert.Equal(30, t.Pitch);
        Assert.Equal(45, t.Roll);
        Assert.Equal(2.0, t.ScaleX);
        Assert.Equal(0.5, t.ScaleY);
        // Defaults on the untouched thing.
        Assert.Equal(0, map.Things[1].Pitch);
        Assert.Equal(1.0, map.Things[1].ScaleX);
        Assert.Equal(1.0, map.Things[1].ScaleY);

        // Round-trip through the writer.
        var written = UdmfMapWriter.Write(map);
        Assert.Contains("pitch = 30;", written);
        Assert.Contains("roll = 45;", written);
        Assert.Contains("scalex = 2.0;", written);
        Assert.Contains("scaley = 0.5;", written);

        var r = UdmfMapLoader.Load(written, out var parser)!;
        Assert.Equal(0, parser.ErrorResult);
        Assert.Equal(30, r.Things[0].Pitch);
        Assert.Equal(45, r.Things[0].Roll);
        Assert.Equal(2.0, r.Things[0].ScaleX);
        Assert.Equal(0.5, r.Things[0].ScaleY);
    }

    [Fact]
    public void UniformScaleShorthandOverridesScaleXY()
    {
        const string udmf = """
            namespace = "ZDoom";
            thing { x = 0.0; y = 0.0; type = 1; scale = 3.0; }
            """;
        var map = UdmfMapLoader.Load(udmf, out _)!;
        Assert.Equal(3.0, map.Things[0].ScaleX);
        Assert.Equal(3.0, map.Things[0].ScaleY);
        // "scale" must not leak into custom Fields.
        Assert.DoesNotContain("scale", map.Things[0].Fields.Keys);
    }

    [Fact]
    public void DefaultScaleEmitsNothing()
    {
        var map = new MapSet { Namespace = "Doom" };
        map.Things.Add(new Thing { Position = new Vector2D(0, 0), Type = 1 });
        var text = UdmfMapWriter.Write(map);
        Assert.DoesNotContain("scalex", text);
        Assert.DoesNotContain("scaley", text);
        Assert.DoesNotContain("pitch", text);
        Assert.DoesNotContain("roll", text);
    }

    [Fact]
    public void VertexZAndSectorSlopesRoundTrip()
    {
        const string udmf = """
            namespace = "ZDoom";
            vertex { x = 0.0; y = 0.0; zfloor = -8.0; zceiling = 200.0; }
            vertex { x = 64.0; y = 0.0; }
            sector { heightfloor = 0; heightceiling = 128; texturefloor = "A"; textureceiling = "B"; lightlevel = 160; floorplane_a = 0.0; floorplane_b = 0.7071; floorplane_c = 0.7071; floorplane_d = -32.0; }
            """;
        var map = UdmfMapLoader.Load(udmf, out _)!;

        Assert.Equal(-8.0, map.Vertices[0].ZFloor);
        Assert.Equal(200.0, map.Vertices[0].ZCeiling);
        Assert.True(double.IsNaN(map.Vertices[1].ZFloor));   // unset
        Assert.True(double.IsNaN(map.Vertices[1].ZCeiling)); // unset

        var s = map.Sectors[0];
        Assert.True(s.FloorSlope.GetLengthSq() > 0);
        Assert.Equal(-32.0, s.FloorSlopeOffset);
        Assert.True(s.CeilSlope.GetLengthSq() == 0); // no ceiling slope set
        // Normal is unit length after normalization.
        Assert.Equal(1.0, s.FloorSlope.GetLength(), 1e-6);

        // The slope keys and z keys must not leak into custom Fields.
        Assert.DoesNotContain("floorplane_a", s.Fields.Keys);
        Assert.DoesNotContain("zfloor", map.Vertices[0].Fields.Keys);

        // Round-trip through writer.
        var written = UdmfMapWriter.Write(map);
        Assert.Contains("zfloor = -8.0;", written);
        Assert.Contains("zceiling = 200.0;", written);
        Assert.Contains("floorplane_d = -32.0;", written);
        Assert.DoesNotContain("ceilingplane_a", written); // not set

        var r = UdmfMapLoader.Load(written, out var parser)!;
        Assert.Equal(0, parser.ErrorResult);
        Assert.Equal(-8.0, r.Vertices[0].ZFloor);
        Assert.Equal(200.0, r.Vertices[0].ZCeiling);
        Assert.Equal(-32.0, r.Sectors[0].FloorSlopeOffset);
        Assert.Equal(map.Sectors[0].FloorSlope.x, r.Sectors[0].FloorSlope.x, 1e-9);
        Assert.Equal(map.Sectors[0].FloorSlope.y, r.Sectors[0].FloorSlope.y, 1e-9);
        Assert.Equal(map.Sectors[0].FloorSlope.z, r.Sectors[0].FloorSlope.z, 1e-9);
    }

    [Fact]
    public void FlatSectorEmitsNoSlopePlanes()
    {
        var map = new MapSet { Namespace = "Doom" };
        map.Sectors.Add(new Sector { Index = 0, FloorTexture = "-", CeilTexture = "-" });
        var text = UdmfMapWriter.Write(map);
        Assert.DoesNotContain("floorplane_", text);
        Assert.DoesNotContain("ceilingplane_", text);
    }

    [Fact]
    public void MultiTagsRoundTripViaMoreIds()
    {
        const string udmf = """
            namespace = "ZDoom";
            vertex { x = 0.0; y = 0.0; }
            vertex { x = 10.0; y = 0.0; }
            sector { heightfloor = 0; heightceiling = 64; texturefloor = "A"; textureceiling = "B"; lightlevel = 160; id = 5; moreids = "6 7 8"; }
            linedef { v1 = 0; v2 = 1; special = 80; id = 3; moreids = "9"; }
            """;
        var map = UdmfMapLoader.Load(udmf, out _)!;

        Assert.Equal(new[] { 5, 6, 7, 8 }, map.Sectors[0].Tags);
        Assert.Equal(5, map.Sectors[0].Tag); // primary
        Assert.Equal(new[] { 3, 9 }, map.Linedefs[0].Tags);
        // moreids must not leak into custom Fields.
        Assert.DoesNotContain("moreids", map.Sectors[0].Fields.Keys);

        // Round-trip through writer.
        var written = UdmfMapWriter.Write(map);
        Assert.Contains("id = 5;", written);
        Assert.Contains("moreids = \"6 7 8\";", written);

        var r = UdmfMapLoader.Load(written, out var parser)!;
        Assert.Equal(0, parser.ErrorResult);
        Assert.Equal(new[] { 5, 6, 7, 8 }, r.Sectors[0].Tags);
        Assert.Equal(new[] { 3, 9 }, r.Linedefs[0].Tags);
    }

    [Fact]
    public void SingleTagEmitsNoMoreIds()
    {
        var map = new MapSet { Namespace = "Doom" };
        map.Sectors.Add(new Sector { Index = 0, FloorTexture = "-", CeilTexture = "-", Tag = 4 });
        var text = UdmfMapWriter.Write(map);
        Assert.Contains("id = 4;", text);
        Assert.DoesNotContain("moreids", text);
    }

    [Fact]
    public void TopLevelCustomMapFieldsRoundTrip()
    {
        const string udmf = """
            namespace = "ZDoom";
            author = "tester";
            gravity = 0.5;
            allowjump = true;
            musicorder = 3;
            vertex { x = 0.0; y = 0.0; }
            """;

        var map = UdmfMapLoader.Load(udmf, out _)!;

        Assert.Equal("tester", map.Fields["author"]);
        Assert.Equal(0.5, (double)map.Fields["gravity"]);
        Assert.True((bool)map.Fields["allowjump"]);
        Assert.Equal(3, (int)map.Fields["musicorder"]);

        var written = UdmfMapWriter.Write(map);
        Assert.Contains("author = \"tester\";", written);
        Assert.Contains("gravity = 0.5;", written);
        Assert.Contains("allowjump = true;", written);
        Assert.Contains("musicorder = 3;", written);

        var reloaded = UdmfMapLoader.Load(written, out var parser)!;
        Assert.Equal(0, parser.ErrorResult);
        Assert.Equal("tester", reloaded.Fields["author"]);
        Assert.Equal(0.5, (double)reloaded.Fields["gravity"]);
        Assert.True((bool)reloaded.Fields["allowjump"]);
        Assert.Equal(3, (int)reloaded.Fields["musicorder"]);
    }

    [Fact]
    public void UnknownTopLevelCollectionsRoundTrip()
    {
        const string udmf = """
            namespace = "ZDoom";
            editorstate
            {
                collapsed = true;
                label = "kept";
                nested
                {
                    value = 7;
                }
            }
            vertex { x = 0.0; y = 0.0; }
            """;

        var map = UdmfMapLoader.Load(udmf, out _)!;

        var state = Assert.Single(map.UnknownUdmfData);
        Assert.Equal("editorstate", state.Key);
        Assert.Equal(3, state.Children.Count);
        Assert.Equal("collapsed", state.Children[0].Key);
        Assert.True((bool)state.Children[0].Value);
        Assert.Equal("label", state.Children[1].Key);
        Assert.Equal("kept", state.Children[1].Value);
        Assert.Equal("nested", state.Children[2].Key);
        Assert.Equal(7, state.Children[2].Children[0].Value);

        var written = UdmfMapWriter.Write(map);
        Assert.Contains("editorstate", written);
        Assert.Contains("label = \"kept\";", written);
        Assert.True(written.IndexOf("editorstate", StringComparison.Ordinal) < written.IndexOf("vertex // 0", StringComparison.Ordinal));

        var reloaded = UdmfMapLoader.Load(written, out var parser)!;
        Assert.Equal(0, parser.ErrorResult);
        var reloadedState = Assert.Single(reloaded.UnknownUdmfData);
        Assert.Equal("editorstate", reloadedState.Key);
        Assert.Equal("nested", reloadedState.Children[2].Key);
        Assert.Equal(7, reloadedState.Children[2].Children[0].Value);
    }

    [Fact]
    public void WritesUnknownTopLevelCollectionsBeforeMapFieldsLikeUdb()
    {
        var map = new MapSet { Namespace = "Doom" };
        map.UnknownUdmfData.Add(new UnknownUdmfEntry("editorstate", new List<UnknownUdmfEntry>
        {
            new("label", "kept"),
        }));
        map.Fields["author"] = "tester";
        map.Vertices.Add(new Vertex(new Vector2D(0, 0)));

        var text = UdmfMapWriter.Write(map);

        AssertInOrder(
            NormalizeLineEndings(text),
            "namespace = \"Doom\";",
            "editorstate",
            "author = \"tester\";",
            "vertex // 0");
    }

    [Fact]
    public void UdmfArgsLoadIntoTypedArray()
    {
        const string udmf = """
            namespace = "Hexen";
            vertex { x = 0.0; y = 0.0; }
            vertex { x = 10.0; y = 0.0; }
            linedef { v1 = 0; v2 = 1; special = 80; arg0 = 5; arg2 = 9; }
            """;
        var map = UdmfMapLoader.Load(udmf, out _)!;
        var l = map.Linedefs[0];
        Assert.Equal(5, l.Args[0]);
        Assert.Equal(0, l.Args[1]);
        Assert.Equal(9, l.Args[2]);
        // args must not be duplicated into custom Fields.
        Assert.DoesNotContain("arg0", l.Fields.Keys);
    }

    [Fact]
    public void StringValuesAreEscaped()
    {
        var map = new MapSet { Namespace = "Doom" };
        map.Sectors.Add(new Sector { Index = 0, FloorTexture = "WITH\"QUOTE", CeilTexture = "BACK\\SLASH" });
        var text = UdmfMapWriter.Write(map);
        Assert.Contains("WITH\\\"QUOTE", text);
        Assert.Contains("BACK\\\\SLASH", text);
        // Parser should still load the round-tripped output cleanly.
        var r = UdmfMapLoader.Load(text, out var parser);
        Assert.NotNull(r);
        Assert.Equal(0, parser.ErrorResult);
        Assert.Equal("WITH\"QUOTE", r!.Sectors[0].FloorTexture);
        Assert.Equal("BACK\\SLASH", r.Sectors[0].CeilTexture);
    }

    [Fact]
    public void ControlCharactersInStringsAreEscaped()
    {
        var map = new MapSet { Namespace = "Doom" };
        map.Fields["comment"] = "line1\nline2\tTabbed\rReturn";

        var text = UdmfMapWriter.Write(map);

        Assert.Contains("comment = \"line1\\nline2\\tTabbed\\rReturn\";", text);
        var reloaded = UdmfMapLoader.Load(text, out var parser)!;
        Assert.Equal(0, parser.ErrorResult);
        Assert.Equal("line1\nline2\tTabbed\rReturn", reloaded.Fields["comment"]);
    }

    private static string Block(string text, string header)
    {
        text = NormalizeLineEndings(text);
        int start = text.IndexOf(header, StringComparison.Ordinal);
        Assert.True(start >= 0);
        int next = text.IndexOf("\n\n", start, StringComparison.Ordinal);
        Assert.True(next > start);
        return text[start..next];
    }

    private static string NormalizeLineEndings(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static void AssertInOrder(string text, params string[] fragments)
    {
        int previous = -1;
        foreach (var fragment in fragments)
        {
            int current = text.IndexOf(fragment, StringComparison.Ordinal);
            Assert.True(current > previous, $"Expected '{fragment}' after offset {previous} in:{Environment.NewLine}{text}");
            previous = current;
        }
    }
}
