// ABOUTME: Finds UDB-style script text usages with line indexes and match spans.
// ABOUTME: Keeps script search behavior testable outside the editor UI.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DBuilder.IO;

public sealed record ScriptUsageResult(string Line, int LineIndex, int MatchStart, int MatchEnd);

public static class ScriptFindUsages
{
    public static bool ContainsText(
        string text,
        string findText,
        bool wholeWord = false,
        bool caseSensitive = false)
        => Find(text, findText, wholeWord, caseSensitive, stopAfterFirst: true).Count > 0;

    public static IReadOnlyList<ScriptUsageResult> Find(
        string text,
        string findText,
        bool wholeWord = false,
        bool caseSensitive = false)
        => Find(text, findText, wholeWord, caseSensitive, stopAfterFirst: false);

    private static IReadOnlyList<ScriptUsageResult> Find(
        string text,
        string findText,
        bool wholeWord,
        bool caseSensitive,
        bool stopAfterFirst)
    {
        var result = new List<ScriptUsageResult>();
        if (string.IsNullOrEmpty(findText)) return result;

        string escapedFindText = Regex.Escape(findText);
        string pattern = wholeWord ? "\\b" + escapedFindText + "\\b" : escapedFindText;
        var options = caseSensitive ? RegexOptions.CultureInvariant : RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var regex = new Regex(pattern, options);
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            foreach (Match match in regex.Matches(lines[i]))
            {
                result.Add(new ScriptUsageResult(lines[i], i, match.Index, match.Index + match.Length));
                if (stopAfterFirst) return result;
            }
        }

        return result;
    }
}
