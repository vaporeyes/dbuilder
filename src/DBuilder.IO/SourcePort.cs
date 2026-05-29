// ABOUTME: Builds a source-port command line for Test Map from DBuilder or UDB argument templates.
// ABOUTME: Splits templates honoring double quotes, substitutes map/file tokens, and drops empty optional args.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;

namespace DBuilder.IO;

public static class SourcePort
{
    /// <summary>The default GZDoom-family argument template: load the IWAD, the edited map PWAD, and warp to the map.</summary>
    public const string DefaultArgsTemplate = "-iwad \"%IWAD\" -file \"%FO\" +map \"%MAP\"";

    /// <summary>
    /// Splits <paramref name="template"/> into argument tokens (double quotes group spaces) and substitutes
    /// %IWAD (iwad path), %FO (the map PWAD path) and %MAP (the map marker).
    /// </summary>
    public static List<string> BuildArgs(string template, string iwad, string file, string map)
    {
        var (l, l1, l2) = WarpTokens(map);
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

        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            string token = tokens[i]
                .Replace("%IWAD", iwad)
                .Replace("%WP", iwad)
                .Replace("%FO", file)
                .Replace("%F", file)
                .Replace("%MAP", map)
                .Replace("%L1", l1)
                .Replace("%L2", l2)
                .Replace("%L", l)
                .Replace("%AP", "")
                .Replace("%S", "3")
                .Replace("%NM", "");
            if (token.Length == 0) tokens.RemoveAt(i);
            else tokens[i] = token;
        }
        return tokens;
    }

    private static (string L, string L1, string L2) WarpTokens(string map)
    {
        var exmx = Regex.Match(map, @"^E(?<e>\d+)M(?<m>\d+)$", RegexOptions.IgnoreCase);
        if (exmx.Success)
            return (map, exmx.Groups["e"].Value, exmx.Groups["m"].Value);

        var mapxx = Regex.Match(map, @"^MAP(?<n>\d+)$", RegexOptions.IgnoreCase);
        if (mapxx.Success)
        {
            string number = mapxx.Groups["n"].Value.PadLeft(2, '0');
            return (map, number[..^1], number[^1..]);
        }

        return (map, map, "");
    }
}
