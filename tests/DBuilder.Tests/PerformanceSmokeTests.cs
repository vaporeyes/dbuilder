// ABOUTME: Deterministic scale smoke tests for large map and resource-stack workflows.
// ABOUTME: Exercises high-count fixtures without timing assertions that vary by machine.

using System.IO;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class PerformanceSmokeTests
{
    [Fact]
    public void LargeUdmfMapSavesFindsAndReloads()
    {
        MapSet map = BuildGridMap(width: 32, height: 32, cellSize: 64);
        byte[] bytes;

        using (var stream = new MemoryStream())
        using (var wad = new WAD(stream))
        {
            WadMaps.SaveMap(wad, "MAP01", map, MapFormat.Udmf);
            bytes = stream.ToArray();
        }

        using var reopened = new WAD(new MemoryStream(bytes), openreadonly: true);
        MapEntry entry = Assert.Single(WadMaps.Find(reopened));
        MapSet loaded = WadMaps.Load(reopened, entry)!;

        Assert.Equal(MapFormat.Udmf, entry.Format);
        Assert.Equal(map.Sectors.Count, loaded.Sectors.Count);
        Assert.Equal(map.Linedefs.Count, loaded.Linedefs.Count);
        Assert.Equal(map.Sidedefs.Count, loaded.Sidedefs.Count);
        Assert.Equal(map.Vertices.Count, loaded.Vertices.Count);
        Assert.Equal(map.Things.Count, loaded.Things.Count);
    }

    [Fact]
    public void LargeMixedResourceStackResolvesNamesFromEverySource()
    {
        string iwad = BuildWadFile(
            isIwad: true,
            FlatLumps("BASE", 80, 10)
                .Prepend(("PLAYPAL", TestArtifacts.GrayscalePlaypal()))
                .ToArray());
        string pwad = BuildWadFile(isIwad: false, FlatLumps("PWAD", 80, 30).ToArray());
        string pk3 = TestArtifacts.BuildPk3(PngEntries("PK3F", 80, 60).ToArray());
        string directory = BuildResourceDirectory("DIRF", 80, 90);

        try
        {
            using var resources = new ResourceManager();
            resources.AddBaseResource(iwad);
            resources.AddResource(pwad);
            resources.AddResource(pk3);
            resources.AddResource(directory);

            Assert.Equal(new byte[] { 10, 10, 10, 255 }, resources.GetFlat("BASE0000")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 45, 45, 45, 255 }, resources.GetFlat("PWAD0079")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 60, 61, 62, 255 }, resources.GetFlat("PK3F0000")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 90, 106, 107, 255 }, resources.GetFlat("DIRF0079")!.Rgba[0..4]);

            string[] flatNames = resources.GetFlatNames().ToArray();
            Assert.Contains("BASE0000", flatNames);
            Assert.Contains("PWAD0079", flatNames);
            Assert.Contains("PK3F0000", flatNames);
            Assert.Contains("DIRF0079", flatNames);
        }
        finally
        {
            File.Delete(iwad);
            File.Delete(pwad);
            File.Delete(pk3);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static MapSet BuildGridMap(int width, int height, int cellSize)
    {
        var map = new MapSet { Namespace = "Doom" };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Sector sector = map.AddSector();
                sector.FloorTexture = "FLOOR1";
                sector.CeilTexture = "CEIL1";
                sector.CeilHeight = 128;
                sector.Brightness = 160;
                sector.Tag = y * width + x;

                int left = x * cellSize;
                int top = y * cellSize;
                Vertex v0 = map.AddVertex(new Vector2D(left, top));
                Vertex v1 = map.AddVertex(new Vector2D(left + cellSize, top));
                Vertex v2 = map.AddVertex(new Vector2D(left + cellSize, top + cellSize));
                Vertex v3 = map.AddVertex(new Vector2D(left, top + cellSize));

                AddLine(map, sector, v0, v1);
                AddLine(map, sector, v1, v2);
                AddLine(map, sector, v2, v3);
                AddLine(map, sector, v3, v0);
            }
        }

        map.AddThing(new Vector2D(cellSize / 2.0, cellSize / 2.0), 3001);
        map.BuildIndexes();
        return map;
    }

    private static void AddLine(MapSet map, Sector sector, Vertex start, Vertex end)
    {
        Linedef line = map.AddLinedef(start, end);
        line.Flags = 1;
        Sidedef side = map.AddSidedef(line, true, sector);
        side.MidTexture = "MIDDLE";
    }

    private static IEnumerable<(string name, byte[] bytes)> FlatLumps(string prefix, int count, int startIndex)
    {
        yield return ("F_START", Array.Empty<byte>());
        for (int i = 0; i < count; i++)
            yield return (prefix + i.ToString("D4"), TestArtifacts.SolidFlat((byte)(startIndex + i % 16)));
        yield return ("F_END", Array.Empty<byte>());
    }

    private static IEnumerable<(string name, byte[] bytes)> PngEntries(string prefix, int count, byte red)
    {
        for (int i = 0; i < count; i++)
        {
            byte green = (byte)(red + 1 + i % 16);
            byte blue = (byte)(red + 2 + i % 16);
            string name = "flats/" + prefix + i.ToString("D4") + ".png";
            yield return (name, TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, red, green, blue, 255)));
        }
    }

    private static string BuildResourceDirectory(string prefix, int count, byte red)
    {
        string root = Path.Combine(Path.GetTempPath(), "dbuilder_perf_dir_" + Guid.NewGuid().ToString("N"));
        string flats = Path.Combine(root, "flats");
        Directory.CreateDirectory(flats);

        for (int i = 0; i < count; i++)
        {
            byte green = (byte)(red + 1 + i % 16);
            byte blue = (byte)(red + 2 + i % 16);
            File.WriteAllBytes(
                Path.Combine(flats, prefix + i.ToString("D4") + ".png"),
                TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, red, green, blue, 255)));
        }

        return root;
    }

    private static string BuildWadFile(bool isIwad, params (string name, byte[] bytes)[] lumps)
        => TestArtifacts.BuildWadFile(isIwad, lumps);
}
