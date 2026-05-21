// ABOUTME: DirectoryFilesList verification tests.
// ABOUTME: Each test builds a small temp directory tree, scans it, and asserts lookup behavior; cleans up on dispose.

using System;
using System.Collections.Generic;
using System.IO;
using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class DirectoryFilesListTests : IDisposable
{
    private readonly string root;

    public DirectoryFilesListTests()
    {
        // Unique temp dir per test instance so parallel runs don't collide.
        root = Path.Combine(Path.GetTempPath(), "DBuilder.Tests.DFL-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        catch { /* best effort */ }
    }

    private void Touch(string relativePath, string contents = "")
    {
        string full = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents);
    }

    [Fact]
    public void ScansFlatDirectoryAndCountsEntries()
    {
        Touch("a.txt");
        Touch("b.png");
        Touch("c.cfg");

        var list = new DirectoryFilesList(root, filter: null, subdirectories: false, FileTitleStyle.DEFAULT);
        Assert.Equal(3, list.Count);
        Assert.True(list.FileExists("a.txt"));
        Assert.True(list.FileExists("c.cfg"));
        Assert.False(list.FileExists("missing.png"));
    }

    [Fact]
    public void RootLevelWadFilesAreSegregated()
    {
        Touch("map1.wad");
        Touch("map2.WAD"); // case-insensitive extension match
        Touch("readme.txt");

        var list = new DirectoryFilesList(root, filter: null, subdirectories: false, FileTitleStyle.DEFAULT);

        // WADs go to the wad list, not the main entries.
        Assert.Equal(1, list.Count); // just readme.txt
        Assert.True(list.FileExists("readme.txt"));
        var wads = list.GetWadFiles();
        Assert.Equal(2, wads.Count);
    }

    [Fact]
    public void NonRootWadsAreTreatedAsRegularFiles()
    {
        Touch("subdir/nested.wad");
        var list = new DirectoryFilesList(root, filter: null, subdirectories: true, FileTitleStyle.DEFAULT);
        Assert.Equal(1, list.Count);
        Assert.Empty(list.GetWadFiles()); // not at root, so not segregated
    }

    [Fact]
    public void SubdirectoryWalkPicksUpNestedFiles()
    {
        Touch("a.txt");
        Touch("sub1/b.txt");
        Touch("sub1/sub2/c.txt");

        var withoutSub = new DirectoryFilesList(root, filter: null, subdirectories: false, FileTitleStyle.DEFAULT);
        var withSub    = new DirectoryFilesList(root, filter: null, subdirectories: true,  FileTitleStyle.DEFAULT);

        Assert.Equal(1, withoutSub.Count);
        Assert.Equal(3, withSub.Count);
    }

    [Fact]
    public void IgnoredExtensionsAreSkipped()
    {
        Touch("keep.png");
        Touch("skip.bak");
        Touch("skip2.tmp");

        var filter = new DirectoryFilesFilter(ignoredExtensions: new[] { "bak", "tmp" });
        var list = new DirectoryFilesList(root, filter, subdirectories: false, FileTitleStyle.DEFAULT);

        Assert.Equal(1, list.Count);
        Assert.True(list.FileExists("keep.png"));
    }

    [Fact]
    public void IgnoredDirectoriesAreSkipped()
    {
        Touch("keep/a.txt");
        Touch(".git/HEAD");
        Touch(".git/refs/heads/main");

        var filter = new DirectoryFilesFilter(ignoredDirectories: new[] { ".git" });
        var list = new DirectoryFilesList(root, filter, subdirectories: true, FileTitleStyle.DEFAULT);

        Assert.Equal(1, list.Count);
        Assert.True(list.FileExists(Path.Combine("keep", "a.txt").ToLowerInvariant()));
    }

    [Fact]
    public void FileExistsIsCaseInsensitive()
    {
        Touch("MyFile.PNG");
        var list = new DirectoryFilesList(root, filter: null, subdirectories: false, FileTitleStyle.DEFAULT);

        Assert.True(list.FileExists("myfile.png"));
        Assert.True(list.FileExists("MYFILE.PNG"));
        Assert.True(list.FileExists("MyFile.png"));
    }

    [Fact]
    public void GetFirstFileFindsByTitle()
    {
        Touch("hello.png");
        Touch("hello.txt");
        Touch("world.txt");

        var list = new DirectoryFilesList(root, filter: null, subdirectories: false, FileTitleStyle.DEFAULT);
        // Without extension constraint - returns first match (order is sorted).
        var hello = list.GetFirstFile("hello", subdirectories: false);
        Assert.NotNull(hello);
        Assert.StartsWith("hello", Path.GetFileNameWithoutExtension(hello!));

        // With extension constraint - exact match.
        var helloPng = list.GetFirstFile("hello", subdirectories: false, extension: "png");
        Assert.Equal("hello.png", helloPng);

        // Not found.
        Assert.Null(list.GetFirstFile("missing", subdirectories: false));
    }

    [Fact]
    public void GetFirstFileFindsInSubdirectory()
    {
        Touch("textures/wall1.png");

        var list = new DirectoryFilesList(root, filter: null, subdirectories: true, FileTitleStyle.DEFAULT);
        var found = list.GetFirstFile("textures" + Path.DirectorySeparatorChar, "wall1", subdirectories: false);
        Assert.NotNull(found);
    }

    [Fact]
    public void GetFirstFilePrefixMatchesForLongTitles()
    {
        // ZDoom long-name compatibility: title longer than 8 chars matches a query that's a prefix of it.
        // Use DEFAULT style so filetitle isn't truncated.
        Touch("textures/FLOOR4_8_LongVariant.png");
        var list = new DirectoryFilesList(root, filter: null, subdirectories: true, FileTitleStyle.DEFAULT);

        // Searching for "FLOOR4_8" should match "FLOOR4_8_LongVariant" via the long-name prefix branch.
        var found = list.GetFirstFile("textures" + Path.DirectorySeparatorChar, "FLOOR4_8", subdirectories: false);
        Assert.NotNull(found);
    }

    [Fact]
    public void GetAllFilesByPathAndExtensionFilters()
    {
        Touch("art/a.png");
        Touch("art/b.png");
        Touch("art/c.txt");
        Touch("other/d.png");

        var list = new DirectoryFilesList(root, filter: null, subdirectories: true, FileTitleStyle.DEFAULT);
        var pngs = list.GetAllFiles("art" + Path.DirectorySeparatorChar, "png", subdirectories: false);
        Assert.Equal(2, pngs.Count);
    }

    [Fact]
    public void CustomSourceListConstructorRespectsFilter()
    {
        // Build a custom list of DirectoryFileEntry directly without touching the filesystem.
        var entries = new List<DirectoryFileEntry>
        {
            new DirectoryFileEntry(Path.Combine(root, "a.txt"), FileTitleStyle.DEFAULT),
            new DirectoryFileEntry(Path.Combine(root, "b.bak"), FileTitleStyle.DEFAULT),
            new DirectoryFileEntry(Path.Combine(root, "c.png"), FileTitleStyle.DEFAULT),
        };
        var filter = new DirectoryFilesFilter(ignoredExtensions: new[] { "bak" });
        var list = new DirectoryFilesList("test", filter, silent: true, entries);
        Assert.Equal(2, list.Count); // bak excluded
    }

    [Fact]
    public void GetAllFilesAllVariants()
    {
        Touch("a.png");
        Touch("sub/b.png");
        var list = new DirectoryFilesList(root, filter: null, subdirectories: true, FileTitleStyle.DEFAULT);

        // All files
        Assert.Equal(2, list.GetAllFiles().Count);
        // Root-only
        Assert.Single(list.GetAllFiles(subdirectories: false));
    }
}
