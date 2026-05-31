// ABOUTME: Tests the legacy UDB object export settings model, defaults, and path validation.
// ABOUTME: Covers OBJ naming, fix-scale, texture export, and missing-directory behavior without UI.

using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class ObjectExportModelTests
{
    [Fact]
    public void DefaultFilePathMatchesUdbMapTitleAndLevelName()
    {
        string path = ObjectExportSettings.DefaultFilePath("/maps", "doom2", "MAP01");

        Assert.Equal(Path.Combine("/maps", "doom2_MAP01.obj"), path);
        Assert.Equal("doom2_MAP01", ObjectExportSettings.DefaultFileName("doom2", "MAP01"));
        Assert.Equal(".obj", ObjectExportSettings.DefaultExtension);
        Assert.Equal("Wavefront obj files|*.obj", ObjectExportSettings.DialogFilter);
        Assert.Equal("Select save location:", ObjectExportSettings.DialogTitle);
    }

    [Fact]
    public void FromOptionsTrimsPathAndKeepsCheckboxValues()
    {
        ObjectExportSettings settings = ObjectExportSettings.FromOptions(new ObjectExportOptions(
            "  /tmp/map.obj  ",
            FixScale: true,
            ExportTextures: true));

        Assert.Equal("/tmp/map.obj", settings.FilePath);
        Assert.True(settings.FixScale);
        Assert.True(settings.ExportTextures);
    }

    [Fact]
    public void ValidateRequiresExistingDirectoryLikeUdbForm()
    {
        IReadOnlyList<string> errors = ObjectExportSettings.Validate(
            new ObjectExportOptions("/missing/map.obj"),
            directory => directory == "/present");

        Assert.Equal(new[] { ObjectExportSettings.InvalidPathMessage }, errors);

        Assert.Empty(ObjectExportSettings.Validate(
            new ObjectExportOptions("/present/map.obj"),
            directory => directory == "/present"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("map.obj")]
    [InlineData("   ")]
    public void ValidateRejectsBlankOrDirectorylessPaths(string path)
    {
        IReadOnlyList<string> errors = ObjectExportSettings.Validate(
            new ObjectExportOptions(path),
            _ => true);

        Assert.Equal(new[] { ObjectExportSettings.InvalidPathMessage }, errors);
    }
}
