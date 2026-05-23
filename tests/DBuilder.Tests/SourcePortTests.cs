// ABOUTME: Tests source-port argument template parsing and %IWAD/%FO/%MAP substitution for Test Map.
// ABOUTME: Ensures quoted paths with spaces stay single tokens and tokens substitute correctly.

using DBuilder.IO;

namespace DBuilder.Tests;

public class SourcePortTests
{
    [Fact]
    public void DefaultTemplateSubstitutesTokens()
    {
        var args = SourcePort.BuildArgs(SourcePort.DefaultArgsTemplate, "/games/DOOM.WAD", "/tmp/test.wad", "E1M1");
        Assert.Equal(new[] { "-iwad", "/games/DOOM.WAD", "-file", "/tmp/test.wad", "+map", "E1M1" }, args);
    }

    [Fact]
    public void QuotedPathWithSpacesStaysOneToken()
    {
        var args = SourcePort.BuildArgs(SourcePort.DefaultArgsTemplate, "/My Games/doom.wad", "/tmp/a b/test.wad", "MAP01");
        Assert.Equal("/My Games/doom.wad", args[1]);
        Assert.Equal("/tmp/a b/test.wad", args[3]);
        Assert.Equal("MAP01", args[5]);
    }

    [Fact]
    public void ExtraFlagsArePassedThrough()
    {
        var args = SourcePort.BuildArgs("-iwad \"%IWAD\" -file \"%FO\" +map \"%MAP\" -skill 4 -nomonsters",
            "iwad.wad", "m.wad", "MAP07");
        Assert.Contains("-skill", args);
        Assert.Contains("4", args);
        Assert.Contains("-nomonsters", args);
    }

    [Fact]
    public void CollapsesRepeatedWhitespace()
    {
        var args = SourcePort.BuildArgs("-warp   %MAP", "i.wad", "m.wad", "5");
        Assert.Equal(new[] { "-warp", "5" }, args);
    }
}
