// ABOUTME: Builds UDB-style recent file menu rows from persisted file and map history.
// ABOUTME: Keeps numbering, separators, and empty-state text out of the Avalonia window code.

namespace DBuilder.IO;

public enum RecentMenuEntryKind
{
    Map,
    File,
    Separator,
    Empty,
}

public sealed record RecentMenuEntry(
    RecentMenuEntryKind Kind,
    string Header,
    string? Path = null,
    string? MapName = null,
    string? ArchivePath = null)
{
    public bool IsMap => Kind == RecentMenuEntryKind.Map;
    public bool IsFile => Kind == RecentMenuEntryKind.File;
    public bool IsSeparator => Kind == RecentMenuEntryKind.Separator;
    public bool IsEmpty => Kind == RecentMenuEntryKind.Empty;
}

public static class RecentMenuModel
{
    public const string EmptyHeader = "No recently opened files";
    public const int MaxDisplayCharacters = 80;

    public static IReadOnlyList<RecentMenuEntry> Build(Settings settings, Func<string, bool> fileExists)
    {
        var entries = new List<RecentMenuEntry>();
        IReadOnlyList<(int Index, RecentMapReference Map)> recentMaps = ExistingRecentMaps(settings, fileExists);
        IReadOnlyList<(int Index, string Path)> recentFiles = ExistingRecentFiles(settings, fileExists);
        int maxRows = settings.NormalizedMaxRecentFiles;
        int mapRows = Math.Min(recentMaps.Count, maxRows);
        int fileRows = Math.Min(recentFiles.Count, maxRows - mapRows);

        for (int i = 0; i < mapRows; i++)
        {
            (int index, RecentMapReference map) = recentMaps[i];
            entries.Add(new RecentMenuEntry(
                RecentMenuEntryKind.Map,
                NumberedHeader(index, RecentMapHeader(map)),
                map.Path,
                map.MapName,
                map.ArchivePath));
        }

        if (mapRows > 0 && fileRows > 0)
            entries.Add(new RecentMenuEntry(RecentMenuEntryKind.Separator, ""));

        for (int i = 0; i < fileRows; i++)
        {
            (int index, string path) = recentFiles[i];
            entries.Add(new RecentMenuEntry(
                RecentMenuEntryKind.File,
                NumberedHeader(index, DisplayFilename(path)),
                path));
        }

        if (entries.Count == 0)
            entries.Add(new RecentMenuEntry(RecentMenuEntryKind.Empty, EmptyHeader));

        return entries;
    }

    public static string RecentMapHeader(RecentMapReference map)
    {
        string fileName = Path.GetFileName(map.Path);
        string mapName = string.IsNullOrWhiteSpace(map.ArchivePath) ? map.MapName : $"{map.ArchivePath}:{map.MapName}";
        return $"{fileName} ({mapName})";
    }

    public static string DisplayFilename(string filename)
    {
        if (filename.Length <= MaxDisplayCharacters) return filename;

        int suffixLength = MaxDisplayCharacters - 6;
        return filename[..3] + "..." + filename[^suffixLength..];
    }

    private static string NumberedHeader(int index, string text)
        => $"&{index + 1}  {text}";

    private static IReadOnlyList<(int Index, string Path)> ExistingRecentFiles(Settings settings, Func<string, bool> fileExists)
    {
        settings.RecentFiles ??= new();
        return settings.RecentFiles
            .Select((path, index) => (Index: index, Path: path))
            .Take(settings.NormalizedMaxRecentFiles)
            .Where(entry => fileExists(entry.Path))
            .ToArray();
    }

    private static IReadOnlyList<(int Index, RecentMapReference Map)> ExistingRecentMaps(Settings settings, Func<string, bool> fileExists)
    {
        settings.RecentMaps ??= new();
        return settings.RecentMaps
            .Select((map, index) => (Index: index, Map: map))
            .Take(settings.NormalizedMaxRecentFiles)
            .Where(entry => fileExists(entry.Map.Path))
            .ToArray();
    }
}
