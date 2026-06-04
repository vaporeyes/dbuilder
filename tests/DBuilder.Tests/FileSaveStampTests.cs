// ABOUTME: Tests file metadata stamps used to guard editor saves from overwriting external changes.
// ABOUTME: Uses temporary files with controlled write times so conflict checks stay deterministic.

using System.IO;
using DBuilder.IO;

namespace DBuilder.Tests;

public class FileSaveStampTests
{
    [Fact]
    public void TryReadReturnsStampForExistingFile()
    {
        string path = TempPath();
        try
        {
            File.WriteAllText(path, "abc");

            Assert.True(FileSaveStamp.TryRead(path, out var stamp));
            Assert.Equal(3, stamp.Length);
        }
        finally { DeleteIfExists(path); }
    }

    [Fact]
    public void HasChangedDetectsLengthChange()
    {
        string path = TempPath();
        try
        {
            File.WriteAllText(path, "abc");
            Assert.True(FileSaveStamp.TryRead(path, out var stamp));

            File.WriteAllText(path, "abcdef");

            Assert.True(FileSaveStamp.HasChanged(path, stamp));
        }
        finally { DeleteIfExists(path); }
    }

    [Fact]
    public void HasChangedDetectsTimestampChange()
    {
        string path = TempPath();
        try
        {
            File.WriteAllText(path, "abc");
            var time = new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, time);
            Assert.True(FileSaveStamp.TryRead(path, out var stamp));

            File.SetLastWriteTimeUtc(path, time.AddSeconds(2));

            Assert.True(FileSaveStamp.HasChanged(path, stamp));
        }
        finally { DeleteIfExists(path); }
    }

    [Fact]
    public void HasChangedReturnsFalseWithoutExpectedStamp()
    {
        Assert.False(FileSaveStamp.HasChanged(TempPath(), null));
    }

    [Fact]
    public void HasChangedTreatsMissingStampedFileAsChanged()
    {
        string path = TempPath();
        File.WriteAllText(path, "abc");
        Assert.True(FileSaveStamp.TryRead(path, out var stamp));
        File.Delete(path);

        Assert.True(FileSaveStamp.HasChanged(path, stamp));
    }

    [Fact]
    public void IsReadOnlyDetectsReadOnlyFiles()
    {
        string path = TempPath();
        try
        {
            File.WriteAllText(path, "abc");

            Assert.False(FileSaveStamp.IsReadOnly(path));

            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

            Assert.True(FileSaveStamp.IsReadOnly(path));
        }
        finally
        {
            if (File.Exists(path))
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void IsReadOnlyReturnsFalseForMissingPath()
    {
        Assert.False(FileSaveStamp.IsReadOnly(null));
        Assert.False(FileSaveStamp.IsReadOnly(TempPath()));
    }

    [Fact]
    public void CanWriteExistingPathBlocksReadOnlyFiles()
    {
        Assert.True(FileSaveStamp.CanWriteExistingPath(null));
        Assert.True(FileSaveStamp.CanWriteExistingPath(TempPath()));

        string path = TempPath();
        try
        {
            File.WriteAllText(path, "abc");
            Assert.True(FileSaveStamp.CanWriteExistingPath(path));

            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

            Assert.False(FileSaveStamp.CanWriteExistingPath(path));
        }
        finally
        {
            if (File.Exists(path))
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void ExistingPathWriteBlockStatusReportsReadOnlyTargets()
    {
        Assert.Null(FileSaveStamp.ExistingPathWriteBlockStatus(null));
        Assert.Null(FileSaveStamp.ExistingPathWriteBlockStatus(TempPath()));

        string path = TempPath();
        try
        {
            File.WriteAllText(path, "abc");
            Assert.Null(FileSaveStamp.ExistingPathWriteBlockStatus(path));

            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

            Assert.Equal(FileSaveStamp.ReadOnlyTargetSaveStatus, FileSaveStamp.ExistingPathWriteBlockStatus(path));
        }
        finally
        {
            if (File.Exists(path))
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            DeleteIfExists(path);
        }
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"dbuilder_stamp_{System.Guid.NewGuid():N}.tmp");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
