// ABOUTME: Parser for ZDoom SNDSEQ sequence declarations.
// ABOUTME: Captures sequence names and ordered commands without loading or playing audio.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DBuilder.IO;

public sealed class SndSeq
{
    public List<SndSeqSequence> Sequences { get; } = new();
}

public sealed class SndSeqSequence
{
    public string Name { get; init; } = "";
    public List<SndSeqCommand> Commands { get; } = new();
}

public sealed record SndSeqCommand(string Name, string? Sound = null, int? Value = null);

public static class SndSeqParser
{
    public static SndSeq Parse(string text)
    {
        var result = new SndSeq();
        var t = Tokenize(text);
        SndSeqSequence? current = null;
        for (int i = 0; i < t.Count;)
        {
            string token = t[i++];
            if (token.StartsWith(":", StringComparison.Ordinal))
            {
                current = new SndSeqSequence { Name = token.Substring(1) };
                result.Sequences.Add(current);
                continue;
            }
            if (current == null) continue;

            string cmd = token.ToLowerInvariant();
            switch (cmd)
            {
                case "play":
                case "playuntildone":
                case "stopsound":
                    current.Commands.Add(new SndSeqCommand(cmd, i < t.Count ? t[i++] : null));
                    break;
                case "delay":
                case "delayrand":
                    if (ReadInt(t, ref i, out int value)) current.Commands.Add(new SndSeqCommand(cmd, Value: value));
                    break;
                case "end":
                    current.Commands.Add(new SndSeqCommand(cmd));
                    current = null;
                    break;
                default:
                    break;
            }
        }
        return result;
    }

    private static bool ReadInt(List<string> t, ref int i, out int value)
    {
        value = 0;
        if (i < t.Count && int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            i++;
            return true;
        }
        return false;
    }

    private static List<string> Tokenize(string s)
    {
        var toks = new List<string>();
        int n = s.Length;
        for (int p = 0; p < n;)
        {
            char c = s[p];
            if (char.IsWhiteSpace(c)) { p++; continue; }
            if (c == '/' && p + 1 < n && s[p + 1] == '/') { p += 2; while (p < n && s[p] != '\n') p++; continue; }
            if (c == '#') { while (p < n && s[p] != '\n') p++; continue; }
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

            int b = p;
            while (p < n && !char.IsWhiteSpace(s[p]) && s[p] != '"') p++;
            toks.Add(s.Substring(b, p - b));
        }
        return toks;
    }
}
