// ABOUTME: Models UDB BuilderModes edit-selection option persistence without editor UI dependencies.
// ABOUTME: Preserves plugin setting keys, defaults, and enum fallback behavior for selection transforms.

namespace DBuilder.Map;

public enum EditSelectionHeightAdjustMode
{
    None = 0,
    AdjustFloors = 1,
    AdjustCeilings = 2,
    AdjustBoth = 3,
}

public sealed record EditSelectionModeSettings(
    bool UsePrecisePosition = true,
    EditSelectionHeightAdjustMode HeightAdjustMode = EditSelectionHeightAdjustMode.None)
{
    public const string UsePrecisePositionKey = "editselectionmode.usepreciseposition";
    public const string HeightAdjustModeKey = "editselectionmode.heightadjustmode";

    public static EditSelectionModeSettings FromDictionary(IReadOnlyDictionary<string, object?> settings)
        => new(
            DrawLineModeSettings.ReadBool(settings, UsePrecisePositionKey, true),
            NormalizeHeightAdjustMode((EditSelectionHeightAdjustMode)DrawLineModeSettings.ReadInt(settings, HeightAdjustModeKey, 0)));

    public void WriteTo(IDictionary<string, object?> settings)
    {
        EditSelectionModeSettings normalized = Normalized();
        settings[UsePrecisePositionKey] = normalized.UsePrecisePosition;
        settings[HeightAdjustModeKey] = (int)normalized.HeightAdjustMode;
    }

    public EditSelectionModeSettings Normalized()
        => this with { HeightAdjustMode = NormalizeHeightAdjustMode(HeightAdjustMode) };

    private static EditSelectionHeightAdjustMode NormalizeHeightAdjustMode(EditSelectionHeightAdjustMode mode)
        => Enum.IsDefined(mode) ? mode : EditSelectionHeightAdjustMode.None;
}
