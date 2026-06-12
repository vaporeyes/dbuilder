// ABOUTME: Modal Settings dialog editing the persisted paths (game-config dir, test source port/IWAD/args, node builder).
// ABOUTME: Reads current values from Settings on open; the host writes the results back and saves on OK.

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class SettingsWindow : PropertyDialog
{
    private const string ShortcutOverrideWatermark = "command.id=Shortcut; use None or Unassigned to clear; separate entries with semicolons, commas, or new lines";

    private readonly TextBox _configDir, _testPort, _testIwad, _testArgs, _testAdditionalParameters, _testSkill, _nodePath, _nodeArgs, _udbScriptExternalEditor, _maxRecentFiles, _autosaveCount, _autosaveInterval, _defaultSectorFloorHeight, _defaultSectorCeilingHeight, _defaultSectorBrightness, _imageBrightness, _doubleSidedAlpha, _visualFov, _viewDistance, _moveSpeed, _mouseSpeed, _mouseSelectionThreshold, _stitchRange, _highlightRange, _thingHighlightRange, _splitLinedefsRange, _autoScrollSpeed, _statusHistoryLimit, _toastDuration, _toastDisabledActions, _shortcutOverrides;
    private readonly ComboBox _defaultViewMode, _modelRenderMode, _lightRenderMode, _changeHeightBySidedef, _eventLineLabelVisibility, _eventLineLabelStyle, _mergeGeometryMode, _toastAnchor, _pasteTagMode;
    private readonly CheckBox _testMonsters, _autosave, _autoClearSidedefTextures, _autoMerge, _splitJoinedSectors, _autoClearSelection, _visualModeClearSelection, _editNewThing, _editNewSector, _additiveSelect, _additivePaintSelect, _dynamicGridSize, _switchViewModes, _drawLineContinuousDrawing, _drawLineAutoCloseDrawing, _drawRectangleContinuousDrawing, _drawRectangleRadialDrawing, _drawRectanglePlaceThingsAtVertices, _drawEllipseContinuousDrawing, _drawEllipseRadialDrawing, _drawEllipsePlaceThingsAtVertices, _drawCurveContinuousDrawing, _drawCurveAutoCloseDrawing, _drawCurvePlaceThingsAtVertices, _drawGridContinuousDrawing, _drawGridTriangulate, _useHighlight, _alphaBasedTextureHighlighting, _enhancedRenderingEffects, _classicRendering, _qualityDisplay, _classicBilinear, _visualBilinear, _blackBrowsers, _flatShadeVertices, _markExtraFloors, _drawFog, _drawSky, _showEventLines, _showVisualVertices, _showErrorsWindow, _fixedThingsScale, _alwaysShowVertices, _selectAdjacentVisualVertexSlopeHandles, _useOppositeSmartPivotHandle, _toastsEnabled, _pasteRemoveActions;
    private readonly bool _drawLineShowGuidelines;
    private readonly int _drawRectangleSubdivisions, _drawRectangleBevelWidth;
    private readonly bool _drawRectangleShowGuidelines;
    private readonly int _drawEllipseSubdivisions, _drawEllipseBevelWidth, _drawEllipseAngle;
    private readonly bool _drawEllipseShowGuidelines;
    private readonly int _drawCurveSegmentLength;
    private readonly DrawGridModeSettings _drawGridSettings;

    public string? ConfigDir, TestPort, TestIwad, TestPortArgs, TestAdditionalParameters, NodeBuilderPath, NodeBuilderArgs, UdbScriptExternalEditor;
    public int? MaxRecentFiles;
    public bool Autosave;
    public int? AutosaveCount;
    public int? AutosaveIntervalMinutes;
    public int? DefaultSectorFloorHeightSetting;
    public int? DefaultSectorCeilingHeightSetting;
    public int? DefaultSectorBrightnessSetting;
    public int? TestSkill;
    public bool TestMonsters;
    public bool AutoClearSidedefTextures;
    public bool AutoMerge;
    public bool SplitJoinedSectors;
    public bool AutoClearSelection;
    public bool VisualModeClearSelection;
    public bool EditNewThing;
    public bool EditNewSector;
    public bool AdditiveSelect;
    public bool AdditivePaintSelect;
    public int ChangeHeightBySidedef;
    public bool DynamicGridSize;
    public bool SwitchViewModes;
    public bool UseHighlight;
    public bool AlphaBasedTextureHighlighting;
    public bool EnhancedRenderingEffects;
    public bool ClassicRendering;
    public int? ImageBrightness;
    public double? DoubleSidedAlpha;
    public int? VisualFov;
    public int? ViewDistance;
    public int? MoveSpeed;
    public int? MouseSpeed;
    public int? MouseSelectionThreshold;
    public int? StitchRange;
    public int? HighlightRange;
    public int? ThingHighlightRange;
    public int? SplitLinedefsRange;
    public int? AutoScrollSpeed;
    public bool QualityDisplay;
    public bool ClassicBilinear;
    public bool VisualBilinear;
    public bool BlackBrowsers;
    public bool FlatShadeVertices;
    public bool MarkExtraFloors;
    public bool DrawFog;
    public bool DrawSky;
    public bool ShowEventLines;
    public int EventLineLabelVisibility;
    public int EventLineLabelStyle;
    public bool ShowVisualVertices;
    public bool ShowErrorsWindow;
    public bool FixedThingsScale;
    public bool AlwaysShowVertices;
    public bool SelectAdjacentVisualVertexSlopeHandles;
    public bool UseOppositeSmartPivotHandle;
    public bool ToastsEnabled;
    public ToastAnchor ToastAnchor;
    public int ToastDurationMilliseconds;
    public Dictionary<string, bool> ToastActionSettings = new(StringComparer.Ordinal);
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
        _autosave = AddCheckBox("Enable autosave", s.Autosave);
        _autosaveCount = AddField("Autosave count", Settings.AutosaveCountText(s));
        _autosaveInterval = AddField("Autosave interval", Settings.AutosaveIntervalText(s));
        _defaultSectorFloorHeight = AddField("Default floor height", Settings.DefaultSectorFloorHeightText(s));
        _defaultSectorCeilingHeight = AddField("Default ceiling height", Settings.DefaultSectorCeilingHeightText(s));
        _defaultSectorBrightness = AddField("Default brightness", Settings.DefaultSectorBrightnessText(s));
        _imageBrightness = AddField("Image brightness", Settings.ImageBrightnessText(s));
        _doubleSidedAlpha = AddField("Double-sided alpha", Settings.DoubleSidedAlphaText(s));
        _visualFov = AddField("Visual FOV", Settings.VisualFovText(s));
        _viewDistance = AddField("View distance", Settings.ViewDistanceText(s));
        _moveSpeed = AddField("Move speed", Settings.MoveSpeedText(s));
        _mouseSpeed = AddField("Mouse speed", Settings.MouseSpeedText(s));
        _mouseSelectionThreshold = AddField("Mouse selection threshold", Settings.MouseSelectionThresholdText(s));
        _stitchRange = AddField("Stitch within", Settings.StitchRangeText(s));
        _highlightRange = AddField("Highlight within", Settings.HighlightRangeText(s));
        _thingHighlightRange = AddField("Highlight things within", Settings.ThingHighlightRangeText(s));
        _splitLinedefsRange = AddField("Split linedefs within", Settings.SplitLinedefsRangeText(s));
        _autoScrollSpeed = AddField("Auto-scroll speed", Settings.AutoScrollSpeedText(s));
        _statusHistoryLimit = AddField("Status history", Settings.StatusHistoryLimitText(s));
        _toastsEnabled = AddCheckBox("Show toasts", s.ToastsEnabled);
        _toastDuration = AddField("Toast duration", ToastPreferences.DurationSecondsText(s.NormalizedToastDurationMilliseconds));
        _toastAnchor = AddCombo("Toast position", ToastAnchorItems(), (int)s.NormalizedToastAnchor);
        _toastDisabledActions = AddField("Disabled toasts", ToastPreferences.DisabledActionsText(s.ToastActionSettings));
        _toastDisabledActions.AcceptsReturn = true;
        _toastDisabledActions.MinHeight = 72;
        _toastDisabledActions.Watermark = "disabled toast ids: " + ToastPreferences.KnownActionNamesText();
        _toastDisabledActions.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
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
        _autoClearSelection = AddCheckBox("Automatically clear selection in Classic Modes", s.AutoClearSelection);
        _visualModeClearSelection = AddCheckBox("Automatically clear selection in Visual Mode", s.VisualModeClearSelection);
        _editNewThing = AddCheckBox("Edit thing properties when inserting a new thing", s.EditNewThing);
        _editNewSector = AddCheckBox("Edit sector properties after drawing a new sector", s.EditNewSector);
        _additiveSelect = AddCheckBox("Additive selecting without holding Shift", s.AdditiveSelect);
        _additivePaintSelect = AddCheckBox("Additive paint selecting without holding Shift", s.NormalizedAdditivePaintSelect);
        _changeHeightBySidedef = AddCombo("When changing height on a wall in Visual Mode", ChangeHeightBySidedefItems(), s.NormalizedChangeHeightBySidedef);
        _dynamicGridSize = AddCheckBox("Dynamic grid size", s.DynamicGridSize);
        _switchViewModes = AddCheckBox("Switch view modes when reselecting a classic mode", s.SwitchViewModes);
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
        _qualityDisplay = AddCheckBox("High quality rendering", s.QualityDisplay);
        _classicBilinear = AddCheckBox("Bilinear filtering in classic modes", s.ClassicBilinear);
        _visualBilinear = AddCheckBox("Bilinear filtering in visual modes", s.VisualBilinear);
        _blackBrowsers = AddCheckBox("Black background in image browser", s.BlackBrowsers);
        _flatShadeVertices = AddCheckBox("Flat shade vertices", s.FlatShadeVertices);
        _markExtraFloors = AddCheckBox("Mark 3D floors in classic modes", s.MarkExtraFloors);
        _drawFog = AddCheckBox("Draw fog", s.DrawFog);
        _drawSky = AddCheckBox("Draw sky", s.DrawSky);
        _showEventLines = AddCheckBox("Show event lines", s.ShowEventLines);
        _eventLineLabelVisibility = AddCombo("Event line labels", EventLineLabelVisibilityItems(), s.NormalizedEventLineLabelVisibility);
        _eventLineLabelStyle = AddCombo("Event line label text", EventLineLabelStyleItems(), s.NormalizedEventLineLabelStyle);
        _showVisualVertices = AddCheckBox("Show visual vertices", s.ShowVisualVertices);
        _showErrorsWindow = AddCheckBox("Show errors window", s.ShowErrorsWindow);
        _fixedThingsScale = AddCheckBox("Fixed things scale", s.FixedThingsScale);
        _alwaysShowVertices = AddCheckBox("Always show vertices", s.AlwaysShowVertices);
        _selectAdjacentVisualVertexSlopeHandles = AddCheckBox("Select adjacent visual vertex slope handles", s.SelectAdjacentVisualVertexSlopeHandles);
        _useOppositeSmartPivotHandle = AddCheckBox("Opposite side/vertex is smart pivot handle on triangular sectors", s.UseOppositeSmartPivotHandle);
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
        Autosave = _autosave.IsChecked == true;
        AutosaveCount = Settings.AcceptAutosaveCountText(_autosaveCount.Text);
        AutosaveIntervalMinutes = Settings.AcceptAutosaveIntervalText(_autosaveInterval.Text);
        DefaultSectorFloorHeightSetting = Settings.AcceptSectorHeightText(_defaultSectorFloorHeight.Text);
        DefaultSectorCeilingHeightSetting = Settings.AcceptSectorHeightText(_defaultSectorCeilingHeight.Text);
        DefaultSectorBrightnessSetting = Settings.AcceptSectorBrightnessText(_defaultSectorBrightness.Text);
        StatusHistoryLimit = Settings.AcceptStatusHistoryLimitText(_statusHistoryLimit.Text);
        AutoClearSidedefTextures = _autoClearSidedefTextures.IsChecked == true;
        AutoMerge = _autoMerge.IsChecked == true;
        SplitJoinedSectors = _splitJoinedSectors.IsChecked == true;
        AutoClearSelection = _autoClearSelection.IsChecked == true;
        VisualModeClearSelection = _visualModeClearSelection.IsChecked == true;
        EditNewThing = _editNewThing.IsChecked == true;
        EditNewSector = _editNewSector.IsChecked == true;
        AdditiveSelect = _additiveSelect.IsChecked == true;
        AdditivePaintSelect = _additivePaintSelect.IsChecked == true;
        ChangeHeightBySidedef = ComboNumber(_changeHeightBySidedef, Settings.DefaultChangeHeightBySidedef);
        DynamicGridSize = _dynamicGridSize.IsChecked == true;
        SwitchViewModes = _switchViewModes.IsChecked == true;
        UseHighlight = _useHighlight.IsChecked == true;
        AlphaBasedTextureHighlighting = _alphaBasedTextureHighlighting.IsChecked == true;
        EnhancedRenderingEffects = _enhancedRenderingEffects.IsChecked == true;
        ClassicRendering = _classicRendering.IsChecked == true;
        ImageBrightness = Settings.AcceptImageBrightnessText(_imageBrightness.Text);
        DoubleSidedAlpha = Settings.AcceptDoubleSidedAlphaText(_doubleSidedAlpha.Text);
        VisualFov = Settings.AcceptVisualFovText(_visualFov.Text);
        ViewDistance = Settings.AcceptViewDistanceText(_viewDistance.Text);
        MoveSpeed = Settings.AcceptMoveSpeedText(_moveSpeed.Text);
        MouseSpeed = Settings.AcceptMouseSpeedText(_mouseSpeed.Text);
        MouseSelectionThreshold = Settings.AcceptMouseSelectionThresholdText(_mouseSelectionThreshold.Text);
        StitchRange = Settings.AcceptStitchRangeText(_stitchRange.Text);
        HighlightRange = Settings.AcceptHighlightRangeText(_highlightRange.Text);
        ThingHighlightRange = Settings.AcceptThingHighlightRangeText(_thingHighlightRange.Text);
        SplitLinedefsRange = Settings.AcceptSplitLinedefsRangeText(_splitLinedefsRange.Text);
        AutoScrollSpeed = Settings.AcceptAutoScrollSpeedText(_autoScrollSpeed.Text);
        QualityDisplay = _qualityDisplay.IsChecked == true;
        ClassicBilinear = _classicBilinear.IsChecked == true;
        VisualBilinear = _visualBilinear.IsChecked == true;
        BlackBrowsers = _blackBrowsers.IsChecked == true;
        FlatShadeVertices = _flatShadeVertices.IsChecked == true;
        MarkExtraFloors = _markExtraFloors.IsChecked == true;
        DrawFog = _drawFog.IsChecked == true;
        DrawSky = _drawSky.IsChecked == true;
        ShowEventLines = _showEventLines.IsChecked == true;
        EventLineLabelVisibility = ComboNumber(_eventLineLabelVisibility, Settings.DefaultEventLineLabelVisibility);
        EventLineLabelStyle = ComboNumber(_eventLineLabelStyle, Settings.DefaultEventLineLabelStyle);
        ShowVisualVertices = _showVisualVertices.IsChecked == true;
        ShowErrorsWindow = _showErrorsWindow.IsChecked == true;
        FixedThingsScale = _fixedThingsScale.IsChecked == true;
        AlwaysShowVertices = _alwaysShowVertices.IsChecked == true;
        SelectAdjacentVisualVertexSlopeHandles = _selectAdjacentVisualVertexSlopeHandles.IsChecked == true;
        UseOppositeSmartPivotHandle = _useOppositeSmartPivotHandle.IsChecked == true;
        ToastsEnabled = _toastsEnabled.IsChecked == true;
        ToastAnchor = (ToastAnchor)ComboNumber(_toastAnchor, (int)ToastPreferences.DefaultAnchor);
        ToastDurationMilliseconds = ToastPreferences.AcceptDurationSecondsText(_toastDuration.Text);
        ToastActionSettings = ToastPreferences.ParseDisabledActionsText(_toastDisabledActions.Text);
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

    private static IEnumerable<CatalogItem> ChangeHeightBySidedefItems()
    {
        yield return new CatalogItem(0, "Do nothing");
        yield return new CatalogItem(1, "Change the ceiling height");
        yield return new CatalogItem(2, "Change the floor height");
        yield return new CatalogItem(3, "Change both floor and ceiling height");
    }

    private static IEnumerable<CatalogItem> EventLineLabelVisibilityItems()
    {
        yield return new CatalogItem(0, "Never show");
        yield return new CatalogItem(1, "Forward only");
        yield return new CatalogItem(2, "Reverse only");
        yield return new CatalogItem(3, "Forward + Reverse");
    }

    private static IEnumerable<CatalogItem> EventLineLabelStyleItems()
    {
        yield return new CatalogItem(0, "Action only");
        yield return new CatalogItem(1, "Action + short arguments");
        yield return new CatalogItem(2, "Action + full arguments");
    }

    private static IEnumerable<CatalogItem> MergeGeometryModeItems()
    {
        yield return new CatalogItem((int)MergeGeometryMode.Classic, "Classic");
        yield return new CatalogItem((int)MergeGeometryMode.Merge, "Merge");
        yield return new CatalogItem((int)MergeGeometryMode.Replace, "Replace");
    }

    private static IEnumerable<CatalogItem> ToastAnchorItems()
    {
        yield return new CatalogItem((int)ToastAnchor.TopLeft, "Top left");
        yield return new CatalogItem((int)ToastAnchor.TopRight, "Top right");
        yield return new CatalogItem((int)ToastAnchor.BottomRight, "Bottom right");
        yield return new CatalogItem((int)ToastAnchor.BottomLeft, "Bottom left");
    }

    private static IEnumerable<CatalogItem> PasteTagModeItems()
    {
        yield return new CatalogItem((int)PasteTagMode.Keep, "Keep tags");
        yield return new CatalogItem((int)PasteTagMode.Renumber, "Renumber conflicting tags");
        yield return new CatalogItem((int)PasteTagMode.Remove, "Remove tags");
    }
}
