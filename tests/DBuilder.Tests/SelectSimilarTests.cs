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

        var options = new ThingSimilarityOptions { Comment = false, Fields = false };

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
    public void SelectThingsMatchesUdbManagedFieldsIndependentlyFromCustomFields()
    {
        var map = new MapSet();
        var source = map.AddThing(new Vector2D(0, 0), 3001);
        source.Selected = true;
        source.Fields["gravity"] = 0.5;
        source.Fields["health"] = 2;
        source.Fields["score"] = 1000;
        source.Fields["floatbobphase"] = 4;
        source.Fields["alpha"] = 0.75;
        source.Fields["fillcolor"] = 0x336699;
        source.Fields["renderstyle"] = "Translucent";
        source.Fields["comment"] = "guard";
        source.Fields["species"] = "imp";

        var match = map.AddThing(new Vector2D(64, 0), 3001);
        match.Fields["gravity"] = 0.5;
        match.Fields["health"] = 2;
        match.Fields["score"] = 1000;
        match.Fields["floatbobphase"] = 4;
        match.Fields["alpha"] = 0.75;
        match.Fields["fillcolor"] = 0x336699;
        match.Fields["renderstyle"] = "translucent";
        match.Fields["comment"] = "guard";
        match.Fields["species"] = "demon";

        var differentGravity = map.AddThing(new Vector2D(128, 0), 3001);
        differentGravity.Fields["gravity"] = 1.0;
        differentGravity.Fields["health"] = 2;
        differentGravity.Fields["score"] = 1000;
        differentGravity.Fields["floatbobphase"] = 4;
        differentGravity.Fields["alpha"] = 0.75;
        differentGravity.Fields["fillcolor"] = 0x336699;
        differentGravity.Fields["renderstyle"] = "Translucent";
        differentGravity.Fields["comment"] = "guard";
        differentGravity.Fields["species"] = "imp";

        var options = new ThingSimilarityOptions { Fields = false };

        Assert.Equal(1, SelectSimilar.SelectThings(map, options));
        Assert.True(match.Selected);
        Assert.False(differentGravity.Selected);
    }

    [Fact]
    public void SelectThingsCanIgnoreUdbManagedFieldsWhenDisabled()
    {
        var map = new MapSet();
        var source = map.AddThing(new Vector2D(0, 0), 3001);
        source.Selected = true;
        source.Fields["gravity"] = 0.5;
        source.Fields["comment"] = "guard";

        var target = map.AddThing(new Vector2D(64, 0), 3001);
        target.Fields["gravity"] = 1.0;
        target.Fields["comment"] = "patrol";

        var options = new ThingSimilarityOptions
        {
            Gravity = false,
            Comment = false,
            Fields = false,
        };

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
    public void SelectSectorsMatchesUdbManagedFieldsIndependentlyFromCustomFields()
    {
        var map = new MapSet();
        var source = map.AddSector();
        source.Selected = true;
        SetSectorManagedFields(source);
        source.Fields["portal"] = "blue";

        var match = map.AddSector();
        SetSectorManagedFields(match);
        match.Fields["portal"] = "red";

        var differentGravity = map.AddSector();
        SetSectorManagedFields(differentGravity);
        differentGravity.Fields["gravity"] = 0.75;
        differentGravity.Fields["portal"] = "blue";

        var options = new SectorSimilarityOptions { Fields = false };

        Assert.Equal(1, SelectSimilar.SelectSectors(map, options));
        Assert.True(match.Selected);
        Assert.False(differentGravity.Selected);
    }

    [Fact]
    public void SelectSectorsCanIgnoreUdbManagedFieldsWhenDisabled()
    {
        var map = new MapSet();
        var source = map.AddSector();
        source.Selected = true;
        source.Fields["lightcolor"] = 0x335577;
        source.Fields["comment"] = "yard";

        var target = map.AddSector();
        target.Fields["lightcolor"] = 0x775533;
        target.Fields["comment"] = "hall";

        var options = new SectorSimilarityOptions
        {
            LightColor = false,
            Comment = false,
            Fields = false,
        };

        Assert.Equal(1, SelectSimilar.SelectSectors(map, options));
        Assert.True(target.Selected);
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
    public void SelectLinedefsMatchesUdbManagedFieldsIndependentlyFromCustomFields()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var source = AddLine(map, sector, new Vector2D(0, 0), new Vector2D(64, 0), "STARTAN3");
        source.Selected = true;
        source.Fields["alpha"] = 0.5;
        source.Fields["renderstyle"] = "Translucent";
        source.Fields["locknumber"] = 3;
        source.Fields["comment"] = "door";
        source.Fields["portal"] = "blue";

        var match = AddLine(map, sector, new Vector2D(0, 64), new Vector2D(64, 64), "STARTAN3");
        match.Fields["alpha"] = 0.5;
        match.Fields["renderstyle"] = "translucent";
        match.Fields["locknumber"] = 3;
        match.Fields["comment"] = "door";
        match.Fields["portal"] = "red";

        var differentAlpha = AddLine(map, sector, new Vector2D(0, 128), new Vector2D(64, 128), "STARTAN3");
        differentAlpha.Fields["alpha"] = 1.0;
        differentAlpha.Fields["renderstyle"] = "Translucent";
        differentAlpha.Fields["locknumber"] = 3;
        differentAlpha.Fields["comment"] = "door";
        differentAlpha.Fields["portal"] = "blue";

        var linedefOptions = new LinedefSimilarityOptions { Fields = false };

        Assert.Equal(1, SelectSimilar.SelectLinedefs(map, linedefOptions));
        Assert.True(match.Selected);
        Assert.False(differentAlpha.Selected);
    }

    [Fact]
    public void SelectLinedefsCanIgnoreUdbManagedFieldsWhenDisabled()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var source = AddLine(map, sector, new Vector2D(0, 0), new Vector2D(64, 0), "STARTAN3");
        source.Selected = true;
        source.Fields["alpha"] = 0.5;
        source.Fields["comment"] = "door";

        var target = AddLine(map, sector, new Vector2D(0, 64), new Vector2D(64, 64), "STARTAN3");
        target.Fields["alpha"] = 1.0;
        target.Fields["comment"] = "window";

        var linedefOptions = new LinedefSimilarityOptions
        {
            Alpha = false,
            Comment = false,
            Fields = false,
        };

        Assert.Equal(1, SelectSimilar.SelectLinedefs(map, linedefOptions));
        Assert.True(target.Selected);
    }

    [Fact]
    public void SelectLinedefsMatchesUdbManagedSidedefFieldsIndependentlyFromCustomFields()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var source = AddLine(map, sector, new Vector2D(0, 0), new Vector2D(64, 0), "STARTAN3");
        source.Selected = true;
        SetSidedefManagedFields(source.Front!);
        source.Front!.Fields["portal"] = "blue";

        var match = AddLine(map, sector, new Vector2D(0, 64), new Vector2D(64, 64), "STARTAN3");
        SetSidedefManagedFields(match.Front!);
        match.Front!.Fields["portal"] = "red";

        var differentScale = AddLine(map, sector, new Vector2D(0, 128), new Vector2D(64, 128), "STARTAN3");
        SetSidedefManagedFields(differentScale.Front!);
        differentScale.Front!.Fields["scalex_mid"] = 2.0;
        differentScale.Front!.Fields["portal"] = "blue";

        var sideOptions = new SidedefSimilarityOptions { Fields = false };

        Assert.Equal(1, SelectSimilar.SelectLinedefs(map, sidedefOptions: sideOptions));
        Assert.True(match.Selected);
        Assert.False(differentScale.Selected);
    }

    [Fact]
    public void SelectLinedefsCanIgnoreUdbManagedSidedefFieldsWhenDisabled()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var source = AddLine(map, sector, new Vector2D(0, 0), new Vector2D(64, 0), "STARTAN3");
        source.Selected = true;
        source.Front!.Fields["light"] = 160;
        source.Front!.Fields["lightabsolute"] = false;

        var target = AddLine(map, sector, new Vector2D(0, 64), new Vector2D(64, 64), "STARTAN3");
        target.Front!.Fields["light"] = 192;
        target.Front!.Fields["lightabsolute"] = true;

        var sideOptions = new SidedefSimilarityOptions
        {
            Brightness = false,
            Fields = false,
        };

        Assert.Equal(1, SelectSimilar.SelectLinedefs(map, sidedefOptions: sideOptions));
        Assert.True(target.Selected);
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
        Assert.Contains("public SelectSimilarDialog(MapControl.EditMode mode, MapFormat mapFormat)", body, StringComparison.Ordinal);
        Assert.Contains("_vertexZFloor = AddUdmfCheckBox(\"Vertex floor height\", SavedVertexOptions.ZFloor);", body, StringComparison.Ordinal);
        Assert.Contains("_sectorFloorTextureOffsets = AddUdmfCheckBox(\"Floor texture offsets\", SavedSectorOptions.FloorTextureOffsets);", body, StringComparison.Ordinal);
        Assert.Contains("_sectorFloorBrightness = AddUdmfCheckBox(\"Floor brightness\", SavedSectorOptions.FloorBrightness);", body, StringComparison.Ordinal);
        Assert.Contains("_sectorFloorGlow = AddUdmfCheckBox(\"Floor glow\", SavedSectorOptions.FloorGlow);", body, StringComparison.Ordinal);
        Assert.Contains("_sectorComment = AddUdmfCheckBox(\"Comment\", SavedSectorOptions.Comment);", body, StringComparison.Ordinal);
        Assert.Contains("_linedefAction = AddCheckBox(\"Action\", SavedLinedefOptions.Action);", body, StringComparison.Ordinal);
        Assert.Contains("_linedefArguments = AddSupportedCheckBox(\"Action arguments\", SavedLinedefOptions.Arguments, doom: false);", body, StringComparison.Ordinal);
        Assert.Contains("_linedefActivation = AddSupportedCheckBox(\"Activation\", SavedLinedefOptions.Activation, doom: false, udmf: false);", body, StringComparison.Ordinal);
        Assert.Contains("_linedefAlpha = AddUdmfCheckBox(\"Alpha\", SavedLinedefOptions.Alpha);", body, StringComparison.Ordinal);
        Assert.Contains("_linedefRenderStyle = AddUdmfCheckBox(\"Render style\", SavedLinedefOptions.RenderStyle);", body, StringComparison.Ordinal);
        Assert.Contains("_linedefLockNumber = AddUdmfCheckBox(\"Lock number\", SavedLinedefOptions.LockNumber);", body, StringComparison.Ordinal);
        Assert.Contains("_linedefComment = AddUdmfCheckBox(\"Comment\", SavedLinedefOptions.Comment);", body, StringComparison.Ordinal);
        Assert.Contains("_sidedefUpperTexture = AddCheckBox(\"Upper texture\", SavedSidedefOptions.UpperTexture);", body, StringComparison.Ordinal);
        Assert.Contains("_sidedefUpperTextureOffsets = AddUdmfCheckBox(\"Upper texture offsets\", SavedSidedefOptions.UpperTextureOffsets);", body, StringComparison.Ordinal);
        Assert.Contains("_sidedefMiddleTextureScale = AddUdmfCheckBox(\"Middle texture scale\", SavedSidedefOptions.MiddleTextureScale);", body, StringComparison.Ordinal);
        Assert.Contains("_sidedefBrightness = AddUdmfCheckBox(\"Brightness\", SavedSidedefOptions.Brightness);", body, StringComparison.Ordinal);
        Assert.Contains("_thingType = AddCheckBox(\"Type\", SavedThingOptions.Type);", body, StringComparison.Ordinal);
        Assert.Contains("_thingHeight = AddSupportedCheckBox(\"Z-height\", SavedThingOptions.Height, doom: false);", body, StringComparison.Ordinal);
        Assert.Contains("_thingConversation = AddUdmfCheckBox(\"Conversation ID\", SavedThingOptions.Conversation);", body, StringComparison.Ordinal);
        Assert.Contains("_thingGravity = AddUdmfCheckBox(\"Gravity\", SavedThingOptions.Gravity);", body, StringComparison.Ordinal);
        Assert.Contains("_thingHealth = AddUdmfCheckBox(\"Health multiplier\", SavedThingOptions.Health);", body, StringComparison.Ordinal);
        Assert.Contains("_thingScore = AddUdmfCheckBox(\"Score\", SavedThingOptions.Score);", body, StringComparison.Ordinal);
        Assert.Contains("_thingFloatBobPhase = AddUdmfCheckBox(\"Float bob phase\", SavedThingOptions.FloatBobPhase);", body, StringComparison.Ordinal);
        Assert.Contains("_thingAlpha = AddUdmfCheckBox(\"Alpha\", SavedThingOptions.Alpha);", body, StringComparison.Ordinal);
        Assert.Contains("_thingFillColor = AddUdmfCheckBox(\"Fill color\", SavedThingOptions.FillColor);", body, StringComparison.Ordinal);
        Assert.Contains("_thingRenderStyle = AddUdmfCheckBox(\"Render style\", SavedThingOptions.RenderStyle);", body, StringComparison.Ordinal);
        Assert.Contains("_thingComment = AddUdmfCheckBox(\"Comment\", SavedThingOptions.Comment);", body, StringComparison.Ordinal);
        Assert.Contains("Content = \"Enable All\"", body, StringComparison.Ordinal);
        Assert.Contains("private void ToggleVisibleChecks()", body, StringComparison.Ordinal);
        Assert.Contains("bool enable = _visibleChecks[0].IsChecked != true;", body, StringComparison.Ordinal);
        Assert.Contains("foreach (CheckBox check in _visibleChecks)", body, StringComparison.Ordinal);
        Assert.Contains("_visibleChecks.Add(check);", body, StringComparison.Ordinal);
        Assert.Contains("private CheckBox? AddUdmfCheckBox(string label, bool isChecked)", body, StringComparison.Ordinal);
        Assert.Contains("private bool SupportsCurrentMapFormat(bool doom, bool hexen, bool udmf)", body, StringComparison.Ordinal);
        Assert.Contains("SavedVertexOptions = VertexOptions;", body, StringComparison.Ordinal);
        Assert.Contains("SavedSectorOptions = SectorOptions;", body, StringComparison.Ordinal);
        Assert.Contains("SavedLinedefOptions = LinedefOptions;", body, StringComparison.Ordinal);
        Assert.Contains("SavedSidedefOptions = SidedefOptions;", body, StringComparison.Ordinal);
        Assert.Contains("SavedThingOptions = ThingOptions;", body, StringComparison.Ordinal);

        string mainWindowBody = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        Assert.Contains("var dlg = new SelectSimilarDialog(MapView.CurrentEditMode, _mapFormat);", mainWindowBody, StringComparison.Ordinal);
    }

    private static Linedef AddLine(MapSet map, Sector sector, Vector2D start, Vector2D end, string middleTexture)
    {
        var line = map.AddLinedef(map.AddVertex(start), map.AddVertex(end));
        var side = map.AddSidedef(line, isFront: true, sector);
        side.MidTexture = middleTexture;
        return line;
    }

    private static void SetSectorManagedFields(Sector sector)
    {
        sector.Fields["xpanningfloor"] = 1;
        sector.Fields["ypanningfloor"] = 2;
        sector.Fields["xpanningceiling"] = 3;
        sector.Fields["ypanningceiling"] = 4;
        sector.Fields["xscalefloor"] = 1.0;
        sector.Fields["yscalefloor"] = 1.5;
        sector.Fields["xscaleceiling"] = 0.75;
        sector.Fields["yscaleceiling"] = 0.5;
        sector.Fields["rotationfloor"] = 45;
        sector.Fields["rotationceiling"] = 90;
        sector.Fields["alphafloor"] = 0.5;
        sector.Fields["alphaceiling"] = 0.75;
        sector.Fields["portal_floor_alpha"] = 0.25;
        sector.Fields["portal_ceil_alpha"] = 0.35;
        sector.Fields["lightfloor"] = 144;
        sector.Fields["lightfloorabsolute"] = true;
        sector.Fields["lightceiling"] = 176;
        sector.Fields["lightceilingabsolute"] = false;
        sector.Fields["renderstylefloor"] = "Translucent";
        sector.Fields["renderstyleceiling"] = "Add";
        sector.Fields["portal_floor_overlaytype"] = "Alpha";
        sector.Fields["portal_ceil_overlaytype"] = "Add";
        sector.Fields["floorterrain"] = "mud";
        sector.Fields["ceilingterrain"] = "stone";
        sector.Fields["lightcolor"] = 0x336699;
        sector.Fields["fadecolor"] = 0x112233;
        sector.Fields["color_floor"] = 0x445566;
        sector.Fields["color_ceiling"] = 0x665544;
        sector.Fields["color_walltop"] = 0x123456;
        sector.Fields["color_wallbottom"] = 0x654321;
        sector.Fields["color_sprites"] = 0x888888;
        sector.Fields["floorglowcolor"] = 0x00ff00;
        sector.Fields["floorglowheight"] = 24;
        sector.Fields["ceilingglowcolor"] = 0xff0000;
        sector.Fields["ceilingglowheight"] = 32;
        sector.Fields["fogdensity"] = 64;
        sector.Fields["desaturation"] = 0.25;
        sector.Fields["damagetype"] = "Fire";
        sector.Fields["damageamount"] = 5;
        sector.Fields["damageinterval"] = 32;
        sector.Fields["leakiness"] = 128;
        sector.Fields["soundsequence"] = "Water";
        sector.Fields["gravity"] = 0.5;
        sector.Fields["comment"] = "yard";
    }

    private static void SetSidedefManagedFields(Sidedef side)
    {
        side.Fields["offsetx_top"] = 1;
        side.Fields["offsety_top"] = 2;
        side.Fields["offsetx_mid"] = 3;
        side.Fields["offsety_mid"] = 4;
        side.Fields["offsetx_bottom"] = 5;
        side.Fields["offsety_bottom"] = 6;
        side.Fields["scalex_top"] = 1.0;
        side.Fields["scaley_top"] = 1.5;
        side.Fields["scalex_mid"] = 1.25;
        side.Fields["scaley_mid"] = 1.75;
        side.Fields["scalex_bottom"] = 0.75;
        side.Fields["scaley_bottom"] = 0.5;
        side.Fields["light"] = 160;
        side.Fields["lightabsolute"] = false;
    }
}
