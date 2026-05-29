// ABOUTME: Persisted editor settings for paths, test launchers, recent files, and recent maps.
// ABOUTME: Serialized as JSON under the user's application-data folder; load is best-effort and never throws.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DBuilder.IO;

public sealed class Settings
{
    public const int MaxRecent = 10;

    public string? ConfigDir { get; set; }
    public string? NodeBuilderPath { get; set; }
    public string? NodeBuilderArgs { get; set; }
    public string? TestPort { get; set; }
    public string? TestPortArgs { get; set; }
    public string? TestIwad { get; set; }
    public List<string> RecentFiles { get; set; } = new();
    public List<RecentMapReference> RecentMaps { get; set; } = new();

    /// <summary>Moves <paramref name="path"/> to the front of the recent list (de-duplicated, capped at MaxRecent).</summary>
    public void AddRecent(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        RecentFiles ??= new();
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > MaxRecent) RecentFiles.RemoveRange(MaxRecent, RecentFiles.Count - MaxRecent);
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
        if (RecentMaps.Count > MaxRecent) RecentMaps.RemoveRange(MaxRecent, RecentMaps.Count - MaxRecent);
    }

    /// <summary>Default settings file location: &lt;app-data&gt;/DBuilder/settings.json.</summary>
    public static string DefaultPath
    {
        get
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(baseDir))
                baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(baseDir, "DBuilder", "settings.json");
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
