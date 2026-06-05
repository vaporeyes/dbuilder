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
        : base(ObjectExportSettings.FormTitle, ObjectExportSettings.FormDescription)
    {
        ResultOptions = options;
        _filePath = AddField(ObjectExportSettings.PathLabel, options.FilePath);
        _fixScale = AddCheckBox(ObjectExportSettings.FixScaleText, options.FixScale);
        _exportTextures = AddCheckBox(ObjectExportSettings.ExportTexturesText, options.ExportTextures);
    }

    protected override void OnConfirm()
    {
        ResultOptions = new ObjectExportOptions(
            _filePath.Text?.Trim() ?? "",
            FixScale: _fixScale.IsChecked == true,
            ExportTextures: _exportTextures.IsChecked == true);
    }
}
