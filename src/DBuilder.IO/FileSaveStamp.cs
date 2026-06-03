// ABOUTME: Tracks a file's save-time identity so editor saves can detect external source changes.
// ABOUTME: Uses timestamp and length metadata only; callers decide how to handle a detected mismatch.

using System;
using System.IO;

namespace DBuilder.IO;

public readonly record struct FileSaveStamp(DateTime LastWriteTimeUtc, long Length)
{
    public static bool TryRead(string? path, out FileSaveStamp stamp)
    {
        stamp = default;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;

        var info = new FileInfo(path);
        stamp = new FileSaveStamp(info.LastWriteTimeUtc, info.Length);
        return true;
    }

    public static bool HasChanged(string? path, FileSaveStamp? expected)
    {
        if (expected is null) return false;
        return !TryRead(path, out var current) || current != expected.Value;
    }

    public static bool IsReadOnly(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        return File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly);
    }

    public static bool CanWriteExistingPath(string? path)
        => !IsReadOnly(path);
}
