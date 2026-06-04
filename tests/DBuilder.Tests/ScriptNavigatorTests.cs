// ABOUTME: Tests script navigator entries that mirror UDB script handler function bars.
// ABOUTME: Covers ACS, DECORATE, MODELDEF, and ZScript header extraction and sorting.

using DBuilder.IO;

namespace DBuilder.Tests;

public class ScriptNavigatorTests
{
    [Fact]
    public void ExtractsAcsScriptsAndFunctionsWithArguments()
    {
        const string text = """
            $skip
            script 12 OPEN
            { // Door opener
            }

            script "NamedScript" (int tid, str label)
            {
            }

            function int CountThings(int tid)
            {
                return 0;
            }
            """;

        var items = ScriptNavigator.GetItems(ScriptType.Acs, text);

        Assert.Equal("Door opener [Script 12](OPEN)", items[0].Name);
        Assert.Equal("int CountThings(int tid)", items[1].Name);
        Assert.Equal("NamedScript (int tid, str label)", items[2].Name);
        Assert.True(items[0].Skipped);
    }

    [Fact]
    public void ExtractsDecorateActorHeaders()
    {
        const string text = """
            actor ZombieReplacement : DoomImp replaces DoomImp
            {
            }

            actor Aardwolf 30000
            {
            }
            """;

        var items = ScriptNavigator.GetItems(ScriptType.Decorate, text);

        Assert.Equal(new[] { "actor Aardwolf 30000", "actor ZombieReplacement : DoomImp replaces DoomImp" }, items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public void ExtractsModeldefModelNames()
    {
        const string text = """
            model Zed
            {
            }

            model Alpha
            {
            }
            """;

        var items = ScriptNavigator.GetItems(ScriptType.ModelDef, text);

        Assert.Equal(new[] { "Alpha", "Zed" }, items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public void ExtractsZScriptTypeHeaders()
    {
        const string text = """
            class Zed : Actor
            {
            }

            struct Alpha
            {
            }

            enum Mode
            {
            }

            extend class IgnoredByUdbNavigator
            {
            }
            """;

        var items = ScriptNavigator.GetItems(ScriptType.ZScript, text);

        Assert.Equal(new[] { "class IgnoredByUdbNavigator", "class Zed : Actor", "enum Mode", "struct Alpha" }, items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public void UnknownScriptTypeHasNoNavigatorItems()
    {
        Assert.Empty(ScriptNavigator.GetItems(ScriptType.Unknown, "actor Example { }"));
    }
}
