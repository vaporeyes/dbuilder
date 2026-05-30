// ABOUTME: Doom-format binary map loader verification tests.
// ABOUTME: Builds a synthetic Doom map (5 lumps) in memory through DBuilder.IO's WAD writer, loads it through DoomMapLoader, asserts the topology + flag decoding.

using System.IO;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class DoomMapLoaderTests
{
    // Builds a synthetic two-vertex / one-linedef Doom-format map in memory and returns the WAD bytes.
    // Verts (2), Sectors (1), Sidedefs (1), Linedefs (1), Things (1).
    private static MemoryStream BuildSyntheticDoomMap(short secondVertexX = 256, short lineV2 = 1, short sidedefSector = 0, ushort thingFlags = 0x000F)
    {
        // VERTEXES (4 bytes each * 2 = 8)
        var vertexes = new MemoryStream();
        using (var w = new BinaryWriter(vertexes, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0);   w.Write((short)0);
            w.Write(secondVertexX); w.Write((short)0);
        }

        // SECTORS (26 bytes * 1 = 26)
        var sectors = new MemoryStream();
        using (var w = new BinaryWriter(sectors, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0);     // floor height
            w.Write((short)128);   // ceil height
            w.Write(WriteFixed("FLOOR1", 8));
            w.Write(WriteFixed("CEIL1", 8));
            w.Write((short)160);   // light
            w.Write((short)0);     // special
            w.Write((short)7);     // tag
        }

        // SIDEDEFS (30 bytes * 1 = 30)
        var sidedefs = new MemoryStream();
        using (var w = new BinaryWriter(sidedefs, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)4);   // offset x
            w.Write((short)8);   // offset y
            w.Write(WriteFixed("UPPER", 8));
            w.Write(WriteFixed("LOWER", 8));
            w.Write(WriteFixed("MIDDLE", 8));
            w.Write(sidedefSector); // sector
        }

        // LINEDEFS (14 bytes * 1 = 14)
        var linedefs = new MemoryStream();
        using (var w = new BinaryWriter(linedefs, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0);              // v1
            w.Write(lineV2);                // v2
            w.Write((short)(0x0001 | 0x0020)); // flags: Blocking + Secret
            w.Write((short)11);             // special
            w.Write((short)42);             // tag
            w.Write((short)0);              // sidefront
            w.Write((short)-1);             // sideback
        }

        // THINGS (10 bytes * 1 = 10)
        var things = new MemoryStream();
        using (var w = new BinaryWriter(things, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)128);            // x
            w.Write((short)64);             // y
            w.Write((short)90);             // angle
            w.Write((short)3001);           // type (imp)
            w.Write(thingFlags); // skill1+2 + skill3 + skill4+5 + ambush by default
        }

        var wadBytes = new MemoryStream();
        using (var wad = new WAD(wadBytes))
        {
            wad.Insert("MAP01", 0, 0);
            WriteLump(wad, "THINGS",   things.ToArray(),  1);
            WriteLump(wad, "LINEDEFS", linedefs.ToArray(), 2);
            WriteLump(wad, "SIDEDEFS", sidedefs.ToArray(), 3);
            WriteLump(wad, "VERTEXES", vertexes.ToArray(), 4);
            WriteLump(wad, "SECTORS",  sectors.ToArray(),  5);
            wad.WriteHeaders();
        }

        wadBytes.Position = 0;
        return wadBytes;
    }

    private static MemoryStream BuildDoomMapWithVertexCount(int vertexCount)
    {
        var vertexes = new MemoryStream(vertexCount * 4);
        using (var w = new BinaryWriter(vertexes, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            for (int i = 0; i < vertexCount; i++)
            {
                w.Write((short)i);
                w.Write((short)0);
            }
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
            w.Write((short)0);
        }

        var wadBytes = new MemoryStream();
        using (var wad = new WAD(wadBytes))
        {
            wad.Insert("MAP01", 0, 0);
            WriteLump(wad, "VERTEXES", vertexes.ToArray(), 1);
            WriteLump(wad, "LINEDEFS", System.Array.Empty<byte>(), 2);
            WriteLump(wad, "SIDEDEFS", System.Array.Empty<byte>(), 3);
            WriteLump(wad, "SECTORS", sectors.ToArray(), 4);
            wad.WriteHeaders();
        }

        wadBytes.Position = 0;
        return wadBytes;
    }

    private static MemoryStream BuildDoomMapWithUnsignedIds()
    {
        const int highVertexIndex = 40000;
        var vertexes = new MemoryStream((highVertexIndex + 1) * 4);
        using (var w = new BinaryWriter(vertexes, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            for (int i = 0; i <= highVertexIndex; i++)
            {
                w.Write((short)i);
                w.Write((short)0);
            }
        }

        var sectors = new MemoryStream();
        using (var w = new BinaryWriter(sectors, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0);
            w.Write((short)128);
            w.Write(WriteFixed("FLOOR1", 8));
            w.Write(WriteFixed("CEIL1", 8));
            w.Write((short)160);
            w.Write((ushort)50000);
            w.Write((ushort)50001);
        }

        var sidedefs = new MemoryStream();
        using (var w = new BinaryWriter(sidedefs, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0);
            w.Write((short)0);
            w.Write(WriteFixed("UPPER", 8));
            w.Write(WriteFixed("LOWER", 8));
            w.Write(WriteFixed("MIDDLE", 8));
            w.Write((ushort)0);
        }

        var linedefs = new MemoryStream();
        using (var w = new BinaryWriter(linedefs, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((ushort)0);
            w.Write((ushort)highVertexIndex);
            w.Write((ushort)0x8001);
            w.Write((ushort)50002);
            w.Write((ushort)50003);
            w.Write((ushort)0);
            w.Write(ushort.MaxValue);
        }

        var wadBytes = new MemoryStream();
        using (var wad = new WAD(wadBytes))
        {
            wad.Insert("MAP01", 0, 0);
            WriteLump(wad, "VERTEXES", vertexes.ToArray(), 1);
            WriteLump(wad, "LINEDEFS", linedefs.ToArray(), 2);
            WriteLump(wad, "SIDEDEFS", sidedefs.ToArray(), 3);
            WriteLump(wad, "SECTORS", sectors.ToArray(), 4);
            wad.WriteHeaders();
        }

        wadBytes.Position = 0;
        return wadBytes;
    }

    private static void WriteLump(WAD wad, string name, byte[] data, int position)
    {
        var lump = wad.Insert(name, position, data.Length)!;
        lump.Stream.Write(data, 0, data.Length);
    }

    private static byte[] WriteFixed(string s, int length)
    {
        var bytes = new byte[length];
        var src = System.Text.Encoding.ASCII.GetBytes(s);
        System.Array.Copy(src, 0, bytes, 0, System.Math.Min(src.Length, length));
        return bytes;
    }

    [Fact]
    public void LoadsAllCoreLumps()
    {
        var wadBytes = BuildSyntheticDoomMap();
        using var wad = new WAD(wadBytes, openreadonly: true);

        var map = DoomMapLoader.Load(wad, "MAP01");

        Assert.NotNull(map);
        Assert.Equal(2, map!.Vertices.Count);
        Assert.Equal(1, map.Sectors.Count);
        Assert.Equal(1, map.Sidedefs.Count);
        Assert.Equal(1, map.Linedefs.Count);
        Assert.Equal(1, map.Things.Count);
    }

    [Fact]
    public void VertexPositionsArePreserved()
    {
        var wadBytes = BuildSyntheticDoomMap();
        using var wad = new WAD(wadBytes, openreadonly: true);
        var map = DoomMapLoader.Load(wad, "MAP01")!;
        Assert.Equal(new Vector2D(0, 0),   map.Vertices[0].Position);
        Assert.Equal(new Vector2D(256, 0), map.Vertices[1].Position);
    }

    [Fact]
    public void SectorFieldsRoundTrip()
    {
        var wadBytes = BuildSyntheticDoomMap();
        using var wad = new WAD(wadBytes, openreadonly: true);
        var map = DoomMapLoader.Load(wad, "MAP01")!;
        var s = map.Sectors[0];
        Assert.Equal(0, s.FloorHeight);
        Assert.Equal(128, s.CeilHeight);
        Assert.Equal("FLOOR1", s.FloorTexture);
        Assert.Equal("CEIL1",  s.CeilTexture);
        Assert.Equal(160, s.Brightness);
        Assert.Equal(7, s.Tag);
    }

    [Fact]
    public void SidedefFieldsRoundTrip()
    {
        var wadBytes = BuildSyntheticDoomMap();
        using var wad = new WAD(wadBytes, openreadonly: true);
        var map = DoomMapLoader.Load(wad, "MAP01")!;
        var sd = map.Sidedefs[0];
        Assert.Equal(4, sd.OffsetX);
        Assert.Equal(8, sd.OffsetY);
        Assert.Equal("UPPER", sd.HighTexture);
        Assert.Equal("LOWER", sd.LowTexture);
        Assert.Equal("MIDDLE", sd.MidTexture);
        Assert.Same(map.Sectors[0], sd.Sector);
    }

    [Fact]
    public void LinedefVertexAndFlagsDecode()
    {
        var wadBytes = BuildSyntheticDoomMap();
        using var wad = new WAD(wadBytes, openreadonly: true);
        var map = DoomMapLoader.Load(wad, "MAP01")!;
        var l = map.Linedefs[0];

        Assert.Same(map.Vertices[0], l.Start);
        Assert.Same(map.Vertices[1], l.End);
        Assert.Equal(0x0001 | 0x0020, l.Flags);
        Assert.Equal(11, l.Action);
        Assert.Equal(42, l.Tag);

        Assert.NotNull(l.Front);
        Assert.Null(l.Back);
        Assert.Same(map.Sidedefs[0], l.Front);
        Assert.Same(l, l.Front!.Line);
        Assert.True(l.Front.IsFront);

        // Doom blocking + secret bits decoded to canonical names
        Assert.Contains("blocking", l.UdmfFlags);
        Assert.Contains("secret",   l.UdmfFlags);
        Assert.DoesNotContain("twosided", l.UdmfFlags); // bit not set
    }

    [Fact]
    public void InvalidVertexReferencesSkipLinedef()
    {
        var wadBytes = BuildSyntheticDoomMap(lineV2: 99);
        using var wad = new WAD(wadBytes, openreadonly: true);

        var map = DoomMapLoader.Load(wad, "MAP01")!;

        Assert.Equal(2, map.Vertices.Count);
        Assert.Empty(map.Linedefs);
        Assert.Empty(map.Sidedefs);
    }

    [Fact]
    public void ZeroLengthLinedefsAreSkipped()
    {
        var wadBytes = BuildSyntheticDoomMap(secondVertexX: 0);
        using var wad = new WAD(wadBytes, openreadonly: true);

        var map = DoomMapLoader.Load(wad, "MAP01")!;

        Assert.Equal(2, map.Vertices.Count);
        Assert.Empty(map.Linedefs);
        Assert.Empty(map.Sidedefs);
    }

    [Fact]
    public void InvalidSectorReferencesSkipSidedef()
    {
        var wadBytes = BuildSyntheticDoomMap(sidedefSector: 99);
        using var wad = new WAD(wadBytes, openreadonly: true);

        var map = DoomMapLoader.Load(wad, "MAP01")!;

        Assert.Single(map.Linedefs);
        Assert.Empty(map.Sidedefs);
        Assert.Null(map.Linedefs[0].Front);
    }

    [Fact]
    public void ThingFieldsAndFlagsDecode()
    {
        var wadBytes = BuildSyntheticDoomMap();
        using var wad = new WAD(wadBytes, openreadonly: true);
        var map = DoomMapLoader.Load(wad, "MAP01")!;
        var t = map.Things[0];
        Assert.Equal(new Vector2D(128, 64), t.Position);
        Assert.Equal(90, t.Angle);
        Assert.Equal(3001, t.Type);

        // All skill bits + ambush set; MultiOnly NOT set so "single" should be true
        Assert.Contains("skill1", t.UdmfFlags);
        Assert.Contains("skill2", t.UdmfFlags);
        Assert.Contains("skill3", t.UdmfFlags);
        Assert.Contains("skill4", t.UdmfFlags);
        Assert.Contains("skill5", t.UdmfFlags);
        Assert.Contains("ambush", t.UdmfFlags);
        Assert.Contains("single", t.UdmfFlags);
        Assert.Contains("dm",     t.UdmfFlags);
        Assert.Contains("coop",   t.UdmfFlags);
    }

    [Fact]
    public void ThingFlagsAboveSignedShortRangeArePreserved()
    {
        var wadBytes = BuildSyntheticDoomMap(thingFlags: 0x800F);
        using var wad = new WAD(wadBytes, openreadonly: true);

        var map = DoomMapLoader.Load(wad, "MAP01")!;

        Assert.Equal(0x800F, map.Things[0].Flags);
        Assert.Contains("skill1", map.Things[0].UdmfFlags);
        Assert.Contains("ambush", map.Things[0].UdmfFlags);
    }

    [Fact]
    public void MissingMapReturnsNull()
    {
        var wadBytes = BuildSyntheticDoomMap();
        using var wad = new WAD(wadBytes, openreadonly: true);
        Assert.Null(DoomMapLoader.Load(wad, "MAP99"));
    }

    [Fact]
    public void VertexEntriesPastBinaryFormatLimitAreIgnored()
    {
        var wadBytes = BuildDoomMapWithVertexCount(ushort.MaxValue + 1);
        using var wad = new WAD(wadBytes, openreadonly: true);

        var map = DoomMapLoader.Load(wad, "MAP01")!;

        Assert.Equal(ushort.MaxValue, map.Vertices.Count);
    }

    [Fact]
    public void UnsignedBinaryFieldsAboveSignedShortRangeArePreserved()
    {
        var wadBytes = BuildDoomMapWithUnsignedIds();
        using var wad = new WAD(wadBytes, openreadonly: true);

        var map = DoomMapLoader.Load(wad, "MAP01")!;

        Assert.Single(map.Linedefs);
        Assert.Same(map.Vertices[40000], map.Linedefs[0].End);
        Assert.Equal(0x8001, map.Linedefs[0].Flags);
        Assert.Equal(50002, map.Linedefs[0].Action);
        Assert.Equal(50003, map.Linedefs[0].Tag);
        Assert.Equal(50000, map.Sectors[0].Special);
        Assert.Equal(50001, map.Sectors[0].Tag);
    }

    [Fact]
    public void MapMissingRequiredLumpsReturnsNull()
    {
        // Build a WAD with only the marker - no sub-lumps
        var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            wad.Insert("MAP01", 0, 0);
            wad.WriteHeaders();
        }
        ms.Position = 0;
        using var wad2 = new WAD(ms, openreadonly: true);
        Assert.Null(DoomMapLoader.Load(wad2, "MAP01"));
    }

    [Fact]
    public void TwoSidedLineThroughBinaryFormat()
    {
        // Build a 4-vert square with a middle divider that has both sidedefs.
        var vertexes = new MemoryStream();
        using (var w = new BinaryWriter(vertexes, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0);   w.Write((short)0);     // 0
            w.Write((short)100); w.Write((short)0);     // 1
            w.Write((short)100); w.Write((short)100);   // 2
            w.Write((short)0);   w.Write((short)100);   // 3
        }
        var sectors = new MemoryStream();
        using (var w = new BinaryWriter(sectors, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            // Two adjacent sectors
            for (int i = 0; i < 2; i++)
            {
                w.Write((short)0);      // floor
                w.Write((short)128);    // ceil
                w.Write(WriteFixed("F", 8));
                w.Write(WriteFixed("C", 8));
                w.Write((short)160);    // light
                w.Write((short)0);
                w.Write((short)0);
            }
        }
        var sidedefs = new MemoryStream();
        using (var w = new BinaryWriter(sidedefs, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            // Side 0 -> sector 0, Side 1 -> sector 1
            for (int i = 0; i < 2; i++)
            {
                w.Write((short)0); w.Write((short)0);
                w.Write(WriteFixed("-", 8));
                w.Write(WriteFixed("-", 8));
                w.Write(WriteFixed("-", 8));
                w.Write((short)i);
            }
        }
        var linedefs = new MemoryStream();
        using (var w = new BinaryWriter(linedefs, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0); w.Write((short)1);
            w.Write((short)0x0004); // TwoSided
            w.Write((short)0); w.Write((short)0);
            w.Write((short)0); // front
            w.Write((short)1); // back
        }

        var wadBytes = new MemoryStream();
        using (var wad = new WAD(wadBytes))
        {
            wad.Insert("MAP01", 0, 0);
            WriteLump(wad, "VERTEXES", vertexes.ToArray(), 1);
            WriteLump(wad, "SECTORS",  sectors.ToArray(),  2);
            WriteLump(wad, "SIDEDEFS", sidedefs.ToArray(), 3);
            WriteLump(wad, "LINEDEFS", linedefs.ToArray(), 4);
            wad.WriteHeaders();
        }

        wadBytes.Position = 0;
        using var wad2 = new WAD(wadBytes, openreadonly: true);
        var map = DoomMapLoader.Load(wad2, "MAP01")!;
        var l = map.Linedefs[0];
        Assert.NotNull(l.Front);
        Assert.NotNull(l.Back);
        Assert.True(l.Front!.IsFront);
        Assert.False(l.Back!.IsFront);
        Assert.Same(map.Sectors[0], l.Front.Sector);
        Assert.Same(map.Sectors[1], l.Back.Sector);
        Assert.Contains("twosided", l.UdmfFlags);
    }
}
