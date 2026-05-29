// ABOUTME: Main editor window code-behind - wires menu/toolbar actions to map load/save and the editing core.
// ABOUTME: Owns the loaded MapSet + UndoManager and keeps the status/info panels in sync with selection.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
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
    private string? _pk3Path;
    private List<Pk3MapEntry>? _pk3Maps;
    private string? _pk3MapArchivePath;
    private string? _iwadPath; // an IWAD (the loaded WAD if it is one, else an added IWAD resource) for Test Map
    private MapFormat _mapFormat = MapFormat.Doom;
    private MapOptions? _mapOptions;
    private Configuration? _mapSettings;
    private GameConfiguration? _config;
    private string _configName = "(none)";
    private string _configFile = "";
    private bool _configIsAuto = true; // true while the config was chosen by default/auto-detect (so WAD open may switch it)

    // Default directory holding the bundled UDB game configurations (the default config lives here too).
    private const string DefaultConfigDir =
        "/Users/jsh/dev/projects/claude_directed_5/UltimateDoomBuilder/Assets/Common/Configurations";

    // Standard macOS GZDoom install, used by Test Map when no port is configured.
    private const string DefaultGzdoomPath = "/Applications/GZDoom.app/Contents/MacOS/gzdoom";

    private Settings _settings = new();
    private readonly string _settingsPath = Settings.DefaultPath;

    // The game-config directory, overridable via settings (falls back to the bundled location).
    private string ConfigDir => string.IsNullOrWhiteSpace(_settings.ConfigDir) ? DefaultConfigDir : _settings.ConfigDir!;

    public MainWindow() : this(null) { }

    public MainWindow(string? openPath)
    {
        InitializeComponent();
        ShowActivated = true;
        MapView.CursorWorldMoved += w => CoordText.Text = $"{w.x:0} , {w.y:0}";
        MapView.Picked += _ => { UpdateInfo(); UpdateStatusDetails(); };
        MapView.EditBegun += desc => _undo?.CreateUndo(desc);
        MapView.Changed += UpdateInfo;
        MapView.EditRequested += OnEditSelected;
        MapView.ModeChanged += () =>
        {
            SetStatus(MapView.In3DMode ? "Mode: 3D" : $"Mode: {MapView.CurrentEditMode}");
            UpdateInfo();
            UpdateStatusDetails();
        };
        MapView.Target3DChanged += desc => { if (desc.Length > 0) SetStatus($"3D target: {desc}  (wheel raises/lowers, Shift = 1)"); };
        MapView.BrowseTexturesRequested += OnBrowseTextures;
        MapView.DrawModeChanged += () =>
        {
            SetStatus(MapView.DrawMode
                ? "Draw mode: click to place vertices, click the first point or Enter to close, Esc/right-click to cancel."
                : "Draw mode off.");
            UpdateStatusDetails();
        };
        Activated += (_, _) => FocusMapViewForShortcuts();

        _settings = Settings.Load(_settingsPath);
        RebuildSelectionGroupsMenu();
        RebuildRecentMenu();
        TryLoadDefaultConfig();

        if (openPath != null && System.IO.File.Exists(openPath))
            _ = LoadArchive(openPath, promptForMap: false);

        UpdateStatusDetails();
    }

    private void SaveSettings() => _settings.Save(_settingsPath);

    // Rebuilds the File > Open Recent submenu from the persisted recent-files list.
    private void RebuildRecentMenu()
    {
        var items = new List<MenuItem>();
        foreach (var path in _settings.RecentFiles)
        {
            var item = new MenuItem { Header = path };
            string captured = path;
            item.Click += async (_, _) =>
            {
                if (System.IO.File.Exists(captured)) await LoadArchive(captured, promptForMap: true);
                else SetStatus($"File not found: {captured}");
            };
            items.Add(item);
        }
        if (items.Count == 0)
            items.Add(new MenuItem { Header = "(none)", IsEnabled = false });
        OpenRecentMenu.ItemsSource = items;
    }

    // Attempts to load a game config on startup from DBUILDER_GAMECONFIG, else a known UDB asset path.
    private void TryLoadDefaultConfig()
    {
        string? path = Environment.GetEnvironmentVariable("DBUILDER_GAMECONFIG");
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            string fallback = System.IO.Path.Combine(ConfigDir, "Doom_DoomDoom.cfg");
            path = System.IO.File.Exists(fallback) ? fallback : null;
        }
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
            var actors = DecorateParser.Parse(text, _resources.GetTextResource);
            _config.MergeActors(actors, doomEdNums);
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
        if (count > 0) SetStatus($"Loaded {count} actor(s) from DECORATE/ZScript resources.");
    }

    private void ApplyResourceConfig()
    {
        if (_resources is null) return;
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
            _configIsAuto = auto;
            MapView.GameConfig = _config; // enables thing sprites in the map view
            ApplyResourceConfig();
            SetStatus($"Game config: {_configName} ({_config.Things.Count} things, {_config.LinedefActions.Count} actions, {_config.SectorEffects.Count} sector types)");
            UpdateInfo();
        }
        catch (Exception ex) { SetStatus($"Config load failed: {ex.Message}"); }
    }

    // ---- File ----

    // Starts a fresh empty map. Keeps any open WAD's resources so textures still resolve while editing.
    private void OnNewMap(object? sender, RoutedEventArgs e)
    {
        var map = new MapSet();
        _map = map;
        _mapMarker = "MAP01";
        _sourceMapMarker = null;
        _wadPath = null;
        _pk3Path = null;
        _pk3Maps = null;
        _pk3MapArchivePath = null;
        _mapOptions = new MapOptions { CurrentName = _mapMarker };
        _mapSettings = new Configuration(sorted: true);
        _mapFormat = MapFormat.Doom;
        _undo = new UndoManager(map);
        MapView.Map = map;
        MapView.Focus();
        Title = CurrentEditorTitle();
        UpdateInfo();
        SetStatus("New empty map. Draw with D (sector) or Shift+D (lines); I inserts a vertex/thing.");
    }

    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        var top = GetTopLevel(this);
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open WAD or PK3",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Doom WAD or PK3") { Patterns = new[] { "*.wad", "*.pk3", "*.pk7", "*.zip" } } },
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            await LoadArchive(path, promptForMap: true);
    }

    // Lets the user pick any map in the currently open WAD (doom2 has 32, hexen 31, ...).
    private async void OnOpenMap(object? sender, RoutedEventArgs e)
    {
        if (_wadPath is null && _pk3Path is null) { SetStatus("Open a WAD or PK3 first."); return; }
        if (_pk3Path is not null && _pk3Maps is not null)
        {
            var displayMaps = new List<MapEntry>();
            foreach (var pk3Map in _pk3Maps) displayMaps.Add(DisplayEntry(pk3Map));
            var pk3Dialog = new MapPickerDialog(displayMaps, CurrentPk3DisplayName());
            if (await pk3Dialog.ShowDialog<bool>(this) && pk3Dialog.Selected is { } selected)
            {
                int index = displayMaps.FindIndex(m => m.Name == selected.Name && m.Format == selected.Format);
                if (index >= 0) LoadPk3MapEntry(_pk3Maps[index]);
            }
            return;
        }

        List<MapEntry> maps;
        using (var wad = new WAD(_wadPath!, openreadonly: true)) maps = WadMaps.Find(wad);
        if (maps.Count == 0) { SetStatus("No maps in this WAD."); return; }

        var dlg = new MapPickerDialog(maps, _mapMarker);
        if (await dlg.ShowDialog<bool>(this) && dlg.Selected is { } entry)
            LoadMapEntry(entry);
    }

    // Adds a base resource (IWAD or PK3) beneath the current map's WAD so its textures/flats/sprites resolve.
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

        var options = new ResourceOptionsDialog(new DataLocation(DataLocation.InferType(path), path));
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
        catch (Exception ex) { SetStatus($"Add resource failed: {ex.Message}"); }
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
            _undo.CreateUndo(ceiling ? "Set ceiling flat" : "Set floor flat");
            foreach (var sector in sectors)
            {
                if (ceiling) sector.CeilTexture = name;
                else sector.FloorTexture = name;
            }
            MapView.MarkGeometryDirty();
            UpdateInfo();
            SetStatus($"Set {sectors.Count} {(ceiling ? "ceiling" : "floor")} flat(s) to {name}.");
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
        _undo.CreateUndo("Set linedef action");
        foreach (var line in lines) line.Action = action;
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus($"Set {lines.Count} linedef action(s) to {action} - {title}");
    }

    private void ApplyEffectToSelectedSectors(int effect, string title)
    {
        if (_map is null || _undo is null || _map.SelectedSectorsCount == 0)
        {
            SetStatus($"Sector effect selected: {effect} - {title}");
            return;
        }

        var sectors = _map.GetSelectedSectors();
        _undo.CreateUndo("Set sector effect");
        foreach (var sector in sectors) sector.Special = effect;
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus($"Set {sectors.Count} sector effect(s) to {effect} - {title}");
    }

    private async void OnSave(object? sender, RoutedEventArgs e) => await DoSave(_mapFormat);

    // Prompts for a target map format and saves a converted copy (flags translated via the game config).
    private async void OnSaveAsFormat(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("Nothing to save."); return; }
        var dlg = new FormatPickerDialog(_mapFormat);
        if (await dlg.ShowDialog<bool>(this)) await DoSave(dlg.ResultFormat);
    }

    private async System.Threading.Tasks.Task DoSave(MapFormat targetFormat)
    {
        if (_map is null) { SetStatus("Nothing to save."); return; }
        var top = GetTopLevel(this);
        if (top is null) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save WAD As",
            SuggestedFileName = _wadPath != null
                ? System.IO.Path.GetFileNameWithoutExtension(_wadPath) + ".edited.wad"
                : (_mapMarker ?? "MAP01") + ".wad",
            DefaultExtension = "wad",
            FileTypeChoices = new[] { new FilePickerFileType("Doom WAD") { Patterns = new[] { "*.wad" } } },
        });
        if (file?.TryGetLocalPath() is not { } outPath) return;
        try
        {
            string marker = _mapMarker ?? "MAP01";
            // When exporting to a different format, translate the flag representation the target writer reads.
            // The fill is additive, so the in-memory map remains valid in its original format afterwards.
            if (targetFormat != _mapFormat)
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
            string nodeStatus = BuildNodesIfConfigured(ref bytes);

            System.IO.File.WriteAllBytes(outPath, bytes);
            SaveCurrentMapOptions(outPath, marker);
            string converted = targetFormat != _mapFormat ? $" (converted from {_mapFormat})" : "";
            SetStatus($"Saved {marker} [{targetFormat}]{converted} to {System.IO.Path.GetFileName(outPath)}{nodeStatus}");
        }
        catch (Exception ex) { SetStatus($"Save failed: {ex.Message}"); }
    }

    private async void OnMapOptions(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        _mapOptions ??= new MapOptions { CurrentName = _mapMarker ?? "MAP01" };
        var dlg = new MapOptionsDialog(_mapMarker ?? "MAP01", _map.Namespace, _mapOptions, _config?.UseLongTextureNames ?? false);
        if (await dlg.ShowDialog<bool>(this))
        {
            _mapMarker = dlg.ResultMarker;
            _map.Namespace = dlg.ResultNamespace;
            _mapOptions.CurrentName = _mapMarker;
            dlg.ApplyTo(_mapOptions);
            Title = CurrentEditorTitle();
            UpdateInfo();
            MapView.Focus();
            SetStatus($"Map options updated: {_mapMarker}.");
        }
    }

    // Runs the external node builder (DBUILDER_NODEBUILDER env, else settings) over the WAD bytes.
    // Returns a short status suffix; on failure the original (node-less) bytes are kept.
    private string BuildNodesIfConfigured(ref byte[] bytes)
    {
        string? exe = Environment.GetEnvironmentVariable("DBUILDER_NODEBUILDER");
        if (string.IsNullOrWhiteSpace(exe)) exe = _settings.NodeBuilderPath;
        if (string.IsNullOrWhiteSpace(exe)) return "  (no nodes - set a node builder in Settings or DBUILDER_NODEBUILDER)";
        if (!System.IO.File.Exists(exe)) return $"  (node builder not found: {exe})";

        string args = Environment.GetEnvironmentVariable("DBUILDER_NODEBUILDER_ARGS")
            ?? (string.IsNullOrWhiteSpace(_settings.NodeBuilderArgs) ? "-o \"%FO\" \"%FI\"" : _settings.NodeBuilderArgs!);
        var result = NodeBuilder.Build(bytes, new NodebuilderConfig(exe, args));
        if (result.Success && result.Output != null) { bytes = result.Output; return "  (nodes built)"; }
        return "  (node build FAILED, saved without nodes)";
    }

    private void SaveCurrentMapOptions(string wadPath, string marker)
    {
        var options = _mapOptions ?? new MapOptions();
        options.CurrentName = marker;
        options.ConfigFile = _configFile;
        options.WriteResources();
        options.WriteDrawingOptions();
        var root = _mapSettings ?? new Configuration(sorted: true);
        options.WriteRootOptions(root);
        root.SaveConfiguration(DbsPath(wadPath));
        _mapOptions = options;
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
        SaveSettings();
        SetStatus("Settings saved.");
    }

    private async void OnLoadConfig(object? sender, RoutedEventArgs e)
    {
        var dlg = new ConfigDialog(ConfigDir, _configName);
        if (await dlg.ShowDialog<bool>(this) && dlg.SelectedPath is { } path) LoadConfig(path);
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
        if (accel)
        {
            switch (e.Key)
            {
                case Avalonia.Input.Key.Z: OnUndo(this, new RoutedEventArgs()); e.Handled = true; return;
                case Avalonia.Input.Key.Y: OnRedo(this, new RoutedEventArgs()); e.Handled = true; return;
                case Avalonia.Input.Key.S: OnSave(this, new RoutedEventArgs()); e.Handled = true; return;
                case Avalonia.Input.Key.X: OnCut(this, new RoutedEventArgs()); e.Handled = true; return;
                case Avalonia.Input.Key.C: OnCopy(this, new RoutedEventArgs()); e.Handled = true; return;
                case Avalonia.Input.Key.V: OnPaste(this, new RoutedEventArgs()); e.Handled = true; return;
                case Avalonia.Input.Key.D: OnDuplicate(this, new RoutedEventArgs()); e.Handled = true; return;
            }
        }
        if (e.Key == Avalonia.Input.Key.Delete || e.Key == Avalonia.Input.Key.Back)
        {
            OnDelete(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }
        // Escape always exits a draw tool, even if focus left the map view (e.g. after using a menu).
        if (e.Key == Avalonia.Input.Key.Escape && MapView.InDrawMode)
        {
            MapView.ExitDrawModes();
            MapView.Focus();
            SetStatus("Draw mode off.");
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private void OnExit(object? sender, RoutedEventArgs e) => Close();

    // ---- Edit ----

    private void OnUndo(object? sender, RoutedEventArgs e)
    {
        if (_undo?.Undo() == true) { MapView.MarkGeometryDirty(); UpdateInfo(); SetStatus("Undo"); }
    }

    private void OnRedo(object? sender, RoutedEventArgs e)
    {
        if (_undo?.Redo() == true) { MapView.MarkGeometryDirty(); UpdateInfo(); SetStatus("Redo"); }
    }

    private void OnCut(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) { SetStatus("No map loaded."); return; }
        int selected = CountSelection();
        if (selected == 0) { SetStatus("Nothing selected to cut."); return; }

        MapView.CopySelection();
        _undo.CreateUndo("Cut selection");
        int removed = _map.DeleteSelection();
        MapView.MarkGeometryDirty();
        UpdateInfo();
        MapView.Focus();
        SetStatus($"Cut {removed} element(s).");
    }

    private void OnCopy(object? sender, RoutedEventArgs e) => RunClipboardEdit(MapView.CopySelection());

    private void OnPaste(object? sender, RoutedEventArgs e) => RunClipboardEdit(MapView.PasteClipboard());

    private void OnDuplicate(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        if (CountSelection() == 0) { SetStatus("Nothing selected to duplicate."); return; }

        MapView.CopySelection();
        RunClipboardEdit(MapView.PasteClipboard());
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
        _undo.CreateUndo("Delete selection");
        int removed = _map.DeleteSelection();
        _map.BuildIndexes();
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus($"Deleted {removed} element(s).");
    }

    // Opens a property dialog for the single selected element (triggered by a double-click).
    private async void OnEditSelected()
    {
        if (_map is null || _undo is null) return;

        if (_map.SelectedThingsCount == 1 && _map.SelectedLinedefsCount == 0 && _map.SelectedSectorsCount == 0)
        {
            var t = _map.GetSelectedThings()[0];
            var dlg = new ThingEditDialog(t, _config, _resources);
            if (await dlg.ShowDialog<bool>(this))
            {
                _undo.CreateUndo("Edit thing");
                t.Type = dlg.ResultType; t.Angle = dlg.ResultAngle; t.Tag = dlg.ResultTag; t.Action = dlg.ResultAction;
                t.Flags = dlg.ResultFlags;
                Array.Copy(dlg.ResultArgs, t.Args, t.Args.Length);
                t.Position = new DBuilder.Geometry.Vector2D(dlg.ResultX, dlg.ResultY); t.Height = dlg.ResultHeight;
                ApplyFields(t.Fields, dlg.ResultFields);
                MapView.InsertThingType = t.Type; // the insert tool reuses the last edited type
                AfterEdit("Thing updated");
            }
        }
        else if (_map.SelectedLinedefsCount == 1 && _map.SelectedThingsCount == 0 && _map.SelectedSectorsCount == 0)
        {
            var l = _map.GetSelectedLinedefs()[0];
            var dlg = new LinedefEditDialog(l, _config, _resources);
            if (await dlg.ShowDialog<bool>(this))
            {
                _undo.CreateUndo("Edit linedef");
                l.Action = dlg.ResultAction; l.Tag = dlg.ResultTag; l.Flags = dlg.ResultFlags;
                Array.Copy(dlg.ResultArgs, l.Args, l.Args.Length);
                ApplyFields(l.Fields, dlg.ResultFields);
                AfterEdit("Linedef updated");
            }
        }
        else if (_map.SelectedSectorsCount == 1 && _map.SelectedThingsCount == 0 && _map.SelectedLinedefsCount == 0)
        {
            var s = _map.GetSelectedSectors()[0];
            var dlg = new SectorEditDialog(s, _config, _resources);
            if (await dlg.ShowDialog<bool>(this))
            {
                _undo.CreateUndo("Edit sector");
                s.FloorHeight = dlg.ResultFloor; s.CeilHeight = dlg.ResultCeil;
                s.FloorTexture = dlg.ResultFloorTex; s.CeilTexture = dlg.ResultCeilTex;
                s.Brightness = dlg.ResultBright; s.Special = dlg.ResultSpecial; s.Tag = dlg.ResultTag;
                ApplyFields(s.Fields, dlg.ResultFields);
                AfterEdit("Sector updated");
            }
        }
    }

    // Opens the named UDMF flags dialog for one selected thing or linedef.
    private async void OnFlags(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) return;

        if (_map.SelectedLinedefsCount == 1 && _map.SelectedThingsCount == 0 && _map.SelectedSectorsCount == 0 && _map.SelectedVerticesCount == 0)
        {
            var line = _map.GetSelectedLinedefs()[0];
            var current = new HashSet<string>(line.UdmfFlags, StringComparer.OrdinalIgnoreCase);
            if (_config != null) current.UnionWith(_config.LinedefFlagsToUdmf(line.Flags));

            var known = _config?.LinedefFlagsTranslation.SelectMany(flag => flag.Fields) ?? current;
            var name = $"Linedef {_map.Linedefs.IndexOf(line)}";
            var dlg = new UdmfFlagsDialog(name, known, current);
            if (!await dlg.ShowDialog<bool>(this)) return;

            _undo.CreateUndo("Edit linedef flags");
            ApplyFlags(line.UdmfFlags, dlg.ResultFlags);
            if (_config != null) line.Flags = _config.LinedefFlagsFromUdmf(line.UdmfFlags);
            AfterEdit($"{name} flags updated");
            return;
        }

        if (_map.SelectedThingsCount == 1 && _map.SelectedLinedefsCount == 0 && _map.SelectedSectorsCount == 0 && _map.SelectedVerticesCount == 0)
        {
            var thing = _map.GetSelectedThings()[0];
            var current = new HashSet<string>(thing.UdmfFlags, StringComparer.OrdinalIgnoreCase);
            if (_config != null) current.UnionWith(_config.ThingFlagsToUdmf(thing.Flags));

            var known = _config?.ThingFlagsTranslation.SelectMany(flag => flag.Fields) ?? current;
            var name = $"Thing {_map.Things.IndexOf(thing)}";
            var dlg = new UdmfFlagsDialog(name, known, current);
            if (!await dlg.ShowDialog<bool>(this)) return;

            _undo.CreateUndo("Edit thing flags");
            ApplyFlags(thing.UdmfFlags, dlg.ResultFlags);
            if (_config != null) thing.Flags = _config.ThingFlagsFromUdmf(thing.UdmfFlags);
            AfterEdit($"{name} flags updated");
            return;
        }

        SetStatus("Select exactly one linedef or thing to edit flags.");
    }

    // Opens the generic UDMF custom-fields dialog for one selected map element, including vertices.
    private async void OnCustomFields(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) return;
        if (!TryGetSingleFieldedSelection(out var element, out string name))
        {
            SetStatus("Select exactly one vertex, linedef, sector or thing to edit custom fields.");
            return;
        }

        var dlg = new CustomFieldsDialog(name, element.Fields);
        if (!await dlg.ShowDialog<bool>(this)) return;

        _undo.CreateUndo("Edit custom fields");
        ApplyFields(element.Fields, dlg.ResultFields);
        AfterEdit($"{name} custom fields updated");
    }

    // Replaces an element's custom UDMF fields with the dialog's parsed result.
    private static void ApplyFields(Dictionary<string, object> target, Dictionary<string, object> result)
    {
        target.Clear();
        foreach (var kv in result) target[kv.Key] = kv.Value;
    }

    private static void ApplyFlags(HashSet<string> target, IEnumerable<string> result)
    {
        target.Clear();
        foreach (string flag in result)
            if (!string.IsNullOrWhiteSpace(flag)) target.Add(flag.Trim());
    }

    private bool TryGetSingleFieldedSelection(out IFielded element, out string name)
    {
        element = null!;
        name = "";
        if (_map is null) return false;

        int total = _map.SelectedVerticesCount + _map.SelectedLinedefsCount + _map.SelectedSectorsCount + _map.SelectedThingsCount;
        if (total != 1) return false;

        if (_map.SelectedVerticesCount == 1)
        {
            var vertex = _map.GetSelectedVertices()[0];
            element = vertex;
            name = $"Vertex {_map.Vertices.IndexOf(vertex)}";
            return true;
        }
        if (_map.SelectedLinedefsCount == 1)
        {
            var line = _map.GetSelectedLinedefs()[0];
            element = line;
            name = $"Linedef {_map.Linedefs.IndexOf(line)}";
            return true;
        }
        if (_map.SelectedSectorsCount == 1)
        {
            var sector = _map.GetSelectedSectors()[0];
            element = sector;
            name = $"Sector {sector.Index}";
            return true;
        }
        if (_map.SelectedThingsCount == 1)
        {
            var thing = _map.GetSelectedThings()[0];
            element = thing;
            name = $"Thing {_map.Things.IndexOf(thing)}";
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

    private void OnModeVertices(object? sender, RoutedEventArgs e) => SetEditMode(MapControl.EditMode.Vertices);
    private void OnModeLinedefs(object? sender, RoutedEventArgs e) => SetEditMode(MapControl.EditMode.Linedefs);
    private void OnModeSectors(object? sender, RoutedEventArgs e) => SetEditMode(MapControl.EditMode.Sectors);
    private void OnModeThings(object? sender, RoutedEventArgs e) => SetEditMode(MapControl.EditMode.Things);

    private void SetEditMode(MapControl.EditMode mode)
    {
        MapView.SetCurrentEditMode(mode);
        SetStatus($"Mode: {mode}");
        UpdateStatusDetails();
        MapView.Focus();
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

        _undo?.CreateUndo($"Add selection to group {groupIndex + 1}");
        _map.AddSelectionToGroup(groupIndex);
        _mapOptions?.WriteSelectionGroups(_map);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus($"Added {selected} selected element(s) to group {groupIndex + 1}.");
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
        SetStatus($"Selected {CountSelection()} element(s) from group {groupIndex + 1}.");
    }

    private void ClearGroup(int groupIndex)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        int mask = MapSet.GroupMask(groupIndex);
        int grouped = CountGroup(mask);
        if (grouped == 0) { SetStatus($"Group {groupIndex + 1} is empty."); return; }

        _undo?.CreateUndo($"Clear group {groupIndex + 1}");
        _map.ClearGroup(mask);
        _mapOptions?.WriteSelectionGroups(_map);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus($"Cleared {grouped} element(s) from group {groupIndex + 1}.");
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

    // Welds the whole map: merges coincident vertices and splits lines at vertices lying on them.
    private void OnStitch(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) return;
        _undo.CreateUndo("Stitch geometry");
        int merged = _map.MergeOverlappingVertices(0.5);
        int split = _map.SplitLinedefsAtVertices(0.5);
        _map.BuildIndexes();
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus($"Stitched: {merged} vertices merged, {split} lines split.");
    }

    private void OnJoinSectors(object? sender, RoutedEventArgs e) => JoinOrMergeSectors(merge: false);
    private void OnMergeSectors(object? sender, RoutedEventArgs e) => JoinOrMergeSectors(merge: true);

    private void OnFlipH(object? sender, RoutedEventArgs e) => Transform(SelectionTransform.Op.FlipHorizontal, "Flip horizontal");
    private void OnFlipV(object? sender, RoutedEventArgs e) => Transform(SelectionTransform.Op.FlipVertical, "Flip vertical");
    private void OnRotateCW(object? sender, RoutedEventArgs e) => Transform(SelectionTransform.Op.RotateCW, "Rotate 90 CW");
    private void OnRotateCCW(object? sender, RoutedEventArgs e) => Transform(SelectionTransform.Op.RotateCCW, "Rotate 90 CCW");
    private void OnScaleUp(object? sender, RoutedEventArgs e) => ScaleSelection(2.0, "Scale up");
    private void OnScaleDown(object? sender, RoutedEventArgs e) => ScaleSelection(0.5, "Scale down");
    private void OnAlignTexturesX(object? sender, RoutedEventArgs e) => AlignTextures(vertical: false);
    private void OnAlignTexturesY(object? sender, RoutedEventArgs e) => AlignTextures(vertical: true);

    private void AlignTextures(bool vertical)
    {
        string status = MapView.AutoAlignSelectedTextures(vertical);
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
        _undo.CreateUndo(desc);
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
        _undo.CreateUndo(desc);
        SelectionTransform.Scale(_map, factor);
        _map.BuildIndexes();
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus($"{desc} applied.");
    }

    // Joins (or merges, also deleting internal walls) the selected sectors into one, undoable.
    private void JoinOrMergeSectors(bool merge)
    {
        if (_map is null || _undo is null) return;
        var sel = _map.GetSelectedSectors();
        if (sel.Count < 2) { SetStatus("Select 2 or more sectors to join/merge."); return; }

        _undo.CreateUndo(merge ? "Merge sectors" : "Join sectors");
        var keep = merge ? _map.MergeSectors(sel) : _map.JoinSectors(sel);
        _map.BuildIndexes();
        if (keep != null) { _map.ClearAllSelected(); keep.Selected = true; }
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(merge ? $"Merged {sel.Count} sectors." : $"Joined {sel.Count} sectors.");
    }

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
        catch (Exception ex) { SetStatus($"Save prefab failed: {ex.Message}"); }
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
        try { MapView.InsertPrefab(System.IO.File.ReadAllBytes(path)); UpdateInfo(); MapView.Focus(); }
        catch (Exception ex) { SetStatus($"Insert prefab failed: {ex.Message}"); }
    }

    private void OnDrawSector(object? sender, RoutedEventArgs e) => ToggleDrawMode(linesOnly: false, "sector");
    private void OnDrawLines(object? sender, RoutedEventArgs e) => ToggleDrawMode(linesOnly: true, "lines-only");
    private void OnDrawCurve(object? sender, RoutedEventArgs e) => ToggleDrawMode(linesOnly: true, "curve", curve: true);
    private void OnMakeSectorAtCursor(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.MakeSectorAtCursor());
    private void OnInsertAtCursor(object? sender, RoutedEventArgs e) => RunCursorEdit(MapView.InsertAtCursor());
    private void OnDrawRectangle(object? sender, RoutedEventArgs e) => ToggleShape(MapControl.ShapeKind.Rectangle, "rectangle");
    private void OnDrawEllipse(object? sender, RoutedEventArgs e) => ToggleShape(MapControl.ShapeKind.Ellipse, "ellipse");

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
            ? $"Draw {name}: click to place vertices, click the first point or Enter to close, Esc/right-click to cancel."
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
        var win = new TagStatisticsWindow(MapSearch.UsedTagStatistics(_map));
        win.TagActivated += (tag, mode) =>
        {
            if (_map is null) return;
            var r = MapSearch.Find(_map, FindCategory.Tag, tag.ToString());
            MapView.RevealSelection(mode ?? MapControl.EditMode.Linedefs, r.Focus);
            UpdateInfo();
            SetStatus($"Tag {tag}: {r.Count} element(s).");
        };
        win.Show(this);
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
            SetStatus($"Thing type {type}: {r.Count} thing(s).");
        };
        win.Show(this);
    }

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
        var dlg = new GridSetupDialog(MapView.GridSetupSnapshot());
        if (await dlg.ShowDialog<bool>(this))
        {
            MapView.ApplyGridSetup(dlg.ResultSize, dlg.ResultOriginX, dlg.ResultOriginY, dlg.ResultRotation);
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

    private void OnGridSizeDown(object? sender, RoutedEventArgs e) => ChangeGridSize(larger: false);
    private void OnGridSizeUp(object? sender, RoutedEventArgs e) => ChangeGridSize(larger: true);

    private void ChangeGridSize(bool larger)
    {
        SetStatus(MapView.ChangeGridSize(larger));
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

        byte[]? bytes;
        using (var wad = new WAD(_wadPath, openreadonly: true)) bytes = WadMaps.ReadMapLump(wad, _mapMarker, "NODES");
        var parts = NodesReader.Parse(bytes ?? Array.Empty<byte>());
        if (parts.Count == 0) { SetStatus("No vanilla NODES data (none built, or ZDoom/GL nodes)."); return; }

        var lines = new List<(DBuilder.Geometry.Vector2D, DBuilder.Geometry.Vector2D)>(parts.Count);
        foreach (var p in parts)
            lines.Add((new DBuilder.Geometry.Vector2D(p.X1, p.Y1), new DBuilder.Geometry.Vector2D(p.X2, p.Y2)));
        MapView.SetNodeLines(lines);
        MapView.ShowNodes = true;
        SetStatus($"Nodes overlay on: {parts.Count} BSP partition line(s).");
    }

    private void OnToggleThingArrows(object? sender, RoutedEventArgs e)
    {
        MapView.ThingArrows = !MapView.ThingArrows;
        SetStatus($"Things: {(MapView.ThingArrows ? "arrows" : "sprites")}");
        MapView.Focus();
    }

    private void OnToggle3DFloors(object? sender, RoutedEventArgs e)
    {
        MapView.Show3DFloors = !MapView.Show3DFloors;
        SetStatus($"3D floors {(MapView.Show3DFloors ? "shown" : "hidden")}.");
        MapView.Focus();
    }

    private async void OnAbout(object? sender, RoutedEventArgs e)
    {
        await new AboutWindow().ShowDialog(this);
        MapView.Focus();
    }

    private void OnShortcuts(object? sender, RoutedEventArgs e) => new ShortcutsWindow().Show(this);

    // ---- Map loading ----

    private ResourceManager? _resources;

    private async Task LoadArchive(string path, bool promptForMap)
    {
        if (IsPk3Path(path)) await LoadPk3(path, promptForMap);
        else await LoadWad(path, promptForMap);
    }

    private async Task LoadWad(string path, bool promptForMap)
    {
        try
        {
            List<MapEntry> maps;
            using (var wad = new WAD(path, openreadonly: true))
            {
                maps = WadMaps.Find(wad);
                AutoDetectConfig(wad); // switch the auto/default config to match this WAD's game
                if (wad.IsIWAD) _iwadPath = path; // loaded an IWAD directly - usable as the Test Map base
            }
            if (maps.Count == 0) { SetStatus($"No map found in {System.IO.Path.GetFileName(path)}"); return; }

            var selected = maps[0];
            if (promptForMap && maps.Count > 1)
            {
                var dlg = new MapPickerDialog(maps, _mapMarker);
                if (!await dlg.ShowDialog<bool>(this) || dlg.Selected is not { } picked) return;
                selected = picked;
            }

            _wadPath = path;
            _pk3Path = null;
            _pk3Maps = null;
            _pk3MapArchivePath = null;

            _settings.AddRecent(path);
            SaveSettings();
            RebuildRecentMenu();

            LoadMapEntry(selected);
            if (maps.Count > 1)
                SetStatus($"Loaded {selected.Name} ({maps.IndexOf(selected) + 1} of {maps.Count} maps - File > Open Map to switch)");
        }
        catch (Exception ex) { SetStatus($"Load failed: {ex.Message}"); }
    }

    private async Task LoadPk3(string path, bool promptForMap)
    {
        try
        {
            var maps = Pk3Maps.Find(path);
            if (maps.Count == 0) { SetStatus($"No embedded map WAD found in {System.IO.Path.GetFileName(path)}"); return; }

            var selected = maps[0];
            if (promptForMap && maps.Count > 1)
            {
                var displayMaps = new List<MapEntry>();
                foreach (var pk3Map in maps) displayMaps.Add(DisplayEntry(pk3Map));
                var dlg = new MapPickerDialog(displayMaps, null);
                if (!await dlg.ShowDialog<bool>(this) || dlg.Selected is not { } picked) return;

                int index = displayMaps.FindIndex(m => m.Name == picked.Name && m.Format == picked.Format);
                if (index >= 0) selected = maps[index];
            }

            _wadPath = null;
            _pk3Path = path;
            _pk3Maps = maps;
            _mapOptions = null;
            _mapSettings = null;

            _resources?.Dispose();
            _resources = new ResourceManager();
            _resources.AddResource(path);
            ApplyResourceConfig();
            MergeActorsFromResources();

            _settings.AddRecent(path);
            SaveSettings();
            RebuildRecentMenu();

            LoadPk3MapEntry(selected);
            if (maps.Count > 1)
                SetStatus($"Loaded {selected.Map.Name} from {selected.ArchivePath} ({maps.IndexOf(selected) + 1} of {maps.Count} maps - File > Open Map to switch)");
        }
        catch (Exception ex) { SetStatus($"PK3 load failed: {ex.Message}"); }
    }

    // Loads a specific map from the currently open WAD into the editor.
    private void LoadMapEntry(MapEntry entry)
    {
        if (_wadPath == null) return;
        try
        {
            using var wad = new WAD(_wadPath, openreadonly: true);
            var map = WadMaps.Load(wad, entry);
            if (map is null) { SetStatus($"Failed to load {entry.Name}"); return; }

            _mapOptions = LoadMapOptions(_wadPath, entry.Name, out _mapSettings);
            int resourceIssues = RebuildWadResources(_wadPath, _mapOptions);

            _map = map;
            _mapMarker = entry.Name;
            _sourceMapMarker = entry.Name;
            _mapFormat = entry.Format;
            _undo = new UndoManager(map);

            MapView.Map = map;
            MapView.Focus(); // so Tab toggles 3D immediately instead of traversing the menu bar
            Title = CurrentEditorTitle();
            UpdateInfo();
            string resources = resourceIssues == 0 ? "" : $" ({resourceIssues} map resource(s) missing or unreadable)";
            SetStatus($"Loaded {entry.Name} [{entry.Format}]: {map.Vertices.Count} verts, {map.Linedefs.Count} lines, {map.Sectors.Count} sectors, {map.Things.Count} things{resources}");
        }
        catch (Exception ex) { SetStatus($"Load failed: {ex.Message}"); }
    }

    private void LoadPk3MapEntry(Pk3MapEntry entry)
    {
        if (_pk3Path == null) return;
        try
        {
            var map = Pk3Maps.Load(_pk3Path, entry);
            if (map is null) { SetStatus($"Failed to load {entry.Map.Name} from {entry.ArchivePath}"); return; }

            _mapOptions = null;
            _mapSettings = null;
            _map = map;
            _mapMarker = entry.Map.Name;
            _sourceMapMarker = null;
            _mapFormat = entry.Map.Format;
            _pk3MapArchivePath = entry.ArchivePath;
            _undo = new UndoManager(map);

            MapView.Map = map;
            MapView.Focus();
            Title = CurrentEditorTitle();
            UpdateInfo();
            SetStatus($"Loaded {entry.Map.Name} [{entry.Map.Format}] from {entry.ArchivePath}: {map.Vertices.Count} verts, {map.Linedefs.Count} lines, {map.Sectors.Count} sectors, {map.Things.Count} things");
        }
        catch (Exception ex) { SetStatus($"PK3 map load failed: {ex.Message}"); }
    }

    private static bool IsPk3Path(string path)
    {
        string ext = System.IO.Path.GetExtension(path);
        return ext.Equals(".pk3", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".pk7", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".zip", StringComparison.OrdinalIgnoreCase);
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
        return options;
    }

    private int RebuildWadResources(string wadPath, MapOptions options)
    {
        _resources?.Dispose();
        _resources = new ResourceManager();
        _resources.AddResource(wadPath);

        int failures = 0;
        foreach (var location in options.GetResources())
        {
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

        ApplyResourceConfig();
        MergeActorsFromResources();
        return failures;
    }

    private static MapEntry DisplayEntry(Pk3MapEntry entry)
        => new($"{entry.Map.Name} @ {entry.ArchivePath}", entry.Map.Format);

    private string? CurrentPk3DisplayName()
        => _mapMarker is null || _pk3MapArchivePath is null ? null : $"{_mapMarker} @ {_pk3MapArchivePath}";

    private string CurrentEditorTitle()
    {
        if (_wadPath is not null)
            return $"DBuilder - {System.IO.Path.GetFileName(_wadPath)} ({_mapMarker ?? "MAP01"})";
        if (_pk3Path is not null)
            return $"DBuilder - {System.IO.Path.GetFileName(_pk3Path)} ({_pk3MapArchivePath}:{_mapMarker ?? "MAP01"})";
        return $"DBuilder - ({_mapMarker ?? "new map"})";
    }

    // Saves the current map to a temporary PWAD (with nodes if a builder is configured) and launches a source port on it.
    private void OnTestMap(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _mapMarker is null) { SetStatus("No map loaded to test."); return; }

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
            BuildNodesIfConfigured(ref bytes); // GZDoom can build nodes itself, but use the configured builder when present

            string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dbuilder_test_{_mapMarker}.wad");
            System.IO.File.WriteAllBytes(temp, bytes);

            string template = Environment.GetEnvironmentVariable("DBUILDER_TESTPORT_ARGS")
                ?? (string.IsNullOrWhiteSpace(_settings.TestPortArgs) ? SourcePort.DefaultArgsTemplate : _settings.TestPortArgs!);
            var args = SourcePort.BuildArgs(template, iwad!, temp, _mapMarker);

            var psi = new System.Diagnostics.ProcessStartInfo(port!) { UseShellExecute = false };
            foreach (var a in args) psi.ArgumentList.Add(a);
            System.Diagnostics.Process.Start(psi);
            SetStatus($"Testing {_mapMarker} in {System.IO.Path.GetFileNameWithoutExtension(port)} (iwad: {System.IO.Path.GetFileName(iwad)}).");
        }
        catch (Exception ex) { SetStatus($"Test Map failed: {ex.Message}"); }
    }

    private FindReplaceWindow? _findWindow;

    // Opens (or focuses) the non-modal Find & Replace window and wires it to the map.
    private void OnFindReplace(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        if (_findWindow != null) { _findWindow.Activate(); return; }

        var win = new FindReplaceWindow();
        _findWindow = win;
        win.Closed += (_, _) => _findWindow = null;
        win.FindRequested += () =>
        {
            if (_map is null) return;
            var r = MapSearch.Find(_map, win.Category, win.FindText);
            MapView.RevealSelection(ModeFor(win.Category), r.Focus);
            win.SetResult(r.Count == 0 ? "No matches." : $"Found {r.Count} match(es).");
            UpdateInfo();
        };
        win.ReplaceRequested += () =>
        {
            if (_map is null || _undo is null) return;
            _undo.CreateUndo("Find & replace");
            int n = MapSearch.Replace(_map, win.Category, win.FindText, win.ReplaceText);
            if (n > 0) { MapView.MarkGeometryDirty(); MapView.RevealSelection(ModeFor(win.Category), null); }
            win.SetResult(n == 0 ? "Nothing replaced." : $"Replaced {n} element(s).");
            UpdateInfo();
        };
        win.NextFreeTagRequested += () =>
        {
            if (_map is null) return;
            int tag = MapSearch.NextFreeTag(_map);
            win.SetFindText(tag.ToString());
            win.SetResult($"Next free tag: {tag}.");
        };
        win.Show(this);
    }

    // Opens a non-modal list of tags in use; selecting one selects and reveals its elements.
    private void OnTagList(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        var win = new TagListWindow(MapSearch.UsedTags(_map));
        win.TagActivated += tag =>
        {
            if (_map is null) return;
            var r = MapSearch.Find(_map, FindCategory.Tag, tag.ToString());
            MapView.RevealSelection(MapControl.EditMode.Linedefs, r.Focus);
            UpdateInfo();
            SetStatus($"Tag {tag}: {r.Count} element(s).");
        };
        win.Show(this);
    }

    // The edit mode that best shows matches of a given find category.
    private static MapControl.EditMode ModeFor(FindCategory cat) => cat switch
    {
        FindCategory.ThingType => MapControl.EditMode.Things,
        FindCategory.SectorEffect or FindCategory.Flat => MapControl.EditMode.Sectors,
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
        SetStatus(issues.Count == 0 ? "Map analysis: no issues found." : $"Map analysis: {issues.Count} issue(s) found.");
    }

    private void OnCleanUpGeometry(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) { SetStatus("No map loaded."); return; }

        using var previewMap = _map.Clone();
        var preview = CleanUpGeometry(previewMap);
        if (preview.Total == 0)
        {
            SetStatus("Geometry cleanup: no changes needed.");
            return;
        }

        _undo.CreateUndo("Clean up geometry");
        var result = CleanUpGeometry(_map);
        _map.BuildIndexes();
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus($"Geometry cleanup: {result.Repaired} reference repair(s), {result.Sectors} sector(s), {result.Vertices} unused vertex removal(s).");
    }

    private static GeometryCleanupResult CleanUpGeometry(MapSet map)
    {
        int repaired = map.RepairReferences();
        int sectors = map.RemoveUnusedSectors();
        int vertices = map.RemoveUnusedVertices();
        return new GeometryCleanupResult(repaired, sectors, vertices);
    }

    private readonly record struct GeometryCleanupResult(int Repaired, int Sectors, int Vertices)
    {
        public int Total => Repaired + Sectors + Vertices;
    }

    // Reads the map's REJECT lump and highlights sectors that cannot see the selected sector (reject visualization).
    private void OnRejectViewer(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        var sel = _map.GetSelectedSectors();
        if (sel.Count != 1) { SetStatus("Select one sector to inspect REJECT."); return; }
        if (_wadPath is null || _mapMarker is null) { SetStatus("Reject viewer needs the source WAD."); return; }

        byte[]? bytes;
        using (var wad = new WAD(_wadPath, openreadonly: true)) bytes = WadMaps.ReadMapLump(wad, _mapMarker, "REJECT");
        var reject = RejectTable.Parse(bytes ?? Array.Empty<byte>(), _map.Sectors.Count);
        if (!reject.HasData) { SetStatus("No usable REJECT data for this map (none built or too small)."); return; }

        int target = sel[0].Index;
        _map.ClearAllSelected();
        int count = 0;
        for (int i = 0; i < _map.Sectors.Count; i++)
        {
            if (i == target) continue;
            if (reject.IsRejected(target, i)) { _map.Sectors[i].Selected = true; count++; }
        }
        MapView.RevealSelection(MapControl.EditMode.Sectors, null);
        UpdateInfo();
        SetStatus($"{count} sector(s) are rejected (cannot see) from sector {target}.");
    }

    // Bakes Plane_Align (181) linedef specials into sector floor/ceiling slope planes so 3D shows them, undoable.
    private void OnApplySlopes(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) return;
        _undo.CreateUndo("Apply slopes");
        int n = SlopeEffects.ApplyAll(_map);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus(n == 0
            ? "No slope specials found (Plane_Align lines or 9502/9503 slope things)."
            : $"Applied {n} slope plane(s) from specials (visible in 3D).");
    }

    // Builds a staircase from the selected sectors (stepped floor heights), undoable.
    private async void OnBuildStairs(object? sender, RoutedEventArgs e)
    {
        if (_map is null || _undo is null) return;
        var sel = _map.GetSelectedSectors();
        if (sel.Count < 2) { SetStatus("Select 2 or more sectors to build stairs."); return; }

        var dlg = new StairBuilderDialog(sel[0].FloorHeight, 8);
        if (!await dlg.ShowDialog<bool>(this)) return;

        _undo.CreateUndo("Build stairs");
        int n = StairBuilder.Apply(sel, dlg.ResultStart, dlg.ResultStep, dlg.ResultMoveCeiling);
        MapView.MarkGeometryDirty();
        UpdateInfo();
        SetStatus($"Built stairs across {n} sectors (start {dlg.ResultStart}, step {dlg.ResultStep}).");
    }

    // Traces Doom-style sound propagation from the single selected sector and highlights everything it reaches.
    private void OnSoundPropagation(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("No map loaded."); return; }
        var sel = _map.GetSelectedSectors();
        if (sel.Count != 1) { SetStatus("Select exactly one sector to trace sound from."); return; }

        var reach = SoundPropagation.Reachable(_map, sel[0]);
        _map.ClearAllSelected();
        int direct = 0, viaBlock = 0;
        foreach (var (s, level) in reach) { s.Selected = true; if (level == 1) direct++; else viaBlock++; }
        MapView.RevealSelection(MapControl.EditMode.Sectors, null);
        UpdateInfo();
        SetStatus($"Sound reaches {reach.Count} sector(s): {direct} direct, {viaBlock} via a sound-blocking line.");
    }

    // Builds the resource/config-aware check context from the loaded resources and game config.
    private MapCheckContext BuildCheckContext()
    {
        Func<string, bool>? texExists = null, flatExists = null;
        if (_resources != null)
        {
            var texSet = new HashSet<string>(_resources.GetTextureNames(), StringComparer.OrdinalIgnoreCase);
            var flatSet = new HashSet<string>(_resources.GetFlatNames(), StringComparer.OrdinalIgnoreCase);
            texExists = n => texSet.Contains(n);
            flatExists = n => flatSet.Contains(n);
        }
        Func<int, bool>? thingKnown = null, actionKnown = null;
        if (_config != null)
        {
            thingKnown = n => _config.GetThing(n) != null;
            actionKnown = a => _config.GetLinedefAction(a) != null
                || _config.DescribeGeneralizedLinedef(a) != null
                || BoomGeneralized.IsGeneralized(a);
        }
        return new MapCheckContext
        {
            TextureExists = texExists,
            FlatExists = flatExists,
            ThingTypeKnown = thingKnown,
            ActionKnown = actionKnown,
        };
    }

    // ---- UI helpers ----

    private void SetStatus(string text) => StatusText.Text = text;

    private void UpdateStatusDetails()
    {
        ModeText.Text = MapView.In3DMode
            ? "Mode: 3D"
            : MapView.InDrawMode ? $"Mode: {MapView.CurrentEditMode} (draw)" : $"Mode: {MapView.CurrentEditMode}";
        var grid = MapView.GridSetupSnapshot();
        string gridSize = grid.GridSizeF % 1.0 == 0.0
            ? grid.GridSize.ToString(CultureInfo.InvariantCulture)
            : grid.GridSizeF.ToString("0.###", CultureInfo.InvariantCulture);
        GridText.Text = $"{(MapView.SnapToGridEnabled ? "Snap" : "Free")}: {gridSize}";
    }

    private void UpdateInfo()
    {
        if (_map is null) { ShowText("No map loaded."); PreviewPanel.Children.Clear(); return; }
        int sv = _map.SelectedVerticesCount, sl = _map.SelectedLinedefsCount, ss = _map.SelectedSectorsCount, st = _map.SelectedThingsCount;
        UpdatePreviews(sv, sl, ss, st);

        if (sv + sl + ss + st == 0)
        {
            ShowText($"Map: {_map.Vertices.Count} vertices, {_map.Linedefs.Count} linedefs, {_map.Sectors.Count} sectors, {_map.Things.Count} things." +
                     $"   Config: {_configName}.   Mode: {MapView.CurrentEditMode} (1 verts, 2 lines, 3 sectors, 4 things).   Tab toggles 3D.   See Help > Shortcuts for all controls.");
            return;
        }

        // Detailed read-out for a single selected element (config-aware names); otherwise a counts summary.
        if (st == 1 && sl == 0 && ss == 0 && sv == 0) ShowThingFields(_map.GetSelectedThings()[0]);
        else if (sl == 1 && st == 0 && ss == 0 && sv == 0) ShowLinedefFields(_map.GetSelectedLinedefs()[0]);
        else if (ss == 1 && st == 0 && sl == 0 && sv == 0) ShowSectorFields(_map.GetSelectedSectors()[0]);
        else if (sv == 1 && st == 0 && sl == 0 && ss == 0) ShowVertexFields(_map.GetSelectedVertices()[0]);
        else
        {
            ShowText($"Selected: {sv} vertices, {sl} linedefs, {ss} sectors, {st} things." +
                     (_undo is { } u ? $"   Undo: {(u.CanUndo ? u.NextUndoDescription : "-")}  Redo: {(u.CanRedo ? u.NextRedoDescription : "-")}" : ""));
        }
    }

    private bool HasArgs => _mapFormat != MapFormat.Doom;

    private void ShowVertexFields(Vertex v)
    {
        var fields = new List<(string, string)>
        {
            ("Position", $"({v.Position.x:0.###}, {v.Position.y:0.###})"),
            ("Linedefs", v.Linedefs.Count.ToString()),
            ("Groups", DescribeGroups(v.Groups)),
            ("Z floor", double.IsNaN(v.ZFloor) ? "-" : v.ZFloor.ToString("0.###")),
            ("Z ceiling", double.IsNaN(v.ZCeiling) ? "-" : v.ZCeiling.ToString("0.###")),
            ("Custom fields", v.Fields.Count.ToString()),
        };
        ShowFields($"Vertex {_map!.Vertices.IndexOf(v)}", fields);
    }

    private void ShowThingFields(Thing t)
    {
        string name = _config?.ThingTitle(t.Type) ?? $"type {t.Type}";
        string action = _config?.LinedefActionTitle(t.Action) ?? (t.Action == 0 ? "None" : $"action {t.Action}");
        string flags = _config != null ? string.Join(", ", _config.DescribeThingFlags(t.Flags)) : $"0x{t.Flags:X4}";
        if (flags.Length == 0) flags = "none";
        var fields = new List<(string, string)>
        {
            ("Type", $"{t.Type} - {name}"),
            ("Action", $"{t.Action} - {action}"),
            ("Position", $"({t.Position.x:0}, {t.Position.y:0}, {t.Height:0})"),
            ("Angle", $"{t.Angle}°"),
            ("Pitch / roll", $"{t.Pitch}° / {t.Roll}°"),
            ("Scale", $"{t.ScaleX:0.###} x {t.ScaleY:0.###}"),
            ("Tag", t.Tag.ToString()),
            ("Flags", flags),
            ("UDMF flags", DescribeStringSet(t.UdmfFlags)),
            ("Groups", DescribeGroups(t.Groups)),
            ("Custom fields", t.Fields.Count.ToString()),
        };
        if (HasArgs) AddArgFields(fields, t.Args, _config?.GetThing(t.Type)?.Args);
        ShowFields($"Thing {_map!.Things.IndexOf(t)}", fields);
    }

    private void ShowLinedefFields(Linedef l)
    {
        string act = _config?.LinedefActionTitle(l.Action) ?? (l.Action == 0 ? "None" : $"action {l.Action}");
        string flags = _config != null ? string.Join(", ", _config.DescribeLinedefFlags(l.Flags)) : $"0x{l.Flags:X4}";
        if (flags.Length == 0) flags = "none";
        double length = (l.End.Position - l.Start.Position).GetLength();
        var fields = new List<(string, string)>
        {
            ("Action", $"{l.Action} - {act}"),
            ("Tags", DescribeTags(l.Tags)),
            ("Length", $"{length:0.###}"),
            ("Angle", $"{Angle2D.RadToDeg(l.Angle):0.#}°"),
            ("Sides", l.Back != null ? "two-sided" : "one-sided"),
            ("Front sector", l.Front?.Sector is { } fs ? fs.Index.ToString() : "-"),
            ("Back sector", l.Back?.Sector is { } bs ? bs.Index.ToString() : "-"),
            ("Front textures", DescribeSideTextures(l.Front)),
            ("Back textures", DescribeSideTextures(l.Back)),
            ("Front offsets", DescribeSideOffsets(l.Front)),
            ("Back offsets", DescribeSideOffsets(l.Back)),
            ("Flags", flags),
            ("UDMF flags", DescribeStringSet(l.UdmfFlags)),
            ("Groups", DescribeGroups(l.Groups)),
            ("Custom fields", l.Fields.Count.ToString()),
        };
        if (HasArgs) AddArgFields(fields, l.Args, _config?.GetLinedefAction(l.Action)?.Args);
        ShowFields($"Linedef {_map!.Linedefs.IndexOf(l)}", fields);
    }

    private void ShowSectorFields(Sector s)
    {
        string eff = _config?.SectorEffectTitle(s.Special) ?? (s.Special == 0 ? "None" : $"effect {s.Special}");
        ShowFields($"Sector {s.Index}", new List<(string, string)>
        {
            ("Floor height", s.FloorHeight.ToString()),
            ("Ceiling height", s.CeilHeight.ToString()),
            ("Floor texture", s.FloorTexture),
            ("Ceiling texture", s.CeilTexture),
            ("Brightness", s.Brightness.ToString()),
            ("Effect", $"{s.Special} - {eff}"),
            ("Tags", DescribeTags(s.Tags)),
            ("Sidedefs", s.Sidedefs.Count.ToString()),
            ("Groups", DescribeGroups(s.Groups)),
            ("Floor slope", DescribeSlope(s.HasFloorSlope, s.FloorSlope, s.FloorSlopeOffset)),
            ("Ceiling slope", DescribeSlope(s.HasCeilSlope, s.CeilSlope, s.CeilSlopeOffset)),
            ("Custom fields", s.Fields.Count.ToString()),
        });
    }

    private static string DescribeTags(IReadOnlyList<int> tags) => tags.Count == 0 ? "0" : string.Join(", ", tags);

    private static string DescribeStringSet(IEnumerable<string> names)
    {
        var values = names.Where(name => !string.IsNullOrWhiteSpace(name)).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        return values.Length == 0 ? "none" : string.Join(", ", values);
    }

    private static string DescribeGroups(int groups)
    {
        if (groups == 0) return "-";
        var result = new List<string>();
        for (int i = 0; i < MapOptions.SelectionGroupCount; i++)
            if ((groups & MapSet.GroupMask(i)) != 0) result.Add((i + 1).ToString());
        return result.Count == 0 ? groups.ToString() : string.Join(", ", result);
    }

    private static string DescribeSideTextures(Sidedef? side)
        => side == null ? "-" : $"U:{side.HighTexture} M:{side.MidTexture} L:{side.LowTexture}";

    private static string DescribeSideOffsets(Sidedef? side)
        => side == null ? "-" : $"{side.OffsetX}, {side.OffsetY}";

    private static string DescribeSlope(bool active, DBuilder.Geometry.Vector3D normal, double offset)
    {
        if (!active) return "flat";
        string d = double.IsNaN(offset) ? "-" : offset.ToString("0.###", CultureInfo.InvariantCulture);
        return $"({normal.x:0.###}, {normal.y:0.###}, {normal.z:0.###}) d {d}";
    }

    // Appends Arg1..Arg5 cells, labeling each with its config arg title when available.
    private static void AddArgFields(List<(string, string)> fields, int[] args, ArgInfo[]? meta)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string title = meta != null && i < meta.Length && meta[i].Used ? $" ({meta[i].Title})" : "";
            fields.Add(($"Arg{i + 1}{title}", args[i].ToString()));
        }
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
    private void UpdatePreviews(int sv, int sl, int ss, int st)
    {
        PreviewPanel.Children.Clear();
        if (_map is null || _resources is null) return;

        if (sl == 1 && st == 0 && ss == 0 && sv == 0)
        {
            var l = _map.GetSelectedLinedefs()[0];
            if (l.Front is { } f) PreviewPanel.Children.Add(SidePreviews("Front", f));
            if (l.Back is { } b) PreviewPanel.Children.Add(SidePreviews("Back", b));
        }
        else if (ss == 1 && st == 0 && sl == 0 && sv == 0)
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
        else if (st == 1 && sl == 0 && ss == 0 && sv == 0)
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
            _undo.CreateUndo("Change texture");
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
            _undo.CreateUndo("Change flat");
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
            _undo.CreateUndo("Change thing type");
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
