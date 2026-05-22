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

        var s0 = map.Sectors[0];
        var sr = r.Sectors[0];
        Assert.Equal(s0.FloorHeight,  sr.FloorHeight);
        Assert.Equal(s0.CeilHeight,   sr.CeilHeight);
        Assert.Equal(s0.FloorTexture, sr.FloorTexture);
        Assert.Equal(s0.CeilTexture,  sr.CeilTexture);
        Assert.Equal(s0.Brightness,   sr.Brightness);

        for (int i = 0; i < map.Things.Count; i++)
        {
            Assert.Equal(map.Things[i].Position, r.Things[i].Position);
            Assert.Equal(map.Things[i].Type,     r.Things[i].Type);
            Assert.Equal(map.Things[i].Angle,    r.Things[i].Angle);
        }
    }

    [Fact]
    public void DefaultsAreOmitted()
    {
        // An empty-ish sector should emit only the block header + braces (no zero/default assignments).
        var map = new MapSet { Namespace = "Doom" };
        map.Sectors.Add(new Sector { Index = 0, FloorTexture = "-", CeilTexture = "-", Brightness = 160 });

        var text = UdmfMapWriter.Write(map);
        Assert.DoesNotContain("heightfloor", text);
        Assert.DoesNotContain("heightceiling", text);
        Assert.DoesNotContain("texturefloor", text);
        Assert.DoesNotContain("textureceiling", text);
        Assert.DoesNotContain("lightlevel", text);
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
            vertex { x = 0.0; y = 0.0; user_weight = 12.5; }
            sector { heightfloor = 0; heightceiling = 64; texturefloor = "A"; textureceiling = "B"; lightlevel = 160; lightcolor = 16711680; xscalefloor = 2.0; comment = "lava pit"; }
            sidedef { sector = 0; scalex_mid = 1.5; }
            thing { x = 10.0; y = 10.0; type = 1; health = 200; nicename = "boss"; }
            """;
        var map = UdmfMapLoader.Load(udmf, out _)!;

        // Custom fields captured on load.
        Assert.Equal(12.5, (double)map.Vertices[0].Fields["user_weight"]);
        Assert.Equal(16711680, (int)map.Sectors[0].Fields["lightcolor"]);
        Assert.Equal(2.0, (double)map.Sectors[0].Fields["xscalefloor"]);
        Assert.Equal("lava pit", (string)map.Sectors[0].Fields["comment"]);
        Assert.Equal(1.5, (double)map.Sidedefs[0].Fields["scalex_mid"]);
        Assert.Equal(200, (int)map.Things[0].Fields["health"]);
        Assert.Equal("boss", (string)map.Things[0].Fields["nicename"]);

        // Survive a write -> reload cycle.
        var written = UdmfMapWriter.Write(map);
        var r = UdmfMapLoader.Load(written, out var parser)!;
        Assert.Equal(0, parser.ErrorResult);
        Assert.Equal(12.5, (double)r.Vertices[0].Fields["user_weight"]);
        Assert.Equal(16711680, (int)r.Sectors[0].Fields["lightcolor"]);
        Assert.Equal(2.0, (double)r.Sectors[0].Fields["xscalefloor"]);
        Assert.Equal("lava pit", (string)r.Sectors[0].Fields["comment"]);
        Assert.Equal(1.5, (double)r.Sidedefs[0].Fields["scalex_mid"]);
        Assert.Equal(200, (int)r.Things[0].Fields["health"]);
        Assert.Equal("boss", (string)r.Things[0].Fields["nicename"]);
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
}
