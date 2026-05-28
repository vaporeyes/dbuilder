// ABOUTME: Tests for WadMaps - map-marker discovery and format detection in a WAD directory.
// ABOUTME: Regression guard: a graphic lump near a map block must not be mistaken for a marker, and a
// ABOUTME: non-zero-length Hexen marker must still be found and loaded with the Hexen (not Doom) loader.

using System.IO;
using System.Linq;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class WadMapsTests
{
    private const string MapLumpConfig = @"
maplumpnames
{
    ~MAP { required = true; blindcopy = true; }
    THINGS { required = true; }
    LINEDEFS { required = true; }
}";

    private const string DoomMapLumpConfig = @"
maplumpnames
{
    ~MAP { required = true; blindcopy = true; }
    THINGS { required = true; }
    LINEDEFS { required = true; }
    BEHAVIOR { forbidden = true; }
}";

    private const string NodeBuildConfig = @"
maplumpnames
{
    ~MAP { required = true; blindcopy = true; }
    THINGS { required = true; nodebuild = true; allowempty = true; }
    LINEDEFS { required = true; nodebuild = true; allowempty = false; }
    REJECT { required = false; nodebuild = true; allowempty = false; }
}";

    private static void WriteLump(WAD wad, string name, byte[] data, int position)
    {
        var lump = wad.Insert(name, position, data.Length)!;
        if (data.Length > 0) lump.Stream.Write(data, 0, data.Length);
    }

    // A single Hexen linedef (16 bytes) from vertex 0 to vertex 1 with a front sidedef.
    private static byte[] HexenLinedef()
    {
        var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((ushort)0); w.Write((ushort)1); // v1, v2
        w.Write((ushort)0);                      // flags
        w.Write((byte)0);                        // action
        w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); // args
        w.Write((ushort)0);                      // front sidedef
        w.Write((ushort)0xFFFF);                 // back sidedef (none)
        return ms.ToArray();
    }

    private static byte[] TwoVertexes()
    {
        var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((short)0); w.Write((short)0);
        w.Write((short)64); w.Write((short)0);
        return ms.ToArray();
    }

    private static byte[] OneSidedef()
    {
        var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((short)0); w.Write((short)0);        // x/y offset
        ms.Write(Encoding.ASCII.GetBytes("-\0\0\0\0\0\0\0"), 0, 8); // upper
        ms.Write(Encoding.ASCII.GetBytes("-\0\0\0\0\0\0\0"), 0, 8); // lower
        ms.Write(Encoding.ASCII.GetBytes("-\0\0\0\0\0\0\0"), 0, 8); // mid
        w.Write((short)0);                            // sector
        return ms.ToArray();
    }

    private static byte[] OneSector()
    {
        var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((short)0); w.Write((short)128);       // floor/ceil height
        ms.Write(Encoding.ASCII.GetBytes("FLOOR0_1"), 0, 8);
        ms.Write(Encoding.ASCII.GetBytes("CEIL1_1\0"), 0, 8);
        w.Write((short)160); w.Write((short)0); w.Write((short)0); // light, special, tag
        return ms.ToArray();
    }

    // Builds a WAD with a graphic lump 11 lumps before a Hexen map whose marker carries 12 bytes.
    private static WAD BuildHexenWadWithGraphic()
    {
        var ms = new MemoryStream();
        var wad = new WAD(ms);
        // A non-map graphic lump (the kind that used to be mis-detected as a marker).
        WriteLump(wad, "CREDIT", new byte[64000], 0);
        // A Hexen map. The marker is deliberately NON-zero-length (12 bytes), like the Hexen IWAD.
        WriteLump(wad, "MAP01", new byte[12], 1);
        WriteLump(wad, "THINGS", new byte[0], 2);
        WriteLump(wad, "LINEDEFS", HexenLinedef(), 3);
        WriteLump(wad, "SIDEDEFS", OneSidedef(), 4);
        WriteLump(wad, "VERTEXES", TwoVertexes(), 5);
        WriteLump(wad, "SECTORS", OneSector(), 6);
        WriteLump(wad, "BEHAVIOR", new byte[] { 0x41, 0x43, 0x53, 0 }, 7);
        wad.WriteHeaders();
        ms.Position = 0;
        return new WAD(ms, openreadonly: true);
    }

    [Fact]
    public void FindsHexenMapAndIgnoresGraphic()
    {
        using var wad = BuildHexenWadWithGraphic();
        var maps = WadMaps.Find(wad);
        Assert.Single(maps);
        Assert.Equal("MAP01", maps[0].Name);
        Assert.Equal(MapFormat.Hexen, maps[0].Format);
    }

    [Fact]
    public void ConfiguredFindRejectsForbiddenMapLumps()
    {
        using var wad = new WAD(new MemoryStream());
        WriteLump(wad, "MAP01", new byte[0], 0);
        WriteLump(wad, "THINGS", new byte[0], 1);
        WriteLump(wad, "LINEDEFS", new byte[0], 2);
        WriteLump(wad, "BEHAVIOR", new byte[0], 3);
        wad.WriteHeaders();

        var config = GameConfiguration.FromText(DoomMapLumpConfig);
        Assert.Empty(WadMaps.Find(wad, config));
    }

    [Fact]
    public void ConfiguredFindRequiresConfiguredLumps()
    {
        using var wad = new WAD(new MemoryStream());
        WriteLump(wad, "MAP01", new byte[0], 0);
        WriteLump(wad, "THINGS", new byte[0], 1);
        wad.WriteHeaders();

        var config = GameConfiguration.FromText(DoomMapLumpConfig);
        Assert.Empty(WadMaps.Find(wad, config));

        WriteLump(wad, "LINEDEFS", new byte[0], 2);
        wad.WriteHeaders();

        var maps = WadMaps.Find(wad, config);
        var entry = Assert.Single(maps);
        Assert.Equal("MAP01", entry.Name);
        Assert.Equal(MapFormat.Doom, entry.Format);
    }

    [Fact]
    public void LoadsHexenMapWithCorrectVertexReferences()
    {
        using var wad = BuildHexenWadWithGraphic();
        var entry = WadMaps.Find(wad)[0];
        var map = WadMaps.Load(wad, entry);

        Assert.NotNull(map);
        Assert.Equal(2, map!.Vertices.Count);
        Assert.Single(map.Linedefs);
        // The 16-byte Hexen stride must resolve v1=0, v2=1 (a 14-byte Doom read would corrupt this).
        Assert.Same(map.Vertices[0], map.Linedefs[0].Start);
        Assert.Same(map.Vertices[1], map.Linedefs[0].End);
    }

    [Fact]
    public void FindSpecificMapLumpStaysInsideConfiguredMapBlock()
    {
        using var wad = new WAD(new MemoryStream());
        WriteLump(wad, "MAP01", new byte[0], 0);
        WriteLump(wad, "THINGS", new byte[0], 1);
        WriteLump(wad, "DECORATE", new byte[0], 2);
        WriteLump(wad, "LINEDEFS", new byte[0], 3);
        wad.WriteHeaders();

        var config = GameConfiguration.FromText(MapLumpConfig);
        int header = wad.FindLumpIndex("MAP01");

        Assert.Equal(header, WadMaps.FindSpecificMapLump(wad, "MAP01", header, "MAP01", config.MapLumpNames));
        Assert.Equal(1, WadMaps.FindSpecificMapLump(wad, "THINGS", header, "MAP01", config.MapLumpNames));
        Assert.Equal(-1, WadMaps.FindSpecificMapLump(wad, "LINEDEFS", header, "MAP01", config.MapLumpNames));
    }

    [Fact]
    public void RemoveSpecificMapLumpRemovesConfiguredLumpOnly()
    {
        using var wad = new WAD(new MemoryStream());
        WriteLump(wad, "MAP01", new byte[0], 0);
        WriteLump(wad, "THINGS", new byte[] { 1 }, 1);
        WriteLump(wad, "LINEDEFS", new byte[] { 2 }, 2);
        WriteLump(wad, "END", new byte[0], 3);
        wad.WriteHeaders();

        var config = GameConfiguration.FromText(MapLumpConfig);
        int header = wad.FindLumpIndex("MAP01");

        Assert.Equal(1, WadMaps.RemoveSpecificMapLump(wad, "THINGS", header, "MAP01", config.MapLumpNames));
        Assert.Equal(new[] { "MAP01", "LINEDEFS", "END" }, wad.Lumps.Select(l => l.Name).ToArray());
        Assert.Equal(-1, WadMaps.RemoveSpecificMapLump(wad, "END", header, "MAP01", config.MapLumpNames));
    }

    [Fact]
    public void ReadMapLumpSkipsNonMapLumpWithSameName()
    {
        using var wad = new WAD(new MemoryStream());
        WriteLump(wad, "MAP01", new byte[] { 9 }, 0);
        WriteLump(wad, "TITLEPIC", new byte[] { 8 }, 1);
        WriteLump(wad, "MAP01", new byte[0], 2);
        WriteLump(wad, "THINGS", new byte[] { 1 }, 3);
        WriteLump(wad, "LINEDEFS", new byte[] { 2 }, 4);
        wad.WriteHeaders();

        Assert.Equal(new byte[] { 1 }, WadMaps.ReadMapLump(wad, "MAP01", "THINGS"));
        Assert.Null(WadMaps.ReadMapLump(wad, "MAP01", "TITLEPIC"));
    }

    [Fact]
    public void LoadsUdmfTextmapAfterValidatedMarker()
    {
        using var wad = new WAD(new MemoryStream());
        WriteLump(wad, "MAP01", new byte[] { 9 }, 0);
        WriteLump(wad, "TITLEPIC", new byte[] { 8 }, 1);
        WriteLump(wad, "TEXTMAP", Encoding.ASCII.GetBytes("namespace = \"Bad\";"), 2);
        WriteLump(wad, "MAP01", new byte[0], 3);
        WriteLump(wad, "TEXTMAP", Encoding.ASCII.GetBytes("namespace = \"Doom\"; vertex { x = 1; y = 2; }"), 4);
        WriteLump(wad, "ENDMAP", new byte[0], 5);
        wad.WriteHeaders();

        var map = WadMaps.Load(wad, new MapEntry("MAP01", MapFormat.Udmf));

        Assert.NotNull(map);
        Assert.Equal("Doom", map!.Namespace);
        Assert.Single(map.Vertices);
    }

    [Fact]
    public void RequiredNodeBuildLumpsPresentIgnoresAllowEmptyAndOptional()
    {
        using var wad = new WAD(new MemoryStream());
        WriteLump(wad, "MAP01", new byte[0], 0);
        WriteLump(wad, "THINGS", new byte[0], 1);
        wad.WriteHeaders();

        var config = GameConfiguration.FromText(NodeBuildConfig);
        Assert.False(WadMaps.RequiredNodeBuildLumpsPresent(wad, "MAP01", config));

        WriteLump(wad, "LINEDEFS", new byte[0], 2);
        wad.WriteHeaders();

        Assert.True(WadMaps.RequiredNodeBuildLumpsPresent(wad, "MAP01", config));
    }

    [Fact]
    public void RequiredNodeBuildLumpsPresentUsesUdbBoundedLookup()
    {
        using var wad = new WAD(new MemoryStream());
        WriteLump(wad, "MAP01", new byte[0], 0);
        WriteLump(wad, "THINGS", new byte[0], 1);
        WriteLump(wad, "DECORATE", new byte[0], 2);
        WriteLump(wad, "LINEDEFS", new byte[0], 3);
        wad.WriteHeaders();

        var config = GameConfiguration.FromText(NodeBuildConfig);

        Assert.True(WadMaps.RequiredNodeBuildLumpsPresent(wad, "MAP01", config));
    }
}
