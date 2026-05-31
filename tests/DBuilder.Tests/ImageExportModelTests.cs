// ABOUTME: Tests UDB BuilderModes image export setting defaults and form index mappings.
// ABOUTME: Covers image format, pixel format, scale, extension, and default output path behavior.

using DBuilder.Map;
using DBuilder.Geometry;

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

    [Fact]
    public void PluginSettingsUseUdbKeysAndDefaults()
    {
        ImageExportPluginSettings settings = ImageExportPluginSettings.FromDictionary(new Dictionary<string, object?>());

        Assert.True(settings.Fullbright);
        Assert.True(settings.ApplySectorColors);
        Assert.False(settings.Brightmap);
        Assert.False(settings.Transparency);
        Assert.False(settings.Tiles);
        Assert.Equal(0, settings.ScaleIndex);
    }

    [Fact]
    public void PluginSettingsReadUdbPersistedValues()
    {
        var source = new Dictionary<string, object?>
        {
            [ImageExportPluginSettings.FullbrightKey] = false,
            [ImageExportPluginSettings.ApplySectorColorsKey] = false,
            [ImageExportPluginSettings.BrightmapKey] = true,
            [ImageExportPluginSettings.TransparencyKey] = true,
            [ImageExportPluginSettings.TilesKey] = true,
            [ImageExportPluginSettings.ScaleKey] = 3,
        };

        ImageExportPluginSettings settings = ImageExportPluginSettings.FromDictionary(source);

        Assert.False(settings.Fullbright);
        Assert.False(settings.ApplySectorColors);
        Assert.True(settings.Brightmap);
        Assert.True(settings.Transparency);
        Assert.True(settings.Tiles);
        Assert.Equal(3, settings.ScaleIndex);
    }

    [Fact]
    public void PluginSettingsWriteUdbPersistedValues()
    {
        var target = new Dictionary<string, object?>();
        var settings = new ImageExportPluginSettings(
            Fullbright: false,
            ApplySectorColors: false,
            Brightmap: true,
            Transparency: true,
            Tiles: true,
            ScaleIndex: 2);

        settings.WriteTo(target);

        Assert.Equal(false, target[ImageExportPluginSettings.FullbrightKey]);
        Assert.Equal(false, target[ImageExportPluginSettings.ApplySectorColorsKey]);
        Assert.Equal(true, target[ImageExportPluginSettings.BrightmapKey]);
        Assert.Equal(true, target[ImageExportPluginSettings.TransparencyKey]);
        Assert.Equal(true, target[ImageExportPluginSettings.TilesKey]);
        Assert.Equal(2, target[ImageExportPluginSettings.ScaleKey]);
    }

    [Theory]
    [InlineData(ImageExportResult.OK, "Export to image", "Export successful.", false)]
    [InlineData(ImageExportResult.Canceled, "Export to image", "Export canceled.", false)]
    [InlineData(ImageExportResult.OutOfMemory, "Export failed", "Exporting failed. There's likely not enough consecutive free memory to create the image. Try a lower color depth or file format", true)]
    [InlineData(ImageExportResult.ImageTooBig, "Export failed", "Exporting failed. The image is likely too big for the current settings. Try a lower color depth or file format", true)]
    public void ResultMessagesMatchUdbStopExportDialogs(
        ImageExportResult result,
        string expectedTitle,
        string expectedMessage,
        bool expectedIsError)
    {
        ImageExportResultMessage message = ImageExportResultMessage.FromResult(result);

        Assert.Equal(expectedTitle, message.Title);
        Assert.Equal(expectedMessage, message.Message);
        Assert.Equal(expectedIsError, message.IsError);
    }

    [Fact]
    public void PlannerUsesUdbSelectionBoundsAndTopLeftOffset()
    {
        Sector sector = BuildSector((10, 20), (138, 20), (138, -44), (10, -44));

        ImageExportLayout layout = ImageExportPlanner.GetLayout([sector]);

        Assert.Equal(new Vector2D(128, 64), layout.Size);
        Assert.Equal(new Vector2D(10, 20), layout.Offset);
    }

    [Fact]
    public void PlannerCountsScaledTilesLikeUdbExporter()
    {
        Sector sector = BuildSector((0, 0), (96, 0), (96, -96), (0, -96));
        ImageExportSettings settings = ImageExportSettings.FromOptions(new ImageExportOptions(
            Path.Combine("export", "MAP01.png"),
            Tiles: true,
            ScaleIndex: 1));

        ImageExportOutputPlan plan = ImageExportPlanner.CreateOutputPlan([sector], settings);

        Assert.Equal(9, plan.TileCount);
    }

    [Fact]
    public void PlannerCountsProgressItemsLikeUdbExporter()
    {
        Sector sector = BuildSector((64, 0), (0, 0), (0, 64), (64, 64));
        ImageExportSettings settings = ImageExportSettings.FromOptions(new ImageExportOptions(
            Path.Combine("export", "MAP01.png"),
            Brightmap: true,
            Tiles: true));

        ImageExportOutputPlan plan = ImageExportPlanner.CreateOutputPlan([sector], settings);

        Assert.Equal(6, plan.ProgressItems);
    }

    [Fact]
    public void SelectSectorsForExportUsesAllSectorsWhenNothingIsSelected()
    {
        var map = new MapSet();
        Sector first = BuildSector((0, 0), (64, 0), (64, -64), (0, -64));
        Sector second = BuildSector((128, 0), (192, 0), (192, -64), (128, -64));
        map.Sectors.Add(first);
        map.Sectors.Add(second);

        ImageExportSectorSelection selection = ImageExportPlanner.SelectSectorsForExport(map);

        Assert.True(selection.CanExport);
        Assert.Null(selection.Warning);
        Assert.Equal([first, second], selection.Sectors);
    }

    [Fact]
    public void SelectSectorsForExportUsesSelectedSectorsWhenAnyAreSelected()
    {
        var map = new MapSet();
        Sector first = BuildSector((0, 0), (64, 0), (64, -64), (0, -64));
        Sector second = BuildSector((128, 0), (192, 0), (192, -64), (128, -64));
        second.Selected = true;
        map.Sectors.Add(first);
        map.Sectors.Add(second);

        ImageExportSectorSelection selection = ImageExportPlanner.SelectSectorsForExport(map);

        Assert.True(selection.CanExport);
        Assert.Null(selection.Warning);
        Assert.Equal([second], selection.Sectors);
    }

    [Fact]
    public void SelectSectorsForExportReportsUdbWarningForEmptyMaps()
    {
        ImageExportSectorSelection selection = ImageExportPlanner.SelectSectorsForExport(new MapSet());

        Assert.False(selection.CanExport);
        Assert.Equal(ImageExportPlanner.NoSectorsWarning, selection.Warning);
        Assert.Empty(selection.Sectors);
    }

    [Fact]
    public void PlannerListsUntiledNormalAndBrightmapNames()
    {
        Sector sector = BuildSector((0, 0), (64, 0), (64, -64), (0, -64));
        ImageExportSettings settings = ImageExportSettings.FromOptions(new ImageExportOptions(
            Path.Combine("export", "MAP01.jpg"),
            Brightmap: true,
            ImageFormatIndex: 1));

        ImageExportOutputPlan plan = ImageExportPlanner.CreateOutputPlan([sector], settings);

        Assert.Equal(
            [
                Path.Combine("export", "MAP01.jpg"),
                Path.Combine("export", "MAP01_brightmap.jpg")
            ],
            plan.ImageNames);
    }

    [Fact]
    public void PlannerListsTiledNamesInUdbReportedOrder()
    {
        Sector sector = BuildSector((0, 0), (128, 0), (128, -64), (0, -64));
        ImageExportSettings settings = ImageExportSettings.FromOptions(new ImageExportOptions(
            Path.Combine("export", "MAP01.png"),
            Brightmap: true,
            Tiles: true));

        ImageExportOutputPlan plan = ImageExportPlanner.CreateOutputPlan([sector], settings);

        Assert.Equal(
            [
                Path.Combine("export", "MAP011.png"),
                Path.Combine("export", "MAP012.png"),
                Path.Combine("export", "MAP011_brightmap.png"),
                Path.Combine("export", "MAP012_brightmap.png")
            ],
            plan.ImageNames);
    }

    private static Sector BuildSector(params (double X, double Y)[] points)
    {
        var sector = new Sector();
        var vertices = points.Select(point => new Vertex(new Vector2D(point.X, point.Y))).ToArray();

        for (int i = 0; i < vertices.Length; i++)
        {
            var line = new Linedef(vertices[i], vertices[(i + 1) % vertices.Length]);
            var side = new Sidedef(line, true) { Sector = sector };
            line.Front = side;
            line.Start.Linedefs.Add(line);
            line.End.Linedefs.Add(line);
            sector.Sidedefs.Add(side);
        }

        return sector;
    }
}
