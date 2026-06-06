// ABOUTME: Tests source-port argument template parsing and %IWAD/%FO/%MAP substitution for Test Map.
// ABOUTME: Ensures quoted paths with spaces stay single tokens and tokens substitute correctly.

using DBuilder.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
    public void UdbLinuxPathSettingConvertsLaunchResourcePaths()
    {
        string? winePrefix = Environment.GetEnvironmentVariable("WINEPREFIX");
        try
        {
            Environment.SetEnvironmentVariable("WINEPREFIX", null);
            var args = SourcePort.BuildArgs(
                "-iwad %WP -iwadfile %WF -file \"%AP\" \"%F\"",
                @"C:\Games\doom2.wad",
                @"Z:\tmp\dbuilder_test_MAP01.wad",
                "MAP01",
                new[] { @"C:\Mods\texture pack.pk3", @"Z:\home\user\music.wad" },
                linuxPaths: true);

            Assert.Equal(
                new[]
                {
                    "-iwad",
                    "/drive_c/Games/doom2.wad",
                    "-iwadfile",
                    "doom2.wad",
                    "-file",
                    "/drive_c/Mods/texture pack.pk3",
                    "/home/user/music.wad",
                    "/tmp/dbuilder_test_MAP01.wad",
                },
                args);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WINEPREFIX", winePrefix);
        }
    }

    [Fact]
    public void UdbShortPathSettingTakesPrecedenceOverLinuxPaths()
    {
        var args = SourcePort.BuildArgs(
            "-iwad %WP -file \"%F\"",
            @"C:\Games\doom2.wad",
            @"Z:\tmp\dbuilder_test_MAP01.wad",
            "MAP01",
            shortPaths: true,
            linuxPaths: true);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.DoesNotContain("/drive_c", args);
            Assert.DoesNotContain("/tmp/dbuilder_test_MAP01.wad", args);
        }
        else
        {
            Assert.Equal(new[] { "-iwad", @"C:\Games\doom2.wad", "-file", @"Z:\tmp\dbuilder_test_MAP01.wad" }, args);
        }
    }

    [Fact]
    public void BuildAdditionalResourcePathsCombinesConfigAndMapResourcesLikeUdb()
    {
        using var dir = new TempDir();
        string iwad = dir.File("doom2.wad");
        string configResource = dir.File("config.pk3");
        string mapResource = dir.File("map.wad");
        string skippedResource = dir.File("skip.pk3");
        File.WriteAllText(iwad, "");
        File.WriteAllText(configResource, "");
        File.WriteAllText(mapResource, "");
        File.WriteAllText(skippedResource, "");

        var resources = SourcePort.BuildAdditionalResourcePaths(
            new[]
            {
                new DataLocation(DataLocationType.Wad, iwad),
                new DataLocation(DataLocationType.Pk3, configResource),
                new DataLocation(DataLocationType.Pk3, skippedResource, notForTesting: true),
            },
            new[]
            {
                new DataLocation(DataLocationType.Wad, mapResource),
                new DataLocation(DataLocationType.Pk3, dir.File("missing.pk3")),
            },
            iwad);

        Assert.Equal(new[] { configResource, mapResource }, resources);
    }

    [Fact]
    public void BuildAdditionalResourcePathsLetsMapResourcesOverrideConfigResources()
    {
        using var dir = new TempDir();
        string shared = dir.File("shared.pk3");
        File.WriteAllText(shared, "");

        var resources = SourcePort.BuildAdditionalResourcePaths(
            new[] { new DataLocation(DataLocationType.Pk3, shared, notForTesting: true) },
            new[] { new DataLocation(DataLocationType.Pk3, shared) },
            iwad: dir.File("doom2.wad"));

        Assert.Equal(new[] { shared }, resources);
    }

    [Fact]
    public void BuildAdditionalResourcePathsAddsCurrentMapArchiveLastLikeUdb()
    {
        using var dir = new TempDir();
        string configResource = dir.File("config.pk3");
        string mapResource = dir.File("map.wad");
        string currentMap = dir.File("current.wad");
        File.WriteAllText(configResource, "");
        File.WriteAllText(mapResource, "");
        File.WriteAllText(currentMap, "");

        var resources = SourcePort.BuildAdditionalResourcePaths(
            new[] { new DataLocation(DataLocationType.Pk3, configResource) },
            new[] { new DataLocation(DataLocationType.Wad, mapResource) },
            iwad: dir.File("doom2.wad"),
            currentMapPath: currentMap);

        Assert.Equal(new[] { configResource, mapResource, currentMap }, resources);
    }

    [Fact]
    public void BuildAdditionalResourcePathsCurrentMapArchiveReplacesDuplicateResource()
    {
        using var dir = new TempDir();
        string currentMap = dir.File("current.wad");
        File.WriteAllText(currentMap, "");

        var resources = SourcePort.BuildAdditionalResourcePaths(
            Array.Empty<DataLocation>(),
            new[] { new DataLocation(DataLocationType.Wad, currentMap, notForTesting: true) },
            iwad: dir.File("doom2.wad"),
            currentMapPath: currentMap);

        Assert.Equal(new[] { currentMap }, resources);
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

    private sealed class TempDir : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "dbuilder-sourceport-" + Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(path);
        }

        public string File(string name)
            => Path.Combine(path, name);

        public void Dispose()
        {
            try { Directory.Delete(path, recursive: true); }
            catch { }
        }
    }
}
