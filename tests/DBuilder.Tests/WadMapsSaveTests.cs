// ABOUTME: Tests for WadMaps.SaveMap - replacing a map block in a WAD in place, preserving other lumps/format.
// ABOUTME: Builds a multi-map WAD with a graphic, edits one map, saves back, and reloads to verify persistence.

using System.IO;
using System.Linq;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class WadMapsSaveTests
{
    private static MapSet SquareMap()
    {
        var map = new MapSet();
        var s = map.AddSector();
        var v = new[]
        {
            map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(0, 64)),
            map.AddVertex(new Vector2D(64, 64)), map.AddVertex(new Vector2D(64, 0)),
        };
        for (int i = 0; i < 4; i++) map.AddSidedef(map.AddLinedef(v[i], v[(i + 1) % 4]), true, s);
        map.BuildIndexes();
        return map;
    }

    private static WAD BuildWad()
    {
        var wad = new WAD(new MemoryStream());
        var g = wad.Insert("CREDIT", 0, 4)!;        // a non-map graphic lump
        g.Stream.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);
        DoomMapWriter.WriteMap(SquareMap(), wad, "MAP01", wad.Lumps.Count);
        DoomMapWriter.WriteMap(SquareMap(), wad, "MAP02", wad.Lumps.Count);
        wad.WriteHeaders();
        return wad;
    }

    [Fact]
    public void SaveMapPersistsEditAndPreservesOtherLumps()
    {
        using var wad = BuildWad();

        var maps = WadMaps.Find(wad);
        Assert.Equal(2, maps.Count);

        var entry1 = maps.First(m => m.Name == "MAP01");
        var loaded = WadMaps.Load(wad, entry1)!;
        loaded.AddThing(new Vector2D(5, 5), 3001); // edit: add one thing
        loaded.BuildIndexes();

        WadMaps.SaveMap(wad, "MAP01", loaded, MapFormat.Doom);

        // The edit persisted, MAP02 and the graphic are untouched, and formats are intact.
        var reloaded1 = WadMaps.Load(wad, WadMaps.Find(wad).First(m => m.Name == "MAP01"))!;
        Assert.Single(reloaded1.Things);
        Assert.Equal(4, reloaded1.Vertices.Count);

        var entry2 = WadMaps.Find(wad).First(m => m.Name == "MAP02");
        Assert.Equal(MapFormat.Doom, entry2.Format);
        var reloaded2 = WadMaps.Load(wad, entry2)!;
        Assert.Equal(4, reloaded2.Vertices.Count);

        Assert.NotNull(wad.FindLump("CREDIT"));
    }

    [Fact]
    public void SaveMapAppendsWhenMarkerAbsent()
    {
        using var wad = new WAD(new MemoryStream());
        WadMaps.SaveMap(wad, "MAP01", SquareMap(), MapFormat.Doom);

        var maps = WadMaps.Find(wad);
        Assert.Single(maps);
        Assert.Equal("MAP01", maps[0].Name);
        Assert.Equal(4, WadMaps.Load(wad, maps[0])!.Vertices.Count);
    }

    [Fact]
    public void SaveMapDoesNotReplaceNonMapLumpWithSameName()
    {
        using var wad = new WAD(new MemoryStream());
        var graphic = wad.Insert("MAP01", 0, 3)!;
        graphic.Stream.Write(new byte[] { 7, 8, 9 }, 0, 3);
        wad.WriteHeaders();

        WadMaps.SaveMap(wad, "MAP01", SquareMap(), MapFormat.Doom);

        Assert.Equal(new byte[] { 7, 8, 9 }, wad.Lumps[0].Stream.ReadAllBytes());
        Assert.Equal("MAP01", wad.Lumps[1].Name);
        Assert.Equal("THINGS", wad.Lumps[2].Name);
        Assert.Single(WadMaps.Find(wad));
    }

    [Fact]
    public void SaveBackFlowCopiesWadEditsMapAndReopens()
    {
        // Mirrors the editor's save-back: copy the source WAD into a fresh one, replace the edited map,
        // serialize to bytes, then reopen and verify everything survived.
        byte[] bytes;
        using (var src = BuildWad())
        {
            var m1 = WadMaps.Load(src, WadMaps.Find(src).First(m => m.Name == "MAP01"))!;
            m1.AddThing(new Vector2D(5, 5), 3001);
            m1.BuildIndexes();

            var ms = new MemoryStream();
            using (var dst = new WAD(ms))
            {
                WadMaps.CopyAllLumps(src, dst);
                WadMaps.SaveMap(dst, "MAP01", m1, MapFormat.Doom);
                bytes = ms.ToArray();
            }
        }

        using var reopened = new WAD(new MemoryStream(bytes), openreadonly: true);
        var maps = WadMaps.Find(reopened);
        Assert.Equal(2, maps.Count);
        Assert.NotNull(reopened.FindLump("CREDIT"));
        Assert.Single(WadMaps.Load(reopened, maps.First(m => m.Name == "MAP01"))!.Things);
        Assert.Equal(4, WadMaps.Load(reopened, maps.First(m => m.Name == "MAP02"))!.Vertices.Count);
    }

    [Fact]
    public void SaveMapDoesNotDuplicateBlockOnResave()
    {
        using var wad = BuildWad();
        int lumpsBefore = wad.Lumps.Count;

        var loaded = WadMaps.Load(wad, WadMaps.Find(wad).First(m => m.Name == "MAP01"))!;
        WadMaps.SaveMap(wad, "MAP01", loaded, MapFormat.Doom);

        // A Doom map block is marker + 5 sub-lumps; re-saving must replace, not append.
        Assert.Equal(lumpsBefore, wad.Lumps.Count);
        Assert.Equal(2, WadMaps.Find(wad).Count);
    }
}
