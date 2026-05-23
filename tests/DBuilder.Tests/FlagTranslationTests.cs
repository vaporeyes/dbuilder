// ABOUTME: Tests config-driven binary<->UDMF flag translation, including compound (a,b) and negated (!a) specs.
// ABOUTME: Uses the canonical Doom linedef/thing flag translation tables from UDB's Doom_misc.cfg.

using System.Linq;
using DBuilder.IO;

namespace DBuilder.Tests;

public class FlagTranslationTests
{
    private const string Cfg = @"
linedefflagstranslation
{
    1 = ""blocking"";
    2 = ""blockmonsters"";
    4 = ""twosided"";
    8 = ""dontpegtop"";
    16 = ""dontpegbottom"";
}
thingflagstranslation
{
    1 = ""skill1,skill2"";
    2 = ""skill3"";
    4 = ""skill4,skill5"";
    8 = ""ambush"";
    16 = ""!single"";
}";

    [Fact]
    public void ParsesTranslationsWithCompoundAndNegation()
    {
        var gc = GameConfiguration.FromText(Cfg);
        Assert.Equal(5, gc.LinedefFlagsTranslation.Count);
        var skills = gc.ThingFlagsTranslation;
        var compound = System.Array.Find(skills.ToArray(), t => t.Flag == 1)!;
        Assert.Equal(new[] { "skill1", "skill2" }, compound.Fields);
        var negated = System.Array.Find(skills.ToArray(), t => t.Flag == 16)!;
        Assert.Equal("single", negated.Fields[0]);
        Assert.False(negated.Values[0]);
    }

    [Fact]
    public void LinedefBitsToUdmf()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var f = gc.LinedefFlagsToUdmf(1 | 4); // blocking + twosided
        Assert.Contains("blocking", f);
        Assert.Contains("twosided", f);
        Assert.DoesNotContain("dontpegtop", f);
    }

    [Fact]
    public void ThingNegatedFlagIsTrueWhenBitClear()
    {
        var gc = GameConfiguration.FromText(Cfg);
        // bit 16 clear -> single = true; bit 1 set -> skill1+skill2 true
        var f = gc.ThingFlagsToUdmf(1);
        Assert.Contains("skill1", f);
        Assert.Contains("skill2", f);
        Assert.Contains("single", f); // negated, present because its bit is clear
        Assert.DoesNotContain("ambush", f);
    }

    [Fact]
    public void ThingNegatedFlagAbsentWhenBitSet()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var f = gc.ThingFlagsToUdmf(16); // single-player-excluded bit set -> single = false (absent)
        Assert.DoesNotContain("single", f);
    }

    [Fact]
    public void UdmfBackToLinedefBits()
    {
        var gc = GameConfiguration.FromText(Cfg);
        int bits = gc.LinedefFlagsFromUdmf(new[] { "blocking", "dontpegbottom" });
        Assert.Equal(1 | 16, bits);
    }

    [Fact]
    public void CompoundThingFlagRoundTrips()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var udmf = gc.ThingFlagsToUdmf(4 | 8); // skill4+skill5, ambush; single true (16 clear)
        int back = gc.ThingFlagsFromUdmf(udmf);
        Assert.Equal(4 | 8, back); // bit 16 stays clear because single is present (true)
    }

    [Fact]
    public void DefaultThingFlagsSetNegatedBit()
    {
        var gc = GameConfiguration.FromText(Cfg);
        // No UDMF flags at all -> single absent (false) -> bit 16 is set by the negated translation.
        int bits = gc.ThingFlagsFromUdmf(System.Array.Empty<string>());
        Assert.Equal(16, bits);
    }
}
