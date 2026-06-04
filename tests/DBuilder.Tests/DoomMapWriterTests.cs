// ABOUTME: Round-trip tests for DoomMapWriter - byte-exact output against synthetic lumps and load->write->load structural equality.
// ABOUTME: Also exercises reference-equality index resolution for vertex/sidedef/sector references shared across linedefs.

using System.IO;
using System.Linq;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class DoomMapWriterTests
{
    // Mirror of DoomMapLoaderTests.BuildSyntheticDoomMap so we can assert byte-exact equality.
    private static (byte[] things, byte[] linedefs, byte[] sidedefs, byte[] vertexes, byte[] sectors) BuildSyntheticLumps()
    {
        var vertexes = new MemoryStream();
        using (var w = new BinaryWriter(vertexes, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0);   w.Write((short)0);
            w.Write((short)256); w.Write((short)0);
        }

        var sectors = new MemoryStream();
        using (var w = new BinaryWriter(sectors, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0);
            w.Write((short)128);
            w.Write(WriteFixed("FLOOR1", 8));
            w.Write(WriteFixed("CEIL1", 8));
            w.Write((short)160);
            w.Write((short)0);
            w.Write((short)7);
        }

        var sidedefs = new MemoryStream();
        using (var w = new BinaryWriter(sidedefs, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)4);
            w.Write((short)8);
            w.Write(WriteFixed("UPPER", 8));
            w.Write(WriteFixed("LOWER", 8));
            w.Write(WriteFixed("MIDDLE", 8));
            w.Write((short)0);
        }

        var linedefs = new MemoryStream();
        using (var w = new BinaryWriter(linedefs, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0);
            w.Write((short)1);
            w.Write((short)(0x0001 | 0x0020));
            w.Write((short)11);
            w.Write((short)42);
            w.Write((short)0);
            w.Write((short)-1);
        }

        var things = new MemoryStream();
        using (var w = new BinaryWriter(things, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)128);
            w.Write((short)64);
            w.Write((short)90);
            w.Write((short)3001);
            w.Write((short)(0x0001 | 0x0002 | 0x0004 | 0x0008));
        }

        return (things.ToArray(), linedefs.ToArray(), sidedefs.ToArray(), vertexes.ToArray(), sectors.ToArray());
    }

    private static byte[] WriteFixed(string s, int length)
    {
        var bytes = new byte[length];
        var src = System.Text.Encoding.ASCII.GetBytes(s);
        System.Array.Copy(src, 0, bytes, 0, System.Math.Min(src.Length, length));
        return bytes;
    }

    // Loads the synthetic map from in-memory WAD bytes, returning the parsed MapSet.
    private static MapSet LoadSynthetic()
    {
        var (things, linedefs, sidedefs, vertexes, sectors) = BuildSyntheticLumps();
        var wadBytes = new MemoryStream();
        using (var wad = new WAD(wadBytes))
        {
            wad.Insert("MAP01", 0, 0);
            InsertLump(wad, "THINGS",   things,   1);
            InsertLump(wad, "LINEDEFS", linedefs, 2);
            InsertLump(wad, "SIDEDEFS", sidedefs, 3);
            InsertLump(wad, "VERTEXES", vertexes, 4);
            InsertLump(wad, "SECTORS",  sectors,  5);
            wad.WriteHeaders();
        }
        wadBytes.Position = 0;
        using var rwad = new WAD(wadBytes, openreadonly: true);
        return DoomMapLoader.Load(rwad, "MAP01")!;
    }

    private static void InsertLump(WAD wad, string name, byte[] data, int position)
    {
        var lump = wad.Insert(name, position, data.Length)!;
        lump.Stream.Write(data, 0, data.Length);
    }

    // ============================================================
    // Byte-exact lump output
    // ============================================================

    [Fact]
    public void WriteVertexesByteExact()
    {
        var map = LoadSynthetic();
        var (_, _, _, expected, _) = BuildSyntheticLumps();
        Assert.Equal(expected, DoomMapWriter.WriteVertexes(map));
    }

    [Fact]
    public void WriteVertexesRoundsCoordinatesLikeUdb()
    {
        var map = new MapSet();
        map.Vertices.Add(new Vertex(new Vector2D(10.6, -10.6)));
        map.Vertices.Add(new Vertex(new Vector2D(10.4, -10.4)));

        var bytes = DoomMapWriter.WriteVertexes(map);

        using var reader = new BinaryReader(new MemoryStream(bytes));
        Assert.Equal(11, reader.ReadInt16());
        Assert.Equal(-11, reader.ReadInt16());
        Assert.Equal(10, reader.ReadInt16());
        Assert.Equal(-10, reader.ReadInt16());
    }

    [Fact]
    public void WriteSectorsByteExact()
    {
        var map = LoadSynthetic();
        var (_, _, _, _, expected) = BuildSyntheticLumps();
        Assert.Equal(expected, DoomMapWriter.WriteSectors(map));
    }

    [Fact]
    public void WriteSidedefsByteExact()
    {
        var map = LoadSynthetic();
        var (_, _, expected, _, _) = BuildSyntheticLumps();
        Assert.Equal(expected, DoomMapWriter.WriteSidedefs(map));
    }

    [Fact]
    public void WriteLinedefsByteExact()
    {
        var map = LoadSynthetic();
        var (_, expected, _, _, _) = BuildSyntheticLumps();
        Assert.Equal(expected, DoomMapWriter.WriteLinedefs(map));
    }

    [Fact]
    public void WriteThingsByteExact()
    {
        var map = LoadSynthetic();
        var (expected, _, _, _, _) = BuildSyntheticLumps();
        Assert.Equal(expected, DoomMapWriter.WriteThings(map));
    }

    // ============================================================
    // Full WriteMap into a WAD + re-load round trip
    // ============================================================

    [Fact]
    public void WriteMapProducesLoadableMap()
    {
        var map = LoadSynthetic();

        var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            DoomMapWriter.WriteMap(map, wad, "MAP01", 0);
        }
        ms.Position = 0;

        using var rwad = new WAD(ms, openreadonly: true);
        var reloaded = DoomMapLoader.Load(rwad, "MAP01");
        Assert.NotNull(reloaded);

        Assert.Equal(map.Vertices.Count, reloaded!.Vertices.Count);
        Assert.Equal(map.Sectors.Count,  reloaded.Sectors.Count);
        Assert.Equal(map.Sidedefs.Count, reloaded.Sidedefs.Count);
        Assert.Equal(map.Linedefs.Count, reloaded.Linedefs.Count);
        Assert.Equal(map.Things.Count,   reloaded.Things.Count);
    }

    [Fact]
    public void WriteMapEmitsDoomMapBlockOrder()
    {
        var map = LoadSynthetic();

        using var wad = new WAD(new MemoryStream());
        DoomMapWriter.WriteMap(map, wad, "MAP01", 0);

        Assert.Equal(
            new[] { "MAP01", "THINGS", "LINEDEFS", "SIDEDEFS", "VERTEXES", "SECTORS", "REJECT", "BLOCKMAP" },
            wad.Lumps.Select(l => l.Name).ToArray());
        Assert.Equal(0, wad.Lumps[6].Length);
        Assert.Equal(0, wad.Lumps[7].Length);
    }

    [Fact]
    public void RoundTripPreservesTopology()
    {
        var map = LoadSynthetic();

        var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            DoomMapWriter.WriteMap(map, wad, "MAP01", 0);
        }
        ms.Position = 0;

        using var rwad = new WAD(ms, openreadonly: true);
        var r = DoomMapLoader.Load(rwad, "MAP01")!;

        // Vertex positions
        for (int i = 0; i < map.Vertices.Count; i++)
            Assert.Equal(map.Vertices[i].Position, r.Vertices[i].Position);

        // Sector fields
        var s0o = map.Sectors[0];
        var s0r = r.Sectors[0];
        Assert.Equal(s0o.FloorHeight,  s0r.FloorHeight);
        Assert.Equal(s0o.CeilHeight,   s0r.CeilHeight);
        Assert.Equal(s0o.FloorTexture, s0r.FloorTexture);
        Assert.Equal(s0o.CeilTexture,  s0r.CeilTexture);
        Assert.Equal(s0o.Brightness,   s0r.Brightness);
        Assert.Equal(s0o.Special,      s0r.Special);
        Assert.Equal(s0o.Tag,          s0r.Tag);

        // Sidedef fields + sector reference
        var sd_o = map.Sidedefs[0];
        var sd_r = r.Sidedefs[0];
        Assert.Equal(sd_o.OffsetX,     sd_r.OffsetX);
        Assert.Equal(sd_o.OffsetY,     sd_r.OffsetY);
        Assert.Equal(sd_o.HighTexture, sd_r.HighTexture);
        Assert.Equal(sd_o.MidTexture,  sd_r.MidTexture);
        Assert.Equal(sd_o.LowTexture,  sd_r.LowTexture);
        Assert.Same(r.Sectors[0],      sd_r.Sector);

        // Linedef fields + vertex/sidedef references
        var l_o = map.Linedefs[0];
        var l_r = r.Linedefs[0];
        Assert.Equal(l_o.Flags,  l_r.Flags);
        Assert.Equal(l_o.Action, l_r.Action);
        Assert.Equal(l_o.Tag,    l_r.Tag);
        Assert.Same(r.Vertices[0], l_r.Start);
        Assert.Same(r.Vertices[1], l_r.End);
        Assert.Same(r.Sidedefs[0], l_r.Front);
        Assert.Null(l_r.Back);

        // Thing fields
        var t_o = map.Things[0];
        var t_r = r.Things[0];
        Assert.Equal(t_o.Position, t_r.Position);
        Assert.Equal(t_o.Angle,    t_r.Angle);
        Assert.Equal(t_o.Type,     t_r.Type);
        Assert.Equal(t_o.Flags,    t_r.Flags);
    }

    [Fact]
    public void TwoSidedLineRoundTripsBothSidedefs()
    {
        // Hand-built two-sector map with a shared two-sided divider.
        var map = new MapSet();
        var sA = new Sector { Index = 0, FloorHeight = 0, CeilHeight = 128, FloorTexture = "F", CeilTexture = "C", Brightness = 160 };
        var sB = new Sector { Index = 1, FloorHeight = 8, CeilHeight = 120, FloorTexture = "F", CeilTexture = "C", Brightness = 144 };
        map.Sectors.Add(sA); map.Sectors.Add(sB);

        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(100, 0));
        map.Vertices.Add(v0); map.Vertices.Add(v1);

        var l = new Linedef(v0, v1) { Flags = 0x0004 /* TwoSided */ };
        var front = new Sidedef(l, true)  { Sector = sA };
        var back  = new Sidedef(l, false) { Sector = sB };
        l.Front = front; l.Back = back;
        map.Sidedefs.Add(front); map.Sidedefs.Add(back);
        map.Linedefs.Add(l);

        var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            DoomMapWriter.WriteMap(map, wad, "MAP01", 0);
        }
        ms.Position = 0;

        using var rwad = new WAD(ms, openreadonly: true);
        var r = DoomMapLoader.Load(rwad, "MAP01")!;

        var lr = r.Linedefs[0];
        Assert.NotNull(lr.Front);
        Assert.NotNull(lr.Back);
        Assert.Same(r.Sectors[0], lr.Front!.Sector);
        Assert.Same(r.Sectors[1], lr.Back!.Sector);
    }

    [Fact]
    public void SharedVertexReferencesResolveToConsistentIndices()
    {
        // Build a small square (4 verts) where each vertex is referenced by two linedefs.
        // Verifies the writer's ReferenceEqualityComparer-backed index produces stable indices
        // when the same Vertex instance appears in multiple Linedef.Start/End slots.
        var map = new MapSet();
        var sector = new Sector { Index = 0, FloorTexture = "-", CeilTexture = "-" };
        map.Sectors.Add(sector);

        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(10, 0));
        var v2 = new Vertex(new Vector2D(10, 10));
        var v3 = new Vertex(new Vector2D(0, 10));
        map.Vertices.AddRange(new[] { v0, v1, v2, v3 });

        Linedef Make(Vertex a, Vertex b)
        {
            var l = new Linedef(a, b);
            var sd = new Sidedef(l, true) { Sector = sector };
            l.Front = sd;
            map.Sidedefs.Add(sd);
            map.Linedefs.Add(l);
            return l;
        }
        Make(v0, v1); Make(v1, v2); Make(v2, v3); Make(v3, v0);

        var lineBytes = DoomMapWriter.WriteLinedefs(map);
        Assert.Equal(4 * DoomMapWriter.LinedefRecordSize, lineBytes.Length);

        // Parse out v1/v2 fields of each linedef and verify they form the closed loop 0->1->2->3->0.
        var expected = new (short v1, short v2)[] { (0, 1), (1, 2), (2, 3), (3, 0) };
        using var br = new BinaryReader(new MemoryStream(lineBytes));
        for (int i = 0; i < 4; i++)
        {
            short rv1 = br.ReadInt16();
            short rv2 = br.ReadInt16();
            br.ReadBytes(DoomMapWriter.LinedefRecordSize - 4);
            Assert.Equal(expected[i].v1, rv1);
            Assert.Equal(expected[i].v2, rv2);
        }
    }

    [Fact]
    public void MissingSectorOnSidedefThrowsLikeUdb()
    {
        var map = new MapSet();
        map.Sectors.Add(new Sector { Index = 0, FloorTexture = "-", CeilTexture = "-" });
        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(10, 0));
        map.Vertices.Add(v0); map.Vertices.Add(v1);
        var l = new Linedef(v0, v1);
        var sd = new Sidedef(l, true) { Sector = null };
        l.Front = sd;
        map.Sidedefs.Add(sd);
        map.Linedefs.Add(l);

        Assert.Throws<InvalidDataException>(() => DoomMapWriter.WriteSidedefs(map));
    }

    [Fact]
    public void MissingLinedefVertexThrowsLikeUdb()
    {
        var map = new MapSet();
        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(10, 0));
        map.Vertices.Add(v0);
        map.Linedefs.Add(new Linedef(v0, v1));

        Assert.Throws<InvalidDataException>(() => DoomMapWriter.WriteLinedefs(map));
    }

    [Fact]
    public void UnsignedBinaryFieldsAboveSignedShortRangeRoundTrip()
    {
        const int highVertexIndex = 40000;
        var map = new MapSet();
        var sector = new Sector
        {
            Index = 0,
            FloorTexture = "-",
            CeilTexture = "-",
            Special = 50000,
            Tag = 50001,
        };
        map.Sectors.Add(sector);

        for (int i = 0; i <= highVertexIndex; i++)
            map.Vertices.Add(new Vertex(new Vector2D(i, 0)));

        var line = new Linedef(map.Vertices[0], map.Vertices[highVertexIndex])
        {
            Flags = 0x8001,
            Action = 50002,
            Tag = 50003,
        };
        var side = new Sidedef(line, true) { Sector = sector };
        line.Front = side;
        map.Sidedefs.Add(side);
        map.Linedefs.Add(line);
        map.Things.Add(new Thing { Position = new Vector2D(0, 0), Type = 1, Flags = 0x800F });

        var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            DoomMapWriter.WriteMap(map, wad, "MAP01", 0);
        }
        ms.Position = 0;

        using var rwad = new WAD(ms, openreadonly: true);
        var reloaded = DoomMapLoader.Load(rwad, "MAP01")!;

        Assert.Same(reloaded.Vertices[highVertexIndex], reloaded.Linedefs[0].End);
        Assert.Equal(0x8001, reloaded.Linedefs[0].Flags);
        Assert.Equal(50002, reloaded.Linedefs[0].Action);
        Assert.Equal(50003, reloaded.Linedefs[0].Tag);
        Assert.Equal(50000, reloaded.Sectors[0].Special);
        Assert.Equal(50001, reloaded.Sectors[0].Tag);
        Assert.Equal(0x800F, reloaded.Things[0].Flags);
    }

    [Fact]
    public void MissingBackSidedefWritesAsNegativeOne()
    {
        var map = LoadSynthetic();
        var lineBytes = DoomMapWriter.WriteLinedefs(map);
        // Last 2 bytes of the linedef record = sideLeft.
        short sideLeft = System.BitConverter.ToInt16(lineBytes, DoomMapWriter.LinedefRecordSize - 2);
        Assert.Equal(-1, sideLeft);
    }

    [Fact]
    public void FixedStringTruncatesAndUppercases()
    {
        // A sector with a long lower-case texture name - the writer must uppercase and truncate to 8 bytes.
        var map = new MapSet();
        map.Sectors.Add(new Sector
        {
            Index = 0,
            FloorTexture = "verylongtexname",
            CeilTexture = "ceil",
        });
        var bytes = DoomMapWriter.WriteSectors(map);
        // Sector layout: floor_h(2) ceil_h(2) floor_tex(8) ceil_tex(8) light(2) special(2) tag(2)
        string floorTex = System.Text.Encoding.ASCII.GetString(bytes, 4, 8);
        string ceilTex  = System.Text.Encoding.ASCII.GetString(bytes, 12, 8);
        Assert.Equal("VERYLONG", floorTex);
        Assert.Equal("CEIL\0\0\0\0", ceilTex);
    }

    [Fact]
    public void WriteMapThrowsOnReadOnlyWad()
    {
        var map = LoadSynthetic();
        var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            wad.Insert("DUMMY", 0, 0);
            wad.WriteHeaders();
        }
        ms.Position = 0;
        using var rwad = new WAD(ms, openreadonly: true);
        Assert.Throws<IOException>(() => DoomMapWriter.WriteMap(map, rwad, "MAP01", 0));
    }
}
