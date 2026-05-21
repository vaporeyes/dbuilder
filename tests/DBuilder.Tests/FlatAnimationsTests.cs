// ABOUTME: FlatAnimations registry tests - chain rotation and static-flat fallthrough.

using DBuilder.IO;

namespace DBuilder.Tests;

public class FlatAnimationsTests
{
    [Fact]
    public void StaticFlatReturnsNull()
    {
        Assert.Null(FlatAnimations.GetChainStarting("FLOOR4_8"));
        Assert.Null(FlatAnimations.GetChainStarting("CEIL3_5"));
        Assert.False(FlatAnimations.IsAnimated("FLOOR4_8"));
    }

    [Fact]
    public void FirstFrameReturnsCanonicalOrder()
    {
        var chain = FlatAnimations.GetChainStarting("NUKAGE1");
        Assert.NotNull(chain);
        Assert.Equal(new[] { "NUKAGE1", "NUKAGE2", "NUKAGE3" }, chain);
    }

    [Fact]
    public void MidFrameReturnsRotated()
    {
        var chain = FlatAnimations.GetChainStarting("NUKAGE2");
        Assert.NotNull(chain);
        Assert.Equal(new[] { "NUKAGE2", "NUKAGE3", "NUKAGE1" }, chain);
    }

    [Fact]
    public void LastFrameReturnsRotated()
    {
        var chain = FlatAnimations.GetChainStarting("FWATER4");
        Assert.NotNull(chain);
        Assert.Equal(new[] { "FWATER4", "FWATER1", "FWATER2", "FWATER3" }, chain);
    }

    [Fact]
    public void LookupIsCaseInsensitive()
    {
        var chain = FlatAnimations.GetChainStarting("nukage1");
        Assert.NotNull(chain);
        Assert.Equal(3, chain!.Count);
    }

    [Fact]
    public void IsAnimatedReportsKnownChainMembers()
    {
        Assert.True(FlatAnimations.IsAnimated("BLOOD1"));
        Assert.True(FlatAnimations.IsAnimated("LAVA3"));
        Assert.True(FlatAnimations.IsAnimated("RROCK07"));
        Assert.True(FlatAnimations.IsAnimated("SLIME11"));
        Assert.False(FlatAnimations.IsAnimated("MFLR8_1"));
    }

    [Fact]
    public void FramePeriodMatchesDoomEngineTiming()
    {
        // Doom advances flat animations every 8 game tics at 35 tics/sec.
        Assert.Equal(8.0 / 35.0, FlatAnimations.FramePeriodSeconds, 1e-9);
    }
}
