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
    public string? UdbScriptExternalEditor { get; set; }
    public int? MaxRecentFiles { get; set; }
    public bool AutoClearSidedefTextures { get; set; } = true;
    public bool DynamicGridSize { get; set; } = true;
    public int? DefaultViewMode { get; set; }
    public int? ModelRenderMode { get; set; }
    public int? LightRenderMode { get; set; }
    public bool EnhancedRenderingEffects { get; set; } = true;
    public bool ClassicRendering { get; set; }
    public bool DrawFog { get; set; }
    public bool DrawSky { get; set; } = true;
    public bool ShowEventLines { get; set; } = true;
    public bool ShowVisualVertices { get; set; } = true;
    public bool UseHighlight { get; set; } = true;
    public bool AlphaBasedTextureHighlighting { get; set; } = true;
    public bool SelectAdjacentVisualVertexSlopeHandles { get; set; }
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
    public TagRangeStoredOptions TagRangeSettings { get; set; } = new();
    public CommentsPanelPersistedSettings CommentsPanelSettings { get; set; } = new(false, false);
    public TagExplorerPersistedSettings TagExplorerSettings { get; set; } = new(
        TagExplorerDisplayMode.TagsAndActions,
        TagExplorerSortMode.ByIndex,
        CommentsOnly: false,
        CenterOnSelected: false,
        SelectOnClick: false);
    public RejectExplorerColorSettings RejectExplorerColors { get; set; } = RejectExplorerModel.DefaultColors;
    public SoundPropagationColorSettings SoundPropagationColors { get; set; } = SoundPropagationColorSettings.Default;
    public List<StairBuilderPrefab> StairBuilderPrefabs { get; set; } = new();
    public VisplaneExplorerInterfaceSettings VisplaneExplorerSettings { get; set; } = new(false, false, 41, 0);
    public double? WindowX { get; set; }
    public double? WindowY { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public List<string> RecentFiles { get; set; } = new();
    public List<RecentMapReference> RecentMaps { get; set; } = new();
    public List<EditorShortcutBinding> ShortcutOverrides { get; set; } = new();
    public Dictionary<string, List<DataLocation>> ConfigurationResources { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int NormalizedStatusHistoryLimit =>
        Math.Clamp(StatusHistoryLimit ?? DefaultStatusHistoryLimit, MinStatusHistoryLimit, MaxStatusHistoryLimit);

    public int NormalizedMaxRecentFiles =>
        Math.Clamp(MaxRecentFiles ?? DefaultMaxRecentFiles, MinMaxRecentFiles, MaxMaxRecentFiles);

    public int NormalizedDefaultViewMode =>
        Math.Clamp(DefaultViewMode ?? 0, 0, 3);

    public ThingModelRenderMode NormalizedModelRenderMode =>
        Enum.IsDefined(typeof(ThingModelRenderMode), ModelRenderMode ?? (int)ThingModelRenderMode.All)
            ? (ThingModelRenderMode)(ModelRenderMode ?? (int)ThingModelRenderMode.All)
            : ThingModelRenderMode.All;

    public ThingLightRenderMode NormalizedLightRenderMode =>
        Enum.IsDefined(typeof(ThingLightRenderMode), LightRenderMode ?? (int)ThingLightRenderMode.All)
            ? (ThingLightRenderMode)(LightRenderMode ?? (int)ThingLightRenderMode.All)
            : ThingLightRenderMode.All;

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

    public TagRangeStoredOptions NormalizedTagRangeSettings =>
        TagRangeModel.NormalizeStoredOptions(TagRangeSettings);

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

    public DataLocationList ResourcesForConfiguration(string configNameOrPath)
    {
        ConfigurationResources ??= new(StringComparer.OrdinalIgnoreCase);
        string key = ConfigurationResourceKey(configNameOrPath);
        if (key.Length == 0 || !ConfigurationResources.TryGetValue(key, out var resources))
            return new DataLocationList();
        return new DataLocationList(resources);
    }

    public void SetResourcesForConfiguration(string configNameOrPath, IEnumerable<DataLocation> resources)
    {
        ConfigurationResources ??= new(StringComparer.OrdinalIgnoreCase);
        string key = ConfigurationResourceKey(configNameOrPath);
        if (key.Length == 0) return;

        var list = resources.Select(location => location.Clone()).ToList();
        if (list.Count == 0) ConfigurationResources.Remove(key);
        else ConfigurationResources[key] = list;
    }

    public static string ConfigurationResourceKey(string? configNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(configNameOrPath)) return "";
        string trimmed = configNameOrPath.Trim();
        string fileName = Path.GetFileNameWithoutExtension(trimmed);
        return (fileName.Length == 0 ? trimmed : fileName).ToLowerInvariant();
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
            settings.ConfigurationResources ??= new(StringComparer.OrdinalIgnoreCase);
            NormalizeConfigurationResources(settings.ConfigurationResources);
            settings.PasteOptions ??= new();
            settings.DrawLineSettings ??= new();
            settings.DrawRectangleSettings ??= new();
            settings.DrawEllipseSettings ??= new();
            settings.DrawCurveSettings ??= new();
            settings.DrawGridSettings ??= new();
            settings.EditSelectionSettings ??= new();
            settings.AutomapSettings ??= new();
            settings.MakeDoorSettings ??= new();
            settings.TagRangeSettings = TagRangeModel.NormalizeStoredOptions(settings.TagRangeSettings);
            settings.CommentsPanelSettings ??= new(false, false);
            settings.TagExplorerSettings ??= new(
                TagExplorerDisplayMode.TagsAndActions,
                TagExplorerSortMode.ByIndex,
                CommentsOnly: false,
                CenterOnSelected: false,
                SelectOnClick: false);
            settings.RejectExplorerColors ??= RejectExplorerModel.DefaultColors;
            settings.SoundPropagationColors ??= SoundPropagationColorSettings.Default;
            settings.StairBuilderPrefabs ??= new();
            settings.VisplaneExplorerSettings ??= new(false, false, 41, 0);
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

    private static void NormalizeConfigurationResources(Dictionary<string, List<DataLocation>> resources)
    {
        foreach (var key in resources.Keys.ToArray())
        {
            if (resources[key] is null) resources[key] = new List<DataLocation>();
            foreach (var location in resources[key])
                location.RequiredArchives ??= new List<string>();
        }
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
