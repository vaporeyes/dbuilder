// ABOUTME: DirectoryFileEntry verification tests.
// ABOUTME: All three title styles, case folding (lookup keys lowercase, filepath* case-preserved), relative-path constructor, long-name truncation.

using System.IO;
using DBuilder.IO;

namespace DBuilder.Tests;

public class DirectoryFileEntryTests
{
    // Use Path.Combine so the tests work identically on Linux/macOS/Windows.
    private static string P(params string[] segments) => Path.Combine(segments);

    [Fact]
    public void DefaultStyleDropsExtensionOnly()
    {
        string full = P("home", "wads", "BarMap.WAD");
        var e = new DirectoryFileEntry(full, FileTitleStyle.DEFAULT);
        Assert.Equal("barmap", e.filetitle);
        Assert.Equal("barmap.wad", e.filename);
        Assert.Equal("wad", e.extension);
    }

    [Fact]
    public void ZDoomStyleTruncatesToEightChars()
    {
        string full = P("anywhere", "LongMapName.wad");
        var e = new DirectoryFileEntry(full, FileTitleStyle.ZDOOM);
        Assert.Equal("longmapn", e.filetitle); // "LongMapName" -> first 8 -> lowercased
    }

    [Fact]
    public void EternityEngineStyleCutsAtFirstDotThenTruncates()
    {
        // "MyMap.alpha.wad" -> "MyMap" (cut at first dot) -> already <=8 chars
        string full = P("dir", "MyMap.alpha.wad");
        var e = new DirectoryFileEntry(full, FileTitleStyle.ETERNITYENGINE);
        Assert.Equal("mymap", e.filetitle);
    }

    [Fact]
    public void EternityEngineStyleTruncatesLongNameBeforeDot()
    {
        string full = P("dir", "VeryLongNameHere.something.wad");
        var e = new DirectoryFileEntry(full, FileTitleStyle.ETERNITYENGINE);
        Assert.Equal("verylong", e.filetitle); // first 8 of "VeryLongNameHere"
    }

    [Fact]
    public void ExtensionFieldExcludesDotAndIsLowercased()
    {
        string full = P("dir", "Map.WAD");
        var e = new DirectoryFileEntry(full, FileTitleStyle.DEFAULT);
        Assert.Equal("wad", e.extension);
        Assert.False(e.extension.StartsWith("."));
    }

    [Fact]
    public void FileWithNoExtensionHasEmptyExtensionField()
    {
        string full = P("dir", "README");
        var e = new DirectoryFileEntry(full, FileTitleStyle.DEFAULT);
        Assert.Equal("", e.extension);
        Assert.Equal("readme", e.filetitle);
    }

    [Fact]
    public void LookupKeysAreLowercasedFilepathPreservesCase()
    {
        string full = P("Documents", "Doom", "MAP.WAD");
        var e = new DirectoryFileEntry(full, FileTitleStyle.DEFAULT);

        // Lookup keys are case-folded.
        Assert.Equal("map.wad", e.filename);
        Assert.Equal("map",     e.filetitle);
        Assert.Equal("wad",     e.extension);

        // filepathname/filepathtitle preserve the original case so they can be used for real disk access.
        Assert.Contains("MAP.WAD", e.filepathname);
        Assert.Contains("MAP",     e.filepathtitle);
    }

    [Fact]
    public void RelativePathConstructorStripsBase()
    {
        // base = "/wads", full = "/wads/Foo/Bar.WAD" -> path is "foo<sep>"
        string baseDir = P("wads");
        string full = P("wads", "Foo", "Bar.WAD");
        var e = new DirectoryFileEntry(full, baseDir, FileTitleStyle.DEFAULT);

        Assert.Equal("bar.wad", e.filename);
        Assert.Equal("bar",     e.filetitle);
        // path should be "foo" + separator, lowercased
        Assert.Equal("foo" + Path.DirectorySeparatorChar, e.path);

        // filepathname combines the (already-built) path with filename - includes the relative dir
        Assert.Contains("Foo", e.filepathname);
        Assert.Contains("Bar.WAD", e.filepathname);
    }

    [Fact]
    public void RelativePathConstructorYieldsEmptyPathForFileAtBase()
    {
        // File sitting directly in the base directory has no sub-path.
        string baseDir = P("wads");
        string full = P("wads", "Map.WAD");
        var e = new DirectoryFileEntry(full, baseDir, FileTitleStyle.DEFAULT);

        Assert.Equal("", e.path);
        Assert.Equal("map.wad", e.filename);
    }

    [Fact]
    public void NonRelativeConstructorPreservesAbsoluteDirectory()
    {
        string full = P("anywhere", "Map.WAD");
        var e = new DirectoryFileEntry(full, FileTitleStyle.DEFAULT);

        // path is the directory with a trailing separator, lowercased.
        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), e.path);
        Assert.Contains("anywhere", e.path);
    }

    [Fact]
    public void DefaultStyleDoesNotTruncate()
    {
        string full = P("dir", "VeryVeryLongFilenameWithoutExtension");
        var e = new DirectoryFileEntry(full, FileTitleStyle.DEFAULT);
        Assert.Equal("veryverylongfilenamewithoutextension", e.filetitle);
    }

    [Fact]
    public void ZDoomStyleHandlesShortName()
    {
        string full = P("dir", "ab.wad");
        var e = new DirectoryFileEntry(full, FileTitleStyle.ZDOOM);
        Assert.Equal("ab", e.filetitle); // no truncation needed
    }

    [Fact]
    public void EternityStyleHandlesNameWithoutDot()
    {
        string full = P("dir", "PlainName");
        var e = new DirectoryFileEntry(full, FileTitleStyle.ETERNITYENGINE);
        // No dot -> keep full name, truncated to 8. "PlainName" is 9 chars -> first 8 -> "PlainNam" -> lowercased.
        Assert.Equal("plainnam", e.filetitle);
    }
}
