// ABOUTME: Tests UDB things filter evaluation against parsed filter metadata and map things.
// ABOUTME: Covers scalar matching, category lookup, fields, inversion, and display mode visibility.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ThingsFilterEvaluatorTests
{
    [Fact]
    public void QualifiesThingByScalarPropertiesAndCategory()
    {
        var config = GameConfiguration.FromText("""
            thingtypes
            {
                keys
                {
                    13 { title = "Red keycard"; }
                }
            }

            thingsfilters
            {
                filter0
                {
                    name = "Red key action";
                    category = "keys";
                    type = 13;
                    angle = 90;
                    zheight = 32;
                    action = 80;
                    arg0 = 1;
                    arg3 = 4;
                    tag = 7;
                }
            }
            """);
        var map = new MapSet();
        var match = map.AddThing(new Vector2D(0, 0), 13);
        match.Angle = 90;
        match.Height = 32.75;
        match.Action = 80;
        match.Args[0] = 1;
        match.Args[3] = 4;
        match.Tag = 7;
        var wrongArg = map.AddThing(new Vector2D(64, 0), 13);
        wrongArg.Angle = 90;
        wrongArg.Height = 32;
        wrongArg.Action = 80;
        wrongArg.Args[0] = 2;
        wrongArg.Args[3] = 4;
        wrongArg.Tag = 7;

        var result = ThingsFilterEvaluator.Evaluate(map, config, config.ThingsFilters[0]);

        Assert.Equal(new[] { match }, result.VisibleThings);
        Assert.Equal(new[] { wrongArg }, result.HiddenThings);
        Assert.True(result.VisualVisibility[match]);
        Assert.False(result.VisualVisibility[wrongArg]);
    }

    [Fact]
    public void QualifiesThingByRequiredForbiddenAndCustomFields()
    {
        var config = GameConfiguration.FromText("""
            thingflagstranslation
            {
                1 = "skill1";
                8 = "dm";
            }

            thingsfilters
            {
                filter0
                {
                    name = "Imp skill filter";

                    fields
                    {
                        skill1 = true;
                        dm = false;
                    }

                    customfieldvalues
                    {
                        species = "DoomImp";
                        count = 3;
                    }

                    customfieldtypes
                    {
                        species = 2;
                        count = 0;
                    }
                }
            }
            """);
        var map = new MapSet();
        var match = map.AddThing(new Vector2D(0, 0), 3001);
        match.UdmfFlags.Add("skill1");
        match.Fields["species"] = "DoomImp";
        match.Fields["count"] = 3;
        var forbidden = map.AddThing(new Vector2D(64, 0), 3001);
        forbidden.UdmfFlags.Add("skill1");
        forbidden.UdmfFlags.Add("dm");
        forbidden.Fields["species"] = "DoomImp";
        forbidden.Fields["count"] = 3;
        var wrongCustom = map.AddThing(new Vector2D(128, 0), 3001);
        wrongCustom.UdmfFlags.Add("skill1");
        wrongCustom.Fields["species"] = "DoomImp";
        wrongCustom.Fields["count"] = 3.0;

        var result = ThingsFilterEvaluator.Evaluate(map, config, config.ThingsFilters[0]);

        Assert.Equal(new[] { match }, result.VisibleThings);
        Assert.Equal(new[] { forbidden, wrongCustom }, result.HiddenThings);
    }

    [Fact]
    public void IgnoresThingFlagFieldsUnknownToCurrentConfiguration()
    {
        var config = GameConfiguration.FromText("""
            thingflagstranslation
            {
                1 = "skill1";
            }

            thingsfilters
            {
                filter0
                {
                    name = "Cross-format flags";

                    fields
                    {
                        skill1 = true;
                        hexenclass1 = true;
                        unknownforbidden = false;
                    }
                }
            }
            """);
        var map = new MapSet();
        var match = map.AddThing(new Vector2D(0, 0), 3001);
        match.UdmfFlags.Add("skill1");

        var result = ThingsFilterEvaluator.Evaluate(map, config, config.ThingsFilters[0]);

        Assert.Equal(new[] { match }, result.VisibleThings);
        Assert.Empty(result.HiddenThings);
    }

    [Fact]
    public void IgnoresCriteriaUnsupportedByDoomFormatInterface()
    {
        var config = GameConfiguration.FromText("""
            formatinterface = "DoomMapSetIO";

            thingsfilters
            {
                filter0
                {
                    name = "Doom format";
                    type = 3001;
                    zheight = 128;
                    action = 80;
                    arg0 = 3;
                    tag = 7;

                    customfieldvalues
                    {
                        species = "DoomImp";
                    }

                    customfieldtypes
                    {
                        species = 2;
                    }
                }
            }
            """);
        var map = new MapSet();
        var match = map.AddThing(new Vector2D(0, 0), 3001);

        var result = ThingsFilterEvaluator.Evaluate(map, config, config.ThingsFilters[0]);

        Assert.Equal(new[] { match }, result.VisibleThings);
        Assert.Empty(result.HiddenThings);
    }

    [Fact]
    public void AppliesCriteriaSupportedByHexenFormatInterface()
    {
        var config = GameConfiguration.FromText("""
            formatinterface = "HexenMapSetIO";

            thingsfilters
            {
                filter0
                {
                    name = "Hexen format";
                    type = 3001;
                    zheight = 64;
                    action = 80;
                    arg0 = 3;
                    tag = 7;

                    customfieldvalues
                    {
                        species = "DoomImp";
                    }

                    customfieldtypes
                    {
                        species = 2;
                    }
                }
            }
            """);
        var map = new MapSet();
        var match = map.AddThing(new Vector2D(0, 0), 3001);
        match.Height = 64;
        match.Action = 80;
        match.Args[0] = 3;
        match.Tag = 7;
        var wrongArg = map.AddThing(new Vector2D(64, 0), 3001);
        wrongArg.Height = 64;
        wrongArg.Action = 80;
        wrongArg.Args[0] = 1;
        wrongArg.Tag = 7;

        var result = ThingsFilterEvaluator.Evaluate(map, config, config.ThingsFilters[0]);

        Assert.Equal(new[] { match }, result.VisibleThings);
        Assert.Equal(new[] { wrongArg }, result.HiddenThings);
    }

    [Fact]
    public void AppliesInvertAndDisplayModes()
    {
        var config = GameConfiguration.FromText("""
            thingsfilters
            {
                visual
                {
                    name = "Visual only";
                    type = 3001;
                    invert = true;
                    displaymode = 2;
                }
                classic
                {
                    name = "Classic only";
                    type = 3001;
                    displaymode = 1;
                }
            }
            """);
        var map = new MapSet();
        var imp = map.AddThing(new Vector2D(0, 0), 3001);
        var player = map.AddThing(new Vector2D(64, 0), 1);

        var visualOnly = ThingsFilterEvaluator.Evaluate(map, config, config.ThingsFilters[0]);
        var classicOnly = ThingsFilterEvaluator.Evaluate(map, config, config.ThingsFilters[1]);

        Assert.Equal(new[] { imp, player }, visualOnly.VisibleThings);
        Assert.False(visualOnly.VisualVisibility[imp]);
        Assert.True(visualOnly.VisualVisibility[player]);
        Assert.Equal(new[] { imp }, classicOnly.VisibleThings);
        Assert.Equal(new[] { player }, classicOnly.HiddenThings);
        Assert.True(classicOnly.VisualVisibility[player]);
    }
}
