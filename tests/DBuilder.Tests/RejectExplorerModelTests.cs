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
