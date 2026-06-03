// ABOUTME: Models UDBScript preferences metadata and external editor persistence behavior.
// ABOUTME: Keeps the preferences tab setting key, labels, and accept rules aligned with upstream UDBScript.

namespace DBuilder.IO;

public sealed record UdbScriptPreferencesMetadata(
    string TabText,
    string ExternalEditorLabel,
    string ExternalEditorSettingKey,
    string ExecutableFileFilter,
    string MissingEditorMessage);

public static class UdbScriptPreferencesModel
{
    public const string TabText = "UDBScript";
    public const string ExternalEditorLabel = "External script editor:";
    public const string ExternalEditorSettingKey = "externaleditor";
    public const string ExecutableFileFilter = "Executables (*.exe, *.cmd, *.bat)|*.exe;*.cmd;*.bat|All files (*.*)|*.*";
    public const string MissingEditorMessage = "No external editor set. Please set the external editor in the UDBScript tab in the preferences.";

    public static UdbScriptPreferencesMetadata Metadata()
        => new(TabText, ExternalEditorLabel, ExternalEditorSettingKey, ExecutableFileFilter, MissingEditorMessage);

    public static UdbScriptSettingOperation? AcceptExternalEditorPath(string path)
        => string.IsNullOrWhiteSpace(path)
            ? null
            : new UdbScriptSettingOperation(UdbScriptSettingOperationKind.Write, ExternalEditorSettingKey, path);
}
