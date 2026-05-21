// ABOUTME: DirectoryFilesList ported from UDB Source/Core/IO/DirectoryFilesList.cs.
// ABOUTME: Indexes a directory tree by case-insensitive file path; segregates WAD files at the root from other resources; provides title/extension/path-based lookups used by PK3-style resource readers.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 *
 * Porting changes from UDB:
 *  - GameConfiguration dep replaced by DirectoryFilesFilter DTO (just the two ignored-collections UDB consumed)
 *  - DataManager.CLASIC_IMAGE_NAME_LENGTH replaced by Lump.CLASSIC_NAME_LENGTH
 *  - General.ErrorLogger replaced by Console.WriteLine (gated on the existing 'silent' parameter)
 *  - Promoted from internal to public for cross-assembly use and testability
 */

using System;
using System.Collections.Generic;
using System.IO;

namespace DBuilder.IO;

/// <summary>
/// Compares two paths case-insensitively after normalizing both separator conventions to the platform's.
/// Used as the equality comparer for the entry dictionary so callers can look up files using either '/' or '\' separators
/// and arbitrary case (important for cross-platform PK3 emulation).
/// </summary>
internal sealed class PathEqualityComparer : IEqualityComparer<string>
{
    public bool Equals(string? s1, string? s2)
    {
        if (s1 == null || s2 == null) return s1 == s2;
        return ConvertPath(s1) == ConvertPath(s2);
    }

    public int GetHashCode(string s) => ConvertPath(s).GetHashCode();

    private static string ConvertPath(string s) =>
        s.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar).ToLowerInvariant();
}

public sealed class DirectoryFilesList
{
    private Dictionary<string, DirectoryFileEntry> entries; //mxd
    private List<string> wadentries; //mxd

    public int Count => entries.Count;

    /// <summary>Scan <paramref name="path"/> and optionally its subdirectories for files; segregates root-level .wad files separately.</summary>
    public DirectoryFilesList(string path, DirectoryFilesFilter? filter, bool subdirectories, FileTitleStyle filetitlestyle)
    {
        path = Path.GetFullPath(path);
        string[] files = Directory.GetFiles(path, "*", subdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        Array.Sort(files); //mxd - deterministic order
        entries = new Dictionary<string, DirectoryFileEntry>(files.Length, new PathEqualityComparer());
        wadentries = new List<string>();

        foreach (string file in files) //mxd
        {
            var e = new DirectoryFileEntry(file, path, filetitlestyle);
            // Root-level .wad files go to a separate list so PK3-style consumers can load them as nested WADs.
            if (string.Compare(e.extension, "wad", true) == 0 && e.path.Length == 0)
            {
                wadentries.Add(file);
                continue;
            }

            if (filter != null && ShouldSkip(e, filter)) continue;

            AddOrReplaceEntry(e);
        }
    }

    /// <summary>Build from a pre-collected list of entries (used when the source isn't a filesystem directory).</summary>
    public DirectoryFilesList(string resourcename, DirectoryFilesFilter? filter, bool silent, ICollection<DirectoryFileEntry> sourceentries)
    {
        entries = new Dictionary<string, DirectoryFileEntry>(sourceentries.Count, new PathEqualityComparer());
        wadentries = new List<string>();
        foreach (DirectoryFileEntry e in sourceentries)
        {
            if (string.Compare(e.extension, "wad", true) == 0 && e.path.Length == 0)
            {
                wadentries.Add(e.filepathname);
                continue;
            }

            if (filter != null && ShouldSkip(e, filter)) continue;

            if (entries.ContainsKey(e.filepathname))
            {
                if (!silent) Console.WriteLine($"[warning] Resource \"{resourcename}\" contains multiple files with the same filename: \"{e.filepathname}\"");
                continue;
            }

            AddOrReplaceEntry(e);
        }
    }

    private static bool ShouldSkip(DirectoryFileEntry e, DirectoryFilesFilter filter)
    {
        if (filter.IgnoredFileExtensions.Contains(e.extension)) return true;

        foreach (string ef in filter.IgnoredDirectoryNames)
        {
            if (e.path.StartsWith(ef + Path.DirectorySeparatorChar))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Adds <paramref name="e"/> to the dictionary or, if a same-key entry already exists, replaces it when the new entry's
    /// filepathname sorts lower (preferring already-lowercased names on filesystems where multiple case variants coexist).
    /// </summary>
    private void AddOrReplaceEntry(DirectoryFileEntry e)
    {
        if (entries.ContainsKey(e.filepathname))
        {
            string existingEntryPath = entries[e.filepathname].filepathname;
            if (e.filepathname.CompareTo(existingEntryPath) == -1)
            {
                entries.Remove(e.filepathname);
                entries.Add(e.filepathname, e);
            }
        }
        else
        {
            entries.Add(e.filepathname, e);
        }
    }

    /// <summary>Case-insensitive existence check by relative file path.</summary>
    public bool FileExists(string filepathname) => entries.ContainsKey(filepathname.ToLowerInvariant());

    public DirectoryFileEntry GetFileInfo(string filepathname) => entries[filepathname];

    //mxd. Root-level .wad files separated out during scan.
    public List<string> GetWadFiles() => wadentries;

    public List<string> GetAllFiles()
    {
        var files = new List<string>(entries.Count);
        foreach (var e in entries.Values) files.Add(e.filepathname);
        return files;
    }

    public List<string> GetAllFiles(bool subdirectories)
    {
        if (subdirectories) return GetAllFiles();
        var files = new List<string>(entries.Count);
        foreach (var e in entries.Values)
            if (e.path.Length == 0) files.Add(e.filepathname);
        return files;
    }

    public List<string> GetAllFiles(string path, bool subdirectories)
    {
        path = CorrectPath(path);
        var files = new List<string>(entries.Count);
        if (subdirectories)
        {
            foreach (var e in entries.Values)
                if (e.path.StartsWith(path)) files.Add(e.filepathname);
        }
        else
        {
            foreach (var e in entries.Values)
                if (e.path == path) files.Add(e.filepathname);
        }
        return files;
    }

    public List<string> GetAllFilesWithTitle(string path, string title)
    {
        path = CorrectPath(path).ToLowerInvariant();
        title = title.ToLowerInvariant();
        var files = new List<string>(entries.Count);
        foreach (var e in entries.Values)
            if (e.path.StartsWith(path) && e.filetitle == title) files.Add(e.filepathname);
        return files;
    }

    public List<string> GetAllFilesWithTitle(string path, string title, bool subdirectories)
    {
        if (subdirectories) return GetAllFilesWithTitle(path, title);
        path = CorrectPath(path).ToLowerInvariant();
        title = title.ToLowerInvariant();
        var files = new List<string>(entries.Count);
        foreach (var e in entries.Values)
            if (e.path == path && e.filetitle == title) files.Add(e.filepathname);
        return files;
    }

    //mxd
    public List<string> GetAllFilesWhichTitleStartsWith(string path, string title)
    {
        path = CorrectPath(path).ToLowerInvariant();
        title = title.ToLowerInvariant();
        var files = new List<string>(entries.Count);
        foreach (var e in entries.Values)
            if (e.path.StartsWith(path) && e.filetitle.StartsWith(title)) files.Add(e.filepathname);
        return files;
    }

    //mxd
    public List<string> GetAllFilesWhichTitleStartsWith(string path, string title, bool subdirectories)
    {
        if (subdirectories) return GetAllFilesWhichTitleStartsWith(path, title);
        path = CorrectPath(path).ToLowerInvariant();
        title = title.ToLowerInvariant();
        var files = new List<string>(entries.Count);
        foreach (var e in entries.Values)
            if (e.path == path && e.filetitle.StartsWith(title)) files.Add(e.filepathname);
        return files;
    }

    public List<string> GetAllFiles(string path, string extension)
    {
        path = CorrectPath(path).ToLowerInvariant();
        extension = extension.ToLowerInvariant();
        var files = new List<string>(entries.Count);
        foreach (var e in entries.Values)
            if (e.path.StartsWith(path) && e.extension == extension) files.Add(e.filepathname);
        return files;
    }

    public List<string> GetAllFiles(string path, string extension, bool subdirectories)
    {
        if (subdirectories) return GetAllFiles(path, extension);
        path = CorrectPath(path).ToLowerInvariant();
        extension = extension.ToLowerInvariant();
        var files = new List<string>(entries.Count);
        foreach (var e in entries.Values)
            if (e.path == path && e.extension == extension) files.Add(e.filepathname);
        return files;
    }

    public string? GetFirstFile(string title, bool subdirectories)
    {
        title = title.ToLowerInvariant();
        if (subdirectories)
        {
            foreach (var e in entries.Values)
                if (e.filetitle == title) return e.filepathname;
        }
        else
        {
            foreach (var e in entries.Values)
                if (e.filetitle == title && e.path.Length == 0) return e.filepathname;
        }
        return null;
    }

    public string? GetFirstFile(string title, bool subdirectories, string extension)
    {
        title = title.ToLowerInvariant();
        extension = extension.ToLowerInvariant();
        if (subdirectories)
        {
            foreach (var e in entries.Values)
                if (e.filetitle == title && e.extension == extension) return e.filepathname;
        }
        else
        {
            foreach (var e in entries.Values)
                if (e.filetitle == title && e.path.Length == 0 && e.extension == extension) return e.filepathname;
        }
        return null;
    }

    /// <summary>
    /// Finds the first file with the given title in the given path. ZDoom long-name compatibility: when the
    /// candidate file's title is longer than the classic 8-char limit, accept any title that starts with the
    /// requested name (so requesting "FLOOR4_8" matches "FLOOR4_8_LongVariant").
    /// </summary>
    public string? GetFirstFile(string path, string title, bool subdirectories)
    {
        title = title.ToLowerInvariant();
        path = CorrectPath(path).ToLowerInvariant();
        if (subdirectories)
        {
            foreach (var e in entries.Values)
                if ((e.filetitle.Length > Lump.CLASSIC_NAME_LENGTH ? e.filetitle.StartsWith(title) : e.filetitle == title)
                    && e.path.StartsWith(path)) return e.filepathname;
        }
        else
        {
            foreach (var e in entries.Values)
                if ((e.filetitle.Length > Lump.CLASSIC_NAME_LENGTH ? e.filetitle.StartsWith(title) : e.filetitle == title)
                    && e.path == path) return e.filepathname;
        }
        return null;
    }

    public string? GetFirstFile(string path, string title, bool subdirectories, string extension)
    {
        title = title.ToLowerInvariant();
        path = CorrectPath(path).ToLowerInvariant();
        extension = extension.ToLowerInvariant();
        if (subdirectories)
        {
            foreach (var e in entries.Values)
                if (e.filetitle == title && e.path.StartsWith(path) && e.extension == extension) return e.filepathname;
        }
        else
        {
            foreach (var e in entries.Values)
                if (e.filetitle == title && e.path == path && e.extension == extension) return e.filepathname;
        }
        return null;
    }

    //mxd. Ensures a path ends with the platform's directory separator; accepts either separator on input.
    private static string CorrectPath(string path)
    {
        if (path.Length > 0)
        {
            if (path[path.Length - 1] == Path.DirectorySeparatorChar) return path;
            if (path[path.Length - 1] == Path.AltDirectorySeparatorChar)
                path = path.Substring(0, path.Length - 1);
            return path + Path.DirectorySeparatorChar;
        }
        return path;
    }
}
