// ABOUTME: Tests UDBScript runner lifecycle metadata and exception handling outcomes.
// ABOUTME: Covers feature-version prompts, runtime text, global binding mode, and undo behavior.

using DBuilder.IO;

namespace DBuilder.Tests;

public class UdbScriptRunnerModelTests
{
    [Fact]
    public void VersionGateMatchesUdbPromptText()
    {
        UdbScriptVersionGate gate = UdbScriptRunnerModel.VersionGate(6, ignoreVersion: false);

        Assert.True(gate.RequiresPrompt);
        Assert.Equal("UDBScript feature version too low", gate.Title);
        Assert.Contains("Required feature version: 6", gate.Message, StringComparison.Ordinal);
        Assert.Contains("UDBScript feature version: 5", gate.Message, StringComparison.Ordinal);
        Assert.EndsWith("Execute anyway?", gate.Message, StringComparison.Ordinal);

        Assert.False(UdbScriptRunnerModel.VersionGate(5, ignoreVersion: false).RequiresPrompt);
        Assert.False(UdbScriptRunnerModel.VersionGate(6, ignoreVersion: true).RequiresPrompt);
    }

    [Fact]
    public void RuntimeAndLifecycleTextMatchesUdb()
    {
        TimeSpan runtime = new(0, 1, 2, 3, 45);

        Assert.Equal("01:02:03.45", UdbScriptRunnerModel.FormatRuntime(runtime));
        Assert.Equal("Script finished. Runtime: 01:02:03.45", UdbScriptRunnerModel.FinishedStatus(runtime));
        Assert.Equal("Script finished", UdbScriptRunnerModel.ScriptFinishedTitle);
        Assert.Equal("Close", UdbScriptRunnerModel.CloseButtonText);
        Assert.Equal("Run script Demo", UdbScriptRunnerModel.UndoDescription("Demo"));
    }

    [Fact]
    public void PreAndPostRunPlansMatchUdbExecutionSideEffects()
    {
        UdbScriptPreRunPlan preRun = UdbScriptRunnerModel.PreRunPlan("Demo", processingCount: 3);

        Assert.True(preRun.EndOptionEdit);
        Assert.True(preRun.FocusEditor);
        Assert.True(preRun.NotifyModeBegin);
        Assert.Equal("Run script Demo", preRun.UndoDescription);
        Assert.False(preRun.ClearAllMarksValue);
        Assert.False(preRun.MapIsSafeToAccess);
        Assert.Equal(3, preRun.DisableProcessingCalls);
        Assert.Equal(0, UdbScriptRunnerModel.PreRunPlan("Demo", processingCount: -1).DisableProcessingCalls);

        UdbScriptPostRunPlan postRun = UdbScriptRunnerModel.PostRunPlan(previousProcessingCount: 3);

        Assert.True(postRun.MapIsSafeToAccess);
        Assert.True(postRun.UpdateMap);
        Assert.True(postRun.UpdateThingsFilter);
        Assert.True(postRun.NotifyModeEnd);
        Assert.Equal(3, postRun.EnableProcessingCalls);
        Assert.Equal(0, UdbScriptRunnerModel.PostRunPlan(previousProcessingCount: -1).EnableProcessingCalls);
    }

    [Fact]
    public void RunnerUiStateMatchesUdbFormLifecycle()
    {
        UdbScriptRunnerUiState initial = UdbScriptRunnerModel.InitialUiState();

        Assert.Equal(1000, UdbScriptRunnerModel.RunnerVisibilityThresholdMilliseconds);
        Assert.Equal(100, UdbScriptRunnerModel.RunnerTimerIntervalMilliseconds);
        Assert.Equal("Running script", initial.Title);
        Assert.Equal("Running script...", initial.StatusText);
        Assert.Equal("Cancel", initial.ActionButtonText);
        Assert.False(initial.ActionButtonEnabled);
        Assert.True(initial.ProgressIsMarquee);
        Assert.Equal(0.0, initial.Opacity);
        Assert.True(initial.AutoClose);

        TimeSpan runtime = new(0, 1, 2, 3, 4);
        UdbScriptRunnerUiState finished = UdbScriptRunnerModel.FinishedUiState(runtime, autoClose: false);

        Assert.Equal("Script finished", finished.Title);
        Assert.Equal("Script finished. Runtime: 01:02:03.4", finished.StatusText);
        Assert.Equal("Close", finished.ActionButtonText);
        Assert.True(finished.ActionButtonEnabled);
        Assert.False(finished.ProgressIsMarquee);
        Assert.Equal(1.0, finished.Opacity);
        Assert.False(finished.AutoClose);
        Assert.Equal(0.0, UdbScriptRunnerModel.FinishedUiState(runtime, autoClose: true).Opacity);
        Assert.True(UdbScriptRunnerModel.FinishedUiState(runtime, autoClose: true).AutoClose);
    }

    [Fact]
    public void RunnerTimerAndLogFormattingMatchUdb()
    {
        Assert.False(UdbScriptRunnerModel.ShouldMakeRunnerVisible(TimeSpan.FromMilliseconds(1000)));
        Assert.True(UdbScriptRunnerModel.ShouldMakeRunnerVisible(TimeSpan.FromMilliseconds(1001)));
        Assert.Equal("Running script (01:02:03)", UdbScriptRunnerModel.RunningWindowTitle(new TimeSpan(0, 1, 2, 3, 999)));
        Assert.Equal("first", UdbScriptRunnerModel.AppendLog("", "first"));
        Assert.Equal("first" + Environment.NewLine + "second", UdbScriptRunnerModel.AppendLog("first", "second"));
    }

    [Fact]
    public void RunnerProgressAndLogReportsMatchUdbFormVisibility()
    {
        UdbScriptRunnerUiState initial = UdbScriptRunnerModel.InitialUiState();

        UdbScriptRunnerUiState progress = UdbScriptRunnerModel.ProgressReportedUiState(initial);

        Assert.True(progress.ActionButtonEnabled);
        Assert.False(progress.ProgressIsMarquee);
        Assert.Equal(1.0, progress.Opacity);
        Assert.True(progress.AutoClose);

        UdbScriptRunnerUiState logged = UdbScriptRunnerModel.LogReportedUiState(initial);

        Assert.True(logged.ActionButtonEnabled);
        Assert.True(logged.ProgressIsMarquee);
        Assert.Equal(1.0, logged.Opacity);
        Assert.False(logged.AutoClose);
    }

    [Fact]
    public void ErrorDialogMetadataMatchesUdbForm()
    {
        UdbScriptErrorDialog dialog = UdbScriptRunnerModel.ErrorDialog("failed", "script stack", "internal stack");

        Assert.Equal("Script Error", dialog.Title);
        Assert.Equal("There was an error while executing the script:", dialog.MessageLabel);
        Assert.Equal("OK", dialog.OkButtonText);
        Assert.Equal("JavaScript stack trace", dialog.JavaScriptStackTraceTabText);
        Assert.Equal("Internal stack trace", dialog.InternalStackTraceTabText);
        Assert.Equal(0, dialog.SelectedTabIndex);
        Assert.Equal("failed\r\nscript stack", dialog.StackTraceText);
        Assert.Equal("internal stack", dialog.InternalStackTraceText);
    }

    [Fact]
    public void ErrorDialogSelectsInternalStackTabWhenScriptStackIsBlank()
    {
        UdbScriptErrorDialog dialog = UdbScriptRunnerModel.ErrorDialog("failed", "   ", "internal stack");

        Assert.Equal(1, dialog.SelectedTabIndex);
        Assert.Equal("failed\r\n   ", dialog.StackTraceText);
        Assert.Equal("internal stack", dialog.InternalStackTraceText);
    }

    [Fact]
    public void HostWrapperReportsProgressAndLogsNonNullText()
    {
        List<int> progressValues = [];
        List<string> logValues = [];
        UdbScriptHostWrapper host = new(
            progress: progressValues.Add,
            logger: logValues.Add);

        host.setProgress(42);
        host.log("hello");
        host.log(123);
        host.log(null);

        Assert.Equal([42], progressValues);
        Assert.Equal(["hello", "123"], logValues);
    }

    [Fact]
    public void HostWrapperShowsMessagesAndNormalizesNull()
    {
        List<string> messages = [];
        UdbScriptHostWrapper host = new(
            messageCallback: message =>
            {
                messages.Add(message);
                return UdbScriptMessageResult.Ok;
            },
            yesNoMessageCallback: message =>
            {
                messages.Add(message);
                return UdbScriptMessageResult.Yes;
            });

        host.showMessage(null);
        bool yes = host.showMessageYesNo("continue");
        bool no = new UdbScriptHostWrapper(yesNoMessageCallback: _ => UdbScriptMessageResult.No)
            .showMessageYesNo("continue");

        Assert.True(yes);
        Assert.False(no);
        Assert.Equal(["", "continue"], messages);
    }

    [Fact]
    public void HostWrapperThrowsAbortWhenMessageAborted()
    {
        UdbScriptHostWrapper messageHost = new(messageCallback: _ => UdbScriptMessageResult.Abort);
        UdbScriptHostWrapper yesNoHost = new(yesNoMessageCallback: _ => UdbScriptMessageResult.Abort);

        Assert.Throws<UdbScriptUserAbortException>(() => messageHost.showMessage("stop"));
        Assert.Throws<UdbScriptUserAbortException>(() => yesNoHost.showMessageYesNo("stop"));
    }

    [Fact]
    public void HostWrapperExitAndDieThrowTypedExceptions()
    {
        UdbScriptHostWrapper host = new();

        Assert.Equal("", Assert.Throws<UdbScriptExitException>(() => host.exit()).Message);
        Assert.Equal("done", Assert.Throws<UdbScriptExitException>(() => host.exit("done")).Message);
        Assert.Equal("", Assert.Throws<UdbScriptDieException>(() => host.die()).Message);
        Assert.Equal("failed", Assert.Throws<UdbScriptDieException>(() => host.die("failed")).Message);
    }

    [Fact]
    public void HostMembersMatchUdbTopLevelPropertySurface()
    {
        Assert.Equal(17, UdbScriptRunnerModel.HostMembers.Count);
        Assert.Equal(
            [
                "GameConfiguration",
                "QueryOptions",
                "ScriptOptions",
                "Angle2D",
                "Data",
                "Line2D",
                "Map",
                "UniValue",
                "Vector2D",
                "Vector3D",
                "Linedef",
                "Sector",
                "Sidedef",
                "Thing",
                "Vertex",
                "Plane",
                "BlockMap",
            ],
            UdbScriptRunnerModel.HostMembers.Select(member => member.Name).ToArray());

        Assert.Contains(
            UdbScriptRunnerModel.HostMembers,
            member => member.Name == "ScriptOptions"
                && member.Kind == UdbScriptHostMemberKind.ScriptOptions
                && member.Target == "ExpandoObject");
        Assert.Contains(
            UdbScriptRunnerModel.HostMembers,
            member => member.Name == "Map"
                && member.Kind == UdbScriptHostMemberKind.Object
                && member.Target == "MapWrapper");
        Assert.Contains(
            UdbScriptRunnerModel.HostMembers,
            member => member.Name == "Vector2D"
                && member.Kind == UdbScriptHostMemberKind.TypeReference
                && member.Target == "Vector2DWrapper");
        Assert.Contains(
            UdbScriptRunnerModel.HostMembers,
            member => member.Name == "Plane"
                && member.Kind == UdbScriptHostMemberKind.TypeReference
                && member.Target == "PlaneWrapper"
                && member.MinVersion == 5);
        Assert.Contains(
            UdbScriptRunnerModel.HostMembers,
            member => member.Name == "BlockMap"
                && member.Kind == UdbScriptHostMemberKind.TypeReference
                && member.Target == "BlockMapWrapper"
                && member.MinVersion == 5);
    }

    [Fact]
    public void MemberFilterMatchesUdbVersionGate()
    {
        Assert.True(UdbScriptRunnerModel.CanAccessMember("index", scriptVersion: 4));
        Assert.False(UdbScriptRunnerModel.CanAccessMember("GetType", scriptVersion: 5));
        Assert.False(UdbScriptRunnerModel.CanAccessMember("blockMap", scriptVersion: 4, minVersion: 5));
        Assert.True(UdbScriptRunnerModel.CanAccessMember("blockMap", scriptVersion: 5, minVersion: 5));
        Assert.True(UdbScriptRunnerModel.CanAccessMember("blockMap", scriptVersion: 6, minVersion: 5));
    }

    [Fact]
    public void RuntimeConstraintPromptMatchesUdbThresholdAndText()
    {
        Assert.Equal(5000, UdbScriptRunnerModel.RuntimeConstraintCheckMilliseconds);
        Assert.False(UdbScriptRunnerModel.RuntimeConstraintPrompt(TimeSpan.FromMilliseconds(5000)).ShouldPrompt);

        UdbScriptRuntimeConstraintPrompt prompt = UdbScriptRunnerModel.RuntimeConstraintPrompt(TimeSpan.FromMilliseconds(5001));

        Assert.True(prompt.ShouldPrompt);
        Assert.Equal("Script", prompt.Title);
        Assert.Equal("The script has been running for some time, want to stop it?", prompt.Message);
    }

    [Fact]
    public void RuntimeConstraintCheckMatchesUdbAbortAndRestartBranches()
    {
        UdbScriptRuntimeConstraintCheckResult withinThreshold = UdbScriptRunnerModel.RuntimeConstraintCheck(
            TimeSpan.FromMilliseconds(5000),
            UdbScriptRuntimeConstraintDialogResult.Yes);

        Assert.False(withinThreshold.Prompt.ShouldPrompt);
        Assert.Equal(UdbScriptRuntimeConstraintDialogResult.None, withinThreshold.DialogResult);
        Assert.False(withinThreshold.ThrowUserAbortException);
        Assert.False(withinThreshold.RestartStopwatch);

        UdbScriptRuntimeConstraintCheckResult yes = UdbScriptRunnerModel.RuntimeConstraintCheck(
            TimeSpan.FromMilliseconds(5001),
            UdbScriptRuntimeConstraintDialogResult.Yes);

        Assert.True(yes.Prompt.ShouldPrompt);
        Assert.Equal(UdbScriptRuntimeConstraintDialogResult.Yes, yes.DialogResult);
        Assert.True(yes.ThrowUserAbortException);
        Assert.False(yes.RestartStopwatch);

        UdbScriptRuntimeConstraintCheckResult no = UdbScriptRunnerModel.RuntimeConstraintCheck(
            TimeSpan.FromMilliseconds(5001),
            UdbScriptRuntimeConstraintDialogResult.No);

        Assert.True(no.Prompt.ShouldPrompt);
        Assert.Equal(UdbScriptRuntimeConstraintDialogResult.No, no.DialogResult);
        Assert.False(no.ThrowUserAbortException);
        Assert.True(no.RestartStopwatch);
    }

    [Fact]
    public void SourcePlanImportsRecursiveLibrariesAndUsesAppRelativeSourceNames()
    {
        string app = Path.Combine(Path.GetTempPath(), "dbuilder_udbscript_runner_" + Guid.NewGuid().ToString("N"));
        try
        {
            string libraries = Path.Combine(app, "UDBScript", "Libraries");
            string nested = Path.Combine(libraries, "Nested");
            Directory.CreateDirectory(nested);
            string first = Path.Combine(libraries, "one.js");
            string second = Path.Combine(nested, "two.js");
            string ignored = Path.Combine(nested, "ignore.txt");
            string script = Path.Combine(app, "UDBScript", "Scripts", "demo.js");
            Directory.CreateDirectory(Path.GetDirectoryName(script)!);
            File.WriteAllText(first, "");
            File.WriteAllText(second, "");
            File.WriteAllText(ignored, "");
            File.WriteAllText(script, "");

            UdbScriptRunSourcePlan plan = UdbScriptRunnerModel.BuildSourcePlan(app, script);

            Assert.Equal(libraries, plan.LibrariesPath);
            Assert.Equal(
                new[] { first, second }.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                plan.Libraries.Select(library => library.Path).ToArray());
            Assert.Equal(
                new[] { "/UDBScript/Libraries/Nested/two.js", "/UDBScript/Libraries/one.js" },
                plan.Libraries.Select(library => library.EngineSourceName).ToArray());
            Assert.Equal(script, plan.Script.Path);
            Assert.Equal("/UDBScript/Scripts/demo.js", plan.Script.EngineSourceName);
        }
        finally
        {
            if (Directory.Exists(app))
                Directory.Delete(app, recursive: true);
        }
    }

    [Fact]
    public void SourcePlanAllowsMissingLibraryDirectory()
    {
        string app = Path.Combine(Path.GetTempPath(), "dbuilder_udbscript_runner_" + Guid.NewGuid().ToString("N"));
        string script = Path.Combine(app, "UDBScript", "Scripts", "demo.js");

        UdbScriptRunSourcePlan plan = UdbScriptRunnerModel.BuildSourcePlan(app, script);

        Assert.Empty(plan.Libraries);
        Assert.Equal(Path.Combine(app, "UDBScript", "Libraries"), plan.LibrariesPath);
        Assert.Equal("/UDBScript/Scripts/demo.js", plan.Script.EngineSourceName);
    }

    [Fact]
    public void LegacyGlobalModeUsesScriptVersionsBeforeFour()
    {
        Assert.True(UdbScriptRunnerModel.UsesLegacyGlobals(1));
        Assert.True(UdbScriptRunnerModel.UsesLegacyGlobals(3));
        Assert.False(UdbScriptRunnerModel.UsesLegacyGlobals(4));
        Assert.False(UdbScriptRunnerModel.UsesLegacyGlobals(5));

        Assert.Contains(UdbScriptRunnerModel.LegacyBindings, binding => binding.Name == "showMessage" && binding.Target == "ShowMessage");
        Assert.Contains(UdbScriptRunnerModel.LegacyBindings, binding => binding.Name == "ScriptOptions" && binding.Target == "ScriptOptions");
        Assert.Contains(UdbScriptRunnerModel.LegacyBindings, binding => binding.Name == "Map" && binding.Target == "MapWrapper");
        Assert.Contains(UdbScriptRunnerModel.LegacyBindings, binding => binding.Name == "Vertex" && binding.Target == "VertexWrapper");
    }

    [Theory]
    [InlineData(UdbScriptRunnerExceptionKind.UserAbort, "", false, true, UdbScriptRunnerStatusKind.Warning, "Script aborted")]
    [InlineData(UdbScriptRunnerExceptionKind.ParserError, "", false, true, UdbScriptRunnerStatusKind.None, "")]
    [InlineData(UdbScriptRunnerExceptionKind.JavaScriptError, "thrown", true, true, UdbScriptRunnerStatusKind.Warning, "thrown")]
    [InlineData(UdbScriptRunnerExceptionKind.JavaScriptError, "object", false, true, UdbScriptRunnerStatusKind.None, "")]
    [InlineData(UdbScriptRunnerExceptionKind.Exit, "done", false, false, UdbScriptRunnerStatusKind.Ready, "done")]
    [InlineData(UdbScriptRunnerExceptionKind.Exit, "", false, false, UdbScriptRunnerStatusKind.None, "")]
    [InlineData(UdbScriptRunnerExceptionKind.Die, "failed", false, true, UdbScriptRunnerStatusKind.Warning, "failed")]
    [InlineData(UdbScriptRunnerExceptionKind.Die, "", false, true, UdbScriptRunnerStatusKind.None, "")]
    [InlineData(UdbScriptRunnerExceptionKind.ExecutionCanceled, "", false, true, UdbScriptRunnerStatusKind.None, "")]
    [InlineData(UdbScriptRunnerExceptionKind.Unknown, "", false, true, UdbScriptRunnerStatusKind.None, "")]
    public void ExceptionClassificationMatchesUdbUndoAndStatusBehavior(
        UdbScriptRunnerExceptionKind kind,
        string message,
        bool javascriptThrowIsString,
        bool withdrawUndo,
        UdbScriptRunnerStatusKind statusKind,
        string statusText)
    {
        UdbScriptRunnerExceptionOutcome outcome = UdbScriptRunnerModel.ClassifyException(kind, message, javascriptThrowIsString);

        Assert.Equal(withdrawUndo, outcome.WithdrawUndo);
        Assert.Equal(statusKind, outcome.StatusKind);
        Assert.Equal(statusText, outcome.StatusText);
    }

    [Fact]
    public void ExceptionHandlingPlanMatchesUdbDialogAndStatusBranches()
    {
        UdbScriptRunnerExceptionHandlingPlan parser = UdbScriptRunnerModel.ExceptionHandlingPlan(
            UdbScriptRunnerExceptionKind.ParserError,
            "bad token");

        Assert.Equal(UdbScriptRunnerExceptionDialogKind.ParserMessageBox, parser.DialogKind);
        Assert.Equal("Script error", parser.DialogTitle);
        Assert.Equal("There is an error while parsing the script:\n\nbad token", parser.DialogMessage);
        Assert.True(parser.Outcome.WithdrawUndo);

        UdbScriptRunnerExceptionHandlingPlan javascriptObject = UdbScriptRunnerModel.ExceptionHandlingPlan(
            UdbScriptRunnerExceptionKind.JavaScriptError,
            "object throw",
            javascriptThrowIsString: false,
            javascriptStackTrace: "script stack",
            internalStackTrace: "internal stack");

        Assert.Equal(UdbScriptRunnerExceptionDialogKind.ErrorDialog, javascriptObject.DialogKind);
        Assert.NotNull(javascriptObject.ErrorDialog);
        Assert.Equal("object throw\r\nscript stack", javascriptObject.ErrorDialog.StackTraceText);
        Assert.Equal("internal stack", javascriptObject.ErrorDialog.InternalStackTraceText);
        Assert.Equal(UdbScriptRunnerStatusKind.None, javascriptObject.Outcome.StatusKind);

        UdbScriptRunnerExceptionHandlingPlan javascriptString = UdbScriptRunnerModel.ExceptionHandlingPlan(
            UdbScriptRunnerExceptionKind.JavaScriptError,
            "string throw",
            javascriptThrowIsString: true);

        Assert.Equal(UdbScriptRunnerExceptionDialogKind.None, javascriptString.DialogKind);
        Assert.Equal(UdbScriptRunnerStatusKind.Warning, javascriptString.Outcome.StatusKind);
        Assert.Equal("string throw", javascriptString.Outcome.StatusText);

        UdbScriptRunnerExceptionHandlingPlan unknown = UdbScriptRunnerModel.ExceptionHandlingPlan(
            UdbScriptRunnerExceptionKind.Unknown,
            "unexpected",
            internalStackTrace: "clr stack");

        Assert.Equal(UdbScriptRunnerExceptionDialogKind.ErrorDialog, unknown.DialogKind);
        Assert.NotNull(unknown.ErrorDialog);
        Assert.Equal("unexpected\r\n", unknown.ErrorDialog.StackTraceText);
        Assert.Equal("clr stack", unknown.ErrorDialog.InternalStackTraceText);
    }

    [Fact]
    public void LibraryImportExceptionPlanMatchesUdbImportErrorBranches()
    {
        UdbScriptLibraryImportExceptionPlan parser = UdbScriptRunnerModel.LibraryImportExceptionPlan(
            UdbScriptRunnerExceptionKind.ParserError,
            "/app/UDBScript/Libraries/bad.js",
            "bad token");

        Assert.False(parser.ImportSucceeded);
        Assert.Equal(UdbScriptRunnerExceptionDialogKind.ParserMessageBox, parser.DialogKind);
        Assert.Equal("Script error", parser.DialogTitle);
        Assert.Equal(
            "There was an error while loading the library /app/UDBScript/Libraries/bad.js:\n\nbad token",
            parser.DialogMessage);
        Assert.Equal(UdbScriptRunnerStatusKind.None, parser.StatusKind);

        UdbScriptLibraryImportExceptionPlan javascriptObject = UdbScriptRunnerModel.LibraryImportExceptionPlan(
            UdbScriptRunnerExceptionKind.JavaScriptError,
            "/app/UDBScript/Libraries/object.js",
            "object throw",
            javascriptThrowIsString: false,
            javascriptStackTrace: "script stack",
            internalStackTrace: "internal stack");

        Assert.False(javascriptObject.ImportSucceeded);
        Assert.Equal(UdbScriptRunnerExceptionDialogKind.ErrorDialog, javascriptObject.DialogKind);
        Assert.NotNull(javascriptObject.ErrorDialog);
        Assert.Equal("object throw\r\nscript stack", javascriptObject.ErrorDialog.StackTraceText);
        Assert.Equal("internal stack", javascriptObject.ErrorDialog.InternalStackTraceText);

        UdbScriptLibraryImportExceptionPlan javascriptString = UdbScriptRunnerModel.LibraryImportExceptionPlan(
            UdbScriptRunnerExceptionKind.JavaScriptError,
            "/app/UDBScript/Libraries/string.js",
            "string throw",
            javascriptThrowIsString: true);

        Assert.False(javascriptString.ImportSucceeded);
        Assert.Equal(UdbScriptRunnerExceptionDialogKind.None, javascriptString.DialogKind);
        Assert.Equal(UdbScriptRunnerStatusKind.Warning, javascriptString.StatusKind);
        Assert.Equal("string throw", javascriptString.StatusText);

        UdbScriptLibraryImportExceptionPlan other = UdbScriptRunnerModel.LibraryImportExceptionPlan(
            UdbScriptRunnerExceptionKind.Exit,
            "/app/UDBScript/Libraries/exit.js");

        Assert.True(other.ImportSucceeded);
        Assert.Equal(UdbScriptRunnerExceptionDialogKind.None, other.DialogKind);
    }
}
