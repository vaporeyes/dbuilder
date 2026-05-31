// ABOUTME: Modal dialog for configuring idStudio map export from the editor.
// ABOUTME: Collects the mod path, map name and texture export options consumed by the data-level exporter.

using System.Globalization;
using Avalonia.Controls;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class IdStudioExportDialog : PropertyDialog
{
    private readonly TextBox _modPath;
    private readonly TextBox _mapName;
    private readonly TextBox _downscale;
    private readonly TextBox _xShift;
    private readonly TextBox _yShift;
    private readonly TextBox _zShift;
    private readonly CheckBox _exportTextures;
    private readonly CheckBox _exportAllTextures;

    public IdStudioExportOptions ResultOptions { get; private set; }

    public IdStudioExportDialog(IdStudioExportOptions options)
        : base("Export idStudio", "Exports the current map as idStudio refmap and material files.")
    {
        ResultOptions = options;
        _modPath = AddField("Mod path", options.ModPath);
        _mapName = AddField("Map name", options.MapName);
        _downscale = AddField("Downscale", options.Downscale.ToString(CultureInfo.InvariantCulture));
        _xShift = AddField("X shift", options.XShift.ToString(CultureInfo.InvariantCulture));
        _yShift = AddField("Y shift", options.YShift.ToString(CultureInfo.InvariantCulture));
        _zShift = AddField("Z shift", options.ZShift.ToString(CultureInfo.InvariantCulture));
        _exportTextures = AddCheckBox("Export textures", options.ExportTextures);
        _exportAllTextures = AddCheckBox("Export all textures", options.ExportAllTextures);
    }

    protected override void OnConfirm()
    {
        ResultOptions = new IdStudioExportOptions
        {
            ModPath = _modPath.Text?.Trim() ?? "",
            MapName = (_mapName.Text?.Trim() ?? "").ToLowerInvariant(),
            Downscale = ParseFloat(_downscale, ResultOptions.Downscale),
            XShift = ParseFloat(_xShift, ResultOptions.XShift),
            YShift = ParseFloat(_yShift, ResultOptions.YShift),
            ZShift = ParseFloat(_zShift, ResultOptions.ZShift),
            ExportTextures = _exportTextures.IsChecked == true,
            ExportAllTextures = _exportAllTextures.IsChecked == true,
        };
    }

    private static float ParseFloat(TextBox box, float fallback)
        => float.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
}
