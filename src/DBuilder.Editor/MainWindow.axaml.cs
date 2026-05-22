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

    public MainWindow() : this(null) { }

    public MainWindow(string? openPath)
    {
        InitializeComponent();
        MapView.CursorWorldMoved += w => CoordText.Text = $"{w.x:0} , {w.y:0}";
        MapView.Picked += _ => UpdateInfo();

        if (openPath != null && System.IO.File.Exists(openPath))
            LoadWad(openPath);
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
            InfoText.Text = $"Map: {_map.Vertices.Count} vertices, {_map.Linedefs.Count} linedefs, {_map.Sectors.Count} sectors, {_map.Things.Count} things. " +
                            "Click to select (Shift = add). Delete removes the selection (undoable).";
            return;
        }
        InfoText.Text = $"Selected: {sv} vertices, {sl} linedefs, {ss} sectors, {st} things." +
                        (_undo is { } u ? $"   Undo: {(u.CanUndo ? u.NextUndoDescription : "-")}  Redo: {(u.CanRedo ? u.NextRedoDescription : "-")}" : "");
    }
}
