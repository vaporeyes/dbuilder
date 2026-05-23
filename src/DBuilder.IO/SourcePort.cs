// ABOUTME: Builds a source-port (GZDoom/etc) command line for Test Map from an argument template with %IWAD/%FO/%MAP tokens.
// ABOUTME: Splits the template into tokens honoring double quotes so paths with spaces stay intact, then substitutes.

using System.Collections.Generic;
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

        for (int i = 0; i < tokens.Count; i++)
            tokens[i] = tokens[i].Replace("%IWAD", iwad).Replace("%FO", file).Replace("%MAP", map);
        return tokens;
    }
}
