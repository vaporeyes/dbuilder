// ABOUTME: Modal dialog for configuring Wavefront OBJ export from the editor.
// ABOUTME: Collects classic and GZDoom export options consumed by the data-level exporter.

using System.Globalization;
using Avalonia.Controls;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class WavefrontExportDialog : PropertyDialog
{
    private readonly TextBox _filePath;
    private readonly TextBox _scale;
    private readonly CheckBox _exportForGzdoom;
    private readonly CheckBox _exportTextures;
    private readonly TextBox _actorName;
    private readonly TextBox _basePath;
    private readonly TextBox _actorPath;
    private readonly TextBox _modelPath;
    private readonly TextBox _sprite;
    private readonly TextBox _skipTextures;
    private readonly CheckBox _ignoreControlSectors;
    private readonly CheckBox _normalizeLowestVertex;
    private readonly CheckBox _centerModel;
    private readonly CheckBox _zScript;
    private readonly CheckBox _generateCode;
    private readonly CheckBox _generateModeldef;
    private readonly CheckBox _noGravity;
    private readonly CheckBox _spawnOnCeiling;
    private readonly CheckBox _solid;

    public WavefrontExportOptions ResultOptions { get; private set; }

    public WavefrontExportDialog(WavefrontExportOptions options)
        : base("Export Wavefront OBJ", "Exports selected sectors, or the whole map, as Wavefront OBJ geometry.")
    {
        ResultOptions = options;
        _filePath = AddField("OBJ path", options.FilePath);
        _scale = AddField("Scale", options.Scale.ToString(CultureInfo.InvariantCulture));
        _exportForGzdoom = AddCheckBox("Export for GZDoom", options.ExportForGZDoom);
        _exportTextures = AddCheckBox("Export textures", options.ExportTextures);
        _actorName = AddField("Actor name", options.ActorName);
        _basePath = AddField("Base path", options.BasePath);
        _actorPath = AddField("Actor path", options.ActorPath);
        _modelPath = AddField("Model path", options.ModelPath);
        _sprite = AddField("Sprite", options.Sprite);
        _skipTextures = AddField("Skip textures", string.Join(", ", options.SkipTextures));
        _ignoreControlSectors = AddCheckBox("Ignore 3D floor control sectors", options.IgnoreControlSectors);
        _normalizeLowestVertex = AddCheckBox("Normalize lowest vertex", options.NormalizeLowestVertex);
        _centerModel = AddCheckBox("Center model", options.CenterModel);
        _zScript = AddCheckBox("Generate ZScript", options.ZScript);
        _generateCode = AddCheckBox("Generate actor code", options.GenerateCode);
        _generateModeldef = AddCheckBox("Generate MODELDEF", options.GenerateModeldef);
        _noGravity = AddCheckBox("No gravity", options.NoGravity);
        _spawnOnCeiling = AddCheckBox("Spawn on ceiling", options.SpawnOnCeiling);
        _solid = AddCheckBox("Solid", options.Solid);
    }

    protected override void OnConfirm()
    {
        ResultOptions = new WavefrontExportOptions
        {
            FilePath = _filePath.Text?.Trim() ?? "",
            Scale = ParseNumber(_scale, ResultOptions.Scale),
            ExportForGZDoom = _exportForGzdoom.IsChecked == true,
            ExportTextures = _exportTextures.IsChecked == true,
            ActorName = _actorName.Text?.Trim() ?? "",
            BasePath = _basePath.Text?.Trim() ?? "",
            ActorPath = _actorPath.Text?.Trim() ?? "",
            ModelPath = _modelPath.Text?.Trim() ?? "",
            Sprite = (_sprite.Text?.Trim() ?? "").ToUpperInvariant(),
            SkipTextures = ParseSkipTextures(_skipTextures.Text),
            IgnoreControlSectors = _ignoreControlSectors.IsChecked == true,
            NormalizeLowestVertex = _normalizeLowestVertex.IsChecked == true,
            CenterModel = _centerModel.IsChecked == true,
            ZScript = _zScript.IsChecked == true,
            GenerateCode = _generateCode.IsChecked == true,
            GenerateModeldef = _generateModeldef.IsChecked == true,
            NoGravity = _noGravity.IsChecked == true,
            SpawnOnCeiling = _spawnOnCeiling.IsChecked == true,
            Solid = _solid.IsChecked == true,
        };
    }

    private static double ParseNumber(TextBox box, double fallback)
        => double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : fallback;

    private static IReadOnlyList<string> ParseSkipTextures(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? Array.Empty<string>()
            : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
