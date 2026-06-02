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
