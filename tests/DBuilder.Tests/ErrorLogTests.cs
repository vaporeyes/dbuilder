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
}
