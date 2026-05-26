// ABOUTME: Tests minimal MapOptions persistence backed by Configuration.
// ABOUTME: Covers UDB-compatible identity, selection group, tag-label and drawing-option write/read shape.

using System.Collections;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class MapOptionsTests
{
    [Fact]
    public void CurrentNameTracksPreviousNameOnFirstRename()
    {
        var options = new MapOptions();

        options.CurrentName = "MAP01";
        options.CurrentName = "MAP02";
        options.CurrentName = "MAP03";

        Assert.Equal("MAP01", options.PreviousName);
        Assert.Equal("MAP03", options.CurrentName);
        Assert.Equal("MAP03", options.LevelName);
        Assert.True(options.LevelNameChanged);
    }

    [Fact]
    public void CurrentNameDoesNotMarkChangedWhenNameStaysSame()
    {
        var options = new MapOptions();

        options.CurrentName = "";

        Assert.Equal("", options.PreviousName);
        Assert.Equal("", options.CurrentName);
        Assert.False(options.LevelNameChanged);
    }

    [Fact]
    public void ReadRootOptionsLoadsRootSettingsAndMapConfiguration()
    {
        var wadConfiguration = new Configuration(sorted: true);
        wadConfiguration.InputConfiguration("""
            gameconfig = "Doom_Doom.cfg";
            strictpatches = 1;
            maps
            {
                MAP01
                {
                    defaultfloortexture = "FLOOR1";
                }
            }
            """);
        var options = new MapOptions();

        options.ReadRootOptions(wadConfiguration, "MAP01");

        Assert.Equal("Doom_Doom.cfg", options.ConfigFile);
        Assert.True(options.StrictPatches);
        Assert.Equal("MAP01", options.CurrentName);
        Assert.Equal("", options.PreviousName);
        Assert.False(options.LevelNameChanged);
        Assert.Equal("FLOOR1", options.MapConfiguration.ReadSetting("defaultfloortexture", ""));
    }

    [Fact]
    public void WriteRootOptionsStoresUdbCompatibleRootSettings()
    {
        var options = new MapOptions
        {
            ConfigFile = "Doom_Doom.cfg",
            StrictPatches = true,
            CurrentName = "MAP01",
            DefaultFloorTexture = "FLOOR1",
        };
        options.WriteDrawingOptions();
        var wadConfiguration = new Configuration(sorted: true);

        options.WriteRootOptions(wadConfiguration);

        Assert.Equal("Doom Builder Map Settings Configuration", wadConfiguration.ReadSetting("type", ""));
        Assert.Equal("Doom_Doom.cfg", wadConfiguration.ReadSetting("gameconfig", ""));
        Assert.Equal(1, wadConfiguration.ReadSetting("strictpatches", 0));
        var mapRoot = Assert.IsAssignableFrom<IDictionary>(wadConfiguration.ReadSetting("maps.MAP01", (IDictionary?)null));
        Assert.Equal("FLOOR1", mapRoot["defaultfloortexture"]);
    }

    [Fact]
    public void WriteSelectionGroupsStoresUdbCompatibleIndexLists()
    {
        var map = BuildMap();
        map.Vertices[0].Groups = MapSet.GroupMask(0);
        map.Vertices[2].Groups = MapSet.GroupMask(0);
        map.Linedefs[0].Groups = MapSet.GroupMask(1);
        map.Sectors[0].Groups = MapSet.GroupMask(1);
        map.Things[0].Groups = MapSet.GroupMask(2);
        var options = new MapOptions();

        options.WriteSelectionGroups(map);

        var groups = options.MapConfiguration.ReadSetting(MapOptions.SelectionGroupsPath, (IDictionary?)null);
        Assert.NotNull(groups);
        var group0 = Assert.IsAssignableFrom<IDictionary>(groups![0]);
        var group1 = Assert.IsAssignableFrom<IDictionary>(groups[1]);
        var group2 = Assert.IsAssignableFrom<IDictionary>(groups[2]);
        Assert.Equal("0 2", group0["vertices"]);
        Assert.Equal("0", group1["linedefs"]);
        Assert.Equal("0", group1["sectors"]);
        Assert.Equal("0", group2["things"]);
    }

    [Fact]
    public void ReadSelectionGroupsRestoresMembershipByIndex()
    {
        var src = BuildMap();
        src.Vertices[1].Groups = MapSet.GroupMask(0);
        src.Linedefs[0].Groups = MapSet.GroupMask(3);
        src.Sectors[0].Groups = MapSet.GroupMask(3);
        src.Things[0].Groups = MapSet.GroupMask(4);
        var options = new MapOptions();
        options.WriteSelectionGroups(src);

        var dst = BuildMap();
        options.ReadSelectionGroups(dst);

        Assert.Equal(0, dst.Vertices[0].Groups);
        Assert.Equal(MapSet.GroupMask(0), dst.Vertices[1].Groups);
        Assert.Equal(MapSet.GroupMask(3), dst.Linedefs[0].Groups);
        Assert.Equal(MapSet.GroupMask(3), dst.Sectors[0].Groups);
        Assert.Equal(MapSet.GroupMask(4), dst.Things[0].Groups);
    }

    [Fact]
    public void ReadSelectionGroupsReplacesPersistedGroupBits()
    {
        var options = new MapOptions();
        options.MapConfiguration.InputConfiguration("""
            selectiongroups
            {
                0
                {
                    vertices = "1";
                }
            }
            """);
        var map = BuildMap();
        map.Vertices[0].Groups = MapSet.GroupMask(0);
        map.Vertices[1].Groups = MapSet.GroupMask(2);

        options.ReadSelectionGroups(map);

        Assert.Equal(0, map.Vertices[0].Groups);
        Assert.Equal(MapSet.GroupMask(0), map.Vertices[1].Groups);
    }

    [Fact]
    public void ReadSelectionGroupsIgnoresInvalidIndices()
    {
        var options = new MapOptions();
        options.MapConfiguration.InputConfiguration("""
            selectiongroups
            {
                0
                {
                    vertices = "-1 0 99 bad";
                    things = "5";
                }
            }
            """);
        var map = BuildMap();

        options.ReadSelectionGroups(map);

        Assert.Equal(MapSet.GroupMask(0), map.Vertices[0].Groups);
        Assert.Equal(0, map.Things[0].Groups);
    }

    [Fact]
    public void WriteTagLabelsStoresUdbCompatibleEntries()
    {
        var options = new MapOptions();
        options.TagLabels[12] = "Door controls";
        options.TagLabels[3] = "Secret lift";

        options.WriteTagLabels();

        var labels = options.MapConfiguration.ReadSetting(MapOptions.TagLabelsPath, (IDictionary?)null);
        Assert.NotNull(labels);
        var first = Assert.IsAssignableFrom<IDictionary>(labels!["taglabel1"]);
        var second = Assert.IsAssignableFrom<IDictionary>(labels["taglabel2"]);
        Assert.Equal(12, first["tag"]);
        Assert.Equal("Door controls", first["label"]);
        Assert.Equal(3, second["tag"]);
        Assert.Equal("Secret lift", second["label"]);
    }

    [Fact]
    public void ReadTagLabelsRestoresValidEntries()
    {
        var options = new MapOptions();
        options.MapConfiguration.InputConfiguration("""
            taglabels
            {
                taglabel1
                {
                    tag = 12;
                    label = "Door controls";
                }
                taglabel2
                {
                    tag = 3;
                    label = "Secret lift";
                }
            }
            """);

        options.ReadTagLabels();

        Assert.Equal("Door controls", options.TagLabels[12]);
        Assert.Equal("Secret lift", options.TagLabels[3]);
    }

    [Fact]
    public void ReadTagLabelsReplacesExistingLabelsAndIgnoresInvalidEntries()
    {
        var options = new MapOptions();
        options.TagLabels[99] = "Stale";
        options.MapConfiguration.InputConfiguration("""
            taglabels
            {
                taglabel1
                {
                    tag = 0;
                    label = "Ignored zero";
                }
                taglabel2
                {
                    tag = 8;
                    label = "";
                }
                taglabel3
                {
                    tag = 5;
                    label = "Valid";
                }
                broken = 12;
            }
            """);

        options.ReadTagLabels();

        Assert.Single(options.TagLabels);
        Assert.Equal("Valid", options.TagLabels[5]);
    }

    [Fact]
    public void WriteTagLabelsRemovesStaleConfigurationWhenEmpty()
    {
        var options = new MapOptions();
        options.TagLabels[1] = "Old";
        options.WriteTagLabels();
        options.TagLabels.Clear();

        options.WriteTagLabels();

        Assert.Null(options.MapConfiguration.ReadSetting(MapOptions.TagLabelsPath, (IDictionary?)null));
    }

    [Fact]
    public void WriteDrawingOptionsStoresUdbCompatibleScalarKeys()
    {
        var options = new MapOptions
        {
            DefaultFloorTexture = "FLOOR1",
            DefaultCeilingTexture = "CEIL1",
            DefaultTopTexture = "TOP",
            DefaultWallTexture = "MID",
            DefaultBottomTexture = "BOT",
            CustomBrightness = 144,
            CustomFloorHeight = -16,
            CustomCeilingHeight = 192,
            OverrideFloorTexture = true,
            OverrideCeilingTexture = true,
            OverrideTopTexture = true,
            OverrideMiddleTexture = true,
            OverrideBottomTexture = true,
            OverrideFloorHeight = true,
            OverrideCeilingHeight = true,
            OverrideBrightness = true,
            UseLongTextureNames = true,
            ViewPosition = new Vector2D(12.5, -4.25),
            ViewScale = 1.75,
            ScriptCompiler = "acc",
        };

        options.WriteDrawingOptions();

        Assert.Equal("FLOOR1", options.MapConfiguration.ReadSetting("defaultfloortexture", ""));
        Assert.Equal("CEIL1", options.MapConfiguration.ReadSetting("defaultceiltexture", ""));
        Assert.Equal("TOP", options.MapConfiguration.ReadSetting("defaulttoptexture", ""));
        Assert.Equal("MID", options.MapConfiguration.ReadSetting("defaultwalltexture", ""));
        Assert.Equal("BOT", options.MapConfiguration.ReadSetting("defaultbottomtexture", ""));
        Assert.Equal(144, options.MapConfiguration.ReadSetting("custombrightness", 0));
        Assert.Equal(-16, options.MapConfiguration.ReadSetting("customfloorheight", 0));
        Assert.Equal(192, options.MapConfiguration.ReadSetting("customceilheight", 0));
        Assert.True(options.MapConfiguration.ReadSetting("overridefloortexture", false));
        Assert.True(options.MapConfiguration.ReadSetting("overrideceiltexture", false));
        Assert.True(options.MapConfiguration.ReadSetting("overridetoptexture", false));
        Assert.True(options.MapConfiguration.ReadSetting("overridemiddletexture", false));
        Assert.True(options.MapConfiguration.ReadSetting("overridebottomtexture", false));
        Assert.True(options.MapConfiguration.ReadSetting("overridefloorheight", false));
        Assert.True(options.MapConfiguration.ReadSetting("overrideceilheight", false));
        Assert.True(options.MapConfiguration.ReadSetting("overridebrightness", false));
        Assert.True(options.MapConfiguration.ReadSetting("uselongtexturenames", false));
        Assert.Equal(12.5, options.MapConfiguration.ReadSetting("viewpositionx", 0.0));
        Assert.Equal(-4.25, options.MapConfiguration.ReadSetting("viewpositiony", 0.0));
        Assert.Equal(1.75, options.MapConfiguration.ReadSetting("viewscale", 0.0));
        Assert.Equal("acc", options.MapConfiguration.ReadSetting("scriptcompiler", ""));
    }

    [Fact]
    public void ReadDrawingOptionsRestoresValuesAndClampsBrightness()
    {
        var options = new MapOptions();
        options.MapConfiguration.InputConfiguration("""
            defaultfloortexture = "FLOOR1";
            defaultceiltexture = "CEIL1";
            defaulttoptexture = "TOP";
            defaultwalltexture = "MID";
            defaultbottomtexture = "BOT";
            custombrightness = 999;
            customfloorheight = -16;
            customceilheight = 192;
            overridefloortexture = true;
            overrideceiltexture = true;
            overridetoptexture = true;
            overridemiddletexture = true;
            overridebottomtexture = true;
            overridefloorheight = true;
            overrideceilheight = true;
            overridebrightness = true;
            uselongtexturenames = true;
            viewpositionx = 12.5;
            viewpositiony = -4.25;
            viewscale = 1.75;
            scriptcompiler = "acc";
            """);

        options.ReadDrawingOptions(longTextureNamesSupported: true);

        Assert.Equal("FLOOR1", options.DefaultFloorTexture);
        Assert.Equal("CEIL1", options.DefaultCeilingTexture);
        Assert.Equal("TOP", options.DefaultTopTexture);
        Assert.Equal("MID", options.DefaultWallTexture);
        Assert.Equal("BOT", options.DefaultBottomTexture);
        Assert.Equal(255, options.CustomBrightness);
        Assert.Equal(-16, options.CustomFloorHeight);
        Assert.Equal(192, options.CustomCeilingHeight);
        Assert.True(options.OverrideFloorTexture);
        Assert.True(options.OverrideCeilingTexture);
        Assert.True(options.OverrideTopTexture);
        Assert.True(options.OverrideMiddleTexture);
        Assert.True(options.OverrideBottomTexture);
        Assert.True(options.OverrideFloorHeight);
        Assert.True(options.OverrideCeilingHeight);
        Assert.True(options.OverrideBrightness);
        Assert.True(options.UseLongTextureNames);
        Assert.Equal(new Vector2D(12.5, -4.25), options.ViewPosition);
        Assert.Equal(1.75, options.ViewScale);
        Assert.Equal("acc", options.ScriptCompiler);
    }

    [Fact]
    public void ReadDrawingOptionsUsesDefaultsAndHonorsLongTextureSupport()
    {
        var options = new MapOptions();
        options.MapConfiguration.InputConfiguration("uselongtexturenames = true; custombrightness = -5;");

        options.ReadDrawingOptions(longTextureNamesSupported: false);

        Assert.Equal("", options.DefaultFloorTexture);
        Assert.Equal(0, options.CustomBrightness);
        Assert.Equal(0, options.CustomFloorHeight);
        Assert.Equal(128, options.CustomCeilingHeight);
        Assert.False(options.UseLongTextureNames);
        Assert.True(double.IsNaN(options.ViewPosition.x));
        Assert.True(double.IsNaN(options.ViewPosition.y));
        Assert.True(double.IsNaN(options.ViewScale));
    }

    [Fact]
    public void WriteDrawingOptionsRemovesStaleOptionalRendererAndScriptSettings()
    {
        var options = new MapOptions
        {
            ViewPosition = new Vector2D(1, 2),
            ViewScale = 3,
            ScriptCompiler = "acc",
        };
        options.WriteDrawingOptions();
        options.ViewPosition = new Vector2D(double.NaN, double.NaN);
        options.ViewScale = double.NaN;
        options.ScriptCompiler = "";

        options.WriteDrawingOptions();

        Assert.True(double.IsNaN(options.MapConfiguration.ReadSetting("viewpositionx", double.NaN)));
        Assert.True(double.IsNaN(options.MapConfiguration.ReadSetting("viewpositiony", double.NaN)));
        Assert.True(double.IsNaN(options.MapConfiguration.ReadSetting("viewscale", double.NaN)));
        Assert.Equal("", options.MapConfiguration.ReadSetting("scriptcompiler", ""));
    }

    private static MapSet BuildMap()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(64, 0));
        var v2 = map.AddVertex(new Vector2D(64, 64));
        var line = map.AddLinedef(v0, v1);
        map.AddLinedef(v1, v2);
        map.AddSidedef(line, true, sector);
        map.AddThing(new Vector2D(32, 16), 3001);
        map.BuildIndexes();
        return map;
    }
}
