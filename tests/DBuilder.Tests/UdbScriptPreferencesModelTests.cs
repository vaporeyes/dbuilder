// ABOUTME: Tests UDBScript preferences metadata and external editor persistence rules.
// ABOUTME: Covers tab labels, file filters, setting keys, and blank editor path handling.

using DBuilder.IO;

namespace DBuilder.Tests;

public class UdbScriptPreferencesModelTests
{
    [Fact]
    public void MetadataMatchesUdbPreferencesForm()
    {
        UdbScriptPreferencesMetadata metadata = UdbScriptPreferencesModel.Metadata();

        Assert.Equal("UDBScript", metadata.TabText);
        Assert.Equal("External script editor:", metadata.ExternalEditorLabel);
        Assert.Equal("externaleditor", metadata.ExternalEditorSettingKey);
        Assert.Equal("notepad.exe", UdbScriptPreferencesModel.DefaultExternalEditorName);
        Assert.Equal("Executables (*.exe, *.cmd, *.bat)|*.exe;*.cmd;*.bat|All files (*.*)|*.*", metadata.ExecutableFileFilter);
        Assert.Equal(
            "No external editor set. Please set the external editor in the UDBScript tab in the preferences.",
            metadata.MissingEditorMessage);
    }

    [Fact]
    public void AcceptExternalEditorPathOnlyWritesNonblankValues()
    {
        Assert.Null(UdbScriptPreferencesModel.AcceptExternalEditorPath(""));
        Assert.Null(UdbScriptPreferencesModel.AcceptExternalEditorPath("   "));

        UdbScriptSettingOperation? operation = UdbScriptPreferencesModel.AcceptExternalEditorPath("/tools/editor.exe");

        Assert.NotNull(operation);
        Assert.Equal(UdbScriptSettingOperationKind.Write, operation.Kind);
        Assert.Equal("externaleditor", operation.Key);
        Assert.Equal("/tools/editor.exe", operation.Value);
    }

    [Fact]
    public void ResolveExternalEditorPathKeepsConfiguredPathOrFindsSystemNotepad()
    {
        string configured = UdbScriptPreferencesModel.ResolveExternalEditorPath(
            "/tools/editor.exe",
            "/windows/system32",
            _ => false);
        string fallback = UdbScriptPreferencesModel.ResolveExternalEditorPath(
            "   ",
            "/windows/system32",
            path => path == Path.Combine("/windows/system32", "notepad.exe"));
        string missing = UdbScriptPreferencesModel.ResolveExternalEditorPath(
            "",
            "/windows/system32",
            _ => false);

        Assert.Equal("/tools/editor.exe", configured);
        Assert.Equal(Path.Combine("/windows/system32", "notepad.exe"), fallback);
        Assert.Equal("", missing);
    }

    [Fact]
    public void EditScriptLaunchPlanMatchesUdbExternalEditorBranches()
    {
        UdbScriptExternalEditorLaunchPlan missing = UdbScriptPreferencesModel.EditScriptLaunchPlan(
            "",
            "/scripts/my script.js");
        UdbScriptExternalEditorLaunchPlan launch = UdbScriptPreferencesModel.EditScriptLaunchPlan(
            "/tools/editor.exe",
            "/scripts/my script.js");

        Assert.False(missing.ShouldLaunch);
        Assert.Equal("", missing.FileName);
        Assert.Equal("", missing.Arguments);
        Assert.Equal(UdbScriptPreferencesModel.MissingEditorMessage, missing.Message);
        Assert.True(launch.ShouldLaunch);
        Assert.Equal("/tools/editor.exe", launch.FileName);
        Assert.Equal("\"/scripts/my script.js\"", launch.Arguments);
        Assert.Null(launch.Message);
    }
}
