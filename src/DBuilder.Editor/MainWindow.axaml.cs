// ABOUTME: Main editor window code-behind - wires menu/toolbar actions to map load/save and the editing core.
// ABOUTME: Owns the loaded MapSet + UndoManager and keeps the status/info panels in sync with selection.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Editor;

public partial class MainWindow : Window
{
    private MapSet? _map;
    private UndoManager? _undo;
    private string? _mapMarker;
    private string? _wadPath;
    private MapFormat _mapFormat = MapFormat.Doom;
    private GameConfiguration? _config;
    private string _configName = "(none)";

    public MainWindow() : this(null) { }

    public MainWindow(string? openPath)
    {
        InitializeComponent();
        MapView.CursorWorldMoved += w => CoordText.Text = $"{w.x:0} , {w.y:0}";
        MapView.Picked += _ => UpdateInfo();
        MapView.EditBegun += desc => _undo?.CreateUndo(desc);
        MapView.Changed += UpdateInfo;
        MapView.EditRequested += OnEditSelected;
        MapView.ModeChanged += () => { SetStatus($"Mode: {MapView.CurrentEditMode}"); UpdateInfo(); };
        MapView.DrawModeChanged += () => SetStatus(MapView.DrawMode
            ? "Draw mode: click to place vertices, click the first point or Enter to close, Esc/right-click to cancel."
            : "Draw mode off.");

        TryLoadDefaultConfig();

        if (openPath != null && System.IO.File.Exists(openPath))
            LoadWad(openPath);
    }

    // Attempts to load a game config on startup from DBUILDER_GAMECONFIG, else a known UDB asset path.
    private void TryLoadDefaultConfig()
    {
        string? path = Environment.GetEnvironmentVariable("DBUILDER_GAMECONFIG");
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            const string fallback = "/Users/jsh/dev/projects/claude_directed_5/UltimateDoomBuilder/Assets/Common/Configurations/Doom_DoomDoom.cfg";
            path = System.IO.File.Exists(fallback) ? fallback : null;
        }
        if (path != null) LoadConfig(path);
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
            var actors = DecorateParser.Parse(text);
            _config.MergeActors(actors, doomEdNums);
            foreach (var a in actors) if (a.DoomEdNum >= 0) count++;
        }
        foreach (var text in zscript)
        {
            var actors = ZScriptParser.Parse(text);
            _config.MergeActors(actors, doomEdNums);
            foreach (var a in actors) if (doomEdNums.ContainsValue(a.ClassName)) count++;
        }

        MapView.GameConfig = _config; // refresh thing labels/sprites
        if (count > 0) SetStatus($"Loaded {count} actor(s) from DECORATE/ZScript resources.");
    }

    private void LoadConfig(string path)
    {
        try
        {
            _config = GameConfiguration.FromFile(path);
            _configName = System.IO.Path.GetFileNameWithoutExtension(path);
            MapView.GameConfig = _config; // enables thing sprites in the map view
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
        _mapFormat = MapFormat.Doom;
        _undo = new UndoManager(map);
        MapView.Map = map;
        MapView.Focus();
        Title = "DBuilder - (new map)";
        UpdateInfo();
        SetStatus("New empty map. Draw with D (sector) or Shift+D (lines); I inserts a vertex/thing.");
    }

    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        var top = GetTopLevel(this);
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open WAD",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Doom WAD") { Patterns = new[] { "*.wad" } } },
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            LoadWad(path);
    }

    // Lets the user pick any map in the currently open WAD (doom2 has 32, hexen 31, ...).
    private async void OnOpenMap(object? sender, RoutedEventArgs e)
    {
        if (_wadPath is null) { SetStatus("Open a WAD first."); return; }
        List<MapEntry> maps;
        using (var wad = new WAD(_wadPath, openreadonly: true)) maps = WadMaps.Find(wad);
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
            Title = "Add Resource (IWAD / PK3)",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("WAD or PK3") { Patterns = new[] { "*.wad", "*.pk3", "*.pk7", "*.zip" } } },
        });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path) return;
        try
        {
            _resources.AddBaseResource(path);
            MapView.MapResources = _resources; // re-trigger texture cache invalidation + redraw
            MergeActorsFromResources();
            SetStatus($"Added resource {System.IO.Path.GetFileName(path)} (textures/flats/actors refreshed)");
        }
        catch (Exception ex) { SetStatus($"Add resource failed: {ex.Message}"); }
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
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
                }
                WadMaps.SaveMap(dst, marker, _map, _mapFormat);
                bytes = msOut.ToArray();
            }

            // Optionally rebuild nodes via an external builder (BSP/BLOCKMAP/REJECT), making the save playable.
            string nodeStatus = BuildNodesIfConfigured(ref bytes);

            System.IO.File.WriteAllBytes(outPath, bytes);
            SetStatus($"Saved {marker} [{_mapFormat}] to {System.IO.Path.GetFileName(outPath)}{nodeStatus}");
        }
        catch (Exception ex) { SetStatus($"Save failed: {ex.Message}"); }
    }

    // Runs the external node builder configured via DBUILDER_NODEBUILDER (+ _ARGS) over the WAD bytes.
    // Returns a short status suffix; on failure the original (node-less) bytes are kept.
    private static string BuildNodesIfConfigured(ref byte[] bytes)
    {
        string? exe = Environment.GetEnvironmentVariable("DBUILDER_NODEBUILDER");
        if (string.IsNullOrWhiteSpace(exe)) return "  (no nodes - set DBUILDER_NODEBUILDER to build)";
        if (!System.IO.File.Exists(exe)) return $"  (node builder not found: {exe})";

        string args = Environment.GetEnvironmentVariable("DBUILDER_NODEBUILDER_ARGS") ?? "-o \"%FO\" \"%FI\"";
        var result = NodeBuilder.Build(bytes, new NodebuilderConfig(exe, args));
        if (result.Success && result.Output != null) { bytes = result.Output; return "  (nodes built)"; }
        return "  (node build FAILED, saved without nodes)";
    }

    private async void OnLoadConfig(object? sender, RoutedEventArgs e)
    {
        var top = GetTopLevel(this);
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Game Configuration",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Game config") { Patterns = new[] { "*.cfg" } } },
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            LoadConfig(path);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        MapView.Focus(); // ensure the map view has keyboard focus on first launch (command-line load)
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
                case Avalonia.Input.Key.C: MapView.CopySelection(); UpdateInfo(); e.Handled = true; return;
                case Avalonia.Input.Key.V: MapView.PasteClipboard(); UpdateInfo(); e.Handled = true; return;
            }
        }
        if (e.Key == Avalonia.Input.Key.Delete || e.Key == Avalonia.Input.Key.Back)
        {
            OnDelete(this, new RoutedEventArgs());
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
            var dlg = new ThingEditDialog(t, _config);
            if (await dlg.ShowDialog<bool>(this))
            {
                _undo.CreateUndo("Edit thing");
                t.Type = dlg.ResultType; t.Angle = dlg.ResultAngle; t.Tag = dlg.ResultTag; t.Action = dlg.ResultAction;
                t.Flags = dlg.ResultFlags;
                t.Position = new DBuilder.Geometry.Vector2D(dlg.ResultX, dlg.ResultY); t.Height = dlg.ResultHeight;
                MapView.InsertThingType = t.Type; // the insert tool reuses the last edited type
                AfterEdit("Thing updated");
            }
        }
        else if (_map.SelectedLinedefsCount == 1 && _map.SelectedThingsCount == 0 && _map.SelectedSectorsCount == 0)
        {
            var l = _map.GetSelectedLinedefs()[0];
            var dlg = new LinedefEditDialog(l, _config);
            if (await dlg.ShowDialog<bool>(this))
            {
                _undo.CreateUndo("Edit linedef");
                l.Action = dlg.ResultAction; l.Tag = dlg.ResultTag; l.Flags = dlg.ResultFlags;
                AfterEdit("Linedef updated");
            }
        }
        else if (_map.SelectedSectorsCount == 1 && _map.SelectedThingsCount == 0 && _map.SelectedLinedefsCount == 0)
        {
            var s = _map.GetSelectedSectors()[0];
            var dlg = new SectorEditDialog(s, _config);
            if (await dlg.ShowDialog<bool>(this))
            {
                _undo.CreateUndo("Edit sector");
                s.FloorHeight = dlg.ResultFloor; s.CeilHeight = dlg.ResultCeil;
                s.FloorTexture = dlg.ResultFloorTex; s.CeilTexture = dlg.ResultCeilTex;
                s.Brightness = dlg.ResultBright; s.Special = dlg.ResultSpecial; s.Tag = dlg.ResultTag;
                AfterEdit("Sector updated");
            }
        }
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

    // ---- View / Help ----

    private void OnFit(object? sender, RoutedEventArgs e) { MapView.FitToMap(); MapView.MarkGeometryDirty(); }

    private void OnToggleThingArrows(object? sender, RoutedEventArgs e)
    {
        MapView.ThingArrows = !MapView.ThingArrows;
        SetStatus($"Things: {(MapView.ThingArrows ? "arrows" : "sprites")}");
        MapView.Focus();
    }

    private void OnAbout(object? sender, RoutedEventArgs e)
        => SetStatus("DBuilder - a cross-platform Doom map editor (Avalonia + Silk.NET).");

    // ---- Map loading ----

    private ResourceManager? _resources;

    private void LoadWad(string path)
    {
        try
        {
            List<MapEntry> maps;
            using (var wad = new WAD(path, openreadonly: true)) maps = WadMaps.Find(wad);
            if (maps.Count == 0) { SetStatus($"No map found in {System.IO.Path.GetFileName(path)}"); return; }

            _wadPath = path;

            // Resource manager over the loaded WAD provides flats/textures for the map view.
            _resources?.Dispose();
            _resources = new ResourceManager();
            _resources.AddResource(path);
            MapView.MapResources = _resources;
            MergeActorsFromResources();

            LoadMapEntry(maps[0]);
            if (maps.Count > 1)
                SetStatus($"Loaded {maps[0].Name} (1 of {maps.Count} maps - File > Open Map to switch)");
        }
        catch (Exception ex) { SetStatus($"Load failed: {ex.Message}"); }
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

            _map = map;
            _mapMarker = entry.Name;
            _mapFormat = entry.Format;
            _undo = new UndoManager(map);

            MapView.Map = map;
            MapView.Focus(); // so Tab toggles 3D immediately instead of traversing the menu bar
            Title = $"DBuilder - {System.IO.Path.GetFileName(_wadPath)} ({entry.Name})";
            UpdateInfo();
            SetStatus($"Loaded {entry.Name} [{entry.Format}]: {map.Vertices.Count} verts, {map.Linedefs.Count} lines, {map.Sectors.Count} sectors, {map.Things.Count} things");
        }
        catch (Exception ex) { SetStatus($"Load failed: {ex.Message}"); }
    }

    // ---- UI helpers ----

    private void SetStatus(string text) => StatusText.Text = text;

    private void UpdateInfo()
    {
        if (_map is null) { InfoText.Text = "No map loaded."; return; }
        int sv = _map.SelectedVerticesCount, sl = _map.SelectedLinedefsCount, ss = _map.SelectedSectorsCount, st = _map.SelectedThingsCount;

        if (sv + sl + ss + st == 0)
        {
            InfoText.Text = $"Map: {_map.Vertices.Count} vertices, {_map.Linedefs.Count} linedefs, {_map.Sectors.Count} sectors, {_map.Things.Count} things." +
                            $"   Config: {_configName}.   Mode: {MapView.CurrentEditMode} (1 verts, 2 lines, 3 sectors, 4 things).   Click select; left-drag box-selects (or moves a grabbed vertex/thing); right-drag pans; wheel or -/= zoom; R fit; double-click edit; right-click splits; S/T toggle fills/things; Y sprites/arrows; D draw sector (Shift+D lines); I insert vertex/thing; M make sector at cursor; F flip linedef (Shift+F sidedefs); A align textures X (Shift+A Y); Ctrl/Cmd+C/V copy/paste; G snap, [ ] grid size; Delete removes (undoable).   Tab = 3D (WASD/arrows/QE, G walk).";
            return;
        }

        // Detailed read-out for a single selected element (config-aware names); otherwise a counts summary.
        if (st == 1 && sl == 0 && ss == 0 && sv == 0)
        {
            var t = _map.GetSelectedThings()[0];
            string name = _config?.ThingTitle(t.Type) ?? $"type {t.Type}";
            InfoText.Text = $"Thing: {t.Type} - {name}    pos ({t.Position.x:0}, {t.Position.y:0})    angle {t.Angle}    tag {t.Tag}" +
                            (t.Action != 0 ? $"    action {t.Action}" : "");
        }
        else if (sl == 1 && st == 0 && ss == 0 && sv == 0)
        {
            var l = _map.GetSelectedLinedefs()[0];
            string act = _config?.LinedefActionTitle(l.Action) ?? (l.Action == 0 ? "None" : $"action {l.Action}");
            string flags = _config != null ? string.Join(", ", _config.DescribeLinedefFlags(l.Flags)) : $"0x{l.Flags:X4}";
            if (flags.Length == 0) flags = "none";
            InfoText.Text = $"Linedef {_map.Linedefs.IndexOf(l)}: action {l.Action} - {act}    tag {l.Tag}    flags: {flags}    " +
                            (l.Back != null ? "two-sided" : "one-sided");
        }
        else if (ss == 1 && st == 0 && sl == 0 && sv == 0)
        {
            var s = _map.GetSelectedSectors()[0];
            string eff = _config?.SectorEffectTitle(s.Special) ?? (s.Special == 0 ? "None" : $"effect {s.Special}");
            InfoText.Text = $"Sector {s.Index}: floor {s.FloorHeight} / ceil {s.CeilHeight}    light {s.Brightness}    effect {s.Special} - {eff}    tag {s.Tag}";
        }
        else
        {
            InfoText.Text = $"Selected: {sv} vertices, {sl} linedefs, {ss} sectors, {st} things." +
                            (_undo is { } u ? $"   Undo: {(u.CanUndo ? u.NextUndoDescription : "-")}  Redo: {(u.CanRedo ? u.NextRedoDescription : "-")}" : "");
        }
    }
}
