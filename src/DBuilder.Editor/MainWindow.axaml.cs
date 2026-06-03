// ABOUTME: Main editor window code-behind - wires menu/toolbar actions to map load/save and the editing core.
// ABOUTME: Owns the loaded MapSet + UndoManager and keeps the status/info panels in sync with selection.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Editor;

public partial class MainWindow : Window
{
    private MapSet? _map;
    private UndoManager? _undo;
    private string? _mapMarker;
    private string? _sourceMapMarker;
    private string? _wadPath;
    private FileSaveStamp? _sourceWadStamp;
    private string? _pk3Path;
    private List<Pk3MapEntry>? _pk3Maps;
    private string? _pk3MapArchivePath;
    private string? _iwadPath; // an IWAD (the loaded WAD if it is one, else an added IWAD resource) for Test Map
    private MapFormat _mapFormat = MapFormat.Doom;
    private readonly StatusHistory _statusHistory = new();
    private MapOptions? _mapOptions;
    private Configuration? _mapSettings;
    private GameConfiguration? _config;
    private CompilerConfiguration _compilerConfig = new();
    private ScriptConfigurationCatalog _scriptConfigurations = new();
    private string _configName = "(none)";
    private string _configFile = "";
    private string _configPath = "";
    private bool _configIsAuto = true; // true while the config was chosen by default/auto-detect (so WAD open may switch it)
    private bool _mapDirty;
    private bool _allowDirtyClose;
    private bool _autosavePending;
    private string _untitledAutosaveId = Guid.NewGuid().ToString("N");
    private AutoSaveKey? _activeAutosaveKey;
    private readonly DispatcherTimer _autosaveTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    // Default directory holding the bundled UDB game configurations (the default config lives here too).
    private static string DefaultConfigDir =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "dev", "repos", "UltimateDoomBuilder", "Assets", "Common", "Configurations");

    // Standard macOS GZDoom install, used by Test Map when no port is configured.
    private const string DefaultGzdoomPath = "/Applications/GZDoom.app/Contents/MacOS/gzdoom";

    private Settings _settings = new();
    private readonly string _settingsPath = Settings.DefaultPath;
    private IReadOnlyList<EditorShortcutBinding> _shortcutBindings = EditorCommandCatalog.DefaultShortcuts;
    private readonly HashSet<string> _pressedWindowShortcuts = new(StringComparer.Ordinal);
    private bool _syncingAutomapControls;
    private CommentsPanelWindow? _commentsPanel;
    private UndoRedoPanelWindow? _undoRedoPanel;
    private TagExplorerWindow? _tagExplorer;
    private UsdfConversationWindow? _usdfConversations;
    private UdbScriptDockerWindow? _udbScriptDocker;
    private UdbScriptRunnerWindow? _udbScriptRunner;
    private UdbScriptInfo? _pendingUdbScript;
    private IReadOnlyDictionary<int, UdbScriptInfo?> _udbScriptSlotAssignments = new Dictionary<int, UdbScriptInfo?>();
    private BlockmapExplorerWindow? _blockmapExplorer;
    private SoundEnvironmentWindow? _soundEnvironments;
    private SoundEnvironmentModeModel? _soundEnvironmentModel;
    private InterpolationTools.Mode _gradientInterpolationMode = InterpolationTools.Mode.LINEAR;
    private string? _lastPrefabPath;

    // The game-config directory, overridable via settings (falls back to the bundled location).
    private string ConfigDir => string.IsNullOrWhiteSpace(_settings.ConfigDir) ? DefaultConfigDir : _settings.ConfigDir!;

    private string NodebuilderConfigDir
    {
        get
        {
            string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" : "Linux";
            string? assetsRoot = AssetsRootFromConfigDir(ConfigDir);
            return assetsRoot is null
                ? ""
                : System.IO.Path.Combine(assetsRoot, platform, "Compilers", "Nodebuilders");
        }
    }

    private string ScriptConfigDir
    {
        get
        {
            string? assetsRoot = AssetsRootFromConfigDir(ConfigDir);
            return assetsRoot is null ? "" : System.IO.Path.Combine(assetsRoot, "Common", "Scripting");
        }
    }

    public MainWindow() : this(null) { }

    public MainWindow(string? openPath)
    {
        InitializeComponent();
        ShowActivated = true;
        _autosaveTimer.Tick += (_, _) => WriteAutosaveIfPending();
        MapView.CursorWorldMoved += w => CoordText.Text = StatusBarModel.CoordinateText(w.x, w.y);
        MapView.Picked += _ =>
        {
            UpdateInfo();
            UpdateStatusDetails();
            RefreshCommentsPanel();
        };
        MapView.EditBegun += desc => CreateUndo(desc);
        MapView.Changed += () =>
        {
            UpdateInfo();
            RefreshCommentsPanel();
            RefreshTagExplorer();
        };
        MapView.EditRequested += OnEditSelected;
        MapView.ModeChanged += () =>
        {
            SetStatus(MapView.In3DMode ? "Mode: 3D" : MapView.AutomapMode ? "Mode: Automap" : MapView.WadAuthorMode ? "Mode: WadAuthor" : MapView.ImageExampleMode ? "Mode: Image Example" : $"Mode: {MapView.CurrentEditMode}");
            UpdateInfo();
            UpdateStatusDetails();
            RefreshCommentsPanel();
        };
        MapView.ActionStateChanged += () =>
        {
            if (_settings.UseHighlight != MapView.UseHighlight)
            {
                _settings.UseHighlight = MapView.UseHighlight;
                SaveSettings();
            }
            if (_settings.AlphaBasedTextureHighlighting != MapView.AlphaBasedTextureHighlighting)
            {
                _settings.AlphaBasedTextureHighlighting = MapView.AlphaBasedTextureHighlighting;
                SaveSettings();
            }
            if (_settings.ModelRenderMode != (int)MapView.ModelRenderMode)
            {
                _settings.ModelRenderMode = (int)MapView.ModelRenderMode;
                SaveSettings();
            }
            if (_settings.LightRenderMode != (int)MapView.LightRenderMode)
            {
                _settings.LightRenderMode = (int)MapView.LightRenderMode;
                SaveSettings();
            }
            if (_settings.EnhancedRenderingEffects != MapView.EnhancedRenderingEffects)
            {
                _settings.EnhancedRenderingEffects = MapView.EnhancedRenderingEffects;
                SaveSettings();
            }
            if (_settings.ClassicRendering != MapView.ClassicRendering)
            {
                _settings.ClassicRendering = MapView.ClassicRendering;
                SaveSettings();
            }
            if (_settings.DrawFog != MapView.DrawFog)
            {
                _settings.DrawFog = MapView.DrawFog;
                SaveSettings();
            }
            if (_settings.DrawSky != MapView.DrawSky)
            {
                _settings.DrawSky = MapView.DrawSky;
                SaveSettings();
            }
            if (_settings.ShowEventLines != MapView.ShowEventLines)
            {
                _settings.ShowEventLines = MapView.ShowEventLines;
                SaveSettings();
            }
            if (_settings.ShowVisualVertices != MapView.ShowVisualVertices)
            {
                _settings.ShowVisualVertices = MapView.ShowVisualVertices;
                SaveSettings();
            }
            if (_settings.SelectAdjacentVisualVertexSlopeHandles != MapView.SelectAdjacentVisualVertexSlopeHandles)
            {
                _settings.SelectAdjacentVisualVertexSlopeHandles = MapView.SelectAdjacentVisualVertexSlopeHandles;
                SaveSettings();
            }
            UpdateCommandAvailability();
            UpdateStatusDetails();
        };
        MapView.Target3DChanged += desc => { if (desc.Length > 0) SetStatus($"3D target: {desc}  (wheel raises/lowers, Shift = 1)"); };
        MapView.BrowseTexturesRequested += OnBrowseTextures;
        MapView.PastePropertiesOptionsRequested += () => OnPastePropertiesWithOptions(this, new RoutedEventArgs());
        MapView.VisplaneExplorerRequested += () => OnVisplaneExplorerMode(this, new RoutedEventArgs());
        Opened += OnOpened;
        MapView.DrawModeChanged += () =>
        {
            SetStatus(MapView.DrawMode
                ? "Draw mode: click to place vertices, click the first point or Enter to close, Esc/right-click to cancel."
                : "Draw mode off.");
            UpdateStatusDetails();
        };
        Activated += (_, _) => FocusMapViewForShortcuts();

        _settings = Settings.Load(_settingsPath);
        MapView.PasteOptions = _settings.NormalizedPasteOptions;
        MapView.DrawLineSettings = _settings.NormalizedDrawLineSettings;
        MapView.DrawRectangleSettings = _settings.NormalizedDrawRectangleSettings;
        MapView.DrawEllipseSettings = _settings.NormalizedDrawEllipseSettings;
        MapView.DrawCurveSettings = _settings.NormalizedDrawCurveSettings;
        MapView.DrawGridSettings = _settings.NormalizedDrawGridSettings;
        MapView.AutomapSettings = _settings.NormalizedAutomapSettings;
        MapView.DynamicGridSizeEnabled = _settings.DynamicGridSize;
        MapView.SetUseHighlight(_settings.UseHighlight);
        MapView.SetAlphaBasedTextureHighlighting(_settings.AlphaBasedTextureHighlighting);
        MapView.SetModelRenderMode(_settings.NormalizedModelRenderMode);
        MapView.SetLightRenderMode(_settings.NormalizedLightRenderMode);
        MapView.SetEnhancedRenderingEffects(_settings.EnhancedRenderingEffects);
        MapView.SetClassicRendering(_settings.ClassicRendering);
        MapView.SetDrawFog(_settings.DrawFog);
        MapView.SetDrawSky(_settings.DrawSky);
        MapView.SetShowEventLines(_settings.ShowEventLines);
        MapView.SetShowVisualVertices(_settings.ShowVisualVertices);
        MapView.SetSelectAdjacentVisualVertexSlopeHandles(_settings.SelectAdjacentVisualVertexSlopeHandles);
        MapView.SetViewMode2D((MapControl.ClassicViewMode)_settings.NormalizedDefaultViewMode);
        ApplyShortcutBindings();
        _statusHistory.SetCapacity(_settings.NormalizedStatusHistoryLimit);
        ApplyWindowPlacement();
        ReloadCompilerConfiguration();
        RebuildSelectionGroupsMenu();
        RebuildRecentMenu();
        TryLoadDefaultConfig();
        UpdateAutomapOptionControls();

        if (openPath != null && System.IO.File.Exists(openPath))
            _ = LoadArchive(openPath, promptForMap: false);

        UpdateStatusDetails();
        UpdateCommandAvailability();
    }

    private void SaveSettings() => _settings.Save(_settingsPath);

    private void SyncMapOptionsToView() => MapView.MapOptions = _mapOptions;

    private void RememberLastMapFolder(string path)
    {
        _settings.RememberMapFolderForPath(path, System.IO.Directory.Exists);
        SaveSettings();
    }

    private async Task<IStorageFolder?> LastUsedMapFolderAsync(TopLevel top)
    {
        string? folder = Settings.ExistingMapFolder(_settings.LastUsedMapFolder, System.IO.Directory.Exists);
        if (folder is null) return null;
        try { return await top.StorageProvider.TryGetFolderFromPathAsync(folder); }
        catch { return null; }
    }

    private void ApplyShortcutBindings()
    {
        _shortcutBindings = EditorCommandCatalog.EffectiveShortcuts(_settings.ShortcutOverrides);
        MapView.ShortcutBindings = _shortcutBindings;
        ApplyMenuShortcutTooltips();
        ApplyToolbarShortcutTooltips();
    }

    private void ApplyMenuShortcutTooltips()
    {
        SetShortcutToolTip(NewMapMenuItem, "New Map", "window.new-map");
        SetShortcutToolTip(OpenWadMenuItem, "Open WAD", "window.open-map");
        SetShortcutToolTip(RecoverAutosaveMenuItem, "Recover Autosave", "window.recover-autosave");
        SetShortcutToolTip(OpenMapMenuItem, "Open Map", "window.open-map-in-current-wad");
        SetShortcutToolTip(ReloadMapMenuItem, "Reload Map", "window.reload-map");
        SetShortcutToolTip(CloseMapMenuItem, "Close Map", "window.close-map");
        SetShortcutToolTip(AddResourceMenuItem, "Add Resource", "window.add-resource");
        SetShortcutToolTip(AddResourceDirectoryMenuItem, "Add Resource Directory", "window.add-resource-directory");
        SetShortcutToolTip(SaveMenuItem, "Save WAD", "window.save");
        SetShortcutToolTip(SaveAsFormatMenuItem, "Save As Format", "window.save-as-format");
        SetShortcutToolTip(LoadGameConfigMenuItem, "Game Configurations", "window.game-configurations");
        SetShortcutToolTip(MapOptionsMenuItem, "Map Options", "window.map-options");
        SetShortcutToolTip(SettingsMenuItem, "Preferences", "window.preferences");
        SetShortcutToolTip(ExitMenuItem, "Exit", "window.exit");
        SetShortcutToolTip(UndoMenuItem, "Undo", "window.undo");
        SetShortcutToolTip(RedoMenuItem, "Redo", "window.redo");
        SetShortcutToolTip(CutMenuItem, "Cut selection", "window.cut");
        SetShortcutToolTip(CopyMenuItem, "Copy selection", "window.copy");
        SetShortcutToolTip(PasteMenuItem, "Paste selection", "window.paste");
        SetShortcutToolTip(PasteSpecialMenuItem, "Paste Selection Special", "window.paste-special");
        SetShortcutToolTip(DuplicateMenuItem, "Duplicate selection", "window.duplicate");
        SetShortcutToolTip(CopyPropertiesMenuItem, "Copy Properties", "window.copy-properties");
        SetShortcutToolTip(PastePropertiesMenuItem, "Paste Properties", "window.paste-properties");
        SetShortcutToolTip(PastePropertiesOptionsMenuItem, "Paste Properties With Options", "window.paste-properties-options");
        SetShortcutToolTip(DeleteMenuItem, "Delete selection", "window.delete");
        SetShortcutToolTip(SelectAllMenuItem, "Select all", "window.select-all");
        SetShortcutToolTip(InvertSelectionMenuItem, "Invert selection", "window.invert-selection");
        SetShortcutToolTip(SelectSimilarMenuItem, "Select Similar Map Elements", "window.select-similar");
        SetShortcutToolTip(SelectNoneMenuItem, "Select none", "window.select-none");
        SetShortcutToolTip(ChangeMapElementIndexMenuItem, "Change Map Element Index", "window.change-map-element-index");
        SetShortcutToolTip(StitchMenuItem, "Stitch geometry", "window.stitch-geometry");
        SetShortcutToolTip(JoinSectorsMenuItem, "Join sectors", "window.join-sectors");
        SetShortcutToolTip(MergeSectorsMenuItem, "Merge sectors", "window.merge-sectors");
        SetShortcutToolTip(LowerFloor8MenuItem, "Lower Floor by 8 mp", "map2d.lower-floor-8");
        SetShortcutToolTip(RaiseFloor8MenuItem, "Raise Floor by 8 mp", "map2d.raise-floor-8");
        SetShortcutToolTip(LowerCeiling8MenuItem, "Lower Ceiling by 8 mp", "map2d.lower-ceiling-8");
        SetShortcutToolTip(RaiseCeiling8MenuItem, "Raise Ceiling by 8 mp", "map2d.raise-ceiling-8");
        SetShortcutToolTip(RaiseBrightness8MenuItem, "Increase Brightness by 8", "map2d.raise-brightness-8");
        SetShortcutToolTip(LowerBrightness8MenuItem, "Decrease Brightness by 8", "map2d.lower-brightness-8");
        SetShortcutToolTip(FlipHorizontalMenuItem, "Flip Horizontal", "window.flip-selection-horizontal");
        SetShortcutToolTip(FlipVerticalMenuItem, "Flip Vertical", "window.flip-selection-vertical");
        SetShortcutToolTip(RotateCwMenuItem, "Rotate 90 CW", "window.rotate-selection-cw");
        SetShortcutToolTip(RotateCcwMenuItem, "Rotate 90 CCW", "window.rotate-selection-ccw");
        SetShortcutToolTip(ScaleUpMenuItem, "Scale Up", "window.scale-selection-up");
        SetShortcutToolTip(ScaleDownMenuItem, "Scale Down", "window.scale-selection-down");
        SetShortcutToolTip(SelectSingleSidedMenuItem, "Select Single-sided", "map2d.select-single-sided");
        SetShortcutToolTip(SelectDoubleSidedMenuItem, "Select Double-sided", "map2d.select-double-sided");
        SetShortcutToolTip(FlipLinedefsMenuItem, "Flip Linedefs", "map2d.flip");
        SetShortcutToolTip(FlipSidedefsMenuItem, "Flip Sidedefs", "map2d.flip-sidedefs");
        SetShortcutToolTip(AlignLinedefsMenuItem, "Align Linedefs", "map2d.align-linedefs");
        SetShortcutToolTip(SplitLinedefsMenuItem, "Split Linedefs", "map2d.split-linedefs");
        SetShortcutToolTip(AlignHorizontalMenuItem, "Align textures X", "map2d.align-textures-x");
        SetShortcutToolTip(AlignVerticalMenuItem, "Align textures Y", "map2d.align-textures-y");
        SetShortcutToolTip(FitSelectedTexturesMenuItem, "Fit Selected Textures", "map2d.fit-selected-textures");
        SetShortcutToolTip(AlignFloorToFrontMenuItem, "Align Floor to Front Side", "window.align-floor-to-front");
        SetShortcutToolTip(AlignFloorToBackMenuItem, "Align Floor to Back Side", "window.align-floor-to-back");
        SetShortcutToolTip(AlignCeilingToFrontMenuItem, "Align Ceiling to Front Side", "window.align-ceiling-to-front");
        SetShortcutToolTip(AlignCeilingToBackMenuItem, "Align Ceiling to Back Side", "window.align-ceiling-to-back");
        SetShortcutToolTip(AlignThingsToWallMenuItem, "Align Things to Wall", "window.align-things-to-wall");
        SetShortcutToolTip(AutoClearSidedefTexturesMenuItem, "Auto Clear Sidedef Textures", "window.toggle-auto-clear-sidedef-textures");
        SetShortcutToolTip(FindReplaceMenuItem, "Find and Replace", "window.find-replace");
        SetShortcutToolTip(PropertiesMenuItem, "Properties", "window.properties");
        SetShortcutToolTip(FlagsMenuItem, "Flags", "window.flags");
        SetShortcutToolTip(CustomFieldsMenuItem, "Custom Fields", "window.custom-fields");
        SetShortcutToolTip(TagsMenuItem, "Tags", "window.tags");
        SetShortcutToolTip(PlaceThingsMenuItem, "Place Things", "map2d.place-things");
        SetShortcutToolTip(InsertPrefabMenuItem, "Insert Prefab File", "window.insert-prefab-file");
        SetShortcutToolTip(InsertPreviousPrefabMenuItem, "Insert Previous Prefab", "window.insert-previous-prefab");
        SetShortcutToolTip(SavePrefabMenuItem, "Create Prefab", "window.create-prefab");
        SetShortcutToolTip(FitMenuItem, "Fit to map", "map2d.fit");
        SetShortcutToolTip(GoToCoordinatesMenuItem, "Go To Coordinates", "window.go-to-coordinates");
        SetShortcutToolTip(AutomapModeMenuItem, "Automap Mode", "map2d.mode-automap");
        SetShortcutToolTip(WadAuthorModeMenuItem, "WadAuthor Mode", "map2d.mode-wadauthor");
        SetShortcutToolTip(VisplaneExplorerModeMenuItem, "Visplane Explorer Mode", "map2d.mode-visplane-explorer");
        SetShortcutToolTip(TagStatisticsMenuItem, "View Used Tags", "window.view-used-tags");
        SetShortcutToolTip(TagExplorerMenuItem, "Tag Explorer", "window.tag-explorer");
        SetShortcutToolTip(ThingStatisticsMenuItem, "View Thing Types", "window.view-thing-types");
        SetShortcutToolTip(InfoPanelMenuItem, "Toggle Info Panel", "window.toggle-info-panel");
        SetShortcutToolTip(UndoRedoPanelMenuItem, "Undo / Redo Panel", "window.undo-redo-panel");
        SetShortcutToolTip(CommentsPanelMenuItem, "Comments", "window.comments-panel");
        SetShortcutToolTip(ToggleCommentsMenuItem, "Toggle Comments", "map2d.toggle-comments");
        SetShortcutToolTip(NodesViewerMenuItem, "Nodes Viewer", "window.nodes-viewer");
        SetShortcutToolTip(StatusHistoryMenuItem, "Status History", "window.status-history");
        SetShortcutToolTip(ErrorLogMenuItem, "Show Errors and Warnings", "window.show-errors");
        SetShortcutToolTip(BrowseWallTexturesMenuItem, "Browse Textures", "window.browse-wall-textures");
        SetShortcutToolTip(BrowseFlatsMenuItem, "Browse Flats", "window.browse-flats");
        SetShortcutToolTip(BrowseFloorFlatsMenuItem, "Set Selected Floor Flats", "window.browse-floor-flats");
        SetShortcutToolTip(BrowseCeilingFlatsMenuItem, "Set Selected Ceiling Flats", "window.browse-ceiling-flats");
        SetShortcutToolTip(BrowseThingsMenuItem, "Browse Things", "window.browse-things");
        SetShortcutToolTip(BrowseLinedefActionsMenuItem, "Browse Linedef Actions", "window.browse-linedef-actions");
        SetShortcutToolTip(BrowseSectorEffectsMenuItem, "Browse Sector Effects", "window.browse-sector-effects");
        SetShortcutToolTip(Toggle3DModeMenuItem, "Toggle 3D Mode", "map2d.toggle-3d");
        SetShortcutToolTip(MoveCameraToCursorMenuItem, "Move Camera to Cursor", "map3d.move-camera-to-cursor");
        SetShortcutToolTip(ToggleFullBrightnessMenuItem, "Full Brightness", "map2d.toggle-full-brightness");
        SetShortcutToolTip(ToggleHighlightMenuItem, "Highlight", "map2d.toggle-highlight");
        SetShortcutToolTip(ModelRenderNoneMenuItem, "No Model Rendering", "window.model-render-none");
        SetShortcutToolTip(ModelRenderSelectionMenuItem, "Model Rendering Selection Only", "window.model-render-selection");
        SetShortcutToolTip(ModelRenderActiveFilterMenuItem, "Model Rendering Active Things Filter Only", "window.model-render-active-filter");
        SetShortcutToolTip(ModelRenderAllMenuItem, "Model Rendering All", "window.model-render-all");
        SetShortcutToolTip(NextModelRenderModeMenuItem, "Next Model Rendering Mode", "window.next-model-render-mode");
        SetShortcutToolTip(ViewModeWireframeMenuItem, "View Wireframe", "map2d.view-mode-wireframe");
        SetShortcutToolTip(ViewModeBrightnessMenuItem, "View Brightness Levels", "map2d.view-mode-brightness");
        SetShortcutToolTip(ViewModeFloorsMenuItem, "View Floor Textures", "map2d.view-mode-floors");
        SetShortcutToolTip(ViewModeCeilingsMenuItem, "View Ceiling Textures", "map2d.view-mode-ceilings");
        SetShortcutToolTip(NextViewModeMenuItem, "Next View Mode", "map2d.next-view-mode");
        SetShortcutToolTip(PreviousViewModeMenuItem, "Previous View Mode", "map2d.previous-view-mode");
        SetShortcutToolTip(ToggleSectorFillsMenuItem, "Show Sector Fills", "map2d.toggle-sector-fills");
        SetShortcutToolTip(ToggleThingsMenuItem, "Show Things", "map2d.toggle-things");
        SetShortcutToolTip(ToggleThingArrowsMenuItem, "Things as arrows", "map2d.toggle-thing-arrows");
        SetShortcutToolTip(ToggleFixedThingsScaleMenuItem, "Fixed Things Scale", "map2d.toggle-fixed-things-scale");
        SetShortcutToolTip(ToggleAlwaysShowVerticesMenuItem, "Always Show Vertices", "map2d.toggle-always-show-vertices");
        SetShortcutToolTip(Toggle3DFloorsMenuItem, "Show 3D Floors", "window.toggle-3d-floors");
        SetShortcutToolTip(ThingFilterMenuItem, "Configure Things Filters", "window.things-filters-setup");
        SetShortcutToolTip(FilterSelectedThingsMenuItem, "Filter Selected Things", "window.filter-selected-things");
        SetShortcutToolTip(VerticesModeMenuItem, "Vertices", "map2d.mode-vertices");
        SetShortcutToolTip(LinedefsModeMenuItem, "Linedefs", "map2d.mode-linedefs");
        SetShortcutToolTip(SectorsModeMenuItem, "Sectors", "map2d.mode-sectors");
        SetShortcutToolTip(ThingsModeMenuItem, "Things", "map2d.mode-things");
        SetShortcutToolTip(GridSetupMenuItem, "Grid and Backdrop Setup", "window.grid-setup");
        SetShortcutToolTip(SmartGridTransformMenuItem, "Smart Grid Transform", "map2d.smart-grid-transform");
        SetShortcutToolTip(AlignGridToLinedefMenuItem, "Align Grid to Selected Linedef", "map2d.align-grid-to-linedef");
        SetShortcutToolTip(SetGridOriginToVertexMenuItem, "Set Grid Origin to Selected Vertex", "map2d.set-grid-origin-to-vertex");
        SetShortcutToolTip(ResetGridTransformMenuItem, "Reset Grid Transform", "map2d.reset-grid-transform");
        SetShortcutToolTip(ToggleSnapToGridMenuItem, "Toggle grid snap", "map2d.toggle-grid-snap");
        SetShortcutToolTip(ToggleDynamicGridSizeMenuItem, "Dynamic Grid Size", "map2d.toggle-dynamic-grid-size");
        SetShortcutToolTip(GridSizeDownMenuItem, "Decrease grid size", "map2d.grid-down");
        SetShortcutToolTip(GridSizeUpMenuItem, "Increase grid size", "map2d.grid-up");
        SetShortcutToolTip(ToggleBlockmapMenuItem, "Show Blockmap", "window.toggle-blockmap");
        SetShortcutToolTip(ToggleNodesMenuItem, "Show Nodes", "window.toggle-nodes");
        SetShortcutToolTip(InsertAtCursorMenuItem, "Insert at Cursor", "map2d.insert");
        SetShortcutToolTip(MakeSectorAtCursorMenuItem, "Make Sector at Cursor", "map2d.make-sector");
        SetShortcutToolTip(DrawSectorMenuItem, "Draw Sector", "map2d.draw-sector");
        SetShortcutToolTip(DrawLinesMenuItem, "Draw Lines Only", "map2d.draw-lines");
        SetShortcutToolTip(DrawCurveMenuItem, "Draw Curve", "map2d.draw-curve");
        SetShortcutToolTip(DrawRectangleMenuItem, "Draw Rectangle", "map2d.draw-rectangle");
        SetShortcutToolTip(DrawEllipseMenuItem, "Draw Ellipse", "map2d.draw-ellipse");
        SetShortcutToolTip(DrawGridMenuItem, "Draw Grid", "map2d.draw-grid");
        SetShortcutToolTip(CheckMapMenuItem, "Check Map", "window.check-map");
        SetShortcutToolTip(CleanUpGeometryMenuItem, "Clean Up Geometry", "window.clean-up-geometry");
        SetShortcutToolTip(ReloadResourcesMenuItem, "Reload Resources", "window.reload-resources");
        SetShortcutToolTip(TestMapMenuItem, "Test Map", "window.test-map");
        SetShortcutToolTip(UdbScriptDockerMenuItem, "Scripts", "window.udbscripts");
        SetShortcutToolTip(SoundPropagationMenuItem, "Sound Propagation", "window.sound-propagation-mode");
        SetShortcutToolTip(SoundEnvironmentsMenuItem, "Sound Environments", "window.sound-environment-mode");
        SetShortcutToolTip(SoundPropagationColorsMenuItem, "Sound Propagation Colors", "window.sound-propagation-colors");
        SetShortcutToolTip(BlockmapExplorerMenuItem, "Blockmap Explorer", "window.blockmap-explorer");
        SetShortcutToolTip(BuildBridgeMenuItem, "Build Bridge", "window.build-bridge");
        SetShortcutToolTip(MakeDoorMenuItem, "Make Door", "window.make-door");
        SetShortcutToolTip(BuildStairsMenuItem, "Build Stairs", "window.build-stairs");
        SetShortcutToolTip(GradientFloorHeightsMenuItem, "Gradient Floor Heights", "window.gradient-floor-heights");
        SetShortcutToolTip(GradientCeilingHeightsMenuItem, "Gradient Ceiling Heights", "window.gradient-ceiling-heights");
        SetShortcutToolTip(GradientBrightnessMenuItem, "Gradient Brightness", "window.gradient-sector-brightness");
        SetShortcutToolTip(GradientFloorLightMenuItem, "Gradient Floor Light", "window.gradient-floor-light");
        SetShortcutToolTip(GradientCeilingLightMenuItem, "Gradient Ceiling Light", "window.gradient-ceiling-light");
        SetShortcutToolTip(GradientLightColorMenuItem, "Gradient Light Color", "window.gradient-light-color");
        SetShortcutToolTip(GradientFadeColorMenuItem, "Gradient Fade Color", "window.gradient-fade-color");
        SetShortcutToolTip(GradientLightAndFadeColorMenuItem, "Gradient Light and Fade Colors", "window.gradient-light-and-fade-colors");
        SetShortcutToolTip(GradientLinedefBrightnessMenuItem, "Gradient Linedef Brightness", "window.gradient-linedef-brightness");
        SetShortcutToolTip(GradientInterpolationLinearMenuItem, "Gradient Interpolation Linear", "window.gradient-interpolation-linear");
        SetShortcutToolTip(GradientInterpolationEaseInOutSineMenuItem, "Gradient Interpolation Ease In/Out Sine", "window.gradient-interpolation-ease-in-out-sine");
        SetShortcutToolTip(GradientInterpolationEaseInSineMenuItem, "Gradient Interpolation Ease In Sine", "window.gradient-interpolation-ease-in-sine");
        SetShortcutToolTip(GradientInterpolationEaseOutSineMenuItem, "Gradient Interpolation Ease Out Sine", "window.gradient-interpolation-ease-out-sine");
        SetShortcutToolTip(ApplySlopeArchMenuItem, "Apply Slope Arch", "window.apply-slope-arch");
        SetShortcutToolTip(ApplySlopesMenuItem, "Apply Slopes", "window.apply-slopes");
        SetShortcutToolTip(UsdfConversationsMenuItem, "Dialog Editor", "window.usdf-dialog-editor");
        SetShortcutToolTip(ToggleAutomapSecretLineMenuItem, "Toggle Selected Line Secret", "window.toggle-automap-secret-line");
        SetShortcutToolTip(ToggleAutomapHiddenLineMenuItem, "Toggle Selected Line Hidden", "window.toggle-automap-hidden-line");
        SetShortcutToolTip(ToggleAutomapTexturedHiddenSectorMenuItem, "Toggle Selected Sector Textured Hidden", "window.toggle-automap-textured-hidden-sector");
        SetShortcutToolTip(SectorColorMenuItem, "Sector Color", "window.sector-color");
        SetShortcutToolTip(DynamicLightColorMenuItem, "Dynamic Light Color", "window.dynamic-light-color");
        SetShortcutToolTip(TagRangeMenuItem, "Tag Range", "window.tag-range");
        SetShortcutToolTip(ImageExampleMenuItem, "Image Example", "map2d.mode-image-example");
        SetShortcutToolTip(ImportObjTerrainMenuItem, "Import OBJ Terrain", "window.import-obj-terrain");
        SetShortcutToolTip(ExportObjectMenuItem, "Export Object OBJ", "window.export-object");
        SetShortcutToolTip(ExportImageMenuItem, "Export Image PNG", "window.export-image");
        SetShortcutToolTip(ExportWavefrontMenuItem, "Export Wavefront OBJ", "window.export-wavefront");
        SetShortcutToolTip(ExportIdStudioMenuItem, "Export idStudio", "window.export-idstudio");
        SetShortcutToolTip(RejectViewerMenuItem, "Reject Explorer", "window.reject-explorer");
        SetShortcutToolTip(ShortcutsMenuItem, "Shortcuts", "window.shortcuts");
        SetShortcutToolTip(AboutMenuItem, "About", "window.about");
    }

    private void ApplyToolbarShortcutTooltips()
    {
        SetShortcutToolTip(OpenMapButton, "Open Map", "window.open-map-in-current-wad");
        SetShortcutToolTip(ReloadMapButton, "Reload Map", "window.reload-map");
        SetShortcutToolTip(CloseMapButton, "Close Map", "window.close-map");
        SetShortcutToolTip(SaveButton, "Save WAD", "window.save");
        SetShortcutToolTip(DeleteButton, "Delete Selection", "window.delete");
        SetShortcutToolTip(FitButton, "Fit to Map", "map2d.fit");
        SetShortcutToolTip(Toggle3DModeButton, "Toggle 3D Mode", "map2d.toggle-3d");
        SetShortcutToolTip(WadAuthorModeButton, "WadAuthor Mode", "map2d.mode-wadauthor");
        SetShortcutToolTip(VerticesModeButton, "Vertices Mode", "map2d.mode-vertices");
        SetShortcutToolTip(LinedefsModeButton, "Linedefs Mode", "map2d.mode-linedefs");
        SetShortcutToolTip(SectorsModeButton, "Sectors Mode", "map2d.mode-sectors");
        SetShortcutToolTip(ThingsModeButton, "Things Mode", "map2d.mode-things");
        SetShortcutToolTip(InsertAtCursorButton, "Insert at Cursor", "map2d.insert");
        SetShortcutToolTip(MakeSectorAtCursorButton, "Make Sector at Cursor", "map2d.make-sector");
        SetShortcutToolTip(DrawSectorButton, "Draw Sector", "map2d.draw-sector");
        SetShortcutToolTip(DrawLinesButton, "Draw Lines Only", "map2d.draw-lines");
        SetShortcutToolTip(DrawCurveButton, "Draw Curve", "map2d.draw-curve");
        SetShortcutToolTip(DrawRectangleButton, "Draw Rectangle", "map2d.draw-rectangle");
        SetShortcutToolTip(DrawEllipseButton, "Draw Ellipse", "map2d.draw-ellipse");
        SetShortcutToolTip(DrawGridButton, "Draw Grid", "map2d.draw-grid");
        SetShortcutToolTip(CheckMapButton, "Check Map", "window.check-map");
        SetShortcutToolTip(CleanUpGeometryButton, "Clean Up Geometry", "window.clean-up-geometry");
        SetShortcutToolTip(ReloadResourcesButton, "Reload Resources", "window.reload-resources");
        SetShortcutToolTip(TestMapButton, "Test Map", "window.test-map");
        SetShortcutToolTip(BuildBridgeButton, "Build Bridge", "window.build-bridge");
        SetShortcutToolTip(MakeDoorButton, "Make Door", "window.make-door");
        SetShortcutToolTip(BuildStairsButton, "Build Stairs", "window.build-stairs");
        SetShortcutToolTip(ApplySlopeArchButton, "Apply Slope Arch", "window.apply-slope-arch");
        SetShortcutToolTip(ApplySlopesButton, "Apply Slopes", "window.apply-slopes");
        SetShortcutToolTip(SectorColorButton, "Sector Color", "window.sector-color");
        SetShortcutToolTip(DynamicLightColorButton, "Dynamic Light Color", "window.dynamic-light-color");
        SetShortcutToolTip(TagRangeButton, "Tag Range", "window.tag-range");
        SetShortcutToolTip(ImportObjTerrainButton, "Import OBJ Terrain", "window.import-obj-terrain");
    }

    private void SetShortcutToolTip(Control control, string label, string commandId)
        => ToolTip.SetTip(control, EditorCommandCatalog.CommandToolTip(label, commandId, _shortcutBindings));

    private void ApplyWindowPlacement()
    {
        if (_settings.WindowWidth is { } width && double.IsFinite(width) && width >= 640) Width = width;
        if (_settings.WindowHeight is { } height && double.IsFinite(height) && height >= 480) Height = height;
        if (_settings.WindowX is { } x && _settings.WindowY is { } y && double.IsFinite(x) && double.IsFinite(y))
            Position = new PixelPoint((int)Math.Round(x), (int)Math.Round(y));
    }

    private void SaveWindowPlacement()
    {
        if (WindowState != WindowState.Normal) return;
        _settings.WindowX = Position.X;
        _settings.WindowY = Position.Y;
        _settings.WindowWidth = Width;
        _settings.WindowHeight = Height;
        SaveSettings();
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        var entries = AutoSaveStore.List();
        if (entries.Count > 0) await PromptRecoverAutosave(entries);
    }

    private void LogAndSetStatus(Exception exception, string context)
    {
        ErrorLog.Append(exception, context);
        SetStatus($"{context}: {exception.Message}");
    }

    private static string? AssetsRootFromConfigDir(string configDir)
    {
        var dir = new System.IO.DirectoryInfo(configDir);
        if (!dir.Exists || dir.Name != "Configurations" || dir.Parent?.Name != "Common") return null;
        return dir.Parent.Parent?.FullName;
    }

    private void ReloadCompilerConfiguration()
    {
        _compilerConfig = CompilerConfiguration.FromDirectory(NodebuilderConfigDir);
        _scriptConfigurations = ScriptConfigurationCatalog.FromDirectory(ScriptConfigDir);
    }

    // Rebuilds the File > Open Recent submenu from persisted recent map and file lists.
    private void RebuildRecentMenu()
    {
        var items = new List<object>();
        foreach (RecentMenuEntry entry in RecentMenuModel.Build(_settings, System.IO.File.Exists))
        {
            if (entry.IsSeparator)
            {
                items.Add(new Separator());
            }
            else if (entry.IsMap)
            {
                var item = new MenuItem { Header = entry.Header };
                var captured = new RecentMapReference
                {
                    Path = entry.Path ?? "",
                    MapName = entry.MapName ?? "",
                    ArchivePath = entry.ArchivePath,
                };
                item.Click += async (_, _) => await LoadRecentMap(captured);
                items.Add(item);
            }
            else if (entry.IsFile)
            {
                var item = new MenuItem { Header = entry.Header };
                string captured = entry.Path ?? "";
                item.Click += async (_, _) =>
                {
                    await LoadArchive(captured, promptForMap: true);
                };
                items.Add(item);
            }
            else
            {
                items.Add(new MenuItem { Header = entry.Header, IsEnabled = false });
            }
        }
        OpenRecentMenu.ItemsSource = items;
    }

    private static string RecentMapHeader(RecentMapReference map)
        => RecentMenuModel.RecentMapHeader(map);

    private async Task LoadRecentMap(RecentMapReference map)
    {
        if (!await ConfirmDiscardDirtyMap()) return;
        if (!System.IO.File.Exists(map.Path))
        {
            SetStatus($"File not found: {map.Path}");
            return;
        }

        if (IsPk3Path(map.Path)) await LoadPk3(map.Path, promptForMap: false, recentMap: map);
        else await LoadWad(map.Path, promptForMap: false, preferredMapName: map.MapName);
    }

    // Attempts to load a game config on startup from DBUILDER_GAMECONFIG, the last used config, or the bundled Doom default.
    private void TryLoadDefaultConfig()
    {
        string? path = Settings.ResolveStartupConfigPath(
            Environment.GetEnvironmentVariable("DBUILDER_GAMECONFIG"),
            ConfigDir,
            _settings.LastUsedConfigName,
            System.IO.File.Exists);
        if (path != null) LoadConfig(path, auto: true);
    }

    // Maps a detected game to its bundled config filename (null when no bundled match).
    private static string? ConfigForGame(DetectedGame game) => game switch
    {
        DetectedGame.Doom => "Doom_DoomDoom.cfg",
        DetectedGame.Doom2 => "Doom_Doom2Doom.cfg",
        DetectedGame.Heretic => "Heretic_HereticDoom.cfg",
        DetectedGame.Hexen => "Hexen_HexenHexen.cfg",
        _ => null,
    };

    // When the active config is still the auto/default one, switch it to match the opened WAD's game.
    private void AutoDetectConfig(WAD wad)
    {
        if (!_configIsAuto) return;
        var file = ConfigForGame(GameDetect.FromWad(wad));
        if (file == null) return;
        string path = System.IO.Path.Combine(ConfigDir, file);
        if (System.IO.File.Exists(path) && !string.Equals(_configName, System.IO.Path.GetFileNameWithoutExtension(file)))
            LoadConfig(path, auto: true);
    }

    // Parses DECORATE + ZScript actors (and MAPINFO DoomEdNums) from the loaded resources and folds them into
    // the game config so mod thing types get titles/sprites/categories in the editor.
    private void MergeActorsFromResources()
    {
        if (_resources is null) return;
        var decorate = _resources.GetTextLumps("DECORATE");
        var zscript = _resources.GetTextLumps("ZSCRIPT");
        if (decorate.Count == 0 && zscript.Count == 0) return;

        _config ??= GameConfiguration.FromText("");

        // Collect editor numbers from every MAPINFO/ZMAPINFO DoomEdNums block (ZScript needs these).
        var doomEdNums = new Dictionary<int, string>();
        foreach (var text in _resources.GetTextLumps("MAPINFO"))
            foreach (var (n, c) in MapInfo.Parse(text).DoomEdNums) doomEdNums[n] = c;
        foreach (var text in _resources.GetTextLumps("ZMAPINFO"))
            foreach (var (n, c) in MapInfo.Parse(text).DoomEdNums) doomEdNums[n] = c;

        int count = 0;
        foreach (var text in decorate)
        {
            var decorateData = DecorateParser.ParseDocument(text, _resources.GetTextResource);
            var actors = decorateData.Actors;
            _config.MergeActors(actors, doomEdNums);
            _config.MergeDamageTypes(decorateData.DamageTypes);
            foreach (var a in actors) if (a.DoomEdNum >= 0) count++;
        }
        foreach (var text in zscript)
        {
            var actors = ZScriptParser.Parse(text, _resources.GetTextResource);
            _config.MergeActors(actors, doomEdNums);
            foreach (var a in actors)
                if (doomEdNums.Values.Contains(a.ClassName, StringComparer.OrdinalIgnoreCase)) count++;
        }

        MapView.GameConfig = _config; // refresh thing labels/sprites
        if (count > 0) SetStatus(GameConfiguration.ActorResourcesStatusText(count));
    }

    private void ApplyResourceConfig()
    {
        if (_resources is null) return;
        _resources.Configuration = _config;
        _resources.MixTexturesFlats = _config?.MixTexturesFlats ?? false;
        MapView.MapResources = _resources;
    }

    private void LoadConfig(string path, bool auto = false)
    {
        try
        {
            _config = GameConfiguration.FromFile(path);
            _configName = System.IO.Path.GetFileNameWithoutExtension(path);
            _configFile = System.IO.Path.GetFileName(path);
            _configPath = path;
            _configIsAuto = auto;
            MapView.GameConfig = _config; // enables thing sprites in the map view
            if (!auto)
            {
                _settings.LastUsedConfigName = System.IO.Path.IsPathRooted(path) ? path : _configFile;
                SaveSettings();
            }
            RebuildResourcesForActiveSource();
            SetStatus($"Game config: {_configName} ({_config.Things.Count} things, {_config.LinedefActions.Count} actions, {_config.SectorEffects.Count} sector types)");
            UpdateStatusDetails();
            UpdateInfo();
        }
        catch (Exception ex) { LogAndSetStatus(ex, "Config load failed"); }
    }

    // ---- File ----

    // Starts a fresh empty map. Keeps any open WAD's resources so textures still resolve while editing.
    private async void OnNewMap(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardDirtyMap()) return;

        var map = new MapSet();
        _map = map;
        _mapMarker = "MAP01";
        _sourceMapMarker = null;
        _activeAutosaveKey = null;
        _untitledAutosaveId = Guid.NewGuid().ToString("N");
        _wadPath = null;
        _sourceWadStamp = null;
        _pk3Path = null;
        _pk3Maps = null;
        _pk3MapArchivePath = null;
        _mapOptions = new MapOptions { CurrentName = _mapMarker, ConfigFile = _configFile };
        SyncMapOptionsToView();
        _mapSettings = new Configuration(sorted: true);
        _mapFormat = _config?.MapFormat ?? MapFormat.Doom;
        MapView.MapFormat = _mapFormat;
        _undo = new UndoManager(map);
        MapView.Map = map;
        MapView.Focus();
        MarkMapDirty();
        UpdateInfo();
        SetStatus($"New empty {_mapFormat} map for {CurrentConfigLabel()}. " + CommandHints("map2d.draw-sector", "map2d.draw-lines", "map2d.insert") + ".");
    }

    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        var top = GetTopLevel(this);
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open WAD or PK3",
            AllowMultiple = false,
            SuggestedStartLocation = await LastUsedMapFolderAsync(top),
            FileTypeFilter = new[] { new FilePickerFileType("Doom WAD or PK3") { Patterns = new[] { "*.wad", "*.pk3", "*.pk7", "*.zip" } } },
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            await LoadArchive(path, promptForMap: true);
    }

    private async void OnRecoverAutosave(object? sender, RoutedEventArgs e)
    {
        var entries = AutoSaveStore.List();
        if (entries.Count == 0)
        {
            SetStatus("No autosave snapshots found.");
            return;
        }

        await PromptRecoverAutosave(entries);
    }

    private async Task PromptRecoverAutosave(IReadOnlyList<AutoSaveEntry> entries)
    {
        var dlg = new AutoSaveRecoveryDialog(entries);
        if (!await dlg.ShowDialog<bool>(this) || dlg.Selected is not { } selected) return;
        if (!await ConfirmDiscardDirtyMap()) return;
        LoadRecoveredAutosave(selected);
    }

    // Lets the user pick any map in the currently open WAD (doom2 has 32, hexen 31, ...).
    private async void OnOpenMap(object? sender, RoutedEventArgs e)
    {
        if (_wadPath is null && _pk3Path is null) { SetStatus("Open a WAD or PK3 first."); return; }
        if (_pk3Path is not null && _pk3Maps is not null)
        {
            var pk3Maps = CurrentPk3Maps(_pk3Path);
            if (pk3Maps.Count == 0) { SetStatus("No maps in this PK3 match the active game configuration."); return; }

            var displayMaps = new List<MapEntry>();
            foreach (var pk3Map in pk3Maps) displayMaps.Add(DisplayEntry(pk3Map));
            var pk3Dialog = new MapPickerDialog(displayMaps, CurrentPk3DisplayName());
            if (await pk3Dialog.ShowDialog<bool>(this) && pk3Dialog.Selected is { } selected)
            {
                int index = displayMaps.FindIndex(m => m.Name == selected.Name && m.Format == selected.Format);
                if (index >= 0 && await ConfirmDiscardDirtyMap())
                {
                    _pk3Maps = pk3Maps;
                    LoadPk3MapEntry(pk3Maps[index]);
                }
            }
            return;
        }

        List<MapEntry> maps;
        using (var wad = new WAD(_wadPath!, openreadonly: true)) maps = CurrentWadMaps(wad);
        if (maps.Count == 0) { SetStatus("No maps in this WAD."); return; }

        var dlg = new MapPickerDialog(maps, _mapMarker, OpenMapOptionsForWadEntry);
        if (await dlg.ShowDialog<bool>(this) && dlg.Selected is { } entry && await ConfirmDiscardDirtyMap())
            LoadMapEntry(entry, dlg.SelectedOptions);
    }

    private async void OnReloadMap(object? sender, RoutedEventArgs e)
    {
        if (_wadPath is null && _pk3Path is null)
        {
            SetStatus("No open map to reload.");
            return;
        }
        if (!await ConfirmDiscardDirtyMap()) return;

        if (_wadPath is not null)
        {
            string path = _wadPath;
            string mapName = _mapMarker ?? "MAP01";
            await LoadWad(path, promptForMap: false, preferredMapName: mapName);
            return;
        }

        if (_pk3Path is not null && _mapMarker is not null)
        {
            var map = new RecentMapReference
            {
                Path = _pk3Path,
                MapName = _mapMarker,
                ArchivePath = _pk3MapArchivePath,
            };
            await LoadPk3(_pk3Path, promptForMap: false, recentMap: map);
        }
    }

    private async void OnCloseMap(object? sender, RoutedEventArgs e)
    {
        if (_map is null)
        {
            SetStatus("No map loaded.");
            return;
        }
        if (!await ConfirmDiscardDirtyMap()) return;

        CloseCurrentMap();
        SetStatus("Map closed.");
    }

    // Adds a base resource beneath the current map's WAD so its textures/flats/sprites resolve.
    private async void OnAddResource(object? sender, RoutedEventArgs e)
    {
        if (_resources is null) { SetStatus("Open a WAD first."); return; }
        var top = GetTopLevel(this);
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add Resource (IWAD / PK3 / ZIP)",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("WAD or PK3") { Patterns = new[] { "*.wad", "*.pk3", "*.pk7", "*.zip", "*.pke", "*.ipk3", "*.ipk7" } } },
        });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path) return;

        await AddResourceLocation(new DataLocation(DataLocation.InferType(path), path));
    }

    private async void OnAddResourceDirectory(object? sender, RoutedEventArgs e)
    {
        if (_resources is null) { SetStatus("Open a WAD first."); return; }
        var top = GetTopLevel(this);
        if (top is null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Add Resource Directory",
            AllowMultiple = false,
        });
        if (folders.Count == 0 || folders[0].TryGetLocalPath() is not { } path) return;

        await AddResourceLocation(new DataLocation(DataLocationType.Directory, path));
    }

    private async Task AddResourceLocation(DataLocation resource)
    {
        if (_resources is null) { SetStatus("Open a WAD first."); return; }

        var requiredArchives = RequiredArchiveDetector.Detect(_config, resource);
        resource.RequiredArchives.Clear();
        resource.RequiredArchives.AddRange(requiredArchives);
        if (RequiredArchiveDetector.RequiresTestExclusion(_config, requiredArchives))
            resource.NotForTesting = true;

        var options = new ResourceOptionsDialog(resource);
        if (!await options.ShowDialog<bool>(this)) return;
        var location = options.ResultLocation;

        try
        {
            _resources.AddBaseResource(location);
            _mapOptions?.AddResource(location);
            _mapOptions?.WriteResources();
            ApplyResourceConfig();

            // Adding the IWAD often reveals the game (a PWAD alone may lack the signature lumps), so re-detect
            // the config before merging actors onto it.
            if (location.Type == DataLocationType.Wad)
            {
                try
                {
                    using var iwad = new WAD(location.Location, openreadonly: true);
                    if (_configIsAuto) AutoDetectConfig(iwad);
                    if (iwad.IsIWAD) _iwadPath = location.Location; // remember the IWAD for Test Map
                }
                catch { /* not a readable WAD - skip detection, still usable as a resource */ }
            }

            MergeActorsFromResources();
            ApplyResourceConfig(); // re-trigger texture cache invalidation + redraw
            SetStatus($"Added resource {location.GetDisplayName()} (textures/flats/actors refreshed)");
        }
        catch (Exception ex) { LogAndSetStatus(ex, "Add resource failed"); }
    }

    // Opens the texture/flat browser for the current 3D target and applies the pick to it.
    private async void OnBrowseTextures(bool flats)
    {
        if (_resources is null) { SetStatus("No resources loaded for textures."); return; }
        var dlg = new TextureBrowserDialog(_resources, flats);
        if (await dlg.ShowDialog<bool>(this) && dlg.Selected is { } name)
        {
            MapView.ApplyChosenTexture(name);
            MapView.Focus();
        }
    }

    private async void OnBrowseWallTextures(object? sender, RoutedEventArgs e)
    {
        if (_resources is null) { SetStatus("No resources loaded for textures."); return; }
        var dlg = new TextureBrowserDialog(_resources, flats: false) { Title = "Browse Textures" };
        if (await dlg.ShowDialog<bool>(this) && dlg.Selected is { } name) SetStatus($"Texture selected: {name}");
        MapView.Focus();
    }

    private async void OnBrowseFlats(object? sender, RoutedEventArgs e)
    {
        if (_resources is null) { SetStatus("No resources loaded for flats."); return; }
        var dlg = new TextureBrowserDialog(_resources, flats: true) { Title = "Browse Flats" };
        if (await dlg.ShowDialog<bool>(this) && dlg.Selected is { } name) SetStatus($"Flat selected: {name}");
        MapView.Focus();
    }

    private async void OnBrowseFloorFlats(object? sender, RoutedEventArgs e)
        => await ApplyFlatToSelectedSectors(ceiling: false);

    private async void OnBrowseCeilingFlats(object? sender, RoutedEventArgs e)
        => await ApplyFlatToSelectedSectors(ceiling: true);

    private void OnReloadResources(object? sender, RoutedEventArgs e)
    {
        if (_wadPath is null || _mapOptions is null)
        {
            SetStatus("Open a WAD map to reload resources.");
            return;
        }

        var preResult = ExternalCommand.Run(_mapOptions.ReloadResourcePreCommand, "Before reload resources");
        if (!preResult.Success)
        {
            SetStatus(preResult.Message);
            return;
        }

        int resourceIssues = RebuildWadResources(_wadPath, _mapOptions);
        var postResult = ExternalCommand.Run(_mapOptions.ReloadResourcePostCommand, "After reload resources");
        if (!postResult.Success)
        {
            SetStatus(postResult.Message);
            return;
        }

        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(ResourceManager.ReloadStatusText(resourceIssues));
    }

    private async Task ApplyFlatToSelectedSectors(bool ceiling)
    {
        if (_resources is null) { SetStatus("No resources loaded for flats."); return; }
        if (_map is null || _undo is null || _map.SelectedSectorsCount == 0)
        {
            SetStatus("Select one or more sectors before applying flats.");
            return;
        }

        var sectors = _map.GetSelectedSectors();
        var dlg = new TextureBrowserDialog(_resources, flats: true)
        {
            Title = ceiling ? "Set Ceiling Flats" : "Set Floor Flats",
        };
        if (await dlg.ShowDialog<bool>(this) && dlg.Selected is { } name)
        {
            CreateUndo(ceiling ? "Set ceiling flat" : "Set floor flat");
            foreach (var sector in sectors)
            {
                if (ceiling) sector.CeilTexture = name;
                else sector.FloorTexture = name;
            }
            MapView.MarkGeometryDirty();
            UpdateInfo();
            SetStatus(CatalogBrowse.FlatAppliedStatusText(sectors.Count, ceiling, name));
        }
        MapView.Focus();
    }

    private async void OnBrowseThingsCatalog(object? sender, RoutedEventArgs e)
        => await ShowCatalog("Browse Things", cfg => CatalogBrowse.Things(cfg), "Thing", MapView.InsertThingType,
            (number, title) =>
            {
                MapView.InsertThingType = number;
                SetStatus($"Insert thing type: {number} - {title}");
            });

    private async void OnBrowseActionsCatalog(object? sender, RoutedEventArgs e)
        => await ShowCatalog("Browse Linedef Actions", cfg => CatalogBrowse.LinedefActions(cfg), "Linedef action",
            CurrentLinedefAction(),
            (number, title) => ApplyActionToSelectedLinedefs(number, title));

    private async void OnBrowseEffectsCatalog(object? sender, RoutedEventArgs e)
        => await ShowCatalog("Browse Sector Effects", cfg => CatalogBrowse.SectorEffects(cfg), "Sector effect",
            CurrentSectorEffect(),
            (number, title) => ApplyEffectToSelectedSectors(number, title));

    private async Task ShowCatalog(
        string title,
        Func<GameConfiguration, List<BrowseEntry>> entries,
        string label,
        int current = 0,
        Action<int, string>? onSelected = null)
    {
        if (_config is null) { SetStatus("No game configuration loaded."); return; }
        var dlg = new BrowserDialog(title, entries(_config), current);
        if (await dlg.ShowDialog<bool>(this) && dlg.SelectedNumber is int n)
        {
            if (onSelected != null) onSelected(n, dlg.SelectedTitle);
            else SetStatus($"{label} selected: {n} - {dlg.SelectedTitle}");
        }
        MapView.Focus();
    }

    private int CurrentLinedefAction()
        => _map?.GetSelectedLinedefs().FirstOrDefault()?.Action ?? 0;

    private int CurrentSectorEffect()
        => _map?.GetSelectedSectors().FirstOrDefault()?.Special ?? 0;

    private void ApplyActionToSelectedLinedefs(int action, string title)
    {
        if (_map is null || _undo is null || _map.SelectedLinedefsCount == 0)
        {
            SetStatus($"Linedef action selected: {action} - {title}");
            return;
        }

        var lines = _map.GetSelectedLinedefs();
        CreateUndo("Set linedef action");
        foreach (var line in lines) line.Action = action;
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(CatalogBrowse.LinedefActionAppliedStatusText(lines.Count, action, title));
    }

    private void ApplyEffectToSelectedSectors(int effect, string title)
    {
        if (_map is null || _undo is null || _map.SelectedSectorsCount == 0)
        {
            SetStatus($"Sector effect selected: {effect} - {title}");
            return;
        }

        var sectors = _map.GetSelectedSectors();
        CreateUndo("Set sector effect");
        foreach (var sector in sectors) sector.Special = effect;
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(CatalogBrowse.SectorEffectAppliedStatusText(sectors.Count, effect, title));
    }

    private async void OnSave(object? sender, RoutedEventArgs e) => await DoSave(_mapFormat);

    private async void OnSaveAs(object? sender, RoutedEventArgs e) => await DoSave(_mapFormat, forcePicker: true);

    // Prompts for a target map format and saves a converted copy (flags translated via the game config).
    private async void OnSaveAsFormat(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("Nothing to save."); return; }
        var dlg = new FormatPickerDialog(_mapFormat);
        if (await dlg.ShowDialog<bool>(this)) await DoSave(dlg.ResultFormat, forcePicker: true);
    }

    private async System.Threading.Tasks.Task DoSave(MapFormat targetFormat, bool forcePicker = false)
    {
        if (_map is null) { SetStatus("Nothing to save."); return; }
        string? outPath = !forcePicker && _wadPath is not null && targetFormat == _mapFormat ? _wadPath : null;
        if (outPath is null)
        {
            var top = GetTopLevel(this);
            if (top is null) return;
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save WAD As",
                SuggestedStartLocation = await LastUsedMapFolderAsync(top),
                SuggestedFileName = _wadPath != null
                    ? System.IO.Path.GetFileNameWithoutExtension(_wadPath) + ".edited.wad"
                    : (_mapMarker ?? "MAP01") + ".wad",
                DefaultExtension = "wad",
                FileTypeChoices = new[] { new FilePickerFileType("Doom WAD") { Patterns = new[] { "*.wad" } } },
            });
            if (file?.TryGetLocalPath() is not { } selectedPath) return;
            outPath = selectedPath;
        }

        try
        {
            string marker = _mapMarker ?? "MAP01";
            bool savedCurrentFormat = targetFormat == _mapFormat;
            bool savingActiveSource = savedCurrentFormat && IsSamePath(outPath, _wadPath);
            if (savingActiveSource && FileSaveStamp.HasChanged(_wadPath, _sourceWadStamp))
            {
                SetStatus("Save blocked: the source WAD changed on disk. Reload the map or use Save WAD As.");
                return;
            }
            if (savingActiveSource && FileSaveStamp.IsReadOnly(_wadPath))
            {
                SetStatus("Save blocked: the source WAD is read-only. Use Save WAD As or clear the read-only flag.");
                return;
            }

            // When exporting to a different format, translate the flag representation the target writer reads.
            // The fill is additive, so the in-memory map remains valid in its original format afterwards.
            if (!savedCurrentFormat)
                MapFormatConverter.Convert(_map, _mapFormat, targetFormat, _config);

            // Build the result in memory (so saving over the source WAD is safe), replacing just this map's
            // block and preserving every other lump in its original format.
            byte[] bytes;
            var msOut = new System.IO.MemoryStream();
            using (var dst = new WAD(msOut))
            {
                if (_wadPath != null && System.IO.File.Exists(_wadPath))
                {
                    using var src = new WAD(_wadPath, openreadonly: true);
                    WadMaps.CopyAllLumps(src, dst);
                    if (!string.IsNullOrEmpty(_sourceMapMarker)
                        && !string.Equals(_sourceMapMarker, marker, StringComparison.OrdinalIgnoreCase))
                        WadMaps.RenameMap(dst, _sourceMapMarker, marker);
                }
                WadMaps.SaveMap(dst, marker, _map, targetFormat, _config);
                bytes = msOut.ToArray();
            }

            // Optionally rebuild nodes via an external builder (BSP/BLOCKMAP/REJECT), making the save playable.
            string nodeStatus = BuildNodesIfConfigured(ref bytes, forTesting: false);

            System.IO.File.WriteAllBytes(outPath, bytes);
            RememberLastMapFolder(outPath);
            SaveCurrentMapOptions(outPath, marker);
            DeleteCurrentAutosave();
            if (savedCurrentFormat)
            {
                _wadPath = outPath;
                _pk3Path = null;
                _pk3Maps = null;
                _pk3MapArchivePath = null;
                _sourceMapMarker = marker;
                UpdateSourceWadStamp();
            }
            ClearMapDirty();
            string converted = targetFormat != _mapFormat ? $" (converted from {_mapFormat})" : "";
            SetStatus($"Saved {marker} [{targetFormat}]{converted} to {System.IO.Path.GetFileName(outPath)}{nodeStatus}");
        }
        catch (Exception ex) { LogAndSetStatus(ex, "Save failed"); }
    }

    private async void OnMapOptions(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        _mapOptions ??= new MapOptions { CurrentName = _mapMarker ?? "MAP01" };
        SyncMapOptionsToView();
        var dlg = new MapOptionsDialog(
            _mapMarker ?? "MAP01",
            _map.Namespace,
            _mapOptions,
            _config?.UseLongTextureNames ?? false,
            _resources,
            _scriptConfigurations,
            _config?.DefaultScriptCompiler ?? "");
        if (await dlg.ShowDialog<bool>(this))
        {
            _mapMarker = dlg.ResultMarker;
            _map.Namespace = dlg.ResultNamespace;
            _mapOptions.CurrentName = _mapMarker;
            dlg.ApplyTo(_mapOptions);
            SyncMapOptionsToView();
            MarkMapDirty();
            UpdateInfo();
            MapView.Focus();
            SetStatus($"Map options updated: {_mapMarker}.");
        }
    }

    // Runs the external node builder (DBUILDER_NODEBUILDER env, else settings) over the WAD bytes.
    // Returns a short status suffix; on failure the original (node-less) bytes are kept.
    private string BuildNodesIfConfigured(ref byte[] bytes, bool forTesting)
    {
        string? exe = Environment.GetEnvironmentVariable("DBUILDER_NODEBUILDER");
        if (string.IsNullOrWhiteSpace(exe)) exe = _settings.NodeBuilderPath;

        string? args = Environment.GetEnvironmentVariable("DBUILDER_NODEBUILDER_ARGS")
            ?? (string.IsNullOrWhiteSpace(_settings.NodeBuilderArgs) ? null : _settings.NodeBuilderArgs);

        NodebuilderConfig? cfg = null;
        string profile = NodebuilderProfileName(forTesting);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            if (!string.IsNullOrWhiteSpace(exe))
                cfg = _compilerConfig.ResolveNodebuilderConfig(profile, exe);
            else if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                cfg = _compilerConfig.ResolveNodebuilderConfig(profile);
        }

        if (cfg is null)
        {
            if (string.IsNullOrWhiteSpace(exe)) return "  (no nodes - set a node builder in Settings or DBUILDER_NODEBUILDER)";
            cfg = new NodebuilderConfig(exe, args ?? "-o \"%FO\" \"%FI\"");
        }
        else if (!string.IsNullOrWhiteSpace(args))
        {
            cfg = cfg with { Parameters = args };
        }

        if (!System.IO.File.Exists(cfg.Executable)) return $"  (node builder not found: {cfg.Executable})";

        var result = NodeBuilder.Build(bytes, cfg, mapMarker: _mapMarker, config: _config);
        if (result.Success && result.Output != null) { bytes = result.Output; return "  (nodes built)"; }
        return "  (node build FAILED, saved without nodes)";
    }

    private string NodebuilderProfileName(bool forTesting)
    {
        if (_config is null) return "";
        if (forTesting)
            return FirstNonBlank(_config.NodeBuilderTest, _config.DefaultTestCompiler, _config.NodeBuilderSave, _config.DefaultSaveCompiler);
        return FirstNonBlank(_config.NodeBuilderSave, _config.DefaultSaveCompiler);
    }

    private static string FirstNonBlank(params string[] values)
    {
        foreach (string value in values)
            if (!string.IsNullOrWhiteSpace(value)) return value;
        return "";
    }

    private static string FirstNonBlankOr(string fallback, params string[] values)
        => FirstNonBlank(values) is { Length: > 0 } value ? value : fallback;

    private void SaveCurrentMapOptions(string wadPath, string marker)
    {
        var options = _mapOptions ?? new MapOptions();
        options.CurrentName = marker;
        options.ConfigFile = _configFile;
        options.ViewPosition = MapView.ViewCenter;
        options.ViewScale = MapView.ViewScale;
        options.WriteResources();
        options.WriteDrawingOptions();
        options.WriteTagLabels();
        options.WriteExternalCommandSettings();
        options.WriteGridSetup(MapView.GridSetupSnapshot());
        var root = _mapSettings ?? new Configuration(sorted: true);
        options.WriteRootOptions(root);
        root.SaveConfiguration(DbsPath(wadPath));
        _mapOptions = options;
        SyncMapOptionsToView();
        _mapSettings = root;
    }

    // Opens the modal Settings dialog and persists changes.
    private async void OnSettings(object? sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_settings);
        if (!await dlg.ShowDialog<bool>(this)) return;
        _settings.ConfigDir = dlg.ConfigDir;
        _settings.TestPort = dlg.TestPort;
        _settings.TestIwad = dlg.TestIwad;
        _settings.TestPortArgs = dlg.TestPortArgs;
        _settings.NodeBuilderPath = dlg.NodeBuilderPath;
        _settings.NodeBuilderArgs = dlg.NodeBuilderArgs;
        _settings.UdbScriptExternalEditor = dlg.UdbScriptExternalEditor;
        _settings.MaxRecentFiles = dlg.MaxRecentFiles;
        _settings.AutoClearSidedefTextures = dlg.AutoClearSidedefTextures;
        _settings.DefaultViewMode = dlg.DefaultViewMode;
        _settings.StatusHistoryLimit = dlg.StatusHistoryLimit;
        _settings.ShortcutOverrides = dlg.ShortcutOverrides;
        _settings.PasteOptions = dlg.PasteOptions;
        MapView.PasteOptions = _settings.NormalizedPasteOptions;
        MapView.SetViewMode2D((MapControl.ClassicViewMode)_settings.NormalizedDefaultViewMode);
        ApplyShortcutBindings();
        _statusHistory.SetCapacity(_settings.NormalizedStatusHistoryLimit);
        RebuildRecentMenu();
        ReloadCompilerConfiguration();
        SaveSettings();
        SetStatus("Settings saved.");
    }

    private async void OnLoadConfig(object? sender, RoutedEventArgs e)
    {
        var currentConfig = string.IsNullOrWhiteSpace(_configPath) ? _configName : _configPath;
        var dlg = new ConfigDialog(ConfigDir, currentConfig, _settings);
        bool load = await dlg.ShowDialog<bool>(this);
        if (dlg.ResourceListChanged) SaveSettings();
        if (load && dlg.SelectedPath is { } path) LoadConfig(path);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        FocusMapViewForShortcuts();
    }

    private void FocusMapViewForShortcuts()
    {
        MacApplicationActivator.Activate();
        Activate();
        Dispatcher.UIThread.Post(() =>
        {
            MacApplicationActivator.Activate();
            Activate();
            MapView.Focus();
        }, DispatcherPriority.Loaded);
        _ = FocusMapViewAfterActivationAsync();
    }

    private async Task FocusMapViewAfterActivationAsync()
    {
        await Task.Delay(250);
        MacApplicationActivator.Activate();
        Activate();
        MapView.Focus();
    }

    // Window-level accelerators. The map control bubbles unhandled keys here. Accept both Ctrl (Win/Linux)
    // and Cmd/Meta (macOS) so undo/redo/save/delete work regardless of which the user presses.
    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        bool accel = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control)
                  || e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Meta);
        bool shift = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift);
        bool alt = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Alt);
        string key = e.Key.ToString();
        string pressKey = EditorCommandCatalog.ShortcutPressKey(EditorCommandScope.Window, key, accel, shift, alt);
        if (EditorCommandCatalog.ResolveShortcut(_shortcutBindings, EditorCommandScope.Window, key, accel, shift, alt) is { } commandId)
        {
            if (!ShouldRunWindowShortcut(commandId, pressKey))
            {
                e.Handled = true;
                return;
            }

            if (RunWindowCommand(commandId))
            {
                e.Handled = true;
                return;
            }
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(Avalonia.Input.KeyEventArgs e)
    {
        RemovePressedWindowShortcut(e.Key.ToString());
        base.OnKeyUp(e);
    }

    private bool ShouldRunWindowShortcut(string commandId, string pressKey)
    {
        bool repeated = !_pressedWindowShortcuts.Add(pressKey);
        return !repeated || EditorCommandCatalog.IsRepeatable(commandId);
    }

    private void RemovePressedWindowShortcut(string key)
    {
        string keyPrefix = EditorCommandCatalog.ShortcutReleasePrefix(EditorCommandScope.Window, key);
        _pressedWindowShortcuts.RemoveWhere(pressKey => pressKey.StartsWith(keyPrefix, StringComparison.Ordinal));
    }

    private bool RunWindowCommand(string commandId)
    {
        switch (commandId)
        {
            case "window.undo": OnUndo(this, new RoutedEventArgs()); return true;
            case "window.redo": OnRedo(this, new RoutedEventArgs()); return true;
            case "window.new-map": OnNewMap(this, new RoutedEventArgs()); return true;
            case "window.open-map": OnOpen(this, new RoutedEventArgs()); return true;
            case "window.recover-autosave": OnRecoverAutosave(this, new RoutedEventArgs()); return true;
            case "window.open-map-in-current-wad": OnOpenMap(this, new RoutedEventArgs()); return true;
            case "window.reload-map": OnReloadMap(this, new RoutedEventArgs()); return true;
            case "window.close-map": OnCloseMap(this, new RoutedEventArgs()); return true;
            case "window.add-resource": OnAddResource(this, new RoutedEventArgs()); return true;
            case "window.add-resource-directory": OnAddResourceDirectory(this, new RoutedEventArgs()); return true;
            case "window.save": OnSave(this, new RoutedEventArgs()); return true;
            case "window.save-map": OnSave(this, new RoutedEventArgs()); return true;
            case "window.save-map-as": OnSaveAs(this, new RoutedEventArgs()); return true;
            case "window.save-as-format": OnSaveAsFormat(this, new RoutedEventArgs()); return true;
            case "window.map-options": OnMapOptions(this, new RoutedEventArgs()); return true;
            case "window.snap-selection-to-grid": OnSnapSelectionToGrid(this, new RoutedEventArgs()); return true;
            case "window.game-configurations": OnLoadConfig(this, new RoutedEventArgs()); return true;
            case "window.preferences": OnSettings(this, new RoutedEventArgs()); return true;
            case "window.exit": OnExit(this, new RoutedEventArgs()); return true;
            case "window.shortcuts": OnShortcuts(this, new RoutedEventArgs()); return true;
            case "window.about": OnAbout(this, new RoutedEventArgs()); return true;
            case "window.view-used-tags": OnTagStatistics(this, new RoutedEventArgs()); return true;
            case "window.tag-explorer": OnTagExplorer(this, new RoutedEventArgs()); return true;
            case "window.udbscripts": OnUdbScriptDocker(this, new RoutedEventArgs()); return true;
            case "window.comments-panel": OnCommentsPanel(this, new RoutedEventArgs()); return true;
            case "window.view-thing-types": OnThingStatistics(this, new RoutedEventArgs()); return true;
            case "window.center-on-coordinates": OnGoToCoordinates(this, new RoutedEventArgs()); return true;
            case "window.go-to-coordinates": OnGoToCoordinates(this, new RoutedEventArgs()); return true;
            case "window.status-history": OnStatusHistory(this, new RoutedEventArgs()); return true;
            case "window.show-errors": OnErrorLog(this, new RoutedEventArgs()); return true;
            case "window.browse-wall-textures": OnBrowseWallTextures(this, new RoutedEventArgs()); return true;
            case "window.browse-flats": OnBrowseFlats(this, new RoutedEventArgs()); return true;
            case "window.browse-floor-flats": OnBrowseFloorFlats(this, new RoutedEventArgs()); return true;
            case "window.browse-ceiling-flats": OnBrowseCeilingFlats(this, new RoutedEventArgs()); return true;
            case "window.browse-things": OnBrowseThingsCatalog(this, new RoutedEventArgs()); return true;
            case "window.browse-linedef-actions": OnBrowseActionsCatalog(this, new RoutedEventArgs()); return true;
            case "window.browse-sector-effects": OnBrowseEffectsCatalog(this, new RoutedEventArgs()); return true;
            case "window.model-render-none": OnModelRenderNone(this, new RoutedEventArgs()); return true;
            case "window.model-render-selection": OnModelRenderSelection(this, new RoutedEventArgs()); return true;
            case "window.model-render-active-filter": OnModelRenderActiveFilter(this, new RoutedEventArgs()); return true;
            case "window.model-render-all": OnModelRenderAll(this, new RoutedEventArgs()); return true;
            case "window.next-model-render-mode": OnNextModelRenderMode(this, new RoutedEventArgs()); return true;
            case "window.toggle-3d-floors": OnToggle3DFloors(this, new RoutedEventArgs()); return true;
            case "window.toggle-blockmap": OnToggleBlockmap(this, new RoutedEventArgs()); return true;
            case "window.toggle-nodes": OnToggleNodes(this, new RoutedEventArgs()); return true;
            case "window.toggle-info-panel": OnToggleInfoPanel(this, new RoutedEventArgs()); return true;
            case "window.cut": OnCut(this, new RoutedEventArgs()); return true;
            case "window.copy": OnCopy(this, new RoutedEventArgs()); return true;
            case "window.paste": OnPaste(this, new RoutedEventArgs()); return true;
            case "window.paste-special": OnPasteSpecial(this, new RoutedEventArgs()); return true;
            case "window.duplicate": OnDuplicate(this, new RoutedEventArgs()); return true;
            case "window.copy-properties": OnCopyProperties(this, new RoutedEventArgs()); return true;
            case "window.paste-properties": OnPasteProperties(this, new RoutedEventArgs()); return true;
            case "window.paste-properties-options": OnPastePropertiesWithOptions(this, new RoutedEventArgs()); return true;
            case "window.delete": OnDelete(this, new RoutedEventArgs()); return true;
            case "window.select-all": OnSelectAll(this, new RoutedEventArgs()); return true;
            case "window.invert-selection": OnInvertSelection(this, new RoutedEventArgs()); return true;
            case "window.select-none": OnSelectNone(this, new RoutedEventArgs()); return true;
            case "window.properties": OnEditProperties(this, new RoutedEventArgs()); return true;
            case "window.flags": OnFlags(this, new RoutedEventArgs()); return true;
            case "window.custom-fields": OnCustomFields(this, new RoutedEventArgs()); return true;
            case "window.tags": OnTagList(this, new RoutedEventArgs()); return true;
            case "window.select-similar": OnSelectSimilar(this, new RoutedEventArgs()); return true;
            case "window.filter-selected-things": OnFilterSelectedThings(this, new RoutedEventArgs()); return true;
            case "window.change-map-element-index": OnChangeMapElementIndex(this, new RoutedEventArgs()); return true;
            case "window.stitch-geometry": OnStitch(this, new RoutedEventArgs()); return true;
            case "window.join-sectors": OnJoinSectors(this, new RoutedEventArgs()); return true;
            case "window.merge-sectors": OnMergeSectors(this, new RoutedEventArgs()); return true;
            case "window.flip-selection-horizontal": OnFlipH(this, new RoutedEventArgs()); return true;
            case "window.flip-selection-vertical": OnFlipV(this, new RoutedEventArgs()); return true;
            case "window.rotate-selection-cw": OnRotateCW(this, new RoutedEventArgs()); return true;
            case "window.rotate-selection-ccw": OnRotateCCW(this, new RoutedEventArgs()); return true;
            case "window.scale-selection-up": OnScaleUp(this, new RoutedEventArgs()); return true;
            case "window.scale-selection-down": OnScaleDown(this, new RoutedEventArgs()); return true;
            case "window.align-floor-to-front": OnAlignFloorToFront(this, new RoutedEventArgs()); return true;
            case "window.align-floor-to-back": OnAlignFloorToBack(this, new RoutedEventArgs()); return true;
            case "window.align-ceiling-to-front": OnAlignCeilingToFront(this, new RoutedEventArgs()); return true;
            case "window.align-ceiling-to-back": OnAlignCeilingToBack(this, new RoutedEventArgs()); return true;
            case "window.align-things-to-wall": OnAlignThingsToWall(this, new RoutedEventArgs()); return true;
            case "window.find-replace": OnFindReplace(this, new RoutedEventArgs()); return true;
            case "window.build-bridge": OnBuildBridge(this, new RoutedEventArgs()); return true;
            case "window.make-door": OnMakeDoor(this, new RoutedEventArgs()); return true;
            case "window.build-stairs": OnBuildStairs(this, new RoutedEventArgs()); return true;
            case "window.tag-range": OnTagRange(this, new RoutedEventArgs()); return true;
            case "window.blockmap-explorer": OnBlockmapExplorer(this, new RoutedEventArgs()); return true;
            case "window.reject-explorer": OnRejectViewer(this, new RoutedEventArgs()); return true;
            case "window.nodes-viewer": OnNodesViewer(this, new RoutedEventArgs()); return true;
            case "window.sound-propagation-mode": OnSoundPropagation(this, new RoutedEventArgs()); return true;
            case "window.sound-environment-mode": OnSoundEnvironments(this, new RoutedEventArgs()); return true;
            case "window.sound-propagation-colors": OnSoundPropagationColors(this, new RoutedEventArgs()); return true;
            case "window.apply-slope-arch": OnApplySlopeArch(this, new RoutedEventArgs()); return true;
            case "window.apply-slopes": OnApplySlopes(this, new RoutedEventArgs()); return true;
            case "window.gradient-floor-heights": OnGradientFloorHeights(this, new RoutedEventArgs()); return true;
            case "window.gradient-ceiling-heights": OnGradientCeilingHeights(this, new RoutedEventArgs()); return true;
            case "window.gradient-sector-brightness": OnGradientBrightness(this, new RoutedEventArgs()); return true;
            case "window.gradient-floor-light": OnGradientFloorLight(this, new RoutedEventArgs()); return true;
            case "window.gradient-ceiling-light": OnGradientCeilingLight(this, new RoutedEventArgs()); return true;
            case "window.gradient-light-color": OnGradientLightColor(this, new RoutedEventArgs()); return true;
            case "window.gradient-fade-color": OnGradientFadeColor(this, new RoutedEventArgs()); return true;
            case "window.gradient-light-and-fade-colors": OnGradientLightAndFadeColor(this, new RoutedEventArgs()); return true;
            case "window.gradient-linedef-brightness": OnGradientLinedefBrightness(this, new RoutedEventArgs()); return true;
            case "window.gradient-interpolation-linear": OnGradientInterpolationLinear(this, new RoutedEventArgs()); return true;
            case "window.gradient-interpolation-ease-in-out-sine": OnGradientInterpolationEaseInOutSine(this, new RoutedEventArgs()); return true;
            case "window.gradient-interpolation-ease-in-sine": OnGradientInterpolationEaseInSine(this, new RoutedEventArgs()); return true;
            case "window.gradient-interpolation-ease-out-sine": OnGradientInterpolationEaseOutSine(this, new RoutedEventArgs()); return true;
            case "window.toggle-automap-secret-line": OnToggleAutomapSecretLine(this, new RoutedEventArgs()); return true;
            case "window.toggle-automap-hidden-line": OnToggleAutomapHiddenLine(this, new RoutedEventArgs()); return true;
            case "window.toggle-automap-textured-hidden-sector": OnToggleAutomapTexturedHiddenSector(this, new RoutedEventArgs()); return true;
            case "window.sector-color": OnSectorColor(this, new RoutedEventArgs()); return true;
            case "window.dynamic-light-color": OnDynamicLightColor(this, new RoutedEventArgs()); return true;
            case "window.toggle-auto-clear-sidedef-textures": OnToggleAutoClearSidedefTextures(this, new RoutedEventArgs()); return true;
            case "window.undo-redo-panel": OnUndoRedoPanel(this, new RoutedEventArgs()); return true;
            case "window.check-map": OnCheckMap(this, new RoutedEventArgs()); return true;
            case "window.clean-up-geometry": OnCleanUpGeometry(this, new RoutedEventArgs()); return true;
            case "window.test-map": OnTestMap(this, new RoutedEventArgs()); return true;
            case "window.things-filters-setup": OnThingFilter(this, new RoutedEventArgs()); return true;
            case "window.reload-resources": OnReloadResources(this, new RoutedEventArgs()); return true;
            case "window.grid-setup": OnGridSetup(this, new RoutedEventArgs()); return true;
            case "window.usdf-conversations": OnUsdfConversations(this, new RoutedEventArgs()); return true;
            case "window.usdf-dialog-editor": OnUsdfConversations(this, new RoutedEventArgs()); return true;
            case "window.import-obj-terrain": OnImportObjTerrain(this, new RoutedEventArgs()); return true;
            case "window.export-object": OnExportObject(this, new RoutedEventArgs()); return true;
            case "window.export-image": OnExportImage(this, new RoutedEventArgs()); return true;
            case "window.export-wavefront": OnExportWavefront(this, new RoutedEventArgs()); return true;
            case "window.export-idstudio": OnExportIdStudio(this, new RoutedEventArgs()); return true;
            case "window.create-prefab": OnSavePrefab(this, new RoutedEventArgs()); return true;
            case "window.insert-prefab-file": OnInsertPrefab(this, new RoutedEventArgs()); return true;
            case "window.insert-previous-prefab": OnInsertPreviousPrefab(this, new RoutedEventArgs()); return true;
            case "window.cancel-draw":
                if (!MapView.InDrawMode) return false;
                MapView.ExitDrawModes();
                MapView.Focus();
                SetStatus("Draw mode off.");
                return true;
            default:
                return RunUdbScriptCommand(commandId) || RunSelectionGroupCommand(commandId);
        }
    }

    private bool RunUdbScriptCommand(string commandId)
    {
        if (commandId == "window.udbscriptexecute")
        {
            RunUdbScriptPlan(UdbScriptActions.ExecuteCurrentPlan(_udbScriptDocker?.CurrentSelection.CurrentScript));
            return true;
        }

        const string slotPrefix = "window.udbscriptexecuteslot";
        if (!commandId.StartsWith(slotPrefix, StringComparison.Ordinal))
            return false;

        RunUdbScriptPlan(UdbScriptActions.ExecuteSlotPlan(commandId, _udbScriptSlotAssignments));
        return true;
    }

    private void RunUdbScriptPlan(UdbScriptExecutionPlan plan)
    {
        if (!plan.ShouldRun || plan.Script is not { } script)
        {
            SetStatus(plan.Slot == 0
                ? "No UDBScript selected."
                : $"No UDBScript assigned to slot {plan.Slot}.");
            return;
        }

        UdbScriptRunnerWindow runner = OpenUdbScriptRunnerWindow();
        _pendingUdbScript = script;
        SetStatus(plan.Slot == 0
            ? $"UDBScript runner started: {script.Name}"
            : $"UDBScript slot {plan.Slot} runner started: {script.Name}");
        runner.Start();
    }

    private UdbScriptRunnerWindow OpenUdbScriptRunnerWindow()
    {
        if (_udbScriptRunner is { } existing)
        {
            existing.Activate();
            return existing;
        }

        var runner = new UdbScriptRunnerWindow();
        _udbScriptRunner = runner;
        runner.RunScriptRequested += () =>
        {
            if (_pendingUdbScript is { } script)
                RunUdbScriptInRunner(runner, script);
        };
        runner.Closed += (_, _) =>
        {
            _udbScriptRunner = null;
            _pendingUdbScript = null;
        };
        runner.CloseRequested += () =>
        {
            _udbScriptRunner = null;
            _pendingUdbScript = null;
        };
        runner.Show(this);
        return runner;
    }

    private async void RunUdbScriptInRunner(UdbScriptRunnerWindow runner, UdbScriptInfo script)
    {
        try
        {
            runner.MarkRunning();
            UdbScriptVersionGateDecision versionDecision = await runner.ConfirmFeatureVersionAsync(
                script.Version,
                ignoreVersion: false);
            if (!versionDecision.ShouldContinue)
            {
                runner.ApplyLog($"Script feature version rejected: {script.Version}");
                runner.Finish(runner.ElapsedRuntime, autoClose: false);
                SetStatus($"UDBScript feature version rejected: {script.Name}");
                return;
            }
            if (versionDecision.SetIgnoreVersion)
                runner.ApplyLog($"Script feature version accepted: {script.Version}");

            UdbScriptRunSourcePlan sourcePlan = UdbScriptRunnerModel.BuildSourcePlan(AppContext.BaseDirectory, script.ScriptFile);
            runner.ApplyStatus($"Preparing script: {script.Name}");
            runner.ApplyLog($"Script: {sourcePlan.Script.EngineSourceName}");
            runner.ApplyLog($"Libraries: {sourcePlan.Libraries.Count}");

            UdbScriptLoadedSourcePlan loadedSources = UdbScriptRunnerModel.LoadSourcePlan(
                sourcePlan,
                System.IO.File.Exists,
                System.IO.File.ReadAllText);
            if (!loadedSources.Success)
            {
                runner.ApplyLog($"Script source file not found: {loadedSources.MissingPath}");
                runner.Finish(runner.ElapsedRuntime, autoClose: false);
                SetStatus($"UDBScript source file not found: {loadedSources.MissingPath}");
                return;
            }

            runner.ApplyLog(UdbScriptRunnerModel.LoadedScriptSourceStatusText(loadedSources.Script?.Text.Length ?? 0));
            UdbScriptRunnerBindingPlan bindingPlan = UdbScriptRunnerModel.BindingPlan(script);
            runner.ApplyLog($"Script options: {bindingPlan.ScriptOptions.Count}");
            runner.ApplyLog(bindingPlan.EngineSetup.UsesLegacyGlobals
                ? "Engine binding mode: legacy globals"
                : "Engine binding mode: UDB object");
            UdbScriptRuntimeConstraintCheckResult runtimeConstraint = await runner.CheckRuntimeConstraintAsync(runner.ElapsedRuntime);
            if (runtimeConstraint.ThrowUserAbortException)
            {
                runner.ApplyLog("Script aborted by runtime constraint prompt.");
                runner.Finish(runner.ElapsedRuntime, autoClose: false);
                SetStatus($"UDBScript runtime constraint aborted: {script.Name}");
                return;
            }
            if (runtimeConstraint.RestartStopwatch)
                runner.ApplyLog("Script runtime constraint prompt continued.");

            runner.ApplyLog("UDBScript JavaScript execution is not wired yet.");
            runner.Finish(runner.ElapsedRuntime, autoClose: false);
            SetStatus($"UDBScript runner prepared: {script.Name}");
        }
        catch (Exception ex)
        {
            await HandleUdbScriptRunnerExceptionAsync(runner, script, ex);
        }
    }

    private async Task HandleUdbScriptRunnerExceptionAsync(UdbScriptRunnerWindow runner, UdbScriptInfo script, Exception exception)
    {
        UdbScriptRunnerExceptionKind kind = UdbScriptRunnerModel.ExceptionKind(exception);
        UdbScriptRunnerExceptionHandlingPlan plan = UdbScriptRunnerModel.ExceptionHandlingPlan(
            kind,
            exception.Message,
            javascriptThrowIsString: false,
            internalStackTrace: exception.ToString());
        await runner.ShowScriptErrorAsync(plan);
        runner.ApplyLog($"Script exception: {kind}");
        runner.Finish(runner.ElapsedRuntime, autoClose: false);
        if (!string.IsNullOrWhiteSpace(plan.Outcome.StatusText))
            SetStatus(plan.Outcome.StatusText);
        else
            SetStatus($"UDBScript execution failed: {script.Name}");
    }

    private bool RunSelectionGroupCommand(string commandId)
    {
        const string selectPrefix = "window.select-group-";
        const string assignPrefix = "window.assign-group-";
        const string clearPrefix = "window.clear-group-";

        if (TryReadSelectionGroup(commandId, selectPrefix, out int selectGroup))
        {
            SelectGroup(selectGroup);
            return true;
        }
        if (TryReadSelectionGroup(commandId, assignPrefix, out int assignGroup))
        {
            AddSelectionToGroup(assignGroup);
            return true;
        }
        if (TryReadSelectionGroup(commandId, clearPrefix, out int clearGroup))
        {
            ClearGroup(clearGroup);
            return true;
        }

        return false;
    }

    private static bool TryReadSelectionGroup(string commandId, string prefix, out int groupIndex)
    {
        groupIndex = -1;
        if (!commandId.StartsWith(prefix, StringComparison.Ordinal)) return false;
        string suffix = commandId[prefix.Length..];
        if (!int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out int groupNumber)) return false;
        if (groupNumber is < 1 or > MapOptions.SelectionGroupCount) return false;
        groupIndex = groupNumber - 1;
        return true;
    }

    private async void OnExit(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardDirtyMap()) return;
        _allowDirtyClose = true;
        Close();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_allowDirtyClose || !_mapDirty)
        {
            SaveWindowPlacement();
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (!await ConfirmDiscardDirtyMap()) return;
        _allowDirtyClose = true;
        Close();
    }

    // ---- Edit ----

    private void OnUndo(object? sender, RoutedEventArgs e)
    {
        if (_undo?.Undo() == true) { MarkMapDirty(); MapView.MarkGeometryDirty(); UpdateInfo(); RefreshUndoRedoPanel(); SetStatus("Undo"); }
    }

    private void OnRedo(object? sender, RoutedEventArgs e)
    {
        if (_undo?.Redo() == true) { MarkMapDirty(); MapView.MarkGeometryDirty(); UpdateInfo(); RefreshUndoRedoPanel(); SetStatus("Redo"); }
    }

    private void OnCut(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) { SetStatus("No map loaded."); return; }
        int selected = CountSelection();
        if (selected == 0) { SetStatus("Nothing selected to cut."); return; }

        MapView.CopySelection();
        CreateUndo("Cut selection");
        int removed = _map.DeleteSelection();
        MapView.MarkGeometryDirty();
        UpdateInfo();
        MapView.Focus();
        SetStatus(SelectionClipboard.CutStatusText(removed));
    }

    private void OnCopy(object? sender, RoutedEventArgs e) => RunClipboardEdit(MapView.CopySelection());

    private void OnPaste(object? sender, RoutedEventArgs e) => RunClipboardEdit(MapView.PasteClipboard());

    private async void OnPasteSpecial(object? sender, RoutedEventArgs e)
    {
        if (_map is null)
        {
            SetStatus("No map loaded.");
            MapView.Focus();
            return;
        }

        var dialog = new PasteOptionsDialog(_settings.NormalizedPasteOptions);
        if (await dialog.ShowDialog<bool>(this))
            RunClipboardEdit(MapView.PasteClipboard(dialog.PasteOptions));
        else
            MapView.Focus();
    }

    private void OnCopyProperties(object? sender, RoutedEventArgs e)
        => RunClipboardEdit(MapView.In3DMode ? MapView.CopyVisualPropertiesTarget() : MapView.CopyPropertiesSelection());

    private void OnPasteProperties(object? sender, RoutedEventArgs e)
        => RunClipboardEdit(MapView.In3DMode ? MapView.PasteVisualPropertiesTargets() : MapView.PastePropertiesSelection());

    private async void OnPastePropertiesWithOptions(object? sender, RoutedEventArgs e)
    {
        if (_map is null)
        {
            SetStatus("No map loaded.");
            MapView.Focus();
            return;
        }

        PastePropertiesOptionsResult options = MapView.In3DMode
            ? MapView.BuildVisualPastePropertiesOptions()
            : MapView.BuildPastePropertiesOptionsForCurrentMode();
        if (!options.IsAvailable)
        {
            SetStatus(options.StatusMessage ?? PastePropertiesOptionsModel.NoCopiedPropertiesMessage);
            MapView.Focus();
            return;
        }
        bool hasTarget = MapView.In3DMode ? MapView.HasVisualPropertyTarget : MapView.HasCurrentPropertyTarget;
        if (!hasTarget)
        {
            SetStatus("This action requires highlight or selection!");
            MapView.Focus();
            return;
        }

        var dialog = new PastePropertiesOptionsDialog(options);
        if (await dialog.ShowDialog<bool>(this))
        {
            ISet<string> enabledKeys = PastePropertiesApplier.EnabledKeys(options);
            RunClipboardEdit(MapView.In3DMode
                ? MapView.PasteVisualPropertiesTargets(enabledKeys)
                : MapView.PastePropertiesSelection(enabledKeys));
        }
        else
        {
            MapView.Focus();
        }
    }

    private void OnDuplicate(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        if (CountSelection() == 0) { SetStatus("Nothing selected to duplicate."); return; }

        RunClipboardEdit(MapView.DuplicateSelection());
    }

    private void RunClipboardEdit(string status)
    {
        UpdateInfo();
        MapView.Focus();
        SetStatus(status);
    }

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) return;
        int sel = _map.SelectedVerticesCount + _map.SelectedLinedefsCount + _map.SelectedSectorsCount + _map.SelectedThingsCount;
        if (sel == 0) { SetStatus("Nothing selected to delete."); return; }
        CreateUndo("Delete selection");
        int removed = _map.DeleteSelection();
        _map.BuildIndexes();
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(SelectionClipboard.DeleteStatusText(removed));
    }

    // Opens a property dialog for the single selected element (triggered by a double-click).
    private async void OnEditSelected()
    {
        if (_map is null || _undo is null) return;

        if (_map.SelectedThingsCount == 1 && _map.SelectedLinedefsCount == 0 && _map.SelectedSidedefsCount == 0 && _map.SelectedSectorsCount == 0 && _map.SelectedVerticesCount == 0)
        {
            var t = _map.GetSelectedThings()[0];
            var dlg = new ThingEditDialog(t, _config, _resources);
            if (await dlg.ShowDialog<bool>(this))
            {
                CreateUndo("Edit thing");
                t.Type = dlg.ResultType; t.Angle = dlg.ResultAngle; t.Tag = dlg.ResultTag; t.Action = dlg.ResultAction;
                t.Flags = dlg.ResultFlags;
                Array.Copy(dlg.ResultArgs, t.Args, t.Args.Length);
                t.Position = new DBuilder.Geometry.Vector2D(dlg.ResultX, dlg.ResultY); t.Height = dlg.ResultHeight;
                ApplyFields(t.Fields, dlg.ResultFields);
                MapView.InsertThingType = t.Type; // the insert tool reuses the last edited type
                AfterEdit("Thing updated");
            }
        }
        else if (_map.SelectedLinedefsCount == 1 && _map.SelectedThingsCount == 0 && _map.SelectedSidedefsCount == 0 && _map.SelectedSectorsCount == 0 && _map.SelectedVerticesCount == 0)
        {
            var l = _map.GetSelectedLinedefs()[0];
            var dlg = new LinedefEditDialog(l, _config, _resources);
            if (await dlg.ShowDialog<bool>(this))
            {
                CreateUndo("Edit linedef");
                l.Action = dlg.ResultAction; l.Tag = dlg.ResultTag; l.Flags = dlg.ResultFlags;
                if (l.Front is { } front)
                {
                    front.HighTexture = dlg.ResultFrontHighTex ?? front.HighTexture;
                    front.MidTexture = dlg.ResultFrontMidTex ?? front.MidTexture;
                    front.LowTexture = dlg.ResultFrontLowTex ?? front.LowTexture;
                    if (dlg.ResultFrontSidedefFlags != null)
                        UdmfFlagChoices.ApplyFlags(front.UdmfFlags, dlg.ResultFrontSidedefFlags);
                }
                if (l.Back is { } back)
                {
                    back.HighTexture = dlg.ResultBackHighTex ?? back.HighTexture;
                    back.MidTexture = dlg.ResultBackMidTex ?? back.MidTexture;
                    back.LowTexture = dlg.ResultBackLowTex ?? back.LowTexture;
                    if (dlg.ResultBackSidedefFlags != null)
                        UdmfFlagChoices.ApplyFlags(back.UdmfFlags, dlg.ResultBackSidedefFlags);
                }
                Array.Copy(dlg.ResultArgs, l.Args, l.Args.Length);
                ApplyFields(l.Fields, dlg.ResultFields);
                AfterEdit("Linedef updated");
            }
        }
        else if (_map.SelectedSidedefsCount == 1 && _map.SelectedThingsCount == 0 && _map.SelectedLinedefsCount == 0 && _map.SelectedSectorsCount == 0 && _map.SelectedVerticesCount == 0)
        {
            var side = _map.GetSelectedSidedefs()[0];
            var dlg = new SidedefEditDialog(side, _config, _resources);
            if (await dlg.ShowDialog<bool>(this))
            {
                CreateUndo("Edit sidedef");
                side.HighTexture = dlg.ResultHighTexture;
                side.MidTexture = dlg.ResultMidTexture;
                side.LowTexture = dlg.ResultLowTexture;
                side.OffsetX = dlg.ResultOffsetX;
                side.OffsetY = dlg.ResultOffsetY;
                if (dlg.ResultSidedefFlags != null)
                    UdmfFlagChoices.ApplyFlags(side.UdmfFlags, dlg.ResultSidedefFlags);
                ApplyFields(side.Fields, dlg.ResultFields);
                AfterEdit("Sidedef updated");
            }
        }
        else if (_map.SelectedSectorsCount == 1 && _map.SelectedThingsCount == 0 && _map.SelectedLinedefsCount == 0 && _map.SelectedSidedefsCount == 0 && _map.SelectedVerticesCount == 0)
        {
            var s = _map.GetSelectedSectors()[0];
            var dlg = new SectorEditDialog(s, _config, _resources);
            if (await dlg.ShowDialog<bool>(this))
            {
                CreateUndo("Edit sector");
                s.FloorHeight = dlg.ResultFloor; s.CeilHeight = dlg.ResultCeil;
                s.FloorTexture = dlg.ResultFloorTex; s.CeilTexture = dlg.ResultCeilTex;
                s.Brightness = dlg.ResultBright; s.Special = dlg.ResultSpecial; s.Tag = dlg.ResultTag;
                ApplyFields(s.Fields, dlg.ResultFields);
                AfterEdit("Sector updated");
            }
        }
        else if (_map.SelectedVerticesCount == 1 && _map.SelectedThingsCount == 0 && _map.SelectedLinedefsCount == 0 && _map.SelectedSidedefsCount == 0 && _map.SelectedSectorsCount == 0)
        {
            var vertex = _map.GetSelectedVertices()[0];
            var dlg = new VertexEditDialog(vertex, _config);
            if (await dlg.ShowDialog<bool>(this))
            {
                CreateUndo("Edit vertex");
                vertex.Position = new DBuilder.Geometry.Vector2D(dlg.ResultX, dlg.ResultY);
                ApplyFields(vertex.Fields, dlg.ResultFields);
                AfterEdit("Vertex updated");
            }
        }
        else SetStatus("Select exactly one vertex, linedef, sidedef, sector or thing to edit properties.");
    }

    private void OnEditProperties(object? sender, RoutedEventArgs e) => OnEditSelected();

    // Opens the named UDMF flags dialog for one selected thing, linedef or sector.
    private async void OnFlags(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) return;

        if (_map.SelectedLinedefsCount == 1 && _map.SelectedThingsCount == 0 && _map.SelectedSectorsCount == 0 && _map.SelectedVerticesCount == 0)
        {
            var line = _map.GetSelectedLinedefs()[0];
            var current = new HashSet<string>(line.UdmfFlags, StringComparer.OrdinalIgnoreCase);
            if (_config != null) current.UnionWith(_config.LinedefFlagsToUdmf(line.Flags));

            var name = $"Linedef {_map.Linedefs.IndexOf(line)}";
            var dlg = new UdmfFlagsDialog(name, UdmfFlagChoices.KnownLinedefFlags(_config, line), current);
            if (!await dlg.ShowDialog<bool>(this)) return;

            CreateUndo("Edit linedef flags");
            UdmfFlagChoices.ApplyFlags(line.UdmfFlags, dlg.ResultFlags);
            if (_config != null) line.Flags = _config.LinedefFlagsFromUdmf(line.UdmfFlags);
            AfterEdit($"{name} flags updated");
            return;
        }

        if (_map.SelectedThingsCount == 1 && _map.SelectedLinedefsCount == 0 && _map.SelectedSectorsCount == 0 && _map.SelectedVerticesCount == 0)
        {
            var thing = _map.GetSelectedThings()[0];
            var current = new HashSet<string>(thing.UdmfFlags, StringComparer.OrdinalIgnoreCase);
            if (_config != null) current.UnionWith(_config.ThingFlagsToUdmf(thing.Flags));

            var name = $"Thing {_map.Things.IndexOf(thing)}";
            var dlg = new UdmfFlagsDialog(name, UdmfFlagChoices.KnownThingFlags(_config, thing), current);
            if (!await dlg.ShowDialog<bool>(this)) return;

            CreateUndo("Edit thing flags");
            UdmfFlagChoices.ApplyFlags(thing.UdmfFlags, dlg.ResultFlags);
            if (_config != null) thing.Flags = _config.ThingFlagsFromUdmf(thing.UdmfFlags);
            AfterEdit($"{name} flags updated");
            return;
        }

        if (_map.SelectedSectorsCount == 1 && _map.SelectedLinedefsCount == 0 && _map.SelectedThingsCount == 0 && _map.SelectedVerticesCount == 0)
        {
            var sector = _map.GetSelectedSectors()[0];
            var name = $"Sector {_map.Sectors.IndexOf(sector)}";
            var dlg = new UdmfFlagsDialog(name, UdmfFlagChoices.KnownSectorFlags(_config, sector), sector.UdmfFlags);
            if (!await dlg.ShowDialog<bool>(this)) return;

            CreateUndo("Edit sector flags");
            UdmfFlagChoices.ApplyFlags(sector.UdmfFlags, dlg.ResultFlags);
            AfterEdit($"{name} flags updated");
            return;
        }

        SetStatus("Select exactly one linedef, sector or thing to edit flags.");
    }

    // Opens the generic UDMF custom-fields dialog for one selected map element, including vertices.
    private async void OnCustomFields(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) return;
        if (!SupportsCustomFields() || !TryGetSingleFieldedSelection(
                out var element,
                out string name,
                out string elementType,
                out IReadOnlyList<string>? additionalFieldNames))
        {
            SetStatus("Select exactly one vertex, linedef, sidedef, sector or thing to edit custom fields.");
            return;
        }

        var dlg = new CustomFieldsDialog(name, element.Fields, _config, elementType, additionalFieldNames, _resources);
        if (!await dlg.ShowDialog<bool>(this)) return;

        CreateUndo("Edit custom fields");
        ApplyFields(element.Fields, dlg.ResultFields);
        if (_mapOptions != null)
            CustomFieldsEditorModelBuilder.StoreRawFieldTypes(_mapOptions, _config, elementType, dlg.ResultRawFields);
        AfterEdit($"{name} custom fields updated");
    }

    // Replaces an element's custom UDMF fields with the dialog's parsed result.
    private static void ApplyFields(Dictionary<string, object> target, Dictionary<string, object> result)
    {
        target.Clear();
        foreach (var kv in result) target[kv.Key] = kv.Value;
    }

    private bool TryGetSingleFieldedSelection(
        out IFielded element,
        out string name,
        out string elementType,
        out IReadOnlyList<string>? additionalFieldNames)
    {
        element = null!;
        name = "";
        elementType = "";
        additionalFieldNames = null;
        if (_map is null) return false;

        int total = _map.SelectedVerticesCount + _map.SelectedLinedefsCount + _map.SelectedSidedefsCount + _map.SelectedSectorsCount + _map.SelectedThingsCount;
        if (total != 1) return false;

        if (_map.SelectedVerticesCount == 1)
        {
            var vertex = _map.GetSelectedVertices()[0];
            element = vertex;
            name = $"Vertex {_map.Vertices.IndexOf(vertex)}";
            elementType = "vertex";
            return true;
        }
        if (_map.SelectedLinedefsCount == 1)
        {
            var line = _map.GetSelectedLinedefs()[0];
            element = line;
            name = $"Linedef {_map.Linedefs.IndexOf(line)}";
            elementType = "linedef";
            return true;
        }
        if (_map.SelectedSidedefsCount == 1)
        {
            var side = _map.GetSelectedSidedefs()[0];
            element = side;
            name = $"Sidedef {_map.Sidedefs.IndexOf(side)}";
            elementType = "sidedef";
            return true;
        }
        if (_map.SelectedSectorsCount == 1)
        {
            var sector = _map.GetSelectedSectors()[0];
            element = sector;
            name = $"Sector {sector.Index}";
            elementType = "sector";
            return true;
        }
        if (_map.SelectedThingsCount == 1)
        {
            var thing = _map.GetSelectedThings()[0];
            element = thing;
            name = $"Thing {_map.Things.IndexOf(thing)}";
            elementType = "thing";
            additionalFieldNames = _config?.GetThing(thing.Type)?.AddUniversalFields;
            return true;
        }

        return false;
    }

    private void AfterEdit(string status)
    {
        _map?.BuildIndexes();
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(status);
    }

    private void OnSelectNone(object? sender, RoutedEventArgs e)
    {
        _map?.ClearAllSelected();
        MapView.MarkGeometryDirty();
        UpdateInfo();
    }

    private void OnSelectAll(object? sender, RoutedEventArgs e)
    {
        int count = MapView.SelectAllInCurrentMode();
        UpdateInfo();
        SetStatus($"Selected {count} {MapView.CurrentEditMode.ToString().ToLowerInvariant()}.");
    }

    private void OnInvertSelection(object? sender, RoutedEventArgs e)
    {
        int count = MapView.InvertSelectionInCurrentMode();
        UpdateInfo();
        SetStatus($"Inverted {MapView.CurrentEditMode.ToString().ToLowerInvariant()} selection: {count} selected.");
    }

    private async void OnSelectSimilar(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        if (CountSelectionInCurrentMode() == 0)
        {
            SetStatus($"Select one or more {MapView.CurrentEditMode.ToString().ToLowerInvariant()} first.");
            return;
        }

        var dlg = new SelectSimilarDialog(MapView.CurrentEditMode);
        if (!await dlg.ShowDialog<bool>(this)) return;

        int changed = MapView.CurrentEditMode switch
        {
            MapControl.EditMode.Vertices => SelectSimilar.SelectVertices(_map, dlg.VertexOptions),
            MapControl.EditMode.Linedefs => SelectSimilar.SelectLinedefs(_map, dlg.LinedefOptions, dlg.SidedefOptions),
            MapControl.EditMode.Sectors => SelectSimilar.SelectSectors(_map, dlg.SectorOptions),
            MapControl.EditMode.Things => SelectSimilar.SelectThings(_map, dlg.ThingOptions),
            _ => 0,
        };

        if (changed == 0)
        {
            SetStatus($"No similar {MapView.CurrentEditMode.ToString().ToLowerInvariant()} found.");
            return;
        }

        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus($"Selected {changed} similar {MapView.CurrentEditMode.ToString().ToLowerInvariant()}.");
        MapView.Focus();
    }

    private async void OnFilterSelectedThings(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        if (_map.SelectedThingsCount == 0) { SetStatus("This action requires a selection!"); return; }

        IReadOnlyList<int> selectedTypes = ThingSelectionFilter.SelectedTypes(_map);
        var dlg = new FilterSelectedThingsDialog(selectedTypes, _config);
        if (await dlg.ShowDialog<bool>(this))
        {
            CreateUndo("Filter selected things");
            int kept = ThingSelectionFilter.KeepSelectedTypes(_map, dlg.SelectedTypes);
            MapView.MarkGeometryDirty();
            MarkMapDirty();
            UpdateInfo();
            SetStatus(ThingSelectionFilter.FilterStatusText(kept));
        }

        MapView.Focus();
    }

    private async void OnChangeMapElementIndex(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) { SetStatus("No map loaded."); return; }
        if (!TryGetMapElementIndexTarget(out MapElementIndexTarget target, out string error))
        {
            SetStatus(error);
            return;
        }

        var dlg = new ChangeMapElementIndexDialog(target.ElementName, target.OldIndex, target.MaxIndex);
        if (await dlg.ShowDialog<bool>(this))
        {
            if (dlg.NewIndex < 0 || dlg.NewIndex > target.MaxIndex)
            {
                SetStatus("Index must be between 0 and " + target.MaxIndex + ".");
                return;
            }

            CreateUndo("Change " + target.ElementName + " index");
            target.Change(dlg.NewIndex);
            _map.BuildIndexes();
            MapView.MarkGeometryDirty();
            UpdateInfo();
            SetStatus("Changed index of " + target.ElementName + " " + target.OldIndex + " to " + dlg.NewIndex + ".");
        }

        MapView.Focus();
    }

    private bool TryGetMapElementIndexTarget(out MapElementIndexTarget target, out string error)
    {
        target = default;
        error = "";
        if (_map is null)
        {
            error = "No map loaded.";
            return false;
        }

        switch (MapView.CurrentEditMode)
        {
            case MapControl.EditMode.Vertices:
            {
                List<Vertex> selected = _map.GetSelectedVertices();
                if (selected.Count != 1)
                {
                    error = "Changing vertex index failed: select exactly 1 vertex.";
                    return false;
                }

                Vertex vertex = selected[0];
                target = new MapElementIndexTarget("vertex", _map.IndexOfVertex(vertex), _map.Vertices.Count - 1, newIndex => _map.ChangeVertexIndex(vertex, newIndex));
                return true;
            }
            case MapControl.EditMode.Linedefs:
            {
                List<Linedef> selected = _map.GetSelectedLinedefs();
                if (selected.Count != 1)
                {
                    error = "Changing linedef index failed: select exactly 1 linedef.";
                    return false;
                }

                Linedef linedef = selected[0];
                target = new MapElementIndexTarget("linedef", _map.IndexOfLinedef(linedef), _map.Linedefs.Count - 1, newIndex => _map.ChangeLinedefIndex(linedef, newIndex));
                return true;
            }
            case MapControl.EditMode.Sectors:
            {
                List<Sector> selected = _map.GetSelectedSectors();
                if (selected.Count != 1)
                {
                    error = "Changing sector index failed: select exactly 1 sector.";
                    return false;
                }

                Sector sector = selected[0];
                target = new MapElementIndexTarget("sector", _map.IndexOfSector(sector), _map.Sectors.Count - 1, newIndex => _map.ChangeSectorIndex(sector, newIndex));
                return true;
            }
            case MapControl.EditMode.Things:
            {
                List<Thing> selected = _map.GetSelectedThings();
                if (selected.Count != 1)
                {
                    error = "Changing thing index failed: select exactly 1 thing.";
                    return false;
                }

                Thing thing = selected[0];
                target = new MapElementIndexTarget("thing", _map.IndexOfThing(thing), _map.Things.Count - 1, newIndex => _map.ChangeThingIndex(thing, newIndex));
                return true;
            }
            default:
                error = "Change Map Element Index is not available in this mode.";
                return false;
        }
    }

    private readonly record struct MapElementIndexTarget(string ElementName, int OldIndex, int MaxIndex, Func<int, bool> Change);

    private int CountSelectionInCurrentMode()
        => _map is null ? 0 : MapView.CurrentEditMode switch
        {
            MapControl.EditMode.Vertices => _map.SelectedVerticesCount,
            MapControl.EditMode.Linedefs => _map.SelectedLinedefsCount,
            MapControl.EditMode.Sectors => _map.SelectedSectorsCount,
            MapControl.EditMode.Things => _map.SelectedThingsCount,
            _ => 0,
        };

    private void OnModeVertices(object? sender, RoutedEventArgs e) => SetEditMode(MapControl.EditMode.Vertices);
    private void OnModeLinedefs(object? sender, RoutedEventArgs e) => SetEditMode(MapControl.EditMode.Linedefs);
    private void OnModeSectors(object? sender, RoutedEventArgs e) => SetEditMode(MapControl.EditMode.Sectors);
    private void OnModeThings(object? sender, RoutedEventArgs e) => SetEditMode(MapControl.EditMode.Things);

    private void SetEditMode(MapControl.EditMode mode)
    {
        MapView.SetCurrentEditMode(mode);
        SetStatus(ModeStatus(mode));
        UpdateStatusDetails();
        MapView.Focus();
    }

    private string ModeStatus(MapControl.EditMode mode)
        => mode == MapControl.EditMode.Things
            ? $"Mode: Things. Click empty space or {CommandHint("map2d.insert")} to place thing type {MapView.InsertThingType}; View > Browsers > Things changes the type."
            : $"Mode: {mode}";

    private void OnAutomapMode(object? sender, RoutedEventArgs e)
    {
        bool enabled = MapView.ToggleAutomapMode();
        MapView.Focus();
        UpdateAutomapOptionControls();
        SetStatus(enabled
            ? "Mode: Automap. Valid automap lines use the Doom automap palette; View > Automap Mode exits."
            : $"Mode: {MapView.CurrentEditMode}");
    }

    private void OnWadAuthorMode(object? sender, RoutedEventArgs e)
    {
        bool enabled = MapView.ToggleWadAuthorMode();
        MapView.Focus();
        SetStatus(enabled
            ? "Mode: WadAuthor. Hover highlights vertices, things, linedefs, and sectors using WadAuthor priority."
            : $"Mode: {MapView.CurrentEditMode}");
        UpdateStatusDetails();
        UpdateCommandAvailability();
    }

    private void OnAutomapOptionChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncingAutomapControls) return;

        _settings.AutomapSettings = new AutomapModeSettings(
            AutomapShowHiddenLinesButton.IsChecked == true,
            AutomapShowSecretSectorsButton.IsChecked == true,
            AutomapShowLocksButton.IsChecked == true,
            AutomapShowTexturesButton.IsChecked == true,
            _settings.NormalizedAutomapSettings.ColorPreset).Normalized();
        MapView.AutomapSettings = _settings.NormalizedAutomapSettings;
        SaveSettings();
        UpdateAutomapOptionControls();
        SetStatus("Automap options updated.");
    }

    private void OnAutomapColorPresetChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingAutomapControls || AutomapColorPresetCombo.SelectedIndex < 0) return;

        _settings.AutomapSettings = _settings.NormalizedAutomapSettings with
        {
            ColorPreset = (AutomapColorPreset)AutomapColorPresetCombo.SelectedIndex,
        };
        MapView.AutomapSettings = _settings.NormalizedAutomapSettings;
        SaveSettings();
        UpdateAutomapOptionControls();
        SetStatus($"Automap color preset: {_settings.NormalizedAutomapSettings.ColorPreset}.");
    }

    private void UpdateAutomapOptionControls()
    {
        _syncingAutomapControls = true;
        try
        {
            AutomapModeSettings settings = _settings.NormalizedAutomapSettings;
            AutomapOptionsPanel.IsVisible = MapView.AutomapMode;
            AutomapShowHiddenLinesButton.IsChecked = settings.ShowHiddenLines;
            AutomapShowSecretSectorsButton.IsChecked = settings.ShowSecretSectors;
            AutomapShowLocksButton.IsChecked = settings.ShowLocks;
            AutomapShowLocksButton.IsVisible = _mapFormat != MapFormat.Doom;
            AutomapShowTexturesButton.IsChecked = settings.ShowTextures;
            AutomapColorPresetCombo.SelectedIndex = (int)settings.ColorPreset;
        }
        finally
        {
            _syncingAutomapControls = false;
        }
    }

    private void RebuildSelectionGroupsMenu()
    {
        var groups = new List<MenuItem>();
        for (int i = 0; i < MapOptions.SelectionGroupCount; i++)
        {
            int groupIndex = i;
            int display = i + 1;
            var group = new MenuItem { Header = $"Group {display}" };
            group.ItemsSource = new[]
            {
                MenuCommand("_Add selection", () => AddSelectionToGroup(groupIndex)),
                MenuCommand("_Select group", () => SelectGroup(groupIndex)),
                MenuCommand("_Clear group", () => ClearGroup(groupIndex)),
            };
            groups.Add(group);
        }

        SelectionGroupsMenu.ItemsSource = groups;
    }

    private static MenuItem MenuCommand(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private void AddSelectionToGroup(int groupIndex)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        int selected = CountSelection();
        if (selected == 0) { SetStatus("Select elements before adding them to a group."); return; }

        CreateUndo($"Add selection to group {groupIndex + 1}");
        _map.AddSelectionToGroup(groupIndex);
        _mapOptions?.WriteSelectionGroups(_map);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(_map.GetGroupInfo(groupIndex).AddedStatusText(selected));
    }

    private void SelectGroup(int groupIndex)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        int mask = MapSet.GroupMask(groupIndex);
        _map.SelectVerticesByGroup(mask);
        _map.SelectLinedefsByGroup(mask);
        _map.SelectSectorsByGroup(mask);
        _map.SelectThingsByGroup(mask);

        MapView.MarkGeometryDirty();
        UpdateInfo();
        MapView.Focus();
        SetStatus(_map.GetGroupInfo(groupIndex).SelectedStatusText());
    }

    private void ClearGroup(int groupIndex)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        int mask = MapSet.GroupMask(groupIndex);
        int grouped = CountGroup(mask);
        if (grouped == 0) { SetStatus($"Group {groupIndex + 1} is empty."); return; }

        CreateUndo($"Clear group {groupIndex + 1}");
        _map.ClearGroup(mask);
        _mapOptions?.WriteSelectionGroups(_map);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(SelectionGroupInfo.ClearedStatusText(groupIndex, grouped));
    }

    private int CountSelection()
        => _map is null ? 0 : _map.SelectedVerticesCount + _map.SelectedLinedefsCount + _map.SelectedSectorsCount + _map.SelectedThingsCount;

    private int CountGroup(int mask)
        => _map is null ? 0 :
            CountGroup(_map.Vertices, mask) +
            CountGroup(_map.Linedefs, mask) +
            CountGroup(_map.Sectors, mask) +
            CountGroup(_map.Things, mask);

    private static int CountGroup<T>(IEnumerable<T> items, int mask) where T : IGroupable
    {
        int count = 0;
        foreach (var item in items)
            if ((item.Groups & mask) != 0) count++;
        return count;
    }

    // Stitches selected geometry against the rest of the map, or welds the whole map when nothing is selected.
    private void OnStitch(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) return;
        CreateUndo("Stitch geometry");
        var selectedVertices = _map.SelectedGeometryVertices();
        if (selectedVertices.Count > 0)
        {
            GeometryStitchResult result = _map.StitchSelectedGeometry(_settings.NormalizedMergeGeometryMode, 0.5);
            _map.BuildIndexes();
            MapView.MarkGeometryDirty();
            UpdateInfo();
            SetStatus($"Stitched selection: {result.JoinedVertices} vertices joined, {result.VertexLineSplits + result.LineLineSplits} lines split.");
            return;
        }

        int merged = _map.MergeOverlappingVertices(0.5);
        int split = _map.SplitLinedefsAtVertices(0.5);
        _map.BuildIndexes();
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus($"Stitched: {merged} vertices merged, {split} lines split.");
    }

    private void OnJoinSectors(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.JoinOrMergeSelectedSectors(merge: false));
    private void OnMergeSectors(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.JoinOrMergeSelectedSectors(merge: true));
    private void OnLowerFloor8(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.AdjustSectorHeights(SectorHeightPart.Floor, -8));
    private void OnRaiseFloor8(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.AdjustSectorHeights(SectorHeightPart.Floor, 8));
    private void OnLowerCeiling8(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.AdjustSectorHeights(SectorHeightPart.Ceiling, -8));
    private void OnRaiseCeiling8(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.AdjustSectorHeights(SectorHeightPart.Ceiling, 8));
    private void OnRaiseBrightness8(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.AdjustSectorBrightness(raise: true));
    private void OnLowerBrightness8(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.AdjustSectorBrightness(raise: false));
    private void OnGradientFloorHeights(object? sender, RoutedEventArgs e) => ApplySectorGradient(SectorGradientTarget.FloorHeight);
    private void OnGradientCeilingHeights(object? sender, RoutedEventArgs e) => ApplySectorGradient(SectorGradientTarget.CeilingHeight);
    private void OnGradientBrightness(object? sender, RoutedEventArgs e) => ApplySectorGradient(SectorGradientTarget.Brightness);
    private void OnGradientFloorLight(object? sender, RoutedEventArgs e) => ApplySectorGradient(SectorGradientTarget.FloorLight);
    private void OnGradientCeilingLight(object? sender, RoutedEventArgs e) => ApplySectorGradient(SectorGradientTarget.CeilingLight);
    private void OnGradientLightColor(object? sender, RoutedEventArgs e) => ApplySectorGradient(SectorGradientTarget.LightColor);
    private void OnGradientFadeColor(object? sender, RoutedEventArgs e) => ApplySectorGradient(SectorGradientTarget.FadeColor);
    private void OnGradientLightAndFadeColor(object? sender, RoutedEventArgs e) => ApplySectorGradient(SectorGradientTarget.LightAndFadeColor);
    private void OnGradientLinedefBrightness(object? sender, RoutedEventArgs e) => ApplyLinedefBrightnessGradient();
    private void OnGradientInterpolationLinear(object? sender, RoutedEventArgs e) => SetGradientInterpolation(InterpolationTools.Mode.LINEAR);
    private void OnGradientInterpolationEaseInOutSine(object? sender, RoutedEventArgs e) => SetGradientInterpolation(InterpolationTools.Mode.EASE_IN_OUT_SINE);
    private void OnGradientInterpolationEaseInSine(object? sender, RoutedEventArgs e) => SetGradientInterpolation(InterpolationTools.Mode.EASE_IN_SINE);
    private void OnGradientInterpolationEaseOutSine(object? sender, RoutedEventArgs e) => SetGradientInterpolation(InterpolationTools.Mode.EASE_OUT_SINE);

    private void OnFlipH(object? sender, RoutedEventArgs e) => Transform(SelectionTransform.Op.FlipHorizontal, "Flip horizontal");
    private void OnFlipV(object? sender, RoutedEventArgs e) => Transform(SelectionTransform.Op.FlipVertical, "Flip vertical");
    private void OnRotateCW(object? sender, RoutedEventArgs e) => Transform(SelectionTransform.Op.RotateCW, "Rotate 90 CW");
    private void OnRotateCCW(object? sender, RoutedEventArgs e) => Transform(SelectionTransform.Op.RotateCCW, "Rotate 90 CCW");
    private void OnScaleUp(object? sender, RoutedEventArgs e) => ScaleSelection(2.0, "Scale up");
    private void OnScaleDown(object? sender, RoutedEventArgs e) => ScaleSelection(0.5, "Scale down");
    private void OnSelectSingleSidedLinedefs(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.KeepSelectedLinedefsBySidedness(doubleSided: false));
    private void OnSelectDoubleSidedLinedefs(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.KeepSelectedLinedefsBySidedness(doubleSided: true));
    private void OnFlipLinedefs(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.FlipLinedefs());
    private void OnFlipSidedefs(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.FlipSidedefs());
    private void OnAlignLinedefs(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.AlignLinedefs());
    private void OnSplitLinedefs(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.SplitLinedefs());
    private void OnAlignTexturesX(object? sender, RoutedEventArgs e) => AlignTextures(vertical: false);
    private void OnAlignTexturesY(object? sender, RoutedEventArgs e) => AlignTextures(vertical: true);
    private void OnFitSelectedTextures(object? sender, RoutedEventArgs e) => FitSelectedTextures();
    private void OnAlignThingsToWall(object? sender, RoutedEventArgs e) => AlignThingsToWall();
    private void OnApplyLightFogFlag(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.ApplyLightFogFlag());
    private void OnAlignFloorToFront(object? sender, RoutedEventArgs e) => AlignFlatToLine(floors: true, frontSide: true);
    private void OnAlignFloorToBack(object? sender, RoutedEventArgs e) => AlignFlatToLine(floors: true, frontSide: false);
    private void OnAlignCeilingToFront(object? sender, RoutedEventArgs e) => AlignFlatToLine(floors: false, frontSide: true);
    private void OnAlignCeilingToBack(object? sender, RoutedEventArgs e) => AlignFlatToLine(floors: false, frontSide: false);

    private void AlignTextures(bool vertical)
    {
        string status = MapView.AutoAlignSelectedTextures(vertical);
        UpdateInfo();
        MapView.Focus();
        SetStatus(status);
    }

    private void AlignThingsToWall()
    {
        string status = MapView.AlignSelectedThingsToWall();
        UpdateInfo();
        MapView.Focus();
        SetStatus(status);
    }

    private void FitSelectedTextures()
    {
        string status = MapView.FitSelectedTextures();
        UpdateInfo();
        MapView.Focus();
        SetStatus(status);
    }

    private void AlignFlatToLine(bool floors, bool frontSide)
    {
        if (_mapFormat != MapFormat.Udmf)
        {
            SetStatus("Flat alignment to linedefs is only available for UDMF maps.");
            return;
        }

        string status = MapView.AlignSelectedFlatsToLinedefs(floors, frontSide);
        UpdateInfo();
        MapView.Focus();
        SetStatus(status);
    }

    // Applies a flip/rotate to the current selection about its center, undoable.
    private void Transform(SelectionTransform.Op op, string desc)
    {
        if (_map is null || _undo is null) return;
        if (_map.SelectedGeometryVertices().Count == 0 && _map.SelectedThingsCount == 0)
        {
            SetStatus("Select elements to transform first.");
            return;
        }
        CreateUndo(desc);
        SelectionTransform.Apply(_map, op);
        _map.BuildIndexes();
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus($"{desc} applied.");
    }

    // Scales the current selection about its center, undoable.
    private void ScaleSelection(double factor, string desc)
    {
        if (_map is null || _undo is null) return;
        if (_map.SelectedGeometryVertices().Count == 0 && _map.SelectedThingsCount == 0)
        {
            SetStatus("Select elements to transform first.");
            return;
        }
        CreateUndo(desc);
        SelectionTransform.Scale(_map, factor);
        _map.BuildIndexes();
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus($"{desc} applied.");
    }

    private void ApplySectorGradient(SectorGradientTarget target)
    {
        if (_map is null || _undo is null) return;
        var selected = _map.GetSelectedSectors();
        if (selected.Count < SectorGradient.MinimumSectorCount)
        {
            SetStatus("Select at least 3 sectors first!");
            return;
        }

        string undoName = target switch
        {
            SectorGradientTarget.FloorHeight => "Gradient floor heights",
            SectorGradientTarget.CeilingHeight => "Gradient ceiling heights",
            SectorGradientTarget.Brightness => "Gradient brightness",
            SectorGradientTarget.FloorLight => "Gradient floor brightness",
            SectorGradientTarget.CeilingLight => "Gradient ceiling brightness",
            SectorGradientTarget.LightColor => "Gradient light color",
            SectorGradientTarget.FadeColor => "Gradient fade color",
            SectorGradientTarget.LightAndFadeColor => "Gradient light and fade colors",
            _ => "Gradient sectors",
        };
        CreateUndo(undoName);

        SectorGradientResult result = SectorGradient.Apply(selected, target, _gradientInterpolationMode);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        MapView.Focus();
        SetStatus(result.Message);
    }

    private void ApplyLinedefBrightnessGradient()
    {
        if (_map is null || _undo is null) return;
        if (_mapFormat != MapFormat.Udmf)
        {
            SetStatus("Linedef brightness gradients are only available for UDMF maps.");
            return;
        }

        var selected = _map.GetSelectedLinedefs();
        if (selected.Count < LinedefGradient.MinimumLinedefCount)
        {
            SetStatus("Select at least 3 linedefs first!");
            return;
        }

        CreateUndo("Linedefs gradient brightness");
        LinedefGradientResult result = LinedefGradient.ApplyBrightness(selected, _gradientInterpolationMode);
        if (result.Applied)
        {
            foreach (var line in selected)
            {
                if (line.Front is not null) SidedefFogTools.UpdateLightFogFlag(line.Front, mapInfo: null, _config);
                if (line.Back is not null) SidedefFogTools.UpdateLightFogFlag(line.Back, mapInfo: null, _config);
            }
        }

        MapView.MarkGeometryDirty();
        UpdateInfo();
        MapView.Focus();
        SetStatus(result.Message);
    }

    private void SetGradientInterpolation(InterpolationTools.Mode mode)
    {
        if (!Enum.IsDefined(mode)) return;
        _gradientInterpolationMode = mode;
        UpdateCommandAvailability();
        SetStatus($"Gradient interpolation: {GradientInterpolationLabel(mode)}.");
    }

    private static string GradientInterpolationLabel(InterpolationTools.Mode mode) => mode switch
    {
        InterpolationTools.Mode.LINEAR => "Linear",
        InterpolationTools.Mode.EASE_IN_OUT_SINE => "Ease In/Out Sine",
        InterpolationTools.Mode.EASE_IN_SINE => "Ease In Sine",
        InterpolationTools.Mode.EASE_OUT_SINE => "Ease Out Sine",
        _ => mode.ToString(),
    };

    // Saves the current selection to a .prefab file (the clipboard byte format).
    private async void OnSavePrefab(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        var data = MapView.GetSelectionPrefab();
        if (data is null) { SetStatus("Select something to save as a prefab."); return; }
        var top = GetTopLevel(this);
        if (top is null) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Prefab",
            SuggestedFileName = "prefab.dbprefab",
            DefaultExtension = "dbprefab",
            FileTypeChoices = new[] { new FilePickerFileType("DBuilder prefab") { Patterns = new[] { "*.dbprefab" } } },
        });
        if (file?.TryGetLocalPath() is not { } path) return;
        try { System.IO.File.WriteAllBytes(path, data); SetStatus($"Saved prefab to {System.IO.Path.GetFileName(path)}."); }
        catch (Exception ex) { LogAndSetStatus(ex, "Save prefab failed"); }
    }

    // Inserts a .prefab file at the cursor (undoable via the EditBegun hook).
    private async void OnInsertPrefab(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("Open a map first."); return; }
        var top = GetTopLevel(this);
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Insert Prefab",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("DBuilder prefab") { Patterns = new[] { "*.dbprefab" } } },
        });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path) return;
        try
        {
            InsertPrefabFile(path);
            _lastPrefabPath = path;
        }
        catch (Exception ex) { LogAndSetStatus(ex, "Insert prefab failed"); }
    }

    private void OnInsertPreviousPrefab(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("Open a map first."); return; }
        if (string.IsNullOrWhiteSpace(_lastPrefabPath) || !System.IO.File.Exists(_lastPrefabPath))
        {
            SetStatus("No previous prefab file available.");
            return;
        }

        try { InsertPrefabFile(_lastPrefabPath); }
        catch (Exception ex) { LogAndSetStatus(ex, "Insert previous prefab failed"); }
    }

    private void InsertPrefabFile(string path)
    {
        MapView.InsertPrefab(System.IO.File.ReadAllBytes(path));
        UpdateInfo();
        MapView.Focus();
    }

    private void OnDrawSector(object? sender, RoutedEventArgs e) => ToggleDrawMode(linesOnly: false, "sector");
    private void OnDrawLines(object? sender, RoutedEventArgs e) => ToggleDrawMode(linesOnly: true, "lines-only");
    private void OnDrawCurve(object? sender, RoutedEventArgs e) => ToggleDrawMode(linesOnly: true, "curve", curve: true);
    private void OnMakeSectorAtCursor(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.MakeSectorAtCursor());
    private void OnInsertAtCursor(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.InsertAtCursor());
    private void OnPlaceThings(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.PlaceThingsFromSelection());
    private void OnDrawRectangle(object? sender, RoutedEventArgs e) => ToggleShape(MapControl.ShapeKind.Rectangle, "rectangle");
    private void OnDrawEllipse(object? sender, RoutedEventArgs e) => ToggleShape(MapControl.ShapeKind.Ellipse, "ellipse");
    private void OnDrawGrid(object? sender, RoutedEventArgs e) => ToggleShape(MapControl.ShapeKind.Grid, "grid");

    private void RunCursorEdit(string status)
    {
        UpdateInfo();
        MapView.Focus();
        SetStatus(status);
    }

    private void ToggleDrawMode(bool linesOnly, string name, bool curve = false)
    {
        MapView.ToggleDrawMode(linesOnly, curve);
        MapView.Focus();
        SetStatus(MapView.DrawMode
            ? $"Draw {name}: click to place vertices, click the first point or {CommandHint("map2d.finish-draw")} to close, {CommandHint("map2d.cancel-draw")} or right-click to cancel."
            : "Draw mode off.");
    }

    private void ToggleShape(MapControl.ShapeKind kind, string name)
    {
        MapView.SetShapeMode(kind);
        MapView.Focus();
        SetStatus(MapView.CurrentShape == kind
            ? $"Draw {name}: drag a box to create the sector (corners snap to grid). Select the menu item again to stop."
            : "Shape draw off.");
    }

    // ---- View / Help ----

    private void OnFit(object? sender, RoutedEventArgs e) { MapView.FitToMap(); MapView.MarkGeometryDirty(); }

    private void OnTagStatistics(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        _mapOptions ??= new MapOptions { CurrentName = _mapMarker ?? "MAP01" };
        SyncMapOptionsToView();
        var win = new TagStatisticsWindow(ConfiguredTagSearch.UsedTagStatistics(_map, _config), _mapOptions.TagLabels);
        win.LabelChanged += (tag, label) =>
        {
            if (_mapOptions is null) return;
            if (string.IsNullOrWhiteSpace(label)) _mapOptions.TagLabels.Remove(tag);
            else _mapOptions.TagLabels[tag] = label;
            MarkMapDirty();
            RefreshTagExplorer();
        };
        win.TagActivated += (tag, mode) =>
        {
            if (_map is null) return;
            var r = ConfiguredTagSearch.Find(_map, tag.ToString(), _config);
            MapView.RevealSelection(mode ?? MapControl.EditMode.Linedefs, r.Focus);
            UpdateInfo();
            SetStatus(TagWindowModel.TagActivatedStatusText(tag, r.Count));
        };
        win.Show(this);
    }

    private void OnTagExplorer(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        if (_tagExplorer != null)
        {
            RefreshTagExplorer();
            _tagExplorer.Activate();
            return;
        }

        var win = new TagExplorerWindow(BuildTagExplorerEntries(null), _mapOptions?.TagLabels, _settings.TagExplorerSettings);
        _tagExplorer = win;
        win.Closed += (_, _) => _tagExplorer = null;
        win.OptionsChanged += () =>
        {
            if (_settings.TagExplorerSettings != win.Settings)
            {
                _settings.TagExplorerSettings = win.Settings;
                SaveSettings();
            }
            RefreshTagExplorer();
        };
        win.EntryActivated += SelectTagExplorerEntry;
        win.ExportRequested += ExportTagExplorer;
        win.Show(this);
    }

    private void OnUdbScriptDocker(object? sender, RoutedEventArgs e)
    {
        if (_udbScriptDocker != null)
        {
            _udbScriptDocker.Activate();
            return;
        }

        UdbScriptDirectory scripts = UdbScriptDiscovery.ApplySavedOptionValues(
            UdbScriptDiscovery.DiscoverFromAppPath(AppContext.BaseDirectory),
            _settings.UdbScriptSettings);
        _udbScriptSlotAssignments = UdbScriptDockerModel.LoadSlotAssignments(
            AllUdbScripts(scripts),
            _settings.UdbScriptSettings);
        IReadOnlySet<string> collapsedDirectoryHashes = UdbScriptDockerModel.LoadCollapsedDirectoryHashes(
            scripts,
            _settings.UdbScriptSettings);
        _udbScriptDocker = new UdbScriptDockerWindow(scripts, _udbScriptSlotAssignments, collapsedDirectoryHashes: collapsedDirectoryHashes);
        _udbScriptDocker.Closed += (_, _) => _udbScriptDocker = null;
        _udbScriptDocker.RunRequested += script => RunUdbScriptPlan(UdbScriptActions.ExecuteCurrentPlan(script));
        _udbScriptDocker.EditRequested += OpenUdbScriptExternalEditor;
        _udbScriptDocker.OptionsRequested += EditUdbScriptOptions;
        _udbScriptDocker.ResetOptionsRequested += ResetUdbScriptOptions;
        _udbScriptDocker.SlotAssignmentRequested += AssignUdbScriptSlot;
        _udbScriptDocker.SlotClearedRequested += ClearUdbScriptSlot;
        _udbScriptDocker.OpenFolderRequested += OpenUdbScriptFolderInExplorer;
        _udbScriptDocker.CollapsedDirectoryHashesChanged += collapsed => SaveUdbScriptSettings(
            UdbScriptDockerModel.SaveDirectoryExpansionOperations(scripts, collapsed));
        _udbScriptDocker.Show(this);
    }

    private void AssignUdbScriptSlot(UdbScriptInfo script, int slot)
    {
        _udbScriptSlotAssignments = UdbScriptDockerModel.AssignSlot(_udbScriptSlotAssignments, slot, script);
        _udbScriptDocker?.ApplySlotAssignments(_udbScriptSlotAssignments);
        IReadOnlyList<UdbScriptSettingOperation> operations = UdbScriptDockerModel.SaveSlotAssignmentOperations(_udbScriptSlotAssignments);
        SaveUdbScriptSlotSettings(operations);
        SetStatus(UdbScriptDockerModel.AssignedSlotStatusText(script, slot, operations.Count));
    }

    private void ClearUdbScriptSlot(UdbScriptInfo script)
    {
        int slot = UdbScriptDockerModel.SlotForScript(script, _udbScriptSlotAssignments);
        if (slot == 0)
        {
            SetStatus($"UDBScript is not assigned to a slot: {script.Name}");
            return;
        }

        _udbScriptSlotAssignments = UdbScriptDockerModel.AssignSlot(_udbScriptSlotAssignments, slot, null);
        _udbScriptDocker?.ApplySlotAssignments(_udbScriptSlotAssignments);
        IReadOnlyList<UdbScriptSettingOperation> operations = UdbScriptDockerModel.SaveSlotAssignmentOperations(_udbScriptSlotAssignments);
        SaveUdbScriptSlotSettings(operations);
        SetStatus(UdbScriptDockerModel.ClearedSlotStatusText(script, slot, operations.Count));
    }

    private void SaveUdbScriptSlotSettings(IReadOnlyList<UdbScriptSettingOperation> operations)
    {
        _settings.UdbScriptSettings ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (string key in _settings.UdbScriptSettings.Keys
            .Where(key => key.StartsWith(UdbScriptDockerModel.ScriptSlotSettingPrefix, StringComparison.Ordinal))
            .ToArray())
            _settings.UdbScriptSettings.Remove(key);

        SaveUdbScriptSettings(operations);
    }

    private void SaveUdbScriptSettings(IReadOnlyList<UdbScriptSettingOperation> operations)
    {
        _settings.UdbScriptSettings ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (UdbScriptSettingOperation operation in operations)
        {
            if (operation.Kind == UdbScriptSettingOperationKind.Write)
                _settings.UdbScriptSettings[operation.Key] = operation.Value;
            else if (operation.Kind == UdbScriptSettingOperationKind.Delete)
                _settings.UdbScriptSettings.Remove(operation.Key);
        }

        SaveSettings();
    }

    private void OpenUdbScriptFolderInExplorer(string folderPath)
    {
        if (!System.IO.Directory.Exists(folderPath))
        {
            SetStatus($"UDBScript folder not found: {folderPath}");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folderPath)
            {
                UseShellExecute = true,
            });
            SetStatus($"UDBScript folder open requested: {folderPath}");
        }
        catch (Exception ex)
        {
            LogAndSetStatus(ex, "UDBScript folder launch failed");
        }
    }

    private static IReadOnlyList<UdbScriptInfo> AllUdbScripts(UdbScriptDirectory directory)
    {
        var scripts = new List<UdbScriptInfo>();
        AddUdbScripts(directory, scripts);
        return scripts;
    }

    private static void AddUdbScripts(UdbScriptDirectory directory, List<UdbScriptInfo> scripts)
    {
        scripts.AddRange(directory.Scripts);
        foreach (UdbScriptDirectory child in directory.Directories)
            AddUdbScripts(child, scripts);
    }

    private void OpenUdbScriptExternalEditor(UdbScriptInfo script)
    {
        string editorPath = UdbScriptPreferencesModel.ResolveExternalEditorPath(
            _settings.UdbScriptExternalEditor ?? "",
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            System.IO.File.Exists);
        UdbScriptExternalEditorLaunchPlan plan = UdbScriptPreferencesModel.EditScriptLaunchPlan(
            editorPath,
            script.ScriptFile);
        if (!plan.ShouldLaunch)
        {
            SetStatus(plan.Message ?? "");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(UdbScriptPreferencesModel.CreateExternalEditorStartInfo(plan));
            SetStatus($"UDBScript edit requested: {script.Name}");
        }
        catch (Exception ex)
        {
            LogAndSetStatus(ex, "UDBScript editor launch failed");
        }
    }

    private async void EditUdbScriptOptions(UdbScriptInfo script)
    {
        if (script.Options.Count == 0)
        {
            SetStatus($"UDBScript has no options: {script.Name}");
            return;
        }

        var dialog = new UdbScriptOptionsDialog(script.Options);
        if (!await dialog.ShowDialog<bool>(this)) return;

        UdbScriptDockerApplyOptionsResult result = UdbScriptDockerModel.ApplyEditedScriptOptions(script, dialog.Options);
        if (result.Script is null) return;

        _udbScriptDocker?.ApplyCurrentScript(result.Script);
        SaveUdbScriptSettings(result.Operations);
        SetStatus(UdbScriptDockerModel.OptionsEditedStatusText(script, result.Operations.Count));
    }

    private void ResetUdbScriptOptions(UdbScriptInfo script)
    {
        UdbScriptDockerResetOptionsResult result = UdbScriptDockerModel.ResetSelectedScriptOptions(script);
        if (result.Script is null) return;

        _udbScriptDocker?.ApplyCurrentScript(result.Script);
        SaveUdbScriptSettings(result.Operations);
        SetStatus(UdbScriptDockerModel.OptionsResetStatusText(script, result.Operations.Count));
    }

    private void RefreshTagExplorer()
    {
        if (_tagExplorer is null) return;
        _tagExplorer.SetEntries(BuildTagExplorerEntries(_tagExplorer.Options), _mapOptions?.TagLabels);
    }

    private async void ExportTagExplorer(string contents)
    {
        if (string.IsNullOrEmpty(contents)) { SetStatus("No Tag Explorer entries to export."); return; }

        var top = GetTopLevel(this);
        if (top?.StorageProvider == null) return;

        string mapName = _mapMarker ?? "map";
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Tag Explorer Info",
            SuggestedFileName = mapName + "_info.txt",
            DefaultExtension = "txt",
            FileTypeChoices = new[] { new FilePickerFileType("Text file") { Patterns = new[] { "*.txt" } } },
        });
        if (file?.TryGetLocalPath() is not { } path) return;

        try
        {
            System.IO.File.WriteAllText(path, contents);
            SetStatus($"Exported Tag Explorer info to {System.IO.Path.GetFileName(path)}.");
        }
        catch (Exception ex)
        {
            LogAndSetStatus(ex, "Tag Explorer export failed");
        }
    }

    private IReadOnlyList<TagExplorerEntry> BuildTagExplorerEntries(TagExplorerOptions? options)
    {
        if (_map is null) return Array.Empty<TagExplorerEntry>();
        TagExplorerOptions effective = options ?? new TagExplorerOptions();
        effective = effective with { IsUdmf = _mapFormat == MapFormat.Udmf };
        return TagExplorerModel.BuildEntries(_map, _config, effective);
    }

    private void SelectTagExplorerEntry(TagExplorerEntry entry)
    {
        if (_map is null) return;
        _map.ClearAllSelected();

        MapControl.EditMode mode;
        Vector2D? focus = null;
        switch (entry.Kind)
        {
            case TagExplorerEntryKind.Thing when entry.Index >= 0 && entry.Index < _map.Things.Count:
                Thing thing = _map.Things[entry.Index];
                thing.Selected = true;
                mode = MapControl.EditMode.Things;
                focus = thing.Position;
                break;
            case TagExplorerEntryKind.Sector when entry.Index >= 0 && entry.Index < _map.Sectors.Count:
                Sector sector = _map.Sectors[entry.Index];
                sector.Selected = true;
                mode = MapControl.EditMode.Sectors;
                focus = SectorFocus(sector);
                break;
            case TagExplorerEntryKind.Linedef when entry.Index >= 0 && entry.Index < _map.Linedefs.Count:
                Linedef line = _map.Linedefs[entry.Index];
                line.Selected = true;
                mode = MapControl.EditMode.Linedefs;
                focus = (line.Start.Position + line.End.Position) * 0.5;
                break;
            default:
                SetStatus("Tag Explorer entry no longer exists.");
                return;
        }

        MapView.RevealSelection(mode, focus);
        UpdateInfo();
        SetStatus($"Tag Explorer: selected {entry.DefaultName.ToLowerInvariant()} {entry.Index}.");
    }

    private static Vector2D? SectorFocus(Sector sector)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        bool found = false;
        foreach (Sidedef side in sector.Sidedefs)
        {
            if (side.Line == null) continue;
            Add(side.Line.Start.Position);
            Add(side.Line.End.Position);
        }

        return found ? new Vector2D((minX + maxX) * 0.5, (minY + maxY) * 0.5) : null;

        void Add(Vector2D point)
        {
            minX = Math.Min(minX, point.x);
            minY = Math.Min(minY, point.y);
            maxX = Math.Max(maxX, point.x);
            maxY = Math.Max(maxY, point.y);
            found = true;
        }
    }

    private void OnThingStatistics(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        var win = new ThingStatisticsWindow(MapSearch.ThingTypeStatistics(_map), _config);
        win.ThingTypeActivated += type =>
        {
            if (_map is null) return;
            var r = MapSearch.Find(_map, FindCategory.ThingType, type.ToString());
            MapView.RevealSelection(MapControl.EditMode.Things, r.Focus);
            UpdateInfo();
            SetStatus(ThingStatisticsWindowModel.TypeActivatedStatusText(type, r.Count));
        };
        win.Show(this);
    }

    private void OnUndoRedoPanel(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        if (_undoRedoPanel != null)
        {
            RefreshUndoRedoPanel();
            _undoRedoPanel.Activate();
            return;
        }

        var win = new UndoRedoPanelWindow(UndoRedoPanelState());
        _undoRedoPanel = win;
        win.Closed += (_, _) => _undoRedoPanel = null;
        win.OperationRequested += PerformUndoRedoPanelOperation;
        win.Show(this);
    }

    private void RefreshUndoRedoPanel()
    {
        _undoRedoPanel?.SetState(UndoRedoPanelState());
    }

    private UndoRedoPanelState UndoRedoPanelState()
        => UndoRedoPanelModel.Build("Map loaded", _undo);

    private void PerformUndoRedoPanelOperation(UndoRedoPanelOperation operation)
    {
        if (_map is null || _undo is null) return;

        int performed = operation.Kind switch
        {
            UndoRedoPanelOperationKind.Undo => _undo.PerformUndo(operation.Levels),
            UndoRedoPanelOperationKind.Redo => _undo.PerformRedo(operation.Levels),
            _ => 0,
        };
        if (performed == 0) return;

        MarkMapDirty();
        MapView.MarkGeometryDirty();
        UpdateInfo();
        RefreshUndoRedoPanel();
        SetStatus(operation.StatusText(performed));
    }

    private void OnCommentsPanel(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        if (_commentsPanel != null)
        {
            RefreshCommentsPanel();
            _commentsPanel.Activate();
            return;
        }

        CommentsPanelMode currentMode = CommentModeFor(MapView.CurrentEditMode);
        var win = new CommentsPanelWindow(
            CommentsPanelModel.BuildGroups(_map, CommentsPanelModel.EffectiveFilterMode(
                _settings.CommentsPanelSettings,
                currentMode,
                CommentsPanelMode.All)),
            _settings.CommentsPanelSettings,
            currentMode);
        _commentsPanel = win;
        win.Closed += (_, _) => _commentsPanel = null;
        win.FilterChanged += _ => RefreshCommentsPanel();
        win.OptionsChanged += settings =>
        {
            _settings.CommentsPanelSettings = settings;
            SaveSettings();
            RefreshCommentsPanel();
        };
        win.GroupActivated += SelectCommentGroup;
        win.RemoveRequested += RemoveCommentGroup;
        win.SetSelectedCommentRequested += SetCommentOnCurrentSelection;
        win.Show(this);
    }

    private void RefreshCommentsPanel()
    {
        if (_map is null || _commentsPanel is null) return;
        _commentsPanel.SetCurrentMode(CommentModeFor(MapView.CurrentEditMode));
        _commentsPanel.SetGroups(CommentsPanelModel.BuildGroups(_map, _commentsPanel.FilterMode, _commentsPanel.SearchText));
        _commentsPanel.SetSelectionComment(CurrentSelectionComment());
    }

    private void SelectCommentGroup(CommentGroup group)
    {
        if (_map is null) return;
        _map.ClearAllSelected();
        foreach (IFielded element in CommentsPanelModel.CreateSelectionTarget(group).Elements)
        {
            if (element is Sidedef side)
            {
                side.Selected = true;
                side.Line.Selected = true;
            }
            else if (element is DBuilder.Map.ISelectable selectable)
            {
                selectable.Selected = true;
            }
        }

        var area = CommentsPanelModel.CreateViewArea(group);
        var focus = new Vector2D(area.X + area.Width * 0.5f, area.Y + area.Height * 0.5f);
        MapView.RevealSelection(EditModeFor(CommentsPanelModel.SelectionMode(group)), focus);
        UpdateInfo();
        RefreshCommentsPanel();
        SetStatus($"Selected comment group: {group.Comment}");
    }

    private void RemoveCommentGroup(CommentGroup group)
    {
        if (_map is null || _undo is null) return;
        CreateUndo("Remove comment");
        CommentsPanelModel.RemoveComment(group);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        RefreshCommentsPanel();
        SetStatus(CommentsPanelModel.RemoveStatusText(group.Elements.Count));
    }

    private void SetCommentOnCurrentSelection(string comment)
    {
        if (_map is null || _undo is null) return;
        IReadOnlyList<IFielded> elements = CurrentCommentSelection();
        if (elements.Count == 0)
        {
            SetStatus($"Select one or more {MapView.CurrentEditMode.ToString().ToLowerInvariant()} first.");
            return;
        }

        CreateUndo("Set comment");
        CommentsPanelModel.SetComment(elements, comment);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        RefreshCommentsPanel();
        SetStatus(CommentsPanelModel.SetStatusText(elements.Count));
    }

    private IReadOnlyList<IFielded> CurrentCommentSelection()
        => MapView.CurrentEditMode switch
        {
            MapControl.EditMode.Vertices => _map?.GetSelectedVertices().Cast<IFielded>().ToList() ?? [],
            MapControl.EditMode.Linedefs => _map?.GetSelectedLinedefs().Cast<IFielded>().ToList() ?? [],
            MapControl.EditMode.Sectors => _map?.GetSelectedSectors().Cast<IFielded>().ToList() ?? [],
            MapControl.EditMode.Things => _map?.GetSelectedThings().Cast<IFielded>().ToList() ?? [],
            _ => [],
        };

    private string CurrentSelectionComment()
    {
        IReadOnlyList<IFielded> elements = CurrentCommentSelection();
        if (elements.Count == 0) return "";

        string? shared = null;
        foreach (IFielded element in elements)
        {
            string comment = element.Fields.TryGetValue(CommentsPanelModel.CommentField, out object? raw)
                ? raw?.ToString() ?? ""
                : "";

            shared ??= comment;
            if (!string.Equals(shared, comment, StringComparison.Ordinal)) return "";
        }

        return shared ?? "";
    }

    private static MapControl.EditMode EditModeFor(CommentsPanelMode mode)
        => mode switch
        {
            CommentsPanelMode.Vertices => MapControl.EditMode.Vertices,
            CommentsPanelMode.Sectors => MapControl.EditMode.Sectors,
            CommentsPanelMode.Things => MapControl.EditMode.Things,
            _ => MapControl.EditMode.Linedefs,
        };

    private static CommentsPanelMode CommentModeFor(MapControl.EditMode mode)
        => mode switch
        {
            MapControl.EditMode.Vertices => CommentsPanelMode.Vertices,
            MapControl.EditMode.Sectors => CommentsPanelMode.Sectors,
            MapControl.EditMode.Things => CommentsPanelMode.Things,
            _ => CommentsPanelMode.Linedefs,
        };

    private void OnStatusHistory(object? sender, RoutedEventArgs e)
        => new StatusHistoryWindow(_statusHistory.Entries, ClearStatusHistory).Show(this);

    private void ClearStatusHistory()
    {
        _statusHistory.Clear();
        StatusText.Text = "Status history cleared.";
    }

    private void OnErrorLog(object? sender, RoutedEventArgs e)
        => new ErrorLogWindow().Show(this);

    private async void OnGoToCoordinates(object? sender, RoutedEventArgs e)
    {
        var dlg = new CenterOnCoordinatesDialog(MapView.ViewCenter);
        if (await dlg.ShowDialog<bool>(this))
        {
            MapView.CenterOn(dlg.ResultX, dlg.ResultY);
            MapView.Focus();
            SetStatus($"Centered on {dlg.ResultX:0.###}, {dlg.ResultY:0.###}.");
        }
    }

    private async void OnGridSetup(object? sender, RoutedEventArgs e)
    {
        var dlg = new GridSetupDialog(MapView.GridSetupSnapshot(), _resources);
        if (await dlg.ShowDialog<bool>(this))
        {
            var grid = new GridSetup(_mapFormat == MapFormat.Udmf);
            grid.SetGridSize(dlg.ResultSize);
            grid.SetGridOrigin(dlg.ResultOriginX, dlg.ResultOriginY);
            grid.SetGridRotation(dlg.ResultRotation);
            grid.SetBackground(dlg.ResultBackground, dlg.ResultBackgroundSource);
            grid.SetBackgroundView(dlg.ResultBackgroundX, dlg.ResultBackgroundY,
                dlg.ResultBackgroundScaleX, dlg.ResultBackgroundScaleY);
            MapView.ApplyGridSetup(grid);
            MarkMapDirty();
            UpdateStatusDetails();
            SetStatus("Grid setup updated.");
        }
    }

    private void OnToggleSnapToGrid(object? sender, RoutedEventArgs e)
    {
        SetStatus(MapView.ToggleSnapToGrid());
        UpdateStatusDetails();
        MapView.Focus();
    }

    private void OnToggleDynamicGridSize(object? sender, RoutedEventArgs e)
    {
        SetStatus(MapView.ToggleDynamicGridSize());
        _settings.DynamicGridSize = MapView.DynamicGridSizeEnabled;
        SaveSettings();
        UpdateStatusDetails();
        MapView.Focus();
    }

    private void OnSmartGridTransform(object? sender, RoutedEventArgs e)
        => ApplyGridTransformCommand(MapView.SmartGridTransform);

    private void OnAlignGridToLinedef(object? sender, RoutedEventArgs e)
        => ApplyGridTransformCommand(MapView.AlignGridToSelectedLinedef);

    private void OnSetGridOriginToVertex(object? sender, RoutedEventArgs e)
        => ApplyGridTransformCommand(MapView.SetGridOriginToSelectedVertex);

    private void OnResetGridTransform(object? sender, RoutedEventArgs e)
        => ApplyGridTransformCommand(MapView.ResetGridTransform);

    private void ApplyGridTransformCommand(Func<string> action)
    {
        GridSetup before = MapView.GridSetupSnapshot();
        string status = action();
        GridSetup after = MapView.GridSetupSnapshot();
        SetStatus(status);
        if (GridTransformChanged(before, after)) MarkMapDirty();
        UpdateStatusDetails();
        MapView.Focus();
    }

    private static bool GridTransformChanged(GridSetup before, GridSetup after)
        => before.GridOriginX != after.GridOriginX
        || before.GridOriginY != after.GridOriginY
        || before.GridRotate != after.GridRotate;

    private void OnSnapSelectionToGrid(object? sender, RoutedEventArgs e)
    {
        SetStatus(MapView.SnapSelectedMapElementsToGrid());
        UpdateInfo();
        UpdateStatusDetails();
        MapView.Focus();
    }

    private void OnGridSizeDown(object? sender, RoutedEventArgs e) => ChangeGridSize(larger: false);
    private void OnGridSizeUp(object? sender, RoutedEventArgs e) => ChangeGridSize(larger: true);

    private void ChangeGridSize(bool larger)
    {
        SetStatus(MapView.ChangeGridSize(larger));
        _settings.DynamicGridSize = MapView.DynamicGridSizeEnabled;
        SaveSettings();
        MarkMapDirty();
        UpdateStatusDetails();
        MapView.Focus();
    }

    // Opens a non-modal panel to show/hide thing categories in the 2D view.
    private void OnThingFilter(object? sender, RoutedEventArgs e)
    {
        if (_config is null || _config.Things.Count == 0) { SetStatus("Load a game config to filter thing categories."); return; }
        var cats = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in _config.Things.Values)
        {
            string key = MapControl.ThingCategoryKey(t.Category);
            if (seen.Add(key)) cats.Add(key);
        }
        cats.Sort(StringComparer.OrdinalIgnoreCase);

        var win = new ThingFilterWindow(cats, MapView.IsThingCategoryHidden, _config.ThingsFilters, MapView.ActiveThingsFilter);
        win.FilterSelected += filter =>
        {
            MapView.SetActiveThingsFilter(filter);
            SetStatus(filter == null ? "Thing filter off." : $"Thing filter: {filter.Name}");
        };
        win.CategoryToggled += (cat, hidden) => MapView.SetThingCategoryHidden(cat, hidden);
        win.Show(this);
    }

    private void OnToggleBlockmap(object? sender, RoutedEventArgs e)
    {
        MapView.ShowBlockmap = !MapView.ShowBlockmap;
        SetStatus($"Blockmap overlay {(MapView.ShowBlockmap ? "on" : "off")}.");
    }

    private void OnBlockmapExplorer(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }

        byte[]? bytes = ReadCurrentMapLump("BLOCKMAP");
        BlockmapLumpData blockmap = BlockmapLump.Parse(bytes);
        _blockmapExplorer?.Close();
        _blockmapExplorer = new BlockmapExplorerWindow(blockmap, _map.Linedefs.Count);
        _blockmapExplorer.Closed += (_, _) =>
        {
            MapView.SetBlockmapExplorerOverlay(null, null, null, includeSharedBlocks: false, showQuestionableBlocks: false);
            _blockmapExplorer = null;
        };
        _blockmapExplorer.OverlayChanged += (column, row, shared, questionable) =>
        {
            MapView.SetBlockmapExplorerOverlay(blockmap, column, row, shared, questionable);
            MapView.ShowBlockmap = true;
        };
        _blockmapExplorer.BlockActivated += (column, row) =>
        {
            double x = blockmap.OriginX + (column + 0.5) * BlockmapLump.BlockSize;
            double y = blockmap.OriginY + (row + 0.5) * BlockmapLump.BlockSize;
            MapView.CenterOn(x, y);
            MapView.ShowBlockmap = true;
            SetStatus($"Blockmap block ({column}, {row}).");
        };
        _blockmapExplorer.Show(this);
        SetStatus($"Blockmap Explorer: {blockmap.Status}, {blockmap.Columns} x {blockmap.Rows}.");
    }

    private void OnToggleSectorFills(object? sender, RoutedEventArgs e)
    {
        bool shown = MapView.ToggleSectorFills();
        SetStatus($"Sector fills {(shown ? "shown" : "hidden")}.");
        MapView.Focus();
    }

    private void OnToggleThings(object? sender, RoutedEventArgs e)
    {
        bool shown = MapView.ToggleThings();
        SetStatus($"Things {(shown ? "shown" : "hidden")}.");
        MapView.Focus();
    }

    private void OnToggle3DMode(object? sender, RoutedEventArgs e)
    {
        bool active = MapView.Toggle3DMode();
        SetStatus(active ? "Mode: 3D" : $"Mode: {MapView.CurrentEditMode}");
        UpdateStatusDetails();
        MapView.Focus();
    }

    // Toggles the BSP node partition overlay, reading and parsing the map's NODES lump on enable.
    private void OnToggleNodes(object? sender, RoutedEventArgs e)
    {
        if (MapView.ShowNodes) { MapView.ShowNodes = false; SetStatus("Nodes overlay off."); return; }
        if (_wadPath is null || _mapMarker is null) { SetStatus("Nodes overlay needs the source WAD."); return; }

        ClassicNodesStructure structure = ReadClassicNodesStructure();
        if (structure.IsValid)
        {
            var lines = new List<(Vector2D, Vector2D)>(structure.Nodes.Count);
            foreach (ClassicNode node in structure.Nodes)
                lines.Add((new Vector2D(node.Partition.X1, node.Partition.Y1), new Vector2D(node.Partition.X2, node.Partition.Y2)));

            IReadOnlyList<ClassicSubsectorPolygon> polygons = NodesReader.BuildClassicSubsectorPolygons(structure, 32767);
            var overlayPolygons = new List<IReadOnlyList<Vector2D>>(polygons.Count);
            foreach (ClassicSubsectorPolygon polygon in polygons)
                overlayPolygons.Add(polygon.Points);

            MapView.SetNodeLines(lines);
            MapView.SetNodePolygons(overlayPolygons);
            MapView.ShowNodes = true;
            SetStatus(NodesViewerModel.OverlayStatusText(lines.Count, overlayPolygons.Count));
            return;
        }

        byte[]? bytes;
        using (var wad = new WAD(_wadPath, openreadonly: true)) bytes = WadMaps.ReadMapLump(wad, _mapMarker, "NODES");
        var parts = NodesReader.Parse(bytes ?? Array.Empty<byte>());
        if (parts.Count == 0) { SetStatus($"Nodes overlay unavailable: {structure.Status}."); return; }

        var fallbackLines = new List<(Vector2D, Vector2D)>(parts.Count);
        foreach (var p in parts)
            fallbackLines.Add((new Vector2D(p.X1, p.Y1), new Vector2D(p.X2, p.Y2)));
        MapView.SetNodeLines(fallbackLines);
        MapView.SetNodePolygons(Array.Empty<IReadOnlyList<Vector2D>>());
        MapView.ShowNodes = true;
        SetStatus(NodesViewerModel.PartitionOverlayStatusText(parts.Count));
    }

    private void OnNodesViewer(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        if (_wadPath is null || _mapMarker is null) { SetStatus("Nodes Viewer needs the source WAD."); return; }

        ClassicNodesStructure structure = ReadClassicNodesStructure();
        var win = new NodesViewerWindow(structure);
        win.Show(this);
        SetStatus(NodesViewerModel.ViewerStatusText(structure));
    }

    private void OnVisplaneExplorerMode(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        if (_mapFormat == MapFormat.Udmf) { SetStatus("Visplane Explorer supports Doom and Hexen map formats."); return; }

        VisplaneExplorerPreflightResult preflight = VisplaneExplorerPreflight.CheckNodesLump(ReadCurrentMapLump("NODES"));
        if (!preflight.Success) { SetStatus($"Visplane Explorer unavailable: {preflight.Message}."); return; }

        VisplaneTileScan scan = VisplaneTileScan.CreateForMap(_map);
        IReadOnlyList<VisplaneTilePoint> queued = scan.QueuePoints(
            CurrentVisplaneViewRectangle(),
            currentQueuedPoints: 0,
            targetQueuedPoints: 1024);
        SetStatus(scan.Progress(queued.Count).FormatStatus());
        MapView.Focus();
    }

    private VisplaneMapRectangle CurrentVisplaneViewRectangle()
    {
        if (_map is null || _map.Vertices.Count == 0)
            return new VisplaneMapRectangle(0, 0, VisplaneTile.TileSize, VisplaneTile.TileSize);

        var area = MapSet.CreateArea(_map.Vertices);
        int left = (int)Math.Floor(area.Left) - VisplaneTile.TileSize;
        int top = (int)Math.Floor(area.Top) - VisplaneTile.TileSize;
        int width = Math.Max(VisplaneTile.TileSize, (int)Math.Ceiling(area.Width) + VisplaneTile.TileSize * 2);
        int height = Math.Max(VisplaneTile.TileSize, (int)Math.Ceiling(area.Height) + VisplaneTile.TileSize * 2);
        return new VisplaneMapRectangle(left, top, width, height);
    }

    private ClassicNodesStructure ReadClassicNodesStructure()
    {
        if (_wadPath is null || _mapMarker is null)
            return ClassicNodesStructure.Failure(ClassicNodesStatus.MissingOrTooShortNodes);

        byte[]? nodes;
        byte[]? segs;
        byte[]? vertices;
        byte[]? subsectors;
        using (var wad = new WAD(_wadPath, openreadonly: true))
        {
            nodes = WadMaps.ReadMapLump(wad, _mapMarker, "NODES");
            segs = WadMaps.ReadMapLump(wad, _mapMarker, "SEGS");
            vertices = WadMaps.ReadMapLump(wad, _mapMarker, "VERTEXES");
            subsectors = WadMaps.ReadMapLump(wad, _mapMarker, "SSECTORS");
        }

        return NodesReader.ParseClassicStructures(
            nodes ?? Array.Empty<byte>(),
            segs ?? Array.Empty<byte>(),
            vertices ?? Array.Empty<byte>(),
            subsectors ?? Array.Empty<byte>());
    }

    private void OnUsdfConversations(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        if (!UsdfDialogueParser.CanEditDialogue(_config)) { SetStatus("Current game configuration has no DIALOGUE map lump."); return; }

        byte[]? bytes = ReadCurrentMapLump("DIALOGUE");
        if (bytes is null || bytes.Length == 0)
        {
            SetStatus("Current map has no DIALOGUE lump.");
            return;
        }

        UsdfParseResult result = UsdfDialogueParser.ParseWithIncludes(
            Encoding.UTF8.GetString(bytes),
            ResolveCurrentDialogueInclude);
        _usdfConversations?.Close();
        _usdfConversations = new UsdfConversationWindow(result);
        _usdfConversations.Closed += (_, _) => _usdfConversations = null;
        _usdfConversations.Show(this);
        SetStatus(UsdfDialogueParser.EditorStatus(result));
    }

    private string? ResolveCurrentDialogueInclude(string lumpName)
    {
        byte[]? bytes = ReadCurrentMapLumpOrGlobalLump(lumpName);
        return bytes is null || bytes.Length == 0 ? null : Encoding.UTF8.GetString(bytes);
    }

    private byte[]? ReadCurrentMapLump(string lumpName)
    {
        if (_wadPath is not null && _mapMarker is not null)
        {
            using var wad = new WAD(_wadPath, openreadonly: true);
            return WadMaps.ReadMapLump(wad, _mapMarker, lumpName, _config);
        }

        if (_pk3Path is not null && _pk3MapArchivePath is not null && _mapMarker is not null)
            return Pk3Maps.ReadMapLump(_pk3Path, new Pk3MapEntry(_pk3MapArchivePath, new MapEntry(_mapMarker, _mapFormat)), lumpName, _config);

        return null;
    }

    private byte[]? ReadCurrentMapLumpOrGlobalLump(string lumpName)
    {
        if (_wadPath is not null && _mapMarker is not null)
        {
            using var wad = new WAD(_wadPath, openreadonly: true);
            return WadMaps.ReadMapLumpOrGlobalLump(wad, _mapMarker, lumpName, _config);
        }

        if (_pk3Path is not null && _pk3MapArchivePath is not null && _mapMarker is not null)
            return Pk3Maps.ReadMapLumpOrGlobalLump(_pk3Path, new Pk3MapEntry(_pk3MapArchivePath, new MapEntry(_mapMarker, _mapFormat)), lumpName, _config);

        return null;
    }

    private void OnToggleThingArrows(object? sender, RoutedEventArgs e)
    {
        MapView.ThingArrows = !MapView.ThingArrows;
        SetStatus($"Things: {(MapView.ThingArrows ? "arrows" : "sprites")}");
        MapView.Focus();
    }

    private void OnToggleComments(object? sender, RoutedEventArgs e)
    {
        bool shown = MapView.ToggleComments();
        SetStatus($"Comment icons {(shown ? "shown" : "hidden")}.");
        MapView.Focus();
    }

    private void OnToggleFixedThingsScale(object? sender, RoutedEventArgs e)
    {
        bool enabled = MapView.ToggleFixedThingsScale();
        SetStatus($"Fixed things scale {(enabled ? "enabled" : "disabled")}.");
        MapView.Focus();
    }

    private void OnToggleAlwaysShowVertices(object? sender, RoutedEventArgs e)
    {
        bool enabled = MapView.ToggleAlwaysShowVertices();
        SetStatus($"Always show vertices {(enabled ? "enabled" : "disabled")}.");
        MapView.Focus();
    }

    private void OnToggleFullBrightness(object? sender, RoutedEventArgs e)
    {
        bool enabled = MapView.ToggleFullBrightness();
        SetStatus($"Full brightness is now {(enabled ? "ON" : "OFF")}.");
        MapView.Focus();
    }

    private void OnMoveCameraToCursor(object? sender, RoutedEventArgs e)
    {
        SetStatus(MapView.MoveCameraToCursor() ? "Moved 3D camera to cursor." : "Aim at map geometry in 3D mode first.");
        MapView.Focus();
    }

    private void OnToggleHighlight(object? sender, RoutedEventArgs e)
    {
        bool enabled = MapView.ToggleHighlight();
        SetStatus($"Highlight is now {(enabled ? "ON" : "OFF")}.");
        MapView.Focus();
    }

    private void OnModelRenderNone(object? sender, RoutedEventArgs e)
        => SetModelRenderMode(ThingModelRenderMode.None);

    private void OnModelRenderSelection(object? sender, RoutedEventArgs e)
        => SetModelRenderMode(ThingModelRenderMode.Selection);

    private void OnModelRenderActiveFilter(object? sender, RoutedEventArgs e)
        => SetModelRenderMode(ThingModelRenderMode.ActiveThingsFilter);

    private void OnModelRenderAll(object? sender, RoutedEventArgs e)
        => SetModelRenderMode(ThingModelRenderMode.All);

    private void OnNextModelRenderMode(object? sender, RoutedEventArgs e)
        => ReportModelRenderMode(MapView.CycleModelRenderMode());

    private void SetModelRenderMode(ThingModelRenderMode mode)
    {
        MapView.SetModelRenderMode(mode);
        ReportModelRenderMode(mode);
    }

    private void ReportModelRenderMode(ThingModelRenderMode mode)
    {
        SetStatus($"Models rendering mode: {ThingModelRenderPlanner.StatusLabel(mode)}.");
        MapView.Focus();
    }

    private void OnViewModeWireframe(object? sender, RoutedEventArgs e)
        => SetClassicViewMode(MapControl.ClassicViewMode.Wireframe);

    private void OnViewModeBrightness(object? sender, RoutedEventArgs e)
        => SetClassicViewMode(MapControl.ClassicViewMode.Brightness);

    private void OnViewModeFloors(object? sender, RoutedEventArgs e)
        => SetClassicViewMode(MapControl.ClassicViewMode.FloorTextures);

    private void OnViewModeCeilings(object? sender, RoutedEventArgs e)
        => SetClassicViewMode(MapControl.ClassicViewMode.CeilingTextures);

    private void OnNextViewMode(object? sender, RoutedEventArgs e)
        => ReportClassicViewMode(MapView.NextViewMode2D());

    private void OnPreviousViewMode(object? sender, RoutedEventArgs e)
        => ReportClassicViewMode(MapView.PreviousViewMode2D());

    private void SetClassicViewMode(MapControl.ClassicViewMode mode)
    {
        MapView.SetViewMode2D(mode);
        ReportClassicViewMode(mode);
    }

    private void ReportClassicViewMode(MapControl.ClassicViewMode mode)
    {
        SetStatus($"View mode: {ClassicViewModeLabel(mode)}.");
        MapView.Focus();
    }

    private static string ClassicViewModeLabel(MapControl.ClassicViewMode mode)
        => mode switch
        {
            MapControl.ClassicViewMode.Wireframe => "Wireframe",
            MapControl.ClassicViewMode.Brightness => "Brightness Levels",
            MapControl.ClassicViewMode.FloorTextures => "Floor Textures",
            MapControl.ClassicViewMode.CeilingTextures => "Ceiling Textures",
            _ => mode.ToString(),
        };

    private void OnToggle3DFloors(object? sender, RoutedEventArgs e)
    {
        MapView.Show3DFloors = !MapView.Show3DFloors;
        SetStatus($"3D floors {(MapView.Show3DFloors ? "shown" : "hidden")}.");
        MapView.Focus();
    }

    private void OnImageExample(object? sender, RoutedEventArgs e)
    {
        if (_map is null)
        {
            SetStatus("Image Example needs a loaded map.");
            return;
        }

        bool active = MapView.ToggleImageExampleMode();
        SetStatus(active ? "Image Example mode." : $"Mode: {MapView.CurrentEditMode}");
        UpdateStatusDetails();
        MapView.Focus();
    }

    private async void OnAbout(object? sender, RoutedEventArgs e)
    {
        await new AboutWindow().ShowDialog(this);
        MapView.Focus();
    }

    private void OnShortcuts(object? sender, RoutedEventArgs e) => new ShortcutsWindow(_shortcutBindings).Show(this);

    // ---- Map loading ----

    private ResourceManager? _resources;

    private async Task LoadArchive(string path, bool promptForMap)
    {
        if (!await ConfirmDiscardDirtyMap()) return;

        if (IsPk3Path(path)) await LoadPk3(path, promptForMap);
        else await LoadWad(path, promptForMap);
    }

    private void LoadRecoveredAutosave(AutoSaveEntry autosave)
    {
        try
        {
            using var wad = new WAD(autosave.SnapshotPath, openreadonly: true);
            var maps = WadMaps.Find(wad);
            var entry = maps.FirstOrDefault(m => string.Equals(m.Name, autosave.Key.MapName, StringComparison.OrdinalIgnoreCase))
                ?? maps.FirstOrDefault();
            if (entry is null)
            {
                SetStatus($"Autosave contains no recoverable map: {autosave.DisplayName}");
                return;
            }

            var map = WadMaps.Load(wad, entry);
            if (map is null)
            {
                SetStatus($"Failed to recover autosave map: {autosave.DisplayName}");
                return;
            }

            _resources?.Dispose();
            _resources = null;
            _mapOptions = new MapOptions { CurrentName = entry.Name };
            SyncMapOptionsToView();
            _mapSettings = new Configuration(sorted: true);
            _map = map;
            _mapMarker = entry.Name;
            _sourceMapMarker = null;
            _wadPath = null;
            _sourceWadStamp = null;
            _pk3Path = null;
            _pk3Maps = null;
            _pk3MapArchivePath = null;
            _activeAutosaveKey = autosave.Key;
            _mapFormat = entry.Format;
            MapView.MapFormat = _mapFormat;
            _undo = new UndoManager(map);

            MapView.MapResources = null;
            MapView.Map = map;
            MapView.Focus();
            _mapDirty = true;
            _autosavePending = false;
            Title = CurrentEditorTitle();
            UpdateInfo();
            UpdateStatusDetails();
            SetStatus($"Recovered autosave {autosave.DisplayName}. Use Save WAD As to keep it.");
        }
        catch (Exception ex) { LogAndSetStatus(ex, "Recover autosave failed"); }
    }

    private async Task LoadWad(string path, bool promptForMap, string? preferredMapName = null)
    {
        try
        {
            List<MapEntry> maps;
            using (var wad = new WAD(path, openreadonly: true))
            {
                AutoDetectConfig(wad); // switch the auto/default config to match this WAD's game
                maps = CurrentWadMaps(wad);
                if (wad.IsIWAD) _iwadPath = path; // loaded an IWAD directly - usable as the Test Map base
            }
            if (maps.Count == 0) { SetStatus($"No map found in {System.IO.Path.GetFileName(path)}"); return; }

            var selected = maps[0];
            if (!string.IsNullOrWhiteSpace(preferredMapName))
            {
                var preferred = maps.FirstOrDefault(m => string.Equals(m.Name, preferredMapName, StringComparison.OrdinalIgnoreCase));
                if (preferred is null)
                {
                    SetStatus($"Recent map not found: {preferredMapName} in {System.IO.Path.GetFileName(path)}");
                    return;
                }
                selected = preferred;
            }
            else if (promptForMap && maps.Count > 1)
            {
                var dlg = new MapPickerDialog(maps, _mapMarker);
                if (!await dlg.ShowDialog<bool>(this) || dlg.Selected is not { } picked) return;
                selected = picked;
            }

            _wadPath = path;
            UpdateSourceWadStamp();
            _pk3Path = null;
            _pk3Maps = null;
            _pk3MapArchivePath = null;

            LoadMapEntry(selected);
            if (maps.Count > 1)
                SetStatus($"Loaded {selected.Name} ({maps.IndexOf(selected) + 1} of {maps.Count} maps - File > Open Map to switch)");
        }
        catch (Exception ex) { LogAndSetStatus(ex, "Load failed"); }
    }

    private async Task LoadPk3(string path, bool promptForMap, RecentMapReference? recentMap = null)
    {
        try
        {
            var maps = CurrentPk3Maps(path);
            if (maps.Count == 0) { SetStatus($"No embedded map WAD found in {System.IO.Path.GetFileName(path)}"); return; }

            var selected = maps[0];
            if (recentMap is not null)
            {
                var preferred = maps.FirstOrDefault(m =>
                    string.Equals(m.Map.Name, recentMap.MapName, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(recentMap.ArchivePath)
                        || string.Equals(m.ArchivePath, recentMap.ArchivePath, StringComparison.OrdinalIgnoreCase)));
                if (preferred is null)
                {
                    SetStatus($"Recent map not found: {RecentMapHeader(recentMap)}");
                    return;
                }
                selected = preferred;
            }
            else if (promptForMap && maps.Count > 1)
            {
                var displayMaps = new List<MapEntry>();
                foreach (var pk3Map in maps) displayMaps.Add(DisplayEntry(pk3Map));
                var dlg = new MapPickerDialog(displayMaps, null);
                if (!await dlg.ShowDialog<bool>(this) || dlg.Selected is not { } picked) return;

                int index = displayMaps.FindIndex(m => m.Name == picked.Name && m.Format == picked.Format);
                if (index >= 0) selected = maps[index];
            }

            _wadPath = null;
            _sourceWadStamp = null;
            _pk3Path = path;
            _pk3Maps = maps;
            _mapOptions = null;
            SyncMapOptionsToView();
            _mapSettings = null;

            RebuildPk3Resources(path);
            MergeActorsFromResources();

            LoadPk3MapEntry(selected);
            if (maps.Count > 1)
                SetStatus($"Loaded {selected.Map.Name} from {selected.ArchivePath} ({maps.IndexOf(selected) + 1} of {maps.Count} maps - File > Open Map to switch)");
        }
        catch (Exception ex) { LogAndSetStatus(ex, "PK3 load failed"); }
    }

    // Loads a specific map from the currently open WAD into the editor.
    private void LoadMapEntry(MapEntry entry, OpenMapSelectionOptions? selectedOptions = null)
    {
        if (_wadPath == null) return;
        try
        {
            using var wad = new WAD(_wadPath, openreadonly: true);
            var map = WadMaps.Load(wad, entry);
            if (map is null) { SetStatus($"Failed to load {entry.Name}"); return; }

            _mapOptions = LoadMapOptions(_wadPath, entry.Name, out _mapSettings);
            selectedOptions?.ApplyTo(_mapOptions);
            LoadConfigFromMapOptions(_mapOptions);
            ApplyOpenMapScriptCompiler(_mapOptions);
            SyncMapOptionsToView();
            int resourceIssues = RebuildWadResources(_wadPath, _mapOptions);

            _map = map;
            _mapMarker = entry.Name;
            _sourceMapMarker = entry.Name;
            _activeAutosaveKey = null;
            _mapFormat = entry.Format;
            MapView.MapFormat = _mapFormat;
            _undo = new UndoManager(map);

            MapView.Map = map;
            ApplyMapGridSetup(_mapOptions);
            MapView.RestoreView(_mapOptions.ViewPosition, _mapOptions.ViewScale);
            MapView.Focus(); // so Tab toggles 3D immediately instead of traversing the menu bar
            ClearMapDirty();
            RememberRecentMap(_wadPath, entry.Name);
            UpdateInfo();
            SetStatus(ResourceManager.MapLoadedStatusText(
                entry.Name,
                entry.Format.ToString(),
                map.Vertices.Count,
                map.Linedefs.Count,
                map.Sectors.Count,
                map.Things.Count,
                resourceIssues));
        }
        catch (Exception ex) { LogAndSetStatus(ex, "Load failed"); }
    }

    private void LoadConfigFromMapOptions(MapOptions options)
    {
        string? path = ConfigPickerModel.ResolveConfigPath(ConfigDir, options.ConfigFile, System.IO.File.Exists);
        if (path is null) return;
        string file = System.IO.Path.GetFileName(path);
        if (ConfigPickerModel.SameConfigPath(_configPath, path)) return;

        _config = GameConfiguration.FromFile(path);
        _configName = System.IO.Path.GetFileNameWithoutExtension(path);
        _configFile = file;
        _configPath = path;
        _configIsAuto = false;
        MapView.GameConfig = _config;
        ReloadCompilerConfiguration();
    }

    private void ApplyOpenMapScriptCompiler(MapOptions options)
    {
        MapOptionsScriptCompilerModel.ApplyOpenMapSelection(
            options,
            _scriptConfigurations,
            _config?.DefaultScriptCompiler ?? "");
    }

    private OpenMapSelectionOptions OpenMapOptionsForWadEntry(MapEntry entry)
    {
        if (_wadPath is null) return default;
        var options = LoadMapOptions(_wadPath, entry.Name, out _);
        return OpenMapSelectionOptions.FromMapOptions(options, LongTextureNamesSupportedForMapOptions(options));
    }

    private bool LongTextureNamesSupportedForMapOptions(MapOptions options)
    {
        return ConfigPickerModel.ResolveLongTextureNameSupport(
            ConfigDir,
            options.ConfigFile,
            _config?.UseLongTextureNames ?? false,
            System.IO.File.Exists,
            path =>
            {
                try { return GameConfiguration.FromFile(path).UseLongTextureNames; }
                catch { return _config?.UseLongTextureNames ?? false; }
            });
    }

    private void LoadPk3MapEntry(Pk3MapEntry entry)
    {
        if (_pk3Path == null) return;
        try
        {
            var map = Pk3Maps.Load(_pk3Path, entry);
            if (map is null) { SetStatus($"Failed to load {entry.Map.Name} from {entry.ArchivePath}"); return; }

            _mapOptions = null;
            SyncMapOptionsToView();
            _mapSettings = null;
            _map = map;
            _mapMarker = entry.Map.Name;
            _sourceMapMarker = null;
            _activeAutosaveKey = null;
            _mapFormat = entry.Map.Format;
            MapView.MapFormat = _mapFormat;
            _pk3MapArchivePath = entry.ArchivePath;
            _undo = new UndoManager(map);

            MapView.Map = map;
            MapView.Focus();
            ClearMapDirty();
            RememberRecentMap(_pk3Path, entry.Map.Name, entry.ArchivePath);
            UpdateInfo();
            SetStatus($"Loaded {entry.Map.Name} [{entry.Map.Format}] from {entry.ArchivePath}: {map.Vertices.Count} verts, {map.Linedefs.Count} lines, {map.Sectors.Count} sectors, {map.Things.Count} things");
        }
        catch (Exception ex) { LogAndSetStatus(ex, "PK3 map load failed"); }
    }

    private void RememberRecentMap(string path, string mapName, string? archivePath = null)
    {
        _settings.AddRecent(path);
        _settings.AddRecentMap(path, mapName, archivePath);
        _settings.RememberMapFolderForPath(path, System.IO.Directory.Exists);
        SaveSettings();
        RebuildRecentMenu();
    }

    private void CloseCurrentMap()
    {
        _map = null;
        _undo = null;
        _mapMarker = null;
        _sourceMapMarker = null;
        _wadPath = null;
        _sourceWadStamp = null;
        _pk3Path = null;
        _pk3Maps = null;
        _pk3MapArchivePath = null;
        _iwadPath = null;
        _activeAutosaveKey = null;
        _mapOptions = null;
        SyncMapOptionsToView();
        _mapSettings = null;
        _resources?.Dispose();
        _resources = null;
        MapView.MapResources = null;
        MapView.Map = null;
        ClearMapDirty();
        UpdateInfo();
        UpdateStatusDetails();
    }

    private static bool IsPk3Path(string path)
    {
        string ext = System.IO.Path.GetExtension(path);
        return ext.Equals(".pk3", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".pk7", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSamePath(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        try
        {
            return string.Equals(
                System.IO.Path.GetFullPath(a),
                System.IO.Path.GetFullPath(b),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void UpdateSourceWadStamp()
    {
        _sourceWadStamp = FileSaveStamp.TryRead(_wadPath, out var stamp) ? stamp : null;
    }

    private static string DbsPath(string wadPath) => System.IO.Path.ChangeExtension(wadPath, ".dbs");

    private MapOptions LoadMapOptions(string wadPath, string mapName, out Configuration root)
    {
        string dbsPath = DbsPath(wadPath);
        try
        {
            root = System.IO.File.Exists(dbsPath)
                ? new Configuration(dbsPath, sorted: true)
                : new Configuration(sorted: true);
        }
        catch
        {
            root = new Configuration(sorted: true);
        }

        var options = new MapOptions();
        options.ReadRootOptions(root, mapName);
        options.ReadResources();
        options.ReadDrawingOptions(_config?.UseLongTextureNames ?? false);
        options.ReadTagLabels();
        options.ReadExternalCommandSettings();
        return options;
    }

    private void ApplyMapGridSetup(MapOptions options)
    {
        var grid = new GridSetup(_mapFormat == MapFormat.Udmf);
        options.ReadGridSetup(grid);
        MapView.ApplyGridSetup(grid);
    }

    private int RebuildWadResources(string wadPath, MapOptions options)
    {
        _resources?.Dispose();
        _resources = new ResourceManager(_config);

        var baseResources = new DataLocationList(CurrentConfigurationResources());
        baseResources.AddRange(options.GetResources());
        int failures = AddBaseResourcesInOrder(baseResources);
        _resources.AddResource(new DataLocation(DataLocationType.Wad, wadPath, option1: options.StrictPatches));

        ApplyResourceConfig();
        MergeActorsFromResources();
        return failures;
    }

    private void RebuildPk3Resources(string pk3Path)
    {
        _resources?.Dispose();
        _resources = new ResourceManager(_config);
        AddBaseResourcesInOrder(CurrentConfigurationResources());
        _resources.AddResource(pk3Path);
        ApplyResourceConfig();
    }

    private void RebuildResourcesForActiveSource()
    {
        if (_wadPath is not null && _mapOptions is not null)
        {
            RebuildWadResources(_wadPath, _mapOptions);
            return;
        }

        if (_pk3Path is not null)
        {
            RebuildPk3Resources(_pk3Path);
            MergeActorsFromResources();
            return;
        }

        ApplyResourceConfig();
    }

    private DataLocationList CurrentConfigurationResources()
        => _settings.ResourcesForConfiguration(_configFile);

    private int AddBaseResourcesInOrder(IEnumerable<DataLocation> locations)
    {
        if (_resources is null) return 0;
        var ordered = locations.ToList();
        int failures = 0;
        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            var location = ordered[i];
            try
            {
                if (location.IsValid()) _resources.AddBaseResource(location);
                else failures++;
            }
            catch
            {
                failures++;
            }
        }

        return failures;
    }

    private static MapEntry DisplayEntry(Pk3MapEntry entry)
        => new($"{entry.Map.Name} @ {entry.ArchivePath}", entry.Map.Format);

    private List<MapEntry> CurrentWadMaps(WAD wad)
        => _config is null ? WadMaps.Find(wad) : WadMaps.Find(wad, _config);

    private List<Pk3MapEntry> CurrentPk3Maps(string path)
        => _config is null ? Pk3Maps.Find(path) : Pk3Maps.Find(path, _config);

    private string? CurrentPk3DisplayName()
        => _mapMarker is null || _pk3MapArchivePath is null ? null : $"{_mapMarker} @ {_pk3MapArchivePath}";

    private string CurrentEditorTitle()
    {
        if (_map is null) return "DBuilder";
        string dirty = _mapDirty ? "*" : "";
        if (_wadPath is not null)
            return $"DBuilder{dirty} - {System.IO.Path.GetFileName(_wadPath)} ({_mapMarker ?? "MAP01"})";
        if (_pk3Path is not null)
            return $"DBuilder{dirty} - {System.IO.Path.GetFileName(_pk3Path)} ({_pk3MapArchivePath}:{_mapMarker ?? "MAP01"})";
        return $"DBuilder{dirty} - ({_mapMarker ?? "new map"})";
    }

    private void CreateUndo(string description)
    {
        _undo?.CreateUndo(description);
        MarkMapDirty();
        RefreshUndoRedoPanel();
        UpdateCommandAvailability();
    }

    private void MarkMapDirty()
    {
        if (_map is null) return;
        _mapDirty = true;
        ScheduleAutosave();
        Title = CurrentEditorTitle();
    }

    private void ClearMapDirty()
    {
        _mapDirty = false;
        _autosavePending = false;
        Title = CurrentEditorTitle();
        UpdateCommandAvailability();
    }

    private void ScheduleAutosave()
    {
        _autosavePending = true;
        if (!_autosaveTimer.IsEnabled) _autosaveTimer.Start();
    }

    private void WriteAutosaveIfPending()
    {
        if (!_autosavePending || !_mapDirty)
        {
            _autosaveTimer.Stop();
            return;
        }

        _autosavePending = false;
        try
        {
            var key = CurrentAutosaveKey();
            if (key is null || _map is null || _mapMarker is null) return;

            byte[] bytes;
            using (var ms = new System.IO.MemoryStream())
            {
                using (var wad = new WAD(ms))
                {
                    WadMaps.SaveMap(wad, _mapMarker, _map, _mapFormat, _config);
                }
                bytes = ms.ToArray();
            }

            if (AutoSaveStore.Write(key, bytes) is not null)
                AutoSaveStore.Prune();
        }
        catch (Exception ex)
        {
            ErrorLog.Append(ex, "Autosave failed");
        }
    }

    private AutoSaveKey? CurrentAutosaveKey()
    {
        if (_activeAutosaveKey is not null) return _activeAutosaveKey;
        if (_mapMarker is null) return null;
        string source = _wadPath ?? _pk3Path ?? $"untitled:{_untitledAutosaveId}";
        return new AutoSaveKey(source, _mapMarker, _pk3MapArchivePath);
    }

    private void DeleteCurrentAutosave()
    {
        _autosavePending = false;
        var key = CurrentAutosaveKey();
        if (key is not null) AutoSaveStore.Delete(key);
        _activeAutosaveKey = null;
    }

    private async Task<bool> ConfirmDiscardDirtyMap()
    {
        if (!_mapDirty) return true;
        var dlg = new UnsavedChangesDialog(_mapMarker ?? "current map");
        bool discard = await dlg.ShowDialog<bool>(this);
        if (discard) DeleteCurrentAutosave();
        return discard;
    }

    // Saves the current map to a temporary PWAD (with nodes if a builder is configured) and launches a source port on it.
    private void OnTestMap(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _mapMarker is null) { SetStatus("No map loaded to test."); return; }
        if (_mapOptions?.TestPreCommand is { } preCommand)
        {
            var preResult = ExternalCommand.Run(preCommand, "Before test map");
            if (!preResult.Success)
            {
                SetStatus(preResult.Message);
                return;
            }
        }

        // Source port: env, else settings, else a standard GZDoom install.
        string? port = Environment.GetEnvironmentVariable("DBUILDER_TESTPORT");
        if (string.IsNullOrWhiteSpace(port) || !System.IO.File.Exists(port)) port = _settings.TestPort;
        if (string.IsNullOrWhiteSpace(port) || !System.IO.File.Exists(port)) port = DefaultGzdoomPath;
        if (string.IsNullOrWhiteSpace(port) || !System.IO.File.Exists(port))
        {
            SetStatus("Set a source port in Settings (or DBUILDER_TESTPORT) to use Test Map.");
            return;
        }

        // IWAD: env, else settings, else the one detected from the loaded WAD / added resource.
        string? iwad = Environment.GetEnvironmentVariable("DBUILDER_TESTIWAD");
        if (string.IsNullOrWhiteSpace(iwad) || !System.IO.File.Exists(iwad)) iwad = _settings.TestIwad;
        if (string.IsNullOrWhiteSpace(iwad) || !System.IO.File.Exists(iwad)) iwad = _iwadPath;
        if (string.IsNullOrWhiteSpace(iwad) || !System.IO.File.Exists(iwad))
        {
            SetStatus("No IWAD for testing - set one in Settings/DBUILDER_TESTIWAD, or open/add an IWAD.");
            return;
        }

        try
        {
            // Build a minimal PWAD containing only the edited map block (the IWAD provides everything else).
            byte[] bytes;
            var ms = new System.IO.MemoryStream();
            using (var dst = new WAD(ms)) { WadMaps.SaveMap(dst, _mapMarker, _map, _mapFormat, _config); bytes = ms.ToArray(); }
            BuildNodesIfConfigured(ref bytes, forTesting: true); // GZDoom can build nodes itself, but use the configured builder when present

            string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dbuilder_test_{_mapMarker}.wad");
            System.IO.File.WriteAllBytes(temp, bytes);

            string template = Environment.GetEnvironmentVariable("DBUILDER_TESTPORT_ARGS")
                ?? TestArgsTemplate();
            var args = SourcePort.BuildArgs(template, iwad!, temp, _mapMarker, TestResourcePaths());

            System.Diagnostics.Process.Start(SourcePort.CreateStartInfo(port!, args));
            if (_mapOptions?.TestPostCommand is { } postCommand)
            {
                var postResult = ExternalCommand.Run(postCommand, "After test map");
                if (!postResult.Success)
                {
                    SetStatus(postResult.Message);
                    return;
                }
            }
            SetStatus($"Testing {_mapMarker} in {System.IO.Path.GetFileNameWithoutExtension(port)} (iwad: {System.IO.Path.GetFileName(iwad)}).");
        }
        catch (Exception ex) { LogAndSetStatus(ex, "Test Map failed"); }
    }

    private string TestArgsTemplate()
    {
        if (!string.IsNullOrWhiteSpace(_settings.TestPortArgs)) return _settings.TestPortArgs!;
        if (!string.IsNullOrWhiteSpace(_config?.TestParameters)) return _config.TestParameters;
        return SourcePort.DefaultArgsTemplate;
    }

    private IEnumerable<string> TestResourcePaths()
    {
        if (_mapOptions is null) yield break;
        foreach (var location in _mapOptions.GetResources())
        {
            if (!location.NotForTesting && location.IsValid()) yield return location.Location;
        }
    }

    private FindReplaceWindow? _findWindow;

    // Opens (or focuses) the non-modal Find & Replace window and wires it to the map.
    private void OnFindReplace(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        if (_findWindow != null) { _findWindow.Activate(); return; }

        var win = new FindReplaceWindow(_config?.MixTexturesFlats == true);
        _findWindow = win;
        win.Closed += (_, _) => _findWindow = null;
        win.FindRequested += () =>
        {
            if (_map is null) return;
            var r = ConfiguredTagSearch.IsReferenceCategory(win.Category)
                ? ConfiguredTagSearch.FindReference(_map, win.Category, win.FindText, _config, win.WithinSelection)
                : win.Category == FindCategory.Tag
                    ? ConfiguredTagSearch.Find(_map, win.FindText, _config, win.WithinSelection)
                    : ConfiguredMapSearch.Find(_map, win.Category, win.FindText, _config, win.WithinSelection);
            MapView.RevealSelection(ModeFor(win.Category), r.Focus);
            win.SetResult(MapSearch.FormatFindResult(r.Count));
            UpdateInfo();
        };
        win.ReplaceRequested += () =>
        {
            if (_map is null || _undo is null) return;
            CreateUndo("Find & replace");
            int n = ConfiguredTagSearch.IsReferenceCategory(win.Category)
                ? ConfiguredTagSearch.ReplaceReference(_map, win.Category, win.FindText, win.ReplaceText, _config, win.WithinSelection)
                : win.Category == FindCategory.Tag
                    ? ConfiguredTagSearch.Replace(_map, win.FindText, win.ReplaceText, _config, win.WithinSelection)
                    : ConfiguredMapSearch.Replace(_map, win.Category, win.FindText, win.ReplaceText, _config, win.WithinSelection);
            if (n > 0) { MapView.MarkGeometryDirty(); MapView.RevealSelection(ModeFor(win.Category), null); }
            win.SetResult(MapSearch.FormatReplaceResult(n));
            UpdateInfo();
        };
        win.NextFreeTagRequested += () =>
        {
            if (_map is null) return;
            int tag = ConfiguredTagSearch.NextFreeTag(_map, _config);
            win.SetFindText(tag.ToString());
            win.SetResult(MapSearch.FormatNextFreeTagResult(tag));
        };
        win.Show(this);
    }

    // Opens a non-modal list of tags in use; selecting one selects and reveals its elements.
    private void OnTagList(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        var win = new TagListWindow(ConfiguredTagSearch.UsedTags(_map, _config), _mapOptions?.TagLabels);
        win.TagActivated += tag =>
        {
            if (_map is null) return;
            var r = ConfiguredTagSearch.Find(_map, tag.ToString(), _config);
            MapView.RevealSelection(MapControl.EditMode.Linedefs, r.Focus);
            UpdateInfo();
            SetStatus(TagWindowModel.TagActivatedStatusText(tag, r.Count));
        };
        win.Show(this);
    }

    // The edit mode that best shows matches of a given find category.
    private static MapControl.EditMode ModeFor(FindCategory cat) => cat switch
    {
        FindCategory.ThingType or
        FindCategory.ThingIndex or
        FindCategory.ThingAngle or
        FindCategory.ThingActionArguments or
        FindCategory.ThingFlags or
        FindCategory.ThingTag or
        FindCategory.ThingSectorReference or
        FindCategory.ThingThingReference => MapControl.EditMode.Things,
        FindCategory.SectorEffect or
        FindCategory.SectorIndex or
        FindCategory.SectorFloorHeight or
        FindCategory.SectorCeilingHeight or
        FindCategory.SectorBrightness or
        FindCategory.SectorFlags or
        FindCategory.SectorTag or
        FindCategory.Flat or
        FindCategory.SectorFloorFlat or
        FindCategory.SectorCeilingFlat or
        FindCategory.SectorUdmfField => MapControl.EditMode.Sectors,
        FindCategory.VertexIndex or
        FindCategory.VertexUdmfField => MapControl.EditMode.Vertices,
        _ => MapControl.EditMode.Linedefs, // actions, tags, sidedef textures
    };

    // Runs the map health checker and opens a non-modal results window; selecting an issue locates it.
    private void OnCheckMap(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        var issues = MapAnalysis.Check(_map, BuildCheckContext());
        var win = new MapCheckWindow(issues);
        win.IssueActivated += iss =>
        {
            MapView.NavigateTo(iss.Target, iss.Focus);
            UpdateInfo();
        };
        win.Show(this);
        SetStatus(MapIssueListModel.AnalysisStatusText(issues.Count));
    }

    private void OnCleanUpGeometry(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) { SetStatus("No map loaded."); return; }

        using var previewMap = _map.Clone();
        var preview = CleanUpGeometry(previewMap, _settings.AutoClearSidedefTextures);
        if (preview.Total == 0)
        {
            SetStatus("Geometry cleanup: no changes needed.");
            return;
        }

        CreateUndo("Clean up geometry");
        var result = CleanUpGeometry(_map, _settings.AutoClearSidedefTextures);
        _map.BuildIndexes();
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(result.StatusText);
    }

    private static GeometryCleanupResult CleanUpGeometry(MapSet map, bool autoClearSidedefTextures)
    {
        int repaired = map.RepairReferences();
        int sectors = map.RemoveUnusedSectors();
        int vertices = map.RemoveUnusedVertices();
        int sidedefTextures = map.RemoveUnneededSidedefTextures(autoClearSidedefTextures);
        return new GeometryCleanupResult(repaired, sectors, vertices, sidedefTextures);
    }

    // Reads the map's REJECT lump and opens a visibility relation summary for the selected sector.
    private void OnRejectViewer(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        var sel = _map.GetSelectedSectors();
        if (_wadPath is null || _mapMarker is null) { SetStatus("Reject viewer needs the source WAD."); return; }

        byte[]? bytes;
        using (var wad = new WAD(_wadPath, openreadonly: true)) bytes = WadMaps.ReadMapLump(wad, _mapMarker, "REJECT");
        var validation = RejectExplorerModel.Validate(bytes, _map.Sectors.Count);
        RejectTable? reject = validation.CanUse ? RejectTable.Parse(bytes ?? Array.Empty<byte>(), _map.Sectors.Count) : null;
        int? target = sel.Count == 1 ? sel[0].Index : null;

        var win = new RejectExplorerWindow(validation, reject, _map.Sectors.Count, target);
        win.SectorActivated += sectorIndex =>
        {
            SelectOneSector(sectorIndex);
            if (reject is { HasData: true })
                ApplyRejectOverlay(reject, sectorIndex);
        };
        win.SelectNoLineOfSightRequested += () =>
        {
            if (reject != null && target is int highlighted)
                SelectRejectedSectors(reject, highlighted);
        };
        win.ConfigureColorsRequested += async () =>
        {
            var dialog = new RejectExplorerColorDialog(_settings.RejectExplorerColors);
            if (!await dialog.ShowDialog<bool>(win)) return;
            _settings.RejectExplorerColors = dialog.ResultColors;
            SaveSettings();
            if (reject is { HasData: true })
                ApplyRejectOverlay(reject, target);
            SetStatus("Reject Explorer colors updated.");
        };
        win.Closed += (_, _) => MapView.SetRejectOverlayColors(null);
        win.Show(this);

        if (reject is { HasData: true } && target is int selectedTarget)
        {
            ApplyRejectOverlay(reject, selectedTarget);
            int count = SelectRejectedSectors(reject, selectedTarget);
            SetStatus(RejectExplorerModel.RejectedSectorsStatusText(count, selectedTarget));
        }
        else if (!validation.CanUse)
        {
            SetStatus($"Reject viewer: {validation.Status} REJECT lump ({validation.ActualBytes}/{validation.ExpectedBytes} bytes).");
        }
        else
        {
            SetStatus("Reject viewer opened. Select one sector before opening to highlight visibility relationships.");
        }
    }

    private void ApplyRejectOverlay(RejectTable reject, int? highlightedSector)
    {
        if (_map is null) return;
        int[] colors = RejectExplorerModel.SectorOverlayColors(reject, _map.Sectors.Count, highlightedSector, _settings.RejectExplorerColors);
        MapView.SetRejectOverlayColors(colors);
    }

    private int SelectRejectedSectors(RejectTable reject, int target)
    {
        if (_map is null) return 0;
        _map.ClearAllSelected();
        IReadOnlyList<int> sectors = RejectExplorerModel.RejectedSectorIndexes(reject, _map.Sectors.Count, target);
        foreach (int sectorIndex in sectors)
            _map.Sectors[sectorIndex].Selected = true;
        MapView.RevealSelection(MapControl.EditMode.Sectors, null);
        UpdateInfo();
        return sectors.Count;
    }

    private void SelectOneSector(int sectorIndex)
    {
        if (_map is null || (uint)sectorIndex >= (uint)_map.Sectors.Count) return;
        _map.ClearAllSelected();
        _map.Sectors[sectorIndex].Selected = true;
        MapView.RevealSelection(MapControl.EditMode.Sectors, null);
        UpdateInfo();
        SetStatus($"Selected sector {sectorIndex}.");
    }

    // Bakes Plane_Align (181) linedef specials into sector floor/ceiling slope planes so 3D shows them, undoable.
    private void OnApplySlopes(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) return;
        CreateUndo("Apply slopes");
        int n = SlopeEffects.ApplyAll(_map);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(SlopeEffects.ApplyStatusText(n));
    }

    private async void OnSectorColor(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) { SetStatus("No map loaded."); return; }
        if (!ColorPickerModel.CanEditSectorColors(_mapFormat == MapFormat.Udmf))
        {
            SetStatus(ColorPickerModel.SectorColorsRequireUdmfWarning);
            return;
        }

        var sectors = _map.GetSelectedSectors();
        if (sectors.Count == 0)
        {
            SetStatus(ColorPickerModel.NoSelectedSectorsWarning);
            return;
        }

        var dlg = new SectorColorDialog(sectors[0], SectorColorField.LightColor, sectors.Count);
        if (!await dlg.ShowDialog<bool>(this))
        {
            MapView.Focus();
            return;
        }

        CreateUndo("Set sector color");
        ColorPickerModel.ApplySectorColorEdit(sectors, dlg.ResultField, dlg.ResultColor, dlg.ResultRemoveDefaults);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(ColorPickerModel.SectorColorAppliedStatusText(dlg.ResultField, sectors.Count, dlg.ResultColor));
        MapView.Focus();
    }

    private async void OnDynamicLightColor(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) { SetStatus("No map loaded."); return; }
        var targets = ColorPickerModel.InternalDynamicLightEditTargets(_map.GetSelectedThings());
        if (targets.Count == 0)
        {
            SetStatus(ColorPickerModel.NoDynamicLightsWarning);
            return;
        }

        DynamicLightEditTarget reference = targets[0].EditTarget;
        DynamicLightPickerState state = ColorPickerModel.CreateDynamicLightPickerState(
            reference.Definition,
            reference.Args,
            reference.AngleDoom,
            reference.Fields,
            relativeMode: false);
        DynamicLightSliderPresentation presentation = ColorPickerModel.DynamicLightSliderPresentationFor(
            reference.Definition,
            ArgTitles(reference.Definition.LightNumber));
        var dlg = new DynamicLightDialog(targets.Count, state, presentation);
        if (!await dlg.ShowDialog<bool>(this))
        {
            MapView.Focus();
            return;
        }

        IReadOnlyList<DynamicLightEditTarget> editTargets = targets.Select(t => t.EditTarget).ToList();
        IReadOnlyList<DynamicLightPickerState>? fixedValues = dlg.ResultRelativeMode
            ? ColorPickerModel.CaptureDynamicLightFixedValues(editTargets)
            : null;
        IReadOnlyList<DynamicLightMutation> mutations = ColorPickerModel.SetDynamicLightSelection(
            editTargets,
            dlg.ResultColor,
            dlg.ResultPrimaryRadius,
            dlg.ResultSecondaryRadius,
            dlg.ResultInterval,
            dlg.ResultRelativeMode,
            fixedValues);

        CreateUndo("Set light color");
        ColorPickerModel.ApplyDynamicLightMutations(targets, mutations);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(ColorPickerModel.DynamicLightColorAppliedStatusText(targets.Count, dlg.ResultColor));
        MapView.Focus();
    }

    private IReadOnlyList<string> ArgTitles(int thingType)
    {
        ThingTypeInfo? info = _config?.GetThing(thingType);
        if (info == null) return Array.Empty<string>();
        return info.Args.Select(arg => arg.Title).ToList();
    }

    private void OnToggleAutomapSecretLine(object? sender, RoutedEventArgs e)
        => ToggleSelectedAutomapLines("Toggle automap secret", AutomapModeModel.ToggleSecretFlag, "automap secret");

    private void OnToggleAutomapHiddenLine(object? sender, RoutedEventArgs e)
        => ToggleSelectedAutomapLines("Toggle automap hidden", AutomapModeModel.ToggleHiddenFlag, "automap hidden");

    private void OnToggleAutomapTexturedHiddenSector(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) { SetStatus("No map loaded."); return; }
        var sectors = _map.GetSelectedSectors();
        if (sectors.Count == 0)
        {
            SetStatus("Select one or more sectors to toggle textured automap visibility.");
            return;
        }

        CreateUndo("Toggle textured automap hidden");
        foreach (var sector in sectors) AutomapModeModel.ToggleTexturedAutomapHiddenFlag(sector);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(AutomapModeModel.ToggleTexturedHiddenSectorStatusText(sectors.Count));
        MapView.Focus();
    }

    private void ToggleSelectedAutomapLines(string undoDescription, Action<Linedef, bool> toggle, string label)
    {
        if (_map is null || _undo is null) { SetStatus("No map loaded."); return; }
        var lines = _map.GetSelectedLinedefs();
        if (lines.Count == 0)
        {
            SetStatus($"Select one or more linedefs to toggle {label}.");
            return;
        }

        CreateUndo(undoDescription);
        bool isUdmf = _mapFormat == MapFormat.Udmf;
        foreach (var line in lines) toggle(line, isUdmf);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(AutomapModeModel.ToggleLineFlagStatusText(label, lines.Count));
        MapView.Focus();
    }

    private async void OnTagRange(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) { SetStatus("No map loaded."); return; }
        TagRangeTargetKind target = DefaultTagRangeTarget();
        int selected = TagRangeModel.SelectedInitialTags(_map, target).Count;
        if (selected == 0)
        {
            SetStatus("Select one or more sectors, linedefs, or things first.");
            return;
        }

        int startTag = ConfiguredTagSearch.NextFreeTag(_map, _config);
        var dlg = new TagRangeDialog(
            target,
            startTag,
            _settings.NormalizedTagRangeSettings,
            TagRangeModel.CreateSelectionContext(_map));
        if (!await dlg.ShowDialog<bool>(this)) return;
        _settings.TagRangeSettings = TagRangeModel.StoredOptionsFrom(dlg.ResultOptions);
        SaveSettings();

        IReadOnlyList<int> initialTags = TagRangeModel.SelectedInitialTags(_map, dlg.ResultTarget);
        if (initialTags.Count == 0)
        {
            SetStatus(TagRangeModel.EmptySelectionStatus(dlg.ResultTarget));
            return;
        }

        HashSet<int> usedTags = TagRangeModel.CollectUsedTags(_map);
        TagRangeResult result = TagRangeModel.CreateRange(initialTags, usedTags, dlg.ResultOptions);
        if (result.OutOfTags)
        {
            SetStatus(TagRangeModel.OutOfTagsStatus(result.Tags.Count));
            return;
        }

        CreateUndo("Tag range");
        int applied = TagRangeModel.ApplyRange(_map, dlg.ResultTarget, result.Tags);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(TagRangeModel.AppliedStatus(applied, result.TagsUsed));
    }

    private TagRangeTargetKind DefaultTagRangeTarget()
    {
        if (MapView.CurrentEditMode == MapControl.EditMode.Sectors && _map?.SelectedSectorsCount > 0)
            return TagRangeTargetKind.Sectors;
        if (MapView.CurrentEditMode == MapControl.EditMode.Things && _map?.SelectedThingsCount > 0)
            return TagRangeTargetKind.Things;
        if (MapView.CurrentEditMode == MapControl.EditMode.Linedefs && _map?.SelectedLinedefsCount > 0)
            return TagRangeTargetKind.Linedefs;
        if (_map?.SelectedSectorsCount > 0) return TagRangeTargetKind.Sectors;
        if (_map?.SelectedLinedefsCount > 0) return TagRangeTargetKind.Linedefs;
        return TagRangeTargetKind.Things;
    }

    private async void OnImportObjTerrain(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) { SetStatus("No map loaded."); return; }
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider == null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import OBJ Terrain",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Wavefront OBJ") { Patterns = new[] { "*.obj" } },
            },
        });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path) return;

        try
        {
            string text = System.IO.File.ReadAllText(path);
            ObjTerrainParseResult parsed = ObjTerrainImporter.Parse(text, axis: ObjTerrainUpAxis.Z);
            if (!parsed.Success)
            {
                SetStatus("OBJ terrain import failed: " + parsed.Errors[0]);
                return;
            }
            if (parsed.Geometry.Faces.Count == 0)
            {
                SetStatus("OBJ terrain import found no usable triangular faces.");
                return;
            }

            CreateUndo("Import OBJ terrain");
            _map.ClearAllSelected();
            ObjTerrainImportResult result = ObjTerrainImporter.BuildMapGeometry(
                _map,
                parsed.Geometry,
                BuildObjTerrainImportOptions());
            MapView.MarkGeometryDirty();
            MapView.RevealSelection(MapControl.EditMode.Sectors, null);
            UpdateInfo();
            MapView.Focus();
            SetStatus($"Imported OBJ terrain: {result.SectorsCreated} sectors, {result.LinedefsCreated} lines, {result.VerticesCreated} vertices.");
        }
        catch (Exception ex)
        {
            LogAndSetStatus(ex, "OBJ terrain import failed");
        }
    }

    private ObjTerrainImportOptions BuildObjTerrainImportOptions()
    {
        int brightness = _mapOptions?.OverrideBrightness == true ? _mapOptions.CustomBrightness : 160;
        return new ObjTerrainImportOptions(
            DefaultBrightness: brightness,
            DefaultFloorTexture: FirstNonBlankOr("FLOOR0_1", _mapOptions?.DefaultFloorTexture ?? "", _config?.DefaultFloorTexture ?? ""),
            DefaultCeilingTexture: FirstNonBlankOr("F_SKY1", _mapOptions?.DefaultCeilingTexture ?? "", _config?.DefaultCeilingTexture ?? ""),
            DefaultWallTexture: FirstNonBlankOr("STARTAN3", _mapOptions?.DefaultWallTexture ?? "", _config?.DefaultWallTexture ?? ""),
            UseVertexHeights: _config?.VertexHeightSupport == true);
    }

    private void OnApplySlopeArch(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) { SetStatus("No map loaded."); return; }
        var sectors = _map.GetSelectedSectors();
        if (sectors.Count == 0) { SetStatus("Select one or more sectors to slope-arch."); return; }
        if (!TrySelectedSectorBounds(sectors, out double minX, out double minY, out double maxX, out double maxY))
        {
            SetStatus("Selected sectors have no linedef bounds.");
            return;
        }
        if (maxX - minX <= 0.0)
        {
            SetStatus("Selected sectors need horizontal span for slope arch.");
            return;
        }

        double centerY = (minY + maxY) * 0.5;
        var options = new SlopeArchOptions
        {
            Theta = Angle2D.PIHALF,
            OffsetAngle = 0.0,
            BaseHeight = sectors[0].FloorHeight,
        };

        CreateUndo("Apply slope arch");
        int n = SlopeArchTool.Apply(sectors, new Vector2D(minX, centerY), new Vector2D(maxX, centerY), options);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(SlopeArchTool.ApplyStatusText(n));
    }

    private static bool TrySelectedSectorBounds(IReadOnlyList<Sector> sectors, out double minX, out double minY, out double maxX, out double maxY)
    {
        minX = minY = double.PositiveInfinity;
        maxX = maxY = double.NegativeInfinity;
        bool found = false;

        foreach (Sector sector in sectors)
        {
            foreach (Sidedef side in sector.Sidedefs)
            {
                if (side.Line == null) continue;
                Vector2D start = side.Line.Start.Position;
                minX = Math.Min(minX, start.x);
                minY = Math.Min(minY, start.y);
                maxX = Math.Max(maxX, start.x);
                maxY = Math.Max(maxY, start.y);

                Vector2D end = side.Line.End.Position;
                minX = Math.Min(minX, end.x);
                minY = Math.Min(minY, end.y);
                maxX = Math.Max(maxX, end.x);
                maxY = Math.Max(maxY, end.y);
                found = true;
            }
        }

        return found;
    }

    // Builds a staircase from the selected sectors (stepped floor heights), undoable.
    private async void OnBuildStairs(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) return;
        var sel = _map.GetSelectedSectors();
        if (sel.Count < 2) { SetStatus("Select 2 or more sectors to build stairs."); return; }

        var dlg = new StairBuilderDialog(sel[0].FloorHeight, 8, sel[0].CeilHeight, 8, _settings.StairBuilderPrefabs);
        bool accepted = await dlg.ShowDialog<bool>(this);
        if (dlg.PrefabsChanged)
        {
            _settings.StairBuilderPrefabs = dlg.ResultPrefabs.ToList();
            SaveSettings();
        }
        if (!accepted) return;

        CreateUndo("Build stairs");
        int n = StairBuilder.Apply(sel, dlg.ResultFloorStart, dlg.ResultFloorStep,
            dlg.ResultApplyCeiling, dlg.ResultCeilingStart, dlg.ResultCeilingStep);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus($"Built stairs across {n} sectors (start {dlg.ResultFloorStart}, step {dlg.ResultFloorStep}).");
    }

    // Traces Doom-style sound propagation from the selected sector, or a leak path between two sectors.
    private void OnSoundPropagation(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        var sel = _map.GetSelectedSectors();
        if (sel.Count == 2) { ShowSoundLeakPath(sel[0], sel[1]); return; }
        if (sel.Count != 1) { SetStatus("Select one sector to trace sound propagation, or two sectors to find a sound leak path."); return; }

        bool udmf = _mapFormat == MapFormat.Udmf;
        var reach = SoundPropagation.Reachable(_map, sel[0], udmf: udmf);
        SoundPropagationModeModel model = SoundPropagation.BuildModeModel(_map, udmf: udmf);
        MapView.SetSectorOverlayColors(model.SectorOverlayColors(_map.Sectors, sel[0], _settings.SoundPropagationColors), 128);
        MapView.SetSoundLeakPath(null);
        _map.ClearAllSelected();
        foreach (Sector sector in reach.Keys) sector.Selected = true;
        MapView.RevealSelection(MapControl.EditMode.Sectors, null);
        UpdateInfo();
        SetStatus(SoundPropagation.SummarizeReachability(reach).StatusText);
    }

    private void ShowSoundLeakPath(Sector source, Sector destination)
    {
        if (_map is null) return;

        bool udmf = _mapFormat == MapFormat.Udmf;
        SoundPropagationModeModel model = SoundPropagation.BuildModeModel(_map, udmf: udmf);
        IReadOnlySet<Sector> sectors = model.GetLeakSearchSectors(source);
        MapView.SetSectorOverlayColors(model.SectorOverlayColors(_map.Sectors, source, _settings.SoundPropagationColors), 128);

        if (!sectors.Contains(destination))
        {
            MapView.SetSoundLeakPath(null);
            SetStatus("Sound can not travel between the two selected sectors.");
            return;
        }

        Vector2D sourcePosition = SoundPropagation.SectorCenter(source);
        Vector2D destinationPosition = SoundPropagation.SectorCenter(destination);
        SoundLeakPath? path = SoundPropagation.FindLeakPath(
            source,
            sourcePosition,
            destination,
            destinationPosition,
            sectors,
            udmf: udmf);

        MapView.SetSoundLeakPath(path);
        MapView.RevealSelection(MapControl.EditMode.Sectors, new Vector2D(
            (sourcePosition.x + destinationPosition.x) * 0.5,
            (sourcePosition.y + destinationPosition.y) * 0.5));
        UpdateInfo();
        SetStatus(path == null
            ? "No sound leak path found between the two selected sectors."
            : path.StatusText);
    }

    private void OnSoundEnvironments(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }

        _soundEnvironmentModel = SoundPropagation.BuildSoundEnvironmentModel(
            _map,
            colors: _settings.SoundPropagationColors,
            udmf: _mapFormat == MapFormat.Udmf);
        MapView.SetSectorOverlayColors(_soundEnvironmentModel.SectorOverlayColors(_map.Sectors, _settings.SoundPropagationColors), 128);
        MapView.SetSoundLeakPath(null);

        if (_soundEnvironments != null)
        {
            _soundEnvironments.SetModel(_soundEnvironmentModel);
            _soundEnvironments.Activate();
            SetStatus(SoundEnvironmentStatus(_soundEnvironmentModel));
            return;
        }

        var win = new SoundEnvironmentWindow(_soundEnvironmentModel, _mapFormat == MapFormat.Udmf);
        _soundEnvironments = win;
        win.Closed += (_, _) => _soundEnvironments = null;
        win.SelectionActivated += SelectSoundEnvironmentRow;
        win.Show(this);
        SetStatus(SoundEnvironmentStatus(_soundEnvironmentModel));
    }

    private void SelectSoundEnvironmentRow(SoundEnvironmentSelection selection)
    {
        if (_map is null) return;

        _map.ClearAllSelected();
        _soundEnvironmentModel ??= SoundPropagation.BuildSoundEnvironmentModel(
            _map,
            colors: _settings.SoundPropagationColors,
            udmf: _mapFormat == MapFormat.Udmf);
        MapView.SetSectorOverlayColors(
            _soundEnvironmentModel.SectorOverlayColors(_map.Sectors, _settings.SoundPropagationColors, selection.Environment),
            128);
        MapView.SetSoundLeakPath(null);

        if (selection.Thing != null)
        {
            selection.Thing.Selected = true;
            MapView.RevealSelection(MapControl.EditMode.Things, selection.Thing.Position);
            UpdateInfo();
            SetStatus($"Sound Environment: selected thing {_map.Things.IndexOf(selection.Thing)}.");
            return;
        }

        if (selection.Linedef != null)
        {
            selection.Linedef.Selected = true;
            Vector2D focus = (selection.Linedef.Start.Position + selection.Linedef.End.Position) * 0.5;
            MapView.RevealSelection(MapControl.EditMode.Linedefs, focus);
            UpdateInfo();
            SetStatus($"Sound Environment: selected linedef {_map.Linedefs.IndexOf(selection.Linedef)}.");
            return;
        }

        if (selection.Environment == null) return;
        foreach (Sector sector in selection.Environment.Sectors) sector.Selected = true;
        MapView.RevealSelection(MapControl.EditMode.Sectors, SoundEnvironmentFocus(selection.Environment));
        UpdateInfo();
        SetStatus($"Sound Environment: selected {selection.Environment.Name}.");
    }

    private static Vector2D SoundEnvironmentFocus(SoundEnvironmentInfo environment)
    {
        if (environment.Sectors.Count == 0) return new Vector2D(0, 0);
        double x = 0;
        double y = 0;
        foreach (Sector sector in environment.Sectors)
        {
            Vector2D center = SoundPropagation.SectorCenter(sector);
            x += center.x;
            y += center.y;
        }

        return new Vector2D(x / environment.Sectors.Count, y / environment.Sectors.Count);
    }

    private static string SoundEnvironmentStatus(SoundEnvironmentModeModel model)
        => "Sound Environments: " + model.SummaryText();

    private async void OnSoundPropagationColors(object? sender, RoutedEventArgs e)
    {
        var dialog = new SoundPropagationColorDialog(_settings.SoundPropagationColors);
        if (!await dialog.ShowDialog<bool>(this)) return;
        _settings.SoundPropagationColors = dialog.ResultColors;
        SaveSettings();
        SetStatus("Sound propagation colors updated.");
    }

    private void OnBuildBridge(object? sender, RoutedEventArgs e)
    {
        RunCursorEdit(MapView.BuildBridgeFromSelectedLinedefs());
        UpdateCommandAvailability();
    }

    private async void OnMakeDoor(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) { SetStatus("No map loaded."); return; }
        var sectors = _map.GetSelectedSectors();
        if (sectors.Count == 0) { SetStatus("Select one or more sectors to make doors."); return; }

        var defaults = new MakeDoorOptions
        {
            DoorTexture = _config?.MakeDoorDoor ?? "-",
            TrackTexture = _config?.MakeDoorTrack ?? "-",
            FloorTexture = MakeDoorTool.DefaultFloorTexture(sectors),
            CeilingTexture = _config?.MakeDoorCeiling ?? "-",
            Action = _config?.MakeDoorAction ?? 0,
            Activate = _config?.MakeDoorActivate ?? 0,
            Args = _config?.MakeDoorArgs.ToArray() ?? new int[5],
            Flags = _config?.MakeDoorFlags ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
        };

        var dlg = new MakeDoorDialog(defaults, _settings.NormalizedMakeDoorSettings, _resources);
        if (!await dlg.ShowDialog<bool>(this))
        {
            MapView.Focus();
            return;
        }

        MakeDoorOptions options = dlg.ResultOptions;
        if (string.IsNullOrWhiteSpace(options.DoorTexture))
        {
            SetStatus("Choose a door texture before making a door.");
            MapView.Focus();
            return;
        }

        _settings.MakeDoorSettings = MakeDoorSettingsFrom(options);
        SaveSettings();
        CreateUndo("Make door");
        MakeDoorResult result = MakeDoorTool.Apply(_map, sectors, options);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(result.StatusText);
        MapView.Focus();
    }

    private static MakeDoorModeSettings MakeDoorSettingsFrom(MakeDoorOptions options)
        => new(
            HasValues: true,
            DoorTexture: options.DoorTexture,
            TrackTexture: options.TrackTexture,
            CeilingTexture: options.CeilingTexture,
            FloorTexture: options.FloorTexture,
            ResetOffsets: options.ResetOffsets,
            ApplyActionSpecials: options.ApplyActionSpecials,
            ApplyTag: options.ApplyTag);

    private async void OnExportIdStudio(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }

        var dlg = new IdStudioExportDialog(DefaultIdStudioExportFormState());
        if (!await dlg.ShowDialog<bool>(this)) return;

        IdStudioExportOptions options = dlg.ResultOptions;
        IReadOnlyList<string> errors = ValidateIdStudioExportOptions(options);
        if (errors.Count > 0)
        {
            SetStatus("idStudio export blocked: " + string.Join(" ", errors));
            return;
        }

        if (options.ExportTextures && _resources is null)
        {
            SetStatus("idStudio export blocked: load resources before exporting textures.");
            return;
        }

        IdStudioExportSettings settings = IdStudioExportSettings.FromOptions(options);
        IdStudioExportPlan plan = IdStudioExportPlanner.CreatePlan(
            _map,
            settings,
            AllIdStudioTextures(flats: false),
            AllIdStudioTextures(flats: true),
            name => IdStudioTexture(name, flats: false),
            name => IdStudioTexture(name, flats: true),
            name => IdStudioDimensions(name, flats: false),
            name => IdStudioDimensions(name, flats: true),
            hasSkyFloor: sector => IsConfiguredSkyFlat(sector.FloorTexture),
            hasSkyCeiling: sector => IsConfiguredSkyFlat(sector.CeilTexture));

        try
        {
            IdStudioExportPlanner.WriteFiles(plan);
            SetStatus(plan.StatusText(settings.MapName));
        }
        catch (Exception ex)
        {
            LogAndSetStatus(ex, "idStudio export failed");
        }
    }

    private async void OnExportImage(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }

        ImageExportSectorSelection selection = ImageExportPlanner.SelectSectorsForExport(_map);
        if (!selection.CanExport)
        {
            SetStatus(selection.Warning ?? "Image export failed.");
            return;
        }

        var top = GetTopLevel(this);
        if (top is null) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Image PNG",
            SuggestedFileName = DefaultImageExportFileName(),
            DefaultExtension = "png",
            FileTypeChoices = new[] { new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } } },
        });
        if (file?.TryGetLocalPath() is not { } path) return;

        var dlg = new ImageExportDialog(DefaultImageExportOptions(path));
        if (!await dlg.ShowDialog<bool>(this)) return;

        ImageExportOptions options = dlg.ResultOptions;
        IReadOnlyList<string> errors = ValidateImageExportOptions(options);
        if (errors.Count > 0)
        {
            SetStatus("Image export blocked: " + string.Join(" ", errors));
            return;
        }

        ImageExportSettings settings = ImageExportSettings.FromOptions(options);
        try
        {
            IReadOnlyList<ImageExportImageFile> files = ImageExportRenderer.CreateImageFiles(
                selection.Sectors,
                settings,
                ImageExportFlat);
            ImageExportRenderer.WriteImageFiles(files);
            SetStatus(settings.ExportStatusText(files.Count));
        }
        catch (OutOfMemoryException)
        {
            ImageExportResultMessage message = ImageExportResultMessage.FromResult(ImageExportResult.OutOfMemory);
            SetStatus(message.Message);
        }
        catch (Exception ex)
        {
            LogAndSetStatus(ex, "Image export failed");
        }
    }

    private async void OnExportObject(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }

        WavefrontExportPreflight preflight = WavefrontExportPlanner.PrepareExportSelection(_map);
        if (!preflight.CanExport)
        {
            SetStatus(preflight.Warning ?? "Object export failed.");
            return;
        }

        var top = GetTopLevel(this);
        if (top is null) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = ObjectExportSettings.DialogTitle,
            SuggestedFileName = DefaultObjectExportFileName(),
            DefaultExtension = "obj",
            FileTypeChoices = new[] { new FilePickerFileType("Wavefront OBJ") { Patterns = new[] { "*.obj" } } },
        });
        if (file?.TryGetLocalPath() is not { } path) return;

        var dlg = new ObjectExportDialog(DefaultObjectExportOptions(path));
        if (!await dlg.ShowDialog<bool>(this)) return;

        ObjectExportOptions options = dlg.ResultOptions;
        IReadOnlyList<string> errors = ObjectExportSettings.Validate(options);
        if (errors.Count > 0)
        {
            SetStatus("Object export blocked: " + string.Join(" ", errors));
            return;
        }

        if (options.ExportTextures && _resources is null)
        {
            SetStatus("Object export blocked: load resources before exporting textures.");
            return;
        }

        ObjectExportSettings settings = ObjectExportSettings.FromOptions(options);
        string mapTitle = System.IO.Path.GetFileName(_wadPath ?? _pk3Path ?? "untitled");
        string levelName = _mapMarker ?? "MAP01";
        string productVersion = typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "";
        WavefrontExportSettings wavefront = ObjectExportWriter.CreateWavefrontExport(
            _map,
            preflight.Sectors,
            settings,
            mapTitle,
            levelName,
            productVersion);
        if (wavefront.Obj.Length == 0)
        {
            SetStatus("Object export failed: no geometry was generated.");
            return;
        }

        try
        {
            WavefrontExportPlanner.WriteFiles(WavefrontExportPlanner.CreateFilePlan(wavefront, mapTitle, levelName, productVersion));
            WavefrontImagePlan imagePlan = WavefrontExportPlanner.CreateImagePlan(
                wavefront,
                name => WavefrontImage(name, flats: false),
                name => WavefrontImage(name, flats: true));
            WavefrontExportPlanner.WriteImageFiles(imagePlan.Files);
            SetStatus(wavefront.ExportStatusText("object OBJ", imagePlan));
        }
        catch (Exception ex)
        {
            LogAndSetStatus(ex, "Object export failed");
        }
    }

    private async void OnExportWavefront(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }

        WavefrontExportPreflight preflight = WavefrontExportPlanner.PrepareExportSelection(_map);
        if (!preflight.CanExport)
        {
            SetStatus(preflight.Warning ?? "OBJ export failed.");
            return;
        }

        var top = GetTopLevel(this);
        if (top is null) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Wavefront OBJ",
            SuggestedFileName = DefaultWavefrontFileName(),
            DefaultExtension = "obj",
            FileTypeChoices = new[] { new FilePickerFileType("Wavefront OBJ") { Patterns = new[] { "*.obj" } } },
        });
        if (file?.TryGetLocalPath() is not { } path) return;

        var dlg = new WavefrontExportDialog(DefaultWavefrontExportOptions(path));
        if (!await dlg.ShowDialog<bool>(this)) return;

        WavefrontExportOptions options = dlg.ResultOptions;
        IReadOnlyList<string> errors = WavefrontExportValidation.Validate(options);
        if (errors.Count > 0)
        {
            SetStatus("Wavefront export blocked: " + string.Join(" ", errors));
            return;
        }

        if (options.ExportTextures && _resources is null)
        {
            SetStatus("Wavefront export blocked: load resources before exporting textures.");
            return;
        }

        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(options);
        string obj = WavefrontGeometryCollector.CreateObjFromMap(
            _map,
            preflight.Sectors,
            settings,
            System.IO.Path.GetFileName(_wadPath ?? _pk3Path ?? "untitled"),
            _mapMarker ?? "MAP01",
            typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "");
        if (obj.Length == 0)
        {
            SetStatus("Wavefront export failed: no geometry was generated.");
            return;
        }

        try
        {
            WavefrontExportPlanner.WriteFiles(WavefrontExportPlanner.CreateFilePlan(
                settings,
                System.IO.Path.GetFileName(_wadPath ?? _pk3Path ?? "untitled"),
                _mapMarker ?? "MAP01",
                typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? ""));
            WavefrontImagePlan imagePlan = WavefrontExportPlanner.CreateImagePlan(
                settings,
                name => WavefrontImage(name, flats: false),
                name => WavefrontImage(name, flats: true));
            WavefrontExportPlanner.WriteImageFiles(imagePlan.Files);
            SetStatus(settings.ExportStatusText("Wavefront OBJ", imagePlan));
        }
        catch (Exception ex)
        {
            LogAndSetStatus(ex, "Wavefront export failed");
        }
    }

    private ImageExportOptions DefaultImageExportOptions(string filePath)
        => new(
            filePath,
            Floor: true,
            Fullbright: true,
            ApplySectorColors: true,
            Brightmap: false,
            Transparency: false,
            Tiles: false,
            ScaleIndex: 0,
            ImageFormatIndex: 0,
            PixelFormatIndex: 0);

    private string DefaultImageExportFileName()
    {
        string baseName = System.IO.Path.GetFileNameWithoutExtension(_wadPath ?? _pk3Path ?? "map");
        string mapName = _mapMarker ?? "MAP01";
        return $"{baseName}_{mapName}.png";
    }

    private static IReadOnlyList<string> ValidateImageExportOptions(ImageExportOptions options)
    {
        var errors = new List<string>();
        string path = options.FilePath.Trim();
        string? directory = System.IO.Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(path)) errors.Add("Output path is required.");
        else if (string.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory)) errors.Add("Output directory does not exist.");
        if (!string.Equals(System.IO.Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
            errors.Add("Only PNG image export is currently supported.");
        return errors;
    }

    private ImageExportTextureData? ImageExportFlat(string name)
    {
        ImageData? image = _resources?.GetFlat(name);
        return image is null
            ? null
            : new ImageExportTextureData(image.Width, image.Height, image.Rgba, (float)Math.Max(0.0001, image.ScaleX));
    }

    private ObjectExportOptions DefaultObjectExportOptions(string filePath)
        => new(
            filePath,
            FixScale: false,
            ExportTextures: false);

    private string DefaultObjectExportFileName()
    {
        string baseName = System.IO.Path.GetFileNameWithoutExtension(_wadPath ?? _pk3Path ?? "map");
        string mapName = _mapMarker ?? "MAP01";
        return ObjectExportSettings.DefaultFileName(baseName, mapName) + ObjectExportSettings.DefaultExtension;
    }

    private WavefrontExportOptions DefaultWavefrontExportOptions(string filePath)
    {
        string directory = System.IO.Path.GetDirectoryName(filePath)
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string actorName = (_mapMarker ?? "MapModel").Replace("-", "", StringComparison.Ordinal);
        if (actorName.Length == 0 || char.IsDigit(actorName[0])) actorName = "MapModel";
        return new WavefrontExportOptions
        {
            FilePath = filePath,
            Scale = 1.0,
            ExportTextures = _resources is not null,
            ActorName = actorName,
            BasePath = directory,
            ActorPath = directory,
            ModelPath = directory,
            Sprite = "PLAY",
            GenerateCode = true,
            GenerateModeldef = true,
        };
    }

    private string DefaultWavefrontFileName()
    {
        string baseName = System.IO.Path.GetFileNameWithoutExtension(_wadPath ?? _pk3Path ?? "map");
        string mapName = _mapMarker ?? "MAP01";
        return $"{baseName}_{mapName}.obj";
    }

    private WavefrontImageData? WavefrontImage(string name, bool flats)
    {
        if (_resources is null) return null;
        ImageData? image = flats ? _resources.GetFlat(name) : _resources.GetWallTexture(name);
        return image is null
            ? null
            : new WavefrontImageData(image.Width, image.Height, WavefrontPngEncoder.EncodeRgba(image.Width, image.Height, image.Rgba));
    }

    private IdStudioExportFormState DefaultIdStudioExportFormState()
    {
        string mapFilePath = _wadPath ?? _pk3Path ?? "map.wad";
        string levelName = _mapMarker ?? "map01";
        int textureCount = _resources?.GetTextureNames().Count ?? 0;
        int flatCount = _resources?.GetFlatNames().Count ?? 0;
        return IdStudioExportFormState.FromMap(_map!, mapFilePath, levelName, textureCount, flatCount);
    }

    private static IReadOnlyList<string> ValidateIdStudioExportOptions(IdStudioExportOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ModPath)) errors.Add("Mod path is required.");
        else if (!System.IO.Directory.Exists(options.ModPath)) errors.Add("Mod path does not exist.");
        if (!IdStudioExportValidation.IsValidMapName(options.MapName))
            errors.Add("Map name must start with a lower-case letter and contain only lower-case letters, digits or underscores.");
        if (options.Downscale <= 0) errors.Add("Downscale must be greater than zero.");
        return errors;
    }

    private IEnumerable<IdStudioTextureImage> AllIdStudioTextures(bool flats)
    {
        if (_resources is null) yield break;
        IReadOnlyList<string> names = flats ? _resources.GetFlatNames() : _resources.GetTextureNames();
        foreach (string name in names)
            if (IdStudioTexture(name, flats) is { } image)
                yield return image;
    }

    private IdStudioTextureImage? IdStudioTexture(string name, bool flats)
    {
        if (_resources is null) return null;
        ImageData? image = flats ? _resources.GetFlat(name) : _resources.GetWallTexture(name);
        if (image is null) return null;

        var pixels = new IdStudioRgba[image.Width * image.Height];
        bool translucent = false;
        bool masked = false;
        for (int i = 0, p = 0; i < image.Rgba.Length; i += 4, p++)
        {
            byte alpha = image.Rgba[i + 3];
            if (alpha == 0) masked = true;
            else if (alpha < 255) translucent = true;
            pixels[p] = new IdStudioRgba(image.Rgba[i], image.Rgba[i + 1], image.Rgba[i + 2], alpha);
        }

        return new IdStudioTextureImage(name, IdStudioTextureExporter.EncodeTga(image.Width, image.Height, pixels), translucent, masked);
    }

    private IdStudioTextureDimensions IdStudioDimensions(string name, bool flats)
    {
        ImageData? image = flats ? _resources?.GetFlat(name) : _resources?.GetWallTexture(name);
        return image is null
            ? new IdStudioTextureDimensions(64, 64)
            : new IdStudioTextureDimensions(image.Width, image.Height);
    }

    private bool IsConfiguredSkyFlat(string? flat)
        => _config is not null
            && !string.IsNullOrWhiteSpace(flat)
            && string.Equals(flat, _config.SkyFlatName, StringComparison.OrdinalIgnoreCase);

    // Builds the resource/config-aware check context from the loaded resources and game config.
    private MapCheckContext BuildCheckContext()
    {
        Func<string, bool>? texExists = null, flatExists = null, isSkyFlat = null;
        Func<string, (int Width, int Height)?>? textureSize = null;
        if (_resources != null)
        {
            var resources = _resources;
            var texSet = new HashSet<string>(_resources.GetTextureNames(), StringComparer.OrdinalIgnoreCase);
            var flatSet = new HashSet<string>(_resources.GetFlatNames(), StringComparer.OrdinalIgnoreCase);
            texExists = n => texSet.Contains(n);
            textureSize = n => resources.GetWallTexture(n) is { } img ? (img.Width, img.Height) : null;
            flatExists = n => flatSet.Contains(n);
        }
        Func<int, bool>? thingKnown = null, actionKnown = null, sectorEffectKnown = null, actionRequiresUpperTexture = null, actionRequiresActivation = null;
        Func<int, string?>? thingObsoleteMessage = null;
        Func<int, int?>? thingErrorCheck = null;
        Func<int, int?>? thingBlocking = null;
        Func<int, int?>? thingHeight = null;
        Func<Thing, Thing, bool>? thingFlagsOverlap = null;
        Func<Thing, IReadOnlyList<string>>? thingUnusedWarnings = null;
        Func<int, string?>? linedefActionId = null, thingClassName = null;
        Func<int, SidedefPart, bool>? ignoreUnknownTexture = null;
        Func<int, ActionTextureCheckKind>? actionTextureChecks = null;
        Func<int, int[], IEnumerable<int>>? actionTextureSectorTags = null;
        IReadOnlySet<string>? triggerActivationFlags = null;
        if (_config != null)
        {
            thingKnown = n => _config.GetThing(n) != null;
            thingObsoleteMessage = n =>
            {
                var thing = _config.GetThing(n);
                return thing?.IsObsolete == true ? thing.ObsoleteMessage : null;
            };
            thingErrorCheck = n => _config.GetThing(n)?.ErrorCheck;
            thingBlocking = n => _config.GetThing(n)?.Blocking;
            thingHeight = n => _config.GetThing(n)?.Height;
            thingFlagsOverlap = (a, b) => ThingFlagsOverlap(_config, a, b);
            thingUnusedWarnings = t => CheckThingFlags(_config, t.UdmfFlags);
            linedefActionId = a => _config.GetLinedefAction(a)?.Id;
            thingClassName = n => _config.GetThing(n)?.ClassName;
            actionKnown = a => _config.GetLinedefAction(a) != null
                || _config.DescribeGeneralizedLinedef(a) != null
                || BoomGeneralized.IsGeneralized(a);
            sectorEffectKnown = e => _config.GetSectorEffect(e) != null || _config.IsGeneralizedSectorEffect(e);
            ignoreUnknownTexture = (a, part) =>
            {
                var exemptions = _config.GetLinedefAction(a)?.ErrorChecker;
                return part switch
                {
                    SidedefPart.Upper => exemptions?.IgnoreUpperTexture == true,
                    SidedefPart.Middle => exemptions?.IgnoreMiddleTexture == true,
                    SidedefPart.Lower => exemptions?.IgnoreLowerTexture == true,
                    _ => false,
                };
            };
            actionRequiresUpperTexture = a => _config.GetLinedefAction(a)?.ErrorChecker.RequiresUpperTexture == true;
            actionTextureChecks = a =>
            {
                var exemptions = _config.GetLinedefAction(a)?.ErrorChecker;
                if (exemptions == null) return ActionTextureCheckKind.None;

                var checks = ActionTextureCheckKind.None;
                if (exemptions.FloorLowerToLowest)
                    checks |= ActionTextureCheckKind.FloorLowerToLowest;
                if (exemptions.FloorRaiseToNextHigher)
                    checks |= ActionTextureCheckKind.FloorRaiseToNextHigher;
                if (exemptions.FloorRaiseToHighest)
                    checks |= ActionTextureCheckKind.FloorRaiseToHighest;
                return checks;
            };
            if (_mapFormat is MapFormat.Hexen or MapFormat.Udmf)
            {
                actionTextureSectorTags = (action, args) =>
                {
                    var actionArgs = _config.GetLinedefAction(action)?.Args;
                    if (actionArgs is not { Length: > 0 } || args.Length == 0)
                        return Array.Empty<int>();

                    return (UniversalType)actionArgs[0].Type == UniversalType.SectorTag && args[0] > 0
                        ? new[] { args[0] }
                        : Array.Empty<int>();
                };
            }
            actionRequiresActivation = a => _config.GetLinedefAction(a)?.RequiresActivation == true;
            isSkyFlat = n => string.Equals(n, _config.SkyFlatName, StringComparison.OrdinalIgnoreCase);
            triggerActivationFlags = _config.LinedefActivations
                .Where(a => a.IsTrigger)
                .Select(a => a.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        return new MapCheckContext
        {
            IsUdmf = _mapFormat == MapFormat.Udmf,
            TextureExists = texExists,
            TextureSize = textureSize,
            FlatExists = flatExists,
            IsSkyFlat = isSkyFlat,
            ThingTypeKnown = thingKnown,
            ThingTitle = type => _config?.ThingTitle(type) ?? type.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ThingObsoleteMessage = thingObsoleteMessage,
            ThingErrorCheck = thingErrorCheck,
            ThingBlocking = thingBlocking,
            ThingHeight = thingHeight,
            ThingFlagsOverlap = thingFlagsOverlap,
            ThingUnusedWarnings = thingUnusedWarnings,
            DefaultThingFlags = _config?.DefaultThingFlags ?? Array.Empty<string>(),
            LinedefActionId = linedefActionId,
            ThingClassName = thingClassName,
            ActionKnown = actionKnown,
            SectorEffectKnown = sectorEffectKnown,
            CheckThingActions = _mapFormat is MapFormat.Hexen or MapFormat.Udmf,
            IgnoreUnknownTexture = ignoreUnknownTexture,
            ActionRequiresUpperTexture = actionRequiresUpperTexture,
            ActionHasSkyTransferStaticInit = (action, args) =>
                string.Equals(_config?.GetLinedefAction(action)?.Id, "Static_Init", StringComparison.OrdinalIgnoreCase)
                && args.Length > 1
                && args[1] == 255,
            ActionTextureChecks = actionTextureChecks,
            ActionTextureSectorTags = actionTextureSectorTags,
            CheckThingActionTextures = _mapFormat is MapFormat.Hexen or MapFormat.Udmf,
            CheckThreeDFloorTextures = _config?.GetLinedefAction(ThreeDFloors.Sector3DFloorAction)?.Id == "Sector_Set3dFloor",
            ActionIsPlaneAlign = action => string.Equals(_config?.GetLinedefAction(action)?.Id, "plane_align", StringComparison.OrdinalIgnoreCase),
            ActionRequiresActivation = actionRequiresActivation,
            TriggerActivationFlags = triggerActivationFlags,
            CheckMissingActivations = _mapFormat == MapFormat.Udmf,
            CheckPolyobjects = _mapFormat is MapFormat.Hexen or MapFormat.Udmf,
            CheckScripts = _mapFormat is MapFormat.Hexen or MapFormat.Udmf,
            CheckNamedScripts = _mapFormat == MapFormat.Udmf,
            CheckTextureAlignment = true,
            DoubleSidedFlag = _config?.DoubleSidedFlag,
            ImpassableFlag = _config?.ImpassableFlag,
            SafeBoundary = _config?.SafeBoundary ?? 0,
        };
    }

    private static bool ThingFlagsOverlap(GameConfiguration config, Thing a, Thing b)
    {
        if (config.ThingFlagsCompare.Count == 0) return true;

        var results = new Dictionary<string, ThingFlagsCompareResult>(StringComparer.Ordinal);
        foreach (var group in config.ThingFlagsCompare.Values)
            results[group.Name] = CompareThingFlagGroup(config, group, a, b);

        foreach (var result in results.Values)
        {
            if (result.Result != 1) continue;
            foreach (string requiredGroup in result.RequiredGroups)
            {
                if (!results.TryGetValue(requiredGroup, out var required) || required.Result != 1)
                {
                    result.Result = 0;
                    break;
                }
            }

            if (result.Result != 1) continue;
            foreach (string ignoredGroup in result.IgnoredGroups)
                if (results.TryGetValue(ignoredGroup, out var ignored))
                    ignored.Result = 0;
        }

        int overlappingGroups = 0;
        int totalGroups = results.Count;
        foreach (var result in results.Values)
        {
            if (result.Result == 1) overlappingGroups++;
            else if (result.Result == 0) totalGroups--;
            else return false;
        }

        return totalGroups > 0 && overlappingGroups == totalGroups;
    }

    private sealed class ThingFlagsCompareResult
    {
        public int Result = -1;
        public HashSet<string> RequiredGroups { get; } = new(StringComparer.Ordinal);
        public HashSet<string> IgnoredGroups { get; } = new(StringComparer.Ordinal);
    }

    private static ThingFlagsCompareResult CompareThingFlagGroup(GameConfiguration config, ThingFlagsCompareGroupInfo group, Thing a, Thing b)
    {
        var result = new ThingFlagsCompareResult();
        foreach (var flag in group.Flags.Values)
        {
            if (flag.RequiredFlag.Length > 0)
            {
                var requiredFlag = FindThingFlagCompare(config, flag.RequiredFlag);
                if (requiredFlag == null || !CompareThingFlag(requiredFlag, a, b))
                {
                    result.Result = -1;
                    continue;
                }
            }

            bool overlaps = CompareThingFlag(flag, a, b);
            if (!overlaps && flag.IgnoreGroupWhenUnset) return new ThingFlagsCompareResult { Result = 0 };
            if (!overlaps) continue;

            result.Result = 1;
            foreach (string ignoredGroup in flag.IgnoredGroups)
                result.IgnoredGroups.Add(ignoredGroup);
            foreach (string requiredGroup in flag.RequiredGroups)
            {
                result.IgnoredGroups.Remove(requiredGroup);
                result.RequiredGroups.Add(requiredGroup);
            }
        }

        return result;
    }

    private static ThingFlagCompareInfo? FindThingFlagCompare(GameConfiguration config, string flag)
    {
        foreach (var group in config.ThingFlagsCompare.Values)
            if (group.Flags.TryGetValue(flag, out var info))
                return info;
        return null;
    }

    private static bool CompareThingFlag(ThingFlagCompareInfo flag, Thing a, Thing b)
    {
        bool aFlag = flag.Invert ? !a.IsFlagSet(flag.Flag) : a.IsFlagSet(flag.Flag);
        bool bFlag = flag.Invert ? !b.IsFlagSet(flag.Flag) : b.IsFlagSet(flag.Flag);
        if (!aFlag && !bFlag && flag.IgnoreGroupWhenUnset) return false;
        return string.Equals(flag.CompareMethod, "equal", StringComparison.OrdinalIgnoreCase)
            ? aFlag == bFlag
            : aFlag && bFlag;
    }

    private static IReadOnlyList<string> CheckThingFlags(GameConfiguration config, IReadOnlySet<string> activeFlags)
    {
        if (config.ThingFlagsCompare.Count == 0) return Array.Empty<string>();

        var flagsPerGroup = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var ignoredGroups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in config.ThingFlagsCompare.Values)
        {
            var groupFlags = new HashSet<string>(StringComparer.Ordinal);
            bool removeGroup = false;
            foreach (var flag in group.Flags.Values)
            {
                var requiredFlag = flag.RequiredFlag.Length == 0 ? null : FindThingFlagCompare(config, flag.RequiredFlag);
                bool requiredMatches = requiredFlag == null || IsThingFlagSet(activeFlags, requiredFlag.Flag, requiredFlag.Invert);
                if (IsThingFlagSet(activeFlags, flag.Flag, flag.Invert) && requiredMatches)
                {
                    groupFlags.Add(flag.Flag);
                    foreach (string ignoredGroup in flag.IgnoredGroups)
                        ignoredGroups.Add(ignoredGroup);
                }
                else if (flag.IgnoreGroupWhenUnset)
                {
                    removeGroup = true;
                    break;
                }
            }

            if (!removeGroup)
                flagsPerGroup[group.Name] = groupFlags;
        }

        foreach (var group in flagsPerGroup)
            foreach (string flag in group.Value)
                foreach (string requiredGroup in config.ThingFlagsCompare[group.Key].Flags[flag].RequiredGroups)
                    ignoredGroups.Remove(requiredGroup);

        foreach (string ignoredGroup in ignoredGroups)
            flagsPerGroup.Remove(ignoredGroup);

        var warnings = new List<string>();
        foreach (var group in flagsPerGroup)
        {
            if (group.Value.Count > 0 || config.ThingFlagsCompare[group.Key].IsOptional) continue;
            warnings.Add(group.Key switch
            {
                "skills" => "Thing is not used in any skill level.",
                "gamemodes" => "Thing is not used in any game mode.",
                "classes" => "Thing is not used by any class.",
                _ => $"At least one \"{group.Key}\" flag should be set.",
            });
        }

        return warnings;
    }

    private static bool IsThingFlagSet(IReadOnlySet<string> flags, string flag, bool invert)
    {
        bool result = flags.Contains(flag);
        return invert ? !result : result;
    }

    // ---- UI helpers ----

    private void SetStatus(string text)
    {
        StatusText.Text = text;
        _statusHistory.Add(text);
    }

    private string CurrentConfigLabel()
        => StatusBarModel.ConfigLabel(_configName, _config?.GameName);

    private void OnToggleInfoPanel(object? sender, RoutedEventArgs e)
    {
        bool visible = !InfoPanel.IsVisible;
        InfoPanel.IsVisible = visible;
        InfoPanelMenuItem.IsChecked = visible;
        SetStatus(visible ? "Info panel expanded." : "Info panel collapsed.");
    }

    private string CommandHint(string commandId) => EditorCommandCatalog.CommandHint(commandId, _shortcutBindings);

    private string CommandHints(params string[] commandIds) => EditorCommandCatalog.CommandHints(_shortcutBindings, commandIds);

    private void UpdateStatusDetails()
    {
        ConfigText.Text = StatusBarModel.ConfigText(_configName, _config?.GameName);
        ModeText.Text = StatusBarModel.ModeText(
            MapView.CurrentEditMode.ToString(),
            MapView.In3DMode,
            MapView.AutomapMode,
            MapView.WadAuthorMode,
            MapView.ImageExampleMode,
            MapView.InDrawMode);
        var grid = MapView.GridSetupSnapshot();
        GridText.Text = StatusBarModel.GridText(MapView.SnapToGridEnabled, grid.GridSizeF);
    }

    private void UpdateInfo()
    {
        UpdateCommandAvailability();
        if (_map is null) { ShowText(InfoSummaryPanelModel.NoMapLoadedText()); PreviewPanel.Children.Clear(); return; }
        InfoPanelSelectionCounts counts = InfoSummaryPanelModel.SelectionCounts(_map);
        int sv = counts.Vertices, sl = counts.Linedefs, sd = counts.Sidedefs, ss = counts.Sectors, st = counts.Things;
        UpdatePreviews(sv, sl, sd, ss, st);

        if (counts.Total == 0)
        {
            ShowText(InfoSummaryPanelModel.MapOverviewText(
                _map,
                _configName,
                MapView.CurrentEditMode.ToString(),
                CommandHints("map2d.mode-vertices", "map2d.mode-linedefs", "map2d.mode-sectors", "map2d.mode-things"),
                CommandHint("map2d.toggle-3d")));
            return;
        }

        // Detailed read-out for a single selected element (config-aware names); otherwise a counts summary.
        if (st == 1 && sl == 0 && sd == 0 && ss == 0 && sv == 0) ShowThingFields(_map.GetSelectedThings()[0]);
        else if (sl == 1 && st == 0 && sd == 0 && ss == 0 && sv == 0) ShowLinedefFields(_map.GetSelectedLinedefs()[0]);
        else if (sd == 1 && st == 0 && sl == 0 && ss == 0 && sv == 0) ShowSidedefFields(_map.GetSelectedSidedefs()[0]);
        else if (ss == 1 && st == 0 && sl == 0 && sd == 0 && sv == 0) ShowSectorFields(_map.GetSelectedSectors()[0]);
        else if (sv == 1 && st == 0 && sl == 0 && sd == 0 && ss == 0) ShowVertexFields(_map.GetSelectedVertices()[0]);
        else
        {
            ShowText(InfoSummaryPanelModel.SelectionSummaryText(
                counts,
                _undo?.CanUndo == true ? _undo.NextUndoDescription : _undo is null ? null : "-",
                _undo?.CanRedo == true ? _undo.NextRedoDescription : _undo is null ? null : "-"));
        }
    }

    private void UpdateCommandAvailability()
    {
        bool hasMap = _map is not null;
        bool hasArchive = _wadPath is not null || _pk3Maps is { Count: > 0 };
        bool hasResources = _resources is not null;
        bool canBrowseCatalogs = hasMap && _config is not null;
        bool canReloadResources = _wadPath is not null && _mapOptions is not null;
        bool canSave = hasMap && (_wadPath is null || FileSaveStamp.CanWriteExistingPath(_wadPath));
        bool hasSelection = hasMap && CountSelection() > 0;
        bool hasCurrentModeSelection = hasMap && CountSelectionInCurrentMode() > 0;
        bool hasSingleCurrentModeSelection = hasMap && CountSelectionInCurrentMode() == 1;
        bool canCopyProperties = hasMap;
        bool canPasteProperties = hasMap && MapView.HasCopiedPropertiesForCurrentMode;
        bool hasSelectedLinedef = _map?.SelectedLinedefsCount > 0;
        bool hasSelectedSector = _map?.SelectedSectorsCount > 0;
        bool hasSelectedThing = _map?.SelectedThingsCount > 0;
        bool hasSelectedInternalDynamicLight = _map is not null && ColorPickerModel.HasInternalDynamicLightSelection(_map.GetSelectedThings());
        bool canPlaceThings = hasMap && MapView.CurrentEditMode is MapControl.EditMode.Vertices or MapControl.EditMode.Linedefs or MapControl.EditMode.Sectors;
        bool canApplyLightFogFlag = hasMap && _mapFormat == MapFormat.Udmf;
        bool hasMultipleSelectedSectors = _map?.SelectedSectorsCount >= 2;
        bool hasSelectedUdmfLinedef = _mapFormat == MapFormat.Udmf && hasSelectedLinedef;
        bool hasGradientSectors = _map?.SelectedSectorsCount >= SectorGradient.MinimumSectorCount;
        bool hasGradientLinedefs = _mapFormat == MapFormat.Udmf && _map?.SelectedLinedefsCount >= LinedefGradient.MinimumLinedefCount;
        bool hasGradientTarget = hasGradientSectors || hasGradientLinedefs;
        bool hasTransformableSelection = _map is not null && (_map.SelectedGeometryVertices().Count > 0 || _map.SelectedThingsCount > 0);
        bool hasSelectedLinedefWithFront = _map?.Linedefs.Any(line => line.Selected && line.Front is not null) == true;
        bool supportsCustomFields = SupportsCustomFields();
        bool hasEditableProperties = _map is not null && EditorPropertySelection.CanEdit(
            _map.SelectedVerticesCount,
            _map.SelectedLinedefsCount,
            _map.SelectedSidedefsCount,
            _map.SelectedSectorsCount,
            _map.SelectedThingsCount);
        bool hasSingleFlagSelection = _map is not null && EditorPropertySelection.CanEditFlags(
            _map.SelectedVerticesCount,
            _map.SelectedLinedefsCount,
            _map.SelectedSidedefsCount,
            _map.SelectedSectorsCount,
            _map.SelectedThingsCount);
        bool hasCustomFieldSelection = _map is not null && EditorPropertySelection.CanEditCustomFields(
            supportsCustomFields,
            _map.SelectedVerticesCount,
            _map.SelectedLinedefsCount,
            _map.SelectedSidedefsCount,
            _map.SelectedSectorsCount,
            _map.SelectedThingsCount);
        bool canUndo = _undo?.CanUndo == true;
        bool canRedo = _undo?.CanRedo == true;
        bool canEditUsdf = hasMap && UsdfDialogueParser.CanEditDialogue(_config);
        bool canFilterThingCategories = hasMap && _config is { Things.Count: > 0 };
        bool canInsertPreviousPrefab = hasMap
            && !string.IsNullOrWhiteSpace(_lastPrefabPath)
            && System.IO.File.Exists(_lastPrefabPath);

        SetEnabled(hasArchive, OpenMapMenuItem, ReloadMapMenuItem, OpenMapButton, ReloadMapButton);
        SetEnabled(hasMap,
            CloseMapMenuItem, MapOptionsMenuItem, PrefabsMenuItem, PasteMenuItem, PasteSpecialMenuItem, SelectAllMenuItem, InvertSelectionMenuItem,
            StitchMenuItem, InsertPrefabMenuItem, FindReplaceMenuItem, TagsMenuItem,
            InsertAtCursorMenuItem, SelectSingleSidedMenuItem, SelectDoubleSidedMenuItem, ChangeMapElementIndexMenuItem, FlipLinedefsMenuItem, FlipSidedefsMenuItem, AlignLinedefsMenuItem, SplitLinedefsMenuItem,
            LowerFloor8MenuItem, RaiseFloor8MenuItem, LowerCeiling8MenuItem, RaiseCeiling8MenuItem, RaiseBrightness8MenuItem, LowerBrightness8MenuItem, VerticesModeMenuItem,
            LinedefsModeMenuItem, SectorsModeMenuItem, ThingsModeMenuItem, FitMenuItem,
            GoToCoordinatesMenuItem, AutomapModeMenuItem, WadAuthorModeMenuItem, VisplaneExplorerModeMenuItem, TagStatisticsMenuItem, TagExplorerMenuItem, ThingStatisticsMenuItem, UndoRedoPanelMenuItem, CommentsPanelMenuItem, ToggleCommentsMenuItem, NodesViewerMenuItem, Toggle3DModeMenuItem,
            MoveCameraToCursorMenuItem, ToggleFullBrightnessMenuItem, ToggleHighlightMenuItem, ViewModeWireframeMenuItem, ViewModeBrightnessMenuItem, ViewModeFloorsMenuItem, ViewModeCeilingsMenuItem, NextViewModeMenuItem, PreviousViewModeMenuItem,
            ModelRenderNoneMenuItem, ModelRenderSelectionMenuItem, ModelRenderActiveFilterMenuItem, ModelRenderAllMenuItem, NextModelRenderModeMenuItem,
            ToggleSectorFillsMenuItem, ToggleThingsMenuItem, ToggleThingArrowsMenuItem, ToggleFixedThingsScaleMenuItem, ToggleAlwaysShowVerticesMenuItem,
            Toggle3DFloorsMenuItem, ThingFilterMenuItem, GridSetupMenuItem, SmartGridTransformMenuItem, AlignGridToLinedefMenuItem, SetGridOriginToVertexMenuItem,
            ResetGridTransformMenuItem, ToggleSnapToGridMenuItem, ToggleDynamicGridSizeMenuItem, GridSizeDownMenuItem, GridSizeUpMenuItem, ToggleBlockmapMenuItem, ToggleNodesMenuItem,
            DrawMenuItem,
            MakeSectorAtCursorMenuItem, DrawSectorMenuItem, DrawLinesMenuItem, DrawCurveMenuItem,
            DrawRectangleMenuItem, DrawEllipseMenuItem, DrawGridMenuItem, CheckMapMenuItem, CleanUpGeometryMenuItem,
            TestMapMenuItem, SoundPropagationMenuItem, SoundEnvironmentsMenuItem, BlockmapExplorerMenuItem, BuildBridgeMenuItem, MakeDoorMenuItem, BuildStairsMenuItem, ApplySlopeArchMenuItem, ApplySlopesMenuItem, SectorColorMenuItem, DynamicLightColorMenuItem, TagRangeMenuItem, ImageExampleMenuItem, ImportObjTerrainMenuItem,
            ExportObjectMenuItem, ExportImageMenuItem, ExportWavefrontMenuItem, ExportIdStudioMenuItem, RejectViewerMenuItem, CloseMapButton, SaveAsMenuItem, SaveAsFormatMenuItem,
            FitButton, Toggle3DModeButton, VerticesModeButton, LinedefsModeButton,
            SectorsModeButton, ThingsModeButton, InsertAtCursorButton, MakeSectorAtCursorButton, DrawSectorButton,
            DrawLinesButton, DrawCurveButton, DrawRectangleButton, DrawEllipseButton, DrawGridButton, CheckMapButton,
            CleanUpGeometryButton, TestMapButton, BuildBridgeButton, MakeDoorButton, BuildStairsButton, ApplySlopeArchButton, ApplySlopesButton, SectorColorButton, DynamicLightColorButton, TagRangeButton, ImportObjTerrainButton, WadAuthorModeButton);
        SetEnabled(canSave, SaveMenuItem, SaveButton);
        SetEnabled(canInsertPreviousPrefab, InsertPreviousPrefabMenuItem);
        SetEnabled(canPlaceThings, PlaceThingsMenuItem);
        SetEnabled(canEditUsdf, UsdfConversationsMenuItem);
        SetEnabled(canFilterThingCategories, ThingFilterMenuItem);
        SetEnabled(canReloadResources, ReloadResourcesMenuItem, ReloadResourcesButton);
        SetEnabled(hasResources, BrowseWallTexturesMenuItem, BrowseFlatsMenuItem);
        SetEnabled(canBrowseCatalogs, BrowseThingsMenuItem, BrowseLinedefActionsMenuItem, BrowseSectorEffectsMenuItem);
        SetEnabled(hasSelection,
            CutMenuItem, CopyMenuItem, DuplicateMenuItem, DeleteMenuItem, SelectNoneMenuItem,
            SavePrefabMenuItem, DeleteButton);
        SetEnabled(canCopyProperties, CopyPropertiesMenuItem);
        SetEnabled(canPasteProperties, PastePropertiesMenuItem, PastePropertiesOptionsMenuItem);
        SetEnabled(hasCurrentModeSelection, SelectSimilarMenuItem);
        SetEnabled(hasSingleCurrentModeSelection, ChangeMapElementIndexMenuItem);
        SetEnabled(canApplyLightFogFlag, ApplyLightFogFlagMenuItem);
        SetEnabled(hasTransformableSelection,
            TransformSelectionMenuItem,
            FlipHorizontalMenuItem, FlipVerticalMenuItem, RotateCwMenuItem, RotateCcwMenuItem,
            ScaleUpMenuItem, ScaleDownMenuItem);
        SetEnabled(hasSelectedLinedefWithFront, AlignTexturesMenuItem, AlignHorizontalMenuItem, AlignVerticalMenuItem, FitSelectedTexturesMenuItem);
        SetEnabled(hasSelectedThing, AlignThingsToWallMenuItem, FilterSelectedThingsMenuItem);
        SetEnabled(hasSelectedInternalDynamicLight, DynamicLightColorMenuItem, DynamicLightColorButton);
        SetEnabled(hasSelectedUdmfLinedef,
            AlignTexturesMenuItem, AlignFloorToFrontMenuItem, AlignFloorToBackMenuItem, AlignCeilingToFrontMenuItem, AlignCeilingToBackMenuItem);
        SetEnabled(hasSelectedLinedef, ToggleAutomapSecretLineMenuItem, ToggleAutomapHiddenLineMenuItem);
        SetEnabled(hasSelectedSector && hasResources, BrowseFloorFlatsMenuItem, BrowseCeilingFlatsMenuItem);
        SetEnabled(hasGradientSectors,
            SectorGradientsMenuItem, GradientFloorHeightsMenuItem, GradientCeilingHeightsMenuItem, GradientBrightnessMenuItem,
            GradientFloorLightMenuItem, GradientCeilingLightMenuItem, GradientLightColorMenuItem, GradientFadeColorMenuItem,
            GradientLightAndFadeColorMenuItem);
        SetEnabled(hasGradientLinedefs, LinedefGradientsMenuItem, GradientLinedefBrightnessMenuItem);
        SetEnabled(hasGradientTarget,
            GradientInterpolationMenuItem, GradientInterpolationLinearMenuItem, GradientInterpolationEaseInOutSineMenuItem,
            GradientInterpolationEaseInSineMenuItem, GradientInterpolationEaseOutSineMenuItem);
        SetEnabled(hasSelectedSector, ToggleAutomapTexturedHiddenSectorMenuItem);
        SetEnabled(hasMultipleSelectedSectors, JoinSectorsMenuItem, MergeSectorsMenuItem);
        SetEnabled(hasEditableProperties, PropertiesMenuItem);
        SetEnabled(hasSingleFlagSelection, FlagsMenuItem);
        SetEnabled(hasCustomFieldSelection, CustomFieldsMenuItem);
        UpdateUndoRedoLabels();
        SetEnabled(canUndo, UndoMenuItem, UndoButton);
        SetEnabled(canRedo, RedoMenuItem, RedoButton);
        UpdateCommandCheckedState();
    }

    private void UpdateUndoRedoLabels()
    {
        string? undo = _undo?.NextUndoDescription;
        string? redo = _undo?.NextRedoDescription;
        UndoMenuItem.Header = string.IsNullOrWhiteSpace(undo) ? "_Undo" : $"_Undo {undo}";
        RedoMenuItem.Header = string.IsNullOrWhiteSpace(redo) ? "_Redo" : $"_Redo {redo}";
        ToolTip.SetTip(UndoButton, string.IsNullOrWhiteSpace(undo) ? "Undo" : $"Undo {undo}");
        ToolTip.SetTip(RedoButton, string.IsNullOrWhiteSpace(redo) ? "Redo" : $"Redo {redo}");
    }

    private bool SupportsCustomFields()
        => _config?.HasCustomFields == true || _mapFormat == MapFormat.Udmf;

    private void UpdateCommandCheckedState()
    {
        bool verticesMode = MapView.CurrentEditMode == MapControl.EditMode.Vertices && !MapView.In3DMode && !MapView.AutomapMode && !MapView.WadAuthorMode;
        bool linedefsMode = MapView.CurrentEditMode == MapControl.EditMode.Linedefs && !MapView.In3DMode && !MapView.AutomapMode && !MapView.WadAuthorMode;
        bool sectorsMode = MapView.CurrentEditMode == MapControl.EditMode.Sectors && !MapView.In3DMode && !MapView.AutomapMode && !MapView.WadAuthorMode;
        bool thingsMode = MapView.CurrentEditMode == MapControl.EditMode.Things && !MapView.In3DMode && !MapView.AutomapMode && !MapView.WadAuthorMode;
        bool drawSector = MapView.DrawMode && !MapView.DrawLinesOnly && !MapView.DrawCurve;
        bool drawLines = MapView.DrawMode && MapView.DrawLinesOnly && !MapView.DrawCurve;
        bool drawCurve = MapView.DrawMode && MapView.DrawCurve;
        bool drawRectangle = MapView.CurrentShape == MapControl.ShapeKind.Rectangle;
        bool drawEllipse = MapView.CurrentShape == MapControl.ShapeKind.Ellipse;
        bool drawGrid = MapView.CurrentShape == MapControl.ShapeKind.Grid;

        SetChecked(AutoClearSidedefTexturesMenuItem, _settings.AutoClearSidedefTextures);
        SetChecked(InfoPanelMenuItem, InfoPanel.IsVisible);
        SetChecked(VerticesModeMenuItem, verticesMode);
        SetChecked(LinedefsModeMenuItem, linedefsMode);
        SetChecked(SectorsModeMenuItem, sectorsMode);
        SetChecked(ThingsModeMenuItem, thingsMode);
        SetChecked(Toggle3DModeMenuItem, MapView.In3DMode);
        SetChecked(AutomapModeMenuItem, MapView.AutomapMode);
        SetChecked(WadAuthorModeMenuItem, MapView.WadAuthorMode);
        SetChecked(ToggleSectorFillsMenuItem, MapView.ShowSectorFills);
        SetChecked(ToggleThingsMenuItem, MapView.ShowThings);
        SetChecked(ToggleThingArrowsMenuItem, MapView.ThingArrows);
        SetChecked(ToggleCommentsMenuItem, MapView.RenderComments);
        SetChecked(ToggleFixedThingsScaleMenuItem, MapView.FixedThingsScale);
        SetChecked(ToggleAlwaysShowVerticesMenuItem, MapView.AlwaysShowVertices);
        SetChecked(ToggleFullBrightnessMenuItem, MapView.FullBrightness);
        SetChecked(ToggleHighlightMenuItem, MapView.UseHighlight);
        SetChecked(ModelRenderNoneMenuItem, MapView.ModelRenderMode == ThingModelRenderMode.None);
        SetChecked(ModelRenderSelectionMenuItem, MapView.ModelRenderMode == ThingModelRenderMode.Selection);
        SetChecked(ModelRenderActiveFilterMenuItem, MapView.ModelRenderMode == ThingModelRenderMode.ActiveThingsFilter);
        SetChecked(ModelRenderAllMenuItem, MapView.ModelRenderMode == ThingModelRenderMode.All);
        SetChecked(ViewModeWireframeMenuItem, MapView.ViewMode2D == MapControl.ClassicViewMode.Wireframe);
        SetChecked(ViewModeBrightnessMenuItem, MapView.ViewMode2D == MapControl.ClassicViewMode.Brightness);
        SetChecked(ViewModeFloorsMenuItem, MapView.ViewMode2D == MapControl.ClassicViewMode.FloorTextures);
        SetChecked(ViewModeCeilingsMenuItem, MapView.ViewMode2D == MapControl.ClassicViewMode.CeilingTextures);
        SetChecked(Toggle3DFloorsMenuItem, MapView.Show3DFloors);
        SetChecked(ToggleSnapToGridMenuItem, MapView.SnapToGridEnabled);
        SetChecked(ToggleDynamicGridSizeMenuItem, MapView.DynamicGridSizeEnabled);
        SetChecked(ToggleBlockmapMenuItem, MapView.ShowBlockmap);
        SetChecked(ToggleNodesMenuItem, MapView.ShowNodes);
        SetChecked(ImageExampleMenuItem, MapView.ImageExampleMode);
        SetChecked(DrawSectorMenuItem, drawSector);
        SetChecked(DrawLinesMenuItem, drawLines);
        SetChecked(DrawCurveMenuItem, drawCurve);
        SetChecked(DrawRectangleMenuItem, drawRectangle);
        SetChecked(DrawEllipseMenuItem, drawEllipse);
        SetChecked(DrawGridMenuItem, drawGrid);
        SetChecked(GradientInterpolationLinearMenuItem, _gradientInterpolationMode == InterpolationTools.Mode.LINEAR);
        SetChecked(GradientInterpolationEaseInOutSineMenuItem, _gradientInterpolationMode == InterpolationTools.Mode.EASE_IN_OUT_SINE);
        SetChecked(GradientInterpolationEaseInSineMenuItem, _gradientInterpolationMode == InterpolationTools.Mode.EASE_IN_SINE);
        SetChecked(GradientInterpolationEaseOutSineMenuItem, _gradientInterpolationMode == InterpolationTools.Mode.EASE_OUT_SINE);
        SetActiveClass(Toggle3DModeButton, MapView.In3DMode);
        SetActiveClass(WadAuthorModeButton, MapView.WadAuthorMode);
        SetActiveClass(VerticesModeButton, verticesMode);
        SetActiveClass(LinedefsModeButton, linedefsMode);
        SetActiveClass(SectorsModeButton, sectorsMode);
        SetActiveClass(ThingsModeButton, thingsMode);
        SetActiveClass(DrawSectorButton, drawSector);
        SetActiveClass(DrawLinesButton, drawLines);
        SetActiveClass(DrawCurveButton, drawCurve);
        SetActiveClass(DrawRectangleButton, drawRectangle);
        SetActiveClass(DrawEllipseButton, drawEllipse);
        SetActiveClass(DrawGridButton, drawGrid);
        UpdateAutomapOptionControls();
    }

    private static void SetEnabled(bool enabled, params Control[] controls)
    {
        foreach (var control in controls) control.IsEnabled = enabled;
    }

    private static void SetChecked(MenuItem item, bool isChecked) => item.IsChecked = isChecked;

    private static void SetActiveClass(Control control, bool active) => control.Classes.Set("active", active);

    private void OnToggleAutoClearSidedefTextures(object? sender, RoutedEventArgs e)
    {
        _settings.AutoClearSidedefTextures = !_settings.AutoClearSidedefTextures;
        SaveSettings();
        UpdateCommandCheckedState();
        SetStatus("Auto removal of unused sidedef textures is " + (_settings.AutoClearSidedefTextures ? "ENABLED" : "DISABLED"));
    }

    private bool HasArgs => _mapFormat != MapFormat.Doom;

    private void ShowVertexFields(Vertex v)
    {
        VertexInfoPanelState state = VertexInfoPanelModel.Build(_map!, v);
        ShowFields(state.Header, state.Fields.Select(field => (field.Label, field.Value)).ToList());
    }

    private void ShowThingFields(Thing t)
    {
        ThingInfoPanelState state = ThingInfoPanelModel.Build(_map!, t, _config, HasArgs);
        ShowFields(state.Header, state.Fields.Select(field => (field.Label, field.Value)).ToList());
    }

    private void ShowLinedefFields(Linedef l)
    {
        LinedefInfoPanelState state = LinedefInfoPanelModel.Build(_map!, l, _config, HasArgs);
        ShowFields(state.Header, state.Fields.Select(field => (field.Label, field.Value)).ToList());
    }

    private void ShowSidedefFields(Sidedef side)
    {
        SidedefInfoPanelState state = SidedefInfoPanelModel.Build(_map!, side);
        ShowFields(state.Header, state.Fields.Select(field => (field.Label, field.Value)).ToList());
    }

    private void ShowSectorFields(Sector s)
    {
        SectorInfoPanelState state = SectorInfoPanelModel.Build(s, _config);
        ShowFields(state.Header, state.Fields.Select(field => (field.Label, field.Value)).ToList());
    }

    // Shows free text (help / multi-selection) and hides the structured field grid.
    private void ShowText(string text)
    {
        InfoText.Text = text;
        InfoText.IsVisible = true;
        InfoFields.IsVisible = false;
        InfoFields.Children.Clear();
    }

    // Number of label/value pairs per row in the field grid (columns align across rows for easy scanning).
    private const int FieldPairsPerRow = 3;
    private static readonly IBrush FieldLabelBrush = new SolidColorBrush(Color.FromRgb(0x8a, 0x93, 0x9e));
    private static readonly IBrush FieldValueBrush = new SolidColorBrush(Color.FromRgb(0xe0, 0xe6, 0xee));

    // Shows a header + an aligned grid of label/value pairs and hides the free-text block.
    private void ShowFields(string header, List<(string Label, string Value)> fields)
    {
        InfoFields.Children.Clear();
        InfoFields.Children.Add(new TextBlock
        {
            Text = header, Foreground = Brushes.LightSkyBlue, FontWeight = FontWeight.Bold, FontSize = 12,
            Margin = new Thickness(0, 0, 0, 4),
        });

        // Each pair is a label column (auto) + a value column (fixed) so values line up down each column.
        var grid = new Grid { RowSpacing = 3, ColumnSpacing = 8 };
        for (int p = 0; p < FieldPairsPerRow; p++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));      // label
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(120)));  // value
        }
        int rows = (fields.Count + FieldPairsPerRow - 1) / FieldPairsPerRow;
        for (int r = 0; r < rows; r++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int i = 0; i < fields.Count; i++)
        {
            int pair = i % FieldPairsPerRow, row = i / FieldPairsPerRow;
            var label = new TextBlock
            {
                Text = fields[i].Label, Foreground = FieldLabelBrush, FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(pair == 0 ? 0 : 8, 0, 0, 0),
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, pair * 2);
            grid.Children.Add(label);

            var value = new TextBlock
            {
                Text = fields[i].Value, Foreground = FieldValueBrush, FontSize = 11, FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetRow(value, row);
            Grid.SetColumn(value, pair * 2 + 1);
            grid.Children.Add(value);
        }

        InfoFields.Children.Add(grid);
        InfoText.IsVisible = false;
        InfoFields.IsVisible = true;
    }

    // Shows texture/sprite thumbnails for a single selected element (sidedef textures, sector flats, thing sprite).
    private void UpdatePreviews(int sv, int sl, int sd, int ss, int st)
    {
        PreviewPanel.Children.Clear();
        if (_map is null || _resources is null) return;

        if (sl == 1 && sd == 0 && st == 0 && ss == 0 && sv == 0)
        {
            var l = _map.GetSelectedLinedefs()[0];
            if (l.Front is { } f) PreviewPanel.Children.Add(SidePreviews("Front", f));
            if (l.Back is { } b) PreviewPanel.Children.Add(SidePreviews("Back", b));
        }
        else if (sd == 1 && st == 0 && sl == 0 && ss == 0 && sv == 0)
        {
            var side = _map.GetSelectedSidedefs()[0];
            PreviewPanel.Children.Add(SidePreviews(side.IsFront ? "Front" : "Back", side));
        }
        else if (ss == 1 && st == 0 && sl == 0 && sd == 0 && sv == 0)
        {
            var s = _map.GetSelectedSectors()[0];
            PreviewPanel.Children.Add(Group("Sector", new[]
            {
                Slot("Floor", s.FloorTexture, _resources.GetFlat(s.FloorTexture),
                    () => _ = ChangeFlat(s, ceiling: false)),
                Slot("Ceiling", s.CeilTexture, _resources.GetFlat(s.CeilTexture),
                    () => _ = ChangeFlat(s, ceiling: true)),
            }));
        }
        else if (st == 1 && sl == 0 && sd == 0 && ss == 0 && sv == 0)
        {
            var t = _map.GetSelectedThings()[0];
            string sprite = _config?.GetThing(t.Type)?.Sprite ?? "";
            PreviewPanel.Children.Add(Group("Thing", new[]
            {
                Slot(sprite.Length > 0 ? sprite : $"type {t.Type}", sprite, _resources.GetSprite(sprite),
                    () => _ = ChangeThingType(t)),
            }));
        }
    }

    private Control SidePreviews(string header, Sidedef sd) => Group($"{header} Sidedef", new[]
    {
        Slot("Upper", sd.HighTexture, _resources!.GetWallTexture(sd.HighTexture),
            () => _ = ChangeTexture("Browse Textures", n => sd.HighTexture = n)),
        Slot("Middle", sd.MidTexture, _resources!.GetWallTexture(sd.MidTexture),
            () => _ = ChangeTexture("Browse Textures", n => sd.MidTexture = n)),
        Slot("Lower", sd.LowTexture, _resources!.GetWallTexture(sd.LowTexture),
            () => _ = ChangeTexture("Browse Textures", n => sd.LowTexture = n)),
    });

    // Opens the wall-texture browser and applies the pick to a sidedef slot (undoable), then refreshes previews.
    private async Task ChangeTexture(string title, Action<string> apply)
    {
        if (_resources is null || _undo is null) return;
        var dlg = new TextureBrowserDialog(_resources, flats: false) { Title = title };
        if (await dlg.ShowDialog<bool>(this) && dlg.Selected is { } name)
        {
            CreateUndo("Change texture");
            apply(name);
            MapView.MarkGeometryDirty();
            UpdateInfo();
            MapView.Focus();
        }
    }

    // Opens the flat browser and applies the pick to a sector's floor or ceiling (undoable).
    private async Task ChangeFlat(Sector s, bool ceiling)
    {
        if (_resources is null || _undo is null) return;
        var dlg = new TextureBrowserDialog(_resources, flats: true) { Title = ceiling ? "Browse Ceiling Flat" : "Browse Floor Flat" };
        if (await dlg.ShowDialog<bool>(this) && dlg.Selected is { } name)
        {
            CreateUndo("Change flat");
            if (ceiling) s.CeilTexture = name; else s.FloorTexture = name;
            MapView.MarkGeometryDirty();
            UpdateInfo();
            MapView.Focus();
        }
    }

    // Opens the categorized thing browser and changes the selected thing's type (undoable).
    private async Task ChangeThingType(Thing t)
    {
        if (_config is null || _undo is null) return;
        var dlg = new BrowserDialog("Browse Things", CatalogBrowse.Things(_config), t.Type);
        if (await dlg.ShowDialog<bool>(this) && dlg.SelectedNumber is int n)
        {
            CreateUndo("Change thing type");
            t.Type = n;
            MapView.InsertThingType = n;
            MapView.MarkGeometryDirty();
            UpdateInfo();
            MapView.Focus();
        }
    }

    // A labeled group of preview slots.
    private static Control Group(string header, IEnumerable<Control> slots)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        panel.Children.Add(new TextBlock { Text = header, Foreground = Brushes.LightSkyBlue, FontSize = 11 });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var s in slots) row.Children.Add(s);
        panel.Children.Add(row);
        return panel;
    }

    // A single 64x64 thumbnail with its texture name underneath ("-" / missing shows an empty box).
    // When onClick is given the thumbnail is clickable (hand cursor) to browse/change the texture.
    private static Control Slot(string label, string texName, ImageData? img, Action? onClick = null)
    {
        var image = new Image { Width = 64, Height = 64, Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None); // crisp pixel art
        if (img != null) image.Source = BitmapConvert.ToBitmap(img);

        var box = new Border
        {
            Width = 66, Height = 66, Background = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x16)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x3a, 0x44)), BorderThickness = new Thickness(1),
            Child = image,
        };
        if (onClick != null)
        {
            box.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            ToolTip.SetTip(box, "Click to change");
            box.PointerPressed += (_, _) => onClick();
        }
        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 1 };
        stack.Children.Add(box);
        stack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrEmpty(texName) || texName == "-" ? "-" : texName,
            Foreground = Brushes.Gray, FontSize = 10, MaxWidth = 66, TextTrimming = TextTrimming.CharacterEllipsis,
        });
        return stack;
    }

}
