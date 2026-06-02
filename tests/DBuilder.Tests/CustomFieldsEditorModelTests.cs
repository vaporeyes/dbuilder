// ABOUTME: Verifies the custom-fields editor splits configured universal fields from raw fields.
// ABOUTME: Keeps standalone custom-fields dialog parity testable without opening Avalonia windows.

using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class CustomFieldsEditorModelTests
{
    [Fact]
    public void BuildSplitsConfiguredFieldsFromRawFields()
    {
        const string cfg = """
            universalfields
            {
                sidedef
                {
                    lightabsolute
                    {
                        type = 3;
                        default = false;
                    }
                    offsetscale
                    {
                        type = 1;
                        default = 1.0;
                    }
                }
            }
            """;
        var config = GameConfiguration.FromText(cfg);
        var fields = new Dictionary<string, object>
        {
            ["lightabsolute"] = true,
            ["comment"] = "keep",
        };

        CustomFieldsEditorModel model = CustomFieldsEditorModelBuilder.Build(config, "sidedef", fields);

        Assert.Equal(new[] { "lightabsolute", "offsetscale" }, model.ConfiguredFields.Select(field => field.Field.Name));
        Assert.Equal(true, model.ConfiguredFields[0].Value);
        Assert.Equal(1.0, model.ConfiguredFields[1].Value);
        Assert.DoesNotContain("lightabsolute", model.RawFields.Keys);
        Assert.Equal("keep", model.RawFields["comment"]);
    }

    [Fact]
    public void BuildSplitsThingUniversalFieldsFromRawFields()
    {
        const string cfg = """
            universalfields
            {
                thing
                {
                    user_score
                    {
                        type = 0;
                        default = 0;
                    }
                    user_speed
                    {
                        type = 1;
                        default = 1.0;
                    }
                }
            }
            """;
        var config = GameConfiguration.FromText(cfg);
        var fields = new Dictionary<string, object>
        {
            ["user_score"] = 25,
            ["note"] = "raw",
        };

        CustomFieldsEditorModel model = CustomFieldsEditorModelBuilder.Build(
            config,
            "thing",
            fields,
            ["user_score"]);

        Assert.Equal(new[] { "user_score", "user_speed" }, model.ConfiguredFields.Select(field => field.Field.Name));
        Assert.Equal(25, model.ConfiguredFields[0].Value);
        Assert.DoesNotContain("user_score", model.RawFields.Keys);
        Assert.Equal("raw", model.RawFields["note"]);
    }

    [Fact]
    public void BuildKeepsAllFieldsRawWithoutConfiguredElementType()
    {
        var fields = new Dictionary<string, object>
        {
            ["alpha"] = 1,
            ["beta"] = "two",
        };

        CustomFieldsEditorModel model = CustomFieldsEditorModelBuilder.Build(null, null, fields);

        Assert.Empty(model.ConfiguredFields);
        Assert.Equal(fields, model.RawFields);
    }

    [Theory]
    [InlineData(true, (int)UniversalType.Boolean)]
    [InlineData(1, (int)UniversalType.Integer)]
    [InlineData(1.5, (int)UniversalType.Float)]
    [InlineData("text", (int)UniversalType.String)]
    public void InferCustomFieldTypeMapsPrimitiveValuesToUdbTypeIds(object value, int expectedType)
        => Assert.Equal(expectedType, CustomFieldsEditorModelBuilder.InferCustomFieldType(value));

    [Fact]
    public void StoreRawFieldTypesWritesDynamicFieldTypes()
    {
        var options = new MapOptions();
        var fields = new Dictionary<string, object>
        {
            ["enabled"] = true,
            ["count"] = 3,
            ["ratio"] = 1.25,
            ["label"] = "alpha",
        };

        CustomFieldsEditorModelBuilder.StoreRawFieldTypes(options, config: null, "thing", fields);

        Assert.Equal((int)UniversalType.Boolean, options.GetUniversalFieldType("thing", "enabled", -1));
        Assert.Equal((int)UniversalType.Integer, options.GetUniversalFieldType("thing", "count", -1));
        Assert.Equal((int)UniversalType.Float, options.GetUniversalFieldType("thing", "ratio", -1));
        Assert.Equal((int)UniversalType.String, options.GetUniversalFieldType("thing", "label", -1));
    }

    [Fact]
    public void StoreRawFieldTypesSkipsConfiguredUniversalFields()
    {
        const string cfg = """
            universalfields
            {
                thing
                {
                    health
                    {
                        type = 0;
                        default = 100;
                    }
                }
            }
            """;
        var config = GameConfiguration.FromText(cfg);
        var options = new MapOptions();
        var fields = new Dictionary<string, object>
        {
            ["health"] = "wrong type",
            ["comment"] = "raw",
        };

        CustomFieldsEditorModelBuilder.StoreRawFieldTypes(options, config, "thing", fields);

        Assert.Equal(-1, options.MapConfiguration.ReadSetting("fieldtypes.thing.health", -1));
        Assert.Equal((int)UniversalType.String, options.GetUniversalFieldType("thing", "comment", -1));
    }
}
