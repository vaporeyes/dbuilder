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

public enum UdbScriptMessageResult
{
    Ok,
    Yes,
    No,
    Abort,
}

public enum UdbScriptHostMemberKind
{
    Object,
    TypeReference,
    ScriptOptions,
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

public sealed record UdbScriptHostMember(
    string Name,
    UdbScriptHostMemberKind Kind,
    string Target,
    uint MinVersion = 1);

public sealed record UdbScriptRuntimeConstraintPrompt(
    bool ShouldPrompt,
    string Title,
    string Message);

public sealed record UdbScriptSourceFile(
    string Path,
    string EngineSourceName);

public sealed record UdbScriptRunSourcePlan(
    string LibrariesPath,
    IReadOnlyList<UdbScriptSourceFile> Libraries,
    UdbScriptSourceFile Script);

public sealed record UdbScriptRunnerUiState(
    string Title,
    string StatusText,
    string ActionButtonText,
    bool ActionButtonEnabled,
    bool ProgressIsMarquee,
    double Opacity,
    bool AutoClose);

public sealed class UdbScriptUserAbortException : Exception
{
    public UdbScriptUserAbortException()
        : base(UdbScriptRunnerModel.UserAbortStatusText)
    {
    }
}

public sealed class UdbScriptExitException : Exception
{
    public UdbScriptExitException(string? message = null)
        : base(message ?? "")
    {
    }
}

public sealed class UdbScriptDieException : Exception
{
    public UdbScriptDieException(string? message = null)
        : base(message ?? "")
    {
    }
}

public sealed class UdbScriptHostWrapper
{
    private readonly Action<int>? progress;
    private readonly Action<string>? logger;
    private readonly Func<string, UdbScriptMessageResult>? messageCallback;
    private readonly Func<string, UdbScriptMessageResult>? yesNoMessageCallback;

    public UdbScriptHostWrapper(
        Action<int>? progress = null,
        Action<string>? logger = null,
        Func<string, UdbScriptMessageResult>? messageCallback = null,
        Func<string, UdbScriptMessageResult>? yesNoMessageCallback = null)
    {
        this.progress = progress;
        this.logger = logger;
        this.messageCallback = messageCallback;
        this.yesNoMessageCallback = yesNoMessageCallback;
    }

    public void setProgress(int value)
        => progress?.Invoke(value);

    public void log(object? text)
    {
        if (text == null)
            return;

        logger?.Invoke(text.ToString() ?? "");
    }

    public void showMessage(object? message)
    {
        UdbScriptMessageResult result = messageCallback?.Invoke(message?.ToString() ?? "") ?? UdbScriptMessageResult.Ok;

        if (result == UdbScriptMessageResult.Abort)
            throw new UdbScriptUserAbortException();
    }

    public bool showMessageYesNo(object? message)
    {
        UdbScriptMessageResult result = yesNoMessageCallback?.Invoke(message?.ToString() ?? "") ?? UdbScriptMessageResult.No;

        if (result == UdbScriptMessageResult.Abort)
            throw new UdbScriptUserAbortException();

        return result is UdbScriptMessageResult.Ok or UdbScriptMessageResult.Yes;
    }

    public void exit(string? message = null)
        => throw new UdbScriptExitException(string.IsNullOrEmpty(message) ? null : message);

    public void die(string? message = null)
        => throw new UdbScriptDieException(string.IsNullOrEmpty(message) ? null : message);
}

public static class UdbScriptRunnerModel
{
    public const uint CurrentFeatureVersion = 5;
    public const long RunnerVisibilityThresholdMilliseconds = 1000;
    public const int RunnerTimerIntervalMilliseconds = 100;
    public const long RuntimeConstraintCheckMilliseconds = 5000;
    public const string FeatureVersionPromptTitle = "UDBScript feature version too low";
    public const string RunningScriptTitle = "Running script";
    public const string RunningScriptStatusText = "Running script...";
    public const string CancelButtonText = "Cancel";
    public const string RuntimeConstraintPromptTitle = "Script";
    public const string RuntimeConstraintPromptMessage = "The script has been running for some time, want to stop it?";
    public const string UserAbortStatusText = "Script aborted";
    public const string ScriptFinishedTitle = "Script finished";
    public const string CloseButtonText = "Close";

    public static IReadOnlyList<UdbScriptHostMember> HostMembers { get; } =
    [
        new("GameConfiguration", UdbScriptHostMemberKind.Object, "GameConfigurationWrapper"),
        new("QueryOptions", UdbScriptHostMemberKind.TypeReference, "QueryOptions"),
        new("ScriptOptions", UdbScriptHostMemberKind.ScriptOptions, "ExpandoObject"),
        new("Angle2D", UdbScriptHostMemberKind.TypeReference, "Angle2DWrapper"),
        new("Data", UdbScriptHostMemberKind.Object, "DataWrapper"),
        new("Line2D", UdbScriptHostMemberKind.TypeReference, "Line2DWrapper"),
        new("Map", UdbScriptHostMemberKind.Object, "MapWrapper"),
        new("UniValue", UdbScriptHostMemberKind.TypeReference, "UniValue"),
        new("Vector2D", UdbScriptHostMemberKind.TypeReference, "Vector2DWrapper"),
        new("Vector3D", UdbScriptHostMemberKind.TypeReference, "Vector3DWrapper"),
        new("Linedef", UdbScriptHostMemberKind.TypeReference, "LinedefWrapper"),
        new("Sector", UdbScriptHostMemberKind.TypeReference, "SectorWrapper"),
        new("Sidedef", UdbScriptHostMemberKind.TypeReference, "SidedefWrapper"),
        new("Thing", UdbScriptHostMemberKind.TypeReference, "ThingWrapper"),
        new("Vertex", UdbScriptHostMemberKind.TypeReference, "VertexWrapper"),
        new("Plane", UdbScriptHostMemberKind.TypeReference, "PlaneWrapper", MinVersion: 5),
        new("BlockMap", UdbScriptHostMemberKind.TypeReference, "BlockMapWrapper", MinVersion: 5),
    ];

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

    public static UdbScriptRunnerUiState InitialUiState()
        => new(
            RunningScriptTitle,
            RunningScriptStatusText,
            CancelButtonText,
            false,
            true,
            0.0,
            true);

    public static UdbScriptRunnerUiState FinishedUiState(TimeSpan runtime, bool autoClose)
        => new(
            ScriptFinishedTitle,
            FinishedStatus(runtime),
            CloseButtonText,
            true,
            false,
            autoClose ? 0.0 : 1.0,
            autoClose);

    public static UdbScriptRunnerUiState ProgressReportedUiState(UdbScriptRunnerUiState state)
        => state with
        {
            ActionButtonEnabled = true,
            ProgressIsMarquee = false,
            Opacity = 1.0,
        };

    public static UdbScriptRunnerUiState LogReportedUiState(UdbScriptRunnerUiState state)
        => state with
        {
            ActionButtonEnabled = true,
            Opacity = 1.0,
            AutoClose = false,
        };

    public static bool ShouldMakeRunnerVisible(TimeSpan elapsed)
        => elapsed.TotalMilliseconds > RunnerVisibilityThresholdMilliseconds;

    public static string RunningWindowTitle(TimeSpan elapsed)
        => RunningScriptTitle + " (" + string.Format("{0:D2}:{1:D2}:{2:D2}", elapsed.Hours, elapsed.Minutes, elapsed.Seconds) + ")";

    public static string AppendLog(string existingLog, string text)
        => string.IsNullOrEmpty(existingLog) ? text : existingLog + Environment.NewLine + text;

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

    public static UdbScriptRuntimeConstraintPrompt RuntimeConstraintPrompt(TimeSpan elapsed)
        => elapsed.TotalMilliseconds > RuntimeConstraintCheckMilliseconds
            ? new UdbScriptRuntimeConstraintPrompt(true, RuntimeConstraintPromptTitle, RuntimeConstraintPromptMessage)
            : new UdbScriptRuntimeConstraintPrompt(false, "", "");

    public static UdbScriptRunSourcePlan BuildSourcePlan(string appPath, string scriptFile)
    {
        string librariesPath = Path.Combine(appPath, UdbScriptDiscovery.ScriptFolder, "Libraries");
        IReadOnlyList<UdbScriptSourceFile> libraries = Directory.Exists(librariesPath)
            ? Directory.GetFiles(librariesPath, "*.js", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new UdbScriptSourceFile(path, EngineSourceName(appPath, path)))
                .ToArray()
            : Array.Empty<UdbScriptSourceFile>();

        return new UdbScriptRunSourcePlan(
            librariesPath,
            libraries,
            new UdbScriptSourceFile(scriptFile, EngineSourceName(appPath, scriptFile)));
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

    private static string EngineSourceName(string appPath, string path)
    {
        if (path.StartsWith(appPath, StringComparison.Ordinal))
            return path[appPath.Length..];

        return path;
    }
}
