// ABOUTME: Verifies UDB-style CommentsPanel grouping over UDMF comment fields.
// ABOUTME: Covers object-group filtering, exact comment keys, and set/remove helpers.

using DBuilder.Geometry;
using DBuilder.Map;
using System.Drawing;

namespace DBuilder.Tests;

public sealed class CommentsPanelModelTests
{
    [Fact]
    public void SettingsUseUdbPluginKeysAndFalseDefaults()
    {
        CommentsPanelPersistedSettings defaults = CommentsPanelModel.ReadSettings(
            new Dictionary<string, object?>());
        CommentsPanelPersistedSettings configured = CommentsPanelModel.ReadSettings(
            new Dictionary<string, object?>
            {
                [CommentsPanelModel.FilterModeSettingKey] = "true",
                [CommentsPanelModel.ClickSelectsSettingKey] = true,
            });
        IReadOnlyDictionary<string, object> written = CommentsPanelModel.WriteSettings(configured);

        Assert.False(defaults.FilterMode);
        Assert.False(defaults.ClickSelects);
        Assert.True(configured.FilterMode);
        Assert.True(configured.ClickSelects);
        Assert.Equal("filtermode", CommentsPanelModel.FilterModeSettingKey);
        Assert.Equal("clickselects", CommentsPanelModel.ClickSelectsSettingKey);
        Assert.True((bool)written[CommentsPanelModel.FilterModeSettingKey]);
        Assert.True((bool)written[CommentsPanelModel.ClickSelectsSettingKey]);
    }

    [Fact]
    public void EffectiveFilterModeUsesCurrentModeOnlyWhenFilterModeIsEnabled()
    {
        Assert.Equal(
            CommentsPanelMode.Linedefs,
            CommentsPanelModel.EffectiveFilterMode(
                new CommentsPanelPersistedSettings(FilterMode: false, ClickSelects: true),
                CommentsPanelMode.Things,
                CommentsPanelMode.Linedefs));
        Assert.Equal(
            CommentsPanelMode.Things,
            CommentsPanelModel.EffectiveFilterMode(
                new CommentsPanelPersistedSettings(FilterMode: true, ClickSelects: false),
                CommentsPanelMode.Things,
                CommentsPanelMode.Linedefs));
    }

    [Fact]
    public void BuildGroupsSeparatesSameCommentByObjectGroup()
    {
        var map = new MapSet();
        var vertex = map.AddVertex(new Vector2D(0, 0));
        var line = map.AddLinedef(vertex, map.AddVertex(new Vector2D(64, 0)));
        var sector = map.AddSector();
        var thing = map.AddThing(new Vector2D(16, 16), 1);

        vertex.Fields["comment"] = "shared";
        line.Fields["comment"] = "shared";
        sector.Fields["comment"] = "shared";
        thing.Fields["comment"] = "shared";

        var groups = CommentsPanelModel.BuildGroups(map);

        Assert.Equal(
            new[] { CommentsPanelMode.Vertices, CommentsPanelMode.Linedefs, CommentsPanelMode.Sectors, CommentsPanelMode.Things },
            groups.Select(g => g.Group));
        Assert.All(groups, g => Assert.Equal("shared", g.Comment));
        Assert.All(groups, g => Assert.Single(g.Elements));
    }

    [Fact]
    public void BuildGroupsIncludesSidedefsInLinedefGroup()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        var front = map.AddSidedef(line, true, map.AddSector());

        line.Fields["comment"] = "line";
        front.Fields["comment"] = "line";

        var group = Assert.Single(CommentsPanelModel.BuildGroups(map));

        Assert.Equal(CommentsPanelMode.Linedefs, group.Group);
        Assert.Equal("line", group.Comment);
        Assert.Equal(new[] { CommentedElementKind.Linedef, CommentedElementKind.Sidedef }, group.Elements.Select(e => e.Kind));
    }

    [Fact]
    public void BuildGroupsFiltersToCurrentMode()
    {
        var map = new MapSet();
        map.AddVertex(new Vector2D(0, 0)).Fields["comment"] = "vertex";
        map.AddThing(new Vector2D(8, 8), 1).Fields["comment"] = "thing";

        var groups = CommentsPanelModel.BuildGroups(map, CommentsPanelMode.Things);

        var group = Assert.Single(groups);
        Assert.Equal(CommentsPanelMode.Things, group.Group);
        Assert.Equal("thing", group.Comment);
        Assert.Equal(CommentedElementKind.Thing, Assert.Single(group.Elements).Kind);
    }

    [Fact]
    public void BuildGroupsSortsByCommentOrdinal()
    {
        var map = new MapSet();
        map.AddThing(new Vector2D(0, 0), 1).Fields["comment"] = "beta";
        map.AddThing(new Vector2D(8, 8), 1).Fields["comment"] = "Alpha";
        map.AddThing(new Vector2D(16, 16), 1).Fields["comment"] = "alpha";

        var groups = CommentsPanelModel.BuildGroups(map);

        Assert.Equal(new[] { "Alpha", "alpha", "beta" }, groups.Select(g => g.Comment));
    }

    [Fact]
    public void BuildGroupsFiltersByCommentTextAndGroupName()
    {
        var map = new MapSet();
        map.AddVertex(new Vector2D(0, 0)).Fields["comment"] = "spawn marker";
        map.AddSector().Fields["comment"] = "secret room";
        map.AddThing(new Vector2D(8, 8), 1).Fields["comment"] = "patrol point";

        var byComment = CommentsPanelModel.BuildGroups(map, searchText: "secret");
        var byGroup = CommentsPanelModel.BuildGroups(map, searchText: "things");

        Assert.Single(byComment);
        Assert.Equal("secret room", byComment[0].Comment);
        Assert.Single(byGroup);
        Assert.Equal(CommentsPanelMode.Things, byGroup[0].Group);
    }

    [Fact]
    public void SetAndRemoveCommentUpdatesElementFields()
    {
        var map = new MapSet();
        var first = map.AddVertex(new Vector2D(0, 0));
        var second = map.AddVertex(new Vector2D(8, 8));

        CommentsPanelModel.SetComment(new IFielded[] { first, second }, "note");

        var group = Assert.Single(CommentsPanelModel.BuildGroups(map, CommentsPanelMode.Vertices));
        Assert.Equal(2, group.Elements.Count);

        CommentsPanelModel.RemoveComment(group);

        Assert.DoesNotContain(CommentsPanelModel.CommentField, first.Fields.Keys);
        Assert.DoesNotContain(CommentsPanelModel.CommentField, second.Fields.Keys);
    }

    [Theory]
    [InlineData(CommentsPanelMode.All, 1, false)]
    [InlineData(CommentsPanelMode.Vertices, 0, false)]
    [InlineData(CommentsPanelMode.Vertices, 1, true)]
    [InlineData(CommentsPanelMode.Linedefs, 2, true)]
    [InlineData(CommentsPanelMode.Sectors, 3, true)]
    [InlineData(CommentsPanelMode.Things, 4, true)]
    public void CanSetSelectionCommentMatchesUdbModeRules(CommentsPanelMode mode, int count, bool expected)
        => Assert.Equal(expected, CommentsPanelModel.CanSetSelectionComment(mode, count));

    [Fact]
    public void CreateSelectionTargetUsesFirstCommentedElementKind()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        var side = map.AddSidedef(line, true, map.AddSector());

        line.Fields["comment"] = "line";
        side.Fields["comment"] = "line";

        var group = Assert.Single(CommentsPanelModel.BuildGroups(map));
        var target = CommentsPanelModel.CreateSelectionTarget(group);

        Assert.Equal(CommentsPanelMode.Linedefs, target.Mode);
        Assert.Equal(new IFielded[] { line, side }, target.Elements);
    }

    [Fact]
    public void CreateSelectionTargetAddsSectorBoundaryLinedefsLikeUdb()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(64, 0));
        var c = map.AddVertex(new Vector2D(64, 64));
        var ab = map.AddLinedef(a, b);
        var bc = map.AddLinedef(b, c);
        map.AddSidedef(ab, true, sector);
        map.AddSidedef(bc, true, sector);
        map.BuildIndexes();
        sector.Fields["comment"] = "sector";

        var group = Assert.Single(CommentsPanelModel.BuildGroups(map));
        var target = CommentsPanelModel.CreateSelectionTarget(group);

        Assert.Equal(CommentsPanelMode.Sectors, target.Mode);
        Assert.Equal(new IFielded[] { sector, ab, bc }, target.Elements);
    }

    [Fact]
    public void CreateEditTargetConvertsSidedefsToOwningLinedefs()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        var side = map.AddSidedef(line, true, map.AddSector());

        side.Fields["comment"] = "side";

        var group = Assert.Single(CommentsPanelModel.BuildGroups(map));
        var target = CommentsPanelModel.CreateEditTarget(group);

        Assert.Equal(CommentsPanelMode.Linedefs, target.Mode);
        Assert.Equal(new IFielded[] { line }, target.Elements);
    }

    [Fact]
    public void CreateEditTargetKeepsElementOrderAndDuplicatesLikeUdb()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        var side = map.AddSidedef(line, true, map.AddSector());

        line.Fields["comment"] = "line";
        side.Fields["comment"] = "line";

        var group = Assert.Single(CommentsPanelModel.BuildGroups(map));
        var target = CommentsPanelModel.CreateEditTarget(group);

        Assert.Equal(new IFielded[] { line, line }, target.Elements);
    }

    [Fact]
    public void CreateViewAreaSquaresAndPadsLineBounds()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(200, 40)));
        line.Fields["comment"] = "line";

        var group = Assert.Single(CommentsPanelModel.BuildGroups(map));
        var area = CommentsPanelModel.CreateViewArea(group);

        Assert.Equal(-100f, area.X);
        Assert.Equal(-180f, area.Y);
        Assert.Equal(400f, area.Width);
        Assert.Equal(400f, area.Height);
    }

    [Fact]
    public void CreateViewAreaUsesThingDisplaySize()
    {
        var map = new MapSet();
        var thing = map.AddThing(new Vector2D(50, 80), 1);
        thing.Size = 16;
        thing.Fields["comment"] = "thing";

        var group = Assert.Single(CommentsPanelModel.BuildGroups(map));
        var area = CommentsPanelModel.CreateViewArea(group);

        Assert.Equal(-82f, area.X);
        Assert.Equal(-52f, area.Y);
        Assert.Equal(264f, area.Width);
        Assert.Equal(264f, area.Height);
    }

    [Fact]
    public void CreateViewAreaUsesSectorSidedefs()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(64, 0));
        var c = map.AddVertex(new Vector2D(64, 96));
        var ab = map.AddLinedef(a, b);
        var bc = map.AddLinedef(b, c);
        map.AddSidedef(ab, true, sector);
        map.AddSidedef(bc, true, sector);
        map.BuildIndexes();
        sector.Fields["comment"] = "sector";

        var group = Assert.Single(CommentsPanelModel.BuildGroups(map));
        var area = CommentsPanelModel.CreateViewArea(group);

        Assert.Equal(-116f, area.X);
        Assert.Equal(-100f, area.Y);
        Assert.Equal(296f, area.Width);
        Assert.Equal(296f, area.Height);
    }

    [Fact]
    public void TryGetCommentResolvesIconPrefixAndTooltipLikeUdb()
    {
        var map = new MapSet();
        var thing = map.AddThing(new Vector2D(0, 0), 1);
        thing.Fields["comment"] = "[!]Problem marker";

        bool found = CommentsPanelModel.TryGetComment(
            thing,
            out string comment,
            out CommentIconKind icon,
            out string tooltip);

        Assert.True(found);
        Assert.Equal("[!]Problem marker", comment);
        Assert.Equal(CommentIconKind.Problem, icon);
        Assert.Equal("Problem marker", tooltip);
    }

    [Fact]
    public void BuildRenderIconsHonorsUdmfAndToggleGates()
    {
        var map = new MapSet();
        map.AddThing(new Vector2D(0, 0), 1).Fields["comment"] = "note";

        Assert.Empty(CommentsPanelModel.BuildRenderIcons(
            map,
            new CommentRenderOptions(CommentsPanelMode.Things, IsUdmf: false)));
        Assert.Empty(CommentsPanelModel.BuildRenderIcons(
            map,
            new CommentRenderOptions(CommentsPanelMode.Things, RenderComments: false)));
        Assert.Empty(CommentsPanelModel.BuildRenderIcons(
            map,
            new CommentRenderOptions(CommentsPanelMode.Vertices)));
    }

    [Fact]
    public void LinedefRenderIconUsesUdbRectangleAndSelectionColor()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        line.Selected = true;
        line.Fields["comment"] = "[i]Line info";

        var icon = Assert.Single(CommentsPanelModel.BuildRenderIcons(
            map,
            new CommentRenderOptions(CommentsPanelMode.Linedefs, Scale: 2.0)));

        Assert.Equal(CommentedElementKind.Linedef, icon.Kind);
        Assert.Same(line, icon.Element);
        Assert.Equal(new RectangleF(28, 9, 8, -8), icon.Rectangle);
        Assert.Equal(CommentIconKind.Info, icon.Icon);
        Assert.Equal(CommentIconColorRole.Selection, icon.Color);
        Assert.Equal("Line info", icon.TooltipText);
    }

    [Fact]
    public void HighlightedLinedefRenderIconUsesHighlightColor()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        line.Fields["comment"] = "plain";

        var icon = Assert.Single(CommentsPanelModel.BuildRenderIcons(
            map,
            new CommentRenderOptions(CommentsPanelMode.Linedefs, Highlighted: line)));

        Assert.Equal(CommentIconColorRole.Highlight, icon.Color);
        Assert.Equal(CommentIconKind.Regular, icon.Icon);
        Assert.Equal("plain", icon.TooltipText);
    }

    [Fact]
    public void SectorRenderIconSkipsSelectedSectorsAndUsesLabelPositionsWhenAvailable()
    {
        var map = new MapSet();
        Sector selected = AddSquareSector(map, 0, 64);
        Sector sector = AddSquareSector(map, 128, 64);
        selected.Selected = true;
        selected.Fields["comment"] = "hidden";
        sector.Fields["comment"] = "[?]Sector question";
        var labels = new Dictionary<Sector, IReadOnlyList<LabelPositionInfo>>
        {
            [sector] = new[] { new LabelPositionInfo(new Vector2D(140, 160), 8), new LabelPositionInfo(new Vector2D(156, 176), 8) },
        };

        var icons = CommentsPanelModel.BuildRenderIcons(
            map,
            new CommentRenderOptions(CommentsPanelMode.Sectors, Scale: 4.0, SectorLabels: labels));

        Assert.Equal(2, icons.Count);
        Assert.All(icons, icon => Assert.Same(sector, icon.Element));
        Assert.Equal(new RectangleF(138, 162, 4, -4), icons[0].Rectangle);
        Assert.Equal(new RectangleF(154, 178, 4, -4), icons[1].Rectangle);
        Assert.All(icons, icon => Assert.Equal(CommentIconKind.Question, icon.Icon));
        Assert.All(icons, icon => Assert.Equal("Sector question", icon.TooltipText));
    }

    [Fact]
    public void SectorRenderIconFallsBackToSectorBoundsCenter()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64);
        sector.Fields["comment"] = "sector";

        var icon = Assert.Single(CommentsPanelModel.BuildRenderIcons(
            map,
            new CommentRenderOptions(CommentsPanelMode.Sectors, Scale: 2.0)));

        Assert.Equal(new RectangleF(28, 36, 8, -8), icon.Rectangle);
        Assert.Equal(CommentIconColorRole.White, icon.Color);
    }

    [Fact]
    public void ThingRenderIconUsesUdbSizeAndScaleRules()
    {
        var map = new MapSet();
        var small = map.AddThing(new Vector2D(0, 0), 1);
        small.Size = 0.3;
        small.Fields["comment"] = "small";
        var thing = map.AddThing(new Vector2D(100, 200), 1);
        thing.Size = 16;
        thing.FixedSize = true;
        thing.Selected = true;
        thing.Fields["comment"] = "[:]:)";

        var icons = CommentsPanelModel.BuildRenderIcons(
            map,
            new CommentRenderOptions(CommentsPanelMode.Things, Scale: 4.0));

        var icon = Assert.Single(icons);
        Assert.Same(thing, icon.Element);
        Assert.Equal(new RectangleF(101.5f, 208.5f, 4, -4), icon.Rectangle);
        Assert.Equal(CommentIconKind.Smile, icon.Icon);
        Assert.Equal(CommentIconColorRole.Selection, icon.Color);
        Assert.Equal(")", icon.TooltipText);
    }

    private static Sector AddSquareSector(MapSet map, double origin, double size)
    {
        Sector sector = map.AddSector();
        Vertex a = map.AddVertex(new Vector2D(origin, origin));
        Vertex b = map.AddVertex(new Vector2D(origin + size, origin));
        Vertex c = map.AddVertex(new Vector2D(origin + size, origin + size));
        Vertex d = map.AddVertex(new Vector2D(origin, origin + size));

        map.AddSidedef(map.AddLinedef(a, b), true, sector);
        map.AddSidedef(map.AddLinedef(b, c), true, sector);
        map.AddSidedef(map.AddLinedef(c, d), true, sector);
        map.AddSidedef(map.AddLinedef(d, a), true, sector);
        map.BuildIndexes();
        return sector;
    }
}
