// ABOUTME: Modal dialog for configuring image export from the editor.
// ABOUTME: Collects the image export options consumed by the data-level rasterizer.

using Avalonia.Controls;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class ImageExportDialog : PropertyDialog
{
    private readonly TextBox _filePath;
    private readonly CheckBox _floor;
    private readonly CheckBox _fullbright;
    private readonly CheckBox _applySectorColors;
    private readonly CheckBox _brightmap;
    private readonly CheckBox _transparency;
    private readonly CheckBox _tiles;
    private readonly ComboBox _imageFormat;
    private readonly ComboBox _pixelFormat;
    private readonly ComboBox _scale;

    public ImageExportOptions ResultOptions { get; private set; }

    public ImageExportDialog(ImageExportOptions options)
        : base(ImageExportSettings.FormTitle, ImageExportSettings.FormDescription)
    {
        ResultOptions = options;
        _filePath = AddField(ImageExportSettings.PathLabel, options.FilePath);
        _floor = AddCheckBox(ImageExportSettings.FloorText, options.Floor);
        _fullbright = AddCheckBox(ImageExportSettings.FullbrightText, options.Fullbright);
        _applySectorColors = AddCheckBox(ImageExportSettings.ApplySectorColorsText, options.ApplySectorColors);
        _brightmap = AddCheckBox(ImageExportSettings.BrightmapText, options.Brightmap);
        _transparency = AddCheckBox(ImageExportSettings.TransparencyText, options.Transparency);
        _tiles = AddCheckBox(ImageExportSettings.TilesText, options.Tiles);
        _imageFormat = AddCombo(ImageExportSettings.ImageFormatLabel, ImageFormatItems(), Math.Max(0, options.ImageFormatIndex));
        _imageFormat.SelectionChanged += (_, _) => SyncPathExtensionToImageFormat();
        _pixelFormat = AddCombo(ImageExportSettings.PixelFormatLabel, PixelFormatItems(), Math.Max(0, options.PixelFormatIndex));
        _scale = AddCombo(ImageExportSettings.ScaleLabel, ScaleItems(), Math.Max(0, options.ScaleIndex));
    }

    protected override void OnConfirm()
    {
        ResultOptions = new ImageExportOptions(
            _filePath.Text?.Trim() ?? "",
            Floor: _floor.IsChecked == true,
            Fullbright: _fullbright.IsChecked == true,
            ApplySectorColors: _applySectorColors.IsChecked == true,
            Brightmap: _brightmap.IsChecked == true,
            Transparency: _transparency.IsChecked == true,
            Tiles: _tiles.IsChecked == true,
            ScaleIndex: ComboNumber(_scale, ResultOptions.ScaleIndex),
            ImageFormatIndex: ComboNumber(_imageFormat, ResultOptions.ImageFormatIndex),
            PixelFormatIndex: ComboNumber(_pixelFormat, ResultOptions.PixelFormatIndex));
    }

    private void SyncPathExtensionToImageFormat()
    {
        string path = _filePath.Text?.Trim() ?? "";
        if (path.Length == 0) return;
        _filePath.Text = ImageExportSettings.ChangeExtensionForFormat(
            path,
            ComboNumber(_imageFormat, ResultOptions.ImageFormatIndex));
    }

    private static IEnumerable<CatalogItem> ImageFormatItems()
    {
        yield return new CatalogItem(0, ImageExportSettings.PngText);
        yield return new CatalogItem(1, ImageExportSettings.JpgText);
    }

    private static IEnumerable<CatalogItem> PixelFormatItems()
    {
        yield return new CatalogItem(0, ImageExportSettings.Format32BitText);
        yield return new CatalogItem(1, ImageExportSettings.Format24BitText);
        yield return new CatalogItem(2, ImageExportSettings.Format16BitText);
    }

    private static IEnumerable<CatalogItem> ScaleItems()
    {
        for (int i = 0; i < ImageExportSettings.ScaleTexts.Length; i++)
            yield return new CatalogItem(i, ImageExportSettings.ScaleTexts[i]);
    }
}
