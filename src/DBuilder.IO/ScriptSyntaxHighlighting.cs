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

public static class ScriptSyntaxHighlighting
{
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
