// ABOUTME: Headless editor workflow smoke tests for open, edit, save, and reopen map paths.
// ABOUTME: Exercises the same map IO and undo snapshot services that MainWindow uses around user edits.

using System.IO;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class EditorWorkflowSmokeTests
{
    [Theory]
    [InlineData(MapFormat.Doom)]
    [InlineData(MapFormat.Hexen)]
    [InlineData(MapFormat.Udmf)]
    public void OpenEditSaveAndReopenPreservesEditedMap(MapFormat format)
    {
        byte[] initialBytes = Save(BuildMap(format), "MAP01", format);

        using var source = new WAD(new MemoryStream(initialBytes), openreadonly: true);
        MapEntry entry = Assert.Single(WadMaps.Find(source));
        MapSet map = WadMaps.Load(source, entry)!;
        var undo = new UndoManager(map);

        undo.CreateUndo("Move thing and raise floor");
        map.Things[0].Position = new Vector2D(96, 80);
        map.Sectors[0].FloorHeight = 24;
        map.Sectors[0].Brightness = 144;
        map.BuildIndexes();

        byte[] editedBytes = Save(map, entry.Name, format);

        using var reopened = new WAD(new MemoryStream(editedBytes), openreadonly: true);
        MapEntry editedEntry = Assert.Single(WadMaps.Find(reopened));
        MapSet edited = WadMaps.Load(reopened, editedEntry)!;

        Assert.Equal(entry.Name, editedEntry.Name);
        Assert.Equal(format, editedEntry.Format);
        Assert.Equal(new Vector2D(96, 80), edited.Things[0].Position);
        Assert.Equal(24, edited.Sectors[0].FloorHeight);
        Assert.Equal(144, edited.Sectors[0].Brightness);
    }

    private static byte[] Save(MapSet map, string marker, MapFormat format)
    {
        using var stream = new MemoryStream();
        using (var wad = new WAD(stream))
            WadMaps.SaveMap(wad, marker, map, format);

        return stream.ToArray();
    }

    private static MapSet BuildMap(MapFormat format)
    {
        var map = new MapSet { Namespace = format == MapFormat.Udmf ? "ZDoom" : "Doom" };
        Sector sector = map.AddSector();
        sector.FloorHeight = 0;
        sector.CeilHeight = 128;
        sector.FloorTexture = "FLOOR1";
        sector.CeilTexture = "CEIL1";
        sector.Brightness = 192;

        Vertex v0 = map.AddVertex(new Vector2D(0, 0));
        Vertex v1 = map.AddVertex(new Vector2D(128, 0));
        Vertex v2 = map.AddVertex(new Vector2D(128, 128));
        Vertex v3 = map.AddVertex(new Vector2D(0, 128));
        Vertex[] vertices = [v0, v1, v2, v3];

        for (int i = 0; i < vertices.Length; i++)
        {
            Linedef line = map.AddLinedef(vertices[i], vertices[(i + 1) % vertices.Length]);
            line.Flags = 1;
            Sidedef side = map.AddSidedef(line, true, sector);
            side.MidTexture = "STARTAN3";
        }

        Thing thing = map.AddThing(new Vector2D(64, 64), 3001);
        thing.Angle = 90;
        thing.Flags = 7;

        map.BuildIndexes();
        return map;
    }
}
