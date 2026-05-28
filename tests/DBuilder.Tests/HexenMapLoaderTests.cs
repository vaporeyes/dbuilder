// ABOUTME: Hexen binary map loader verification tests.
// ABOUTME: Builds a synthetic Hexen-format map (16-byte linedefs, 20-byte things) in memory and round-trips through HexenMapLoader.

using System.IO;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class HexenMapLoaderTests
{
    private static MemoryStream BuildSyntheticHexenMap(short secondVertexX = 100, ushort lineV2 = 1)
    {
        // 2 verts, 1 sector, 1 sidedef, 1 linedef, 1 thing.
        var vertexes = new MemoryStream();
        using (var w = new BinaryWriter(vertexes, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0);   w.Write((short)0);
            w.Write(secondVertexX); w.Write((short)0);
        }

        var sectors = new MemoryStream();
        using (var w = new BinaryWriter(sectors, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0);     // floor
            w.Write((short)128);   // ceil
            w.Write(FixedString("FLOOR", 8));
            w.Write(FixedString("CEIL", 8));
            w.Write((short)200);   // light
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
            w.Write((short)0); // sector
        }

        // Hexen linedef: 16 bytes (v1, v2, flags, action byte, args[5], s1, s2)
        var linedefs = new MemoryStream();
        using (var w = new BinaryWriter(linedefs, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((ushort)0);             // v1
            w.Write(lineV2);                // v2
            // Blocking + Repeats + SPAC=1 (useract)  =>  0x0001 | 0x0200 | (1<<10) = 0x0001 | 0x0200 | 0x0400 = 0x0601
            w.Write((ushort)(0x0001 | 0x0200 | (1 << 10)));
            w.Write((byte)80);              // action
            w.Write((byte)1); w.Write((byte)2); w.Write((byte)3); w.Write((byte)0); w.Write((byte)0); // args
            w.Write((ushort)0);             // sidefront
            w.Write((ushort)0xFFFF);        // sideback (-1)
        }

        // Hexen thing: 20 bytes (tid, x, y, z, angle, type, flags, action byte, args[5])
        var things = new MemoryStream();
        using (var w = new BinaryWriter(things, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((ushort)42);            // tid
            w.Write((short)50);             // x
            w.Write((short)25);             // y
            w.Write((short)16);             // z
            w.Write((short)90);             // angle
            w.Write((short)9001);           // type
            // Skill1+2 + skill3 + ambush + class1 + single + coop + dm = 0x0001|0x0002|0x0008|0x0020|0x0100|0x0200|0x0400 = 0x072B
            w.Write((ushort)(0x0001 | 0x0002 | 0x0008 | 0x0020 | 0x0100 | 0x0200 | 0x0400));
            w.Write((byte)80);              // action
            w.Write((byte)5); w.Write((byte)10); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); // args
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
            // BEHAVIOR lump marks this as Hexen-format
            WriteLump(wad, "BEHAVIOR", new byte[] { 0x41, 0x43, 0x53, 0x00 }, 6); // "ACS\0"
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

    private static byte[] FixedString(string s, int length)
    {
        var bytes = new byte[length];
        var src = System.Text.Encoding.ASCII.GetBytes(s);
        System.Array.Copy(src, 0, bytes, 0, System.Math.Min(src.Length, length));
        return bytes;
    }

    [Fact]
    public void IsHexenFormatDetectsBehavior()
    {
        var wadBytes = BuildSyntheticHexenMap();
        using var wad = new WAD(wadBytes, openreadonly: true);
        Assert.True(HexenMapLoader.IsHexenFormat(wad, "MAP01"));
    }

    [Fact]
    public void LoadsTopology()
    {
        var wadBytes = BuildSyntheticHexenMap();
        using var wad = new WAD(wadBytes, openreadonly: true);
        var map = HexenMapLoader.Load(wad, "MAP01");
        Assert.NotNull(map);
        Assert.Equal(2, map!.Vertices.Count);
        Assert.Equal(1, map.Sectors.Count);
        Assert.Equal(1, map.Sidedefs.Count);
        Assert.Equal(1, map.Linedefs.Count);
        Assert.Equal(1, map.Things.Count);
    }

    [Fact]
    public void LinedefFlagsAndActionDecode()
    {
        var wadBytes = BuildSyntheticHexenMap();
        using var wad = new WAD(wadBytes, openreadonly: true);
        var map = HexenMapLoader.Load(wad, "MAP01")!;
        var l = map.Linedefs[0];

        Assert.Equal(80, l.Action);
        Assert.Contains("blocking",      l.UdmfFlags);
        Assert.Contains("repeatspecial", l.UdmfFlags);
        Assert.Contains("playeruse",     l.UdmfFlags); // SPAC=1
        // Args 1, 2, 3 were nonzero; stored as named UdmfFlags
        Assert.Contains("arg0=1", l.UdmfFlags);
        Assert.Contains("arg1=2", l.UdmfFlags);
        Assert.Contains("arg2=3", l.UdmfFlags);
        Assert.DoesNotContain("arg3=0", l.UdmfFlags);
        Assert.DoesNotContain("arg4=0", l.UdmfFlags);
    }

    [Fact]
    public void InvalidVertexReferencesSkipLinedef()
    {
        var wadBytes = BuildSyntheticHexenMap(lineV2: 99);
        using var wad = new WAD(wadBytes, openreadonly: true);

        var map = HexenMapLoader.Load(wad, "MAP01")!;

        Assert.Equal(2, map.Vertices.Count);
        Assert.Empty(map.Linedefs);
    }

    [Fact]
    public void ZeroLengthLinedefsAreSkipped()
    {
        var wadBytes = BuildSyntheticHexenMap(secondVertexX: 0);
        using var wad = new WAD(wadBytes, openreadonly: true);

        var map = HexenMapLoader.Load(wad, "MAP01")!;

        Assert.Equal(2, map.Vertices.Count);
        Assert.Empty(map.Linedefs);
    }

    [Fact]
    public void ThingTidHeightActionDecode()
    {
        var wadBytes = BuildSyntheticHexenMap();
        using var wad = new WAD(wadBytes, openreadonly: true);
        var map = HexenMapLoader.Load(wad, "MAP01")!;
        var t = map.Things[0];

        Assert.Equal(new Vector2D(50, 25), t.Position);
        Assert.Equal(16, t.Height);
        Assert.Equal(42, t.Tag);
        Assert.Equal(9001, t.Type);
        Assert.Equal(90, t.Angle);
        Assert.Equal(80, t.Action);

        // Hexen has explicit single/dm/coop bits (no inversion like Doom).
        Assert.Contains("skill1", t.UdmfFlags);
        Assert.Contains("skill2", t.UdmfFlags);
        Assert.Contains("skill3", t.UdmfFlags);
        Assert.Contains("ambush", t.UdmfFlags);
        Assert.Contains("class1", t.UdmfFlags);
        Assert.Contains("single", t.UdmfFlags);
        Assert.Contains("coop",   t.UdmfFlags);
        Assert.Contains("dm",     t.UdmfFlags);
    }

    [Fact]
    public void DoomFormatMapIsNotHexen()
    {
        // Build a WAD with a MAP01 marker but no BEHAVIOR lump
        var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            wad.Insert("MAP01", 0, 0);
            wad.Insert("VERTEXES", 1, 0);
            wad.WriteHeaders();
        }
        ms.Position = 0;
        using var wad2 = new WAD(ms, openreadonly: true);
        Assert.False(HexenMapLoader.IsHexenFormat(wad2, "MAP01"));
    }
}
