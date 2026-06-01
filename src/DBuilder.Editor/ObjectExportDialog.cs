// ABOUTME: Modal dialog for configuring the legacy UDB object OBJ export.
// ABOUTME: Collects path, GZDoom scale fix, and texture export options.

using Avalonia.Controls;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class ObjectExportDialog : PropertyDialog
{
    private readonly TextBox _filePath;
    private readonly CheckBox _fixScale;
    private readonly CheckBox _exportTextures;

    public ObjectExportOptions ResultOptions { get; private set; }

    public ObjectExportDialog(ObjectExportOptions options)
        : base("Export to Wavefront .obj", "Exports selected sectors, or the whole map, using the legacy object exporter.")
    {
        ResultOptions = options;
        _filePath = AddField("OBJ path", options.FilePath);
        _fixScale = AddCheckBox("Export for GZDoom (Fix Vertical Scale)", options.FixScale);
        _exportTextures = AddCheckBox("Export textures", options.ExportTextures);
    }

    protected override void OnConfirm()
    {
        ResultOptions = new ObjectExportOptions(
            _filePath.Text?.Trim() ?? "",
            FixScale: _fixScale.IsChecked == true,
            ExportTextures: _exportTextures.IsChecked == true);
    }
}
