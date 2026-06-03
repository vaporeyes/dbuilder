// ABOUTME: Tests editor-facing WAD map marker normalization used by the basic map options dialog.
// ABOUTME: Keeps marker names classic-lump-safe before they reach WAD save/rename code.

using DBuilder.IO;

namespace DBuilder.Tests;

public class MapNameRulesTests
{
    [Theory]
    [InlineData("map01", "MAP01")]
    [InlineData(" e1m1 ", "E1M1")]
    [InlineData("long_map_name", "LONG_MAP")]
    [InlineData("map-01", "MAP01")]
    public void NormalizeMarkerUppercasesFiltersAndTruncates(string input, string expected)
    {
        Assert.Equal(expected, MapNameRules.NormalizeMarker(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NormalizeMarkerFallsBackWhenEmpty(string? input)
    {
        Assert.Equal("MAP01", MapNameRules.NormalizeMarker(input));
    }

    [Fact]
    public void IsValidMarkerRejectsConfiguredMapLumpNamesAfterNormalization()
    {
        var config = GameConfiguration.FromText("""
            maplumpnames
            {
                THINGS { required = true; }
            }
            """);

        Assert.False(MapNameRules.IsValidMarker("things", config));
        Assert.True(MapNameRules.IsValidMarker("map01", config));
    }
}
