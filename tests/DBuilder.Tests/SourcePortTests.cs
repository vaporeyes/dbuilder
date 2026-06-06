// ABOUTME: Tests source-port argument template parsing and %IWAD/%FO/%MAP substitution for Test Map.
// ABOUTME: Ensures quoted paths with spaces stay single tokens and tokens substitute correctly.

using DBuilder.IO;
using System.Diagnostics;

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

    [Fact]
    public void UdbModernTemplateSubstitutesConfiguredTokens()
    {
        var args = SourcePort.BuildArgs("-iwad \"%WP\" -skill \"%S\" -file \"%AP\" \"%F\" +map %L %NM",
            "doom2.wad", "edit.wad", "MAP07");

        Assert.Equal(new[] { "-iwad", "doom2.wad", "-skill", "3", "-file", "edit.wad", "+map", "MAP07" }, args);
    }

    [Fact]
    public void UdbTokenCaseVariantsNormalizeBeforeSubstitution()
    {
        var args = SourcePort.BuildArgs("-iwad %wp -iwadfile %Wf -skill %s -file %ap %f -warp %l1 %l2",
            "/games/doom2.wad", "edit.wad", "MAP11", new[] { "textures.pk3" });

        Assert.Equal(new[] { "-iwad", "/games/doom2.wad", "-iwadfile", "doom2.wad", "-skill", "3", "-file", "textures.pk3", "edit.wad", "-warp", "11" }, args);
    }

    [Fact]
    public void DBuilderTemplateTokenCaseVariantsNormalizeBeforeSubstitution()
    {
        var args = SourcePort.BuildArgs("-iwad %iwad -file %Fo +map %mAp",
            "/games/doom2.wad", "edit.wad", "MAP07");

        Assert.Equal(new[] { "-iwad", "/games/doom2.wad", "-file", "edit.wad", "+map", "MAP07" }, args);
    }

    [Fact]
    public void TokenCaseNormalizationKeepsUnknownPlaceholderPrefixes()
    {
        var args = SourcePort.BuildArgs("-file %foobar %mapextra",
            "doom2.wad", "edit.wad", "MAP07");

        Assert.Equal(new[] { "-file", "%foobar", "%mapextra" }, args);
    }

    [Fact]
    public void UdbNoMonstersTokenExpandsWhenTestingWithoutMonsters()
    {
        var args = SourcePort.BuildArgs("-file \"%F\" +map %L %nm",
            "doom2.wad", "edit.wad", "MAP07", testMonsters: false);

        Assert.Equal(new[] { "-file", "edit.wad", "+map", "MAP07", "-nomonsters" }, args);
    }

    [Fact]
    public void UdbSkillTokenUsesRequestedTestSkill()
    {
        var args = SourcePort.BuildArgs("-skill %S -file \"%F\"",
            "doom2.wad", "edit.wad", "MAP07", skill: 5);

        Assert.Equal(new[] { "-skill", "5", "-file", "edit.wad" }, args);
    }

    [Fact]
    public void UdbAdditionalFilesExpandAsSeparateFileArguments()
    {
        var args = SourcePort.BuildArgs("-file \"%AP\" \"%F\"",
            "doom2.wad", "edit.wad", "MAP01", new[] { "textures.pk3", "/tmp/music pack.wad" });

        Assert.Equal(new[] { "-file", "textures.pk3", "/tmp/music pack.wad", "edit.wad" }, args);
    }

    [Fact]
    public void UdbAdditionalParametersAppendAfterTemplateArguments()
    {
        var args = SourcePort.BuildArgs("-file \"%F\" +map %L",
            "doom2.wad", "edit.wad", "MAP01", additionalParameters: "+set test_value \"value with spaces\" -host 2");

        Assert.Equal(new[] { "-file", "edit.wad", "+map", "MAP01", "+set", "test_value", "value with spaces", "-host", "2" }, args);
    }

    [Fact]
    public void UdbVanillaMapxxTemplateBuildsTwoDigitWarp()
    {
        var args = SourcePort.BuildArgs("-warp %L1%L2", "doom2.wad", "edit.wad", "MAP11");

        Assert.Equal(new[] { "-warp", "11" }, args);
    }

    [Fact]
    public void UdbVanillaExmxTemplateBuildsEpisodeAndMapWarp()
    {
        var args = SourcePort.BuildArgs("-warp %L1 %L2", "doom.wad", "edit.wad", "E2M8");

        Assert.Equal(new[] { "-warp", "2", "8" }, args);
    }

    [Fact]
    public void UdbMapNumberTokensUseFirstTwoNumericGroups()
    {
        var args = SourcePort.BuildArgs("-warp %L1 %L2", "doom.wad", "edit.wad", "HUB02_MAP03");

        Assert.Equal(new[] { "-warp", "2", "3" }, args);
    }

    [Fact]
    public void CreateStartInfoUsesDBuilderLaunchDefaults()
    {
        var startInfo = SourcePort.CreateStartInfo("/ports/gzdoom", new[] { "-iwad", "doom2.wad", "-file", "edit.wad" });

        Assert.Equal("/ports/gzdoom", startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal("/ports", startInfo.WorkingDirectory);
        Assert.Equal(new[] { "-iwad", "doom2.wad", "-file", "edit.wad" }, startInfo.ArgumentList);
    }

    [Fact]
    public void CreateStartInfoLeavesWorkingDirectoryEmptyForPathlessExecutable()
    {
        var startInfo = SourcePort.CreateStartInfo("gzdoom", new[] { "-iwad", "doom2.wad" });

        Assert.Equal("gzdoom", startInfo.FileName);
        Assert.Equal("", startInfo.WorkingDirectory);
    }

    [Fact]
    public void LaunchUsesMockedProcessExecution()
    {
        ProcessStartInfo? captured = null;

        SourcePortLaunchResult result = SourcePort.Launch(
            "/ports/gzdoom",
            new[] { "-iwad", "doom2.wad", "-file", "edit.wad" },
            startInfo =>
            {
                captured = startInfo;
                return true;
            });

        Assert.True(result.Success, result.Message);
        Assert.Equal("Source port launched.", result.Message);
        Assert.NotNull(captured);
        Assert.Equal("/ports/gzdoom", captured.FileName);
        Assert.Equal(new[] { "-iwad", "doom2.wad", "-file", "edit.wad" }, captured.ArgumentList);
        Assert.Same(captured, result.StartInfo);
    }

    [Fact]
    public void LaunchReportsFailedMockedProcessStart()
    {
        SourcePortLaunchResult result = SourcePort.Launch(
            "/ports/gzdoom",
            new[] { "-iwad", "doom2.wad" },
            _ => false);

        Assert.False(result.Success);
        Assert.Equal("Source port launch failed: process did not start.", result.Message);
    }

    [Fact]
    public void LaunchReportsMockedProcessException()
    {
        SourcePortLaunchResult result = SourcePort.Launch(
            "/ports/gzdoom",
            new[] { "-iwad", "doom2.wad" },
            _ => throw new InvalidOperationException("blocked"));

        Assert.False(result.Success);
        Assert.Equal("Source port launch failed: blocked", result.Message);
    }
}
