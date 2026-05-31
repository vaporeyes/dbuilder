// ABOUTME: Detects script text type using UDB script editor header heuristics.
// ABOUTME: Provides UI-independent MODELDEF, ACS, DECORATE, and ZScript detection for script resources.

using System;
using System.Collections.Generic;
using System.Text;

namespace DBuilder.IO;

public static class ScriptTypeDetector
{
    public static ScriptType Detect(string text)
    {
        var tokens = Tokenize(text);
        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i].ToUpperInvariant();
            ScriptType type = token switch
            {
                "MODEL" => DetectModelDef(tokens, i),
                "SCRIPT" => DetectAcs(tokens, i),
                "ACTOR" => DetectDecorate(tokens, i),
                "CLASS" or "STRUCT" or "ENUM" or "EXTEND" => DetectZScript(tokens, i),
                _ => ScriptType.Unknown,
            };
            if (type != ScriptType.Unknown) return type;
        }
        return ScriptType.Unknown;
    }

    private static ScriptType DetectModelDef(IReadOnlyList<string> tokens, int index)
        => TokenAt(tokens, index + 2) == "{" ? ScriptType.ModelDef : ScriptType.Unknown;

    private static ScriptType DetectAcs(IReadOnlyList<string> tokens, int index)
        => TokenAt(tokens, index + 3) == "{" ? ScriptType.Acs : ScriptType.Unknown;

    private static ScriptType DetectDecorate(IReadOnlyList<string> tokens, int index)
    {
        string token = TokenAt(tokens, index + 2);
        if (token == ":" || token == "{" || token.Equals("REPLACES", StringComparison.OrdinalIgnoreCase))
            return ScriptType.Decorate;

        token = TokenAt(tokens, index + 3);
        if (token != "{") token = TokenAt(tokens, index + 4);
        return token == "{" ? ScriptType.Decorate : ScriptType.Unknown;
    }

    private static ScriptType DetectZScript(IReadOnlyList<string> tokens, int index)
    {
        string original = tokens[index].ToUpperInvariant();
        int offset = 0;
        if (original == "EXTEND")
        {
            original = TokenAt(tokens, index + 1).ToUpperInvariant();
            offset = 1;
        }

        string token = TokenAt(tokens, index + offset + 2);
        if ((original != "ENUM" && (token == ":" || token == ";"))
            || token == "{"
            || (original == "CLASS" && token.Equals("REPLACES", StringComparison.OrdinalIgnoreCase)))
            return ScriptType.ZScript;

        token = TokenAt(tokens, index + offset + 3);
        return token is "{" or ";" ? ScriptType.ZScript : ScriptType.Unknown;
    }

    private static string TokenAt(IReadOnlyList<string> tokens, int index)
        => index >= 0 && index < tokens.Count ? tokens[index] : "";

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        for (int i = 0; i < text.Length;)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                i += 2;
                while (i < text.Length && text[i] != '\n') i++;
                continue;
            }
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) i++;
                i = Math.Min(i + 2, text.Length);
                continue;
            }
            if (c == '"')
            {
                tokens.Add(ReadQuoted(text, ref i));
                continue;
            }
            if (c is '{' or '}' or ':' or ';' or ',')
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }

            int start = i;
            while (i < text.Length
                   && !char.IsWhiteSpace(text[i])
                   && text[i] is not ('{' or '}' or ':' or ';' or ',' or '"'))
            {
                if (text[i] == '/' && i + 1 < text.Length && (text[i + 1] == '/' || text[i + 1] == '*')) break;
                i++;
            }
            if (i > start) tokens.Add(text[start..i]);
            else i++;
        }
        return tokens;
    }

    private static string ReadQuoted(string text, ref int index)
    {
        var builder = new StringBuilder();
        index++;
        while (index < text.Length && text[index] != '"')
        {
            if (text[index] == '\\' && index + 1 < text.Length)
            {
                builder.Append(text[index + 1]);
                index += 2;
            }
            else
            {
                builder.Append(text[index]);
                index++;
            }
        }
        if (index < text.Length) index++;
        return builder.ToString();
    }
}
