// ABOUTME: Builds UDB-style script lexer keyword sets for syntax highlighting.
// ABOUTME: Keeps Scintilla lexer configuration behavior testable outside the editor UI.

using System;
using System.Collections.Generic;

namespace DBuilder.IO;

public sealed record ScriptLexerDefinition(
    int Lexer,
    int KeywordsIndex,
    int ConstantsIndex,
    int PropertiesIndex,
    int SnippetIndex);

public sealed record ScriptSyntaxKeywordSet(int Index, string Kind, string Words);

public sealed record ScriptAutoCompleteItem(string Word, int ImageIndex)
{
    public string Entry => Word + "?" + ImageIndex;
}

public sealed record ScriptFunctionCallPosition(string FunctionName, int ArgumentIndex, int FunctionStartOffset);

public sealed record ScriptFunctionCallTip(
    string FunctionName,
    string Definition,
    int FunctionStartOffset,
    int HighlightStart,
    int HighlightEnd);

public static class ScriptSyntaxHighlighting
{
    public const int ConstantImageIndex = 0;
    public const int KeywordImageIndex = 1;
    public const int SnippetImageIndex = 3;
    public const int PropertyImageIndex = 4;
    private const int MaxBacktrackLength = 200;

    public static ScriptLexerDefinition GetLexerDefinition(Configuration lexerConfiguration, int lexer)
    {
        string name = "lexer" + lexer;
        return new ScriptLexerDefinition(
            lexer,
            lexerConfiguration.ReadSetting(name + ".keywordsindex", -1),
            lexerConfiguration.ReadSetting(name + ".constantsindex", -1),
            lexerConfiguration.ReadSetting(name + ".propertiesindex", -1),
            lexerConfiguration.ReadSetting(name + ".snippetindex", -1));
    }

    public static IReadOnlyList<ScriptSyntaxKeywordSet> BuildKeywordSets(
        ScriptConfigurationInfo scriptConfiguration,
        ScriptLexerDefinition lexerDefinition)
    {
        var sets = new List<ScriptSyntaxKeywordSet>();
        AddSet(sets, lexerDefinition.KeywordsIndex, "keywords", scriptConfiguration.Keywords, scriptConfiguration.CaseSensitive);
        AddSet(sets, lexerDefinition.PropertiesIndex, "properties", PropertyWords(scriptConfiguration), scriptConfiguration.CaseSensitive);
        AddSet(sets, lexerDefinition.ConstantsIndex, "constants", scriptConfiguration.Constants, scriptConfiguration.CaseSensitive);
        AddSet(sets, lexerDefinition.SnippetIndex, "snippets", scriptConfiguration.Snippets, scriptConfiguration.CaseSensitive);
        return sets;
    }

    public static IReadOnlyList<ScriptAutoCompleteItem> BuildAutoCompleteItems(ScriptConfigurationInfo scriptConfiguration)
    {
        var items = new Dictionary<string, ScriptAutoCompleteItem>(StringComparer.Ordinal);
        var snippets = new HashSet<string>(scriptConfiguration.Snippets, StringComparer.Ordinal);

        foreach (string keyword in scriptConfiguration.Keywords)
        {
            if (!snippets.Contains(keyword))
                items.TryAdd(keyword, new ScriptAutoCompleteItem(keyword, KeywordImageIndex));
        }

        foreach (string property in scriptConfiguration.Properties)
        {
            if (!snippets.Contains(property))
                items.TryAdd(property, new ScriptAutoCompleteItem(property, PropertyImageIndex));
        }

        foreach (string constant in scriptConfiguration.Constants)
        {
            if (!items.ContainsKey(constant) && !snippets.Contains(constant))
                items.Add(constant, new ScriptAutoCompleteItem(constant, ConstantImageIndex));
        }

        foreach (string snippet in scriptConfiguration.Snippets)
            items.TryAdd(snippet, new ScriptAutoCompleteItem(snippet, SnippetImageIndex));

        return new List<ScriptAutoCompleteItem>(items.Values);
    }

    public static ScriptFunctionCallPosition? FindFunctionCallPosition(
        ScriptConfigurationInfo scriptConfiguration,
        string text,
        int caretOffset)
    {
        if (scriptConfiguration.ArgumentDelimiter.Length == 0
            || scriptConfiguration.FunctionClose.Length == 0
            || scriptConfiguration.FunctionOpen.Length == 0
            || scriptConfiguration.Terminator.Length == 0)
        {
            return null;
        }

        int argumentDelimiter = scriptConfiguration.ArgumentDelimiter[0];
        int functionClose = scriptConfiguration.FunctionClose[0];
        int functionOpen = scriptConfiguration.FunctionOpen[0];
        int terminator = scriptConfiguration.Terminator[0];
        int position = Math.Min(caretOffset, text.Length);
        int limit = Math.Max(0, position - MaxBacktrackLength);
        int bracketLevel = 0;
        int argumentIndex = 0;

        while (position > limit)
        {
            position--;
            int current = text[position];
            if (current == functionClose)
            {
                bracketLevel++;
            }
            else if (current == functionOpen)
            {
                bracketLevel--;
                if (bracketLevel < 0)
                {
                    int wordPosition = SkipWhitespaceBackward(text, position - 1, limit);
                    if (wordPosition < limit) break;

                    int wordStart = WordStart(text, wordPosition);
                    int wordEnd = WordEnd(text, wordPosition);
                    string word = text[wordStart..wordEnd];
                    if (word.Length == 0) break;
                    if (word[0] == argumentDelimiter)
                    {
                        bracketLevel++;
                        argumentIndex = 0;
                    }
                    else if (scriptConfiguration.IsKeyword(word))
                    {
                        return new ScriptFunctionCallPosition(
                            scriptConfiguration.GetKeywordCase(word),
                            argumentIndex,
                            wordStart);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else if (current == argumentDelimiter)
            {
                if (bracketLevel == 0) argumentIndex++;
            }
            else if (current == terminator)
            {
                break;
            }
        }

        return null;
    }

    public static ScriptFunctionCallTip? BuildFunctionCallTip(
        ScriptConfigurationInfo scriptConfiguration,
        ScriptFunctionCallPosition position)
    {
        string? definition = scriptConfiguration.GetFunctionDefinition(position.FunctionName);
        if (definition is null) return null;

        int highlightStart = 0;
        int highlightEnd = 0;
        int argsOpenPosition = definition.IndexOf(scriptConfiguration.FunctionOpen, StringComparison.Ordinal);
        int argsClosePosition = definition.LastIndexOf(scriptConfiguration.FunctionClose, StringComparison.Ordinal);
        if (argsOpenPosition > -1
            && argsClosePosition > -1
            && scriptConfiguration.ArgumentDelimiter.Length > 0)
        {
            string argsText = definition.Substring(argsOpenPosition + 1, argsClosePosition - argsOpenPosition - 1);
            string[] args = argsText.Split(scriptConfiguration.ArgumentDelimiter[0]);
            if (position.ArgumentIndex >= 0 && position.ArgumentIndex < args.Length)
            {
                int argOffset = 0;
                for (int i = 0; i < position.ArgumentIndex; i++) argOffset += args[i].Length + 1;
                highlightStart = argsOpenPosition + argOffset + 1;
                highlightEnd = highlightStart + args[position.ArgumentIndex].Length;
            }
        }

        return new ScriptFunctionCallTip(
            position.FunctionName,
            definition,
            position.FunctionStartOffset,
            highlightStart,
            highlightEnd);
    }

    private static void AddSet(
        List<ScriptSyntaxKeywordSet> sets,
        int index,
        string kind,
        IEnumerable<string> words,
        bool caseSensitive)
    {
        if (index < 0) return;
        string text = string.Join(" ", words);
        if (!caseSensitive) text = text.ToLowerInvariant();
        sets.Add(new ScriptSyntaxKeywordSet(index, kind, text));
    }

    private static IReadOnlyList<string> PropertyWords(ScriptConfigurationInfo scriptConfiguration)
    {
        if (scriptConfiguration.ScriptType != ScriptType.Decorate) return scriptConfiguration.Properties;

        var result = new List<string>();
        var added = new HashSet<string>(StringComparer.Ordinal);
        foreach (string property in scriptConfiguration.Properties)
        {
            string normalized = property.Replace(":", "");
            if (normalized.Contains('.', StringComparison.Ordinal))
            {
                foreach (string part in normalized.Split('.', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (added.Add(part)) result.Add(part);
                }
            }
            else if (added.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    private static int SkipWhitespaceBackward(string text, int position, int limit)
    {
        while (position >= limit && char.IsWhiteSpace(text[position])) position--;
        return position;
    }

    private static int WordStart(string text, int position)
    {
        int start = position;
        while (start > 0 && IsWordChar(text[start - 1])) start--;
        return start;
    }

    private static int WordEnd(string text, int position)
    {
        int end = position;
        while (end < text.Length && IsWordChar(text[end])) end++;
        return end;
    }

    private static bool IsWordChar(char c)
        => char.IsLetterOrDigit(c) || c == '_';
}
