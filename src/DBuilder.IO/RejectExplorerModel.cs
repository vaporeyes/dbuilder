// ABOUTME: Models UDB RejectExplorer validation and sector visibility relationships.
// ABOUTME: Keeps REJECT lump size checks and overlay state classification separate from editor rendering.

using System.Collections.Generic;
using System.Globalization;

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

public sealed record RejectExplorerColorSettings(
    int Default,
    int Highlight,
    int Bidirectional,
    int UnidirectionalFrom,
    int UnidirectionalTo);

public sealed record RejectExplorerActionDescriptor(
    string Id,
    string Title,
    string Category,
    string Description,
    bool AllowKeys,
    bool AllowMouse,
    bool AllowScroll);

public sealed record RejectExplorerModeDescriptor(
    string DisplayName,
    string SwitchAction,
    string ButtonImage,
    int ButtonOrder,
    string ButtonGroup,
    IReadOnlyList<string> SupportedMapFormats,
    bool UseByDefault,
    bool Volatile,
    string HelpPath);

public sealed record RejectExplorerColorField(
    string Key,
    string Label,
    int DefaultColor);

public sealed record RejectExplorerRow(
    int SectorIndex,
    RejectExplorerRelation Relation,
    bool FromHighlighted,
    bool ToHighlighted);

public sealed record RejectExplorerEngageDecision(bool CanEngage, string Title, string Message, bool IsWarning);

public static class RejectExplorerModel
{
    public const string DefaultColorKey = "colors.default";
    public const string HighlightColorKey = "colors.highlight";
    public const string BidirectionalColorKey = "colors.bidirectional";
    public const string UnidirectionalFromColorKey = "colors.unidirectionalfrom";
    public const string UnidirectionalToColorKey = "colors.unidirectionalto";
    public const string ColorConfigurationTitle = "Color Configuration";
    public const string ResetColorsText = "Reset colors";
    public const string DoomMapSetIo = "DoomMapSetIO";
    public const string HexenMapSetIo = "HexenMapSetIO";

    public static RejectExplorerModeDescriptor ModeDescriptor { get; } = new(
        "Reject Explorer",
        "rejectexplorermode",
        "reject.png",
        int.MinValue + 504,
        "000_editing",
        [DoomMapSetIo, HexenMapSetIo],
        UseByDefault: true,
        Volatile: true,
        "/gzdb/features/classic_modes/mode_rejectexplorer.html");

    public static RejectExplorerColorSettings DefaultColors { get; } = new(
        Default: unchecked((int)0xFFA0A0A0),
        Highlight: unchecked((int)0xFF00C000),
        Bidirectional: unchecked((int)0xFF00A000),
        UnidirectionalFrom: unchecked((int)0xFFA0A000),
        UnidirectionalTo: unchecked((int)0xFFA000A0));

    public static RejectExplorerActionDescriptor ColorConfigurationAction { get; } = new(
        "rejectexplorercolorconfiguration",
        "Configure colors",
        "rejectexplorermode",
        "Configure colors for reject explorer mode",
        AllowKeys: true,
        AllowMouse: true,
        AllowScroll: true);

    public static IReadOnlyList<RejectExplorerColorField> ColorConfigurationFields { get; } =
    [
        new(DefaultColorKey, "Default color:", DefaultColors.Default),
        new(HighlightColorKey, "Highlight color:", DefaultColors.Highlight),
        new(BidirectionalColorKey, "Bidirectional color:", DefaultColors.Bidirectional),
        new(UnidirectionalFromColorKey, "Unidirectional from color:", DefaultColors.UnidirectionalFrom),
        new(UnidirectionalToColorKey, "Unidirectional to color:", DefaultColors.UnidirectionalTo),
    ];

    public static RejectExplorerColorSettings ColorsFromSettings(IReadOnlyDictionary<string, object?> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        RejectExplorerColorSettings fallback = DefaultColors;
        return fallback with
        {
            Default = ReadColor(settings, DefaultColorKey, fallback.Default),
            Highlight = ReadColor(settings, HighlightColorKey, fallback.Highlight),
            Bidirectional = ReadColor(settings, BidirectionalColorKey, fallback.Bidirectional),
            UnidirectionalFrom = ReadColor(settings, UnidirectionalFromColorKey, fallback.UnidirectionalFrom),
            UnidirectionalTo = ReadColor(settings, UnidirectionalToColorKey, fallback.UnidirectionalTo),
        };
    }

    public static IReadOnlyDictionary<string, object> ColorsToSettings(RejectExplorerColorSettings colors)
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [DefaultColorKey] = colors.Default,
            [HighlightColorKey] = colors.Highlight,
            [BidirectionalColorKey] = colors.Bidirectional,
            [UnidirectionalFromColorKey] = colors.UnidirectionalFrom,
            [UnidirectionalToColorKey] = colors.UnidirectionalTo,
        };

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

    public static RejectExplorerEngageDecision EngageDecision(RejectExplorerValidation validation)
        => validation.Status switch
        {
            RejectExplorerValidationStatus.Missing => new(
                CanEngage: false,
                "Failed to engage Reject Explorer Mode",
                "Map has no REJECT lump.",
                IsWarning: false),
            RejectExplorerValidationStatus.Empty => new(
                CanEngage: false,
                "Failed to engage Reject Explorer Mode",
                "REJECT lump is empty.",
                IsWarning: false),
            RejectExplorerValidationStatus.TooSmall => new(
                CanEngage: false,
                "Failed to engage Reject Explorer Mode",
                $"REJECT lump is too small. Expected {validation.ExpectedBytes} bytes, got {validation.ActualBytes} bytes.",
                IsWarning: false),
            RejectExplorerValidationStatus.TooLarge => new(
                CanEngage: true,
                "Reject Explorer Mode",
                $"REJECT lump is too large. Expected {validation.ExpectedBytes} bytes, got {validation.ActualBytes} bytes.",
                IsWarning: true),
            _ => new(CanEngage: true, "Reject Explorer Mode", "REJECT lump loaded.", IsWarning: false),
        };

    public static IReadOnlyList<RejectExplorerRow> BuildRows(RejectTable reject, int sectorCount, int? highlightedSector)
    {
        var rows = new List<RejectExplorerRow>(sectorCount < 0 ? 0 : sectorCount);
        for (int i = 0; i < sectorCount; i++)
        {
            RejectExplorerRelation relation = RelationToHighlight(reject, i, highlightedSector);
            bool fromHighlighted = highlightedSector is int h && SectorHasLineOfSight(reject, h, i);
            bool toHighlighted = highlightedSector is int h2 && SectorHasLineOfSight(reject, i, h2);
            rows.Add(new RejectExplorerRow(i, relation, fromHighlighted, toHighlighted));
        }

        return rows;
    }

    public static string FormatValidation(RejectExplorerValidation validation)
        => $"REJECT: {validation.Status} ({CountLabel(validation.ActualBytes, "byte")}, expected {validation.ExpectedBytes})";

    public static string RejectedSectorsStatusText(int rejectedSectorCount, int sourceSector)
        => $"{CountLabel(rejectedSectorCount, "sector")} {(rejectedSectorCount == 1 ? "is" : "are")} rejected (cannot see) from sector {sourceSector}.";

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";

    public static string FormatCounts(IReadOnlyList<RejectExplorerRow> rows)
    {
        int bidirectional = rows.Count(row => row.Relation == RejectExplorerRelation.Bidirectional);
        int from = rows.Count(row => row.Relation == RejectExplorerRelation.UnidirectionalFrom);
        int to = rows.Count(row => row.Relation == RejectExplorerRelation.UnidirectionalTo);
        int blocked = rows.Count(row => row.Relation == RejectExplorerRelation.Default);
        return $"Relations: {bidirectional} bidirectional, {from} visible from highlighted, {to} visible to highlighted, {blocked} no line of sight or default.";
    }

    public static string FormatRow(RejectExplorerRow row)
        => $"Sector {row.SectorIndex}: {Label(row.Relation)}  from highlighted: {YesNo(row.FromHighlighted)}  to highlighted: {YesNo(row.ToHighlighted)}";

    public static bool SectorHasLineOfSight(RejectTable reject, int fromSector, int toSector)
        => !reject.IsRejected(fromSector, toSector);

    public static IReadOnlyList<int> RejectedSectorIndexes(RejectTable reject, int sectorCount, int sourceSector)
    {
        var sectors = new List<int>();
        for (int i = 0; i < sectorCount; i++)
        {
            if (i == sourceSector) continue;
            if (reject.IsRejected(sourceSector, i)) sectors.Add(i);
        }

        return sectors;
    }

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

    public static int ColorForRelation(RejectExplorerRelation relation, RejectExplorerColorSettings? colors = null)
    {
        colors ??= DefaultColors;
        return relation switch
        {
            RejectExplorerRelation.Highlight => colors.Highlight,
            RejectExplorerRelation.Bidirectional => colors.Bidirectional,
            RejectExplorerRelation.UnidirectionalFrom => colors.UnidirectionalFrom,
            RejectExplorerRelation.UnidirectionalTo => colors.UnidirectionalTo,
            _ => colors.Default,
        };
    }

    public static int[] SectorOverlayColors(
        RejectTable reject,
        int sectorCount,
        int? highlightedSector,
        RejectExplorerColorSettings? colors = null)
    {
        var result = new int[sectorCount < 0 ? 0 : sectorCount];
        for (int i = 0; i < result.Length; i++)
            result[i] = ColorForRelation(RelationToHighlight(reject, i, highlightedSector), colors);

        return result;
    }

    private static int ReadColor(IReadOnlyDictionary<string, object?> settings, string key, int fallback)
    {
        if (!settings.TryGetValue(key, out object? value)) return fallback;
        return value switch
        {
            int color => color,
            uint color => unchecked((int)color),
            long color when color is >= int.MinValue and <= uint.MaxValue => unchecked((int)color),
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int color) => color,
            string text when uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint color) => unchecked((int)color),
            _ => fallback,
        };
    }

    private static string Label(RejectExplorerRelation relation)
        => relation switch
        {
            RejectExplorerRelation.Highlight => "highlighted",
            RejectExplorerRelation.Bidirectional => "bidirectional",
            RejectExplorerRelation.UnidirectionalFrom => "from highlighted",
            RejectExplorerRelation.UnidirectionalTo => "to highlighted",
            _ => "no line of sight",
        };

    private static string YesNo(bool value) => value ? "yes" : "no";
}
