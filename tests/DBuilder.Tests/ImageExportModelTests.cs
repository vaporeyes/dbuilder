// ABOUTME: Tests UDB BuilderModes image export setting defaults and form index mappings.
// ABOUTME: Covers image format, pixel format, scale, extension, and default output path behavior.

using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class ImageExportModelTests
{
    [Fact]
    public void FromOptionsMapsUdbFormFieldsToExportSettings()
    {
        ImageExportSettings settings = ImageExportSettings.FromOptions(new ImageExportOptions(
            " " + Path.Combine("export", "MAP01.png") + " ",
            Floor: false,
            Fullbright: false,
            ApplySectorColors: false,
            Brightmap: true,
            Transparency: true,
            Tiles: true,
            ScaleIndex: 2,
            ImageFormatIndex: 1,
            PixelFormatIndex: 2));

        Assert.Equal("export", settings.Directory);
        Assert.Equal("MAP01", settings.Name);
        Assert.Equal(".png", settings.Extension);
        Assert.False(settings.Floor);
        Assert.False(settings.Fullbright);
        Assert.False(settings.ApplySectorColors);
        Assert.True(settings.Brightmap);
        Assert.True(settings.Transparency);
        Assert.True(settings.Tiles);
        Assert.Equal(4.0f, settings.Scale);
        Assert.Equal(ImageExportPixelFormat.Format16BppRgb555, settings.PixelFormat);
        Assert.Equal(ImageExportImageFormat.Jpeg, settings.ImageFormat);
    }

    [Theory]
    [InlineData(0, ImageExportImageFormat.Png, ".png")]
    [InlineData(1, ImageExportImageFormat.Jpeg, ".jpg")]
    [InlineData(5, ImageExportImageFormat.Png, ".png")]
    public void ImageFormatIndexMatchesUdbSelection(int index, ImageExportImageFormat expectedFormat, string expectedExtension)
    {
        Assert.Equal(expectedFormat, ImageExportSettings.ImageFormatFromIndex(index));
        Assert.Equal(expectedExtension, ImageExportSettings.ExtensionForFormatIndex(index));
    }

    [Theory]
    [InlineData(0, ImageExportPixelFormat.Format32BppArgb)]
    [InlineData(1, ImageExportPixelFormat.Format24BppRgb)]
    [InlineData(2, ImageExportPixelFormat.Format16BppRgb555)]
    [InlineData(7, ImageExportPixelFormat.Format32BppArgb)]
    public void PixelFormatIndexMatchesUdbSelection(int index, ImageExportPixelFormat expected)
        => Assert.Equal(expected, ImageExportSettings.PixelFormatFromIndex(index));

    [Theory]
    [InlineData(0, 1.0f)]
    [InlineData(1, 2.0f)]
    [InlineData(2, 4.0f)]
    [InlineData(3, 8.0f)]
    [InlineData(-1, 1.0f)]
    public void ScaleIndexUsesPowersOfTwoLikeUdbCombo(int index, float expected)
        => Assert.Equal(expected, ImageExportSettings.ScaleFromIndex(index));

    [Fact]
    public void FormatSelectionChangesFileExtensionLikeUdbBrowseHandler()
    {
        Assert.Equal(Path.Combine("tmp", "map.jpg"), ImageExportSettings.ChangeExtensionForFormat(Path.Combine("tmp", "map.png"), 1));
        Assert.Equal(Path.Combine("tmp", "map.png"), ImageExportSettings.ChangeExtensionForFormat(Path.Combine("tmp", "map.jpg"), 0));
    }

    [Fact]
    public void DefaultOutputPathUsesMapDirectoryAndGeneratedPngName()
    {
        string path = ImageExportSettings.DefaultFilePath(
            Path.Combine("maps", "doom2.wad"),
            "doom2.wad",
            "MAP01",
            "abc123.tmp");

        Assert.Equal(Path.Combine("maps", "doom2_MAP01_abc123.png"), path);
        Assert.Equal(
            "doom2_MAP01_abc123",
            ImageExportSettings.DefaultFilePath(null, "doom2.wad", "MAP01", "abc123.tmp"));
    }
}
