// ABOUTME: Persisted editor settings for paths, test launchers, recent files, and recent maps.
// ABOUTME: Serialized as JSON under the user's application-data folder; load is best-effort and never throws.

using System;
using System.Collections.Generic;
using System.Globalization;
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
    public const int DefaultTestSkill = 3;
    public const int MinTestSkill = 1;
    public const int MaxTestSkill = 5;
    public const int DefaultAutosaveCount = 5;
    public const int MinAutosaveCount = 1;
    public const int MaxAutosaveCount = 50;
    public const int DefaultAutosaveIntervalMinutes = 5;
    public const int MinAutosaveIntervalMinutes = 1;
    public const int MaxAutosaveIntervalMinutes = 60;
    public const int DefaultSectorFloorHeight = 0;
    public const int DefaultSectorCeilingHeight = 128;
    public const int DefaultSectorBrightness = 192;
    public const int DefaultImageBrightness = 3;
    public const int MinImageBrightness = 0;
    public const int MaxImageBrightness = 10;
    public const double DefaultDoubleSidedAlpha = 0.4;
    public const byte DefaultDoubleSidedAlphaByte = 102;
    public const int DefaultVisualFov = 80;
    public const int MinVisualFov = 50;
    public const int MaxVisualFov = 170;
    public const int DefaultViewDistance = 3000;
    public const int MinViewDistance = 500;
    public const int MaxViewDistance = 64000;
    public const int DefaultMoveSpeed = 100;
    public const int MinMoveSpeed = 100;
    public const int MaxMoveSpeed = 2000;
    public const int DefaultMouseSpeed = 100;
    public const int MinMouseSpeed = 100;
    public const int MaxMouseSpeed = 2000;
    public const int DefaultMouseSelectionThreshold = 2;
    public const int DefaultStitchRange = 20;
    public const int DefaultHighlightRange = 20;
    public const int DefaultThingHighlightRange = 10;
    public const int DefaultSplitLinedefsRange = 10;
    public const int DefaultAutoScrollSpeed = 0;
    public const int MinAutoScrollSpeed = 0;
    public const int MaxAutoScrollSpeed = 5;

    public string? ConfigDir { get; set; }
    public string? LastUsedConfigName { get; set; }
    public string? LastUsedMapFolder { get; set; }
    public string? NodeBuilderPath { get; set; }
    public string? NodeBuilderArgs { get; set; }
    public string? TestPort { get; set; }
    public string? TestPortArgs { get; set; }
    public string? TestAdditionalParameters { get; set; }
    public string? TestIwad { get; set; }
    public int? TestSkill { get; set; }
    public bool TestMonsters { get; set; } = true;
    public string? UdbScriptExternalEditor { get; set; }
    public Dictionary<string, object?> UdbScriptSettings { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, object?> UsdfDialogEditorSettings { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, object?> TabbedDockerLayoutSettings { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, Dictionary<string, object?>> PluginSettings { get; set; } = new(StringComparer.Ordinal);
    public int? MaxRecentFiles { get; set; }
    public bool Autosave { get; set; } = true;
    public int? AutosaveCount { get; set; }
    public int? AutosaveIntervalMinutes { get; set; }
    public int? DefaultSectorFloorHeightSetting { get; set; }
    public int? DefaultSectorCeilingHeightSetting { get; set; }
    public int? DefaultSectorBrightnessSetting { get; set; }
    public bool AutoClearSidedefTextures { get; set; } = true;
    public bool AutoMerge { get; set; } = true;
    public bool SplitJoinedSectors { get; set; } = true;
    public bool AutoClearSelection { get; set; }
    public bool EditNewThing { get; set; } = true;
    public bool EditNewSector { get; set; }
    public bool RenderGrid { get; set; } = true;
    public bool DynamicGridSize { get; set; } = true;
    public bool SwitchViewModes { get; set; }
    public bool FixedThingsScale { get; set; }
    public bool AlwaysShowVertices { get; set; } = true;
    public int? DefaultViewMode { get; set; }
    public int? ModelRenderMode { get; set; }
    public int? LightRenderMode { get; set; }
    public bool EnhancedRenderingEffects { get; set; } = true;
    public bool ClassicRendering { get; set; }
    public int? ImageBrightness { get; set; }
    public double? DoubleSidedAlpha { get; set; }
    public int? VisualFov { get; set; }
    public int? ViewDistance { get; set; }
    public int? MoveSpeed { get; set; }
    public int? MouseSpeed { get; set; }
    public int? MouseSelectionThreshold { get; set; }
    public int? StitchRange { get; set; }
    public int? HighlightRange { get; set; }
    public int? ThingHighlightRange { get; set; }
    public int? SplitLinedefsRange { get; set; }
    public int? AutoScrollSpeed { get; set; }
    public bool QualityDisplay { get; set; } = true;
    public bool ClassicBilinear { get; set; }
    public bool VisualBilinear { get; set; }
    public bool BlackBrowsers { get; set; } = true;
    public bool FlatShadeVertices { get; set; }
    public bool MarkExtraFloors { get; set; } = true;
    public bool DrawFog { get; set; }
    public bool DrawSky { get; set; } = true;
    public bool ShowEventLines { get; set; } = true;
    public bool ShowVisualVertices { get; set; } = true;
    public bool ShowErrorsWindow { get; set; } = true;
    public bool UseHighlight { get; set; } = true;
    public bool AlphaBasedTextureHighlighting { get; set; } = true;
    public bool SelectAdjacentVisualVertexSlopeHandles { get; set; }
    public bool UseOppositeSmartPivotHandle { get; set; } = true;
    public bool ToastsEnabled { get; set; } = true;
    public ToastAnchor ToastAnchor { get; set; } = ToastPreferences.DefaultAnchor;
    public int? ToastDurationMilliseconds { get; set; }
    public Dictionary<string, bool> ToastActionSettings { get; set; } = new(StringComparer.Ordinal);
    public int? StatusHistoryLimit { get; set; }
    public MergeGeometryMode MergeGeometryMode { get; set; } = MergeGeometryMode.Replace;
    public PasteOptions PasteOptions { get; set; } = new();
    public DrawLineModeSettings DrawLineSettings { get; set; } = new();
    public DrawRectangleModeSettings DrawRectangleSettings { get; set; } = new();
    public DrawEllipseModeSettings DrawEllipseSettings { get; set; } = new();
    public DrawCurveModeSettings DrawCurveSettings { get; set; } = new();
    public CurveLinedefsOptions CurveLinedefsSettings { get; set; } = new();
    public DrawGridModeSettings DrawGridSettings { get; set; } = new();
    public EditSelectionModeSettings EditSelectionSettings { get; set; } = new();
    public AutomapModeSettings AutomapSettings { get; set; } = new();
    public List<LinedefColorPreset> LinedefColorPresets { get; set; } = new();
    public ThreeDFloorControlSectorAreaSettings ThreeDFloorControlSectorAreaSettings { get; set; } = new();
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
    public VisplaneExplorerInterfaceSettings VisplaneExplorerSettings { get; set; } = new(false, false, VisplaneExplorerStat.Visplanes, 41, 0);
    public double? WindowX { get; set; }
    public double? WindowY { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public List<string> RecentFiles { get; set; } = new();
    public List<RecentMapReference> RecentMaps { get; set; } = new();
    public List<EditorShortcutBinding> ShortcutOverrides { get; set; } = new();
    public Dictionary<string, bool> MapErrorCheckSettings { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<DataLocation>> ConfigurationResources { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int NormalizedStatusHistoryLimit =>
        Math.Clamp(StatusHistoryLimit ?? DefaultStatusHistoryLimit, MinStatusHistoryLimit, MaxStatusHistoryLimit);

    public int NormalizedMaxRecentFiles =>
        Math.Clamp(MaxRecentFiles ?? DefaultMaxRecentFiles, MinMaxRecentFiles, MaxMaxRecentFiles);

    public int NormalizedTestSkill =>
        Math.Clamp(TestSkill ?? DefaultTestSkill, MinTestSkill, MaxTestSkill);

    public int NormalizedAutosaveCount =>
        Math.Clamp(AutosaveCount ?? DefaultAutosaveCount, MinAutosaveCount, MaxAutosaveCount);

    public int NormalizedAutosaveIntervalMinutes =>
        Math.Clamp(AutosaveIntervalMinutes ?? DefaultAutosaveIntervalMinutes, MinAutosaveIntervalMinutes, MaxAutosaveIntervalMinutes);

    public int NormalizedDefaultSectorFloorHeight =>
        DefaultSectorFloorHeightSetting ?? DefaultSectorFloorHeight;

    public int NormalizedDefaultSectorCeilingHeight =>
        DefaultSectorCeilingHeightSetting ?? DefaultSectorCeilingHeight;

    public int NormalizedDefaultSectorBrightness =>
        Math.Clamp(DefaultSectorBrightnessSetting ?? DefaultSectorBrightness, 0, 255);

    public int NormalizedImageBrightness =>
        Math.Clamp(ImageBrightness ?? DefaultImageBrightness, MinImageBrightness, MaxImageBrightness);

    public double NormalizedDoubleSidedAlpha =>
        Math.Clamp(DoubleSidedAlpha ?? DefaultDoubleSidedAlpha, 0.0, 1.0);

    public byte NormalizedDoubleSidedAlphaByte =>
        AlphaToByte(NormalizedDoubleSidedAlpha);

    public int NormalizedVisualFov =>
        Math.Clamp(VisualFov ?? DefaultVisualFov, MinVisualFov, MaxVisualFov);

    public int NormalizedViewDistance =>
        Math.Clamp(ViewDistance ?? DefaultViewDistance, MinViewDistance, MaxViewDistance);

    public int NormalizedMoveSpeed =>
        Math.Clamp(MoveSpeed ?? DefaultMoveSpeed, MinMoveSpeed, MaxMoveSpeed);

    public int NormalizedMouseSpeed =>
        Math.Clamp(MouseSpeed ?? DefaultMouseSpeed, MinMouseSpeed, MaxMouseSpeed);

    public int NormalizedMouseSelectionThreshold =>
        Math.Max(0, MouseSelectionThreshold ?? DefaultMouseSelectionThreshold);

    public int NormalizedStitchRange =>
        Math.Max(0, StitchRange ?? DefaultStitchRange);

    public int NormalizedHighlightRange =>
        Math.Max(0, HighlightRange ?? DefaultHighlightRange);

    public int NormalizedThingHighlightRange =>
        Math.Max(0, ThingHighlightRange ?? DefaultThingHighlightRange);

    public int NormalizedSplitLinedefsRange =>
        Math.Max(0, SplitLinedefsRange ?? DefaultSplitLinedefsRange);

    public int NormalizedAutoScrollSpeed =>
        Math.Clamp(AutoScrollSpeed ?? DefaultAutoScrollSpeed, MinAutoScrollSpeed, MaxAutoScrollSpeed);

    public ToastAnchor NormalizedToastAnchor =>
        ToastPreferences.NormalizeAnchor(ToastAnchor);

    public int NormalizedToastDurationMilliseconds =>
        ToastPreferences.NormalizeDurationMilliseconds(ToastDurationMilliseconds);

    public static int? AcceptMaxRecentFilesText(string? text)
    {
        if (!int.TryParse(text, out int value) || value <= 0) return null;
        return Math.Clamp(value, MinMaxRecentFiles, MaxMaxRecentFiles);
    }

    public static int? AcceptStatusHistoryLimitText(string? text)
    {
        if (!int.TryParse(text, out int value) || value <= 0) return null;
        return Math.Clamp(value, MinStatusHistoryLimit, MaxStatusHistoryLimit);
    }

    public static int? AcceptTestSkillText(string? text)
    {
        if (!int.TryParse(text, out int value) || value <= 0) return null;
        return Math.Clamp(value, MinTestSkill, MaxTestSkill);
    }

    public static int? AcceptAutosaveCountText(string? text)
    {
        if (!int.TryParse(text, out int value) || value <= 0) return null;
        return Math.Clamp(value, MinAutosaveCount, MaxAutosaveCount);
    }

    public static int? AcceptAutosaveIntervalText(string? text)
    {
        if (!int.TryParse(text, out int value) || value <= 0) return null;
        return Math.Clamp(value, MinAutosaveIntervalMinutes, MaxAutosaveIntervalMinutes);
    }

    public static int? AcceptSectorHeightText(string? text)
        => int.TryParse(text, out int value) ? value : null;

    public static int? AcceptSectorBrightnessText(string? text)
        => int.TryParse(text, out int value) ? Math.Clamp(value, 0, 255) : null;

    public static int? AcceptImageBrightnessText(string? text)
        => int.TryParse(text, out int value) ? Math.Clamp(value, MinImageBrightness, MaxImageBrightness) : null;

    public static double? AcceptDoubleSidedAlphaText(string? text)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) return null;
        if (value > 1.0) value /= 100.0;
        return Math.Clamp(value, 0.0, 1.0);
    }

    public static int? AcceptVisualFovText(string? text)
        => int.TryParse(text, out int value) ? Math.Clamp(value, MinVisualFov, MaxVisualFov) : null;

    public static int? AcceptViewDistanceText(string? text)
        => int.TryParse(text, out int value) ? Math.Clamp(value, MinViewDistance, MaxViewDistance) : null;

    public static int? AcceptMoveSpeedText(string? text)
        => int.TryParse(text, out int value) ? Math.Clamp(value, MinMoveSpeed, MaxMoveSpeed) : null;

    public static int? AcceptMouseSpeedText(string? text)
        => int.TryParse(text, out int value) ? Math.Clamp(value, MinMouseSpeed, MaxMouseSpeed) : null;

    public static int? AcceptMouseSelectionThresholdText(string? text)
        => int.TryParse(text, out int value) ? Math.Max(0, value) : null;

    public static int? AcceptStitchRangeText(string? text)
        => int.TryParse(text, out int value) ? Math.Max(0, value) : null;

    public static int? AcceptHighlightRangeText(string? text)
        => int.TryParse(text, out int value) ? Math.Max(0, value) : null;

    public static int? AcceptThingHighlightRangeText(string? text)
        => int.TryParse(text, out int value) ? Math.Max(0, value) : null;

    public static int? AcceptSplitLinedefsRangeText(string? text)
        => int.TryParse(text, out int value) ? Math.Max(0, value) : null;

    public static int? AcceptAutoScrollSpeedText(string? text)
        => int.TryParse(text, out int value) ? Math.Clamp(value, MinAutoScrollSpeed, MaxAutoScrollSpeed) : null;

    public static byte AlphaToByte(double alpha)
        => (byte)(Math.Clamp(alpha, 0.0, 1.0) * 255.0);

    public static string MaxRecentFilesText(Settings settings)
        => settings.NormalizedMaxRecentFiles.ToString(CultureInfo.InvariantCulture);

    public static string StatusHistoryLimitText(Settings settings)
        => settings.NormalizedStatusHistoryLimit.ToString(CultureInfo.InvariantCulture);

    public static string TestSkillText(Settings settings)
        => settings.NormalizedTestSkill.ToString(CultureInfo.InvariantCulture);

    public static string AutosaveCountText(Settings settings)
        => settings.NormalizedAutosaveCount.ToString(CultureInfo.InvariantCulture);

    public static string AutosaveIntervalText(Settings settings)
        => settings.NormalizedAutosaveIntervalMinutes.ToString(CultureInfo.InvariantCulture);

    public static string DefaultSectorFloorHeightText(Settings settings)
        => settings.NormalizedDefaultSectorFloorHeight.ToString(CultureInfo.InvariantCulture);

    public static string DefaultSectorCeilingHeightText(Settings settings)
        => settings.NormalizedDefaultSectorCeilingHeight.ToString(CultureInfo.InvariantCulture);

    public static string DefaultSectorBrightnessText(Settings settings)
        => settings.NormalizedDefaultSectorBrightness.ToString(CultureInfo.InvariantCulture);

    public static string ImageBrightnessText(Settings settings)
        => settings.NormalizedImageBrightness.ToString(CultureInfo.InvariantCulture);

    public static string DoubleSidedAlphaText(Settings settings)
        => (settings.NormalizedDoubleSidedAlpha * 100.0).ToString("0.###", CultureInfo.InvariantCulture);

    public static string VisualFovText(Settings settings)
        => settings.NormalizedVisualFov.ToString(CultureInfo.InvariantCulture);

    public static string ViewDistanceText(Settings settings)
        => settings.NormalizedViewDistance.ToString(CultureInfo.InvariantCulture);

    public static string MoveSpeedText(Settings settings)
        => settings.NormalizedMoveSpeed.ToString(CultureInfo.InvariantCulture);

    public static string MouseSpeedText(Settings settings)
        => settings.NormalizedMouseSpeed.ToString(CultureInfo.InvariantCulture);

    public static string MouseSelectionThresholdText(Settings settings)
        => settings.NormalizedMouseSelectionThreshold.ToString(CultureInfo.InvariantCulture);

    public static string StitchRangeText(Settings settings)
        => settings.NormalizedStitchRange.ToString(CultureInfo.InvariantCulture);

    public static string HighlightRangeText(Settings settings)
        => settings.NormalizedHighlightRange.ToString(CultureInfo.InvariantCulture);

    public static string ThingHighlightRangeText(Settings settings)
        => settings.NormalizedThingHighlightRange.ToString(CultureInfo.InvariantCulture);

    public static string SplitLinedefsRangeText(Settings settings)
        => settings.NormalizedSplitLinedefsRange.ToString(CultureInfo.InvariantCulture);

    public static string AutoScrollSpeedText(Settings settings)
        => settings.NormalizedAutoScrollSpeed == 0
            ? "0"
            : settings.NormalizedAutoScrollSpeed.ToString(CultureInfo.InvariantCulture);

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

    public CurveLinedefsOptions NormalizedCurveLinedefsSettings =>
        (CurveLinedefsSettings ?? new CurveLinedefsOptions()).Normalized();

    public DrawGridModeSettings NormalizedDrawGridSettings =>
        (DrawGridSettings ?? new DrawGridModeSettings()).Normalized();

    public EditSelectionModeSettings NormalizedEditSelectionSettings =>
        (EditSelectionSettings ?? new EditSelectionModeSettings()).Normalized();

    public AutomapModeSettings NormalizedAutomapSettings =>
        (AutomapSettings ?? new AutomapModeSettings()).Normalized();

    public IReadOnlyList<LinedefColorPreset> NormalizedLinedefColorPresets =>
        LinedefColorPresetModel.NormalizedPresets(LinedefColorPresets);

    public ThreeDFloorControlSectorAreaSettings NormalizedThreeDFloorControlSectorAreaSettings =>
        ThreeDFloorControlSectorAreaSettings ?? new ThreeDFloorControlSectorAreaSettings();

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
        return RecentFiles
            .Where(fileExists)
            .Take(NormalizedMaxRecentFiles)
            .ToArray();
    }

    public IReadOnlyList<RecentMapReference> ExistingRecentMaps(Func<string, bool> fileExists)
    {
        RecentMaps ??= new();
        return RecentMaps
            .Where(map => fileExists(map.Path))
            .Take(NormalizedMaxRecentFiles)
            .ToArray();
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

    public IReadOnlyList<MapErrorCheckerDescriptor> EnabledMapErrorCheckers()
    {
        MapErrorCheckSettings ??= new(StringComparer.Ordinal);
        return MapAnalysis.CheckerDescriptors
            .Where(descriptor => MapErrorCheckSettings.TryGetValue(descriptor.SettingsKey, out bool enabled)
                ? enabled
                : descriptor.DefaultChecked)
            .ToArray();
    }

    public MapErrorCheckerSelectionModel MapErrorCheckerSelection()
    {
        MapErrorCheckSettings ??= new(StringComparer.Ordinal);
        return new MapErrorCheckerSelectionModel(MapAnalysis.CheckerDescriptors, MapErrorCheckSettings);
    }

    public void ApplyMapErrorCheckerSelection(MapErrorCheckerSelectionModel selection)
    {
        MapErrorCheckSettings = new Dictionary<string, bool>(selection.ToSettings(), StringComparer.Ordinal);
    }

    public void SetMapErrorCheckerEnabled(MapErrorCheckerDescriptor descriptor, bool enabled)
    {
        MapErrorCheckSettings ??= new(StringComparer.Ordinal);
        MapErrorCheckSettings[descriptor.SettingsKey] = enabled;
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
            settings.MapErrorCheckSettings ??= new(StringComparer.Ordinal);
            settings.ToastActionSettings ??= new(StringComparer.Ordinal);
            settings.ConfigurationResources ??= new(StringComparer.OrdinalIgnoreCase);
            settings.UdbScriptSettings ??= new(StringComparer.Ordinal);
            settings.UsdfDialogEditorSettings ??= new(StringComparer.Ordinal);
            settings.TabbedDockerLayoutSettings ??= new(StringComparer.Ordinal);
            settings.PluginSettings = DBuilderPluginHostModel.NormalizeSettingsStore(settings.PluginSettings);
            NormalizeConfigurationResources(settings.ConfigurationResources);
            settings.PasteOptions ??= new();
            settings.DrawLineSettings ??= new();
            settings.DrawRectangleSettings ??= new();
            settings.DrawEllipseSettings ??= new();
            settings.DrawCurveSettings ??= new();
            settings.CurveLinedefsSettings ??= new();
            settings.DrawGridSettings ??= new();
            settings.EditSelectionSettings ??= new();
            settings.AutomapSettings ??= new();
            settings.LinedefColorPresets ??= new();
            settings.ThreeDFloorControlSectorAreaSettings ??= new();
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
            settings.VisplaneExplorerSettings ??= new(false, false, VisplaneExplorerStat.Visplanes, 41, 0);
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
