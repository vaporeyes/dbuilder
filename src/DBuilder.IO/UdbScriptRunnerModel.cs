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

public enum UdbScriptRunnerActionMode
{
    CancelRunningScript,
    CloseRunner,
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

public enum UdbScriptRunnerExceptionDialogKind
{
    None,
    ParserMessageBox,
    ErrorDialog,
}

public enum UdbScriptRuntimeConstraintDialogResult
{
    None,
    Yes,
    No,
}

public enum UdbScriptVersionGateDialogResult
{
    None,
    Yes,
    No,
}

public sealed record UdbScriptRunnerExceptionOutcome(
    UdbScriptRunnerExceptionKind Kind,
    bool WithdrawUndo,
    UdbScriptRunnerStatusKind StatusKind,
    string StatusText);

public sealed record UdbScriptRunnerExceptionHandlingPlan(
    UdbScriptRunnerExceptionOutcome Outcome,
    UdbScriptRunnerExceptionDialogKind DialogKind,
    string DialogTitle,
    string DialogMessage,
    UdbScriptErrorDialog? ErrorDialog);

public sealed record UdbScriptLibraryImportExceptionPlan(
    UdbScriptRunnerExceptionKind Kind,
    bool ImportSucceeded,
    UdbScriptRunnerExceptionDialogKind DialogKind,
    string DialogTitle,
    string DialogMessage,
    UdbScriptRunnerStatusKind StatusKind,
    string StatusText,
    UdbScriptErrorDialog? ErrorDialog);

public sealed record UdbScriptVersionGate(
    bool RequiresPrompt,
    string Title,
    string Message);

public sealed record UdbScriptVersionGateDecision(
    UdbScriptVersionGate Gate,
    UdbScriptVersionGateDialogResult DialogResult,
    bool ShouldContinue,
    bool SetIgnoreVersion);

public sealed record UdbScriptLegacyBinding(string Name, string Target);

public sealed record UdbScriptHostMember(
    string Name,
    UdbScriptHostMemberKind Kind,
    string Target,
    uint MinVersion = 1);

public sealed record UdbScriptEngineSetupPlan(
    bool UsesCancellationToken,
    bool AllowsOperatorOverloading,
    bool FiltersGetTypeMember,
    bool CatchesScriptRuntimeException,
    bool CatchesVectorConversionException,
    bool UsesLegacyGlobals,
    IReadOnlyList<string> EngineBindings);

public sealed record UdbScriptRunnerBindingPlan(
    uint ScriptVersion,
    UdbScriptEngineSetupPlan EngineSetup,
    IReadOnlyDictionary<string, object> ScriptOptions,
    bool CreateQueryOptions,
    bool CreateHostWrapper);

public sealed record UdbScriptRuntimeConstraintPrompt(
    bool ShouldPrompt,
    string Title,
    string Message);

public sealed record UdbScriptMessageDialogPlan(
    string Title,
    string PrimaryButtonText,
    string? SecondaryButtonText,
    string AbortButtonText,
    string AbortConfirmationTitle,
    string AbortConfirmationMessage,
    string Message,
    bool MessageReadOnly,
    string MessageScrollBars,
    bool StopStopwatchBeforeDialog,
    bool StartStopwatchAfterDialog);

public sealed record UdbScriptMessageDialogResultPlan(
    bool ThrowUserAbortException,
    bool ReturnValue);

public sealed record UdbScriptRuntimeConstraintCheckResult(
    UdbScriptRuntimeConstraintPrompt Prompt,
    UdbScriptRuntimeConstraintDialogResult DialogResult,
    bool ThrowUserAbortException,
    bool RestartStopwatch);

public sealed record UdbScriptErrorDialog(
    string Title,
    string MessageLabel,
    string OkButtonText,
    string JavaScriptStackTraceTabText,
    string InternalStackTraceTabText,
    int SelectedTabIndex,
    string StackTraceText,
    string InternalStackTraceText);

public sealed record UdbScriptSourceFile(
    string Path,
    string EngineSourceName);

public sealed record UdbScriptRunSourcePlan(
    string LibrariesPath,
    IReadOnlyList<UdbScriptSourceFile> Libraries,
    UdbScriptSourceFile Script);

public sealed record UdbScriptLoadedSourceFile(
    UdbScriptSourceFile Source,
    string Text);

public sealed record UdbScriptLoadedSourcePlan(
    bool Success,
    string MissingPath,
    IReadOnlyList<UdbScriptLoadedSourceFile> Libraries,
    UdbScriptLoadedSourceFile? Script);

public sealed record UdbScriptExecutionSource(
    UdbScriptLoadedSourceFile Source,
    bool IsLibrary,
    bool TimedByRunStopwatch);

public sealed record UdbScriptRunExecutionPlan(
    UdbScriptSourceFile Script,
    bool ReadScriptFile,
    bool ResetStopwatch,
    bool StartStopwatchBeforeExecute,
    bool StopStopwatchAfterExecute);

public sealed record UdbScriptPreRunPlan(
    bool EndOptionEdit,
    bool FocusEditor,
    bool NotifyModeBegin,
    string UndoDescription,
    bool ClearAllMarksValue,
    bool MapIsSafeToAccess,
    int DisableProcessingCalls);

public sealed record UdbScriptPostRunPlan(
    bool MapIsSafeToAccess,
    bool UpdateMap,
    bool UpdateThingsFilter,
    bool NotifyModeEnd,
    int EnableProcessingCalls);

public sealed record UdbScriptRunnerUiState(
    string Title,
    string StatusText,
    string ActionButtonText,
    bool ActionButtonEnabled,
    bool ProgressIsMarquee,
    double Opacity,
    bool AutoClose);

public sealed record UdbScriptRunnerFormControl(
    string Name,
    int X,
    int Y,
    int Width,
    int Height,
    bool AnchorRight,
    bool AnchorBottom);

public sealed record UdbScriptRunnerFormMetadata(
    int ClientWidth,
    int ClientHeight,
    int MinimumWidth,
    int MinimumHeight,
    bool ControlBox,
    bool ShowIcon,
    bool StartsMinimized,
    string InitialTitle,
    IReadOnlyList<UdbScriptRunnerFormControl> Controls);

public sealed record UdbScriptRunnerStartPlan(
    bool CreateCancellationTokenSource,
    bool ResetRunningSeconds,
    bool ResetProgressValue,
    bool ClearLog,
    bool StartTimer,
    bool StartStopwatch,
    bool InvokeRunScript,
    UdbScriptRunnerUiState InitialState);

public sealed record UdbScriptRunnerActionButtonPlan(
    UdbScriptRunnerActionMode Mode,
    bool DisableActionButton,
    bool CancelToken,
    bool MakeInvisible,
    bool CloseWindow);

public sealed record UdbScriptRunnerLifecycleEventPlan(
    bool MakeInvisibleOnLoad,
    bool StopTimerOnClosed);

public sealed record UdbScriptRunnerTimerTickPlan(
    bool MakeVisible,
    bool EnableActionButton,
    bool UpdateRunningSeconds,
    double RunningSeconds,
    string Title);

public sealed record UdbScriptProgressUpdatePlan(
    bool SetContinuousProgressStyle,
    bool UpdateValue,
    int AppliedValue,
    IReadOnlyList<int> ValueWrites,
    bool MakeVisible);

public sealed record UdbScriptInvokePausedPlan(
    bool MarshalToUiThread,
    bool StopStopwatchBeforeInvoke,
    bool InvokeDelegate,
    bool StartStopwatchAfterInvoke,
    bool ReturnDelegateResult);

public sealed record UdbScriptRunActionPlan(
    bool MarshalToUiThread,
    bool InvokeAction);

public sealed record UdbScriptRunScriptWorkflowPlan(
    bool CreateProgressCallbacks,
    bool SetRunningBeforePreRun,
    bool InvokePreRun,
    bool RunOnBackgroundTask,
    bool StopStopwatchAfterRun,
    bool HandleExceptions,
    bool InvokePostRun,
    bool ClearRunningAfterPostRun,
    UdbScriptRunnerUiState FinishedState,
    int ResetProgressValue,
    bool ForceContinuousProgressStyle,
    bool CloseWhenAutoClose);

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
    public const string MessageDialogTitle = "Script Message";
    public const string MessageDialogOkButtonText = "OK";
    public const string MessageDialogYesButtonText = "Yes";
    public const string MessageDialogNoButtonText = "No";
    public const string MessageDialogAbortButtonText = "Abort script";
    public const string MessageDialogAbortConfirmationTitle = "Abort script";
    public const string MessageDialogAbortConfirmationMessage = "Are you sure you want to abort the script?";
    public const string MessageDialogScrollBars = "Both";
    public const string ScriptFinishedTitle = "Script finished";
    public const string CloseButtonText = "Close";
    public const string ErrorDialogTitle = "Script Error";
    public const string ErrorDialogMessageLabel = "There was an error while executing the script:";
    public const string ErrorDialogOkButtonText = "OK";
    public const string ErrorDialogJavaScriptStackTraceTabText = "JavaScript stack trace";
    public const string ErrorDialogInternalStackTraceTabText = "Internal stack trace";
    public const string ParserErrorDialogTitle = "Script error";
    public const string ParserErrorDialogPrefix = "There is an error while parsing the script:\n\n";
    public const string LibraryParserErrorPrefix = "There was an error while loading the library ";

    public static string LoadedScriptSourceStatusText(int characterCount)
        => $"Loaded script source: {CountLabel(characterCount, "character")}";

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

    public static bool CanAccessMember(string memberName, uint scriptVersion, uint minVersion = 1)
        => memberName != nameof(GetType) && minVersion <= scriptVersion;

    public static UdbScriptEngineSetupPlan EngineSetupPlan(uint scriptVersion, bool debugBuild = false)
    {
        string[] bindings = UsesLegacyGlobals(scriptVersion)
            ? LegacyBindings.Select(binding => binding.Name).ToArray()
            : ["UDB"];

        if (debugBuild)
            bindings = bindings.Append("log").ToArray();

        return new(
            UsesCancellationToken: true,
            AllowsOperatorOverloading: true,
            FiltersGetTypeMember: true,
            CatchesScriptRuntimeException: true,
            CatchesVectorConversionException: true,
            UsesLegacyGlobals(scriptVersion),
            bindings);
    }

    public static UdbScriptRunnerBindingPlan BindingPlan(UdbScriptInfo script, bool debugBuild = false)
        => new(
            script.Version,
            EngineSetupPlan(script.Version, debugBuild),
            UdbScriptOptionsUiModel.GetScriptOptions(script.Options),
            CreateQueryOptions: true,
            CreateHostWrapper: true);

    public static string UndoDescription(string scriptName)
        => "Run script " + scriptName;

    public static UdbScriptPreRunPlan PreRunPlan(string scriptName, int processingCount)
        => new(
            true,
            true,
            true,
            UndoDescription(scriptName),
            false,
            false,
            Math.Max(0, processingCount));

    public static UdbScriptPostRunPlan PostRunPlan(int previousProcessingCount)
        => new(
            true,
            true,
            true,
            true,
            Math.Max(0, previousProcessingCount));

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

    public static UdbScriptRunnerFormMetadata FormMetadata()
        => new(
            ClientWidth: 524,
            ClientHeight: 184,
            MinimumWidth: 540,
            MinimumHeight: 200,
            ControlBox: false,
            ShowIcon: false,
            StartsMinimized: true,
            InitialTitle: RunningScriptTitle,
            [
                new("progressbar", 12, 25, 419, 23, AnchorRight: true, AnchorBottom: false),
                new("lbStatus", 12, 9, 84, 13, AnchorRight: false, AnchorBottom: false),
                new("btnAction", 437, 25, 75, 23, AnchorRight: true, AnchorBottom: false),
                new("tbLog", 12, 54, 500, 118, AnchorRight: true, AnchorBottom: true),
            ]);

    public static UdbScriptRunnerStartPlan StartPlan()
        => new(
            CreateCancellationTokenSource: true,
            ResetRunningSeconds: true,
            ResetProgressValue: true,
            ClearLog: true,
            StartTimer: true,
            StartStopwatch: true,
            InvokeRunScript: true,
            InitialUiState());

    public static UdbScriptRunnerActionButtonPlan ActionButtonPlan(bool running)
        => running
            ? new(
                UdbScriptRunnerActionMode.CancelRunningScript,
                DisableActionButton: true,
                CancelToken: true,
                MakeInvisible: false,
                CloseWindow: false)
            : new(
                UdbScriptRunnerActionMode.CloseRunner,
                DisableActionButton: false,
                CancelToken: false,
                MakeInvisible: true,
                CloseWindow: true);

    public static UdbScriptRunnerLifecycleEventPlan LifecycleEventPlan()
        => new(
            MakeInvisibleOnLoad: true,
            StopTimerOnClosed: true);

    public static UdbScriptRunnerUiState FinishedUiState(TimeSpan runtime, bool autoClose)
        => new(
            ScriptFinishedTitle,
            FinishedStatus(runtime),
            CloseButtonText,
            true,
            false,
            autoClose ? 0.0 : 1.0,
            autoClose);

    public static UdbScriptRunScriptWorkflowPlan RunScriptWorkflowPlan(
        TimeSpan runtime,
        bool autoClose,
        bool hasException)
        => new(
            CreateProgressCallbacks: true,
            SetRunningBeforePreRun: true,
            InvokePreRun: true,
            RunOnBackgroundTask: true,
            StopStopwatchAfterRun: true,
            HandleExceptions: hasException,
            InvokePostRun: true,
            ClearRunningAfterPostRun: true,
            FinishedUiState(runtime, autoClose),
            ResetProgressValue: 0,
            ForceContinuousProgressStyle: true,
            CloseWhenAutoClose: autoClose);

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

    public static UdbScriptRunnerUiState StatusReportedUiState(UdbScriptRunnerUiState state, string status)
        => state with { StatusText = status };

    public static bool ShouldMakeRunnerVisible(TimeSpan elapsed)
        => elapsed.TotalMilliseconds > RunnerVisibilityThresholdMilliseconds;

    public static string RunningWindowTitle(TimeSpan elapsed)
        => RunningScriptTitle + " (" + string.Format("{0:D2}:{1:D2}:{2:D2}", elapsed.Hours, elapsed.Minutes, elapsed.Seconds) + ")";

    public static UdbScriptRunnerTimerTickPlan TimerTickPlan(
        TimeSpan elapsed,
        double runningSeconds,
        double opacity)
    {
        double elapsedSeconds = Math.Floor(elapsed.TotalSeconds);
        bool updateRunningSeconds = elapsedSeconds > runningSeconds;

        return new(
            MakeVisible: opacity == 0.0 && ShouldMakeRunnerVisible(elapsed),
            EnableActionButton: opacity == 0.0 && ShouldMakeRunnerVisible(elapsed),
            UpdateRunningSeconds: updateRunningSeconds,
            RunningSeconds: updateRunningSeconds ? elapsedSeconds : runningSeconds,
            Title: updateRunningSeconds ? RunningWindowTitle(elapsed) : "");
    }

    public static UdbScriptProgressUpdatePlan ProgressUpdatePlan(
        int currentValue,
        int requestedValue,
        int minimum = 0,
        int maximum = 100,
        bool styleIsContinuous = false)
    {
        bool updateValue = currentValue != requestedValue;
        int appliedValue = Math.Clamp(requestedValue, minimum, maximum);

        IReadOnlyList<int> valueWrites = updateValue
            ? appliedValue == maximum
                ? [appliedValue, appliedValue - 1, appliedValue]
                : [appliedValue + 1, appliedValue]
            : [];

        return new(
            SetContinuousProgressStyle: !styleIsContinuous,
            UpdateValue: updateValue,
            AppliedValue: appliedValue,
            valueWrites,
            MakeVisible: true);
    }

    public static UdbScriptInvokePausedPlan InvokePausedPlan(bool invokeRequired)
        => new(
            MarshalToUiThread: invokeRequired,
            StopStopwatchBeforeInvoke: true,
            InvokeDelegate: true,
            StartStopwatchAfterInvoke: true,
            ReturnDelegateResult: true);

    public static UdbScriptRunActionPlan RunActionPlan(bool invokeRequired)
        => new(
            MarshalToUiThread: invokeRequired,
            InvokeAction: true);

    public static string AppendLog(string existingLog, string text)
        => string.IsNullOrEmpty(existingLog) ? text : existingLog + Environment.NewLine + text;

    public static UdbScriptErrorDialog ErrorDialog(string message, string stackTrace, string internalStackTrace)
        => new(
            ErrorDialogTitle,
            ErrorDialogMessageLabel,
            ErrorDialogOkButtonText,
            ErrorDialogJavaScriptStackTraceTabText,
            ErrorDialogInternalStackTraceTabText,
            string.IsNullOrWhiteSpace(stackTrace) ? 1 : 0,
            message + "\r\n" + stackTrace,
            internalStackTrace);

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

    public static UdbScriptVersionGateDecision VersionGateDecision(
        uint scriptVersion,
        bool ignoreVersion,
        UdbScriptVersionGateDialogResult dialogResult)
    {
        UdbScriptVersionGate gate = VersionGate(scriptVersion, ignoreVersion);
        if (!gate.RequiresPrompt)
            return new(gate, UdbScriptVersionGateDialogResult.None, true, false);

        return new(
            gate,
            dialogResult,
            dialogResult == UdbScriptVersionGateDialogResult.Yes,
            dialogResult == UdbScriptVersionGateDialogResult.Yes);
    }

    public static UdbScriptRuntimeConstraintPrompt RuntimeConstraintPrompt(TimeSpan elapsed)
        => elapsed.TotalMilliseconds > RuntimeConstraintCheckMilliseconds
            ? new UdbScriptRuntimeConstraintPrompt(true, RuntimeConstraintPromptTitle, RuntimeConstraintPromptMessage)
            : new UdbScriptRuntimeConstraintPrompt(false, "", "");

    public static UdbScriptRuntimeConstraintCheckResult RuntimeConstraintCheck(
        TimeSpan elapsed,
        UdbScriptRuntimeConstraintDialogResult dialogResult)
    {
        UdbScriptRuntimeConstraintPrompt prompt = RuntimeConstraintPrompt(elapsed);
        if (!prompt.ShouldPrompt)
            return new(prompt, UdbScriptRuntimeConstraintDialogResult.None, false, false);

        return new(
            prompt,
            dialogResult,
            dialogResult == UdbScriptRuntimeConstraintDialogResult.Yes,
            dialogResult == UdbScriptRuntimeConstraintDialogResult.No);
    }

    public static UdbScriptMessageDialogPlan MessageDialogPlan(object? message, bool yesNo)
        => new(
            MessageDialogTitle,
            yesNo ? MessageDialogYesButtonText : MessageDialogOkButtonText,
            yesNo ? MessageDialogNoButtonText : null,
            MessageDialogAbortButtonText,
            MessageDialogAbortConfirmationTitle,
            MessageDialogAbortConfirmationMessage,
            NormalizeMessageDialogText(message),
            MessageReadOnly: true,
            MessageDialogScrollBars,
            StopStopwatchBeforeDialog: true,
            StartStopwatchAfterDialog: true);

    public static UdbScriptMessageDialogResultPlan MessageDialogResultPlan(UdbScriptMessageResult result)
        => new(
            result == UdbScriptMessageResult.Abort,
            result is UdbScriptMessageResult.Ok or UdbScriptMessageResult.Yes);

    private static string NormalizeMessageDialogText(object? message)
        => (message?.ToString() ?? "").Replace("\n", Environment.NewLine);

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

    public static UdbScriptRunExecutionPlan RunExecutionPlan(string appPath, string scriptFile)
        => new(
            new UdbScriptSourceFile(scriptFile, EngineSourceName(appPath, scriptFile)),
            ReadScriptFile: true,
            ResetStopwatch: true,
            StartStopwatchBeforeExecute: true,
            StopStopwatchAfterExecute: true);

    public static UdbScriptLoadedSourcePlan LoadSourcePlan(
        UdbScriptRunSourcePlan plan,
        Func<string, bool> exists,
        Func<string, string> readAllText)
    {
        var libraries = new List<UdbScriptLoadedSourceFile>();
        foreach (UdbScriptSourceFile library in plan.Libraries)
        {
            if (!exists(library.Path))
                return new UdbScriptLoadedSourcePlan(false, library.Path, libraries, null);

            libraries.Add(new UdbScriptLoadedSourceFile(library, readAllText(library.Path)));
        }

        if (!exists(plan.Script.Path))
            return new UdbScriptLoadedSourcePlan(false, plan.Script.Path, libraries, null);

        return new UdbScriptLoadedSourcePlan(
            true,
            "",
            libraries,
            new UdbScriptLoadedSourceFile(plan.Script, readAllText(plan.Script.Path)));
    }

    public static IReadOnlyList<UdbScriptExecutionSource> ExecutionSources(UdbScriptLoadedSourcePlan plan)
    {
        if (!plan.Success || plan.Script is null)
            return Array.Empty<UdbScriptExecutionSource>();

        var sources = new List<UdbScriptExecutionSource>(plan.Libraries.Count + 1);
        sources.AddRange(plan.Libraries.Select(library => new UdbScriptExecutionSource(
            library,
            IsLibrary: true,
            TimedByRunStopwatch: false)));
        sources.Add(new UdbScriptExecutionSource(
            plan.Script,
            IsLibrary: false,
            TimedByRunStopwatch: true));
        return sources;
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

    public static UdbScriptRunnerExceptionHandlingPlan ExceptionHandlingPlan(
        UdbScriptRunnerExceptionKind kind,
        string message = "",
        bool javascriptThrowIsString = false,
        string javascriptStackTrace = "",
        string internalStackTrace = "")
    {
        UdbScriptRunnerExceptionOutcome outcome = ClassifyException(kind, message, javascriptThrowIsString);

        return kind switch
        {
            UdbScriptRunnerExceptionKind.ParserError => new(
                outcome,
                UdbScriptRunnerExceptionDialogKind.ParserMessageBox,
                ParserErrorDialogTitle,
                ParserErrorDialogPrefix + message,
                null),
            UdbScriptRunnerExceptionKind.JavaScriptError when !javascriptThrowIsString => new(
                outcome,
                UdbScriptRunnerExceptionDialogKind.ErrorDialog,
                "",
                "",
                ErrorDialog(message, javascriptStackTrace, internalStackTrace)),
            UdbScriptRunnerExceptionKind.Unknown => new(
                outcome,
                UdbScriptRunnerExceptionDialogKind.ErrorDialog,
                "",
                "",
                ErrorDialog(message, "", internalStackTrace)),
            _ => new(outcome, UdbScriptRunnerExceptionDialogKind.None, "", "", null),
        };
    }

    public static UdbScriptRunnerExceptionKind ExceptionKind(Exception exception)
        => exception switch
        {
            UdbScriptUserAbortException => UdbScriptRunnerExceptionKind.UserAbort,
            UdbScriptExitException => UdbScriptRunnerExceptionKind.Exit,
            UdbScriptDieException => UdbScriptRunnerExceptionKind.Die,
            OperationCanceledException => UdbScriptRunnerExceptionKind.ExecutionCanceled,
            _ => UdbScriptRunnerExceptionKind.Unknown,
        };

    public static UdbScriptLibraryImportExceptionPlan LibraryImportExceptionPlan(
        UdbScriptRunnerExceptionKind kind,
        string libraryPath,
        string message = "",
        bool javascriptThrowIsString = false,
        string javascriptStackTrace = "",
        string internalStackTrace = "")
    {
        return kind switch
        {
            UdbScriptRunnerExceptionKind.ParserError => new(
                kind,
                false,
                UdbScriptRunnerExceptionDialogKind.ParserMessageBox,
                ParserErrorDialogTitle,
                LibraryParserErrorPrefix + libraryPath + ":\n\n" + message,
                UdbScriptRunnerStatusKind.None,
                "",
                null),
            UdbScriptRunnerExceptionKind.JavaScriptError when !javascriptThrowIsString => new(
                kind,
                false,
                UdbScriptRunnerExceptionDialogKind.ErrorDialog,
                "",
                "",
                UdbScriptRunnerStatusKind.None,
                "",
                ErrorDialog(message, javascriptStackTrace, internalStackTrace)),
            UdbScriptRunnerExceptionKind.JavaScriptError => new(
                kind,
                false,
                UdbScriptRunnerExceptionDialogKind.None,
                "",
                "",
                UdbScriptRunnerStatusKind.Warning,
                message,
                null),
            _ => new(kind, true, UdbScriptRunnerExceptionDialogKind.None, "", "", UdbScriptRunnerStatusKind.None, "", null),
        };
    }

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";

    private static string EngineSourceName(string appPath, string path)
    {
        string normalizedAppPath = TrimTrailingDirectorySeparators(NormalizeDirectorySeparators(appPath));
        string normalizedPath = NormalizeDirectorySeparators(path);

        if (normalizedPath.Length > normalizedAppPath.Length
            && normalizedPath.StartsWith(normalizedAppPath, StringComparison.Ordinal)
            && normalizedPath[normalizedAppPath.Length] == '/')
        {
            return normalizedPath[normalizedAppPath.Length..];
        }

        return normalizedPath;
    }

    private static string NormalizeDirectorySeparators(string path)
        => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/').Replace('\\', '/');

    private static string TrimTrailingDirectorySeparators(string path)
        => path.TrimEnd('/');
}
