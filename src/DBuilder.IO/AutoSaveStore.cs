// ABOUTME: Stores recoverable autosave WAD snapshots under the DBuilder app-data directory.
// ABOUTME: Creates deterministic per-map autosave file names from source archive and map identifiers.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DBuilder.IO;

public sealed record AutoSaveKey(string SourcePath, string MapName, string? ArchivePath = null)
{
    public string DisplayName => string.IsNullOrWhiteSpace(ArchivePath)
        ? $"{Path.GetFileName(SourcePath)}:{MapName}"
        : $"{Path.GetFileName(SourcePath)}:{ArchivePath}:{MapName}";
}

public static class AutoSaveStore
{
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

    private static string KeyText(AutoSaveKey key)
        => $"{key.SourcePath}\n{key.ArchivePath ?? ""}\n{key.MapName}";

    private static string SanitizeFileName(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        return name;
    }
}
