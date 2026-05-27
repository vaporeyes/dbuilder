// ABOUTME: Tests UDB-style script editor configuration parsing for lexer, compiler and autocomplete metadata.
// ABOUTME: Covers script type mapping and case-preserving keyword, constant and property lookups.

using DBuilder.IO;

namespace DBuilder.Tests;

public class ScriptConfigurationTests
{
    [Fact]
    public void PlainTextConfigurationMatchesUdbDefaults()
    {
        var cfg = ScriptConfigurationInfo.PlainText;
        Assert.Equal("Plain text", cfg.Description);
        Assert.Equal(65001, cfg.CodePage);
        Assert.Equal(ScriptType.Unknown, cfg.ScriptType);
        Assert.Equal(new[] { "txt" }, cfg.Extensions);
    }

    [Fact]
    public void ParsesScriptConfigurationMetadata()
    {
        const string text = @"
description = ""ZDoom ACS"";
codepage = 65001;
extensions = ""acs, h"";
compiler = ""acc"";
parameters = ""-i %FI -o %FO"";
resultlump = ""BEHAVIOR"";
casesensitive = false;
insertcase = 1;
lexer = 35;
keywordhelp = ""acs.html"";
functionopen = ""("";
functionclose = "")"";
codeblockopen = ""{"";
codeblockclose = ""}"";
arrayopen = ""["";
arrayclose = ""]"";
argumentdelimiter = "","";
terminator = "";"";
extrawordchars = ""_"";
scripttype = ""ACS"";
keywords
{
    script = ""script number OPEN"";
    Thing_ChangeTID = ""Thing_ChangeTID(tid, newtid)"";
}
constants
{
    TRUE = ""true"";
    APROP_Health = ""health"";
}
properties
{
    Health = ""health"";
}";

        var cfg = ScriptConfigurationInfo.FromText(text);
        Assert.Equal("ZDoom ACS", cfg.Description);
        Assert.Equal("acc", cfg.CompilerName);
        Assert.Equal("-i %FI -o %FO", cfg.Parameters);
        Assert.Equal("BEHAVIOR", cfg.ResultLump);
        Assert.Equal(ScriptType.Acs, cfg.ScriptType);
        Assert.Equal(new[] { "acs", "h" }, cfg.Extensions);
        Assert.Contains('(', cfg.BraceChars);
        Assert.Contains('{', cfg.BraceChars);
        Assert.True(cfg.IsKeyword("thing_changetid"));
        Assert.Equal("Thing_ChangeTID", cfg.GetKeywordCase("thing_changetid"));
        Assert.Equal("Thing_ChangeTID(tid, newtid)", cfg.GetFunctionDefinition("Thing_ChangeTID"));
        Assert.True(cfg.IsConstant("aprop_health"));
        Assert.Equal("APROP_Health", cfg.GetConstantCase("aprop_health"));
        Assert.True(cfg.IsProperty("health"));
        Assert.Equal("Health", cfg.GetPropertyCase("health"));
    }

    [Fact]
    public void UnknownScriptTypeFallsBackToUnknown()
    {
        var cfg = ScriptConfigurationInfo.FromText(@"description = ""Mystery""; scripttype = ""doesnotexist"";");
        Assert.Equal(ScriptType.Unknown, cfg.ScriptType);
    }
}
