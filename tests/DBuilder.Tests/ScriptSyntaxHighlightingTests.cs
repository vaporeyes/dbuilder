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
}
