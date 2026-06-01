// ABOUTME: Persisted editor settings for paths, test launchers, recent files, and recent maps.
// ABOUTME: Serialized as JSON under the user's application-data folder; load is best-effort and never throws.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed class Settings
{
    public const int DefaultMaxRecentFiles = 8;
    public const int MinMaxRecentFiles = 8;
    public const int MaxMaxRecentFiles = 25;
    public const int DefaultStatusHistoryLimit = 100;
    public const int MinStatusHistoryLimit = 10;
    public const int MaxStatusHistoryLimit = 1000;

    public string? ConfigDir { get; set; }
    public string? LastUsedConfigName { get; set; }
    public string? LastUsedMapFolder { get; set; }
    public string? NodeBuilderPath { get; set; }
    public string? NodeBuilderArgs { get; set; }
    public string? TestPort { get; set; }
    public string? TestPortArgs { get; set; }
    public string? TestIwad { get; set; }
    public int? MaxRecentFiles { get; set; }
    public bool AutoClearSidedefTextures { get; set; } = true;
    public int? StatusHistoryLimit { get; set; }
    public MergeGeometryMode MergeGeometryMode { get; set; } = MergeGeometryMode.Replace;
    public PasteOptions PasteOptions { get; set; } = new();
    public DrawLineModeSettings DrawLineSettings { get; set; } = new();
    public DrawRectangleModeSettings DrawRectangleSettings { get; set; } = new();
    public DrawEllipseModeSettings DrawEllipseSettings { get; set; } = new();
    public DrawCurveModeSettings DrawCurveSettings { get; set; } = new();
    public DrawGridModeSettings DrawGridSettings { get; set; } = new();
    public EditSelectionModeSettings EditSelectionSettings { get; set; } = new();
    public AutomapModeSettings AutomapSettings { get; set; } = new();
    public MakeDoorModeSettings MakeDoorSettings { get; set; } = new();
    public double? WindowX { get; set; }
    public double? WindowY { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public List<string> RecentFiles { get; set; } = new();
    public List<RecentMapReference> RecentMaps { get; set; } = new();
    public List<EditorShortcutBinding> ShortcutOverrides { get; set; } = new();

    public int NormalizedStatusHistoryLimit =>
        Math.Clamp(StatusHistoryLimit ?? DefaultStatusHistoryLimit, MinStatusHistoryLimit, MaxStatusHistoryLimit);

    public int NormalizedMaxRecentFiles =>
        Math.Clamp(MaxRecentFiles ?? DefaultMaxRecentFiles, MinMaxRecentFiles, MaxMaxRecentFiles);

    public MergeGeometryMode NormalizedMergeGeometryMode =>
        Enum.IsDefined(MergeGeometryMode) ? MergeGeometryMode : MergeGeometryMode.Replace;

    public PasteOptions NormalizedPasteOptions =>
        PasteOptions is { } options && Enum.IsDefined(options.ChangeTags) ? options : new PasteOptions();

    public DrawRectangleModeSettings NormalizedDrawRectangleSettings =>
        (DrawRectangleSettings ?? new DrawRectangleModeSettings()).Normalized();

    public DrawEllipseModeSettings NormalizedDrawEllipseSettings =>
        (DrawEllipseSettings ?? new DrawEllipseModeSettings()).Normalized();

    public DrawLineModeSettings NormalizedDrawLineSettings =>
        (DrawLineSettings ?? new DrawLineModeSettings()).Normalized();

    public DrawCurveModeSettings NormalizedDrawCurveSettings =>
        (DrawCurveSettings ?? new DrawCurveModeSettings()).Normalized();

    public DrawGridModeSettings NormalizedDrawGridSettings =>
        (DrawGridSettings ?? new DrawGridModeSettings()).Normalized();

    public EditSelectionModeSettings NormalizedEditSelectionSettings =>
        (EditSelectionSettings ?? new EditSelectionModeSettings()).Normalized();

    public AutomapModeSettings NormalizedAutomapSettings =>
        (AutomapSettings ?? new AutomapModeSettings()).Normalized();

    public MakeDoorModeSettings NormalizedMakeDoorSettings =>
        (MakeDoorSettings ?? new MakeDoorModeSettings()).Normalized();

    /// <summary>Moves <paramref name="path"/> to the front of the recent list (de-duplicated, capped at MaxRecent).</summary>
    public void AddRecent(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        RecentFiles ??= new();
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        int maxRecent = NormalizedMaxRecentFiles;
        if (RecentFiles.Count > maxRecent) RecentFiles.RemoveRange(maxRecent, RecentFiles.Count - maxRecent);
    }

    /// <summary>Moves a map reference to the front of the recent maps list, de-duplicated and capped.</summary>
    public void AddRecentMap(string path, string mapName, string? archivePath = null)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(mapName)) return;
        RecentMaps ??= new();
        var entry = new RecentMapReference
        {
            Path = path,
            MapName = mapName,
            ArchivePath = string.IsNullOrWhiteSpace(archivePath) ? null : archivePath,
        };
        RecentMaps.RemoveAll(entry.Matches);
        RecentMaps.Insert(0, entry);
        int maxRecent = NormalizedMaxRecentFiles;
        if (RecentMaps.Count > maxRecent) RecentMaps.RemoveRange(maxRecent, RecentMaps.Count - maxRecent);
    }

    public IReadOnlyList<string> ExistingRecentFiles(Func<string, bool> fileExists)
    {
        RecentFiles ??= new();
        return RecentFiles.Where(fileExists).ToArray();
    }

    public IReadOnlyList<RecentMapReference> ExistingRecentMaps(Func<string, bool> fileExists)
    {
        RecentMaps ??= new();
        return RecentMaps.Where(map => fileExists(map.Path)).ToArray();
    }

    public void RememberMapFolderForPath(string? path, Func<string, bool>? directoryExists = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        string? folder = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(folder)) return;
        if (directoryExists is not null && !directoryExists(folder)) return;
        LastUsedMapFolder = folder;
    }

    public static string? ExistingMapFolder(string? folder, Func<string, bool> directoryExists)
    {
        if (string.IsNullOrWhiteSpace(folder)) return null;
        string trimmed = folder.Trim();
        return directoryExists(trimmed) ? trimmed : null;
    }

    public static string? ResolveStartupConfigPath(
        string? environmentPath,
        string configDir,
        string? lastUsedConfigName,
        Func<string, bool> fileExists,
        string fallbackFileName = "Doom_DoomDoom.cfg")
    {
        if (!string.IsNullOrWhiteSpace(environmentPath) && fileExists(environmentPath))
            return environmentPath;

        string? lastUsedPath = ResolveConfigPath(configDir, lastUsedConfigName);
        if (lastUsedPath is not null && fileExists(lastUsedPath))
            return lastUsedPath;

        string fallback = Path.Combine(configDir, fallbackFileName);
        return fileExists(fallback) ? fallback : null;
    }

    private static string? ResolveConfigPath(string configDir, string? configName)
    {
        if (string.IsNullOrWhiteSpace(configName)) return null;
        string trimmed = configName.Trim();
        return Path.IsPathRooted(trimmed) ? trimmed : Path.Combine(configDir, trimmed);
    }

    /// <summary>Default settings file location: &lt;app-data&gt;/DBuilder/settings.json.</summary>
    public static string DefaultPath
    {
        get
        {
            return Path.Combine(DefaultPathDirectory, "settings.json");
        }
    }

    public static string DefaultPathDirectory
    {
        get
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(baseDir))
                baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(baseDir, "DBuilder");
        }
    }

    /// <summary>Loads settings from <paramref name="path"/>, or returns defaults if it is missing or unreadable.</summary>
    public static Settings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new Settings();
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new Settings();
            settings.RecentFiles ??= new();
            settings.RecentMaps ??= new();
            settings.ShortcutOverrides ??= new();
            settings.PasteOptions ??= new();
            settings.DrawLineSettings ??= new();
            settings.DrawRectangleSettings ??= new();
            settings.DrawEllipseSettings ??= new();
            settings.DrawCurveSettings ??= new();
            settings.DrawGridSettings ??= new();
            settings.EditSelectionSettings ??= new();
            settings.AutomapSettings ??= new();
            settings.MakeDoorSettings ??= new();
            return settings;
        }
        catch { return new Settings(); }
    }

    /// <summary>Writes settings to <paramref name="path"/> (creating the directory). Returns false on failure.</summary>
    public bool Save(string path)
    {
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch { return false; }
    }
}

public sealed class RecentMapReference
{
    public string Path { get; set; } = "";
    public string MapName { get; set; } = "";
    public string? ArchivePath { get; set; }

    public bool Matches(RecentMapReference other) =>
        string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase)
        && string.Equals(MapName, other.MapName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(ArchivePath ?? "", other.ArchivePath ?? "", StringComparison.OrdinalIgnoreCase);
}

public sealed record MakeDoorModeSettings(
    bool HasValues = false,
    string? DoorTexture = null,
    string? TrackTexture = null,
    string? CeilingTexture = null,
    string? FloorTexture = null,
    bool ResetOffsets = true,
    bool ApplyActionSpecials = true,
    bool ApplyTag = false)
{
    public MakeDoorModeSettings Normalized()
        => new(
            HasValues,
            Normalize(DoorTexture),
            Normalize(TrackTexture),
            Normalize(CeilingTexture),
            Normalize(FloorTexture),
            ResetOffsets,
            ApplyActionSpecials,
            ApplyTag);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
