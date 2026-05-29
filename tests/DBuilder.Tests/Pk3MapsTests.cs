// ABOUTME: Tests PK3 map discovery and loading for embedded map WAD entries.
// ABOUTME: Builds temporary PK3 files with UDMF maps and confirms the loader delegates through WadMaps.

using System.IO;
using System.IO.Compression;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class Pk3MapsTests
{
    [Fact]
    public void FindsMapsInEmbeddedWadEntries()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("maps/map01.wad", BuildUdmfWad("MAP01")),
            ("textures/WALL.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 255, 0, 0, 255))));

        try
        {
            var maps = Pk3Maps.Find(pk3);

            var entry = Assert.Single(maps);
            Assert.Equal("maps/map01.wad", entry.ArchivePath);
            Assert.Equal("MAP01", entry.Map.Name);
            Assert.Equal(MapFormat.Udmf, entry.Map.Format);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void LoadsMapFromEmbeddedWadEntry()
    {
        string pk3 = TestArtifacts.BuildPk3(("maps/map01.wad", BuildUdmfWad("MAP01")));

        try
        {
            var entry = Assert.Single(Pk3Maps.Find(pk3));
            var map = Pk3Maps.Load(pk3, entry);

            Assert.NotNull(map);
            Assert.Equal("ZDoom", map!.Namespace);
            Assert.Equal(4, map.Vertices.Count);
            Assert.Equal(4, map.Linedefs.Count);
            Assert.Single(map.Sectors);
            Assert.Equal(7, map.Sectors[0].Tag);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void FindsAndLoadsMapsInNestedPk3Entries()
    {
        string pk3 = TestArtifacts.BuildPk3(("archives/nested.pk3", BuildNestedPk3WithMap()));

        try
        {
            var entry = Assert.Single(Pk3Maps.Find(pk3));

            Assert.Equal("archives/nested.pk3!maps/map02.wad", entry.ArchivePath);
            Assert.Equal("MAP02", entry.Map.Name);

            var map = Pk3Maps.Load(pk3, entry);

            Assert.NotNull(map);
            Assert.Equal("ZDoom", map!.Namespace);
            Assert.Equal(7, map.Sectors[0].Tag);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    private static byte[] BuildUdmfWad(string marker)
    {
        using var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            WriteLump(wad, marker, Array.Empty<byte>());
            WriteLump(wad, "TEXTMAP", Encoding.ASCII.GetBytes("""
                namespace = "ZDoom";
                vertex { x = 0.0; y = 0.0; }
                vertex { x = 64.0; y = 0.0; }
                vertex { x = 64.0; y = 64.0; }
                vertex { x = 0.0; y = 64.0; }
                sector { heightfloor = 0; heightceiling = 128; texturefloor = "FLOOR0_1"; textureceiling = "CEIL1_1"; id = 7; }
                sidedef { sector = 0; texturemiddle = "STARTAN3"; }
                sidedef { sector = 0; texturemiddle = "STARTAN3"; }
                sidedef { sector = 0; texturemiddle = "STARTAN3"; }
                sidedef { sector = 0; texturemiddle = "STARTAN3"; }
                linedef { v1 = 0; v2 = 1; sidefront = 0; }
                linedef { v1 = 1; v2 = 2; sidefront = 1; }
                linedef { v1 = 2; v2 = 3; sidefront = 2; }
                linedef { v1 = 3; v2 = 0; sidefront = 3; }
                """));
            WriteLump(wad, "ENDMAP", Array.Empty<byte>());
            wad.WriteHeaders();
        }

        return ms.ToArray();
    }

    private static byte[] BuildNestedPk3WithMap()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var stream = zip.CreateEntry("maps/map02.wad").Open();
            byte[] wadBytes = BuildUdmfWad("MAP02");
            stream.Write(wadBytes, 0, wadBytes.Length);
        }

        return ms.ToArray();
    }

    private static void WriteLump(WAD wad, string name, byte[] data)
    {
        var lump = wad.Insert(name, wad.Lumps.Count, data.Length)!;
        if (data.Length > 0) lump.Stream.Write(data, 0, data.Length);
    }
}
