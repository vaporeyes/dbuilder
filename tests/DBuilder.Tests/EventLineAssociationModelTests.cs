// ABOUTME: Tests event-line association planning for UDB-compatible tagged links.
// ABOUTME: Covers Doom-format linedef tags that target sectors when configured.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class EventLineAssociationModelTests
{
    [Fact]
    public void SectorFieldAssociationsLinkMatchingCustomFields()
    {
        var map = new MapSet();
        Sector source = map.AddSector();
        source.Fields["portal"] = 7;
        Sector target = map.AddSector();
        target.Fields["portal"] = 7;
        Sector other = map.AddSector();
        other.Fields["portal"] = 8;
        GameConfiguration config = SectorFieldAssociationConfig();

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);

        EventLineAssociation association = Assert.Single(associations);
        Assert.Equal(EventLineElementKind.Sector, association.SourceKind);
        Assert.Equal(source.Index, association.SourceIndex);
        Assert.Equal(EventLineElementKind.Sector, association.TargetKind);
        Assert.Equal(target.Index, association.TargetIndex);
        Assert.Equal(7, association.Tag);
        Assert.DoesNotContain(associations, a => a.TargetIndex == other.Index);
    }

    [Fact]
    public void SectorFieldAssociationsHonorAbsoluteModifierAndDefaultSuppression()
    {
        var map = new MapSet();
        Sector source = map.AddSector();
        source.Fields["portal"] = -7;
        Sector target = map.AddSector();
        target.Fields["portal"] = 7;
        Sector defaultSource = map.AddSector();
        defaultSource.Fields["portal"] = 0;
        Sector defaultTarget = map.AddSector();
        defaultTarget.Fields["portal"] = 0;
        GameConfiguration config = SectorFieldAssociationConfig(modify: "abs");

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);
        IReadOnlyList<EventLineAssociation> defaultAssociations =
            EventLineAssociationModel.ForElement(map, defaultSource, config);

        EventLineAssociation association = Assert.Single(associations);
        Assert.Equal(target.Index, association.TargetIndex);
        Assert.Equal(7, association.Tag);
        Assert.Empty(defaultAssociations);
        Assert.DoesNotContain(associations, a => a.TargetIndex == defaultTarget.Index);
    }

    [Fact]
    public void SectorFieldAssociationsSkipNeverShowEventLines()
    {
        var map = new MapSet();
        Sector source = map.AddSector();
        source.Fields["portal"] = 7;
        Sector target = map.AddSector();
        target.Fields["portal"] = 7;
        GameConfiguration config = SectorFieldAssociationConfig(neverShowEventLines: true);

        Assert.Empty(EventLineAssociationModel.ForElement(map, source, config));
    }

    [Fact]
    public void LinedefFieldAssociationsLinkToMatchingSectors()
    {
        var map = new MapSet();
        Linedef source = AddLine(map, 0);
        source.Fields["portal"] = 7;
        Sector target = map.AddSector();
        target.Fields["portal"] = 7;
        Sector other = map.AddSector();
        other.Fields["portal"] = 8;
        GameConfiguration config = SectorFieldAssociationConfig();

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);

        EventLineAssociation association = Assert.Single(associations);
        Assert.Equal(EventLineElementKind.Linedef, association.SourceKind);
        Assert.Equal(source.Index, association.SourceIndex);
        Assert.Equal(EventLineElementKind.Sector, association.TargetKind);
        Assert.Equal(target.Index, association.TargetIndex);
        Assert.Equal(7, association.Tag);
        Assert.DoesNotContain(associations, a => a.TargetIndex == other.Index);
    }

    [Fact]
    public void ThingFieldAssociationsLinkToMatchingSectors()
    {
        var map = new MapSet();
        Thing source = map.AddThing(new Vector2D(0, 0), 3001);
        source.Fields["portal"] = 7;
        Sector target = map.AddSector();
        target.Fields["portal"] = 7;
        Sector other = map.AddSector();
        other.Fields["portal"] = 8;
        GameConfiguration config = SectorFieldAssociationConfig();

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);

        EventLineAssociation association = Assert.Single(associations);
        Assert.Equal(EventLineElementKind.Thing, association.SourceKind);
        Assert.Equal(source.Index, association.SourceIndex);
        Assert.Equal(EventLineElementKind.Sector, association.TargetKind);
        Assert.Equal(target.Index, association.TargetIndex);
        Assert.Equal(7, association.Tag);
        Assert.DoesNotContain(associations, a => a.TargetIndex == other.Index);
    }

    [Fact]
    public void SectorAssociationsIncludeLinedefsAndThingsReferencingSectorTags()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        sector.Tags.AddRange(new[] { 0, 7 });
        Linedef line = AddLine(map, 0);
        line.Action = 80;
        line.Args[0] = 7;
        Thing thing = map.AddThing(new Vector2D(32, 32), 3001);
        thing.Action = 80;
        thing.Args[0] = 7;
        GameConfiguration config = Config(actionArgTargets: true);

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, sector, config);

        Assert.Contains(associations, a =>
            a.SourceKind == EventLineElementKind.Sector &&
            a.SourceIndex == sector.Index &&
            a.TargetKind == EventLineElementKind.Linedef &&
            a.TargetIndex == line.Index &&
            a.Tag == 7);
        Assert.Contains(associations, a =>
            a.TargetKind == EventLineElementKind.Thing &&
            a.TargetIndex == thing.Index &&
            a.Tag == 7);
    }

    [Fact]
    public void LinedefAssociationsIncludeLinedefsAndThingsReferencingLinedefTags()
    {
        var map = new MapSet();
        Linedef source = AddLine(map, 19);
        Linedef line = AddLine(map, 0);
        line.Action = 80;
        line.Args[2] = 19;
        Thing thing = map.AddThing(new Vector2D(32, 32), 3001);
        thing.Action = 80;
        thing.Args[2] = 19;
        GameConfiguration config = Config(actionArgTargets: true);

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);

        Assert.Contains(associations, a =>
            a.SourceKind == EventLineElementKind.Linedef &&
            a.SourceIndex == source.Index &&
            a.TargetKind == EventLineElementKind.Linedef &&
            a.TargetIndex == line.Index &&
            a.Tag == 19);
        Assert.Contains(associations, a =>
            a.TargetKind == EventLineElementKind.Thing &&
            a.TargetIndex == thing.Index &&
            a.Tag == 19);
    }

    [Fact]
    public void ThingAssociationsIncludeLinedefsAndThingsReferencingThingTag()
    {
        var map = new MapSet();
        Thing source = map.AddThing(new Vector2D(0, 0), 3001);
        source.Tag = 9;
        Linedef line = AddLine(map, 0);
        line.Action = 80;
        line.Args[1] = 9;
        Thing thing = map.AddThing(new Vector2D(32, 32), 3002);
        thing.Action = 80;
        thing.Args[1] = 9;
        GameConfiguration config = Config(actionArgTargets: true);

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);

        Assert.Contains(associations, a =>
            a.SourceKind == EventLineElementKind.Thing &&
            a.SourceIndex == source.Index &&
            a.TargetKind == EventLineElementKind.Linedef &&
            a.TargetIndex == line.Index &&
            a.Tag == 9);
        Assert.Contains(associations, a =>
            a.TargetKind == EventLineElementKind.Thing &&
            a.TargetIndex == thing.Index &&
            a.Tag == 9);
    }

    [Fact]
    public void LinedefActionArgsLinkToTaggedSectorsLinedefsAndThings()
    {
        var map = new MapSet();
        Linedef source = AddLine(map, 0);
        source.Action = 80;
        source.Args[0] = 7;
        source.Args[1] = 9;
        source.Args[2] = 11;
        Sector sector = map.AddSector();
        sector.Tag = 7;
        Thing thing = map.AddThing(new Vector2D(32, 32), 3001);
        thing.Tag = 9;
        Linedef targetLine = AddLine(map, 11);
        GameConfiguration config = Config(actionArgTargets: true);

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);

        Assert.Contains(associations, a =>
            a.TargetKind == EventLineElementKind.Sector &&
            a.TargetIndex == sector.Index &&
            a.Tag == 7);
        Assert.Contains(associations, a =>
            a.TargetKind == EventLineElementKind.Thing &&
            a.TargetIndex == thing.Index &&
            a.Tag == 9);
        Assert.Contains(associations, a =>
            a.TargetKind == EventLineElementKind.Linedef &&
            a.TargetIndex == targetLine.Index &&
            a.Tag == 11);
    }

    [Fact]
    public void ThingActionArgsLinkToTaggedTargetsLikeUdb()
    {
        var map = new MapSet();
        Thing source = map.AddThing(new Vector2D(0, 0), 3001);
        source.Action = 80;
        source.Args[0] = 7;
        source.Args[1] = 9;
        source.Args[2] = 11;
        Sector sector = map.AddSector();
        sector.Tag = 7;
        Thing targetThing = map.AddThing(new Vector2D(32, 32), 3002);
        targetThing.Tag = 9;
        Linedef targetLine = AddLine(map, 11);
        GameConfiguration config = Config(actionArgTargets: true);

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);

        Assert.Contains(associations, a =>
            a.SourceKind == EventLineElementKind.Thing &&
            a.SourceIndex == source.Index &&
            a.TargetKind == EventLineElementKind.Sector &&
            a.TargetIndex == sector.Index);
        Assert.Contains(associations, a =>
            a.TargetKind == EventLineElementKind.Thing &&
            a.TargetIndex == targetThing.Index);
        Assert.Contains(associations, a =>
            a.TargetKind == EventLineElementKind.Linedef &&
            a.TargetIndex == targetLine.Index);
    }

    [Fact]
    public void ThingTypeArgsLinkToTaggedTargetsWhenThingHasNoAction()
    {
        var map = new MapSet();
        Thing source = map.AddThing(new Vector2D(0, 0), 9001);
        source.Args[0] = 7;
        source.Args[1] = 9;
        source.Args[2] = 11;
        Sector sector = map.AddSector();
        sector.Tag = 7;
        Thing targetThing = map.AddThing(new Vector2D(32, 32), 3002);
        targetThing.Tag = 9;
        Linedef targetLine = AddLine(map, 11);
        GameConfiguration config = ThingTypeArgConfig();

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);

        Assert.Contains(associations, a =>
            a.TargetKind == EventLineElementKind.Sector &&
            a.TargetIndex == sector.Index);
        Assert.Contains(associations, a =>
            a.TargetKind == EventLineElementKind.Thing &&
            a.TargetIndex == targetThing.Index);
        Assert.Contains(associations, a =>
            a.TargetKind == EventLineElementKind.Linedef &&
            a.TargetIndex == targetLine.Index);
    }

    [Fact]
    public void ThingDirectLinksMatchTargetTypeAndTagLikeUdb()
    {
        var map = new MapSet();
        Thing source = map.AddThing(new Vector2D(0, 0), 9001);
        source.Tag = 7;
        Thing target = map.AddThing(new Vector2D(32, 0), 9002);
        target.Tag = 7;
        Thing wrongType = map.AddThing(new Vector2D(64, 0), 3002);
        wrongType.Tag = 7;
        Thing wrongTag = map.AddThing(new Vector2D(96, 0), 9002);
        wrongTag.Tag = 8;
        GameConfiguration config = ThingTypeArgConfig(thingLink: 9002);

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);

        EventLineAssociation association = Assert.Single(associations);
        Assert.Equal(EventLineElementKind.Thing, association.SourceKind);
        Assert.Equal(source.Index, association.SourceIndex);
        Assert.Equal(EventLineElementKind.Thing, association.TargetKind);
        Assert.Equal(target.Index, association.TargetIndex);
        Assert.Equal(7, association.Tag);
        Assert.DoesNotContain(associations, a => a.TargetIndex == wrongType.Index);
        Assert.DoesNotContain(associations, a => a.TargetIndex == wrongTag.Index);
    }

    [Fact]
    public void ThingDirectLinksIgnoreZeroAndChildLinks()
    {
        var map = new MapSet();
        Thing zeroTag = map.AddThing(new Vector2D(0, 0), 9001);
        Thing target = map.AddThing(new Vector2D(32, 0), 9002);
        Thing childLink = map.AddThing(new Vector2D(64, 0), 9001);
        childLink.Tag = 7;
        target.Tag = 7;

        Assert.Empty(EventLineAssociationModel.ForElement(
            map,
            zeroTag,
            ThingTypeArgConfig(thingLink: 9002)));
        Assert.Empty(EventLineAssociationModel.ForElement(
            map,
            childLink,
            ThingTypeArgConfig(thingLink: -9002)));
    }

    [Fact]
    public void ThingTypeArgsAreSkippedForChildLinksAndSelfLinksLikeUdb()
    {
        var map = new MapSet();
        Thing childLink = map.AddThing(new Vector2D(0, 0), 9001);
        childLink.Args[0] = 7;
        Thing selfLink = map.AddThing(new Vector2D(16, 0), 9002);
        selfLink.Args[0] = 7;
        Sector sector = map.AddSector();
        sector.Tag = 7;

        Assert.Empty(EventLineAssociationModel.ForElement(
            map,
            childLink,
            ThingTypeArgConfig(thingLink: -3002)));
        Assert.Empty(EventLineAssociationModel.ForElement(
            map,
            selfLink,
            ThingTypeArgConfig(selfLinkType: 9002)));
    }

    [Fact]
    public void ReverseAssociationsIncludeThingTypeArgs()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        sector.Tag = 7;
        Thing source = map.AddThing(new Vector2D(0, 0), 9001);
        source.Args[0] = 7;
        GameConfiguration config = ThingTypeArgConfig();

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, sector, config);

        EventLineAssociation association = Assert.Single(associations);
        Assert.Equal(EventLineElementKind.Sector, association.SourceKind);
        Assert.Equal(sector.Index, association.SourceIndex);
        Assert.Equal(EventLineElementKind.Thing, association.TargetKind);
        Assert.Equal(source.Index, association.TargetIndex);
        Assert.Equal(7, association.Tag);
    }

    [Fact]
    public void ActionArgAssociationsIgnoreZeroUnknownAndDuplicateArgs()
    {
        var map = new MapSet();
        Linedef source = AddLine(map, 0);
        source.Action = 80;
        source.Args[0] = 7;
        source.Args[1] = 0;
        source.Args[2] = 7;
        source.Args[3] = 99;
        Sector sector = map.AddSector();
        sector.Tag = 7;
        Thing thing = map.AddThing(new Vector2D(32, 32), 3001);
        thing.Tag = 99;
        GameConfiguration config = Config(actionArgTargets: true, duplicateSectorArg: true);

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);

        EventLineAssociation association = Assert.Single(associations);
        Assert.Equal(EventLineElementKind.Sector, association.TargetKind);
        Assert.Equal(sector.Index, association.TargetIndex);
        Assert.DoesNotContain(associations, a =>
            a.TargetKind == EventLineElementKind.Thing &&
            a.TargetIndex == thing.Index);
    }

    [Fact]
    public void ActionArgAssociationsMatchSectorAndLinedefMoreIds()
    {
        var map = new MapSet();
        Linedef source = AddLine(map, 0);
        source.Action = 80;
        source.Args[0] = 17;
        source.Args[2] = 19;
        Sector sector = map.AddSector();
        sector.Tags.AddRange(new[] { 0, 17 });
        Linedef line = AddLine(map, 0);
        line.Tags.Add(19);
        GameConfiguration config = Config(actionArgTargets: true);

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);

        Assert.Contains(associations, a =>
            a.TargetKind == EventLineElementKind.Sector &&
            a.TargetIndex == sector.Index &&
            a.Tag == 17);
        Assert.Contains(associations, a =>
            a.TargetKind == EventLineElementKind.Linedef &&
            a.TargetIndex == line.Index &&
            a.Tag == 19);
    }

    [Fact]
    public void LineTagIndicatesSectorsSkipsActionArgAssociationsLikeUdb()
    {
        var map = new MapSet();
        Linedef source = AddLine(map, 7);
        source.Action = 80;
        source.Args[0] = 13;
        Sector sectorFromLineTag = map.AddSector();
        sectorFromLineTag.Tag = 7;
        Sector sectorFromArg = map.AddSector();
        sectorFromArg.Tag = 13;
        GameConfiguration config = Config(
            lineTagIndicatesSectors: true,
            actionArgTargets: true);

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);

        EventLineAssociation association = Assert.Single(associations);
        Assert.Equal(sectorFromLineTag.Index, association.TargetIndex);
        Assert.DoesNotContain(associations, a => a.TargetIndex == sectorFromArg.Index);
    }

    [Fact]
    public void LineToLineTagLinksLinedefTagsToTaggedLinedefs()
    {
        var map = new MapSet();
        Linedef source = AddLine(map, 11);
        source.Action = 80;
        Linedef target = AddLine(map, 11);
        Linedef other = AddLine(map, 12);
        GameConfiguration config = Config(lineToLineTag: true);

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);

        EventLineAssociation association = Assert.Single(associations);
        Assert.Equal(EventLineElementKind.Linedef, association.SourceKind);
        Assert.Equal(source.Index, association.SourceIndex);
        Assert.Equal(EventLineElementKind.Linedef, association.TargetKind);
        Assert.Equal(target.Index, association.TargetIndex);
        Assert.Equal(11, association.Tag);
        Assert.DoesNotContain(associations, a => a.TargetIndex == other.Index);
    }

    [Fact]
    public void LineToLineSameActionRequiresMatchingActionLikeUdb()
    {
        var map = new MapSet();
        Linedef source = AddLine(map, 11);
        source.Action = 80;
        Linedef sameAction = AddLine(map, 11);
        sameAction.Action = 80;
        Linedef differentAction = AddLine(map, 11);
        differentAction.Action = 81;
        GameConfiguration config = Config(lineToLineTag: true, lineToLineSameAction: true);

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);

        EventLineAssociation association = Assert.Single(associations);
        Assert.Equal(sameAction.Index, association.TargetIndex);
        Assert.DoesNotContain(associations, a => a.TargetIndex == differentAction.Index);
    }

    [Fact]
    public void LineToLineTagIgnoresSelfAndTagZero()
    {
        var map = new MapSet();
        Linedef source = AddLine(map, 0);
        source.Action = 80;
        AddLine(map, 0);
        GameConfiguration config = Config(lineToLineTag: true);

        Assert.Empty(EventLineAssociationModel.ForElement(map, source, config));
    }

    [Fact]
    public void LinedefAssociationsIncludeLineToLineAndDoomSectorTargets()
    {
        var map = new MapSet();
        Linedef source = AddLine(map, 7);
        source.Action = 80;
        Linedef targetLine = AddLine(map, 7);
        Sector targetSector = map.AddSector();
        targetSector.Tag = 7;
        GameConfiguration config = Config(lineToLineTag: true, lineTagIndicatesSectors: true);

        IReadOnlyList<EventLineAssociation> associations =
            EventLineAssociationModel.ForElement(map, source, config);

        Assert.Contains(associations, a =>
            a.TargetKind == EventLineElementKind.Linedef &&
            a.TargetIndex == targetLine.Index);
        Assert.Contains(associations, a =>
            a.TargetKind == EventLineElementKind.Sector &&
            a.TargetIndex == targetSector.Index);
    }

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

    private static GameConfiguration Config(
        bool lineTagIndicatesSectors = false,
        bool lineToLineTag = false,
        bool lineToLineSameAction = false,
        bool actionArgTargets = false,
        bool duplicateSectorArg = false)
    {
        string lineToLineTagValue = lineToLineTag ? "true" : "false";
        string lineToLineSameActionValue = lineToLineSameAction ? "true" : "false";
        string lineTagIndicatesSectorsValue = lineTagIndicatesSectors ? "true" : "false";
        string arg0Type = actionArgTargets ? ((int)UniversalType.SectorTag).ToString() : "0";
        string arg1Type = actionArgTargets ? ((int)UniversalType.ThingTag).ToString() : "0";
        string arg2Type = actionArgTargets
            ? ((int)(duplicateSectorArg ? UniversalType.SectorTag : UniversalType.LinedefTag)).ToString()
            : "0";

        return GameConfiguration.FromText($$"""
            linetagindicatesectors = {{lineTagIndicatesSectorsValue}};
            linedeftypes
            {
                event
                {
                    80
                    {
                        title = "Event";
                        linetolinetag = {{lineToLineTagValue}};
                        linetolinesameaction = {{lineToLineSameActionValue}};
                        arg0 { type = {{arg0Type}}; }
                        arg1 { type = {{arg1Type}}; }
                        arg2 { type = {{arg2Type}}; }
                    }
                    81 { title = "Other Event"; }
                }
            }
            """);
    }

    private static GameConfiguration SectorFieldAssociationConfig(
        string modify = "",
        bool neverShowEventLines = false)
    {
        string neverShowEventLinesValue = neverShowEventLines ? "true" : "false";

        return GameConfiguration.FromText($$"""
            universalfields
            {
                sector
                {
                    portal
                    {
                        type = {{(int)UniversalType.Integer}};
                        default = 0;
                        associations
                        {
                            0
                            {
                                property = "portal";
                                modify = "{{modify}}";
                                nevershoweventlines = {{neverShowEventLinesValue}};
                            }
                        }
                    }
                }
            }
            """);
    }

    private static GameConfiguration ThingTypeArgConfig(int thingLink = 0, int selfLinkType = 0)
    {
        int secondThingLink = selfLinkType == 0 ? 0 : selfLinkType;

        return GameConfiguration.FromText($$"""
            thingtypes
            {
                scripted
                {
                    9001
                    {
                        title = "Scripted";
                        thinglink = {{thingLink}};
                        arg0 { type = {{(int)UniversalType.SectorTag}}; }
                        arg1 { type = {{(int)UniversalType.ThingTag}}; }
                        arg2 { type = {{(int)UniversalType.LinedefTag}}; }
                    }
                    9002
                    {
                        title = "Self Link";
                        thinglink = {{secondThingLink}};
                        arg0 { type = {{(int)UniversalType.SectorTag}}; }
                    }
                }
            }
            """);
    }
}
