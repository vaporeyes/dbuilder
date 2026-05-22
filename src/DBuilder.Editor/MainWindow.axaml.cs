// ABOUTME: Main editor window code-behind - wires menu/toolbar actions to map load/save and the editing core.
// ABOUTME: Owns the loaded MapSet + UndoManager and keeps the status/info panels in sync with selection.

using System;
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

    private void LoadConfig(string path)
    {
        try
        {
            _config = GameConfiguration.FromFile(path);
            _configName = System.IO.Path.GetFileNameWithoutExtension(path);
            SetStatus($"Game config: {_configName} ({_config.Things.Count} things, {_config.LinedefActions.Count} actions, {_config.SectorEffects.Count} sector types)");
            UpdateInfo();
        }
        catch (Exception ex) { SetStatus($"Config load failed: {ex.Message}"); }
    }

    // ---- File ----

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

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_map is null) { SetStatus("Nothing to save."); return; }
        var top = GetTopLevel(this);
        if (top is null) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save as UDMF WAD",
            SuggestedFileName = (_mapMarker ?? "MAP01") + ".edited.wad",
            DefaultExtension = "wad",
            FileTypeChoices = new[] { new FilePickerFileType("Doom WAD") { Patterns = new[] { "*.wad" } } },
        });
        if (file?.TryGetLocalPath() is not { } outPath) return;
        try
        {
            if (System.IO.File.Exists(outPath)) System.IO.File.Delete(outPath);
            using (var wad = new WAD(outPath, openreadonly: false))
                UdmfMapWriter.WriteMap(_map, wad, _mapMarker ?? "MAP01", 0);
            SetStatus($"Saved UDMF to {outPath}");
        }
        catch (Exception ex) { SetStatus($"Save failed: {ex.Message}"); }
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

    private void OnSelectNone(object? sender, RoutedEventArgs e)
    {
        _map?.ClearAllSelected();
        MapView.MarkGeometryDirty();
        UpdateInfo();
    }

    // ---- View / Help ----

    private void OnFit(object? sender, RoutedEventArgs e) { MapView.FitToMap(); MapView.MarkGeometryDirty(); }

    private void OnAbout(object? sender, RoutedEventArgs e)
        => SetStatus("DBuilder - a cross-platform Doom map editor (Avalonia + Silk.NET).");

    // ---- Map loading ----

    private void LoadWad(string path)
    {
        try
        {
            using var wad = new WAD(path, openreadonly: true);
            var (marker, map) = LoadFirstMap(wad);
            if (map is null) { SetStatus($"No map found in {System.IO.Path.GetFileName(path)}"); return; }

            _map = map;
            _mapMarker = marker;
            _undo = new UndoManager(map);
            MapView.Map = map;
            Title = $"DBuilder - {System.IO.Path.GetFileName(path)} ({marker})";
            UpdateInfo();
            SetStatus($"Loaded {marker}: {map.Vertices.Count} verts, {map.Linedefs.Count} lines, {map.Sectors.Count} sectors, {map.Things.Count} things");
        }
        catch (Exception ex) { SetStatus($"Load failed: {ex.Message}"); }
    }

    // Finds the first map block and loads it with the matching loader (UDMF / Hexen-binary / Doom-binary).
    private static (string? marker, MapSet? map) LoadFirstMap(WAD wad)
    {
        for (int i = 0; i < wad.Lumps.Count; i++)
        {
            string name = wad.Lumps[i].Name;
            bool udmf = false, binary = false, hexen = false;
            for (int j = i + 1; j < wad.Lumps.Count && j <= i + 12; j++)
            {
                string sub = wad.Lumps[j].Name;
                if (sub == "TEXTMAP") { udmf = true; break; }
                if (sub == "BEHAVIOR") hexen = true;
                if (sub is "VERTEXES" or "LINEDEFS" or "SIDEDEFS" or "SECTORS" or "THINGS") binary = true;
            }
            if (udmf)
            {
                var textmap = wad.FindLump("TEXTMAP");
                if (textmap != null)
                {
                    var m = UdmfMapLoader.Load(System.Text.Encoding.ASCII.GetString(textmap.Stream.ReadAllBytes()), out _);
                    return (name, m);
                }
            }
            if (binary)
            {
                var m = hexen ? HexenMapLoader.Load(wad, name) : DoomMapLoader.Load(wad, name);
                if (m != null) return (name, m);
            }
        }
        return (null, null);
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
                            $"   Config: {_configName}.   Click to select (Shift = add); drag to move; Delete removes (undoable).";
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
            InfoText.Text = $"Linedef {_map.Linedefs.IndexOf(l)}: action {l.Action} - {act}    tag {l.Tag}    flags 0x{l.Flags:X4}    " +
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
