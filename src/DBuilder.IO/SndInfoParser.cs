// ABOUTME: Parser for ZDoom SNDINFO logical sound declarations.
// ABOUTME: Captures sound name to lump path mappings and aliases without loading audio data.

using System;
using System.Collections.Generic;
using System.Text;

namespace DBuilder.IO;

public sealed class SndInfo
{
    public Dictionary<string, string> Sounds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Aliases { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> RandomGroups { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class SndInfoParser
{
    private enum AssignmentFormat { None, Old, New }

    public static SndInfo Parse(string text) => Parse(text, baseGame: null);

    public static SndInfo Parse(string text, TerrainBaseGame? baseGame)
    {
        var result = new SndInfo();
        TerrainBaseGame? conditionalGame = null;
        AssignmentFormat format = AssignmentFormat.None;
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string rawLine = lines[lineIndex];
            var t = Tokenize(StripLineComment(rawLine));
            if (t.Count == 0) continue;

            string first = t[0];
            if (TryReadConditional(first, out var game))
            {
                conditionalGame = game;
                continue;
            }
            if (first.Equals("$endif", StringComparison.OrdinalIgnoreCase))
            {
                conditionalGame = null;
                continue;
            }
            if (baseGame.HasValue && conditionalGame.HasValue && conditionalGame.Value != baseGame.Value) continue;

            if (first.Equals("$alias", StringComparison.OrdinalIgnoreCase))
            {
                if (t.Count >= 3) result.Aliases[t[1]] = t[2];
                continue;
            }
            if (first.Equals("$random", StringComparison.OrdinalIgnoreCase))
            {
                CollectRandomTokens(lines, ref lineIndex, t);
                if (!ParseRandom(result, t)) return result;
                continue;
            }

            if (first.StartsWith("$", StringComparison.Ordinal)) continue;
            if (t.Count >= 3 && t[1] == "=")
            {
                if (!TrySetAssignmentFormat(ref format, AssignmentFormat.New)) return result;
                result.Sounds[first] = t[2];
            }
            else if (t.Count >= 2)
            {
                if (!TrySetAssignmentFormat(ref format, AssignmentFormat.Old)) return result;
                result.Sounds[first] = t[1];
            }
        }
        return result;
    }

    private static bool TrySetAssignmentFormat(ref AssignmentFormat current, AssignmentFormat next)
    {
        if (current == AssignmentFormat.None)
        {
            current = next;
            return true;
        }

        return current == next;
    }

    private static void CollectRandomTokens(string[] lines, ref int lineIndex, List<string> tokens)
    {
        while (!tokens.Contains("}") && lineIndex + 1 < lines.Length)
        {
            lineIndex++;
            tokens.AddRange(Tokenize(StripLineComment(lines[lineIndex])));
        }
    }

    private static bool TryReadConditional(string token, out TerrainBaseGame game)
    {
        if (token.Equals("$ifdoom", StringComparison.OrdinalIgnoreCase)) { game = TerrainBaseGame.Doom; return true; }
        if (token.Equals("$ifheretic", StringComparison.OrdinalIgnoreCase)) { game = TerrainBaseGame.Heretic; return true; }
        if (token.Equals("$ifhexen", StringComparison.OrdinalIgnoreCase)) { game = TerrainBaseGame.Hexen; return true; }
        if (token.Equals("$ifstrife", StringComparison.OrdinalIgnoreCase)) { game = TerrainBaseGame.Strife; return true; }
        game = default;
        return false;
    }

    private static bool ParseRandom(SndInfo result, List<string> tokens)
    {
        if (tokens.Count < 4) return false;
        string name = tokens[1];
        var sounds = new List<string>();
        for (int i = 2; i < tokens.Count; i++)
        {
            if (tokens[i] is "{" or "}") continue;
            sounds.Add(tokens[i]);
        }
        if (name.Length == 0 || sounds.Count == 0 || sounds.Contains(name, StringComparer.OrdinalIgnoreCase)) return false;
        result.RandomGroups[name] = sounds;
        return true;
    }

    private static string StripLineComment(string line)
    {
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') quoted = !quoted;
            if (quoted) continue;
            if (line[i] == '#') return line.Substring(0, i);
            if (line[i] == '/' && i + 1 < line.Length && line[i + 1] == '/') return line.Substring(0, i);
        }
        return line;
    }

    private static List<string> Tokenize(string s)
    {
        var toks = new List<string>();
        int n = s.Length;
        for (int p = 0; p < n;)
        {
            char c = s[p];
            if (char.IsWhiteSpace(c)) { p++; continue; }
            if (c == '"')
            {
                var sb = new StringBuilder();
                p++;
                while (p < n && s[p] != '"')
                {
                    if (s[p] == '\\' && p + 1 < n) { sb.Append(s[p + 1]); p += 2; }
                    else sb.Append(s[p++]);
                }
                if (p < n) p++;
                toks.Add(sb.ToString());
                continue;
            }
            if (c is '{' or '}')
            {
                toks.Add(c.ToString());
                p++;
                continue;
            }

            int b = p;
            while (p < n && !char.IsWhiteSpace(s[p]) && s[p] != '"' && s[p] != '{' && s[p] != '}') p++;
            toks.Add(s.Substring(b, p - b));
        }
        return toks;
    }
}
