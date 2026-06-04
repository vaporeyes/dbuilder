// ABOUTME: Tests find/replace over a MapSet by category and the next-free-tag helper.
// ABOUTME: Covers numeric (type/action/effect/tag) and textual (texture/flat) categories plus selection side effects.

using System.Linq;
using DBuilder.Geometry;
using DBuilder.IO;
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
        map.Things[0].Angle = 90;
        map.Things[1].Angle = 180;
        map.Things[2].Angle = 90;
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
    public void FindThingTypeAcceptsCommaSeparatedValues()
    {
        var map = Build();

        var result = MapSearch.Find(map, FindCategory.ThingType, "3001, 9");

        Assert.Equal(3, result.Count);
        Assert.All(map.Things, thing => Assert.True(thing.Selected));
    }

    [Fact]
    public void ReplaceThingTypeAcceptsCommaSeparatedValues()
    {
        var map = Build();

        int changed = MapSearch.Replace(map, FindCategory.ThingType, "3001, 9", "2001, 2002");

        Assert.Equal(3, changed);
        Assert.All(map.Things, thing => Assert.Contains(thing.Type, new[] { 2001, 2002 }));
    }

    [Fact]
    public void ThingTypeCommaListRejectsInvalidEntries()
    {
        var map = Build();

        Assert.Equal(0, MapSearch.Find(map, FindCategory.ThingType, "3001, nope").Count);
        Assert.Equal(0, MapSearch.Replace(map, FindCategory.ThingType, "3001", "2001, nope"));
        Assert.Equal(2, map.Things.Count(thing => thing.Type == 3001));
    }

    [Fact]
    public void ReplaceThingTypeRejectsClassicOutOfRangeReplacementValues()
    {
        var map = Build();

        int changed = MapSearch.Replace(map, FindCategory.ThingType, "3001", "32768");

        Assert.Equal(0, changed);
        Assert.Equal(2, map.Things.Count(thing => thing.Type == 3001));
    }

    [Fact]
    public void CategoryDescriptorsExposeFindReplaceDialogOrderAndLabels()
    {
        IReadOnlyList<FindCategoryDescriptor> descriptors = MapSearch.CategoryDescriptors;

        Assert.Equal(Enum.GetValues<FindCategory>().Length, descriptors.Count);
        Assert.Equal(Enum.GetValues<FindCategory>().OrderBy(category => category), descriptors.Select(descriptor => descriptor.Category).OrderBy(category => category));
        Assert.Equal(new FindCategoryDescriptor(FindCategory.ThingType, "Thing Type", BrowseButton: true), descriptors[0]);
        Assert.Equal(new FindCategoryDescriptor(FindCategory.TextureOrFlat, "Any Texture or Flat", BrowseButton: true), descriptors[25]);
        Assert.Equal(new FindCategoryDescriptor(FindCategory.ThingUdmfField, "Thing UDMF Field"), descriptors[^1]);
        Assert.Equal("Sidedef Texture (Middle)", descriptors.Single(descriptor => descriptor.Category == FindCategory.SidedefMiddleTexture).ToString());
        Assert.Equal("Sector Height (Ceiling)", descriptors.Single(descriptor => descriptor.Category == FindCategory.SectorCeilingHeight).Label);
        Assert.Equal("Sector Flat (Floor)", descriptors.Single(descriptor => descriptor.Category == FindCategory.SectorFloorFlat).Label);
        Assert.True(descriptors.Single(descriptor => descriptor.Category == FindCategory.LinedefActionArguments).BrowseButton);
        Assert.True(descriptors.Single(descriptor => descriptor.Category == FindCategory.SectorEffect).BrowseButton);
        Assert.False(descriptors.Single(descriptor => descriptor.Category == FindCategory.ThingTag).BrowseButton);
        Assert.False(descriptors.Single(descriptor => descriptor.Category == FindCategory.AnyUdmfField).BrowseButton);
    }

    [Theory]
    [InlineData(0, "No matches.")]
    [InlineData(1, "Found 1 match.")]
    [InlineData(3, "Found 3 matches.")]
    public void FormatFindResultMatchesEditorStatusText(int count, string expected)
        => Assert.Equal(expected, MapSearch.FormatFindResult(count));

    [Theory]
    [InlineData(0, "Nothing replaced.")]
    [InlineData(1, "Replaced 1 element.")]
    [InlineData(3, "Replaced 3 elements.")]
    public void FormatReplaceResultMatchesEditorStatusText(int count, string expected)
        => Assert.Equal(expected, MapSearch.FormatReplaceResult(count));

    [Fact]
    public void FormatNextFreeTagResultMatchesEditorStatusText()
        => Assert.Equal("Next free tag: 4.", MapSearch.FormatNextFreeTagResult(4));

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
    public void FindWithinSelectionLimitsElementScansBeforeClearingSelection()
    {
        var map = Build();
        map.Linedefs[0].Selected = true;
        map.Sectors[1].Selected = true;
        map.Things[1].Selected = true;
        map.Things[2].Selected = true;

        SearchResult thingResult = MapSearch.Find(map, FindCategory.ThingAngle, "90", withinSelection: true);
        Assert.Equal(1, thingResult.Count);
        Assert.False(map.Things[0].Selected);
        Assert.False(map.Things[1].Selected);
        Assert.True(map.Things[2].Selected);

        map.Linedefs[0].Selected = true;
        SearchResult sidedefResult = MapSearch.Find(map, FindCategory.SidedefMiddleTexture, "STARTAN3", withinSelection: true);
        Assert.Equal(1, sidedefResult.Count);
        Assert.True(map.Linedefs[0].Selected);
        Assert.False(map.Linedefs[1].Selected);

        map.Sectors[1].Selected = true;
        SearchResult flatResult = MapSearch.Find(map, FindCategory.Flat, "FLOOR4_8", withinSelection: true);
        Assert.Equal(1, flatResult.Count);
        Assert.False(map.Sectors[0].Selected);
        Assert.True(map.Sectors[1].Selected);
    }

    [Fact]
    public void ReplaceWithinSelectionLimitsMutations()
    {
        var map = Build();
        map.Sectors[1].Selected = true;
        map.Linedefs[0].Selected = true;

        Assert.Equal(1, MapSearch.Replace(map, FindCategory.Flat, "FLOOR4_8", "STONE1", withinSelection: true));
        Assert.Equal("FLOOR4_8", map.Sectors[0].FloorTexture);
        Assert.Equal("STONE1", map.Sectors[1].CeilTexture);

        Assert.Equal(1, MapSearch.Replace(map, FindCategory.SidedefMiddleTexture, "STARTAN3", "BROWN1", withinSelection: true));
        Assert.Equal("BROWN1", map.Sidedefs[0].MidTexture);
        Assert.Equal("STARTAN3", map.Sidedefs[1].MidTexture);
    }

    [Fact]
    public void FindTagHonorsFormatSpecificTagOwners()
    {
        var map = Build();
        var r = MapSearch.Find(map, FindCategory.Tag, "5", new TagSearchOptions(IncludeLinedefs: true, IncludeThings: false));

        Assert.Equal(2, r.Count);
        Assert.True(map.Sectors[0].Selected);
        Assert.True(map.Linedefs[0].Selected);
        Assert.False(map.Things[2].Selected);
    }

    [Fact]
    public void FindTagMatchesMoreIds()
    {
        var map = Build();
        map.Sectors[0].Tags.Add(17);
        map.Linedefs[0].Tags.Add(17);

        var r = MapSearch.Find(map, FindCategory.Tag, "17");

        Assert.Equal(2, r.Count);
        Assert.True(map.Sectors[0].Selected);
        Assert.True(map.Linedefs[0].Selected);
    }

    [Fact]
    public void FindSpecificTagCategoriesMatchOnlyTheirElementTypes()
    {
        var map = Build();

        var linedefs = MapSearch.Find(map, FindCategory.LinedefTag, "5");
        Assert.Equal(1, linedefs.Count);
        Assert.True(map.Linedefs[0].Selected);
        Assert.False(map.Sectors[0].Selected);
        Assert.False(map.Things[2].Selected);

        var sectors = MapSearch.Find(map, FindCategory.SectorTag, "5");
        Assert.Equal(1, sectors.Count);
        Assert.False(map.Linedefs[0].Selected);
        Assert.True(map.Sectors[0].Selected);
        Assert.False(map.Things[2].Selected);

        var things = MapSearch.Find(map, FindCategory.ThingTag, "5");
        Assert.Equal(1, things.Count);
        Assert.False(map.Linedefs[0].Selected);
        Assert.False(map.Sectors[0].Selected);
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
    public void FindSectorEffectMinusOneSelectsAnyNonzeroEffect()
    {
        var map = Build();
        map.Sectors[1].Special = 0;

        SearchResult result = MapSearch.Find(map, FindCategory.SectorEffect, "-1");

        Assert.Equal(1, result.Count);
        Assert.True(map.Sectors[0].Selected);
        Assert.False(map.Sectors[1].Selected);
    }

    [Fact]
    public void ReplaceSectorEffectMinusOneChangesAnyNonzeroEffect()
    {
        var map = Build();
        map.Sectors[1].Special = 0;

        int changed = MapSearch.Replace(map, FindCategory.SectorEffect, "-1", "11");

        Assert.Equal(1, changed);
        Assert.Equal(11, map.Sectors[0].Special);
        Assert.Equal(0, map.Sectors[1].Special);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("32768")]
    public void ReplaceSectorEffectRejectsInvalidReplacementValues(string replacement)
    {
        var map = Build();

        int changed = MapSearch.Replace(map, FindCategory.SectorEffect, "9", replacement);

        Assert.Equal(0, changed);
        Assert.Equal(9, map.Sectors[0].Special);
        Assert.Equal(9, map.Sectors[1].Special);
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
    public void FindLinedefActionMinusOneSelectsAnyNonzeroAction()
    {
        var map = Build();
        map.Linedefs[0].Action = 11;
        map.Linedefs[1].Action = 0;

        SearchResult result = MapSearch.Find(map, FindCategory.LinedefAction, "-1");

        Assert.Equal(1, result.Count);
        Assert.True(map.Linedefs[0].Selected);
        Assert.False(map.Linedefs[1].Selected);
    }

    [Fact]
    public void ReplaceLinedefActionMinusOneChangesAnyNonzeroAction()
    {
        var map = Build();
        map.Linedefs[0].Action = 11;
        map.Linedefs[1].Action = 0;

        int changed = MapSearch.Replace(map, FindCategory.LinedefAction, "-1", "97");

        Assert.Equal(1, changed);
        Assert.Equal(97, map.Linedefs[0].Action);
        Assert.Equal(0, map.Linedefs[1].Action);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("32768")]
    public void ReplaceLinedefActionRejectsInvalidReplacementValues(string replacement)
    {
        var map = Build();

        int changed = MapSearch.Replace(map, FindCategory.LinedefAction, "11", replacement);

        Assert.Equal(0, changed);
        Assert.Equal(11, map.Linedefs[0].Action);
    }

    [Fact]
    public void FindAndReplaceLinedefActionArguments()
    {
        var map = Build();
        map.Linedefs[0].Action = 80;
        map.Linedefs[0].Args[0] = 17;
        map.Linedefs[0].Args[1] = 41;
        map.Linedefs[1].Action = 80;
        map.Linedefs[1].Args[0] = 17;
        map.Linedefs[1].Args[1] = 42;

        SearchResult result = MapSearch.Find(map, FindCategory.LinedefActionArguments, "80 17 *");

        Assert.Equal(2, result.Count);
        Assert.True(map.Linedefs[0].Selected);
        Assert.True(map.Linedefs[1].Selected);
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.LinedefActionArguments, "80 17 41", "81 * 99"));
        Assert.Equal(81, map.Linedefs[0].Action);
        Assert.Equal(17, map.Linedefs[0].Args[0]);
        Assert.Equal(99, map.Linedefs[0].Args[1]);
        Assert.Equal(80, map.Linedefs[1].Action);
        Assert.Equal(42, map.Linedefs[1].Args[1]);
    }

    [Fact]
    public void ActionArgumentQueriesIgnoreInvalidLaterArgsLikeUdb()
    {
        var map = Build();
        map.Linedefs[0].Action = 80;
        map.Linedefs[0].Args[0] = 17;
        map.Linedefs[0].Args[1] = 41;
        map.Linedefs[1].Action = 80;
        map.Linedefs[1].Args[0] = 18;
        map.Linedefs[1].Args[1] = 41;
        map.Things[0].Action = 80;
        map.Things[0].Args[0] = 17;
        map.Things[0].Args[1] = 41;
        map.Things[1].Action = 80;
        map.Things[1].Args[0] = 18;
        map.Things[1].Args[1] = 41;

        SearchResult lineResult = MapSearch.Find(map, FindCategory.LinedefActionArguments, "80 17 invalid");

        Assert.Equal(1, lineResult.Count);
        Assert.True(map.Linedefs[0].Selected);
        Assert.False(map.Linedefs[1].Selected);

        SearchResult thingResult = MapSearch.Find(map, FindCategory.ThingActionArguments, "80 17 invalid");

        Assert.Equal(1, thingResult.Count);
        Assert.True(map.Things[0].Selected);
        Assert.False(map.Things[1].Selected);
    }

    [Fact]
    public void ActionArgumentReplacementsIgnoreInvalidLaterArgsLikeUdb()
    {
        var map = Build();
        map.Linedefs[0].Action = 80;
        map.Linedefs[0].Args[0] = 17;
        map.Linedefs[0].Args[1] = 41;
        map.Things[0].Action = 80;
        map.Things[0].Args[0] = 17;
        map.Things[0].Args[1] = 41;

        Assert.Equal(1, MapSearch.Replace(map, FindCategory.LinedefActionArguments, "80 17 41", "81 invalid 99"));
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.ThingActionArguments, "80 17 41", "82 invalid 99"));

        Assert.Equal(81, map.Linedefs[0].Action);
        Assert.Equal(17, map.Linedefs[0].Args[0]);
        Assert.Equal(99, map.Linedefs[0].Args[1]);
        Assert.Equal(82, map.Things[0].Action);
        Assert.Equal(17, map.Things[0].Args[0]);
        Assert.Equal(99, map.Things[0].Args[1]);
    }

    [Fact]
    public void FindLinedefActionArgumentsMinusOneMatchesAnyNonzeroAction()
    {
        var map = Build();
        map.Linedefs[0].Action = 80;
        map.Linedefs[0].Args[0] = 17;
        map.Linedefs[1].Action = 0;
        map.Linedefs[1].Args[0] = 17;

        SearchResult result = MapSearch.Find(map, FindCategory.LinedefActionArguments, "-1 17");

        Assert.Equal(1, result.Count);
        Assert.True(map.Linedefs[0].Selected);
        Assert.False(map.Linedefs[1].Selected);
    }

    [Fact]
    public void FindAndReplaceLinedefActionArgumentsSupportNamedScriptArg0()
    {
        var map = Build();
        map.Linedefs[0].Action = 80;
        map.Linedefs[0].Fields["arg0str"] = "OpenDoor";
        map.Linedefs[0].Args[1] = 41;
        map.Linedefs[1].Action = 80;
        map.Linedefs[1].Fields["arg0str"] = "CloseDoor";
        map.Linedefs[1].Args[1] = 41;

        SearchResult result = MapSearch.Find(map, FindCategory.LinedefActionArguments, "80 opendoor 41");

        Assert.Equal(1, result.Count);
        Assert.True(map.Linedefs[0].Selected);
        Assert.False(map.Linedefs[1].Selected);
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.LinedefActionArguments, "80 \"OpenDoor\" 41", "81 \"RaiseDoor\" 99"));
        Assert.Equal(81, map.Linedefs[0].Action);
        Assert.Equal("RaiseDoor", map.Linedefs[0].Fields["arg0str"]);
        Assert.Equal(99, map.Linedefs[0].Args[1]);
        Assert.Equal(0, map.Linedefs[0].Args[0]);
    }

    [Fact]
    public void FindAndReplaceThingActionArguments()
    {
        var map = Build();
        map.Things[0].Action = 80;
        map.Things[0].Args[0] = 17;
        map.Things[0].Args[1] = 41;
        map.Things[1].Action = 80;
        map.Things[1].Args[0] = 18;
        map.Things[1].Args[1] = 41;

        SearchResult result = MapSearch.Find(map, FindCategory.ThingActionArguments, "80 * 41");

        Assert.Equal(2, result.Count);
        Assert.True(map.Things[0].Selected);
        Assert.True(map.Things[1].Selected);
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.ThingActionArguments, "80 17 41", "82 25 *"));
        Assert.Equal(82, map.Things[0].Action);
        Assert.Equal(25, map.Things[0].Args[0]);
        Assert.Equal(41, map.Things[0].Args[1]);
        Assert.Equal(80, map.Things[1].Action);
    }

    [Fact]
    public void FindThingActionArgumentsMinusOneMatchesAnyNonzeroAction()
    {
        var map = Build();
        map.Things[0].Action = 80;
        map.Things[0].Args[0] = 17;
        map.Things[1].Action = 0;
        map.Things[1].Args[0] = 17;

        SearchResult result = MapSearch.Find(map, FindCategory.ThingActionArguments, "-1 17");

        Assert.Equal(1, result.Count);
        Assert.True(map.Things[0].Selected);
        Assert.False(map.Things[1].Selected);
    }

    [Fact]
    public void FindAndReplaceThingActionArgumentsSupportNamedScriptArg0()
    {
        var map = Build();
        map.Things[0].Action = 80;
        map.Things[0].Fields["arg0str"] = "SpawnWave";
        map.Things[0].Args[1] = 5;
        map.Things[1].Action = 80;
        map.Things[1].Fields["arg0str"] = "OtherWave";
        map.Things[1].Args[1] = 5;

        SearchResult result = MapSearch.Find(map, FindCategory.ThingActionArguments, "80 \"SpawnWave\" 5");

        Assert.Equal(1, result.Count);
        Assert.True(map.Things[0].Selected);
        Assert.False(map.Things[1].Selected);
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.ThingActionArguments, "80 SpawnWave 5", "82 NextWave 9"));
        Assert.Equal(82, map.Things[0].Action);
        Assert.Equal("NextWave", map.Things[0].Fields["arg0str"]);
        Assert.Equal(9, map.Things[0].Args[1]);
        Assert.Equal(0, map.Things[0].Args[0]);
    }

    [Fact]
    public void ActionArgumentReplacementRejectsInvalidAction()
    {
        var map = Build();
        map.Linedefs[0].Action = 80;
        map.Linedefs[0].Args[0] = 17;

        int changed = MapSearch.Replace(map, FindCategory.LinedefActionArguments, "80 17", "-1 25");

        Assert.Equal(0, changed);
        Assert.Equal(80, map.Linedefs[0].Action);
        Assert.Equal(17, map.Linedefs[0].Args[0]);
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
    public void TextureAndFlatFindPatternsAreTrimmedLikeUdb()
    {
        var map = Build();

        Assert.Equal(4, MapSearch.Find(map, FindCategory.Texture, " STARTAN3 ").Count);
        Assert.Equal(4, MapSearch.Replace(map, FindCategory.SidedefMiddleTexture, " START?N3 ", "BROWN1"));
        Assert.All(map.Sidedefs, sidedef => Assert.Equal("BROWN1", sidedef.MidTexture));

        Assert.Equal(2, MapSearch.Find(map, FindCategory.Flat, " FLOOR4_8 ").Count);
        Assert.Equal(2, MapSearch.Replace(map, FindCategory.Flat, " FLOOR4_8 ", "FLAT5_5"));
        Assert.Equal("FLAT5_5", map.Sectors[0].FloorTexture);
        Assert.Equal("FLAT5_5", map.Sectors[1].CeilTexture);
    }

    [Fact]
    public void FindAndReplaceTextureCountsMatchingSidedefSlotsLikeUdb()
    {
        var map = Build();
        map.Sectors[0].FloorHeight = 0;
        map.Sectors[0].CeilHeight = 128;
        map.Sectors[1].FloorHeight = 24;
        map.Sectors[1].CeilHeight = 96;
        var back = map.AddSidedef(map.Linedefs[0], false, map.Sectors[1]);
        map.BuildIndexes();
        map.Sidedefs[0].HighTexture = "SUPPORT3";
        map.Sidedefs[0].MidTexture = "OTHER";
        map.Sidedefs[0].LowTexture = "SUPPORT3";
        back.HighTexture = "OTHER";
        back.LowTexture = "OTHER";

        SearchResult result = MapSearch.Find(map, FindCategory.Texture, "SUPPORT3");

        Assert.Equal(2, result.Count);
        Assert.True(map.Linedefs[0].Selected);
        Assert.Equal(2, MapSearch.Replace(map, FindCategory.Texture, "SUPPORT3", "STONE2"));
        Assert.Equal("STONE2", map.Sidedefs[0].HighTexture);
        Assert.Equal("STONE2", map.Sidedefs[0].LowTexture);
    }

    [Theory]
    [InlineData(FindCategory.Texture, "STARTAN3", "")]
    [InlineData(FindCategory.Texture, "STARTAN3", "LONGTEX01")]
    [InlineData(FindCategory.Flat, "FLOOR4_8", "")]
    [InlineData(FindCategory.Flat, "FLOOR4_8", "LONGFLAT1")]
    public void ReplaceTextureAndFlatRejectsInvalidReplacementNames(FindCategory category, string find, string replacement)
    {
        var map = Build();

        int changed = MapSearch.Replace(map, category, find, replacement);

        Assert.Equal(0, changed);
        Assert.Equal("STARTAN3", map.Sidedefs[0].MidTexture);
        Assert.Equal("FLOOR4_8", map.Sectors[0].FloorTexture);
    }

    [Fact]
    public void FindAndReplaceTextureSupportsWildcards()
    {
        var map = Build();

        SearchResult result = MapSearch.Find(map, FindCategory.Texture, "START*");

        Assert.Equal(4, result.Count);
        Assert.Equal(4, map.Linedefs.Count(l => l.Selected));
        Assert.Equal(4, MapSearch.Replace(map, FindCategory.SidedefMiddleTexture, "START?N3", "BROWN1"));
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
    public void FindAndReplaceFlatCountsMatchingSectorSlotsLikeUdb()
    {
        var map = Build();
        map.Sectors[0].FloorTexture = "FLOOR4_8";
        map.Sectors[0].CeilTexture = "FLOOR4_8";

        SearchResult result = MapSearch.Find(map, FindCategory.Flat, "FLOOR4_8");

        Assert.Equal(3, result.Count);
        Assert.True(map.Sectors[0].Selected);
        Assert.True(map.Sectors[1].Selected);
        Assert.Equal(3, MapSearch.Replace(map, FindCategory.Flat, "FLOOR4_8", "FLAT5_5"));
        Assert.Equal("FLAT5_5", map.Sectors[0].FloorTexture);
        Assert.Equal("FLAT5_5", map.Sectors[0].CeilTexture);
        Assert.Equal("FLAT5_5", map.Sectors[1].CeilTexture);
    }

    [Fact]
    public void FindAndReplaceFlatSupportsWildcards()
    {
        var map = Build();

        SearchResult result = MapSearch.Find(map, FindCategory.Flat, "FLOOR*");

        Assert.Equal(2, result.Count);
        Assert.True(map.Sectors[0].Selected);
        Assert.True(map.Sectors[1].Selected);
        Assert.Equal(2, MapSearch.Replace(map, FindCategory.Flat, "FLOOR?_?", "STONE1"));
        Assert.Equal("STONE1", map.Sectors[0].FloorTexture);
        Assert.Equal("STONE1", map.Sectors[1].CeilTexture);
    }

    [Fact]
    public void FindAnyTextureOrFlatTouchesBothSidedefsAndSectors()
    {
        var map = Build();

        SearchResult result = MapSearch.Find(map, FindCategory.TextureOrFlat, "FLOOR4_8");
        Assert.Equal(2, result.Count);
        Assert.True(map.Sectors[0].Selected);
        Assert.True(map.Sectors[1].Selected);

        Assert.Equal(4, MapSearch.Find(map, FindCategory.TextureOrFlat, "START*").Count);
    }

    [Fact]
    public void ReplaceAnyTextureOrFlatRequiresMixedTexturesAndFlats()
    {
        var map = Build();

        Assert.False(MapSearch.CanReplace(FindCategory.TextureOrFlat));
        Assert.True(MapSearch.CanReplace(FindCategory.TextureOrFlat, mixTexturesFlats: true));
        Assert.Equal(0, MapSearch.Replace(map, FindCategory.TextureOrFlat, "FLOOR4_8", "STONE1"));
        Assert.Equal("FLOOR4_8", map.Sectors[0].FloorTexture);
        Assert.Equal("FLOOR4_8", map.Sectors[1].CeilTexture);

        Assert.Equal(2, MapSearch.Replace(map, FindCategory.TextureOrFlat, "FLOOR4_8", "STONE1", withinSelection: false, mixTexturesFlats: true));

        Assert.Equal("STONE1", map.Sectors[0].FloorTexture);
        Assert.Equal("STONE1", map.Sectors[1].CeilTexture);
    }

    [Fact]
    public void NonReplaceableFindCategoriesReturnNoChanges()
    {
        var map = Build();
        map.Things[0].SetStringField("comment", "old");

        Assert.False(MapSearch.CanReplace(FindCategory.ThingIndex));
        Assert.False(MapSearch.CanReplace(FindCategory.ThingUdmfField));

        Assert.Equal(0, MapSearch.Replace(map, FindCategory.ThingIndex, "0", "1"));
        Assert.Equal(0, MapSearch.Replace(map, FindCategory.ThingUdmfField, "comment old", "comment new"));
        Assert.Equal(3001, map.Things[0].Type);
        Assert.Equal("old", map.Things[0].GetStringField("comment"));
    }

    [Fact]
    public void FindIndexCategoriesSelectSpecificElements()
    {
        var map = Build();

        Assert.Equal(1, MapSearch.Find(map, FindCategory.VertexIndex, "2").Count);
        Assert.True(map.Vertices[2].Selected);

        Assert.Equal(1, MapSearch.Find(map, FindCategory.LinedefIndex, "1").Count);
        Assert.True(map.Linedefs[1].Selected);

        Assert.Equal(1, MapSearch.Find(map, FindCategory.SidedefIndex, "1").Count);
        Assert.True(map.Sidedefs[1].Line.Selected);

        Assert.Equal(1, MapSearch.Find(map, FindCategory.SectorIndex, "1").Count);
        Assert.True(map.Sectors[1].Selected);

        Assert.Equal(1, MapSearch.Find(map, FindCategory.ThingIndex, "1").Count);
        Assert.True(map.Things[1].Selected);
    }

    [Fact]
    public void FindAndReplaceSectorHeightsAndBrightness()
    {
        var map = Build();
        map.Sectors[0].FloorHeight = -16;
        map.Sectors[1].FloorHeight = -16;
        map.Sectors[0].CeilHeight = 128;
        map.Sectors[1].Brightness = 192;

        Assert.Equal(2, MapSearch.Find(map, FindCategory.SectorFloorHeight, "-16").Count);
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.SectorCeilingHeight, "128", "160"));
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.SectorBrightness, "192", "224"));

        Assert.Equal(160, map.Sectors[0].CeilHeight);
        Assert.Equal(224, map.Sectors[1].Brightness);
    }

    [Theory]
    [InlineData("< 192", 1)]
    [InlineData("<= 192", 2)]
    [InlineData(">192", 0)]
    [InlineData(">=192", 1)]
    public void FindSectorBrightnessSupportsUdbComparisonPrefixes(string value, int expectedCount)
    {
        var map = Build();
        map.Sectors[0].Brightness = 128;
        map.Sectors[1].Brightness = 192;

        SearchResult result = MapSearch.Find(map, FindCategory.SectorBrightness, value);

        Assert.Equal(expectedCount, result.Count);
    }

    [Fact]
    public void ReplaceSectorBrightnessSupportsComparisonPrefixes()
    {
        var map = Build();
        map.Sectors[0].Brightness = 128;
        map.Sectors[1].Brightness = 192;

        int changed = MapSearch.Replace(map, FindCategory.SectorBrightness, "<= 192", "224");

        Assert.Equal(2, changed);
        Assert.Equal(224, map.Sectors[0].Brightness);
        Assert.Equal(224, map.Sectors[1].Brightness);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("256")]
    public void ReplaceSectorBrightnessRejectsInvalidReplacementValues(string replacement)
    {
        var map = Build();
        map.Sectors[0].Brightness = 128;

        int changed = MapSearch.Replace(map, FindCategory.SectorBrightness, "128", replacement);

        Assert.Equal(0, changed);
        Assert.Equal(128, map.Sectors[0].Brightness);
    }

    [Fact]
    public void FindAndReplaceSpecificTextureAndFlatSlots()
    {
        var map = Build();
        map.Sidedefs[0].HighTexture = "SUPPORT3";
        map.Sidedefs[1].LowTexture = "BROWN96";

        Assert.Equal(1, MapSearch.Find(map, FindCategory.SidedefUpperTexture, "support3").Count);
        Assert.Equal(4, MapSearch.Find(map, FindCategory.SidedefMiddleTexture, "startan3").Count);
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.SidedefLowerTexture, "brown96", "STONE2"));
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.SectorFloorFlat, "floor4_8", "FLAT1"));
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.SectorCeilingFlat, "floor4_8", "FLAT2"));

        Assert.Equal("STONE2", map.Sidedefs[1].LowTexture);
        Assert.Equal("FLAT1", map.Sectors[0].FloorTexture);
        Assert.Equal("FLAT2", map.Sectors[1].CeilTexture);
    }

    [Fact]
    public void FindAndReplaceThingAngle()
    {
        var map = Build();

        Assert.Equal(2, MapSearch.Find(map, FindCategory.ThingAngle, "90").Count);
        Assert.Equal(2, MapSearch.Replace(map, FindCategory.ThingAngle, "90", "270"));

        Assert.Equal(270, map.Things[0].Angle);
        Assert.Equal(270, map.Things[2].Angle);
        Assert.Equal(180, map.Things[1].Angle);
    }

    [Fact]
    public void ReplaceThingAngleNormalizesReplacementLikeUdb()
    {
        var map = Build();

        Assert.Equal(2, MapSearch.Replace(map, FindCategory.ThingAngle, "90", "450"));

        Assert.Equal(90, map.Things[0].Angle);
        Assert.Equal(90, map.Things[2].Angle);

        Assert.Equal(2, MapSearch.Replace(map, FindCategory.ThingAngle, "90", "-90"));

        Assert.Equal(270, map.Things[0].Angle);
        Assert.Equal(270, map.Things[2].Angle);
    }

    [Fact]
    public void FindAndReplaceThingAngleMatchCanonicalDoomAngleLikeUdb()
    {
        var map = Build();
        map.Things[0].Angle = 450;

        SearchResult found = MapSearch.Find(map, FindCategory.ThingAngle, "90");

        Assert.Equal(2, found.Count);
        Assert.True(map.Things[0].Selected);
        Assert.True(map.Things[2].Selected);
        Assert.Equal(2, MapSearch.Replace(map, FindCategory.ThingAngle, "90", "-90"));
        Assert.Equal(270, map.Things[0].Angle);
        Assert.Equal(270, map.Things[2].Angle);
    }

    [Fact]
    public void FindAndReplaceLinedefFlags()
    {
        var map = Build();
        map.Linedefs[0].SetFlag("blocking", true);
        map.Linedefs[0].SetFlag("playeruse", true);
        map.Linedefs[1].SetFlag("blocking", true);
        map.Linedefs[1].SetFlag("playeruse", false);

        SearchResult found = MapSearch.Find(map, FindCategory.LinedefFlags, "blocking, !playeruse");

        Assert.Equal(1, found.Count);
        Assert.True(map.Linedefs[1].Selected);
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.LinedefFlags, "blocking, !playeruse", "!blocking, monsteruse"));
        Assert.False(map.Linedefs[1].IsFlagSet("blocking"));
        Assert.True(map.Linedefs[1].IsFlagSet("monsteruse"));
        Assert.True(map.Linedefs[0].IsFlagSet("blocking"));
    }

    [Fact]
    public void FindAndReplaceSidedefFlags()
    {
        var map = Build();
        map.Sidedefs[0].SetFlag("clipmidtex", true);
        map.Sidedefs[1].SetFlag("clipmidtex", true);
        map.Sidedefs[1].SetFlag("wrapmidtex", true);

        SearchResult found = MapSearch.Find(map, FindCategory.SidedefFlags, "clipmidtex, !wrapmidtex");

        Assert.Equal(1, found.Count);
        Assert.True(map.Sidedefs[0].Line.Selected);
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.SidedefFlags, "clipmidtex, !wrapmidtex", "!clipmidtex, wrapmidtex"));
        Assert.False(map.Sidedefs[0].IsFlagSet("clipmidtex"));
        Assert.True(map.Sidedefs[0].IsFlagSet("wrapmidtex"));
        Assert.True(map.Sidedefs[1].IsFlagSet("clipmidtex"));
    }

    [Fact]
    public void FindAndReplaceSectorFlags()
    {
        var map = Build();
        map.Sectors[0].SetFlag("secret", true);
        map.Sectors[0].SetFlag("nofallingdamage", true);
        map.Sectors[1].SetFlag("secret", true);

        SearchResult found = MapSearch.Find(map, FindCategory.SectorFlags, "secret, !nofallingdamage");

        Assert.Equal(1, found.Count);
        Assert.True(map.Sectors[1].Selected);
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.SectorFlags, "secret, !nofallingdamage", "!secret, damagehazard"));
        Assert.False(map.Sectors[1].IsFlagSet("secret"));
        Assert.True(map.Sectors[1].IsFlagSet("damagehazard"));
        Assert.True(map.Sectors[0].IsFlagSet("secret"));
    }

    [Fact]
    public void FindAndReplaceThingFlags()
    {
        var map = Build();
        map.Things[0].SetFlag("ambush", true);
        map.Things[0].SetFlag("skill1", true);
        map.Things[1].SetFlag("ambush", true);

        SearchResult found = MapSearch.Find(map, FindCategory.ThingFlags, "ambush, !skill1");

        Assert.Equal(1, found.Count);
        Assert.True(map.Things[1].Selected);
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.ThingFlags, "ambush, !skill1", "!ambush, skill2"));
        Assert.False(map.Things[1].IsFlagSet("ambush"));
        Assert.True(map.Things[1].IsFlagSet("skill2"));
        Assert.True(map.Things[0].IsFlagSet("ambush"));
    }

    [Fact]
    public void FindUdmfFieldsSupportsKeyAndValueWildcards()
    {
        var map = Build();
        map.Sectors[0].Fields["lightcolor"] = 0x112233;
        map.Sectors[0].Fields["lightfloor"] = 24;
        map.Linedefs[1].Fields["comment"] = "door alpha";
        map.Sidedefs[2].Fields["comment"] = "door beta";
        map.Things[0].Fields["arg0str"] = "OpenDoor";
        map.Vertices[1].Fields["zfloor"] = 16;

        SearchResult allLights = MapSearch.Find(map, FindCategory.AnyUdmfField, "light*");
        Assert.Equal(2, allLights.Count);
        Assert.True(map.Sectors[0].Selected);

        SearchResult comments = MapSearch.Find(map, FindCategory.AnyUdmfField, "comment door*");
        Assert.Equal(2, comments.Count);
        Assert.True(map.Linedefs[1].Selected);
        Assert.True(map.Sidedefs[2].Line.Selected);

        SearchResult thingArg = MapSearch.Find(map, FindCategory.ThingUdmfField, "arg?str Open*");
        Assert.Equal(1, thingArg.Count);
        Assert.True(map.Things[0].Selected);

        SearchResult vertexHeight = MapSearch.Find(map, FindCategory.VertexUdmfField, "zfloor 16");
        Assert.Equal(1, vertexHeight.Count);
        Assert.True(map.Vertices[1].Selected);
    }

    [Fact]
    public void FindUdmfFieldCategoriesScopeToElementTypes()
    {
        var map = Build();
        map.Sectors[0].Fields["comment"] = "shared";
        map.Linedefs[0].Fields["comment"] = "shared";
        map.Sidedefs[0].Fields["comment"] = "shared";
        map.Things[0].Fields["comment"] = "shared";
        map.Vertices[0].Fields["comment"] = "shared";

        Assert.Equal(1, MapSearch.Find(map, FindCategory.SectorUdmfField, "comment shared").Count);
        Assert.True(map.Sectors[0].Selected);

        Assert.Equal(1, MapSearch.Find(map, FindCategory.LinedefUdmfField, "comment shared").Count);
        Assert.True(map.Linedefs[0].Selected);

        Assert.Equal(1, MapSearch.Find(map, FindCategory.SidedefUdmfField, "comment shared").Count);
        Assert.True(map.Sidedefs[0].Line.Selected);

        Assert.Equal(1, MapSearch.Find(map, FindCategory.ThingUdmfField, "comment shared").Count);
        Assert.True(map.Things[0].Selected);

        Assert.Equal(1, MapSearch.Find(map, FindCategory.VertexUdmfField, "comment shared").Count);
        Assert.True(map.Vertices[0].Selected);
    }

    [Fact]
    public void UsedTagsAggregatesAcrossTypesAscending()
    {
        var map = Build(); // tag 5 used by sector s1, linedef 0, thing[2]
        var tags = MapSearch.UsedTags(map);
        Assert.Single(tags);
        Assert.Equal(5, tags[0].Tag);
        Assert.Equal(3, tags[0].Count);

        map.Sectors[1].Tag = 2;
        var tags2 = MapSearch.UsedTags(map);
        Assert.Equal(new[] { 2, 5 }, tags2.ConvertAll(t => t.Tag)); // ascending
    }

    [Fact]
    public void UsedTagsIncludesMoreIds()
    {
        var map = Build();
        map.Sectors[0].Tags.Add(7);
        map.Linedefs[0].Tags.Add(7);
        map.Linedefs[1].Tag = 2;

        var tags = MapSearch.UsedTags(map);

        Assert.Equal(new[] { 2, 5, 7 }, tags.ConvertAll(t => t.Tag));
        Assert.Equal(3, tags.First(t => t.Tag == 5).Count);
        Assert.Equal(2, tags.First(t => t.Tag == 7).Count);
    }

    [Fact]
    public void UsedTagsHonorsFormatSpecificTagOwners()
    {
        var map = Build();

        var tags = MapSearch.UsedTags(map, new TagSearchOptions(IncludeLinedefs: false, IncludeThings: false));

        Assert.Single(tags);
        Assert.Equal(5, tags[0].Tag);
        Assert.Equal(1, tags[0].Count);
    }

    [Fact]
    public void TagWindowModelFormatsHeadersAndRows()
    {
        Assert.Equal("No tags in use.", TagWindowModel.TagListHeaderText(0));
        Assert.Equal("1 tag. Click to select its elements.", TagWindowModel.TagListHeaderText(1));
        Assert.Equal("2 tags. Click to select its elements.", TagWindowModel.TagListHeaderText(2));
        Assert.Equal("No tags in use.", TagWindowModel.TagStatisticsHeaderText(0));
        Assert.Equal("1 tag in use.", TagWindowModel.TagStatisticsHeaderText(1));
        Assert.Equal("2 tags in use.", TagWindowModel.TagStatisticsHeaderText(2));

        var labels = new Dictionary<int, string> { [7] = "Exit" };
        Assert.Equal("Tag 7 - Exit  (1 element)", TagWindowModel.TagListRowText(7, 1, labels));
        Assert.Equal("Tag 8  (2 elements)", TagWindowModel.TagListRowText(8, 2, labels));
        Assert.Equal("Tag 7: 1 element.", TagWindowModel.TagActivatedStatusText(7, 1));
        Assert.Equal("Tag 8: 2 elements.", TagWindowModel.TagActivatedStatusText(8, 2));
    }

    [Fact]
    public void TagStatisticsWindowModelBuildsRowsLikeUdbTagStatisticsForm()
    {
        var tags = new[]
        {
            new TagStatistic(7, Sectors: 1, Linedefs: 2, Things: 3),
            new TagStatistic(3, Sectors: 4, Linedefs: 0, Things: 1),
            new TagStatistic(5, Sectors: 0, Linedefs: 1, Things: 0),
        };
        var labels = new Dictionary<int, string>
        {
            [7] = "Exit",
            [3] = "  ",
        };

        IReadOnlyList<TagStatisticsRow> rows = TagWindowModel.BuildTagStatisticsRows(tags, labels);

        Assert.Equal(
            new[]
            {
                new TagStatisticsRow(3, "  ", 4, 0, 1),
                new TagStatisticsRow(5, "", 0, 1, 0),
                new TagStatisticsRow(7, "Exit", 1, 2, 3),
            },
            rows);
    }

    [Fact]
    public void UsedTagStatisticsSplitsCountsByElementType()
    {
        var map = Build();
        map.Sectors[0].Tags.Add(7);
        map.Linedefs[0].Tags.Add(7);
        map.Things[0].Tag = 7;

        var tags = MapSearch.UsedTagStatistics(map);

        Assert.Equal(new[] { 5, 7 }, tags.ConvertAll(t => t.Tag));
        var tag5 = tags.First(t => t.Tag == 5);
        Assert.Equal(1, tag5.Sectors);
        Assert.Equal(1, tag5.Linedefs);
        Assert.Equal(1, tag5.Things);
        Assert.Equal(3, tag5.Total);

        var tag7 = tags.First(t => t.Tag == 7);
        Assert.Equal(1, tag7.Sectors);
        Assert.Equal(1, tag7.Linedefs);
        Assert.Equal(1, tag7.Things);
    }

    [Fact]
    public void UsedTagStatisticsHonorsFormatSpecificTagOwners()
    {
        var map = Build();

        var tags = MapSearch.UsedTagStatistics(map, new TagSearchOptions(IncludeLinedefs: false, IncludeThings: false));

        var tag5 = Assert.Single(tags);
        Assert.Equal(5, tag5.Tag);
        Assert.Equal(1, tag5.Sectors);
        Assert.Equal(0, tag5.Linedefs);
        Assert.Equal(0, tag5.Things);
        Assert.Equal(1, tag5.Total);
    }

    [Fact]
    public void UsedTagStatisticsSkipsSectorAndLinedefMoreidsWhenPrimaryTagIsZero()
    {
        var map = Build();
        map.Sectors[0].Tags.Clear();
        map.Sectors[0].Tags.AddRange([0, 7]);
        map.Linedefs[0].Tags.Clear();
        map.Linedefs[0].Tags.AddRange([0, 7]);
        map.Things[0].Tag = 0;

        var tags = MapSearch.UsedTagStatistics(map);

        Assert.DoesNotContain(tags, tag => tag.Tag == 7);
    }

    [Fact]
    public void ThingTypeStatisticsCountsTypesAscending()
    {
        var map = Build();

        var types = MapSearch.ThingTypeStatistics(map);

        Assert.Equal(new[] { 9, 3001 }, types.ConvertAll(t => t.Type));
        Assert.Equal(1, types.First(t => t.Type == 9).Count);
        Assert.Equal(2, types.First(t => t.Type == 3001).Count);
    }

    [Fact]
    public void ThingStatisticsWindowModelFormatsHeaderCounts()
    {
        Assert.Equal("0 things, 0 type rows.", ThingStatisticsWindowModel.HeaderText(0, 0));
        Assert.Equal("1 thing, 1 type row.", ThingStatisticsWindowModel.HeaderText(1, 1));
        Assert.Equal("2 things, 3 type rows.", ThingStatisticsWindowModel.HeaderText(2, 3));
        Assert.Equal("Thing type 3001: 1 thing.", ThingStatisticsWindowModel.TypeActivatedStatusText(3001, 1));
        Assert.Equal("Thing type 3001: 2 things.", ThingStatisticsWindowModel.TypeActivatedStatusText(3001, 2));
    }

    [Fact]
    public void ThingStatisticsWindowModelBuildsRowsLikeUdbThingStatisticsForm()
    {
        var config = GameConfiguration.FromText("""
            thingtypes
            {
                monsters
                {
                    title = "Monsters";
                    3001 { title = "Imp"; class = "DoomImp"; }
                    3002 { title = "Demon"; }
                }
            }
            """);
        var used = new[]
        {
            new ThingTypeStatistic(3001, 2),
            new ThingTypeStatistic(9, 1),
        };

        IReadOnlyList<ThingStatisticsRow> allRows = ThingStatisticsWindowModel.BuildRows(used, config, hideUnused: false);
        IReadOnlyList<ThingStatisticsRow> usedRows = ThingStatisticsWindowModel.BuildRows(used, config, hideUnused: true);

        Assert.Equal(
            new[]
            {
                new ThingStatisticsRow(9, "Unknown thing", "-", 1),
                new ThingStatisticsRow(3001, "Imp", "DoomImp", 2),
                new ThingStatisticsRow(3002, "Demon", "", 0),
            },
            allRows);
        Assert.Equal(new[] { 9, 3001 }, usedRows.Select(row => row.Type).ToArray());
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
    public void NextFreeTagSkipsMoreIds()
    {
        var map = Build();
        map.Linedefs[0].Tags.AddRange(new[] { 1, 2, 3, 4 });

        Assert.Equal(6, MapSearch.NextFreeTag(map));
    }

    [Fact]
    public void MapSetNewTagCanScopeToElementOwnerType()
    {
        var map = Build();
        map.Sectors[1].Tag = 1;
        map.Linedefs[1].Tag = 1;
        map.Things[0].Tag = 1;
        map.Things[1].Tag = 2;

        Assert.Equal(2, map.GetNewTag(MapTagKind.Linedef));
        Assert.Equal(2, map.GetNewTag(MapTagKind.Sector));
        Assert.Equal(3, map.GetNewTag(MapTagKind.Thing));
    }

    [Fact]
    public void MapSetMultipleNewTagsCanScopeToMarkedGeometry()
    {
        var map = Build();
        map.Sectors[1].Tag = 1;
        map.Linedefs[1].Tag = 2;
        map.Things[0].Tag = 3;
        map.Sectors[1].Marked = true;
        map.Linedefs[1].Marked = false;
        map.Things[0].Marked = false;

        Assert.Equal(new[] { 2, 3, 4 }, map.GetMultipleNewTags(3, markedOnly: true));
        Assert.Equal(new[] { 4, 6 }, map.GetMultipleNewTags(2, markedOnly: false));
    }

    [Fact]
    public void ReplaceTagUpdatesMoreIds()
    {
        var map = Build();
        map.Sectors[0].Tags.Add(17);
        map.Linedefs[0].Tags.Add(17);

        int changed = MapSearch.Replace(map, FindCategory.Tag, "17", "19");

        Assert.Equal(2, changed);
        Assert.Equal(new[] { 5, 19 }, map.Sectors[0].Tags);
        Assert.Equal(new[] { 5, 19 }, map.Linedefs[0].Tags);
    }

    [Fact]
    public void ReplaceSpecificTagCategoriesTouchOnlyTheirElementTypes()
    {
        var map = Build();

        Assert.Equal(1, MapSearch.Replace(map, FindCategory.LinedefTag, "5", "11"));
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.SectorTag, "5", "12"));
        Assert.Equal(1, MapSearch.Replace(map, FindCategory.ThingTag, "5", "13"));

        Assert.Equal(11, map.Linedefs[0].Tag);
        Assert.Equal(12, map.Sectors[0].Tag);
        Assert.Equal(13, map.Things[2].Tag);
    }

    [Fact]
    public void ReplaceTagHonorsFormatSpecificTagOwners()
    {
        var map = Build();

        int changed = MapSearch.Replace(
            map,
            FindCategory.Tag,
            "5",
            "8",
            new TagSearchOptions(IncludeLinedefs: false, IncludeThings: false));

        Assert.Equal(1, changed);
        Assert.Equal(8, map.Sectors[0].Tag);
        Assert.Equal(5, map.Linedefs[0].Tag);
        Assert.Equal(5, map.Things[2].Tag);
    }

    [Fact]
    public void ReplaceWithNonNumericValueDoesNothing()
    {
        var map = Build();
        Assert.Equal(0, MapSearch.Replace(map, FindCategory.ThingType, "3001", "notanumber"));
        Assert.Equal(2, map.Things.Count(t => t.Type == 3001)); // unchanged
    }
}
