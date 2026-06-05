// ABOUTME: Tests UDB things filter evaluation against parsed filter metadata and map things.
// ABOUTME: Covers scalar matching, category lookup, fields, inversion, and display mode visibility.

using DBuilder.Geometry;
using DBuilder.Editor;
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

    [Fact]
    public void ConfigurationAndDraftPreserveDisplayModeLikeUdb()
    {
        var config = GameConfiguration.FromText("""
            thingsfilters
            {
                low
                {
                    name = "Low";
                    displaymode = -20;
                }
                high
                {
                    name = "High";
                    displaymode = 20;
                }
            }
            """);

        Assert.Equal(-20, config.ThingsFilters[0].DisplayMode);
        Assert.Equal(20, config.ThingsFilters[1].DisplayMode);

        var draft = new ThingsFilterDraft { DisplayMode = 20, ThingType = 3001 };
        Assert.Equal(20, draft.ToInfo("filter0").DisplayMode);
    }

    [Fact]
    public void UnsupportedDisplayModesEvaluateAsAlwaysLikeUdb()
    {
        var config = GameConfiguration.FromText("""
            thingsfilters
            {
                low
                {
                    name = "Low";
                    type = 3001;
                    displaymode = -20;
                }
                high
                {
                    name = "High";
                    type = 3001;
                    displaymode = 20;
                }
            }
            """);
        var map = new MapSet();
        var match = map.AddThing(new Vector2D(0, 0), 3001);
        var miss = map.AddThing(new Vector2D(64, 0), 1);

        var low = ThingsFilterEvaluator.Evaluate(map, config, config.ThingsFilters[0]);
        var high = ThingsFilterEvaluator.Evaluate(map, config, config.ThingsFilters[1]);

        Assert.Equal(new[] { match }, low.VisibleThings);
        Assert.Equal(new[] { miss }, low.HiddenThings);
        Assert.True(low.VisualVisibility[match]);
        Assert.False(low.VisualVisibility[miss]);
        Assert.Equal(low.VisibleThings, high.VisibleThings);
        Assert.Equal(low.HiddenThings, high.HiddenThings);
        Assert.Equal(low.VisualVisibility[match], high.VisualVisibility[match]);
        Assert.Equal(low.VisualVisibility[miss], high.VisualVisibility[miss]);
    }

    [Fact]
    public void DraftUsesUdbDefaultsAndValidationRules()
    {
        var draft = new ThingsFilterDraft();

        Assert.Equal(ThingsFilterDraft.DefaultName, draft.Name);
        Assert.Equal("", draft.Category);
        Assert.Equal(-1, draft.ThingType);
        Assert.Equal(-1, draft.ThingAngle);
        Assert.Equal(int.MinValue, draft.ThingZHeight);
        Assert.Equal(-1, draft.ThingAction);
        Assert.Equal([-1, -1, -1, -1, -1], draft.ThingArgs);
        Assert.Equal(-1, draft.ThingTag);
        Assert.False(draft.IsValid());

        draft.ThingArgs[0] = 7;
        Assert.True(draft.IsValid());

        draft.ThingArgs[0] = -1;
        draft.RequiredFields.Add("skill1");
        Assert.True(draft.IsValid());
    }

    [Fact]
    public void DraftWritesUdbThingsFilterSettings()
    {
        var draft = new ThingsFilterDraft
        {
            Name = "Custom imps",
            Category = "monsters",
            Invert = true,
            DisplayMode = 20,
            ThingType = 3001,
            ThingAngle = 90,
            ThingZHeight = 32,
            ThingAction = 80,
            ThingTag = 4,
        };
        draft.ThingArgs[0] = 7;
        draft.ThingArgs[4] = 9;
        draft.RequiredFields.Add("skill1");
        draft.ForbiddenFields.Add("ambush");
        draft.CustomFields["species"] = new ThingsFilterCustomFieldInfo("species", (int)UniversalType.String, "DoomImp");

        var configuration = new Configuration(sorted: true);
        draft.WriteSettings(configuration, "thingsfilters.custom0");

        var config = GameConfiguration.FromText(configuration.OutputConfiguration("\n"));
        var filter = Assert.Single(config.ThingsFilters);

        Assert.Equal("custom0", filter.Key);
        Assert.Equal("Custom imps", filter.Name);
        Assert.Equal("monsters", filter.Category);
        Assert.True(filter.Invert);
        Assert.Equal(20, filter.DisplayMode);
        Assert.Equal(3001, filter.ThingType);
        Assert.Equal(90, filter.ThingAngle);
        Assert.Equal(32, filter.ThingZHeight);
        Assert.Equal(80, filter.ThingAction);
        Assert.Equal([7, -1, -1, -1, 9], filter.ThingArgs);
        Assert.Equal(4, filter.ThingTag);
        Assert.Equal(["skill1"], filter.RequiredFields);
        Assert.Equal(["ambush"], filter.ForbiddenFields);
        var custom = Assert.Single(filter.CustomFields);
        Assert.Equal("species", custom.Key);
        Assert.Equal((int)UniversalType.String, custom.Value.Type);
        Assert.Equal("DoomImp", custom.Value.Value);
    }

    [Fact]
    public void CollectionDraftSortsAddsDeletesAndPreservesActiveFilterByName()
    {
        var config = GameConfiguration.FromText("""
            thingsfilters
            {
                zed
                {
                    name = "Zeds";
                    type = 9;
                }

                active
                {
                    name = "Active";
                    type = 3001;
                }
            }
            """);

        var collection = ThingsFilterCollectionDraft.FromFilters(config.ThingsFilters);

        Assert.Equal(["Active", "Zeds"], collection.Filters.Select(entry => entry.Draft.Name).ToArray());

        var added = collection.AddNew();
        Assert.Equal("filter0", added.Key);
        added.Draft.Name = "Middle";
        added.Draft.ThingType = 2001;

        Assert.False(collection.RemoveAt(-1));
        Assert.True(collection.RemoveAt(1));
        collection.SortByName();

        Assert.Equal(["Active", "Middle"], collection.Filters.Select(entry => entry.Draft.Name).ToArray());
        ThingsFilterInfo? active = collection.FindByName("Active");
        Assert.NotNull(active);
        Assert.Equal(3001, active.ThingType);
        Assert.Null(collection.FindByName("Zeds"));
    }

    [Fact]
    public void CollectionDraftFindsActiveFilterByStableKeyAfterRename()
    {
        var config = GameConfiguration.FromText("""
            thingsfilters
            {
                active
                {
                    name = "Before";
                    type = 3001;
                }
            }
            """);

        var collection = ThingsFilterCollectionDraft.FromFilters(config.ThingsFilters);
        ThingsFilterDraftEntry entry = Assert.Single(collection.Filters);
        entry.Draft.Name = "After";

        ThingsFilterInfo? active = collection.FindByKey("active");

        Assert.NotNull(active);
        Assert.Equal("After", active.Name);
        Assert.Equal(3001, active.ThingType);
        Assert.Null(collection.FindByKey("missing"));
    }

    [Fact]
    public void CollectionDraftReplacesThingsFiltersConfigurationBlock()
    {
        var configuration = new Configuration(sorted: true);
        configuration.WriteSetting("thingsfilters.stale.name", "Stale");
        configuration.WriteSetting("thingsfilters.stale.type", 1);

        var collection = new ThingsFilterCollectionDraft();
        var first = collection.AddNew();
        first.Draft.Name = "First";
        first.Draft.ThingType = 3001;
        var second = collection.AddNew();
        second.Draft.Name = "Second";
        second.Draft.ThingType = 3002;

        collection.WriteSettings(configuration);

        var config = GameConfiguration.FromText(configuration.OutputConfiguration("\n"));

        Assert.Equal(["First", "Second"], config.ThingsFilters.Select(filter => filter.Name).ToArray());
        Assert.Equal(["filter0", "filter1"], config.ThingsFilters.Select(filter => filter.Key).ToArray());
        Assert.DoesNotContain(config.ThingsFilters, filter => filter.Name == "Stale");
    }

    [Fact]
    public void CollectionDraftSkipsInvalidFiltersDuringWriteBack()
    {
        var configuration = new Configuration(sorted: true);

        var collection = new ThingsFilterCollectionDraft();
        var invalid = collection.AddNew();
        invalid.Draft.Name = "Empty";
        var valid = collection.AddNew();
        valid.Draft.Name = "Valid";
        valid.Draft.ThingType = 3001;

        collection.WriteSettings(configuration);

        var config = GameConfiguration.FromText(configuration.OutputConfiguration("\n"));

        var filter = Assert.Single(config.ThingsFilters);
        Assert.Equal("filter1", filter.Key);
        Assert.Equal("Valid", filter.Name);
        Assert.Equal(3001, filter.ThingType);
    }

    [Fact]
    public void CollectionDraftSkipsInvalidFiltersDuringActiveLookup()
    {
        var collection = new ThingsFilterCollectionDraft();
        var invalid = collection.AddNew();
        invalid.Draft.Name = "Empty";
        var valid = collection.AddNew();
        valid.Draft.Name = "Valid";
        valid.Draft.ThingType = 3001;

        Assert.Null(collection.FindByName("Empty"));
        Assert.Null(collection.FindByKey("filter0"));

        ThingsFilterInfo? byName = collection.FindByName("Valid");
        ThingsFilterInfo? byKey = collection.FindByKey("filter1");

        Assert.NotNull(byName);
        Assert.NotNull(byKey);
        Assert.Equal(3001, byName.ThingType);
        Assert.Equal(3001, byKey.ThingType);
    }

    [Fact]
    public void DraftIgnoresBlankFieldCriteriaDuringValidationAndWriteBack()
    {
        var draft = new ThingsFilterDraft();
        draft.RequiredFields.Add("");
        draft.RequiredFields.Add(" ");
        draft.ForbiddenFields.Add("");
        draft.CustomFields[""] = new ThingsFilterCustomFieldInfo("", (int)UniversalType.String, "ignored");
        draft.CustomFields[" "] = new ThingsFilterCustomFieldInfo(" ", (int)UniversalType.String, "ignored");

        Assert.False(draft.IsValid());

        draft.RequiredFields.Add("skill1");
        draft.ForbiddenFields.Add("ambush");
        draft.CustomFields["species"] = new ThingsFilterCustomFieldInfo("species", (int)UniversalType.String, "DoomImp");

        var configuration = new Configuration(sorted: true);
        draft.WriteSettings(configuration, "thingsfilters.custom0");

        var filter = Assert.Single(GameConfiguration.FromText(configuration.OutputConfiguration("\n")).ThingsFilters);

        Assert.Equal(["skill1"], filter.RequiredFields);
        Assert.Equal(["ambush"], filter.ForbiddenFields);
        var custom = Assert.Single(filter.CustomFields);
        Assert.Equal("species", custom.Key);
    }

    [Fact]
    public void ThingFilterWindowSelectsActiveFilterByStableKey()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/ThingFilterWindow.cs"));

        Assert.Contains("choices.FindIndex(c => IsActiveFilter(c.Filter, activeFilter))", body, StringComparison.Ordinal);
        Assert.Contains("string.Equals(choice.Key, active.Key, StringComparison.Ordinal)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ReferenceEquals(c.Filter, activeFilter)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ThingFilterWindowBuildsNestedCategoryLabelsLikeUdb()
    {
        const string cfg = """
            thingtypes
            {
                monsters
                {
                    title = "Monsters";

                    bosses
                    {
                        title = "Bosses";
                        3003 = "Baron of Hell";
                    }
                }
            }
            """;
        GameConfiguration config = GameConfiguration.FromText(cfg);

        IReadOnlyList<ThingFilterCategoryChoice> choices = ThingFilterWindow.CategoryChoices(config);

        ThingFilterCategoryChoice choice = Assert.Single(choices);
        Assert.Equal("monsters.bosses", choice.Key);
        Assert.Equal("Monsters / Bosses", choice.Label);
    }
}
