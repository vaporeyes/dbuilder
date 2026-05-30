// ABOUTME: Tests for CvarInfoParser over CVARINFO variable declarations.
// ABOUTME: Covers scopes, archive flags, types, defaults, and semicolon-delimited rows.

using DBuilder.IO;

namespace DBuilder.Tests;

public class CvarInfoParserTests
{
    [Fact]
    public void ParsesVariableDeclarations()
    {
        const string text = @"
server int sv_monsters = 1;
user archive bool cl_showkeys = true;
nosave string session_name = ""map test"";
ignored int nope = 0;";

        var info = CvarInfoParser.Parse(text);

        Assert.Equal(3, info.Variables.Count);
        Assert.Contains(info.Variables, c => c.Scope == "server" && c.Type == "int" && c.Name == "sv_monsters" && c.DefaultValue == "1");
        var archived = Assert.Single(info.Variables, c => c.Name == "cl_showkeys");
        Assert.True(archived.Archive);
        Assert.Equal("true", archived.DefaultValue);
        Assert.Contains(info.Variables, c => c.Scope == "nosave" && c.DefaultValue == "map test");
    }

    [Fact]
    public void AllowsDeclarationsWithoutDefaults()
    {
        const string text = @"user float cl_alpha;";

        var variable = Assert.Single(CvarInfoParser.Parse(text).Variables);

        Assert.Equal("cl_alpha", variable.Name);
        Assert.Null(variable.DefaultValue);
    }

    [Fact]
    public void ParsesAdditionalFlagsAndHandlerClass()
    {
        const string text = @"
server cheat latch int sv_secret = 3;
local noarchive handlerclass(""UiHandler"") string ui_mode = ""compact"";";

        var info = CvarInfoParser.Parse(text);

        var server = Assert.Single(info.Variables, c => c.Name == "sv_secret");
        Assert.Equal("server", server.Scope);
        Assert.Contains("cheat", server.Flags);
        Assert.Contains("latch", server.Flags);
        Assert.False(server.Archive);

        var local = Assert.Single(info.Variables, c => c.Name == "ui_mode");
        Assert.Equal("local", local.Scope);
        Assert.Contains("noarchive", local.Flags);
        Assert.Contains("handlerclass", local.Flags);
        Assert.Equal("UiHandler", local.HandlerClass);
        Assert.Equal("compact", local.DefaultValue);
    }

    [Fact]
    public void AllowsRecognizedFlagPrefixesWithoutScope()
    {
        const string text = @"noarchive int loose_cvar = 1;";

        var variable = Assert.Single(CvarInfoParser.Parse(text).Variables);

        Assert.Equal("", variable.Scope);
        Assert.Equal("loose_cvar", variable.Name);
        Assert.Contains("noarchive", variable.Flags);
        Assert.Equal("1", variable.DefaultValue);
    }

    [Fact]
    public void SkipsDeclarationsWithInvalidTypedDefaults()
    {
        const string text = @"
user int bad_int = nope;
user float bad_float = nope;
user bool bad_bool = maybe;
user color bad_color = notacolor;
user int good_int = -3;
user float good_float = 1.5;
user bool good_bool = false;
user color good_color = ""#2040ff"";";

        var info = CvarInfoParser.Parse(text);

        Assert.DoesNotContain(info.Variables, c => c.Name == "bad_int");
        Assert.DoesNotContain(info.Variables, c => c.Name == "bad_float");
        Assert.DoesNotContain(info.Variables, c => c.Name == "bad_bool");
        Assert.DoesNotContain(info.Variables, c => c.Name == "bad_color");
        Assert.Contains(info.Variables, c => c.Name == "good_int");
        Assert.Contains(info.Variables, c => c.Name == "good_float");
        Assert.Contains(info.Variables, c => c.Name == "good_bool");
        Assert.Contains(info.Variables, c => c.Name == "good_color");
    }

    [Fact]
    public void KeepsFirstCvarDefinitionWhenDuplicated()
    {
        const string text = @"
user int duplicate_cvar = 1;
user int duplicate_cvar = 2;";

        var variable = Assert.Single(CvarInfoParser.Parse(text).Variables);

        Assert.Equal("duplicate_cvar", variable.Name);
        Assert.Equal("1", variable.DefaultValue);
    }
}
