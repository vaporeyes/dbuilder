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
    public const string EmptyHeader = "(none)";

    public static IReadOnlyList<RecentMenuEntry> Build(Settings settings, Func<string, bool> fileExists)
    {
        var entries = new List<RecentMenuEntry>();
        IReadOnlyList<RecentMapReference> recentMaps = settings.ExistingRecentMaps(fileExists);
        IReadOnlyList<string> recentFiles = settings.ExistingRecentFiles(fileExists);

        for (int i = 0; i < recentMaps.Count; i++)
        {
            RecentMapReference map = recentMaps[i];
            entries.Add(new RecentMenuEntry(
                RecentMenuEntryKind.Map,
                NumberedHeader(i, RecentMapHeader(map)),
                map.Path,
                map.MapName,
                map.ArchivePath));
        }

        if (recentMaps.Count > 0 && recentFiles.Count > 0)
            entries.Add(new RecentMenuEntry(RecentMenuEntryKind.Separator, ""));

        for (int i = 0; i < recentFiles.Count; i++)
        {
            string path = recentFiles[i];
            entries.Add(new RecentMenuEntry(
                RecentMenuEntryKind.File,
                NumberedHeader(i, path),
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

    private static string NumberedHeader(int index, string text)
        => $"&{index + 1}  {text}";
}
