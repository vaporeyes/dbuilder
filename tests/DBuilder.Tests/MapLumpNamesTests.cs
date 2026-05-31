// ABOUTME: Tests config-driven maplumpnames parsing and the IsMapLump convenience used to drive save-back.
// ABOUTME: Mirrors the maplumpnames block structure from UDB's Doom_misc.cfg (per-lump required/nodebuild/script flags).

using System.Linq;
using DBuilder.IO;

namespace DBuilder.Tests;

public class MapLumpNamesTests
{
    private const string Cfg = @"
maplumpnames
{
    ~MAP
    {
        required = true;
        blindcopy = true;
        nodebuild = false;
    }
    THINGS
    {
        required = true;
        nodebuild = true;
        allowempty = true;
    }
    NODES
    {
        required = false;
        nodebuild = true;
    }
    BEHAVIOR
    {
        forbidden = true;
    }
    SCRIPTS
    {
        required = false;
        script = ""Hexen_ACS.cfg"";
    }
    ACS
    {
        required = false;
        script = ""Hexen_ACS.cfg"";
        scriptbuild = true;
    }
}";

    [Fact]
    public void ParsesLumpProperties()
    {
        var gc = GameConfiguration.FromText(Cfg);
        Assert.Equal(6, gc.MapLumpNames.Count);
        var things = gc.MapLumpNames["THINGS"];
        Assert.True(things.Required);
        Assert.True(things.NodeBuild);
        Assert.True(things.AllowEmpty);
        Assert.False(things.BlindCopy);
    }

    [Fact]
    public void MarkerAndScriptAndForbiddenFlags()
    {
        var gc = GameConfiguration.FromText(Cfg);
        Assert.True(gc.MapLumpNames["~MAP"].IsMarker);
        Assert.True(gc.MapLumpNames["~MAP"].BlindCopy);
        Assert.True(gc.MapLumpNames["BEHAVIOR"].Forbidden);
        Assert.Equal("Hexen_ACS.cfg", gc.MapLumpNames["SCRIPTS"].Script);
    }

    [Fact]
    public void ScriptBuildSuppressesStaticScriptConfig()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var lump = gc.MapLumpNames["ACS"];
        Assert.True(lump.ScriptBuild);
        Assert.Null(lump.Script);
        Assert.Null(lump.ScriptConfiguration);
    }

    [Fact]
    public void ResolvesConfiguredScriptConfigurationByLowerFileName()
    {
        var scripts = new ScriptConfigurationCatalog();
        scripts.Add("hexen_acs.cfg", ScriptConfigurationInfo.FromText("""
            description = "Hexen ACS";
            scripttype = "ACS";
            """));

        var gc = GameConfiguration.FromText(Cfg, scripts);

        Assert.Equal("Hexen ACS", gc.MapLumpNames["SCRIPTS"].ScriptConfiguration?.Description);
    }

    [Fact]
    public void UnknownMapLumpScriptConfigurationFallsBackToPlainText()
    {
        var gc = GameConfiguration.FromText(Cfg, new ScriptConfigurationCatalog());

        Assert.Equal(ScriptConfigurationInfo.PlainText, gc.MapLumpNames["SCRIPTS"].ScriptConfiguration);
    }

    [Fact]
    public void IsMapLumpExcludesMarkerAndUnknowns()
    {
        var gc = GameConfiguration.FromText(Cfg);
        Assert.True(gc.IsMapLump("THINGS"));
        Assert.True(gc.IsMapLump("nodes")); // case-insensitive
        Assert.False(gc.IsMapLump("~MAP")); // marker is not a sub-lump
        Assert.False(gc.IsMapLump("GL_VERT")); // not configured
    }

    [Fact]
    public void HasScriptLumpsDetectsStaticAndBuildScriptLumps()
    {
        var gc = GameConfiguration.FromText(Cfg);

        Assert.True(gc.HasScriptLumps());
    }

    [Fact]
    public void HasScriptLumpsIsFalseWithoutScriptMapLumps()
    {
        var gc = GameConfiguration.FromText("""
            maplumpnames
            {
                THINGS
                {
                    required = true;
                }
            }
            """);

        Assert.False(gc.HasScriptLumps());
    }

    [Fact]
    public void PreservesConfiguredLumpOrder()
    {
        var gc = GameConfiguration.FromText(Cfg);

        Assert.Equal(new[] { "~MAP", "THINGS", "NODES", "BEHAVIOR", "SCRIPTS", "ACS" }, gc.MapLumpNames.Keys.ToArray());
    }

    [Fact]
    public void EmptyWhenNotConfigured()
    {
        var gc = GameConfiguration.FromText("");
        Assert.Empty(gc.MapLumpNames);
        Assert.False(gc.IsMapLump("THINGS"));
    }
}
