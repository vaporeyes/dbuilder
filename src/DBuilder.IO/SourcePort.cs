// ABOUTME: Builds a source-port command line for Test Map from DBuilder or UDB argument templates.
// ABOUTME: Splits templates honoring double quotes, substitutes map/file tokens, and drops empty optional args.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Text;

namespace DBuilder.IO;

public static class SourcePort
{
    /// <summary>The default GZDoom-family argument template: load the IWAD, the edited map PWAD, and warp to the map.</summary>
    public const string DefaultArgsTemplate = "-iwad \"%IWAD\" -file \"%FO\" +map \"%MAP\"";

    /// <summary>
    /// Splits <paramref name="template"/> into argument tokens (double quotes group spaces) and substitutes
    /// UDB and DBuilder placeholders for IWAD, map resources, skill, map marker, and no-monsters mode.
    /// </summary>
    public static List<string> BuildArgs(
        string template,
        string iwad,
        string file,
        string map,
        IEnumerable<string>? additionalFiles = null,
        bool testMonsters = true,
        int skill = 3,
        string? additionalParameters = null,
        bool shortPaths = false,
        bool linuxPaths = false)
    {
        var (l, l1, l2) = WarpTokens(map);
        string launchIwad = ConvertPath(iwad, shortPaths, linuxPaths);
        string launchFile = ConvertPath(file, shortPaths, linuxPaths);
        string additional = BuildAdditionalFiles(additionalFiles, shortPaths, linuxPaths);
        string iwadFile = ConvertPath(FileName(iwad), shortPaths, linuxPaths);
        string noMonsters = testMonsters ? "" : "-nomonsters";
        template = NormalizeUdbTokens(template)
            .Replace("\"%AP\"", additional)
            .Replace("%AP", additional);

        var tokens = SplitArguments(template);

        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            string token = tokens[i]
                .Replace("%IWAD", launchIwad)
                .Replace("%WP", launchIwad)
                .Replace("%WF", iwadFile)
                .Replace("%FO", launchFile)
                .Replace("%F", launchFile)
                .Replace("%MAP", map)
                .Replace("%L1", l1)
                .Replace("%L2", l2)
                .Replace("%L", l)
                .Replace("%S", skill.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Replace("%NM", noMonsters);
            if (token.Length == 0) tokens.RemoveAt(i);
            else tokens[i] = token;
        }
        if (!string.IsNullOrWhiteSpace(additionalParameters))
            tokens.AddRange(SplitArguments(additionalParameters));
        return tokens;
    }

    public static ProcessStartInfo CreateStartInfo(string executable, IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
        string? workingDirectory = Path.GetDirectoryName(executable);
        if (!string.IsNullOrWhiteSpace(workingDirectory))
            startInfo.WorkingDirectory = workingDirectory;
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }

    public static SourcePortLaunchResult Launch(
        string executable,
        IEnumerable<string> arguments,
        Func<ProcessStartInfo, bool>? start = null)
    {
        ProcessStartInfo startInfo = CreateStartInfo(executable, arguments);
        try
        {
            bool launched = start is null
                ? Process.Start(startInfo) is not null
                : start(startInfo);
            return launched
                ? SourcePortLaunchResult.Ok(startInfo)
                : SourcePortLaunchResult.Fail(startInfo, "Source port launch failed: process did not start.");
        }
        catch (Exception ex)
        {
            return SourcePortLaunchResult.Fail(startInfo, "Source port launch failed: " + ex.Message);
        }
    }

    private static string BuildAdditionalFiles(IEnumerable<string>? additionalFiles, bool shortPaths, bool linuxPaths)
    {
        if (additionalFiles is null) return "";

        var result = new List<string>();
        foreach (string file in additionalFiles)
        {
            if (string.IsNullOrWhiteSpace(file)) continue;
            result.Add(Quote(ConvertPath(file, shortPaths, linuxPaths)));
        }
        return string.Join(" ", result);
    }

    private static string ConvertPath(string value, bool shortPaths, bool linuxPaths)
    {
        if (shortPaths) return ToShortPath(value);
        if (linuxPaths) return ToLinuxPath(value);
        return value;
    }

    private static string ToShortPath(string value)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return value;

        int length = GetShortPathName(value, null, 0);
        if (length <= 0) return value;

        var buffer = new StringBuilder(length);
        int written = GetShortPathName(value, buffer, buffer.Capacity);
        return written > 0 ? buffer.ToString() : value;
    }

    private static string ToLinuxPath(string value)
    {
        string path = value.Replace('\\', '/');
        string prefix = Environment.GetEnvironmentVariable("WINEPREFIX") ?? "";
        if (path.StartsWith("C:", StringComparison.Ordinal))
            return prefix + "/drive_c" + path[2..];
        if (path.StartsWith("Z:", StringComparison.Ordinal))
            return path[2..];
        return path;
    }

    private static string FileName(string path)
    {
        int slash = path.LastIndexOf('/');
        int backslash = path.LastIndexOf('\\');
        int index = Math.Max(slash, backslash);
        return index >= 0 ? path[(index + 1)..] : path;
    }

    private static string Quote(string value)
        => "\"" + value.Replace("\"", "\\\"") + "\"";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetShortPathName(string longPath, StringBuilder? shortPath, int bufferSize);

    private static List<string> SplitArguments(string template)
    {
        var tokens = new List<string>();
        var cur = new StringBuilder();
        bool inQuote = false, has = false;
        foreach (char ch in template)
        {
            if (ch == '"') { inQuote = !inQuote; has = true; }
            else if (char.IsWhiteSpace(ch) && !inQuote) { if (has) { tokens.Add(cur.ToString()); cur.Clear(); has = false; } }
            else { cur.Append(ch); has = true; }
        }
        if (has) tokens.Add(cur.ToString());
        return tokens;
    }

    private static string NormalizeUdbTokens(string template)
        => Regex.Replace(
            template,
            "%(IWAD|MAP|FO|WP|WF|L1|L2|AP|NM|F|L|S)(?![A-Za-z0-9_])",
            match => "%" + match.Groups[1].Value.ToUpperInvariant(),
            RegexOptions.IgnoreCase);

    private static (string L, string L1, string L2) WarpTokens(string map)
    {
        var exmx = Regex.Match(map, @"^E(?<e>\d+)M(?<m>\d+)$", RegexOptions.IgnoreCase);
        if (exmx.Success)
            return (map, exmx.Groups["e"].Value, exmx.Groups["m"].Value);

        var numbers = Regex.Matches(map, @"\d+");
        if (numbers.Count == 0) return (map, "", "");

        string l1 = int.Parse(numbers[0].Value, System.Globalization.CultureInfo.InvariantCulture)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        string l2 = numbers.Count > 1
            ? int.Parse(numbers[1].Value, System.Globalization.CultureInfo.InvariantCulture)
                .ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "";
        return (map, l1, l2);
    }
}

public sealed record SourcePortLaunchResult(bool Success, string Message, ProcessStartInfo StartInfo)
{
    public static SourcePortLaunchResult Ok(ProcessStartInfo startInfo)
        => new(true, "Source port launched.", startInfo);

    public static SourcePortLaunchResult Fail(ProcessStartInfo startInfo, string message)
        => new(false, message, startInfo);
}
