// ABOUTME: Modal dialog for configuring PNG image export from the editor.
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
            ImageFormatIndex: 0,
            PixelFormatIndex: 0);
    }

    private static IEnumerable<CatalogItem> ScaleItems()
    {
        for (int i = 0; i < ImageExportSettings.ScaleTexts.Length; i++)
            yield return new CatalogItem(i, ImageExportSettings.ScaleTexts[i]);
    }
}
