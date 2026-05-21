// ABOUTME: DirectoryFileEntry ported from UDB Source/Core/IO/DirectoryFileEntry.cs.
// ABOUTME: Decomposes a file path into name/title/extension/path components.  Lookup keys are folded to lowercase for cross-platform case-insensitive matching; filepathname/filepathtitle preserve original case for actual filesystem access.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 *
 * UDB marks this internal; ported as public for testability and future cross-assembly use
 * (PK3StructuredReader, DirectoryFilesList, etc.).
 */

using System.IO;

namespace DBuilder.IO;

public enum FileTitleStyle
{
    /// <summary>Standard: filename without extension.</summary>
    DEFAULT,
    /// <summary>ZDoom: filename without extension, truncated to 8 chars.</summary>
    ZDOOM,
    /// <summary>Eternity Engine: everything before the first dot, truncated to 8 chars.</summary>
    ETERNITYENGINE,
}

public struct DirectoryFileEntry
{
    // Example for fullname = "C:\WADs\Foo\Bar.WAD" created from "C:\WADs":
    //   filename       = "bar.wad"
    //   filetitle      = "bar"
    //   extension      = "wad"
    //   path           = "foo\"
    //   filepathname   = "Foo\Bar.WAD"   (case preserved)
    //   filepathtitle  = "Foo\Bar"       (case preserved)
    public string filename;
    public string filetitle;
    public string extension;
    public string path;
    public string filepathname;
    public string filepathtitle;

    /// <summary>Build relative to <paramref name="frompath"/>: <c>path</c> becomes the sub-directory under that base.</summary>
    public DirectoryFileEntry(string fullname, string frompath, FileTitleStyle filetitlestyle)
    {
        filename = Path.GetFileName(fullname);
        filetitle = GetFileTitle(fullname, filetitlestyle);
        extension = Path.GetExtension(fullname);
        if (extension.Length > 1)
            extension = extension.Substring(1);
        else
            extension = "";
        path = Path.GetDirectoryName(fullname) ?? "";
        if (path.Length > (frompath.Length + 1))
            path = path.Substring(frompath.Length + 1) + Path.DirectorySeparatorChar;
        else
            path = "";
        filepathname = Path.Combine(path, filename);
        filepathtitle = Path.Combine(path, filetitle);

        // Lookup keys folded to lowercase (case-insensitive on Linux/macOS where filenames are case-sensitive
        // but Doom semantics expect case-insensitive lookup).  filepathname/filepathtitle keep original case for
        // actual disk access.
        filename = filename.ToLowerInvariant();
        filetitle = filetitle.ToLowerInvariant();
        extension = extension.ToLowerInvariant();
        path = path.ToLowerInvariant();
    }

    /// <summary>Build without a base path: <c>path</c> is the absolute directory of <paramref name="fullname"/>.</summary>
    public DirectoryFileEntry(string fullname, FileTitleStyle filetitlestyle)
    {
        filename = Path.GetFileName(fullname);
        filetitle = GetFileTitle(fullname, filetitlestyle);
        extension = Path.GetExtension(fullname);
        if (extension.Length > 1)
            extension = extension.Substring(1);
        else
            extension = "";
        path = Path.GetDirectoryName(fullname) ?? "";
        if (!string.IsNullOrEmpty(path)) path += Path.DirectorySeparatorChar; //mxd
        filepathname = Path.Combine(path, filename);
        filepathtitle = Path.Combine(path, filetitle);

        filename = filename.ToLowerInvariant();
        filetitle = filetitle.ToLowerInvariant();
        extension = extension.ToLowerInvariant();
        path = path.ToLowerInvariant();
    }

    private static string GetFileTitle(string fullname, FileTitleStyle filetitlestyle)
    {
        if (filetitlestyle == FileTitleStyle.ZDOOM)
        {
            // ZDoom: drop extension, then truncate to 8 chars.
            string t = Path.GetFileNameWithoutExtension(fullname);
            return t.Length > 8 ? t.Substring(0, 8) : t;
        }
        else if (filetitlestyle == FileTitleStyle.ETERNITYENGINE)
        {
            // Eternity: everything before the first dot, truncated to 8 chars.
            string t = Path.GetFileName(fullname);
            int dotindex = t.IndexOf('.');
            if (dotindex > 0)
                t = t.Substring(0, dotindex);
            return t.Length > 8 ? t.Substring(0, 8) : t;
        }
        else
        {
            // Default: drop extension only.
            return Path.GetFileNameWithoutExtension(fullname);
        }
    }
}
