// ABOUTME: Builds a source-port command line for Test Map from DBuilder or UDB argument templates.
// ABOUTME: Splits templates honoring double quotes, substitutes map/file tokens, and drops empty optional args.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        string? additionalParameters = null)
    {
        var (l, l1, l2) = WarpTokens(map);
        string additional = BuildAdditionalFiles(additionalFiles);
        string iwadFile = Path.GetFileName(iwad);
        string noMonsters = testMonsters ? "" : "-nomonsters";
        template = NormalizeUdbTokens(template)
            .Replace("\"%AP\"", additional)
            .Replace("%AP", additional);

        var tokens = SplitArguments(template);

        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            string token = tokens[i]
                .Replace("%IWAD", iwad)
                .Replace("%WP", iwad)
                .Replace("%WF", iwadFile)
                .Replace("%FO", file)
                .Replace("%F", file)
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

    private static string BuildAdditionalFiles(IEnumerable<string>? additionalFiles)
    {
        if (additionalFiles is null) return "";

        var result = new List<string>();
        foreach (string file in additionalFiles)
        {
            if (string.IsNullOrWhiteSpace(file)) continue;
            result.Add(Quote(file));
        }
        return string.Join(" ", result);
    }

    private static string Quote(string value)
        => "\"" + value.Replace("\"", "\\\"") + "\"";

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
