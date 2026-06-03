// ABOUTME: Models UDBScript preferences metadata and external editor persistence behavior.
// ABOUTME: Keeps the preferences tab setting key, labels, and accept rules aligned with upstream UDBScript.

using System.Diagnostics;

namespace DBuilder.IO;

public sealed record UdbScriptPreferencesMetadata(
    string TabText,
    string ExternalEditorLabel,
    string ExternalEditorSettingKey,
    string ExecutableFileFilter,
    string MissingEditorMessage);

public sealed record UdbScriptExternalEditorLaunchPlan(
    bool ShouldLaunch,
    string FileName,
    string Arguments,
    string? Message);

public static class UdbScriptPreferencesModel
{
    public const string TabText = "UDBScript";
    public const string ExternalEditorLabel = "External script editor:";
    public const string ExternalEditorSettingKey = "externaleditor";
    public const string DefaultExternalEditorName = "notepad.exe";
    public const string ExecutableFileFilter = "Executables (*.exe, *.cmd, *.bat)|*.exe;*.cmd;*.bat|All files (*.*)|*.*";
    public const string MissingEditorMessage = "No external editor set. Please set the external editor in the UDBScript tab in the preferences.";

    public static UdbScriptPreferencesMetadata Metadata()
        => new(TabText, ExternalEditorLabel, ExternalEditorSettingKey, ExecutableFileFilter, MissingEditorMessage);

    public static UdbScriptSettingOperation? AcceptExternalEditorPath(string path)
        => string.IsNullOrWhiteSpace(path)
            ? null
            : new UdbScriptSettingOperation(UdbScriptSettingOperationKind.Write, ExternalEditorSettingKey, path);

    public static string ResolveExternalEditorPath(
        string configuredPath,
        string systemDirectory,
        Func<string, bool> fileExists)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return configuredPath;

        string defaultEditor = Path.Combine(systemDirectory, DefaultExternalEditorName);
        return fileExists(defaultEditor) ? defaultEditor : "";
    }

    public static UdbScriptExternalEditorLaunchPlan EditScriptLaunchPlan(
        string editorPath,
        string scriptFile)
    {
        if (string.IsNullOrWhiteSpace(editorPath))
            return new UdbScriptExternalEditorLaunchPlan(false, "", "", MissingEditorMessage);

        return new UdbScriptExternalEditorLaunchPlan(
            true,
            editorPath,
            "\"" + scriptFile + "\"",
            null);
    }

    public static ProcessStartInfo CreateExternalEditorStartInfo(UdbScriptExternalEditorLaunchPlan plan)
        => new(plan.FileName)
        {
            Arguments = plan.Arguments,
            UseShellExecute = false,
        };
}
