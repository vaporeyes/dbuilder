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
}
