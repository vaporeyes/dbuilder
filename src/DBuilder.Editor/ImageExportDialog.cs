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
        : base("Export Image PNG", "Exports selected sectors, or the whole map, as a PNG image.")
    {
        ResultOptions = options;
        _filePath = AddField("PNG path", options.FilePath);
        _floor = AddCheckBox("Export floor flats", options.Floor);
        _fullbright = AddCheckBox("Fullbright", options.Fullbright);
        _applySectorColors = AddCheckBox("Apply sector colors", options.ApplySectorColors);
        _brightmap = AddCheckBox("Generate brightmap", options.Brightmap);
        _transparency = AddCheckBox("Transparent background", options.Transparency);
        _tiles = AddCheckBox("Export 64x64 tiles", options.Tiles);
        _scale = AddCombo("Scale", ScaleItems(), Math.Max(0, options.ScaleIndex));
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
        yield return new CatalogItem(0, "1x");
        yield return new CatalogItem(1, "2x");
        yield return new CatalogItem(2, "4x");
        yield return new CatalogItem(3, "8x");
    }
}
