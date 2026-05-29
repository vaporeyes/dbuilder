// ABOUTME: Writes editor error logs and crash reports to the DBuilder app-data directory.
// ABOUTME: Provides shared logging for caught workflow failures and unhandled exception dialogs.

using System;
using System.IO;
using System.Text;

namespace DBuilder.IO;

public static class ErrorLog
{
    public static string DefaultDirectory => Settings.DefaultPathDirectory;
    public static string DefaultLogPath => Path.Combine(DefaultDirectory, "DBuilder.log");
    public static string DefaultCrashReportPath => Path.Combine(DefaultDirectory, "DBuilderCrash.txt");

    public static string? Append(Exception exception, string context, string? path = null)
    {
        try
        {
            path ??= DefaultLogPath;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(path, EntryText(exception, context));
            return path;
        }
        catch
        {
            return null;
        }
    }

    public static string? WriteCrashReport(Exception exception, string? path = null, string? logPath = null)
    {
        try
        {
            path ??= DefaultCrashReportPath;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, CrashReportText(exception));
            Append(exception, "Unhandled exception", logPath);
            return path;
        }
        catch
        {
            return null;
        }
    }

    public static string EntryText(Exception exception, string context)
    {
        var text = new StringBuilder();
        text.AppendLine($"[{DateTimeOffset.UtcNow:O}] {context}");
        text.AppendLine(exception.ToString());
        text.AppendLine();
        return text.ToString();
    }

    public static string CrashReportText(Exception exception)
    {
        var text = new StringBuilder();
        text.AppendLine("***********SYSTEM INFO***********");
        text.AppendLine($"OS: {Environment.OSVersion}");
        text.AppendLine($"Runtime: {Environment.Version}");
        text.AppendLine($"Process: {(Environment.Is64BitProcess ? "x64" : "x86")}");
        text.AppendLine();
        text.AppendLine("********EXCEPTION DETAILS********");
        text.AppendLine(exception.ToString());
        return text.ToString();
    }
}
