// ABOUTME: Round-trip tests for HexenMapWriter - byte-exact 16-byte LINEDEFS / 20-byte THINGS and load->write->load topology+args+tid+z preservation.
// ABOUTME: Mirrors HexenMapLoaderTests synthetic fixture so the writer can be validated as the literal inverse of the loader.

using System.IO;
using System.Linq;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class HexenMapWriterTests
{
    private static (byte[] things, byte[] linedefs, byte[] sidedefs, byte[] vertexes, byte[] sectors) BuildSyntheticLumps()
    {
        var vertexes = new MemoryStream();
        using (var w = new BinaryWriter(vertexes, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0);   w.Write((short)0);
            w.Write((short)100); w.Write((short)0);
        }

        var sectors = new MemoryStream();
        using (var w = new BinaryWriter(sectors, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0);
            w.Write((short)128);
            w.Write(FixedString("FLOOR", 8));
            w.Write(FixedString("CEIL", 8));
            w.Write((short)200);
            w.Write((short)0);
            w.Write((short)0);
        }

        var sidedefs = new MemoryStream();
        using (var w = new BinaryWriter(sidedefs, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0); w.Write((short)0);
            w.Write(FixedString("-", 8));
            w.Write(FixedString("-", 8));
            w.Write(FixedString("WALL", 8));
            w.Write((short)0);
        }

        var linedefs = new MemoryStream();
        using (var w = new BinaryWriter(linedefs, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)(0x0001 | 0x0200 | (1 << 10)));
            w.Write((byte)80);
            w.Write((byte)1); w.Write((byte)2); w.Write((byte)3); w.Write((byte)0); w.Write((byte)0);
            w.Write((ushort)0);
            w.Write((ushort)0xFFFF);
        }

        var things = new MemoryStream();
        using (var w = new BinaryWriter(things, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((ushort)42);
            w.Write((short)50);
            w.Write((short)25);
            w.Write((short)16);
            w.Write((short)90);
            w.Write((short)9001);
            w.Write((ushort)(0x0001 | 0x0002 | 0x0008 | 0x0020 | 0x0100 | 0x0200 | 0x0400));
            w.Write((byte)80);
            w.Write((byte)5); w.Write((byte)10); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
        }

        return (things.ToArray(), linedefs.ToArray(), sidedefs.ToArray(), vertexes.ToArray(), sectors.ToArray());
    }

    private static byte[] FixedString(string s, int length)
    {
        var bytes = new byte[length];
        var src = System.Text.Encoding.ASCII.GetBytes(s);
        System.Array.Copy(src, 0, bytes, 0, System.Math.Min(src.Length, length));
        return bytes;
    }

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
            InsertLump(wad, "BEHAVIOR", new byte[] { 0x41, 0x43, 0x53, 0x00 }, 6);
            wad.WriteHeaders();
        }
        wadBytes.Position = 0;
        using var rwad = new WAD(wadBytes, openreadonly: true);
        return HexenMapLoader.Load(rwad, "MAP01")!;
    }

    private static void InsertLump(WAD wad, string name, byte[] data, int position)
    {
        var lump = wad.Insert(name, position, data.Length)!;
        if (data.Length > 0) lump.Stream.Write(data, 0, data.Length);
    }

    // ============================================================
    // Byte-exact lump output
    // ============================================================

    [Fact]
    public void WriteLinedefsByteExact()
    {
        var map = LoadSynthetic();
        var (_, expected, _, _, _) = BuildSyntheticLumps();
        Assert.Equal(expected, HexenMapWriter.WriteLinedefs(map));
    }

    [Fact]
    public void WriteThingsByteExact()
    {
        var map = LoadSynthetic();
        var (expected, _, _, _, _) = BuildSyntheticLumps();
        Assert.Equal(expected, HexenMapWriter.WriteThings(map));
    }

    [Fact]
    public void SharedVertexWriterRoundsCoordinatesLikeUdb()
    {
        var map = new MapSet();
        map.Vertices.Add(new Vertex(new Vector2D(4.6, -4.6)));

        var bytes = DoomMapWriter.WriteVertexes(map);

        using var reader = new BinaryReader(new MemoryStream(bytes));
        Assert.Equal(5, reader.ReadInt16());
        Assert.Equal(-5, reader.ReadInt16());
    }

    // ============================================================
    // Full WriteMap into a WAD + re-load round trip
    // ============================================================

    [Fact]
    public void WriteMapMarksAsHexenAndRoundTrips()
    {
        var map = LoadSynthetic();

        var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            HexenMapWriter.WriteMap(map, wad, "MAP01", 0, behaviorBytes: new byte[] { 0x41, 0x43, 0x53, 0x00 });
        }
        ms.Position = 0;

        using var rwad = new WAD(ms, openreadonly: true);
        Assert.True(HexenMapLoader.IsHexenFormat(rwad, "MAP01"));
        var r = HexenMapLoader.Load(rwad, "MAP01")!;

        Assert.Equal(map.Vertices.Count, r.Vertices.Count);
        Assert.Equal(map.Linedefs.Count, r.Linedefs.Count);
        Assert.Equal(map.Things.Count,   r.Things.Count);

        // Linedef args round-trip
        var l_o = map.Linedefs[0];
        var l_r = r.Linedefs[0];
        Assert.Equal(l_o.Flags,  l_r.Flags);
        Assert.Equal(l_o.Action, l_r.Action);
        Assert.Equal(l_o.Args[0], l_r.Args[0]);
        Assert.Equal(l_o.Args[1], l_r.Args[1]);
        Assert.Equal(l_o.Args[2], l_r.Args[2]);
        Assert.Equal(l_o.Args[3], l_r.Args[3]);
        Assert.Equal(l_o.Args[4], l_r.Args[4]);
        Assert.Same(r.Sidedefs[0], l_r.Front);
        Assert.Null(l_r.Back);

        // Thing tid/z/args round-trip
        var t_o = map.Things[0];
        var t_r = r.Things[0];
        Assert.Equal(t_o.Position, t_r.Position);
        Assert.Equal(t_o.Height,   t_r.Height);
        Assert.Equal(t_o.Tag,      t_r.Tag);
        Assert.Equal(t_o.Type,     t_r.Type);
        Assert.Equal(t_o.Angle,    t_r.Angle);
        Assert.Equal(t_o.Action,   t_r.Action);
        Assert.Equal(t_o.Flags,    t_r.Flags);
        Assert.Equal(t_o.Args[0], t_r.Args[0]);
        Assert.Equal(t_o.Args[1], t_r.Args[1]);
    }

    [Fact]
    public void WriteMapEmitsHexenMapBlockOrder()
    {
        var map = LoadSynthetic();

        using var wad = new WAD(new MemoryStream());
        HexenMapWriter.WriteMap(map, wad, "MAP01", 0, behaviorBytes: new byte[] { 0x41, 0x43, 0x53, 0x00 });

        Assert.Equal(
            new[] { "MAP01", "THINGS", "LINEDEFS", "SIDEDEFS", "VERTEXES", "SECTORS", "REJECT", "BLOCKMAP", "BEHAVIOR" },
            wad.Lumps.Select(l => l.Name).ToArray());
        Assert.Equal(0, wad.Lumps[6].Length);
        Assert.Equal(0, wad.Lumps[7].Length);
        Assert.Equal(new byte[] { 0x41, 0x43, 0x53, 0x00 }, wad.Lumps[8].Stream.ReadAllBytes());
        Assert.True(HexenMapLoader.IsHexenFormat(wad, "MAP01"));
    }

    [Fact]
    public void MissingBackSidedefWritesAsFFFF()
    {
        var map = LoadSynthetic();
        var bytes = HexenMapWriter.WriteLinedefs(map);
        // Hexen linedef: last 2 bytes = s2 (back).
        ushort sideBack = System.BitConverter.ToUInt16(bytes, HexenMapWriter.LinedefRecordSize - 2);
        Assert.Equal((ushort)0xFFFF, sideBack);
    }

    [Fact]
    public void MissingLinedefVertexThrowsLikeUdb()
    {
        var map = new MapSet();
        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(10, 0));
        map.Vertices.Add(v0);
        map.Linedefs.Add(new Linedef(v0, v1));

        Assert.Throws<InvalidDataException>(() => HexenMapWriter.WriteLinedefs(map));
    }

    [Fact]
    public void MissingSidedefSectorThrowsLikeUdb()
    {
        var map = new MapSet();
        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(10, 0));
        map.Vertices.Add(v0);
        map.Vertices.Add(v1);
        var line = new Linedef(v0, v1);
        var side = new Sidedef(line, true);
        line.Front = side;
        map.Linedefs.Add(line);
        map.Sidedefs.Add(side);

        Assert.Throws<InvalidDataException>(() => HexenMapWriter.WriteMap(map, new WAD(new MemoryStream()), "MAP01", 0));
    }

    [Fact]
    public void ArgsZeroByDefaultWriteAsZero()
    {
        // Build a map without going through the loader so arg bytes are at their default zero.
        var map = new MapSet();
        map.Sectors.Add(new Sector { Index = 0, FloorTexture = "-", CeilTexture = "-" });
        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(10, 0));
        map.Vertices.Add(v0); map.Vertices.Add(v1);
        var l = new Linedef(v0, v1) { Action = 12 };
        var sd = new Sidedef(l, true) { Sector = map.Sectors[0] };
        l.Front = sd;
        map.Sidedefs.Add(sd); map.Linedefs.Add(l);

        var bytes = HexenMapWriter.WriteLinedefs(map);
        Assert.Equal(HexenMapWriter.LinedefRecordSize, bytes.Length);
        // args[5] sits at bytes 7..11
        for (int i = 7; i < 12; i++) Assert.Equal(0, bytes[i]);
        // action at byte 6
        Assert.Equal(12, bytes[6]);
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
        Assert.Throws<IOException>(() => HexenMapWriter.WriteMap(map, rwad, "MAP01", 0));
    }

    [Fact]
    public void ThingHighTidPreservedAsUnsigned()
    {
        // tid is a uint16 field - values above 32767 must round-trip without sign-extension corruption.
        var map = new MapSet();
        map.Things.Add(new Thing { Tag = 50000, Type = 1, Position = new Vector2D(0, 0) });

        var bytes = HexenMapWriter.WriteThings(map);
        ushort tid = System.BitConverter.ToUInt16(bytes, 0);
        Assert.Equal((ushort)50000, tid);
    }
}
