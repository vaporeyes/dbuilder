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

public static class ScriptSyntaxHighlighting
{
    public const int ConstantImageIndex = 0;
    public const int KeywordImageIndex = 1;
    public const int SnippetImageIndex = 3;
    public const int PropertyImageIndex = 4;

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
}
