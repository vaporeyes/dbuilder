// ABOUTME: Builds game-configuration picker rows from cfg files for editor dialogs.
// ABOUTME: Keeps current-config selection stable across parsed titles, filenames, and external paths.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DBuilder.IO;

public sealed record ConfigPickerRow(string Title, string Engine, string Path)
{
    public string FileName => System.IO.Path.GetFileName(Path);
    public string FileNameWithoutExtension => System.IO.Path.GetFileNameWithoutExtension(Path);

    public override string ToString()
        => string.IsNullOrWhiteSpace(Engine) ? Title : $"{Title} [{Engine}]";
}

public static class ConfigPickerModel
{
    public static List<ConfigPickerRow> LoadRows(string configDir)
    {
        if (!Directory.Exists(configDir)) return new List<ConfigPickerRow>();

        return Directory.EnumerateFiles(configDir, "*.cfg", SearchOption.AllDirectories)
            .Where(path => !IsIncludeFile(configDir, path))
            .Select(ReadRow)
            .OrderBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static int SelectedIndex(IReadOnlyList<ConfigPickerRow> rows, string currentNameOrPath)
    {
        if (rows.Count == 0) return -1;
        string current = currentNameOrPath.Trim();
        if (current.Length == 0) return 0;

        int index = FindIndex(rows, row =>
            string.Equals(row.Title, current, StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.FileNameWithoutExtension, current, StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.FileName, current, StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.Path, current, StringComparison.OrdinalIgnoreCase));

        return index >= 0 ? index : 0;
    }

    private static bool IsIncludeFile(string configDir, string path)
    {
        string relative = Path.GetRelativePath(configDir, path);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => string.Equals(part, "Includes", StringComparison.OrdinalIgnoreCase));
    }

    private static ConfigPickerRow ReadRow(string path)
    {
        string fallback = Path.GetFileNameWithoutExtension(path);
        try
        {
            var cfg = new Configuration(path);
            string title = cfg.ReadSetting("game", fallback) ?? fallback;
            string engine = cfg.ReadSetting("engine", "") ?? "";
            return new ConfigPickerRow(title, engine, path);
        }
        catch
        {
            return new ConfigPickerRow(fallback, "", path);
        }
    }

    private static int FindIndex(IReadOnlyList<ConfigPickerRow> rows, Func<ConfigPickerRow, bool> predicate)
    {
        for (int i = 0; i < rows.Count; i++)
            if (predicate(rows[i])) return i;
        return -1;
    }
}
