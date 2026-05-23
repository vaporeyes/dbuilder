// ABOUTME: Tests find/replace over a MapSet by category and the next-free-tag helper.
// ABOUTME: Covers numeric (type/action/effect/tag) and textual (texture/flat) categories plus selection side effects.

using System.Linq;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class MapSearchTests
{
    private static MapSet Build()
    {
        var map = new MapSet();
        var s1 = map.AddSector();
        s1.Special = 9; s1.Tag = 5; s1.FloorTexture = "FLOOR4_8"; s1.CeilTexture = "CEIL1_1";
        var s2 = map.AddSector();
        s2.Special = 9; s2.Tag = 0; s2.FloorTexture = "NUKAGE1"; s2.CeilTexture = "FLOOR4_8";

        var v = new[]
        {
            map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(0, 64)),
            map.AddVertex(new Vector2D(64, 64)), map.AddVertex(new Vector2D(64, 0)),
        };
        for (int i = 0; i < 4; i++)
        {
            var l = map.AddLinedef(v[i], v[(i + 1) % 4]);
            var sd = map.AddSidedef(l, true, s1);
            sd.MidTexture = "STARTAN3";
            if (i == 0) { l.Action = 11; l.Tag = 5; }
        }
        map.AddThing(new Vector2D(10, 10), 3001); // imp
        map.AddThing(new Vector2D(20, 20), 3001);
        map.AddThing(new Vector2D(30, 30), 9);     // tag holder
        map.Things[2].Tag = 5;
        map.BuildIndexes();
        return map;
    }

    [Fact]
    public void FindThingTypeSelectsMatches()
    {
        var map = Build();
        var r = MapSearch.Find(map, FindCategory.ThingType, "3001");
        Assert.Equal(2, r.Count);
        Assert.Equal(2, map.Things.Count(t => t.Selected));
        Assert.NotNull(r.Focus);
    }

    [Fact]
    public void FindTagSpansLinesSectorsThings()
    {
        var map = Build();
        var r = MapSearch.Find(map, FindCategory.Tag, "5");
        // sector s1 (tag 5), linedef 0 (tag 5), thing[2] (tag 5)
        Assert.Equal(3, r.Count);
        Assert.True(map.Sectors[0].Selected);
        Assert.True(map.Linedefs[0].Selected);
        Assert.True(map.Things[2].Selected);
    }

    [Fact]
    public void FindClearsPriorSelection()
    {
        var map = Build();
        map.Things[0].Selected = true;
        MapSearch.Find(map, FindCategory.SectorEffect, "9");
        Assert.False(map.Things[0].Selected); // cleared before the new selection
        Assert.Equal(2, map.Sectors.Count(s => s.Selected));
    }

    [Fact]
    public void ReplaceLinedefAction()
    {
        var map = Build();
        int n = MapSearch.Replace(map, FindCategory.LinedefAction, "11", "97");
        Assert.Equal(1, n);
        Assert.Equal(97, map.Linedefs[0].Action);
    }

    [Fact]
    public void ReplaceTextureCaseInsensitive()
    {
        var map = Build();
        int n = MapSearch.Replace(map, FindCategory.Texture, "startan3", "BROWN1");
        Assert.Equal(4, n); // all four sidedefs
        Assert.All(map.Sidedefs, sd => Assert.Equal("BROWN1", sd.MidTexture));
    }

    [Fact]
    public void ReplaceFlatTouchesFloorAndCeiling()
    {
        var map = Build();
        // FLOOR4_8 appears as s1 floor and s2 ceiling -> two sectors changed.
        int n = MapSearch.Replace(map, FindCategory.Flat, "FLOOR4_8", "FLAT5_5");
        Assert.Equal(2, n);
        Assert.Equal("FLAT5_5", map.Sectors[0].FloorTexture);
        Assert.Equal("FLAT5_5", map.Sectors[1].CeilTexture);
    }

    [Fact]
    public void NextFreeTagSkipsUsed()
    {
        var map = Build();
        // tag 5 is used; tags 1..4 are free -> next free is 1.
        Assert.Equal(1, MapSearch.NextFreeTag(map));
        map.Sectors[1].Tag = 1; map.Linedefs[1].Tag = 2; map.Things[0].Tag = 3;
        Assert.Equal(4, MapSearch.NextFreeTag(map));
    }

    [Fact]
    public void ReplaceWithNonNumericValueDoesNothing()
    {
        var map = Build();
        Assert.Equal(0, MapSearch.Replace(map, FindCategory.ThingType, "3001", "notanumber"));
        Assert.Equal(2, map.Things.Count(t => t.Type == 3001)); // unchanged
    }
}
