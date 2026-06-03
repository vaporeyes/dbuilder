// ABOUTME: Tests UDBScript QueryOptions runtime option state against upstream behavior.
// ABOUTME: Covers addOption validation, enum defaults, clear, and queried option values.

using System.Dynamic;
using DBuilder.IO;

namespace DBuilder.Tests;

public class UdbScriptQueryOptionsModelTests
{
    [Fact]
    public void PromptMetadataMatchesUdbQueryOptionsDialog()
    {
        UdbScriptQueryOptionsPromptMetadata metadata = UdbScriptQueryOptionsModel.PromptMetadata();

        Assert.Equal("Query options", metadata.Title);
        Assert.Equal("OK", metadata.OkButtonText);
        Assert.Equal("Cancel", metadata.CancelButtonText);
        Assert.True(metadata.IsFixedDialog);
        Assert.True(metadata.AcceptsOk);
        Assert.True(metadata.Cancels);
    }

    [Fact]
    public void QueryPlanMatchesUdbPausedDialogReturnContract()
    {
        UdbScriptQueryOptionsQueryPlan ok = UdbScriptQueryOptionsModel.QueryPlan(UdbScriptQueryOptionsDialogResult.Ok);
        UdbScriptQueryOptionsQueryPlan cancel = UdbScriptQueryOptionsModel.QueryPlan(UdbScriptQueryOptionsDialogResult.Cancel);

        Assert.True(ok.InvokesRunnerPaused);
        Assert.Equal("Query options", ok.Prompt.Title);
        Assert.Equal(UdbScriptQueryOptionsDialogResult.Ok, ok.DialogResult);
        Assert.True(ok.ReturnValue);

        Assert.True(cancel.InvokesRunnerPaused);
        Assert.Equal(UdbScriptQueryOptionsDialogResult.Cancel, cancel.DialogResult);
        Assert.False(cancel.ReturnValue);
    }

    [Fact]
    public void ApiMembersMatchUdbQueryOptionsSurface()
    {
        Assert.Equal(
            ["options", "addOption", "addOption", "clear", "query"],
            UdbScriptQueryOptionsModel.ApiMembers.Select(member => member.Name).ToArray());

        UdbScriptQueryOptionsApiMember options = UdbScriptQueryOptionsModel.ApiMembers[0];
        Assert.Equal(UdbScriptQueryOptionsMemberKind.Property, options.Kind);
        Assert.Equal("ExpandoObject", options.ReturnType);
        Assert.Empty(options.Parameters);

        UdbScriptQueryOptionsApiMember addWithoutEnum = UdbScriptQueryOptionsModel.ApiMembers[1];
        Assert.Equal(UdbScriptQueryOptionsMemberKind.Method, addWithoutEnum.Kind);
        Assert.Equal("void", addWithoutEnum.ReturnType);
        Assert.Equal(["string", "string", "int", "object"], addWithoutEnum.Parameters);

        UdbScriptQueryOptionsApiMember addWithEnum = UdbScriptQueryOptionsModel.ApiMembers[2];
        Assert.Equal("void", addWithEnum.ReturnType);
        Assert.Equal(["string", "string", "int", "object", "object"], addWithEnum.Parameters);

        UdbScriptQueryOptionsApiMember query = UdbScriptQueryOptionsModel.ApiMembers[4];
        Assert.Equal(UdbScriptQueryOptionsMemberKind.Method, query.Kind);
        Assert.Equal("bool", query.ReturnType);
        Assert.Empty(query.Parameters);
    }

    [Fact]
    public void AddOptionAcceptsValidTypesAndReturnsQueriedValues()
    {
        var model = new UdbScriptQueryOptionsModel();

        UdbScriptQueryOptionAddResult added = model.AddOption(
            "demo.js",
            "length",
            "Length",
            (int)UniversalType.Integer,
            128);

        Assert.True(added.Added);
        UdbScriptOption option = Assert.Single(model.Options);
        Assert.Equal("length", option.Name);
        Assert.Equal("Length", option.Description);
        Assert.Equal((int)UniversalType.Integer, option.Type);
        Assert.Equal(128, option.DefaultValue);
        Assert.Equal(128, option.Value);
        Assert.Equal("length", option.SettingKey);

        Assert.True(model.SetValue("length", 256));
        Assert.Equal(256, model.GetScriptOptions()["length"]);
    }

    [Fact]
    public void AddOptionRejectsInvalidTypesWithUdbErrorText()
    {
        var model = new UdbScriptQueryOptionsModel();

        UdbScriptQueryOptionAddResult added = model.AddOption(
            "demo.js",
            "broken",
            "Broken",
            (int)UniversalType.EnumBits,
            0);

        Assert.False(added.Added);
        Assert.Equal("Error in script demo.js: option broken has invalid type 12", added.ErrorDescription);
        Assert.Empty(model.Options);
    }

    [Fact]
    public void AddOptionMapsDictionaryEnumDefaults()
    {
        var model = new UdbScriptQueryOptionsModel();

        model.AddOption(
            "demo.js",
            "direction",
            "Direction",
            (int)UniversalType.EnumOption,
            2,
            new Dictionary<string, object?>
            {
                ["1"] = "Up",
                ["2"] = "Down",
                ["3"] = null,
            });

        UdbScriptOption option = Assert.Single(model.Options);
        Assert.Equal("Down", option.DefaultValue);
        Assert.Equal("Down", option.Value);
        Assert.Equal(new[] { "1:Up", "2:Down", "3:" }, option.EnumValues.Select(value => value.Key + ":" + value.Label).ToArray());
    }

    [Fact]
    public void AddOptionMapsExpandoEnumDefaults()
    {
        dynamic values = new ExpandoObject();
        values.One = "First";
        values.Two = "Second";
        var model = new UdbScriptQueryOptionsModel();

        model.AddOption(
            "demo.js",
            "mode",
            "Mode",
            (int)UniversalType.EnumOption,
            "Two",
            values);

        UdbScriptOption option = Assert.Single(model.Options);
        Assert.Equal("Second", option.DefaultValue);
        Assert.Equal(new[] { "One:First", "Two:Second" }, option.EnumValues.Select(value => value.Key + ":" + value.Label).ToArray());
    }

    [Fact]
    public void ClearRemovesOptionsAndDuplicateNamesReturnLastValue()
    {
        var model = new UdbScriptQueryOptionsModel();

        model.AddOption("demo.js", "size", "Size", (int)UniversalType.Integer, 64);
        model.AddOption("demo.js", "size", "Size override", (int)UniversalType.Integer, 128);

        Assert.Equal(2, model.Options.Count);
        Assert.Equal(128, model.GetScriptOptions()["size"]);

        model.Clear();

        Assert.Empty(model.Options);
        Assert.Empty(model.GetScriptOptions());
    }

    [Fact]
    public void GetScriptOptionsUsesOptionUiHandlerConversions()
    {
        var model = new UdbScriptQueryOptionsModel();
        model.AddOption(
            "demo.js",
            "direction",
            "Direction",
            (int)UniversalType.EnumOption,
            2,
            new Dictionary<string, object?>
            {
                ["1"] = "Up",
                ["2"] = "Down",
            });
        model.AddOption("demo.js", "length", "Length", (int)UniversalType.Integer, 128);

        model.SetValue("direction", "Up");
        model.SetValue("length", "256");

        IReadOnlyDictionary<string, object> values = model.GetScriptOptions();

        Assert.Equal(1, values["direction"]);
        Assert.Equal(256, values["length"]);
    }
}
