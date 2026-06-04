// ABOUTME: Extracts UDB-style script navigator entries for script editor function bars.
// ABOUTME: Covers ACS, DECORATE, MODELDEF, and ZScript text without depending on editor UI controls.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DBuilder.IO;

public sealed record ScriptNavigatorItem(string Name, int StartOffset, bool IsIncluded = false, bool Skipped = false)
{
    public static int CompareByName(ScriptNavigatorItem left, ScriptNavigatorItem right)
        => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
}

public static class ScriptNavigator
{
    public static IReadOnlyList<ScriptNavigatorItem> GetItems(ScriptType type, string text)
    {
        var tokens = Tokenize(text);
        var items = new List<ScriptNavigatorItem>();
        switch (type)
        {
            case ScriptType.Acs: AddAcsItems(tokens, text, items); break;
            case ScriptType.Decorate: AddHeaderItems(tokens, items, "ACTOR"); break;
            case ScriptType.ModelDef: AddModelItems(tokens, items); break;
            case ScriptType.ZScript: AddZScriptItems(tokens, items); break;
        }
        items.Sort(ScriptNavigatorItem.CompareByName);
        return items;
    }

    private static void AddAcsItems(IReadOnlyList<Token> tokens, string text, List<ScriptNavigatorItem> items)
    {
        int braceLevel = 0;
        bool skipNextScript = false;
        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i].Text.ToLowerInvariant();
            if (token == "{") { braceLevel++; continue; }
            if (token == "}") { braceLevel--; continue; }
            if (braceLevel > 0) continue;

            if (token == "$skip")
            {
                skipNextScript = true;
                continue;
            }

            if (token == "script")
            {
                AddAcsScript(tokens, text, ref i, items, ref skipNextScript);
            }
            else if (token == "function")
            {
                AddAcsFunction(tokens, ref i, items);
            }
        }
    }

    private static void AddAcsScript(
        IReadOnlyList<Token> tokens,
        string text,
        ref int index,
        List<ScriptNavigatorItem> items,
        ref bool skipNextScript)
    {
        int nameIndex = index + 1;
        if (nameIndex >= tokens.Count) return;
        var nameToken = tokens[nameIndex];
        var args = ParseAcsArgs(tokens, ref nameIndex);
        string argsText = FormatAcsArgs(args);

        if (nameToken.Quoted)
        {
            items.Add(new ScriptNavigatorItem(nameToken.Text + " " + argsText, nameToken.StartOffset, Skipped: skipNextScript));
            skipNextScript = false;
            index = nameIndex;
            return;
        }

        if (int.TryParse(nameToken.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
        {
            string customName = NumberedScriptCustomName(tokens, nameIndex, text);
            string name = customName.Length > 0 ? customName + " [Script " + number + "]" : "Script " + number;
            items.Add(new ScriptNavigatorItem(name + argsText, nameToken.StartOffset, Skipped: skipNextScript));
            skipNextScript = false;
            index = nameIndex;
        }
    }

    private static string NumberedScriptCustomName(IReadOnlyList<Token> tokens, int startIndex, string text)
    {
        for (int i = startIndex + 1; i < tokens.Count; i++)
        {
            if (tokens[i].Text == "{") return LineCommentAfter(text, tokens[i].StartOffset + 1);
            if (tokens[i].Text is ";" or "}") return "";
        }

        return "";
    }

    private static string LineCommentAfter(string text, int offset)
    {
        int lineEnd = text.IndexOf('\n', offset);
        if (lineEnd < 0) lineEnd = text.Length;
        int comment = text.IndexOf("//", offset, lineEnd - offset, StringComparison.Ordinal);
        return comment < 0 ? "" : text[(comment + 2)..lineEnd].Trim();
    }

    private static void AddAcsFunction(IReadOnlyList<Token> tokens, ref int index, List<ScriptNavigatorItem> items)
    {
        if (index + 2 >= tokens.Count) return;
        var returnType = tokens[index + 1];
        var name = tokens[index + 2];
        int argIndex = index + 2;
        var args = ParseAcsArgs(tokens, ref argIndex);
        items.Add(new ScriptNavigatorItem(returnType.Text + " " + name.Text + FormatAcsArgs(args), name.StartOffset));
        index = argIndex;
    }

    private static List<(string Type, string Name)> ParseAcsArgs(IReadOnlyList<Token> tokens, ref int index)
    {
        var args = new List<(string Type, string Name)>();
        int next = index + 1;
        if (next >= tokens.Count || tokens[next].Text != "(")
        {
            if (next < tokens.Count) args.Add((tokens[next].Text.ToUpperInvariant(), ""));
            index = next;
            return args;
        }

        index = next;
        while (index + 1 < tokens.Count)
        {
            index++;
            string type = tokens[index].Text;
            if (type is ")" or "{" or "}") break;
            if (type.Equals("void", StringComparison.OrdinalIgnoreCase))
            {
                args.Add(("void", ""));
                break;
            }
            if (index + 1 >= tokens.Count) break;
            string name = tokens[++index].Text;
            args.Add((type, name));
            if (index + 1 >= tokens.Count || tokens[index + 1].Text != ",") break;
            index++;
        }
        return args;
    }

    private static string FormatAcsArgs(IReadOnlyList<(string Type, string Name)> args)
    {
        if (args.Count == 0) return "(void)";
        var parts = new List<string>(args.Count);
        foreach (var arg in args) parts.Add((arg.Type + " " + arg.Name).TrimEnd());
        return "(" + string.Join(", ", parts) + ")";
    }

    private static void AddHeaderItems(IReadOnlyList<Token> tokens, List<ScriptNavigatorItem> items, string header)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            if (!tokens[i].Text.Equals(header, StringComparison.OrdinalIgnoreCase)) continue;
            var definition = new List<string> { tokens[i].Text };
            int line = tokens[i].Line;
            for (int j = i + 1; j < tokens.Count && tokens[j].Line == line; j++)
            {
                if (tokens[j].Text is "{" or "}") break;
                definition.Add(tokens[j].Text);
            }
            if (definition.Count > 1) items.Add(new ScriptNavigatorItem(string.Join(" ", definition), tokens[i].StartOffset));
        }
    }

    private static void AddModelItems(IReadOnlyList<Token> tokens, List<ScriptNavigatorItem> items)
    {
        for (int i = 0; i + 2 < tokens.Count; i++)
        {
            if (!tokens[i].Text.Equals("model", StringComparison.OrdinalIgnoreCase)) continue;
            if (tokens[i + 2].Text != "{") continue;

            items.Add(new ScriptNavigatorItem(tokens[i + 1].Text, tokens[i + 1].StartOffset));
            i = SkipBlock(tokens, i + 2);
        }
    }

    private static int SkipBlock(IReadOnlyList<Token> tokens, int openBraceIndex)
    {
        int level = 0;
        for (int i = openBraceIndex; i < tokens.Count; i++)
        {
            if (tokens[i].Text == "{") level++;
            else if (tokens[i].Text == "}")
            {
                level--;
                if (level == 0) return i;
            }
        }

        return openBraceIndex;
    }

    private static void AddZScriptItems(IReadOnlyList<Token> tokens, List<ScriptNavigatorItem> items)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i].Text.ToUpperInvariant();
            if (token is not ("CLASS" or "STRUCT" or "ENUM")) continue;
            var definition = new List<string> { tokens[i].Text };
            int line = tokens[i].Line;
            for (int j = i + 1; j < tokens.Count && tokens[j].Line == line; j++)
            {
                if (tokens[j].Text is "{" or "}") break;
                definition.Add(tokens[j].Text);
            }
            if (definition.Count > 1) items.Add(new ScriptNavigatorItem(string.Join(" ", definition), tokens[i].StartOffset));
        }
    }

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        int line = 0;
        for (int i = 0; i < text.Length;)
        {
            char c = text[i];
            if (c == '\n') { line++; i++; continue; }
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
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/'))
                {
                    if (text[i] == '\n') line++;
                    i++;
                }
                i = Math.Min(i + 2, text.Length);
                continue;
            }
            if (c == '"')
            {
                int start = i;
                tokens.Add(new Token(ReadQuoted(text, ref i, ref line), start, line, Quoted: true));
                continue;
            }
            if (c is '{' or '}' or '(' or ')' or ':' or ';' or ',')
            {
                tokens.Add(new Token(c.ToString(), i, line));
                i++;
                continue;
            }

            int tokenStart = i;
            while (i < text.Length
                   && !char.IsWhiteSpace(text[i])
                   && text[i] is not ('{' or '}' or '(' or ')' or ':' or ';' or ',' or '"'))
            {
                if (text[i] == '/' && i + 1 < text.Length && (text[i + 1] == '/' || text[i + 1] == '*')) break;
                i++;
            }
            if (i > tokenStart) tokens.Add(new Token(text[tokenStart..i], tokenStart, line));
            else i++;
        }
        return tokens;
    }

    private static string ReadQuoted(string text, ref int index, ref int line)
    {
        var builder = new StringBuilder();
        index++;
        while (index < text.Length && text[index] != '"')
        {
            if (text[index] == '\n') line++;
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

    private readonly record struct Token(string Text, int StartOffset, int Line, bool Quoted = false);
}
