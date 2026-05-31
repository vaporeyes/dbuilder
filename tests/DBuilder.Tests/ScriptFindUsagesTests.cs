// ABOUTME: Covers UDB-style script find-usages matching and result spans.
// ABOUTME: Verifies whole-word and case-sensitive script search options.

using DBuilder.IO;

namespace DBuilder.Tests;

public class ScriptFindUsagesTests
{
    [Fact]
    public void FindsAllMatchesWithLineIndexesAndSpansLikeUdb()
    {
        const string text = """
            script 1 OPEN { Thing_Spawn(1); }
            Thing_Spawn(2); Thing_Spawn(3);
            """;

        var usages = ScriptFindUsages.Find(text, "Thing_Spawn", caseSensitive: true);

        Assert.Collection(
            usages,
            usage =>
            {
                Assert.Equal("script 1 OPEN { Thing_Spawn(1); }", usage.Line);
                Assert.Equal(0, usage.LineIndex);
                Assert.Equal(16, usage.MatchStart);
                Assert.Equal(27, usage.MatchEnd);
            },
            usage =>
            {
                Assert.Equal("Thing_Spawn(2); Thing_Spawn(3);", usage.Line);
                Assert.Equal(1, usage.LineIndex);
                Assert.Equal(0, usage.MatchStart);
                Assert.Equal(11, usage.MatchEnd);
            },
            usage =>
            {
                Assert.Equal("Thing_Spawn(2); Thing_Spawn(3);", usage.Line);
                Assert.Equal(1, usage.LineIndex);
                Assert.Equal(16, usage.MatchStart);
                Assert.Equal(27, usage.MatchEnd);
            });
    }

    [Fact]
    public void HonorsWholeWordAndCaseOptionsLikeUdb()
    {
        const string text = """
            ThingCount
            thing
            Thing
            Things
            """;

        var insensitive = ScriptFindUsages.Find(text, "Thing", wholeWord: true);
        var sensitive = ScriptFindUsages.Find(text, "Thing", wholeWord: true, caseSensitive: true);

        Assert.Equal(new[] { 1, 2 }, insensitive.Select(usage => usage.LineIndex));
        var usage = Assert.Single(sensitive);
        Assert.Equal(2, usage.LineIndex);
        Assert.Equal("Thing", usage.Line);
    }

    [Fact]
    public void ContainsTextStopsAtFirstMatchingLineLikeUdb()
    {
        const string text = """
            alpha
            beta
            """;

        Assert.True(ScriptFindUsages.ContainsText(text, "BETA"));
        Assert.False(ScriptFindUsages.ContainsText(text, "BETA", caseSensitive: true));
    }
}
