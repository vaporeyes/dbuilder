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
    public void ScriptInfoDefaultsToPromptingForUnsupportedFeatureVersions()
    {
        UdbScriptInfo script = new(
            "Demo",
            "Description",
            6,
            "/scripts/demo.js",
            "hash",
            null,
            Array.Empty<UdbScriptOption>());

        Assert.False(script.IgnoreVersion);
        Assert.True(UdbScriptRunnerModel.VersionGate(script.Version, script.IgnoreVersion).RequiresPrompt);
        Assert.False(UdbScriptRunnerModel.VersionGate(
            script.Version,
            (script with { IgnoreVersion = true }).IgnoreVersion).RequiresPrompt);
    }

    [Fact]
    public void VersionGateDecisionMatchesUdbPreRunContinueAndIgnoreVersionBranches()
    {
        UdbScriptVersionGateDecision current = UdbScriptRunnerModel.VersionGateDecision(
            5,
            ignoreVersion: false,
            UdbScriptVersionGateDialogResult.No);

        Assert.False(current.Gate.RequiresPrompt);
        Assert.Equal(UdbScriptVersionGateDialogResult.None, current.DialogResult);
        Assert.True(current.ShouldContinue);
        Assert.False(current.SetIgnoreVersion);

        UdbScriptVersionGateDecision accepted = UdbScriptRunnerModel.VersionGateDecision(
            6,
            ignoreVersion: false,
            UdbScriptVersionGateDialogResult.Yes);

        Assert.True(accepted.Gate.RequiresPrompt);
        Assert.Equal(UdbScriptVersionGateDialogResult.Yes, accepted.DialogResult);
        Assert.True(accepted.ShouldContinue);
        Assert.True(accepted.SetIgnoreVersion);

        UdbScriptVersionGateDecision declined = UdbScriptRunnerModel.VersionGateDecision(
            6,
            ignoreVersion: false,
            UdbScriptVersionGateDialogResult.No);

        Assert.True(declined.Gate.RequiresPrompt);
        Assert.Equal(UdbScriptVersionGateDialogResult.No, declined.DialogResult);
        Assert.False(declined.ShouldContinue);
        Assert.False(declined.SetIgnoreVersion);
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

    [Theory]
    [InlineData(0, "Loaded script source: 0 characters")]
    [InlineData(1, "Loaded script source: 1 character")]
    [InlineData(2, "Loaded script source: 2 characters")]
    public void LoadedScriptSourceStatusTextFormatsSingularAndPluralCharacterCounts(int characterCount, string expected)
        => Assert.Equal(expected, UdbScriptRunnerModel.LoadedScriptSourceStatusText(characterCount));

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
    public void RunnerFormMetadataMatchesUdbDesignerSurface()
    {
        UdbScriptRunnerFormMetadata form = UdbScriptRunnerModel.FormMetadata();

        Assert.Equal(524, form.ClientWidth);
        Assert.Equal(184, form.ClientHeight);
        Assert.Equal(540, form.MinimumWidth);
        Assert.Equal(200, form.MinimumHeight);
        Assert.False(form.ControlBox);
        Assert.False(form.ShowIcon);
        Assert.True(form.StartsMinimized);
        Assert.Equal("Running script", form.InitialTitle);
        Assert.Equal(
            ["progressbar", "lbStatus", "btnAction", "tbLog"],
            form.Controls.Select(control => control.Name).ToArray());

        UdbScriptRunnerFormControl progress = Assert.Single(form.Controls, control => control.Name == "progressbar");

        Assert.Equal(12, progress.X);
        Assert.Equal(25, progress.Y);
        Assert.Equal(419, progress.Width);
        Assert.Equal(23, progress.Height);
        Assert.True(progress.AnchorRight);
        Assert.False(progress.AnchorBottom);

        UdbScriptRunnerFormControl log = Assert.Single(form.Controls, control => control.Name == "tbLog");

        Assert.Equal(12, log.X);
        Assert.Equal(54, log.Y);
        Assert.Equal(500, log.Width);
        Assert.Equal(118, log.Height);
        Assert.True(log.AnchorRight);
        Assert.True(log.AnchorBottom);
    }

    [Fact]
    public void RunnerStartAndActionButtonPlansMatchUdbFormBranches()
    {
        UdbScriptRunnerStartPlan start = UdbScriptRunnerModel.StartPlan();

        Assert.True(start.CreateCancellationTokenSource);
        Assert.True(start.ResetRunningSeconds);
        Assert.True(start.ResetProgressValue);
        Assert.True(start.ClearLog);
        Assert.True(start.StartTimer);
        Assert.True(start.StartStopwatch);
        Assert.True(start.InvokeRunScript);
        Assert.Equal("Running script", start.InitialState.Title);
        Assert.Equal("Running script...", start.InitialState.StatusText);
        Assert.Equal("Cancel", start.InitialState.ActionButtonText);
        Assert.False(start.InitialState.ActionButtonEnabled);
        Assert.True(start.InitialState.ProgressIsMarquee);
        Assert.Equal(0.0, start.InitialState.Opacity);
        Assert.True(start.InitialState.AutoClose);

        UdbScriptRunnerActionButtonPlan running = UdbScriptRunnerModel.ActionButtonPlan(running: true);

        Assert.Equal(UdbScriptRunnerActionMode.CancelRunningScript, running.Mode);
        Assert.True(running.DisableActionButton);
        Assert.True(running.CancelToken);
        Assert.False(running.MakeInvisible);
        Assert.False(running.CloseWindow);

        UdbScriptRunnerActionButtonPlan finished = UdbScriptRunnerModel.ActionButtonPlan(running: false);

        Assert.Equal(UdbScriptRunnerActionMode.CloseRunner, finished.Mode);
        Assert.False(finished.DisableActionButton);
        Assert.False(finished.CancelToken);
        Assert.True(finished.MakeInvisible);
        Assert.True(finished.CloseWindow);
    }

    [Fact]
    public void RunnerLifecycleEventPlanMatchesUdbLoadAndCloseBranches()
    {
        UdbScriptRunnerLifecycleEventPlan plan = UdbScriptRunnerModel.LifecycleEventPlan();

        Assert.True(plan.MakeInvisibleOnLoad);
        Assert.True(plan.StopTimerOnClosed);
    }

    [Fact]
    public void RunScriptWorkflowPlanMatchesUdbFormOrchestration()
    {
        TimeSpan runtime = new(0, 0, 0, 2, 30);

        UdbScriptRunScriptWorkflowPlan success = UdbScriptRunnerModel.RunScriptWorkflowPlan(
            runtime,
            autoClose: true,
            hasException: false);

        Assert.True(success.CreateProgressCallbacks);
        Assert.True(success.SetRunningBeforePreRun);
        Assert.True(success.InvokePreRun);
        Assert.True(success.RunOnBackgroundTask);
        Assert.True(success.StopStopwatchAfterRun);
        Assert.False(success.HandleExceptions);
        Assert.True(success.InvokePostRun);
        Assert.True(success.ClearRunningAfterPostRun);
        Assert.Equal("Script finished", success.FinishedState.Title);
        Assert.Equal("Script finished. Runtime: 00:00:02.30", success.FinishedState.StatusText);
        Assert.Equal("Close", success.FinishedState.ActionButtonText);
        Assert.Equal(0, success.ResetProgressValue);
        Assert.True(success.ForceContinuousProgressStyle);
        Assert.True(success.CloseWhenAutoClose);

        UdbScriptRunScriptWorkflowPlan failure = UdbScriptRunnerModel.RunScriptWorkflowPlan(
            runtime,
            autoClose: false,
            hasException: true);

        Assert.True(failure.HandleExceptions);
        Assert.True(failure.StopStopwatchAfterRun);
        Assert.True(failure.InvokePostRun);
        Assert.False(failure.CloseWhenAutoClose);
        Assert.Equal(1.0, failure.FinishedState.Opacity);
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
    public void RunnerTimerTickPlanMatchesUdbVisibilityAndTitleBranches()
    {
        UdbScriptRunnerTimerTickPlan threshold = UdbScriptRunnerModel.TimerTickPlan(
            TimeSpan.FromMilliseconds(1000),
            runningSeconds: 1,
            opacity: 0.0);

        Assert.False(threshold.MakeVisible);
        Assert.False(threshold.EnableActionButton);
        Assert.False(threshold.UpdateRunningSeconds);
        Assert.Equal(1, threshold.RunningSeconds);
        Assert.Equal("", threshold.Title);

        UdbScriptRunnerTimerTickPlan visible = UdbScriptRunnerModel.TimerTickPlan(
            TimeSpan.FromMilliseconds(1001),
            runningSeconds: 1,
            opacity: 0.0);

        Assert.True(visible.MakeVisible);
        Assert.True(visible.EnableActionButton);
        Assert.False(visible.UpdateRunningSeconds);
        Assert.Equal(1, visible.RunningSeconds);
        Assert.Equal("", visible.Title);

        UdbScriptRunnerTimerTickPlan title = UdbScriptRunnerModel.TimerTickPlan(
            new TimeSpan(0, 1, 2, 3, 999),
            runningSeconds: 62,
            opacity: 1.0);

        Assert.False(title.MakeVisible);
        Assert.False(title.EnableActionButton);
        Assert.True(title.UpdateRunningSeconds);
        Assert.Equal(3723, title.RunningSeconds);
        Assert.Equal("Running script (01:02:03)", title.Title);

        UdbScriptRunnerTimerTickPlan alreadyVisible = UdbScriptRunnerModel.TimerTickPlan(
            TimeSpan.FromMilliseconds(1001),
            runningSeconds: 0,
            opacity: 1.0);

        Assert.False(alreadyVisible.MakeVisible);
        Assert.False(alreadyVisible.EnableActionButton);
    }

    [Fact]
    public void ProgressUpdatePlanMatchesUdbProgressBarBranches()
    {
        UdbScriptProgressUpdatePlan normal = UdbScriptRunnerModel.ProgressUpdatePlan(
            currentValue: 0,
            requestedValue: 42);

        Assert.True(normal.SetContinuousProgressStyle);
        Assert.True(normal.UpdateValue);
        Assert.Equal(42, normal.AppliedValue);
        Assert.Equal([43, 42], normal.ValueWrites);
        Assert.True(normal.MakeVisible);

        UdbScriptProgressUpdatePlan maximum = UdbScriptRunnerModel.ProgressUpdatePlan(
            currentValue: 10,
            requestedValue: 150);

        Assert.Equal(100, maximum.AppliedValue);
        Assert.Equal([100, 99, 100], maximum.ValueWrites);

        UdbScriptProgressUpdatePlan minimum = UdbScriptRunnerModel.ProgressUpdatePlan(
            currentValue: 10,
            requestedValue: -5);

        Assert.Equal(0, minimum.AppliedValue);
        Assert.Equal([1, 0], minimum.ValueWrites);

        UdbScriptProgressUpdatePlan unchanged = UdbScriptRunnerModel.ProgressUpdatePlan(
            currentValue: 42,
            requestedValue: 42,
            styleIsContinuous: true);

        Assert.False(unchanged.SetContinuousProgressStyle);
        Assert.False(unchanged.UpdateValue);
        Assert.Empty(unchanged.ValueWrites);
        Assert.True(unchanged.MakeVisible);
    }

    [Fact]
    public void InvokePausedPlanMatchesUdbTimerPauseHook()
    {
        UdbScriptInvokePausedPlan direct = UdbScriptRunnerModel.InvokePausedPlan(invokeRequired: false);
        UdbScriptInvokePausedPlan marshaled = UdbScriptRunnerModel.InvokePausedPlan(invokeRequired: true);

        Assert.False(direct.MarshalToUiThread);
        Assert.True(direct.StopStopwatchBeforeInvoke);
        Assert.True(direct.InvokeDelegate);
        Assert.True(direct.StartStopwatchAfterInvoke);
        Assert.True(direct.ReturnDelegateResult);
        Assert.True(marshaled.MarshalToUiThread);
        Assert.True(marshaled.StopStopwatchBeforeInvoke);
        Assert.True(marshaled.InvokeDelegate);
        Assert.True(marshaled.StartStopwatchAfterInvoke);
        Assert.True(marshaled.ReturnDelegateResult);
    }

    [Fact]
    public void RunActionPlanMatchesUdbRunnerFormActionHook()
    {
        UdbScriptRunActionPlan direct = UdbScriptRunnerModel.RunActionPlan(invokeRequired: false);
        UdbScriptRunActionPlan marshaled = UdbScriptRunnerModel.RunActionPlan(invokeRequired: true);

        Assert.False(direct.MarshalToUiThread);
        Assert.True(direct.InvokeAction);
        Assert.True(marshaled.MarshalToUiThread);
        Assert.True(marshaled.InvokeAction);
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
    public void RunnerStatusReportOnlyUpdatesStatusText()
    {
        UdbScriptRunnerUiState initial = UdbScriptRunnerModel.InitialUiState();

        UdbScriptRunnerUiState status = UdbScriptRunnerModel.StatusReportedUiState(initial, "Halfway done");

        Assert.Equal(initial.Title, status.Title);
        Assert.Equal("Halfway done", status.StatusText);
        Assert.Equal(initial.ActionButtonText, status.ActionButtonText);
        Assert.Equal(initial.ActionButtonEnabled, status.ActionButtonEnabled);
        Assert.Equal(initial.ProgressIsMarquee, status.ProgressIsMarquee);
        Assert.Equal(initial.Opacity, status.Opacity);
        Assert.Equal(initial.AutoClose, status.AutoClose);
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
    public void MessageDialogPlansMatchUdbShowMessageBranches()
    {
        UdbScriptMessageDialogPlan ok = UdbScriptRunnerModel.MessageDialogPlan(null, yesNo: false);
        UdbScriptMessageDialogPlan yesNo = UdbScriptRunnerModel.MessageDialogPlan("continue\nnow", yesNo: true);
        UdbScriptMessageDialogResultPlan okResult = UdbScriptRunnerModel.MessageDialogResultPlan(UdbScriptMessageResult.Ok);
        UdbScriptMessageDialogResultPlan yesResult = UdbScriptRunnerModel.MessageDialogResultPlan(UdbScriptMessageResult.Yes);
        UdbScriptMessageDialogResultPlan noResult = UdbScriptRunnerModel.MessageDialogResultPlan(UdbScriptMessageResult.No);
        UdbScriptMessageDialogResultPlan abortResult = UdbScriptRunnerModel.MessageDialogResultPlan(UdbScriptMessageResult.Abort);

        Assert.Equal("Script Message", ok.Title);
        Assert.Equal("OK", ok.PrimaryButtonText);
        Assert.Null(ok.SecondaryButtonText);
        Assert.Equal("Abort script", ok.AbortButtonText);
        Assert.Equal("Abort script", ok.AbortConfirmationTitle);
        Assert.Equal("Are you sure you want to abort the script?", ok.AbortConfirmationMessage);
        Assert.Equal("", ok.Message);
        Assert.True(ok.MessageReadOnly);
        Assert.Equal("Both", ok.MessageScrollBars);
        Assert.True(ok.StopStopwatchBeforeDialog);
        Assert.True(ok.StartStopwatchAfterDialog);
        Assert.Equal("Yes", yesNo.PrimaryButtonText);
        Assert.Equal("No", yesNo.SecondaryButtonText);
        Assert.Equal("continue" + Environment.NewLine + "now", yesNo.Message);
        Assert.False(okResult.ThrowUserAbortException);
        Assert.True(okResult.ReturnValue);
        Assert.True(yesResult.ReturnValue);
        Assert.False(noResult.ReturnValue);
        Assert.True(abortResult.ThrowUserAbortException);
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
    public void EngineSetupPlanMatchesUdbJintOptionsAndBindings()
    {
        UdbScriptEngineSetupPlan legacy = UdbScriptRunnerModel.EngineSetupPlan(3, debugBuild: true);

        Assert.True(legacy.UsesCancellationToken);
        Assert.True(legacy.AllowsOperatorOverloading);
        Assert.True(legacy.FiltersGetTypeMember);
        Assert.True(legacy.CatchesScriptRuntimeException);
        Assert.True(legacy.CatchesVectorConversionException);
        Assert.True(legacy.UsesLegacyGlobals);
        Assert.Equal(
            [
                "showMessage",
                "showMessageYesNo",
                "exit",
                "die",
                "QueryOptions",
                "ScriptOptions",
                "Map",
                "GameConfiguration",
                "Angle2D",
                "Vector3D",
                "Vector2D",
                "Line2D",
                "UniValue",
                "Data",
                "Linedef",
                "Sector",
                "Sidedef",
                "Thing",
                "Vertex",
                "log",
            ],
            legacy.EngineBindings);

        UdbScriptEngineSetupPlan modern = UdbScriptRunnerModel.EngineSetupPlan(4);

        Assert.False(modern.UsesLegacyGlobals);
        Assert.Equal(["UDB"], modern.EngineBindings);
    }

    [Fact]
    public void BindingPlanUsesScriptVersionOptionsAndRuntimeHelpers()
    {
        var length = new UdbScriptOption(
            "length",
            "Length",
            (int)UniversalType.Integer,
            128,
            "256",
            Array.Empty<UdbScriptEnumValue>(),
            "settings.length");
        var direction = new UdbScriptOption(
            "direction",
            "Direction",
            (int)UniversalType.EnumOption,
            "Down",
            "Up",
            new[]
            {
                new UdbScriptEnumValue("1", "Up"),
                new UdbScriptEnumValue("2", "Down"),
            },
            "settings.direction");
        UdbScriptInfo script = new(
            "Demo",
            "Description",
            3,
            "/scripts/demo.js",
            "hash",
            null,
            new[] { length, direction });

        UdbScriptRunnerBindingPlan plan = UdbScriptRunnerModel.BindingPlan(script);

        Assert.Equal(3u, plan.ScriptVersion);
        Assert.True(plan.EngineSetup.UsesLegacyGlobals);
        Assert.Contains("ScriptOptions", plan.EngineSetup.EngineBindings);
        Assert.True(plan.CreateQueryOptions);
        Assert.True(plan.CreateHostWrapper);
        Assert.Equal(256, plan.ScriptOptions["length"]);
        Assert.Equal(1, plan.ScriptOptions["direction"]);
    }

    [Fact]
    public void BindingPlanUsesUdbObjectForModernScripts()
    {
        UdbScriptInfo script = new(
            "Demo",
            "Description",
            4,
            "/scripts/demo.js",
            "hash",
            null,
            Array.Empty<UdbScriptOption>());

        UdbScriptRunnerBindingPlan plan = UdbScriptRunnerModel.BindingPlan(script);

        Assert.False(plan.EngineSetup.UsesLegacyGlobals);
        Assert.Equal(["UDB"], plan.EngineSetup.EngineBindings);
        Assert.Empty(plan.ScriptOptions);
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
    public void RunExecutionPlanMatchesUdbFileAndStopwatchSequence()
    {
        string app = Path.Combine(Path.GetTempPath(), "dbuilder_udbscript_runner");
        string script = Path.Combine(app, "UDBScript", "Scripts", "demo.js");

        UdbScriptRunExecutionPlan plan = UdbScriptRunnerModel.RunExecutionPlan(app, script);

        Assert.Equal(script, plan.Script.Path);
        Assert.Equal("/UDBScript/Scripts/demo.js", plan.Script.EngineSourceName);
        Assert.True(plan.ReadScriptFile);
        Assert.True(plan.ResetStopwatch);
        Assert.True(plan.StartStopwatchBeforeExecute);
        Assert.True(plan.StopStopwatchAfterExecute);
    }

    [Fact]
    public void RunExecutionPlanNormalizesAppRelativeEngineSourceNames()
    {
        UdbScriptRunExecutionPlan trailingSeparator = UdbScriptRunnerModel.RunExecutionPlan(
            "/app/",
            "/app/UDBScript/Scripts/demo.js");
        UdbScriptRunExecutionPlan windowsSeparators = UdbScriptRunnerModel.RunExecutionPlan(
            "C:\\UDB",
            "C:\\UDB\\UDBScript\\Scripts\\demo.js");
        UdbScriptRunExecutionPlan siblingPrefix = UdbScriptRunnerModel.RunExecutionPlan(
            "/app",
            "/application/UDBScript/Scripts/demo.js");

        Assert.Equal("/UDBScript/Scripts/demo.js", trailingSeparator.Script.EngineSourceName);
        Assert.Equal("/UDBScript/Scripts/demo.js", windowsSeparators.Script.EngineSourceName);
        Assert.Equal("/application/UDBScript/Scripts/demo.js", siblingPrefix.Script.EngineSourceName);
    }

    [Fact]
    public void LoadSourcePlanReadsLibrariesBeforeScript()
    {
        var plan = new UdbScriptRunSourcePlan(
            "/app/UDBScript/Libraries",
            new[]
            {
                new UdbScriptSourceFile("/app/UDBScript/Libraries/one.js", "/UDBScript/Libraries/one.js"),
                new UdbScriptSourceFile("/app/UDBScript/Libraries/two.js", "/UDBScript/Libraries/two.js"),
            },
            new UdbScriptSourceFile("/app/UDBScript/Scripts/demo.js", "/UDBScript/Scripts/demo.js"));
        var contents = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/app/UDBScript/Libraries/one.js"] = "one",
            ["/app/UDBScript/Libraries/two.js"] = "two",
            ["/app/UDBScript/Scripts/demo.js"] = "demo",
        };
        var readOrder = new List<string>();

        UdbScriptLoadedSourcePlan loaded = UdbScriptRunnerModel.LoadSourcePlan(
            plan,
            contents.ContainsKey,
            path =>
            {
                readOrder.Add(path);
                return contents[path];
            });

        Assert.True(loaded.Success);
        Assert.Equal("", loaded.MissingPath);
        Assert.Equal(["one", "two"], loaded.Libraries.Select(library => library.Text).ToArray());
        Assert.NotNull(loaded.Script);
        Assert.Equal("demo", loaded.Script.Text);
        Assert.Equal(
            ["/app/UDBScript/Libraries/one.js", "/app/UDBScript/Libraries/two.js", "/app/UDBScript/Scripts/demo.js"],
            readOrder);
    }

    [Fact]
    public void ExecutionSourcesExecuteLibrariesBeforeTimedScriptLikeUdb()
    {
        var plan = new UdbScriptLoadedSourcePlan(
            true,
            "",
            new[]
            {
                new UdbScriptLoadedSourceFile(
                    new UdbScriptSourceFile("/app/UDBScript/Libraries/one.js", "/UDBScript/Libraries/one.js"),
                    "one"),
                new UdbScriptLoadedSourceFile(
                    new UdbScriptSourceFile("/app/UDBScript/Libraries/two.js", "/UDBScript/Libraries/two.js"),
                    "two"),
            },
            new UdbScriptLoadedSourceFile(
                new UdbScriptSourceFile("/app/UDBScript/Scripts/demo.js", "/UDBScript/Scripts/demo.js"),
                "demo"));

        IReadOnlyList<UdbScriptExecutionSource> sources = UdbScriptRunnerModel.ExecutionSources(plan);

        Assert.Equal(
            ["/UDBScript/Libraries/one.js", "/UDBScript/Libraries/two.js", "/UDBScript/Scripts/demo.js"],
            sources.Select(source => source.Source.Source.EngineSourceName).ToArray());
        Assert.Equal([true, true, false], sources.Select(source => source.IsLibrary).ToArray());
        Assert.Equal([false, false, true], sources.Select(source => source.TimedByRunStopwatch).ToArray());
        Assert.Equal(["one", "two", "demo"], sources.Select(source => source.Source.Text).ToArray());
    }

    [Fact]
    public void ExecutionSourcesStopWhenSourceLoadingFails()
    {
        var plan = new UdbScriptLoadedSourcePlan(
            false,
            "/app/UDBScript/Libraries/missing.js",
            new[]
            {
                new UdbScriptLoadedSourceFile(
                    new UdbScriptSourceFile("/app/UDBScript/Libraries/one.js", "/UDBScript/Libraries/one.js"),
                    "one"),
            },
            null);

        Assert.Empty(UdbScriptRunnerModel.ExecutionSources(plan));
    }

    [Fact]
    public void LoadSourcePlanStopsAtMissingSourceFile()
    {
        var plan = new UdbScriptRunSourcePlan(
            "/app/UDBScript/Libraries",
            new[]
            {
                new UdbScriptSourceFile("/app/UDBScript/Libraries/one.js", "/UDBScript/Libraries/one.js"),
                new UdbScriptSourceFile("/app/UDBScript/Libraries/missing.js", "/UDBScript/Libraries/missing.js"),
            },
            new UdbScriptSourceFile("/app/UDBScript/Scripts/demo.js", "/UDBScript/Scripts/demo.js"));
        var contents = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/app/UDBScript/Libraries/one.js"] = "one",
            ["/app/UDBScript/Scripts/demo.js"] = "demo",
        };
        int reads = 0;

        UdbScriptLoadedSourcePlan loaded = UdbScriptRunnerModel.LoadSourcePlan(
            plan,
            contents.ContainsKey,
            path =>
            {
                reads++;
                return contents[path];
            });

        Assert.False(loaded.Success);
        Assert.Equal("/app/UDBScript/Libraries/missing.js", loaded.MissingPath);
        UdbScriptLoadedSourceFile library = Assert.Single(loaded.Libraries);
        Assert.Equal("one", library.Text);
        Assert.Null(loaded.Script);
        Assert.Equal(1, reads);
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
    public void ExceptionKindMapsTypedRunnerExceptions()
    {
        Assert.Equal(
            UdbScriptRunnerExceptionKind.UserAbort,
            UdbScriptRunnerModel.ExceptionKind(new UdbScriptUserAbortException()));
        Assert.Equal(
            UdbScriptRunnerExceptionKind.Exit,
            UdbScriptRunnerModel.ExceptionKind(new UdbScriptExitException("done")));
        Assert.Equal(
            UdbScriptRunnerExceptionKind.Die,
            UdbScriptRunnerModel.ExceptionKind(new UdbScriptDieException("failed")));
        Assert.Equal(
            UdbScriptRunnerExceptionKind.ExecutionCanceled,
            UdbScriptRunnerModel.ExceptionKind(new OperationCanceledException()));
        Assert.Equal(
            UdbScriptRunnerExceptionKind.Unknown,
            UdbScriptRunnerModel.ExceptionKind(new InvalidOperationException("failed")));
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
