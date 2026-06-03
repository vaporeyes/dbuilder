// ABOUTME: Covers UDB-style required-archive warnings for resource location lists.
// ABOUTME: Uses synthetic game configuration text and resource metadata without loading archives.

using DBuilder.IO;

namespace DBuilder.Tests;

public class ResourceArchiveWarningModelTests
{
    [Fact]
    public void BuildWarningsReportsMissingRequiredArchives()
    {
        var config = GameConfiguration.FromText("""
            requiredarchives
            {
                gzdoom { filename = "gzdoom.pk3"; }
                actors { filename = "actors.pk3"; }
            }
            """);
        var resources = new DataLocationList
        {
            new(DataLocationType.Pk3, "/tmp/gzdoom.pk3")
            {
                RequiredArchives = new List<string> { "gzdoom" },
            },
            new(DataLocationType.Pk3, "/tmp/unknown.pk3")
            {
                RequiredArchives = null!,
            },
        };

        var warnings = ResourceArchiveWarningModel.BuildWarnings(config, resources);

        var warning = Assert.Single(warnings);
        Assert.Contains("Warning: a resource archive is required for this game configuration, but not present:", warning);
        Assert.Contains("\"actors.pk3\"", warning);
    }

    [Fact]
    public void BuildWarningsReportsDuplicateRequiredArchives()
    {
        var config = GameConfiguration.FromText("""
            requiredarchives
            {
                gzdoom { filename = "gzdoom.pk3"; }
            }
            """);
        var resources = new DataLocationList
        {
            new(DataLocationType.Pk3, "/tmp/gzdoom-a.pk3")
            {
                RequiredArchives = new List<string> { "gzdoom" },
            },
            new(DataLocationType.Pk3, "/tmp/gzdoom-b.pk3")
            {
                RequiredArchives = new List<string> { "gzdoom" },
            },
        };

        var warnings = ResourceArchiveWarningModel.BuildWarnings(config, resources);

        var warning = Assert.Single(warnings);
        Assert.Contains("Warning: required archive was added more than once:", warning);
        Assert.Contains("\"gzdoom.pk3\"", warning);
    }

    [Fact]
    public void BuildWarningsIgnoresAbsentConfiguration()
    {
        var warnings = ResourceArchiveWarningModel.BuildWarnings(null, new DataLocationList
        {
            new(DataLocationType.Pk3, "/tmp/gzdoom.pk3")
            {
                RequiredArchives = new List<string> { "gzdoom" },
            },
        });

        Assert.Empty(warnings);
    }

    [Fact]
    public void BuildWarningsReportsEmptyMapResourceListWhenRequested()
    {
        var warnings = ResourceArchiveWarningModel.BuildWarnings(
            null,
            new DataLocationList(),
            includeEmptyMapWarning: true);

        var warning = Assert.Single(warnings);
        Assert.Equal(
            "Warning: you are about to edit a map without any resources.\n"
            + "Textures, flats and sprites may not be shown correctly or may not show up at all.",
            warning);
    }

    [Fact]
    public void BuildWarningsSkipsEmptyResourceListWarningForConfigurationResources()
    {
        var warnings = ResourceArchiveWarningModel.BuildWarnings(
            null,
            new DataLocationList(),
            includeEmptyMapWarning: false);

        Assert.Empty(warnings);
    }
}
