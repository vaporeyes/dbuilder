// ABOUTME: Tests UDB-style selection group bitmasks on groupable map elements.
// ABOUTME: Confirms MapSet helpers add, select and clear groups for vertices, linedefs, sectors and things.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SelectionGroupTests
{
    [Fact]
    public void AddSelectionToGroupStoresSelectedGroupableElements()
    {
        var map = BuildMap();
        map.Vertices[0].Selected = true;
        map.Linedefs[0].Selected = true;
        map.Sidedefs[0].Selected = true;
        map.Sectors[0].Selected = true;
        map.Things[0].Selected = true;

        map.AddSelectionToGroup(2);

        int mask = MapSet.GroupMask(2);
        Assert.Equal(mask, map.Vertices[0].Groups);
        Assert.Equal(0, map.Vertices[1].Groups);
        Assert.Equal(mask, map.Linedefs[0].Groups);
        Assert.Equal(mask, map.Sectors[0].Groups);
        Assert.Equal(mask, map.Things[0].Groups);
    }

    [Fact]
    public void SelectByGroupUpdatesOnlyTheRequestedElementType()
    {
        var map = BuildMap();
        map.Vertices[0].Groups = MapSet.GroupMask(1);
        map.Vertices[1].Selected = true;
        map.Linedefs[0].Groups = MapSet.GroupMask(1);
        map.Linedefs[0].Selected = true;

        map.SelectVerticesByGroup(MapSet.GroupMask(1));

        Assert.True(map.Vertices[0].Selected);
        Assert.False(map.Vertices[1].Selected);
        Assert.True(map.Linedefs[0].Selected);
    }

    [Fact]
    public void ClearGroupRemovesMembershipFromAllGroupableTypes()
    {
        var map = BuildMap();
        int target = MapSet.GroupMask(3);
        int other = MapSet.GroupMask(4);
        map.Vertices[0].Groups = target | other;
        map.Linedefs[0].Groups = target;
        map.Sectors[0].Groups = target;
        map.Things[0].Groups = target;

        map.ClearGroup(target);

        Assert.Equal(other, map.Vertices[0].Groups);
        Assert.Equal(0, map.Linedefs[0].Groups);
        Assert.Equal(0, map.Sectors[0].Groups);
        Assert.Equal(0, map.Things[0].Groups);
    }

    [Fact]
    public void GroupIndexMustFitSignedBitmask()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapSet.GroupMask(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapSet.GroupMask(31));
    }

    private static MapSet BuildMap()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(v0, v1);
        map.AddSidedef(line, true, sector);
        map.AddThing(new Vector2D(32, 16), 3001);
        map.BuildIndexes();
        return map;
    }
}
