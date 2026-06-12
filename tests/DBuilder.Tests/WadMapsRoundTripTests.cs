// ABOUTME: Public WadMaps save/load round-trip coverage for every supported map format.
// ABOUTME: Uses synthetic in-memory maps so format coverage does not depend on external IWAD fixtures.

using System.IO;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class WadMapsRoundTripTests
{
    [Theory]
    [InlineData("Doom", "Doom_DoomDoom.cfg")]
    [InlineData("Doom II", "Doom_Doom2Doom.cfg")]
    [InlineData("Heretic", "Heretic_HereticDoom.cfg")]
    [InlineData("Hexen", "Hexen_HexenHexen.cfg")]
    [InlineData("Boom", "Boom_Doom2Doom.cfg")]
    [InlineData("MBF", "MBF21_Doom2Doom.cfg")]
    [InlineData("ZDoom", "ZDoom_DoomHexen.cfg")]
    [InlineData("GZDoom", "GZDoom_DoomUDMF.cfg")]
    public void SaveMapThenLoadRoundTripsRepresentativeUdbGameConfigurations(string family, string configFile)
    {
        string? udbRoot = FindUdbRoot();
        if (udbRoot == null) return;

        string path = Path.Combine(udbRoot, "Assets", "Common", "Configurations", configFile);
        Assert.True(File.Exists(path), $"Expected UDB configuration for {family} at {path}.");

        GameConfiguration config = GameConfiguration.FromFile(path);
        MapSet map = BuildMap(config.MapFormat);
        byte[] bytes;

        using (var stream = new MemoryStream())
        using (var wad = new WAD(stream))
        {
            WadMaps.SaveMap(wad, "MAP01", map, config.MapFormat);
            bytes = stream.ToArray();
        }

        using var reopened = new WAD(new MemoryStream(bytes), openreadonly: true);
        MapEntry entry = Assert.Single(WadMaps.Find(reopened));
        Assert.Equal("MAP01", entry.Name);
        Assert.Equal(config.MapFormat, entry.Format);

        MapSet loaded = WadMaps.Load(reopened, entry)!;
        AssertMapRoundTrip(map, loaded, config.MapFormat);
    }

    [Theory]
    [InlineData(MapFormat.Doom)]
    [InlineData(MapFormat.Hexen)]
    [InlineData(MapFormat.Udmf)]
    public void SaveMapThenLoadRoundTripsSupportedFormats(MapFormat format)
    {
        MapSet map = BuildMap(format);
        byte[] bytes;

        using (var stream = new MemoryStream())
        using (var wad = new WAD(stream))
        {
            WadMaps.SaveMap(wad, "MAP01", map, format);
            bytes = stream.ToArray();
        }

        using var reopened = new WAD(new MemoryStream(bytes), openreadonly: true);
        MapEntry entry = Assert.Single(WadMaps.Find(reopened));
        Assert.Equal("MAP01", entry.Name);
        Assert.Equal(format, entry.Format);

        MapSet loaded = WadMaps.Load(reopened, entry)!;
        AssertMapRoundTrip(map, loaded, format);
    }

    private static MapSet BuildMap(MapFormat format)
    {
        var map = new MapSet { Namespace = "Doom" };
        var sector = map.AddSector();
        sector.FloorHeight = -16;
        sector.CeilHeight = 128;
        sector.FloorTexture = "FLOOR1";
        sector.CeilTexture = "CEIL1";
        sector.Brightness = 192;
        sector.Special = 7;
        sector.Tag = 3;

        var vertices = new[]
        {
            map.AddVertex(new Vector2D(0, 0)),
            map.AddVertex(new Vector2D(128, 0)),
            map.AddVertex(new Vector2D(128, 128)),
            map.AddVertex(new Vector2D(0, 128)),
        };

        for (int i = 0; i < vertices.Length; i++)
        {
            Linedef line = map.AddLinedef(vertices[i], vertices[(i + 1) % vertices.Length]);
            line.Flags = format == MapFormat.Udmf ? 0 : 1;
            line.Action = format == MapFormat.Doom ? 11 : 80;
            line.Tag = 5;
            line.Args[0] = format == MapFormat.Doom ? 0 : 1;
            line.Args[1] = format == MapFormat.Doom ? 0 : 2;

            Sidedef side = map.AddSidedef(line, true, sector);
            side.OffsetX = 4;
            side.OffsetY = 8;
            side.HighTexture = "UPPER";
            side.MidTexture = "MIDDLE";
            side.LowTexture = "LOWER";
        }

        Thing thing = map.AddThing(new Vector2D(64, 64), 3001);
        thing.Angle = 90;
        thing.Flags = format == MapFormat.Udmf ? 0 : 7;
        thing.Tag = format == MapFormat.Doom ? 0 : 9;
        thing.Action = format == MapFormat.Doom ? 0 : 80;
        thing.Height = format == MapFormat.Doom ? 0 : 16;
        thing.Args[0] = format == MapFormat.Doom ? 0 : 3;
        thing.Args[1] = format == MapFormat.Doom ? 0 : 4;

        map.BuildIndexes();
        return map;
    }

    private static string? FindUdbRoot()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string sibling = Path.GetFullPath(Path.Combine(repositoryRoot, "..", "UltimateDoomBuilder"));
        if (Directory.Exists(Path.Combine(sibling, "Assets", "Common", "Configurations"))) return sibling;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string root = Path.Combine(home, "dev", "repos", "UltimateDoomBuilder");
        return Directory.Exists(Path.Combine(root, "Assets", "Common", "Configurations")) ? root : null;
    }

    private static void AssertMapRoundTrip(MapSet expected, MapSet actual, MapFormat format)
    {
        Assert.Equal(expected.Vertices.Count, actual.Vertices.Count);
        Assert.Equal(expected.Linedefs.Count, actual.Linedefs.Count);
        Assert.Equal(expected.Sidedefs.Count, actual.Sidedefs.Count);
        Assert.Equal(expected.Sectors.Count, actual.Sectors.Count);
        Assert.Equal(expected.Things.Count, actual.Things.Count);

        for (int i = 0; i < expected.Vertices.Count; i++)
            Assert.Equal(expected.Vertices[i].Position, actual.Vertices[i].Position);

        for (int i = 0; i < expected.Linedefs.Count; i++)
        {
            Linedef expectedLine = expected.Linedefs[i];
            Linedef actualLine = actual.Linedefs[i];

            Assert.Equal(expected.Vertices.IndexOf(expectedLine.Start), actual.Vertices.IndexOf(actualLine.Start));
            Assert.Equal(expected.Vertices.IndexOf(expectedLine.End), actual.Vertices.IndexOf(actualLine.End));
            Assert.Equal(expectedLine.Flags, actualLine.Flags);
            Assert.Equal(expectedLine.Action, actualLine.Action);
            Assert.Same(actual.Sidedefs[i], actualLine.Front);
            Assert.Null(actualLine.Back);

            if (format == MapFormat.Doom)
            {
                Assert.Equal(expectedLine.Tag, actualLine.Tag);
            }
            else
            {
                Assert.Equal(expectedLine.Args[0], actualLine.Args[0]);
                Assert.Equal(expectedLine.Args[1], actualLine.Args[1]);
            }
        }

        Sector expectedSector = expected.Sectors[0];
        Sector actualSector = actual.Sectors[0];
        Assert.Equal(expectedSector.FloorHeight, actualSector.FloorHeight);
        Assert.Equal(expectedSector.CeilHeight, actualSector.CeilHeight);
        Assert.Equal(expectedSector.FloorTexture, actualSector.FloorTexture);
        Assert.Equal(expectedSector.CeilTexture, actualSector.CeilTexture);
        Assert.Equal(expectedSector.Brightness, actualSector.Brightness);
        Assert.Equal(expectedSector.Special, actualSector.Special);
        Assert.Equal(expectedSector.Tag, actualSector.Tag);

        Thing expectedThing = expected.Things[0];
        Thing actualThing = actual.Things[0];
        Assert.Equal(expectedThing.Position, actualThing.Position);
        Assert.Equal(expectedThing.Angle, actualThing.Angle);
        Assert.Equal(expectedThing.Type, actualThing.Type);
        Assert.Equal(expectedThing.Flags, actualThing.Flags);

        if (format != MapFormat.Doom)
        {
            Assert.Equal(expectedThing.Height, actualThing.Height);
            Assert.Equal(expectedThing.Tag, actualThing.Tag);
            Assert.Equal(expectedThing.Action, actualThing.Action);
            Assert.Equal(expectedThing.Args[0], actualThing.Args[0]);
            Assert.Equal(expectedThing.Args[1], actualThing.Args[1]);
        }
    }
}
