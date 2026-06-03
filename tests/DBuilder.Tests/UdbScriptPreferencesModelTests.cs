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
}
