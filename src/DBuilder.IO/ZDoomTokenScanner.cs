// ABOUTME: Shared token scanner for simple ZDoom-style text lumps.
// ABOUTME: Handles comments, quoted strings, and punctuation used by lightweight data parsers.

using System.Collections.Generic;
using System.Text;

namespace DBuilder.IO;

internal static class ZDoomTokenScanner
{
    public static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        int n = text.Length;
        for (int p = 0; p < n;)
        {
            char c = text[p];
            if (char.IsWhiteSpace(c)) { p++; continue; }
            if (c == '/' && p + 1 < n && text[p + 1] == '/') { p += 2; while (p < n && text[p] != '\n') p++; continue; }
            if (c == '/' && p + 1 < n && text[p + 1] == '*')
            {
                p += 2;
                while (p + 1 < n && !(text[p] == '*' && text[p + 1] == '/')) p++;
                p = p + 1 < n ? p + 2 : n;
                continue;
            }
            if (c == '#') { while (p < n && text[p] != '\n') p++; continue; }
            if (c == '"')
            {
                var sb = new StringBuilder();
                p++;
                while (p < n && text[p] != '"')
                {
                    if (text[p] == '\\' && p + 1 < n) { sb.Append(text[p + 1]); p += 2; }
                    else sb.Append(text[p++]);
                }
                if (p < n) p++;
                tokens.Add(sb.ToString());
                continue;
            }
            if (c is '{' or '}' or '=' or ';' or ',')
            {
                tokens.Add(c.ToString());
                p++;
                continue;
            }

            int b = p;
            while (p < n && !char.IsWhiteSpace(text[p]) && text[p] != '"' && text[p] != '{' && text[p] != '}' && text[p] != '=' && text[p] != ';' && text[p] != ',') p++;
            tokens.Add(text.Substring(b, p - b));
        }
        return tokens;
    }
}
