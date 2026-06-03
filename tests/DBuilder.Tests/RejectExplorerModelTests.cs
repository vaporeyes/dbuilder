// ABOUTME: Verifies RejectExplorer REJECT lump validation and visibility relationship classification.
// ABOUTME: Covers UDB-style missing, empty, too-small, too-large, and directional overlay states.

using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class RejectExplorerModelTests
{
    [Fact]
    public void DefaultColorsMatchUdbRejectExplorerDefaults()
    {
        RejectExplorerColorSettings colors = RejectExplorerModel.DefaultColors;

        Assert.Equal(unchecked((int)0xFFA0A0A0), colors.Default);
        Assert.Equal(unchecked((int)0xFF00C000), colors.Highlight);
        Assert.Equal(unchecked((int)0xFF00A000), colors.Bidirectional);
        Assert.Equal(unchecked((int)0xFFA0A000), colors.UnidirectionalFrom);
        Assert.Equal(unchecked((int)0xFFA000A0), colors.UnidirectionalTo);
    }

    [Fact]
    public void ColorForRelationUsesConfiguredColors()
    {
        var colors = new RejectExplorerColorSettings(
            Default: 1,
            Highlight: 2,
            Bidirectional: 3,
            UnidirectionalFrom: 4,
            UnidirectionalTo: 5);

        Assert.Equal(1, RejectExplorerModel.ColorForRelation(RejectExplorerRelation.Default, colors));
        Assert.Equal(2, RejectExplorerModel.ColorForRelation(RejectExplorerRelation.Highlight, colors));
        Assert.Equal(3, RejectExplorerModel.ColorForRelation(RejectExplorerRelation.Bidirectional, colors));
        Assert.Equal(4, RejectExplorerModel.ColorForRelation(RejectExplorerRelation.UnidirectionalFrom, colors));
        Assert.Equal(5, RejectExplorerModel.ColorForRelation(RejectExplorerRelation.UnidirectionalTo, colors));
    }

    [Fact]
    public void ColorConfigurationMetadataMatchesUdbDialogAndAction()
    {
        RejectExplorerActionDescriptor action = RejectExplorerModel.ColorConfigurationAction;
        IReadOnlyList<RejectExplorerColorField> fields = RejectExplorerModel.ColorConfigurationFields;

        Assert.Equal("Color Configuration", RejectExplorerModel.ColorConfigurationTitle);
        Assert.Equal("Reset colors", RejectExplorerModel.ResetColorsText);
        Assert.Equal("rejectexplorercolorconfiguration", action.Id);
        Assert.Equal("Configure colors", action.Title);
        Assert.Equal("rejectexplorermode", action.Category);
        Assert.Equal("Configure colors for reject explorer mode", action.Description);
        Assert.True(action.AllowKeys);
        Assert.True(action.AllowMouse);
        Assert.True(action.AllowScroll);

        Assert.Equal(5, fields.Count);
        Assert.Equal(new RejectExplorerColorField("colors.default", "Default color:", unchecked((int)0xFFA0A0A0)), fields[0]);
        Assert.Equal(new RejectExplorerColorField("colors.highlight", "Highlight color:", unchecked((int)0xFF00C000)), fields[1]);
        Assert.Equal(new RejectExplorerColorField("colors.bidirectional", "Bidirectional color:", unchecked((int)0xFF00A000)), fields[2]);
        Assert.Equal(new RejectExplorerColorField("colors.unidirectionalfrom", "Unidirectional from color:", unchecked((int)0xFFA0A000)), fields[3]);
        Assert.Equal(new RejectExplorerColorField("colors.unidirectionalto", "Unidirectional to color:", unchecked((int)0xFFA000A0)), fields[4]);
    }

    [Fact]
    public void ColorSettingsRoundTripUsesUdbPluginKeys()
    {
        var settings = new Dictionary<string, object?>
        {
            [RejectExplorerModel.DefaultColorKey] = unchecked((int)0xFF010203),
            [RejectExplorerModel.HighlightColorKey] = 0xFF040506u,
            [RejectExplorerModel.BidirectionalColorKey] = "4278650889",
            [RejectExplorerModel.UnidirectionalFromColorKey] = "invalid",
            [RejectExplorerModel.UnidirectionalToColorKey] = unchecked((long)0xFF0A0B0C),
        };

        RejectExplorerColorSettings colors = RejectExplorerModel.ColorsFromSettings(settings);
        IReadOnlyDictionary<string, object> written = RejectExplorerModel.ColorsToSettings(colors);

        Assert.Equal(unchecked((int)0xFF010203), colors.Default);
        Assert.Equal(unchecked((int)0xFF040506), colors.Highlight);
        Assert.Equal(unchecked((int)0xFF070809), colors.Bidirectional);
        Assert.Equal(RejectExplorerModel.DefaultColors.UnidirectionalFrom, colors.UnidirectionalFrom);
        Assert.Equal(unchecked((int)0xFF0A0B0C), colors.UnidirectionalTo);
        Assert.Equal(unchecked((int)0xFF010203), written[RejectExplorerModel.DefaultColorKey]);
        Assert.Equal(unchecked((int)0xFF040506), written[RejectExplorerModel.HighlightColorKey]);
        Assert.Equal(unchecked((int)0xFF070809), written[RejectExplorerModel.BidirectionalColorKey]);
        Assert.Equal(RejectExplorerModel.DefaultColors.UnidirectionalFrom, written[RejectExplorerModel.UnidirectionalFromColorKey]);
        Assert.Equal(unchecked((int)0xFF0A0B0C), written[RejectExplorerModel.UnidirectionalToColorKey]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(9, 11)]
    public void ExpectedByteCountRoundsSectorMatrixBits(int sectors, int expected)
        => Assert.Equal(expected, RejectExplorerModel.ExpectedByteCount(sectors));

    [Fact]
    public void ValidateReportsMissingEmptyTooSmallValidAndTooLarge()
    {
        Assert.Equal(
            RejectExplorerValidationStatus.Missing,
            RejectExplorerModel.Validate(null, sectorCount: 3).Status);
        Assert.Equal(
            RejectExplorerValidationStatus.Empty,
            RejectExplorerModel.Validate(Array.Empty<byte>(), sectorCount: 3).Status);
        Assert.Equal(
            RejectExplorerValidationStatus.TooSmall,
            RejectExplorerModel.Validate(new byte[1], sectorCount: 3).Status);

        var valid = RejectExplorerModel.Validate(new byte[2], sectorCount: 3);
        Assert.Equal(RejectExplorerValidationStatus.Valid, valid.Status);
        Assert.True(valid.CanUse);

        var tooLarge = RejectExplorerModel.Validate(new byte[3], sectorCount: 3);
        Assert.Equal(RejectExplorerValidationStatus.TooLarge, tooLarge.Status);
        Assert.True(tooLarge.CanUse);
    }

    [Fact]
    public void RelationToHighlightClassifiesDirectionalVisibility()
    {
        var reject = RejectTable.Parse(BuildReject(4, (0, 3), (2, 0)), 4);

        Assert.Equal(RejectExplorerRelation.Default, RejectExplorerModel.RelationToHighlight(reject, 1, null));
        Assert.Equal(RejectExplorerRelation.Highlight, RejectExplorerModel.RelationToHighlight(reject, 0, 0));
        Assert.Equal(RejectExplorerRelation.Bidirectional, RejectExplorerModel.RelationToHighlight(reject, 1, 0));
        Assert.Equal(RejectExplorerRelation.UnidirectionalFrom, RejectExplorerModel.RelationToHighlight(reject, 2, 0));
        Assert.Equal(RejectExplorerRelation.UnidirectionalTo, RejectExplorerModel.RelationToHighlight(reject, 3, 0));
    }

    [Fact]
    public void SectorHasLineOfSightIsInverseOfRejectBit()
    {
        var reject = RejectTable.Parse(BuildReject(2, (0, 1)), 2);

        Assert.False(RejectExplorerModel.SectorHasLineOfSight(reject, 0, 1));
        Assert.True(RejectExplorerModel.SectorHasLineOfSight(reject, 1, 0));
    }

    [Fact]
    public void BuildRowsAndFormattingDescribeHighlightedRelations()
    {
        var validation = RejectExplorerModel.Validate(new byte[2], sectorCount: 3);
        var reject = RejectTable.Parse(BuildReject(4, (0, 3), (2, 0)), 4);

        IReadOnlyList<RejectExplorerRow> rows = RejectExplorerModel.BuildRows(reject, 4, 0);

        Assert.Equal("REJECT: Valid (2 byte(s), expected 2)", RejectExplorerModel.FormatValidation(validation));
        Assert.Equal(
            "Relations: 1 bidirectional, 1 visible from highlighted, 1 visible to highlighted, 0 no line of sight or default.",
            RejectExplorerModel.FormatCounts(rows));
        Assert.Equal(
            new[]
            {
                RejectExplorerRelation.Highlight,
                RejectExplorerRelation.Bidirectional,
                RejectExplorerRelation.UnidirectionalFrom,
                RejectExplorerRelation.UnidirectionalTo,
            },
            rows.Select(row => row.Relation));
        Assert.Equal(
            "Sector 2: from highlighted  from highlighted: yes  to highlighted: no",
            RejectExplorerModel.FormatRow(rows[2]));
    }

    [Fact]
    public void SectorOverlayColorsFollowHighlightedRelations()
    {
        var reject = RejectTable.Parse(BuildReject(4, (0, 3), (2, 0)), 4);
        var colors = new RejectExplorerColorSettings(
            Default: 10,
            Highlight: 20,
            Bidirectional: 30,
            UnidirectionalFrom: 40,
            UnidirectionalTo: 50);

        int[] overlay = RejectExplorerModel.SectorOverlayColors(reject, 4, 0, colors);

        Assert.Equal(new[] { 20, 30, 40, 50 }, overlay);
    }

    private static byte[] BuildReject(int sectorCount, params (int From, int To)[] rejected)
    {
        var bytes = new byte[RejectExplorerModel.ExpectedByteCount(sectorCount)];
        foreach (var (from, to) in rejected)
        {
            int bit = from * sectorCount + to;
            bytes[bit >> 3] |= (byte)(1 << (bit & 7));
        }

        return bytes;
    }
}
