// ABOUTME: Tests UDB-compatible toast preference normalization and action toggle helpers.
// ABOUTME: Covers persisted disabled-action text without requiring a live toast manager.

using DBuilder.IO;

namespace DBuilder.Tests;

public class ToastPreferencesTests
{
    [Fact]
    public void DefaultsMatchUdbToastPreferences()
    {
        Assert.Equal(ToastAnchor.BottomRight, ToastPreferences.DefaultAnchor);
        Assert.Equal(3000, ToastPreferences.DefaultDurationMilliseconds);
        Assert.Equal(1000, ToastPreferences.MinDurationMilliseconds);
    }

    [Fact]
    public void NormalizesInvalidAnchorAndDuration()
    {
        Assert.Equal(ToastAnchor.BottomRight, ToastPreferences.NormalizeAnchor((ToastAnchor)99));
        Assert.Equal(3000, ToastPreferences.NormalizeDurationMilliseconds(null));
        Assert.Equal(1000, ToastPreferences.NormalizeDurationMilliseconds(0));
        Assert.Equal(5000, ToastPreferences.NormalizeDurationMilliseconds(5000));
    }

    [Theory]
    [InlineData("", 1000)]
    [InlineData("0", 1000)]
    [InlineData("3", 3000)]
    [InlineData("12", 12000)]
    public void AcceptDurationSecondsTextClampsToAtLeastOneSecond(string text, int expected)
    {
        Assert.Equal(expected, ToastPreferences.AcceptDurationSecondsText(text));
    }

    [Fact]
    public void DisabledActionsTextStoresFalseEntriesOnly()
    {
        var settings = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["togglehighlight"] = false,
            ["autosave"] = true,
            ["resourcewarningsanderrors"] = false,
        };

        string text = ToastPreferences.DisabledActionsText(settings);

        Assert.DoesNotContain("autosave", text, StringComparison.Ordinal);
        Assert.Contains("resourcewarningsanderrors", text, StringComparison.Ordinal);
        Assert.Contains("togglehighlight", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseDisabledActionsTextAcceptsUdbActionIds()
    {
        Dictionary<string, bool> parsed = ToastPreferences.ParseDisabledActionsText(
            "togglehighlight; autosave\nresourcewarningsanderrors, autosave");

        Assert.Equal(3, parsed.Count);
        Assert.False(parsed["togglehighlight"]);
        Assert.False(parsed["autosave"]);
        Assert.False(parsed["resourcewarningsanderrors"]);
    }

    [Fact]
    public void NormalizeActionsSortsByTitleAndAppliesPersistedToggles()
    {
        var actions = new[]
        {
            new ToastActionPreference("togglehighlight", "Highlight", "Changed highlight", true),
            new ToastActionPreference("autosave", "Autosave", "Autosave finished", true),
            new ToastActionPreference("autosave", "Duplicate", "Ignored", true),
        };
        var persisted = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["togglehighlight"] = false,
        };

        IReadOnlyList<ToastActionPreference> normalized = ToastPreferences.NormalizeActions(actions, persisted);

        Assert.Equal(new[] { "autosave", "togglehighlight" }, normalized.Select(action => action.Name));
        Assert.True(normalized[0].Enabled);
        Assert.False(normalized[1].Enabled);
    }
}
