// ABOUTME: Verifies UDB-style CommentsPanel grouping over UDMF comment fields.
// ABOUTME: Covers object-group filtering, exact comment keys, and set/remove helpers.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class CommentsPanelModelTests
{
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
}
