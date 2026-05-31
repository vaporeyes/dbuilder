// ABOUTME: Tests UDB BuilderModes edit-selection setting keys, defaults, and enum fallback behavior.
// ABOUTME: Covers precise-position and sector height-adjust option persistence without UI.

using DBuilder.Map;

namespace DBuilder.Tests;

public class EditSelectionModeSettingsTests
{
    [Fact]
    public void SettingsUseUdbKeysAndDefaults()
    {
        EditSelectionModeSettings settings = EditSelectionModeSettings.FromDictionary(new Dictionary<string, object?>());

        Assert.True(settings.UsePrecisePosition);
        Assert.Equal(EditSelectionHeightAdjustMode.None, settings.HeightAdjustMode);

        var target = new Dictionary<string, object?>();
        new EditSelectionModeSettings(false, EditSelectionHeightAdjustMode.AdjustCeilings).WriteTo(target);

        Assert.Equal(false, target[EditSelectionModeSettings.UsePrecisePositionKey]);
        Assert.Equal((int)EditSelectionHeightAdjustMode.AdjustCeilings, target[EditSelectionModeSettings.HeightAdjustModeKey]);
    }

    [Theory]
    [InlineData(0, EditSelectionHeightAdjustMode.None)]
    [InlineData(1, EditSelectionHeightAdjustMode.AdjustFloors)]
    [InlineData(2, EditSelectionHeightAdjustMode.AdjustCeilings)]
    [InlineData(3, EditSelectionHeightAdjustMode.AdjustBoth)]
    [InlineData(99, EditSelectionHeightAdjustMode.None)]
    public void SettingsReadUdbHeightAdjustModeIntegers(int persistedValue, EditSelectionHeightAdjustMode expected)
    {
        EditSelectionModeSettings settings = EditSelectionModeSettings.FromDictionary(new Dictionary<string, object?>
        {
            [EditSelectionModeSettings.UsePrecisePositionKey] = false,
            [EditSelectionModeSettings.HeightAdjustModeKey] = persistedValue,
        });

        Assert.False(settings.UsePrecisePosition);
        Assert.Equal(expected, settings.HeightAdjustMode);
    }

    [Fact]
    public void SettingsNormalizeDeserializedHeightAdjustMode()
    {
        var settings = new EditSelectionModeSettings(
            HeightAdjustMode: (EditSelectionHeightAdjustMode)99);

        Assert.Equal(EditSelectionHeightAdjustMode.None, settings.Normalized().HeightAdjustMode);
    }
}
