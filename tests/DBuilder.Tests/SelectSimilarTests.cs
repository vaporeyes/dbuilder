// ABOUTME: Tests UDB-style select-similar property matching for map elements.
// ABOUTME: Covers vertex, linedef, sector and thing matching with property option flags.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SelectSimilarTests
{
    [Fact]
    public void SelectThingsMatchesAnySelectedSourceByEnabledProperties()
    {
        var map = new MapSet();
        var source = map.AddThing(new Vector2D(0, 0), 3001);
        source.Selected = true;
        source.Angle = 90;
        source.Tag = 7;
        source.Flags = 1;
        source.UdmfFlags.Add("ambush");
        source.Fields["comment"] = "guard";

        var match = map.AddThing(new Vector2D(64, 0), 3001);
        match.Angle = 90;
        match.Tag = 7;
        match.Flags = 1;
        match.UdmfFlags.Add("ambush");
        match.Fields["comment"] = "guard";

        var differentComment = map.AddThing(new Vector2D(128, 0), 3001);
        differentComment.Angle = 90;
        differentComment.Tag = 7;
        differentComment.Flags = 1;
        differentComment.UdmfFlags.Add("ambush");
        differentComment.Fields["comment"] = "patrol";

        Assert.Equal(1, SelectSimilar.SelectThings(map));
        Assert.True(match.Selected);
        Assert.False(differentComment.Selected);
    }

    [Fact]
    public void SelectThingsCanMatchConversationIndependentlyFromCustomFields()
    {
        var map = new MapSet();
        var source = map.AddThing(new Vector2D(0, 0), 3001);
        source.Selected = true;
        source.Fields["conversation"] = 5;
        source.Fields["comment"] = "alpha";

        var sameConversation = map.AddThing(new Vector2D(64, 0), 3001);
        sameConversation.Fields["conversation"] = 5;
        sameConversation.Fields["comment"] = "beta";

        var differentConversation = map.AddThing(new Vector2D(128, 0), 3001);
        differentConversation.Fields["conversation"] = 7;
        differentConversation.Fields["comment"] = "alpha";

        var options = new ThingSimilarityOptions { Fields = false };

        Assert.Equal(1, SelectSimilar.SelectThings(map, options));
        Assert.True(sameConversation.Selected);
        Assert.False(differentConversation.Selected);
    }

    [Fact]
    public void SelectThingsCanIgnoreConversationWhenDisabled()
    {
        var map = new MapSet();
        var source = map.AddThing(new Vector2D(0, 0), 3001);
        source.Selected = true;
        source.Fields["conversation"] = 5;

        var target = map.AddThing(new Vector2D(64, 0), 3001);
        target.Fields["conversation"] = 7;

        var options = new ThingSimilarityOptions { Conversation = false, Fields = false };

        Assert.Equal(1, SelectSimilar.SelectThings(map, options));
        Assert.True(target.Selected);
    }

    [Fact]
    public void SelectSectorsCanIgnoreDisabledProperties()
    {
        var map = new MapSet();
        var source = map.AddSector();
        source.Selected = true;
        source.FloorHeight = 0;
        source.CeilHeight = 128;
        source.FloorTexture = "FLOOR4_8";
        source.CeilTexture = "CEIL1_1";
        source.Brightness = 160;
        source.Special = 9;
        source.Tags.AddRange(new[] { 5, 17 });

        var match = map.AddSector();
        match.FloorHeight = 0;
        match.CeilHeight = 128;
        match.FloorTexture = "floor4_8";
        match.CeilTexture = "CEIL1_1";
        match.Brightness = 192;
        match.Special = 9;
        match.Tags.AddRange(new[] { 17, 5 });

        var differentTexture = map.AddSector();
        differentTexture.FloorHeight = 0;
        differentTexture.CeilHeight = 128;
        differentTexture.FloorTexture = "NUKAGE1";
        differentTexture.CeilTexture = "CEIL1_1";
        differentTexture.Brightness = 192;
        differentTexture.Special = 9;
        differentTexture.Tags.AddRange(new[] { 17, 5 });

        var options = new SectorSimilarityOptions { Brightness = false };

        Assert.Equal(1, SelectSimilar.SelectSectors(map, options));
        Assert.True(match.Selected);
        Assert.False(differentTexture.Selected);
    }

    [Fact]
    public void SelectLinedefsMatchesLinedefAndAnySidedefPair()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var source = AddLine(map, sector, new Vector2D(0, 0), new Vector2D(64, 0), "STARTAN3");
        source.Selected = true;
        source.Action = 80;
        source.Args[0] = 12;
        source.Tags.Add(7);
        source.Front!.OffsetX = 16;

        var reversedSideMatch = AddLine(map, sector, new Vector2D(0, 64), new Vector2D(64, 64), "-");
        reversedSideMatch.Action = 80;
        reversedSideMatch.Args[0] = 12;
        reversedSideMatch.Tags.Add(7);
        var back = map.AddSidedef(reversedSideMatch, isFront: false, sector);
        back.MidTexture = "startan3";
        back.OffsetX = 16;

        var differentArg = AddLine(map, sector, new Vector2D(0, 128), new Vector2D(64, 128), "STARTAN3");
        differentArg.Action = 80;
        differentArg.Args[0] = 13;
        differentArg.Tags.Add(7);
        differentArg.Front!.OffsetX = 16;

        map.BuildIndexes();

        Assert.Equal(1, SelectSimilar.SelectLinedefs(map));
        Assert.True(reversedSideMatch.Selected);
        Assert.False(differentArg.Selected);
    }

    [Fact]
    public void SelectVerticesMatchesUdmfHeightsAndCustomFields()
    {
        var map = new MapSet();
        var source = map.AddVertex(new Vector2D(0, 0));
        source.Selected = true;
        source.ZFloor = 8;
        source.ZCeiling = 120;
        source.Fields["comment"] = "ridge";

        var match = map.AddVertex(new Vector2D(64, 0));
        match.ZFloor = 8;
        match.ZCeiling = 120;
        match.Fields["comment"] = "ridge";

        var differentHeight = map.AddVertex(new Vector2D(128, 0));
        differentHeight.ZFloor = 16;
        differentHeight.ZCeiling = 120;
        differentHeight.Fields["comment"] = "ridge";

        Assert.Equal(1, SelectSimilar.SelectVertices(map));
        Assert.True(match.Selected);
        Assert.False(differentHeight.Selected);
    }

    [Fact]
    public void SelectSimilarDialogPersistsOptionsBetweenOpensLikeUdb()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SelectSimilarDialog.cs"));

        Assert.Contains("private static VertexSimilarityOptions SavedVertexOptions { get; set; } = new();", body, StringComparison.Ordinal);
        Assert.Contains("_vertexZFloor = AddCheckBox(\"Vertex floor height\", SavedVertexOptions.ZFloor);", body, StringComparison.Ordinal);
        Assert.Contains("_linedefAction = AddCheckBox(\"Action\", SavedLinedefOptions.Action);", body, StringComparison.Ordinal);
        Assert.Contains("_sidedefUpperTexture = AddCheckBox(\"Upper texture\", SavedSidedefOptions.UpperTexture);", body, StringComparison.Ordinal);
        Assert.Contains("_thingType = AddCheckBox(\"Type\", SavedThingOptions.Type);", body, StringComparison.Ordinal);
        Assert.Contains("_thingConversation = AddCheckBox(\"Conversation ID\", SavedThingOptions.Conversation);", body, StringComparison.Ordinal);
        Assert.Contains("SavedVertexOptions = VertexOptions;", body, StringComparison.Ordinal);
        Assert.Contains("SavedSectorOptions = SectorOptions;", body, StringComparison.Ordinal);
        Assert.Contains("SavedLinedefOptions = LinedefOptions;", body, StringComparison.Ordinal);
        Assert.Contains("SavedSidedefOptions = SidedefOptions;", body, StringComparison.Ordinal);
        Assert.Contains("SavedThingOptions = ThingOptions;", body, StringComparison.Ordinal);
    }

    private static Linedef AddLine(MapSet map, Sector sector, Vector2D start, Vector2D end, string middleTexture)
    {
        var line = map.AddLinedef(map.AddVertex(start), map.AddVertex(end));
        var side = map.AddSidedef(line, isFront: true, sector);
        side.MidTexture = middleTexture;
        return line;
    }
}
