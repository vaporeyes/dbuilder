// ABOUTME: Tests for ReverbsParser over REVERBS sound environment declarations.
// ABOUTME: Covers environment ids, quoted names, property block skipping, duplicate names, and duplicate ids.

using DBuilder.IO;

namespace DBuilder.Tests;

public class ReverbsParserTests
{
    [Fact]
    public void ParsesReverbNamesAndArguments()
    {
        const string text = @"
""Small Room"" 1 0
{
    Room -1000
    DecayTime 1.49
}
Cave 2 3";

        var reverbs = ReverbsParser.Parse(text);

        Assert.Equal(2, reverbs.Environments.Count);
        Assert.Equal(new ReverbDefinition("Small Room", 1, 0), reverbs.Environments["Small Room"]);
        Assert.Equal(new ReverbDefinition("Cave", 2, 3), reverbs.Environments["Cave"]);
    }

    [Fact]
    public void DuplicateNamesUpdateButDuplicateIdsAreIgnored()
    {
        const string text = @"
Cave 2 3
Hall 2 3
Cave 4 5";

        var reverbs = ReverbsParser.Parse(text);

        Assert.Single(reverbs.Environments);
        Assert.Equal(new ReverbDefinition("Cave", 4, 5), reverbs.Environments["Cave"]);
    }

    [Fact]
    public void UsesUdbCombinedIntegerKeyForDuplicateIds()
    {
        const string text = @"
First 1 1000
Second 2 0";

        var reverbs = ReverbsParser.Parse(text);

        Assert.Single(reverbs.Environments);
        Assert.Equal(new ReverbDefinition("First", 1, 1000), reverbs.Environments["First"]);
    }

    [Fact]
    public void UsesUdbOrdinalNamesAndSortedOrder()
    {
        const string text = @"
cave 4 0
Hall 3 0
Cave 2 0";

        var reverbs = ReverbsParser.Parse(text);

        Assert.Equal(new[] { "Cave", "Hall", "cave" }, reverbs.Environments.Keys.ToArray());
        Assert.Equal(new ReverbDefinition("cave", 4, 0), reverbs.Environments["cave"]);
        Assert.Equal(new ReverbDefinition("Cave", 2, 0), reverbs.Environments["Cave"]);
    }

    [Fact]
    public void MalformedEnvironmentIdsStopParsingLikeUdb()
    {
        const string text = @"
Cave 2 0
Bad bogus 1
Hall 3 0";

        var reverbs = ReverbsParser.Parse(text);

        Assert.Equal(new[] { "Cave" }, reverbs.Environments.Keys.ToArray());
    }

    [Fact]
    public void EmptyEnvironmentNamesStopParsingLikeUdb()
    {
        const string text = @"
Cave 2 0
"""" 3 0
Hall 4 0";

        var reverbs = ReverbsParser.Parse(text);

        Assert.Equal(new[] { "Cave" }, reverbs.Environments.Keys.ToArray());
    }
}
