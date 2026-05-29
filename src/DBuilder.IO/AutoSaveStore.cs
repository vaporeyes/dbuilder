// ABOUTME: Stores recoverable autosave WAD snapshots under the DBuilder app-data directory.
// ABOUTME: Creates deterministic per-map autosave file names from source archive and map identifiers.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace DBuilder.IO;

public sealed record AutoSaveKey(string SourcePath, string MapName, string? ArchivePath = null)
{
    public string DisplayName => string.IsNullOrWhiteSpace(ArchivePath)
        ? $"{Path.GetFileName(SourcePath)}:{MapName}"
        : $"{Path.GetFileName(SourcePath)}:{ArchivePath}:{MapName}";
}

public sealed record AutoSaveEntry(AutoSaveKey Key, string SnapshotPath, DateTimeOffset LastWriteTime, string DisplayName);

public static class AutoSaveStore
{
    public const int DefaultMaxSnapshots = 25;

    public static string DefaultDirectory => Path.Combine(Settings.DefaultPathDirectory, "Autosave");

    public static string PathFor(AutoSaveKey key, string? directory = null)
    {
        directory ??= DefaultDirectory;
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(KeyText(key)))).ToLowerInvariant();
        string name = SanitizeFileName($"{key.MapName}-{hash[..12]}.wad");
        return Path.Combine(directory, name);
    }

    public static string? Write(AutoSaveKey key, byte[] wadBytes, string? directory = null)
    {
        try
        {
            string path = PathFor(key, directory);
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, wadBytes);
            File.WriteAllText(path + ".txt", MetadataText(key));
            return path;
        }
        catch
        {
            return null;
        }
    }

    public static bool Delete(AutoSaveKey key, string? directory = null)
    {
        try
        {
            string path = PathFor(key, directory);
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".txt")) File.Delete(path + ".txt");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static List<AutoSaveEntry> List(string? directory = null)
    {
        directory ??= DefaultDirectory;
        var entries = new List<AutoSaveEntry>();
        if (!Directory.Exists(directory)) return entries;

        foreach (string path in Directory.EnumerateFiles(directory, "*.wad"))
        {
            var entry = ReadEntry(path);
            if (entry is not null) entries.Add(entry);
        }

        entries.Sort((a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
        return entries;
    }

    public static int Prune(int maxSnapshots = DefaultMaxSnapshots, string? directory = null)
    {
        if (maxSnapshots < 0) throw new ArgumentOutOfRangeException(nameof(maxSnapshots));

        int deleted = 0;
        foreach (var entry in List(directory).Skip(maxSnapshots))
        {
            try
            {
                if (File.Exists(entry.SnapshotPath)) File.Delete(entry.SnapshotPath);
                string metadataPath = entry.SnapshotPath + ".txt";
                if (File.Exists(metadataPath)) File.Delete(metadataPath);
                deleted++;
            }
            catch
            {
            }
        }
        return deleted;
    }

    public static string MetadataText(AutoSaveKey key)
    {
        var text = new StringBuilder();
        text.AppendLine($"source={key.SourcePath}");
        text.AppendLine($"map={key.MapName}");
        if (!string.IsNullOrWhiteSpace(key.ArchivePath)) text.AppendLine($"archive={key.ArchivePath}");
        text.AppendLine($"display={key.DisplayName}");
        text.AppendLine($"utc={DateTimeOffset.UtcNow:O}");
        return text.ToString();
    }

    private static AutoSaveEntry? ReadEntry(string path)
    {
        try
        {
            string metadataPath = path + ".txt";
            if (!File.Exists(metadataPath)) return null;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadAllLines(metadataPath))
            {
                int equals = line.IndexOf('=');
                if (equals <= 0) continue;
                values[line[..equals]] = line[(equals + 1)..];
            }

            if (!values.TryGetValue("source", out string? source) || string.IsNullOrWhiteSpace(source)) return null;
            if (!values.TryGetValue("map", out string? map) || string.IsNullOrWhiteSpace(map)) return null;
            values.TryGetValue("archive", out string? archive);
            values.TryGetValue("display", out string? display);
            var key = new AutoSaveKey(source, map, archive);
            return new AutoSaveEntry(key, path, File.GetLastWriteTimeUtc(path),
                string.IsNullOrWhiteSpace(display) ? key.DisplayName : display);
        }
        catch
        {
            return null;
        }
    }

    private static string KeyText(AutoSaveKey key)
        => $"{key.SourcePath}\n{key.ArchivePath ?? ""}\n{key.MapName}";

    private static string SanitizeFileName(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        return name;
    }
}
