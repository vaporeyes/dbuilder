// ABOUTME: Tests DBuilder error log and crash report file writing.
// ABOUTME: Covers caught exception append logging and unhandled exception crash report output.

using DBuilder.IO;

namespace DBuilder.Tests;

public class ErrorLogTests
{
    [Fact]
    public void AppendWritesContextAndExceptionDetails()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_errorlog_" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "DBuilder.log");

        var written = ErrorLog.Append(new InvalidOperationException("boom"), "Load failed", path);

        Assert.Equal(path, written);
        string text = File.ReadAllText(path);
        Assert.Contains("Load failed", text);
        Assert.Contains("InvalidOperationException", text);
        Assert.Contains("boom", text);
    }

    [Fact]
    public void WriteCrashReportWritesSystemInfoAndAppendsLog()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_crash_" + Guid.NewGuid().ToString("N"));
        string crashPath = Path.Combine(dir, "DBuilderCrash.txt");
        string logPath = Path.Combine(dir, "DBuilder.log");

        var written = ErrorLog.WriteCrashReport(new ApplicationException("crash"), crashPath, logPath);

        Assert.Equal(crashPath, written);
        string crash = File.ReadAllText(crashPath);
        Assert.Contains("SYSTEM INFO", crash);
        Assert.Contains("EXCEPTION DETAILS", crash);
        Assert.Contains("ApplicationException", crash);
        Assert.Contains("crash", File.ReadAllText(logPath));
    }

    [Fact]
    public void ReadRecentTextReturnsTailOfLog()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_error_tail_" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "DBuilder.log");
        Directory.CreateDirectory(dir);
        File.WriteAllText(path, "0123456789");

        string text = ErrorLog.ReadRecentText(path, maxCharacters: 4);

        Assert.Equal("6789", text);
    }

    [Fact]
    public void ListReportPathsReturnsNewestDBuilderReports()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_error_reports_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string oldPath = Path.Combine(dir, "DBuilder.log");
        string newPath = Path.Combine(dir, "DBuilderCrash.txt");
        string ignoredPath = Path.Combine(dir, "Other.log");
        File.WriteAllText(oldPath, "old");
        File.WriteAllText(newPath, "new");
        File.WriteAllText(ignoredPath, "ignored");
        File.SetLastWriteTimeUtc(oldPath, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newPath, new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        string[] paths = ErrorLog.ListReportPaths(dir);

        Assert.Equal(new[] { newPath, oldPath }, paths);
    }
}
