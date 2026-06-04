// ABOUTME: Covers UDB-style script lexer keyword set preparation.
// ABOUTME: Verifies lexer indexes, case normalization, and DECORATE property splitting.

using DBuilder.IO;

namespace DBuilder.Tests;

public class ScriptSyntaxHighlightingTests
{
    [Fact]
    public void ReadsLexerKeywordIndexesLikeUdb()
    {
        var lexerConfig = new Configuration();
        lexerConfig.InputConfiguration("""
            lexer35
            {
                keywordsindex = 0;
                constantsindex = 1;
                propertiesindex = 3;
                snippetindex = 2;
            }
            """);

        var definition = ScriptSyntaxHighlighting.GetLexerDefinition(lexerConfig, 35);

        Assert.Equal(35, definition.Lexer);
        Assert.Equal(0, definition.KeywordsIndex);
        Assert.Equal(1, definition.ConstantsIndex);
        Assert.Equal(3, definition.PropertiesIndex);
        Assert.Equal(2, definition.SnippetIndex);
    }

    [Fact]
    public void BuildsCaseInsensitiveKeywordSetsLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            description = "ACS";
            lexer = 35;
            casesensitive = false;
            scripttype = "ACS";

            keywords { Script = ""; Function = ""; }
            constants { TRUE = ""; }
            properties { GameSkill = ""; }
            """);
        var definition = new ScriptLexerDefinition(35, 0, 1, 3, -1);

        var sets = ScriptSyntaxHighlighting.BuildKeywordSets(config, definition);

        Assert.Collection(
            sets,
            set =>
            {
                Assert.Equal(0, set.Index);
                Assert.Equal("keywords", set.Kind);
                Assert.Equal("function script", set.Words);
            },
            set =>
            {
                Assert.Equal(3, set.Index);
                Assert.Equal("properties", set.Kind);
                Assert.Equal("gameskill", set.Words);
            },
            set =>
            {
                Assert.Equal(1, set.Index);
                Assert.Equal("constants", set.Kind);
                Assert.Equal("true", set.Words);
            });
    }

    [Fact]
    public void SplitsDecoratePropertiesForHighlightingLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            description = "DECORATE";
            casesensitive = true;
            scripttype = "DECORATE";

            properties
            {
                A.B = "";
                A.C = "";
                RenderStyle:Add = "";
            }
            """);
        var definition = new ScriptLexerDefinition(35, -1, -1, 3, -1);

        var set = Assert.Single(ScriptSyntaxHighlighting.BuildKeywordSets(config, definition));

        Assert.Equal(3, set.Index);
        Assert.Equal("properties", set.Kind);
        Assert.Equal("A B C RenderStyleAdd", set.Words);
    }

    [Fact]
    public void BuildsAutocompleteItemsLikeUdb()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_snippets_" + Guid.NewGuid().ToString("N"));
        string snippets = Path.Combine(dir, "ACS");
        Directory.CreateDirectory(snippets);
        try
        {
            File.WriteAllText(Path.Combine(snippets, "Script.txt"), "script [EP]");
            File.WriteAllText(Path.Combine(snippets, "Door Open.txt"), "Door_Open();");
            var config = ScriptConfigurationInfo.FromText("""
                snippetsdir = "ACS";
                keywords { Script = ""; Function = ""; }
                constants { Function = ""; OPEN = ""; }
                properties { Door.Open = ""; }
                """, dir);

            var entries = ScriptSyntaxHighlighting.BuildAutoCompleteItems(config)
                .Select(item => item.Entry)
                .ToArray();

            Assert.Contains("Function?1", entries);
            Assert.Contains("Door.Open?4", entries);
            Assert.Contains("OPEN?0", entries);
            Assert.Contains("Script?3", entries);
            Assert.Contains("Door_Open?3", entries);
            Assert.DoesNotContain("Script?1", entries);
            Assert.DoesNotContain("Function?0", entries);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void BuildsAutocompleteListTextLikeUdb()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_autocomplete_" + Guid.NewGuid().ToString("N"));
        string snippets = Path.Combine(dir, "ACS");
        Directory.CreateDirectory(snippets);
        try
        {
            File.WriteAllText(Path.Combine(snippets, "Door Open.txt"), "Door_Open();");
            var config = ScriptConfigurationInfo.FromText("""
                snippetsdir = "ACS";
                keywords { Function = ""; Script = ""; }
                constants { OPEN = ""; Script = ""; }
                properties { Door.Open = ""; }
                """, dir);

            string list = ScriptSyntaxHighlighting.BuildAutoCompleteList(config);

            Assert.Equal("Function?1 Script?1 Door.Open?4 OPEN?0 Door_Open?3", list);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void FindsFunctionCallArgumentPositionLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            functionopen = "(";
            functionclose = ")";
            argumentdelimiter = ",";
            terminator = ";";
            keywords { Thing_Spawn = "Thing_Spawn(tid, type, angle)"; }
            """);
        const string text = "Thing_Spawn(1, 3004, ";

        var position = ScriptSyntaxHighlighting.FindFunctionCallPosition(config, text, text.Length);

        Assert.NotNull(position);
        Assert.Equal("Thing_Spawn", position.FunctionName);
        Assert.Equal(2, position.ArgumentIndex);
        Assert.Equal(0, position.FunctionStartOffset);
    }

    [Fact]
    public void FindsParentFunctionWhenNestedArgumentClosesLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            functionopen = "(";
            functionclose = ")";
            argumentdelimiter = ",";
            terminator = ";";
            keywords
            {
                Outer = "Outer(a, b)";
                Inner = "Inner(a)";
            }
            """);
        const string text = "Outer(Inner(1), ";

        var position = ScriptSyntaxHighlighting.FindFunctionCallPosition(config, text, text.Length);

        Assert.NotNull(position);
        Assert.Equal("Outer", position.FunctionName);
        Assert.Equal(1, position.ArgumentIndex);
        Assert.Equal(0, position.FunctionStartOffset);
    }

    [Fact]
    public void StopsFunctionCallSearchAtTerminatorLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            functionopen = "(";
            functionclose = ")";
            argumentdelimiter = ",";
            terminator = ";";
            keywords { Thing_Spawn = "Thing_Spawn(tid)"; }
            """);
        const string text = "Thing_Spawn(1); more text";

        var position = ScriptSyntaxHighlighting.FindFunctionCallPosition(config, text, text.Length);

        Assert.Null(position);
    }

    [Fact]
    public void FunctionCallSearchRespectsCaseSensitiveConfigurationsLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            casesensitive = true;
            functionopen = "(";
            functionclose = ")";
            argumentdelimiter = ",";
            terminator = ";";
            keywords { Thing_Spawn = "Thing_Spawn(tid)"; }
            """);

        Assert.NotNull(ScriptSyntaxHighlighting.FindFunctionCallPosition(config, "Thing_Spawn(", "Thing_Spawn(".Length));
        Assert.Null(ScriptSyntaxHighlighting.FindFunctionCallPosition(config, "thing_spawn(", "thing_spawn(".Length));
    }

    [Fact]
    public void FunctionCallSearchUsesConfiguredExtraWordCharactersLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            extrawordchars = ".";
            functionopen = "(";
            functionclose = ")";
            argumentdelimiter = ",";
            terminator = ";";
            keywords { Namespace.Function = "Namespace.Function(value)"; }
            """);
        const string text = "Namespace.Function(";

        var position = ScriptSyntaxHighlighting.FindFunctionCallPosition(config, text, text.Length);

        Assert.NotNull(position);
        Assert.Equal("Namespace.Function", position.FunctionName);
        Assert.Equal(0, position.FunctionStartOffset);
    }

    [Fact]
    public void FunctionCallSearchClampsCaretOffsetLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            functionopen = "(";
            functionclose = ")";
            argumentdelimiter = ",";
            terminator = ";";
            keywords { Thing_Spawn = "Thing_Spawn(tid)"; }
            """);
        const string text = "Thing_Spawn(";

        Assert.Null(ScriptSyntaxHighlighting.FindFunctionCallPosition(config, text, -1));
        Assert.NotNull(ScriptSyntaxHighlighting.FindFunctionCallPosition(config, text, text.Length + 20));
    }

    [Fact]
    public void BuildsFunctionCallTipHighlightLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            functionopen = "(";
            functionclose = ")";
            argumentdelimiter = ",";
            keywords { Thing_Spawn = "Thing_Spawn(tid, type, angle)"; }
            """);
        var position = new ScriptFunctionCallPosition("Thing_Spawn", 1, 4);

        var tip = ScriptSyntaxHighlighting.BuildFunctionCallTip(config, position);

        Assert.NotNull(tip);
        Assert.Equal("Thing_Spawn", tip.FunctionName);
        Assert.Equal("Thing_Spawn(tid, type, angle)", tip.Definition);
        Assert.Equal(4, tip.FunctionStartOffset);
        Assert.Equal(" type", tip.Definition[tip.HighlightStart..tip.HighlightEnd]);
    }

    [Fact]
    public void LeavesFunctionCallTipHighlightEmptyWhenArgumentIsMissingLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            functionopen = "(";
            functionclose = ")";
            argumentdelimiter = ",";
            keywords { Thing_Spawn = "Thing_Spawn(tid)"; }
            """);
        var position = new ScriptFunctionCallPosition("Thing_Spawn", 3, 0);

        var tip = ScriptSyntaxHighlighting.BuildFunctionCallTip(config, position);

        Assert.NotNull(tip);
        Assert.Equal(0, tip.HighlightStart);
        Assert.Equal(0, tip.HighlightEnd);
    }
}
