// ABOUTME: Modal Settings dialog editing the persisted paths (game-config dir, test source port/IWAD/args, node builder).
// ABOUTME: Reads current values from Settings on open; the host writes the results back and saves on OK.

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class SettingsWindow : PropertyDialog
{
    private const string ShortcutOverrideWatermark = "command.id=Shortcut; use None to clear; separate entries with semicolons, commas, or new lines";

    private readonly TextBox _configDir, _testPort, _testIwad, _testArgs, _testAdditionalParameters, _testSkill, _nodePath, _nodeArgs, _udbScriptExternalEditor, _maxRecentFiles, _statusHistoryLimit, _shortcutOverrides;
    private readonly ComboBox _defaultViewMode, _modelRenderMode, _lightRenderMode, _mergeGeometryMode, _pasteTagMode;
    private readonly CheckBox _testMonsters, _autoClearSidedefTextures, _autoMerge, _splitJoinedSectors, _dynamicGridSize, _drawLineContinuousDrawing, _drawLineAutoCloseDrawing, _drawRectangleContinuousDrawing, _drawRectangleRadialDrawing, _drawRectanglePlaceThingsAtVertices, _drawEllipseContinuousDrawing, _drawEllipseRadialDrawing, _drawEllipsePlaceThingsAtVertices, _drawCurveContinuousDrawing, _drawCurveAutoCloseDrawing, _drawCurvePlaceThingsAtVertices, _drawGridContinuousDrawing, _drawGridTriangulate, _useHighlight, _alphaBasedTextureHighlighting, _enhancedRenderingEffects, _classicRendering, _drawFog, _drawSky, _showEventLines, _showVisualVertices, _selectAdjacentVisualVertexSlopeHandles, _pasteRemoveActions;
    private readonly bool _drawLineShowGuidelines;
    private readonly int _drawRectangleSubdivisions, _drawRectangleBevelWidth;
    private readonly bool _drawRectangleShowGuidelines;
    private readonly int _drawEllipseSubdivisions, _drawEllipseBevelWidth, _drawEllipseAngle;
    private readonly bool _drawEllipseShowGuidelines;
    private readonly int _drawCurveSegmentLength;
    private readonly DrawGridModeSettings _drawGridSettings;

    public string? ConfigDir, TestPort, TestIwad, TestPortArgs, TestAdditionalParameters, NodeBuilderPath, NodeBuilderArgs, UdbScriptExternalEditor;
    public int? MaxRecentFiles;
    public int? TestSkill;
    public bool TestMonsters;
    public bool AutoClearSidedefTextures;
    public bool AutoMerge;
    public bool SplitJoinedSectors;
    public bool DynamicGridSize;
    public bool UseHighlight;
    public bool AlphaBasedTextureHighlighting;
    public bool EnhancedRenderingEffects;
    public bool ClassicRendering;
    public bool DrawFog;
    public bool DrawSky;
    public bool ShowEventLines;
    public bool ShowVisualVertices;
    public bool SelectAdjacentVisualVertexSlopeHandles;
    public int DefaultViewMode;
    public int ModelRenderMode;
    public int LightRenderMode;
    public MergeGeometryMode MergeGeometryMode;
    public int? StatusHistoryLimit;
    public DrawLineModeSettings DrawLineSettings = new();
    public DrawRectangleModeSettings DrawRectangleSettings = new();
    public DrawEllipseModeSettings DrawEllipseSettings = new();
    public DrawCurveModeSettings DrawCurveSettings = new();
    public DrawGridModeSettings DrawGridSettings = new();
    public PasteOptions PasteOptions = new();
    public List<EditorShortcutBinding> ShortcutOverrides = new();

    public SettingsWindow(Settings s) : base("Settings", "Leave a field blank to use the built-in default.")
    {
        Width = 540;
        _configDir = AddField("Game config dir", s.ConfigDir ?? "");
        _testPort  = AddField("Test source port", s.TestPort ?? "");
        _testIwad  = AddField("Test IWAD", s.TestIwad ?? "");
        _testArgs  = AddField("Test port args", s.TestPortArgs ?? "");
        _testAdditionalParameters = AddField("Test additional parameters", s.TestAdditionalParameters ?? "");
        _testSkill = AddField("Test skill", Settings.TestSkillText(s));
        _testMonsters = AddCheckBox("Test with monsters", s.TestMonsters);
        _nodePath  = AddField("Node builder", s.NodeBuilderPath ?? "");
        _nodeArgs  = AddField("Node builder args", s.NodeBuilderArgs ?? "");
        _udbScriptExternalEditor = AddFieldWithButton(
            UdbScriptPreferencesModel.Metadata().ExternalEditorLabel,
            s.UdbScriptExternalEditor ?? "",
            "...",
            BrowseExternalEditor);
        _maxRecentFiles = AddField("Max recent files", Settings.MaxRecentFilesText(s));
        _statusHistoryLimit = AddField("Status history", Settings.StatusHistoryLimitText(s));
        _shortcutOverrides = AddField("Shortcut overrides", EditorCommandCatalog.OverrideText(s.ShortcutOverrides));
        _shortcutOverrides.AcceptsReturn = true;
        _shortcutOverrides.MinHeight = 72;
        _shortcutOverrides.Watermark = ShortcutOverrideWatermark;
        _shortcutOverrides.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        _drawLineShowGuidelines = s.NormalizedDrawLineSettings.ShowGuidelines;
        _drawRectangleSubdivisions = s.NormalizedDrawRectangleSettings.Subdivisions;
        _drawRectangleBevelWidth = s.NormalizedDrawRectangleSettings.BevelWidth;
        _drawRectangleShowGuidelines = s.NormalizedDrawRectangleSettings.ShowGuidelines;
        _drawEllipseSubdivisions = s.NormalizedDrawEllipseSettings.Subdivisions;
        _drawEllipseBevelWidth = s.NormalizedDrawEllipseSettings.BevelWidth;
        _drawEllipseAngle = s.NormalizedDrawEllipseSettings.Angle;
        _drawEllipseShowGuidelines = s.NormalizedDrawEllipseSettings.ShowGuidelines;
        _drawCurveSegmentLength = s.NormalizedDrawCurveSettings.SegmentLength;
        _drawGridSettings = s.NormalizedDrawGridSettings;
        _autoClearSidedefTextures = AddCheckBox("Auto-clear sidedef textures", s.AutoClearSidedefTextures);
        _autoMerge = AddCheckBox("Snap to geometry", s.AutoMerge);
        _splitJoinedSectors = AddCheckBox("Split joined sectors", s.SplitJoinedSectors);
        _dynamicGridSize = AddCheckBox("Dynamic grid size", s.DynamicGridSize);
        _drawLineContinuousDrawing = AddCheckBox("Draw lines continuously", s.NormalizedDrawLineSettings.ContinuousDrawing);
        _drawLineAutoCloseDrawing = AddCheckBox("Auto-close drawn lines", s.NormalizedDrawLineSettings.AutoCloseDrawing);
        _drawRectangleContinuousDrawing = AddCheckBox("Draw rectangles continuously", s.NormalizedDrawRectangleSettings.ContinuousDrawing);
        _drawRectangleRadialDrawing = AddCheckBox("Draw rectangles radially", s.NormalizedDrawRectangleSettings.RadialDrawing);
        _drawRectanglePlaceThingsAtVertices = AddCheckBox("Place things at rectangle vertices", s.NormalizedDrawRectangleSettings.PlaceThingsAtVertices);
        _drawEllipseContinuousDrawing = AddCheckBox("Draw ellipses continuously", s.NormalizedDrawEllipseSettings.ContinuousDrawing);
        _drawEllipseRadialDrawing = AddCheckBox("Draw ellipses radially", s.NormalizedDrawEllipseSettings.RadialDrawing);
        _drawEllipsePlaceThingsAtVertices = AddCheckBox("Place things at ellipse vertices", s.NormalizedDrawEllipseSettings.PlaceThingsAtVertices);
        _drawCurveContinuousDrawing = AddCheckBox("Draw curves continuously", s.NormalizedDrawCurveSettings.ContinuousDrawing);
        _drawCurveAutoCloseDrawing = AddCheckBox("Auto-close drawn curves", s.NormalizedDrawCurveSettings.AutoCloseDrawing);
        _drawCurvePlaceThingsAtVertices = AddCheckBox("Place things at curve vertices", s.NormalizedDrawCurveSettings.PlaceThingsAtVertices);
        _drawGridContinuousDrawing = AddCheckBox("Draw grids continuously", s.NormalizedDrawGridSettings.ContinuousDrawing);
        _drawGridTriangulate = AddCheckBox("Triangulate drawn grids", s.NormalizedDrawGridSettings.Triangulate);
        _useHighlight = AddCheckBox("Use highlight", s.UseHighlight);
        _alphaBasedTextureHighlighting = AddCheckBox("Alpha-based texture highlighting", s.AlphaBasedTextureHighlighting);
        _enhancedRenderingEffects = AddCheckBox("Enhanced rendering effects", s.EnhancedRenderingEffects);
        _classicRendering = AddCheckBox("Classic rendering", s.ClassicRendering);
        _drawFog = AddCheckBox("Draw fog", s.DrawFog);
        _drawSky = AddCheckBox("Draw sky", s.DrawSky);
        _showEventLines = AddCheckBox("Show event lines", s.ShowEventLines);
        _showVisualVertices = AddCheckBox("Show visual vertices", s.ShowVisualVertices);
        _selectAdjacentVisualVertexSlopeHandles = AddCheckBox("Select adjacent visual vertex slope handles", s.SelectAdjacentVisualVertexSlopeHandles);
        _defaultViewMode = AddCombo("Default view mode", DefaultViewModeItems(), s.NormalizedDefaultViewMode);
        _modelRenderMode = AddCombo("Model render mode", ModelRenderModeItems(), (int)s.NormalizedModelRenderMode);
        _lightRenderMode = AddCombo("Light render mode", LightRenderModeItems(), (int)s.NormalizedLightRenderMode);
        _mergeGeometryMode = AddCombo("Merge geometry mode", MergeGeometryModeItems(), (int)s.NormalizedMergeGeometryMode);
        _pasteTagMode = AddCombo("Pasted tags", PasteTagModeItems(), (int)s.NormalizedPasteOptions.ChangeTags);
        _pasteRemoveActions = AddCheckBox("Remove pasted actions", s.NormalizedPasteOptions.RemoveActions);
    }

    protected override void OnConfirm()
    {
        ConfigDir = NullIfBlank(_configDir.Text);
        TestPort = NullIfBlank(_testPort.Text);
        TestIwad = NullIfBlank(_testIwad.Text);
        TestPortArgs = NullIfBlank(_testArgs.Text);
        TestAdditionalParameters = NullIfBlank(_testAdditionalParameters.Text);
        TestSkill = Settings.AcceptTestSkillText(_testSkill.Text);
        TestMonsters = _testMonsters.IsChecked == true;
        NodeBuilderPath = NullIfBlank(_nodePath.Text);
        NodeBuilderArgs = NullIfBlank(_nodeArgs.Text);
        UdbScriptExternalEditor = UdbScriptPreferencesModel.AcceptExternalEditorPath(_udbScriptExternalEditor.Text ?? "")?.Value?.ToString();
        MaxRecentFiles = Settings.AcceptMaxRecentFilesText(_maxRecentFiles.Text);
        StatusHistoryLimit = Settings.AcceptStatusHistoryLimitText(_statusHistoryLimit.Text);
        AutoClearSidedefTextures = _autoClearSidedefTextures.IsChecked == true;
        AutoMerge = _autoMerge.IsChecked == true;
        SplitJoinedSectors = _splitJoinedSectors.IsChecked == true;
        DynamicGridSize = _dynamicGridSize.IsChecked == true;
        UseHighlight = _useHighlight.IsChecked == true;
        AlphaBasedTextureHighlighting = _alphaBasedTextureHighlighting.IsChecked == true;
        EnhancedRenderingEffects = _enhancedRenderingEffects.IsChecked == true;
        ClassicRendering = _classicRendering.IsChecked == true;
        DrawFog = _drawFog.IsChecked == true;
        DrawSky = _drawSky.IsChecked == true;
        ShowEventLines = _showEventLines.IsChecked == true;
        ShowVisualVertices = _showVisualVertices.IsChecked == true;
        SelectAdjacentVisualVertexSlopeHandles = _selectAdjacentVisualVertexSlopeHandles.IsChecked == true;
        DefaultViewMode = ComboNumber(_defaultViewMode, 0);
        ModelRenderMode = ComboNumber(_modelRenderMode, (int)ThingModelRenderMode.All);
        LightRenderMode = ComboNumber(_lightRenderMode, (int)ThingLightRenderMode.All);
        MergeGeometryMode = (MergeGeometryMode)ComboNumber(_mergeGeometryMode, (int)MergeGeometryMode.Replace);
        DrawLineSettings = new DrawLineModeSettings(
            ContinuousDrawing: _drawLineContinuousDrawing.IsChecked == true,
            AutoCloseDrawing: _drawLineAutoCloseDrawing.IsChecked == true,
            ShowGuidelines: _drawLineShowGuidelines);
        DrawRectangleSettings = new DrawRectangleModeSettings(
            Subdivisions: _drawRectangleSubdivisions,
            BevelWidth: _drawRectangleBevelWidth,
            ContinuousDrawing: _drawRectangleContinuousDrawing.IsChecked == true,
            ShowGuidelines: _drawRectangleShowGuidelines,
            RadialDrawing: _drawRectangleRadialDrawing.IsChecked == true,
            PlaceThingsAtVertices: _drawRectanglePlaceThingsAtVertices.IsChecked == true);
        DrawEllipseSettings = new DrawEllipseModeSettings(
            Subdivisions: _drawEllipseSubdivisions,
            BevelWidth: _drawEllipseBevelWidth,
            Angle: _drawEllipseAngle,
            ContinuousDrawing: _drawEllipseContinuousDrawing.IsChecked == true,
            ShowGuidelines: _drawEllipseShowGuidelines,
            RadialDrawing: _drawEllipseRadialDrawing.IsChecked == true,
            PlaceThingsAtVertices: _drawEllipsePlaceThingsAtVertices.IsChecked == true);
        DrawCurveSettings = new DrawCurveModeSettings(
            SegmentLength: _drawCurveSegmentLength,
            ContinuousDrawing: _drawCurveContinuousDrawing.IsChecked == true,
            AutoCloseDrawing: _drawCurveAutoCloseDrawing.IsChecked == true,
            PlaceThingsAtVertices: _drawCurvePlaceThingsAtVertices.IsChecked == true);
        DrawGridSettings = _drawGridSettings with
        {
            ContinuousDrawing = _drawGridContinuousDrawing.IsChecked == true,
            Triangulate = _drawGridTriangulate.IsChecked == true,
        };
        ShortcutOverrides = EditorCommandCatalog.ParseOverrideText(_shortcutOverrides.Text);
        PasteOptions = new PasteOptions
        {
            ChangeTags = (PasteTagMode)ComboNumber(_pasteTagMode, (int)PasteTagMode.Keep),
            RemoveActions = _pasteRemoveActions.IsChecked == true,
        };
    }

    private static string? NullIfBlank(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();

    private async Task BrowseExternalEditor(TextBox box)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UdbScriptPreferencesModel.Metadata().ExternalEditorLabel,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Executables") { Patterns = new[] { "*.exe", "*.cmd", "*.bat" } },
                FilePickerFileTypes.All,
            },
        });

        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path) return;
        box.Text = path;
    }

    private static IEnumerable<CatalogItem> DefaultViewModeItems()
    {
        yield return new CatalogItem(0, "Wireframe");
        yield return new CatalogItem(1, "Brightness Levels");
        yield return new CatalogItem(2, "Floor Textures");
        yield return new CatalogItem(3, "Ceiling Textures");
    }

    private static IEnumerable<CatalogItem> ModelRenderModeItems()
    {
        yield return new CatalogItem((int)ThingModelRenderMode.None, "None");
        yield return new CatalogItem((int)ThingModelRenderMode.Selection, "Selection only");
        yield return new CatalogItem((int)ThingModelRenderMode.ActiveThingsFilter, "Active things filter only");
        yield return new CatalogItem((int)ThingModelRenderMode.All, "All");
    }

    private static IEnumerable<CatalogItem> LightRenderModeItems()
    {
        yield return new CatalogItem((int)ThingLightRenderMode.None, "None");
        yield return new CatalogItem((int)ThingLightRenderMode.All, "All");
        yield return new CatalogItem((int)ThingLightRenderMode.Animated, "Animated");
    }

    private static IEnumerable<CatalogItem> MergeGeometryModeItems()
    {
        yield return new CatalogItem((int)MergeGeometryMode.Classic, "Classic");
        yield return new CatalogItem((int)MergeGeometryMode.Merge, "Merge");
        yield return new CatalogItem((int)MergeGeometryMode.Replace, "Replace");
    }

    private static IEnumerable<CatalogItem> PasteTagModeItems()
    {
        yield return new CatalogItem((int)PasteTagMode.Keep, "Keep tags");
        yield return new CatalogItem((int)PasteTagMode.Renumber, "Renumber conflicting tags");
        yield return new CatalogItem((int)PasteTagMode.Remove, "Remove tags");
    }
}
