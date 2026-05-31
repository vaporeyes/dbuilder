// ABOUTME: Models UDB RejectExplorer validation and sector visibility relationships.
// ABOUTME: Keeps REJECT lump size checks and overlay state classification separate from editor rendering.

namespace DBuilder.IO;

public enum RejectExplorerValidationStatus
{
    Valid,
    Missing,
    Empty,
    TooSmall,
    TooLarge,
}

public enum RejectExplorerRelation
{
    Default,
    Highlight,
    Bidirectional,
    UnidirectionalFrom,
    UnidirectionalTo,
}

public sealed record RejectExplorerValidation(
    RejectExplorerValidationStatus Status,
    int ExpectedBytes,
    int ActualBytes)
{
    public bool CanUse => Status is RejectExplorerValidationStatus.Valid or RejectExplorerValidationStatus.TooLarge;
}

public static class RejectExplorerModel
{
    public static int ExpectedByteCount(int sectorCount)
        => sectorCount <= 0 ? 0 : (sectorCount * sectorCount + 7) / 8;

    public static RejectExplorerValidation Validate(byte[]? rejectData, int sectorCount)
    {
        int expected = ExpectedByteCount(sectorCount);
        if (rejectData == null) return new RejectExplorerValidation(RejectExplorerValidationStatus.Missing, expected, 0);
        if (rejectData.Length == 0) return new RejectExplorerValidation(RejectExplorerValidationStatus.Empty, expected, 0);
        if (rejectData.Length < expected) return new RejectExplorerValidation(RejectExplorerValidationStatus.TooSmall, expected, rejectData.Length);
        if (rejectData.Length > expected) return new RejectExplorerValidation(RejectExplorerValidationStatus.TooLarge, expected, rejectData.Length);

        return new RejectExplorerValidation(RejectExplorerValidationStatus.Valid, expected, rejectData.Length);
    }

    public static bool SectorHasLineOfSight(RejectTable reject, int fromSector, int toSector)
        => !reject.IsRejected(fromSector, toSector);

    public static RejectExplorerRelation RelationToHighlight(RejectTable reject, int sectorIndex, int? highlightedSector)
    {
        if (highlightedSector == null) return RejectExplorerRelation.Default;
        int highlighted = highlightedSector.Value;
        if (sectorIndex == highlighted) return RejectExplorerRelation.Highlight;

        bool fromHighlighted = SectorHasLineOfSight(reject, highlighted, sectorIndex);
        bool toHighlighted = SectorHasLineOfSight(reject, sectorIndex, highlighted);

        if (fromHighlighted && toHighlighted) return RejectExplorerRelation.Bidirectional;
        if (fromHighlighted) return RejectExplorerRelation.UnidirectionalFrom;
        if (toHighlighted) return RejectExplorerRelation.UnidirectionalTo;
        return RejectExplorerRelation.Default;
    }
}
