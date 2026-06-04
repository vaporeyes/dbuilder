// ABOUTME: Tests MainWindow command dispatch metadata without constructing the Avalonia window.
// ABOUTME: Keeps stable window command ids wired to editor handlers as menu actions move into the catalog.

using System.Reflection;
using DBuilder.Editor;
using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class MainWindowCommandTests
{
    [Theory]
    [InlineData("window.recover-autosave", "OnRecoverAutosave")]
    [InlineData("window.reload-map", "OnReloadMap")]
    [InlineData("window.add-resource", "OnAddResource")]
    [InlineData("window.add-resource-directory", "OnAddResourceDirectory")]
    [InlineData("window.save-as-format", "OnSaveAsFormat")]
    [InlineData("window.exit", "OnExit")]
    [InlineData("window.shortcuts", "OnShortcuts")]
    [InlineData("window.about", "OnAbout")]
    [InlineData("window.select-all", "OnSelectAll")]
    [InlineData("window.invert-selection", "OnInvertSelection")]
    [InlineData("window.select-none", "OnSelectNone")]
    [InlineData("window.flags", "OnFlags")]
    [InlineData("window.custom-fields", "OnCustomFields")]
    [InlineData("window.tags", "OnTagList")]
    [InlineData("window.stitch-geometry", "OnStitch")]
    [InlineData("window.join-sectors", "OnJoinSectors")]
    [InlineData("window.merge-sectors", "OnMergeSectors")]
    [InlineData("window.flip-selection-horizontal", "OnFlipH")]
    [InlineData("window.flip-selection-vertical", "OnFlipV")]
    [InlineData("window.rotate-selection-cw", "OnRotateCW")]
    [InlineData("window.rotate-selection-ccw", "OnRotateCCW")]
    [InlineData("window.scale-selection-up", "OnScaleUp")]
    [InlineData("window.scale-selection-down", "OnScaleDown")]
    [InlineData("window.align-floor-to-front", "OnAlignFloorToFront")]
    [InlineData("window.align-floor-to-back", "OnAlignFloorToBack")]
    [InlineData("window.align-ceiling-to-front", "OnAlignCeilingToFront")]
    [InlineData("window.align-ceiling-to-back", "OnAlignCeilingToBack")]
    [InlineData("window.align-things-to-wall", "OnAlignThingsToWall")]
    [InlineData("window.find-replace", "OnFindReplace")]
    [InlineData("window.status-history", "OnStatusHistory")]
    [InlineData("window.browse-wall-textures", "OnBrowseWallTextures")]
    [InlineData("window.browse-flats", "OnBrowseFlats")]
    [InlineData("window.browse-floor-flats", "OnBrowseFloorFlats")]
    [InlineData("window.browse-ceiling-flats", "OnBrowseCeilingFlats")]
    [InlineData("window.browse-things", "OnBrowseThingsCatalog")]
    [InlineData("window.browse-linedef-actions", "OnBrowseActionsCatalog")]
    [InlineData("window.browse-sector-effects", "OnBrowseEffectsCatalog")]
    [InlineData("window.model-render-none", "OnModelRenderNone")]
    [InlineData("window.model-render-selection", "OnModelRenderSelection")]
    [InlineData("window.model-render-active-filter", "OnModelRenderActiveFilter")]
    [InlineData("window.model-render-all", "OnModelRenderAll")]
    [InlineData("window.next-model-render-mode", "OnNextModelRenderMode")]
    [InlineData("window.toggle-3d-floors", "OnToggle3DFloors")]
    [InlineData("window.toggle-blockmap", "OnToggleBlockmap")]
    [InlineData("window.toggle-nodes", "OnToggleNodes")]
    [InlineData("window.gradient-floor-heights", "OnGradientFloorHeights")]
    [InlineData("window.gradient-ceiling-heights", "OnGradientCeilingHeights")]
    [InlineData("window.gradient-sector-brightness", "OnGradientBrightness")]
    [InlineData("window.gradient-floor-light", "OnGradientFloorLight")]
    [InlineData("window.gradient-ceiling-light", "OnGradientCeilingLight")]
    [InlineData("window.gradient-light-color", "OnGradientLightColor")]
    [InlineData("window.gradient-fade-color", "OnGradientFadeColor")]
    [InlineData("window.gradient-light-and-fade-colors", "OnGradientLightAndFadeColor")]
    [InlineData("window.gradient-linedef-brightness", "OnGradientLinedefBrightness")]
    [InlineData("window.gradient-interpolation-linear", "OnGradientInterpolationLinear")]
    [InlineData("window.gradient-interpolation-ease-in-out-sine", "OnGradientInterpolationEaseInOutSine")]
    [InlineData("window.gradient-interpolation-ease-in-sine", "OnGradientInterpolationEaseInSine")]
    [InlineData("window.gradient-interpolation-ease-out-sine", "OnGradientInterpolationEaseOutSine")]
    [InlineData("window.apply-slope-arch", "OnApplySlopeArch")]
    [InlineData("window.apply-slopes", "OnApplySlopes")]
    [InlineData("window.toggle-automap-secret-line", "OnToggleAutomapSecretLine")]
    [InlineData("window.toggle-automap-hidden-line", "OnToggleAutomapHiddenLine")]
    [InlineData("window.toggle-automap-textured-hidden-sector", "OnToggleAutomapTexturedHiddenSector")]
    [InlineData("window.sector-color", "OnSectorColor")]
    [InlineData("window.dynamic-light-color", "OnDynamicLightColor")]
    [InlineData("window.import-obj-terrain", "OnImportObjTerrain")]
    [InlineData("window.export-idstudio", "OnExportIdStudio")]
    [InlineData("window.check-map", "OnCheckMap")]
    [InlineData("window.clean-up-geometry", "OnCleanUpGeometry")]
    [InlineData("window.build-bridge", "OnBuildBridge")]
    [InlineData("window.build-stairs", "OnBuildStairs")]
    [InlineData("window.usdf-conversations", "OnUsdfConversations")]
    [InlineData("window.usdf-dialog-editor", "OnUsdfConversations")]
    public void MenuCommandsAreRoutedThroughWindowCommandDispatch(string commandId, string handlerName)
    {
        Type type = typeof(MainWindow);
        MethodInfo? dispatcher = type.GetMethod("RunWindowCommand", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? handler = type.GetMethod(handlerName, BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(dispatcher);
        Assert.NotNull(handler);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        Assert.Contains($"case \"{commandId}\"", body, StringComparison.Ordinal);
        Assert.Contains($"{handlerName}(this, new RoutedEventArgs())", body, StringComparison.Ordinal);
    }

    [Fact]
    public void VisplaneExplorerModeReportsQueuedProgressStatus()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("scan.QueuePoints(", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(scan.Progress(queued.Count).FormatStatus())", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveCommandAvailabilityReflectsWritableSourceArchive()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("bool canSave = hasMap && (_wadPath is null || FileSaveStamp.CanWriteExistingPath(_wadPath));", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canSave, SaveMenuItem, SaveButton);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewCommandAvailabilityReflectsMapResourcesAndConfigState()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("bool hasResources = _resources is not null;", body, StringComparison.Ordinal);
        Assert.Contains("bool canBrowseCatalogs = hasMap && _config is not null;", body, StringComparison.Ordinal);
        Assert.Contains("bool canBrowseAny = hasResources || canBrowseCatalogs;", body, StringComparison.Ordinal);
        Assert.Contains("bool canFilterThingCategories = hasMap && _config is { Things.Count: > 0 };", body, StringComparison.Ordinal);
        Assert.Contains("GridSetupMenuItem, SmartGridTransformMenuItem, AlignGridToLinedefMenuItem, SetGridOriginToVertexMenuItem,", body, StringComparison.Ordinal);
        Assert.Contains("ResetGridTransformMenuItem, ToggleSnapToGridMenuItem, ToggleDynamicGridSizeMenuItem, GridSizeDownMenuItem, GridSizeUpMenuItem", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canFilterThingCategories, ThingFilterMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canBrowseAny, BrowsersMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasResources, BrowseWallTexturesMenuItem, BrowseFlatsMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canBrowseCatalogs, BrowseThingsMenuItem, BrowseLinedefActionsMenuItem, BrowseSectorEffectsMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasSelectedSector && hasResources, BrowseFloorFlatsMenuItem, BrowseCeilingFlatsMenuItem);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void DrawMenuAvailabilityReflectsMapState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"_Draw\" x:Name=\"DrawMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("DrawMenuItem,\n            MakeSectorAtCursorMenuItem, DrawSectorMenuItem, DrawLinesMenuItem, DrawCurveMenuItem,", code, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowsersMenuAvailabilityReflectsChildAvailability()
    {
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"_Browsers\" x:Name=\"BrowsersMenuItem\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckMapUsesPersistedUdbCheckerSelection()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("var checkerSelection = _settings.MapErrorCheckerSelection();", code, StringComparison.Ordinal);
        Assert.Contains("MapAnalysis.Check(map, BuildCheckContext(), checkerSelection.EnabledDescriptors())", code, StringComparison.Ordinal);
        Assert.Contains("enabled => MapAnalysis.Check(map, BuildCheckContext(), enabled),", code, StringComparison.Ordinal);
        Assert.Contains("fix => ApplyMapCheckFix(map, fix));", code, StringComparison.Ordinal);
        Assert.Contains("win.IssuesChanged += count => SetStatus(MapIssueListModel.AnalysisStatusText(count));", code, StringComparison.Ordinal);
        Assert.Contains("_settings.ApplyMapErrorCheckerSelection(checkerSelection);", code, StringComparison.Ordinal);
        Assert.Contains("SaveSettings();", code, StringComparison.Ordinal);
        Assert.Contains("_undo?.CreateUndo(\"Fix map analysis issue\");", code, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"Applied fix: {fix.Label}\");", code, StringComparison.Ordinal);
    }

    [Fact]
    public void MapCheckWindowExposesUdbCheckerSelectionRows()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapCheckWindow.cs"));

        Assert.Contains("MapErrorCheckerSelectionModel? checkerSelection = null", code, StringComparison.Ordinal);
        Assert.Contains("Func<IReadOnlyList<MapErrorCheckerDescriptor>, IReadOnlyList<MapIssue>>? runChecks = null", code, StringComparison.Ordinal);
        Assert.Contains("Func<MapIssueFix, bool>? applyFix = null", code, StringComparison.Ordinal);
        Assert.Contains("CheckerSelectionPanel(checkerSelection, runChecks)", code, StringComparison.Ordinal);
        Assert.Contains("new Expander", code, StringComparison.Ordinal);
        Assert.Contains("new CheckBox", code, StringComparison.Ordinal);
        Assert.Contains("Content = \"Run Checks\"", code, StringComparison.Ordinal);
        Assert.Contains("_model.ReplaceIssues(issues);", code, StringComparison.Ordinal);
        Assert.Contains("selection.SetChecked(row.SettingsKey, check.IsChecked == true)", code, StringComparison.Ordinal);
        Assert.Contains("ApplySelectedFix(index)", code, StringComparison.Ordinal);
        Assert.Contains("_applyFix(issue.Fixes[index])", code, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomapMenuAvailabilityReflectsChildAvailability()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"_Automap\" x:Name=\"AutomapMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("bool hasSelectedAutomapTarget = hasSelectedLinedef || hasSelectedSector;", code, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasSelectedAutomapTarget, AutomapMenuItem);", code, StringComparison.Ordinal);
    }

    [Fact]
    public void SectorHeightsMenuAvailabilityReflectsMapState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"Sector _Heights\" x:Name=\"SectorHeightsMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SectorHeightsMenuItem,\n            LowerFloor8MenuItem, RaiseFloor8MenuItem, LowerCeiling8MenuItem,", code, StringComparison.Ordinal);
    }

    [Fact]
    public void AlignTexturesParentAvailabilityReflectsEitherChildGroup()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("bool canAlignTextures = hasSelectedLinedefWithFront || hasSelectedUdmfLinedef;", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canAlignTextures, AlignTexturesMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasSelectedLinedefWithFront, AlignHorizontalMenuItem, AlignVerticalMenuItem, FitSelectedTexturesMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasSelectedUdmfLinedef,\n            AlignFloorToFrontMenuItem, AlignFloorToBackMenuItem, AlignCeilingToFrontMenuItem, AlignCeilingToBackMenuItem);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectionGroupsMenuAvailabilityReflectsMapState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"Selection _Groups\" x:Name=\"SelectionGroupsMenu\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectAllMenuItem, InvertSelectionMenuItem, SelectionGroupsMenu,", code, StringComparison.Ordinal);
    }

    [Fact]
    public void GridMenuAvailabilityReflectsMapState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"_Grid\" x:Name=\"GridMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ThingFilterMenuItem, GridMenuItem, GridSetupMenuItem, SmartGridTransformMenuItem,", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewModeMenuAvailabilityReflectsMapState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"_View Mode\" x:Name=\"ViewModeMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToggleHighlightMenuItem, ViewModeMenuItem, ViewModeWireframeMenuItem,", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ModelRenderingMenuAvailabilityReflectsMapState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"_Model Rendering\" x:Name=\"ModelRenderingMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ModelRenderingMenuItem, ModelRenderNoneMenuItem, ModelRenderSelectionMenuItem,", code, StringComparison.Ordinal);
    }

    [Fact]
    public void EditModeMenuAvailabilityReflectsMapState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"_Edit Mode\" x:Name=\"EditModeMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("LowerBrightness8MenuItem, EditModeMenuItem, VerticesModeMenuItem,", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolbarTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("ApplyToolbarShortcutTooltips();", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SaveButton, \"Save WAD\", \"window.save\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(OpenMapButton, \"Open Map\", \"window.open-map-in-current-wad\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(VerticesModeButton, \"Vertices Mode\", \"map2d.mode-vertices\");", body, StringComparison.Ordinal);
        Assert.Contains("EditorCommandCatalog.CommandToolTip(label, commandId, _shortcutBindings)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void AddResourceUsesSharedRequiredArchiveDefaultsModel()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("ConfigResourceDefaultsModel.ApplyRequiredArchiveDefaults(_config, resource);", body, StringComparison.Ordinal);
        Assert.DoesNotContain("RequiredArchiveDetector.Detect(_config, resource)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("RequiredArchiveDetector.RequiresTestExclusion(_config, requiredArchives)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveBackBlocksMapRenameTargetConflicts()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("if (!WadMaps.RenameMap(dst, _sourceMapMarker, marker))", body, StringComparison.Ordinal);
        Assert.Contains("Save blocked: target map {marker} already exists.", body, StringComparison.Ordinal);
    }

    [Fact]
    public void MenuTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("ApplyMenuShortcutTooltips();", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SaveMenuItem, \"Save WAD\", \"window.save\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(VerticesModeMenuItem, \"Vertices\", \"map2d.mode-vertices\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(DrawSectorMenuItem, \"Draw Sector\", \"map2d.draw-sector\");", body, StringComparison.Ordinal);
    }

    [Fact]
    public void FileMenuTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("x:Name=\"NewMapMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(NewMapMenuItem, \"New Map\", \"window.new-map\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(OpenMapMenuItem, \"Open Map\", \"window.open-map-in-current-wad\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SaveAsFormatMenuItem, \"Save As Format\", \"window.save-as-format\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SettingsMenuItem, \"Preferences\", \"window.preferences\");", code, StringComparison.Ordinal);
    }

    [Fact]
    public void EditMenuTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetShortcutToolTip(CopyMenuItem, \"Copy selection\", \"window.copy\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SelectSimilarMenuItem, \"Select Similar Map Elements\", \"window.select-similar\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(LowerFloor8MenuItem, \"Lower Floor by 8 mp\", \"map2d.lower-floor-8\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(FitSelectedTexturesMenuItem, \"Fit Selected Textures\", \"map2d.fit-selected-textures\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(InsertPrefabMenuItem, \"Insert Prefab File\", \"window.insert-prefab-file\");", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewMenuTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetShortcutToolTip(GoToCoordinatesMenuItem, \"Go To Coordinates\", \"window.go-to-coordinates\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(InfoPanelMenuItem, \"Toggle Info Panel\", \"window.toggle-info-panel\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ToggleSectorFillsMenuItem, \"Show Sector Fills\", \"map2d.toggle-sector-fills\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ModelRenderAllMenuItem, \"Model Rendering All\", \"window.model-render-all\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ToggleDynamicGridSizeMenuItem, \"Dynamic Grid Size\", \"map2d.toggle-dynamic-grid-size\");", body, StringComparison.Ordinal);
    }

    [Fact]
    public void RemainingViewMenuTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("x:Name=\"StatusHistoryMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BrowseWallTexturesMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"GridSetupMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(StatusHistoryMenuItem, \"Status History\", \"window.status-history\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ErrorLogMenuItem, \"Show Errors and Warnings\", \"window.show-errors\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(BrowseThingsMenuItem, \"Browse Things\", \"window.browse-things\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ThingFilterMenuItem, \"Configure Things Filters\", \"window.things-filters-setup\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(FilterSelectedThingsMenuItem, \"Filter Selected Things\", \"window.filter-selected-things\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(GridSetupMenuItem, \"Grid and Backdrop Setup\", \"window.grid-setup\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ToggleSnapToGridMenuItem, \"Toggle grid snap\", \"map2d.toggle-grid-snap\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(GridSizeUpMenuItem, \"Increase grid size\", \"map2d.grid-up\");", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolsMenuTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetShortcutToolTip(UdbScriptDockerMenuItem, \"Scripts\", \"window.udbscripts\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SoundPropagationMenuItem, \"Sound Propagation\", \"window.sound-propagation-mode\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(GradientFloorHeightsMenuItem, \"Gradient Floor Heights\", \"window.gradient-floor-heights\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ToggleAutomapSecretLineMenuItem, \"Toggle Selected Line Secret\", \"window.toggle-automap-secret-line\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ExportIdStudioMenuItem, \"Export idStudio\", \"window.export-idstudio\");", body, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpMenuTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("x:Name=\"ShortcutsMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AboutMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ShortcutsMenuItem, \"Shortcuts\", \"window.shortcuts\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(AboutMenuItem, \"About\", \"window.about\");", code, StringComparison.Ordinal);
    }

    [Fact]
    public void InfoPanelCheckedStateRefreshesFromPanelVisibility()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetChecked(InfoPanelMenuItem, InfoPanel.IsVisible);", body, StringComparison.Ordinal);
        Assert.Contains("case \"window.toggle-info-panel\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolbarModeAndDrawButtonsRefreshActiveState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Button.toolbarButton.active", xaml, StringComparison.Ordinal);
        Assert.Contains("SetActiveClass(VerticesModeButton, verticesMode);", code, StringComparison.Ordinal);
        Assert.Contains("SetActiveClass(Toggle3DModeButton, MapView.In3DMode);", code, StringComparison.Ordinal);
        Assert.Contains("SetActiveClass(DrawSectorButton, drawSector);", code, StringComparison.Ordinal);
        Assert.Contains("control.Classes.Set(\"active\", active)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void UsdfMenuLabelMatchesDialogEditorToolSurface()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Equal("Dialog Editor...", UsdfDialogEditorModel.MenuItem.Text);
        Assert.Contains("Header=\"USDF _Dialog Editor...\"", body, StringComparison.Ordinal);
    }
}
