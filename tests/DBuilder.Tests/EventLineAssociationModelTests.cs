// ABOUTME: Tests event-line association planning for UDB-compatible tagged links.
// ABOUTME: Covers Doom-format linedef tags that target sectors when configured.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class EventLineAssociationModelTests
{
    [Fact]
    public void LineTagIndicatesSectorsLinksLinedefTagsToSectorTags()
    {
        var map = new MapSet();
        Linedef line = AddLine(map, 7);
        Sector target = map.AddSector();
        target.Tag = 7;
        Sector other = map.AddSector();
        other.Tag = 8;
        GameConfiguration config = Config(lineTagIndicatesSectors: true);

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, line, config);

        EventLineAssociation association = Assert.Single(associations);
        Assert.Equal(EventLineElementKind.Linedef, association.SourceKind);
        Assert.Equal(line.Index, association.SourceIndex);
        Assert.Equal(EventLineElementKind.Sector, association.TargetKind);
        Assert.Equal(target.Index, association.TargetIndex);
        Assert.Equal(7, association.Tag);
    }

    [Fact]
    public void LineTagIndicatesSectorsLinksSectorTagsBackToLinedefTags()
    {
        var map = new MapSet();
        Linedef first = AddLine(map, 3);
        Linedef second = AddLine(map, 5);
        Sector sector = map.AddSector();
        sector.Tags.AddRange(new[] { 5, 9 });
        GameConfiguration config = Config(lineTagIndicatesSectors: true);

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, sector, config);

        EventLineAssociation association = Assert.Single(associations);
        Assert.Equal(EventLineElementKind.Sector, association.SourceKind);
        Assert.Equal(sector.Index, association.SourceIndex);
        Assert.Equal(EventLineElementKind.Linedef, association.TargetKind);
        Assert.Equal(second.Index, association.TargetIndex);
        Assert.Equal(5, association.Tag);
        Assert.DoesNotContain(associations, a => a.TargetIndex == first.Index);
    }

    [Fact]
    public void LineTagIndicatesSectorsIgnoresTagZero()
    {
        var map = new MapSet();
        Linedef line = AddLine(map, 0);
        Sector sector = map.AddSector();
        sector.Tag = 0;
        GameConfiguration config = Config(lineTagIndicatesSectors: true);

        Assert.Empty(EventLineAssociationModel.ForElement(map, line, config));
        Assert.Empty(EventLineAssociationModel.ForElement(map, sector, config));
    }

    [Fact]
    public void DisabledLineTagIndicatesSectorsDoesNotCreateDoomTagAssociations()
    {
        var map = new MapSet();
        Linedef line = AddLine(map, 7);
        Sector sector = map.AddSector();
        sector.Tag = 7;
        GameConfiguration config = Config(lineTagIndicatesSectors: false);

        Assert.Empty(EventLineAssociationModel.ForElement(map, line, config));
        Assert.Empty(EventLineAssociationModel.ForElement(map, sector, config));
    }

    private static Linedef AddLine(MapSet map, int tag)
    {
        Vertex start = map.AddVertex(new Vector2D(0, 0));
        Vertex end = map.AddVertex(new Vector2D(64, 0));
        Linedef line = map.AddLinedef(start, end);
        line.Tag = tag;
        return line;
    }

    private static GameConfiguration Config(bool lineTagIndicatesSectors)
        => GameConfiguration.FromText(
            lineTagIndicatesSectors
                ? "linetagindicatesectors = true;"
                : "linetagindicatesectors = false;");
}
