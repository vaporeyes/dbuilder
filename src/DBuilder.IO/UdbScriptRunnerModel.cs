// ABOUTME: Models UDBScript runner lifecycle text, version gating, and exception outcomes.
// ABOUTME: Keeps runner UI and future execution plumbing aligned with upstream UDBScript behavior.

namespace DBuilder.IO;

public enum UdbScriptRunnerExceptionKind
{
    UserAbort,
    ParserError,
    JavaScriptError,
    Exit,
    Die,
    ExecutionCanceled,
    Unknown,
}

public enum UdbScriptRunnerStatusKind
{
    None,
    Ready,
    Warning,
}

public sealed record UdbScriptRunnerExceptionOutcome(
    UdbScriptRunnerExceptionKind Kind,
    bool WithdrawUndo,
    UdbScriptRunnerStatusKind StatusKind,
    string StatusText);

public sealed record UdbScriptVersionGate(
    bool RequiresPrompt,
    string Title,
    string Message);

public sealed record UdbScriptLegacyBinding(string Name, string Target);

public static class UdbScriptRunnerModel
{
    public const uint CurrentFeatureVersion = 5;
    public const string FeatureVersionPromptTitle = "UDBScript feature version too low";
    public const string UserAbortStatusText = "Script aborted";
    public const string ScriptFinishedTitle = "Script finished";
    public const string CloseButtonText = "Close";

    public static IReadOnlyList<UdbScriptLegacyBinding> LegacyBindings { get; } =
    [
        new("showMessage", "ShowMessage"),
        new("showMessageYesNo", "ShowMessageYesNo"),
        new("exit", "ExitScript"),
        new("die", "DieScript"),
        new("QueryOptions", "QueryOptions"),
        new("ScriptOptions", "ScriptOptions"),
        new("Map", "MapWrapper"),
        new("GameConfiguration", "GameConfigurationWrapper"),
        new("Angle2D", "Angle2DWrapper"),
        new("Vector3D", "Vector3DWrapper"),
        new("Vector2D", "Vector2DWrapper"),
        new("Line2D", "Line2DWrapper"),
        new("UniValue", "UniValue"),
        new("Data", "DataWrapper"),
        new("Linedef", "LinedefWrapper"),
        new("Sector", "SectorWrapper"),
        new("Sidedef", "SidedefWrapper"),
        new("Thing", "ThingWrapper"),
        new("Vertex", "VertexWrapper"),
    ];

    public static bool UsesLegacyGlobals(uint scriptVersion)
        => scriptVersion < 4;

    public static string UndoDescription(string scriptName)
        => "Run script " + scriptName;

    public static string FinishedStatus(TimeSpan runtime)
        => "Script finished. Runtime: " + FormatRuntime(runtime);

    public static string FormatRuntime(TimeSpan runtime)
        => string.Format(
            "{0:D2}:{1:D2}:{2:D2}.{3:D}",
            runtime.Hours,
            runtime.Minutes,
            runtime.Seconds,
            runtime.Milliseconds);

    public static UdbScriptVersionGate VersionGate(uint scriptVersion, bool ignoreVersion)
    {
        if (scriptVersion <= CurrentFeatureVersion || ignoreVersion)
            return new UdbScriptVersionGate(false, "", "");

        return new UdbScriptVersionGate(
            true,
            FeatureVersionPromptTitle,
            "The script requires a higher version of the feature set than this version of UDBScript supports. Executing this script might fail\n\n" +
            "Required feature version: " + scriptVersion + "\n" +
            "UDBScript feature version: " + CurrentFeatureVersion + "\n\n" +
            "Execute anyway?");
    }

    public static UdbScriptRunnerExceptionOutcome ClassifyException(
        UdbScriptRunnerExceptionKind kind,
        string message = "",
        bool javascriptThrowIsString = false)
    {
        return kind switch
        {
            UdbScriptRunnerExceptionKind.UserAbort => new(kind, true, UdbScriptRunnerStatusKind.Warning, UserAbortStatusText),
            UdbScriptRunnerExceptionKind.ParserError => new(kind, true, UdbScriptRunnerStatusKind.None, ""),
            UdbScriptRunnerExceptionKind.JavaScriptError when javascriptThrowIsString => new(kind, true, UdbScriptRunnerStatusKind.Warning, message),
            UdbScriptRunnerExceptionKind.JavaScriptError => new(kind, true, UdbScriptRunnerStatusKind.None, ""),
            UdbScriptRunnerExceptionKind.Exit => new(kind, false, string.IsNullOrEmpty(message) ? UdbScriptRunnerStatusKind.None : UdbScriptRunnerStatusKind.Ready, message),
            UdbScriptRunnerExceptionKind.Die => new(kind, true, string.IsNullOrEmpty(message) ? UdbScriptRunnerStatusKind.None : UdbScriptRunnerStatusKind.Warning, message),
            UdbScriptRunnerExceptionKind.ExecutionCanceled => new(kind, true, UdbScriptRunnerStatusKind.None, ""),
            _ => new(UdbScriptRunnerExceptionKind.Unknown, true, UdbScriptRunnerStatusKind.None, ""),
        };
    }
}
