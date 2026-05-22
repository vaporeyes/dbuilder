// ABOUTME: Tests for BoomGeneralized - decoding generalized linedef action numbers into descriptions.
// ABOUTME: Anchors on hand-computed values across each category and the generalized-range boundaries.

using DBuilder.IO;

namespace DBuilder.Tests;

public class BoomGeneralizedTests
{
    [Theory]
    [InlineData(0x2F7F, false)] // just below crusher base
    [InlineData(0x2F80, true)]  // crusher base
    [InlineData(0x6000, true)]  // floor base
    [InlineData(0x7FFF, true)]  // top of range
    [InlineData(0x8000, false)] // just past the range
    [InlineData(11, false)]     // a normal (non-generalized) action
    public void IsGeneralizedMatchesRange(int action, bool expected)
        => Assert.Equal(expected, BoomGeneralized.IsGeneralized(action));

    [Fact]
    public void NonGeneralizedDescribesAsNull()
        => Assert.Null(BoomGeneralized.Describe(11));

    [Fact]
    public void DecodesGeneralizedFloor()
    {
        // 0x6000 | SR(3) | dir up(0x40) | target LnF(0x80) -> 0x60C3
        string d = BoomGeneralized.Describe(0x60C3)!;
        Assert.StartsWith("Floor [SR]:", d);
        Assert.Contains("Up to Lowest neighbor floor", d);
        Assert.Contains("Slow", d);
        Assert.DoesNotContain("Crushes", d);
    }

    [Fact]
    public void DecodesGeneralizedFloorCrusher()
    {
        // Floor with crush bit (0x1000) and fast speed (2<<3 = 0x10): 0x6000 | 0x10 | 0x1000 = 0x7010
        string d = BoomGeneralized.Describe(0x7010)!;
        Assert.StartsWith("Floor [", d);
        Assert.Contains("Fast", d);
        Assert.Contains("Crushes", d);
    }

    [Fact]
    public void CeilingUsesCeilingTargetNames()
    {
        // 0x4000 | target index 0 -> "Highest neighbor ceiling"
        string d = BoomGeneralized.Describe(0x4000)!;
        Assert.StartsWith("Ceiling [W1]:", d);
        Assert.Contains("Highest neighbor ceiling", d);
    }

    [Fact]
    public void DecodesGeneralizedDoor()
    {
        // 0x3C00 | fast(0x10) | kind OpenStay(1<<5 = 0x20) -> 0x3C30
        string d = BoomGeneralized.Describe(0x3C30)!;
        Assert.StartsWith("Door [W1]:", d);
        Assert.Contains("Open Stay", d);
        Assert.Contains("Fast", d);
    }

    [Fact]
    public void DecodesGeneralizedCrusher()
    {
        // 0x2F80 | WR(1) | normal(1<<3 = 0x08) | silent(0x40) -> 0x2FC9
        string d = BoomGeneralized.Describe(0x2FC9)!;
        Assert.StartsWith("Crusher [WR]:", d);
        Assert.Contains("Normal", d);
        Assert.Contains("silent", d);
        Assert.DoesNotContain("monsters can activate", d);
    }

    [Fact]
    public void GameConfigTitleFallsBackToGeneralizedDescription()
    {
        // An empty config has no catalog entry, so a generalized number should decode rather than read "Unknown".
        var gc = GameConfiguration.FromText("");
        string title = gc.LinedefActionTitle(0x60C3);
        Assert.StartsWith("Floor [SR]:", title);
        Assert.Equal("Unknown (999)", gc.LinedefActionTitle(999));
    }
}
