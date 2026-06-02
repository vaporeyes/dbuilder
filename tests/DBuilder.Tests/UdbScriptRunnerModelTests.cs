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

        TimeSpan runtime = new(0, 1, 2, 3, 4);
        UdbScriptRunnerUiState finished = UdbScriptRunnerModel.FinishedUiState(runtime, autoClose: false);

        Assert.Equal("Script finished", finished.Title);
        Assert.Equal("Script finished. Runtime: 01:02:03.4", finished.StatusText);
        Assert.Equal("Close", finished.ActionButtonText);
        Assert.True(finished.ActionButtonEnabled);
        Assert.False(finished.ProgressIsMarquee);
        Assert.Equal(1.0, finished.Opacity);
        Assert.Equal(0.0, UdbScriptRunnerModel.FinishedUiState(runtime, autoClose: true).Opacity);
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
}
