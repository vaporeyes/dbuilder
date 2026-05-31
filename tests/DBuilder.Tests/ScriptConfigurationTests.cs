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

    [Fact]
    public void MissingConfiguredExtensionsKeepUdbEmptyExtensionEntry()
    {
        var cfg = ScriptConfigurationInfo.FromText(@"description = ""Mystery"";");

        Assert.Equal(new[] { "" }, cfg.Extensions);
    }

    [Fact]
    public void LoadsScriptSnippetsFromConfiguredSnippetsDirectory()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_snippets_" + Guid.NewGuid().ToString("N"));
        string snippets = Path.Combine(dir, "ACS");
        Directory.CreateDirectory(snippets);
        try
        {
            File.WriteAllLines(Path.Combine(snippets, "Open Door.txt"), new[] { "script 1 OPEN", "{" });
            File.WriteAllText(Path.Combine(snippets, "Empty.txt"), "");
            const string text = """
                description = "ZDoom ACS";
                snippetsdir = "ACS";
                """;

            var cfg = ScriptConfigurationInfo.FromText(text, dir);

            Assert.Equal(new[] { "Open_Door" }, cfg.Snippets);
            Assert.Equal(new[] { "script 1 OPEN", "{" }, cfg.GetSnippet("open_door"));
            Assert.Null(cfg.GetSnippet("Empty"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CatalogLoadsTopLevelScriptConfigurationsByLowerFileName()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_scripts_" + Guid.NewGuid().ToString("N"));
        string nested = Path.Combine(dir, "Nested");
        Directory.CreateDirectory(nested);
        try
        {
            File.WriteAllText(Path.Combine(dir, "ZDoom_ACS.cfg"), """
                description = "ZDoom ACS";
                scripttype = "ACS";
                compiler = "acc";
                """);
            File.WriteAllText(Path.Combine(nested, "Nested.cfg"), """
                description = "Nested";
                scripttype = "DECORATE";
                """);

            var catalog = ScriptConfigurationCatalog.FromDirectory(dir);

            Assert.True(catalog.Configurations.ContainsKey("zdoom_acs.cfg"));
            Assert.False(catalog.Configurations.ContainsKey("nested.cfg"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CatalogPassesSnippetsPathToScriptConfigurations()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_scripts_" + Guid.NewGuid().ToString("N"));
        string snippetsRoot = Path.Combine(dir, "Snippets");
        string acsSnippets = Path.Combine(snippetsRoot, "ACS");
        Directory.CreateDirectory(acsSnippets);
        try
        {
            File.WriteAllText(Path.Combine(dir, "ZDoom_ACS.cfg"), """
                description = "ZDoom ACS";
                scripttype = "ACS";
                snippetsdir = "ACS";
                """);
            File.WriteAllLines(Path.Combine(acsSnippets, "Spawn Thing.txt"), new[] { "Thing_Spawn(0, 1, 0);" });

            var catalog = ScriptConfigurationCatalog.FromDirectory(dir, snippetsRoot);
            var cfg = catalog.Configurations["zdoom_acs.cfg"];

            Assert.Equal(new[] { "Thing_Spawn(0, 1, 0);" }, cfg.GetSnippet("Spawn_Thing"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CatalogSelectsAcsScriptConfigurationFromMapOverrideBeforeDefault()
    {
        var catalog = new ScriptConfigurationCatalog();
        catalog.Add("default_acs.cfg", ScriptConfigurationInfo.FromText("""
            description = "Default ACS";
            scripttype = "ACS";
            """));
        catalog.Add("map_acs.cfg", ScriptConfigurationInfo.FromText("""
            description = "Map ACS";
            scripttype = "ACS";
            """));

        var selected = catalog.GetScriptConfiguration(
            ScriptType.Acs,
            mapScriptCompiler: "map_acs.cfg",
            defaultScriptCompiler: "default_acs.cfg");

        Assert.NotNull(selected);
        Assert.Equal("Map ACS", selected.Description);
    }

    [Fact]
    public void CatalogSelectsDefaultAcsScriptConfigurationWhenMapOverrideIsEmpty()
    {
        var catalog = new ScriptConfigurationCatalog();
        catalog.Add("default_acs.cfg", ScriptConfigurationInfo.FromText("""
            description = "Default ACS";
            scripttype = "ACS";
            """));

        var selected = catalog.GetScriptConfiguration(
            ScriptType.Acs,
            defaultScriptCompiler: "default_acs.cfg");

        Assert.NotNull(selected);
        Assert.Equal("Default ACS", selected.Description);
    }

    [Fact]
    public void CatalogSelectsFirstMatchingNonAcsScriptType()
    {
        var catalog = new ScriptConfigurationCatalog();
        catalog.Add("decorate.cfg", ScriptConfigurationInfo.FromText("""
            description = "DECORATE";
            scripttype = "DECORATE";
            """));

        var selected = catalog.GetScriptConfiguration(ScriptType.Decorate);

        Assert.NotNull(selected);
        Assert.Equal("DECORATE", selected.Description);
    }
}
