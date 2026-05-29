// ABOUTME: Tests MapSet.Clone deep-copy behavior for map elements and their references.
// ABOUTME: Covers topology remapping, custom fields, args, flags and transient selection/mark state.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class MapSetCloneTests
{
    [Fact]
    public void CloneCopiesElementsAndRemapsReferences()
    {
        var map = BuildSample();

        var clone = map.Clone();

        Assert.NotSame(map, clone);
        Assert.Equal(map.Namespace, clone.Namespace);
        Assert.Equal("tester", clone.Fields["author"]);
        Assert.Equal("editorstate", clone.UnknownUdmfData[0].Key);
        Assert.Equal("view", clone.UnknownUdmfData[0].Children[0].Key);
        Assert.Equal("2d", clone.UnknownUdmfData[0].Children[0].Value);
        Assert.Equal(map.Vertices.Count, clone.Vertices.Count);
        Assert.Equal(map.Linedefs.Count, clone.Linedefs.Count);
        Assert.Equal(map.Sidedefs.Count, clone.Sidedefs.Count);
        Assert.Equal(map.Sectors.Count, clone.Sectors.Count);
        Assert.Equal(map.Things.Count, clone.Things.Count);

        Assert.NotSame(map.Vertices[0], clone.Vertices[0]);
        Assert.NotSame(map.Linedefs[0], clone.Linedefs[0]);
        Assert.NotSame(map.Sidedefs[0], clone.Sidedefs[0]);
        Assert.NotSame(map.Sectors[0], clone.Sectors[0]);
        Assert.NotSame(map.Things[0], clone.Things[0]);
        Assert.False(clone.Vertices[0].IsDisposed);
        Assert.False(clone.Linedefs[0].IsDisposed);
        Assert.False(clone.Sidedefs[0].IsDisposed);
        Assert.False(clone.Sectors[0].IsDisposed);
        Assert.False(clone.Things[0].IsDisposed);

        Assert.Same(clone.Vertices[0], clone.Linedefs[0].Start);
        Assert.Same(clone.Vertices[1], clone.Linedefs[0].End);
        Assert.Same(clone.Sidedefs[0], clone.Linedefs[0].Front);
        Assert.Same(clone.Sidedefs[1], clone.Linedefs[0].Back);
        Assert.Same(clone.Sectors[0], clone.Sidedefs[0].Sector);
        Assert.Same(clone.Sectors[1], clone.Sidedefs[1].Sector);
        Assert.Same(clone.Sidedefs[1], clone.Sidedefs[0].Other);
        Assert.Same(clone.Sectors[0], clone.Things[0].Sector);
    }

    [Fact]
    public void ClonePreservesElementDataAndTransientState()
    {
        var clone = BuildSample().Clone();

        Assert.True(clone.Vertices[0].Selected);
        Assert.True(clone.Linedefs[0].Marked);
        Assert.True(clone.Sidedefs[0].Selected);
        Assert.True(clone.Sectors[1].Marked);
        Assert.True(clone.Things[0].Selected);

        Assert.Equal(MapSet.GroupMask(1), clone.Vertices[0].Groups);
        Assert.Equal(MapSet.GroupMask(2), clone.Linedefs[0].Groups);
        Assert.Equal(MapSet.GroupMask(3), clone.Sectors[0].Groups);
        Assert.Equal(MapSet.GroupMask(4), clone.Things[0].Groups);
        Assert.Equal(-8.0, clone.Vertices[0].ZFloor);
        Assert.Equal(16711680, clone.Sectors[0].GetField<int>("lightcolor"));
        Assert.Contains("secret", clone.Sectors[0].UdmfFlags);
        Assert.Equal(new[] { 4, 12 }, clone.Sectors[0].Tags);
        Assert.Equal(9, clone.Linedefs[0].GetArg(2));
        Assert.Contains("blocking", clone.Linedefs[0].UdmfFlags);
        Assert.Equal("MID", clone.Sidedefs[0].MidTexture);
        Assert.Contains("lightabsolute", clone.Sidedefs[0].UdmfFlags);
        Assert.Equal(200, clone.Things[0].GetField<int>("health"));
        Assert.Equal(33, clone.Things[0].GetArg(1));
    }

    [Fact]
    public void CloneIsIndependentFromOriginalMutations()
    {
        var map = BuildSample();
        var clone = map.Clone();

        map.Vertices[0].Position = new Vector2D(999, 999);
        map.Fields["author"] = "changed";
        map.UnknownUdmfData.Clear();
        map.Sectors[0].Fields["lightcolor"] = 1;
        map.Linedefs[0].SetArg(2, 1);
        map.Sidedefs[0].MidTexture = "CHANGED";
        map.Things[0].Position = new Vector2D(10, 10);

        Assert.Equal(new Vector2D(0, 0), clone.Vertices[0].Position);
        Assert.Equal("tester", clone.Fields["author"]);
        Assert.Single(clone.UnknownUdmfData);
        Assert.Equal(16711680, clone.Sectors[0].GetField<int>("lightcolor"));
        Assert.Equal(9, clone.Linedefs[0].GetArg(2));
        Assert.Equal("MID", clone.Sidedefs[0].MidTexture);
        Assert.Equal(new Vector2D(64, 32), clone.Things[0].Position);
    }

    private static MapSet BuildSample()
    {
        var map = new MapSet { Namespace = "Doom" };
        map.Fields["author"] = "tester";
        map.UnknownUdmfData.Add(new UnknownUdmfEntry("editorstate", new List<UnknownUdmfEntry>
        {
            new("view", "2d"),
        }));
        var frontSector = map.AddSector();
        frontSector.Groups = MapSet.GroupMask(3);
        frontSector.Tag = 4;
        frontSector.Tags.Add(12);
        frontSector.UdmfFlags.Add("secret");
        frontSector.SetField("lightcolor", 16711680);
        var backSector = map.AddSector();
        backSector.Marked = true;

        var v0 = map.AddVertex(new Vector2D(0, 0));
        v0.Selected = true;
        v0.Groups = MapSet.GroupMask(1);
        v0.ZFloor = -8.0;
        var v1 = map.AddVertex(new Vector2D(128, 0));
        var line = map.AddLinedef(v0, v1);
        line.Marked = true;
        line.Groups = MapSet.GroupMask(2);
        line.Flags = 1;
        line.Action = 80;
        line.SetArg(2, 9);
        line.UdmfFlags.Add("blocking");

        var front = map.AddSidedef(line, true, frontSector);
        front.Selected = true;
        front.MidTexture = "MID";
        front.OffsetX = 16;
        front.UdmfFlags.Add("lightabsolute");
        var back = map.AddSidedef(line, false, backSector);
        back.LowTexture = "LOW";

        var thing = map.AddThing(new Vector2D(64, 32), 3001);
        thing.Selected = true;
        thing.Groups = MapSet.GroupMask(4);
        thing.Sector = frontSector;
        thing.SetArg(1, 33);
        thing.SetField("health", 200);
        map.BuildIndexes();
        return map;
    }
}
