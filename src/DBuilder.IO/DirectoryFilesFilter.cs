// ABOUTME: Small filter DTO consumed by DirectoryFilesList - replaces the GameConfiguration dep used by UDB.
// ABOUTME: Holds the two collections UDB pulls from game-config: file extensions to skip entirely, and directory names whose subtrees are ignored.

using System;
using System.Collections.Generic;

namespace DBuilder.IO;

public sealed class DirectoryFilesFilter
{
    /// <summary>File extensions (without leading dot, case-insensitive) to skip when scanning a directory tree.</summary>
    public HashSet<string> IgnoredFileExtensions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Directory names (case-insensitive) whose entire subtree is skipped.</summary>
    public HashSet<string> IgnoredDirectoryNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public DirectoryFilesFilter() { }

    public DirectoryFilesFilter(IEnumerable<string>? ignoredExtensions = null, IEnumerable<string>? ignoredDirectories = null)
    {
        if (ignoredExtensions != null) foreach (var e in ignoredExtensions) IgnoredFileExtensions.Add(e);
        if (ignoredDirectories != null) foreach (var d in ignoredDirectories) IgnoredDirectoryNames.Add(d);
    }
}
