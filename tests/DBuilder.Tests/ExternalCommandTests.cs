// ABOUTME: Tests external command parsing used by map pre/post command execution.
// ABOUTME: Keeps coverage independent of real launched tools by validating planned invocations.

using DBuilder.IO;

namespace DBuilder.Tests;

public class ExternalCommandTests
{
    [Fact]
    public void BuildInvocationsSplitsQuotedArguments()
    {
        var settings = new ExternalCommandSettings
        {
            Commands = "\"/tools/my tool\" --file \"a b.wad\" --flag",
            WorkingDirectory = "/tmp/project",
        };

        var invocations = ExternalCommand.BuildInvocations(settings);

        Assert.Single(invocations);
        Assert.Equal("/tools/my tool", invocations[0].FileName);
        Assert.Equal(new[] { "--file", "a b.wad", "--flag" }, invocations[0].Arguments);
        Assert.Equal("/tmp/project", invocations[0].WorkingDirectory);
    }

    [Fact]
    public void BuildInvocationsUsesOneCommandPerLine()
    {
        var settings = new ExternalCommandSettings
        {
            Commands = "first --one\n\nsecond \"two words\"",
        };

        var invocations = ExternalCommand.BuildInvocations(settings);

        Assert.Equal(2, invocations.Count);
        Assert.Equal("first", invocations[0].FileName);
        Assert.Equal(new[] { "--one" }, invocations[0].Arguments);
        Assert.Equal("second", invocations[1].FileName);
        Assert.Equal(new[] { "two words" }, invocations[1].Arguments);
    }

    [Fact]
    public void BuildInvocationsSkipsBlankCommands()
    {
        var settings = new ExternalCommandSettings { Commands = " \r\n\t" };

        Assert.Empty(ExternalCommand.BuildInvocations(settings));
    }

    [Fact]
    public void CreateStartInfoUsesDBuilderLaunchDefaults()
    {
        var invocation = new ExternalCommandInvocation(
            "/tools/buildnodes",
            new[] { "-o", "out.wad", "in.wad" },
            "/tmp/project");

        var startInfo = ExternalCommandLaunch.CreateStartInfo(invocation);

        Assert.Equal("/tools/buildnodes", startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardError);
        Assert.Equal("/tmp/project", startInfo.WorkingDirectory);
        Assert.Equal(new[] { "-o", "out.wad", "in.wad" }, startInfo.ArgumentList);
    }
}
