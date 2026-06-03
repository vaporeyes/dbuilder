// ABOUTME: Tests the editor-facing UDBScript runner window API surface.
// ABOUTME: Keeps the Avalonia runner shell discoverable without starting a UI platform.

using System.Reflection;
using DBuilder.Editor;

namespace DBuilder.Tests;

public class UdbScriptRunnerWindowTests
{
    [Fact]
    public void RunnerWindowExposesExpectedRunnerUiApi()
    {
        Type type = typeof(UdbScriptRunnerWindow);

        Assert.Equal("DBuilder.Editor.UdbScriptRunnerWindow", type.FullName);
        Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
        Assert.NotNull(type.GetEvent("CancelRequested"));
        Assert.NotNull(type.GetEvent("CloseRequested"));
        Assert.NotNull(type.GetEvent("RunScriptRequested"));
        Assert.NotNull(type.GetEvent("PauseRequested"));
        Assert.NotNull(type.GetEvent("ResumeRequested"));
        Assert.NotNull(type.GetProperty("IsRunnerRunning"));
        Assert.NotNull(type.GetProperty("IsProgressMarquee"));
        Assert.NotNull(type.GetProperty("ProgressValue"));
        Assert.NotNull(type.GetProperty("AutoClose"));
        Assert.NotNull(type.GetProperty("IsRuntimeTimerEnabled"));
        Assert.NotNull(type.GetProperty("ElapsedRuntime"));
        Assert.NotNull(type.GetProperty("CancellationToken"));
        AssertPublicInstanceMethod(type, "Start");
        AssertPublicInstanceMethod(type, "MarkRunning");
        AssertPublicInstanceMethod(type, "InvokePaused");
        AssertPublicInstanceMethod(type, "InvokePausedAsync");
        AssertPublicInstanceMethod(type, "RunAction");
        AssertPublicInstanceMethod(type, "QueryOptionsAsync");
        AssertPublicInstanceMethod(type, "ShowScriptMessageAsync");
        AssertPublicInstanceMethod(type, "ConfirmFeatureVersionAsync");
        AssertPublicInstanceMethod(type, "CheckRuntimeConstraintAsync");
        AssertPublicInstanceMethod(type, "ShowScriptErrorAsync");
        AssertPublicInstanceMethod(type, "Finish");
        AssertPublicInstanceMethod(type, "ApplyProgress");
        AssertPublicInstanceMethod(type, "ApplyStatus");
        AssertPublicInstanceMethod(type, "ApplyLog");
        AssertPublicInstanceMethod(type, "ApplyTimerTick");
        AssertPublicInstanceMethod(type, "ApplyState");
    }

    [Fact]
    public void QueryOptionsDialogExposesExpectedRuntimePromptSurface()
    {
        Type type = typeof(UdbScriptQueryOptionsDialog);

        Assert.Equal("DBuilder.Editor.UdbScriptQueryOptionsDialog", type.FullName);
        Assert.NotNull(type.GetConstructor([typeof(DBuilder.IO.UdbScriptQueryOptionsModel)]));
        Assert.True(typeof(PropertyDialog).IsAssignableFrom(type));
    }

    [Fact]
    public void ScriptOptionsDialogExposesExpectedEditorSurface()
    {
        Type type = typeof(UdbScriptOptionsDialog);

        Assert.Equal("DBuilder.Editor.UdbScriptOptionsDialog", type.FullName);
        Assert.NotNull(type.GetConstructor([typeof(IReadOnlyList<DBuilder.IO.UdbScriptOption>)]));
        Assert.NotNull(type.GetProperty("Options"));
        Assert.True(typeof(PropertyDialog).IsAssignableFrom(type));
    }

    [Fact]
    public void MessageDialogExposesExpectedRuntimePromptSurface()
    {
        Type type = typeof(UdbScriptMessageDialog);

        Assert.Equal("DBuilder.Editor.UdbScriptMessageDialog", type.FullName);
        Assert.NotNull(type.GetConstructor([typeof(DBuilder.IO.UdbScriptMessageDialogPlan)]));
        Assert.True(typeof(Avalonia.Controls.Window).IsAssignableFrom(type));
    }

    [Fact]
    public void ConfirmationDialogExposesExpectedRuntimePromptSurface()
    {
        Type type = typeof(UdbScriptConfirmationDialog);

        Assert.Equal("DBuilder.Editor.UdbScriptConfirmationDialog", type.FullName);
        Assert.NotNull(type.GetConstructor([typeof(string), typeof(string), typeof(string), typeof(string)]));
        Assert.True(typeof(Avalonia.Controls.Window).IsAssignableFrom(type));
    }

    [Fact]
    public void ScriptErrorDialogsExposeExpectedExceptionSurfaces()
    {
        Type errorType = typeof(UdbScriptErrorDialogWindow);
        Type parserType = typeof(UdbScriptParserErrorDialogWindow);

        Assert.Equal("DBuilder.Editor.UdbScriptErrorDialogWindow", errorType.FullName);
        Assert.NotNull(errorType.GetConstructor([typeof(DBuilder.IO.UdbScriptErrorDialog)]));
        Assert.True(typeof(Avalonia.Controls.Window).IsAssignableFrom(errorType));

        Assert.Equal("DBuilder.Editor.UdbScriptParserErrorDialogWindow", parserType.FullName);
        Assert.NotNull(parserType.GetConstructor([typeof(string), typeof(string)]));
        Assert.True(typeof(Avalonia.Controls.Window).IsAssignableFrom(parserType));
    }

    private static void AssertPublicInstanceMethod(Type type, string name)
        => Assert.NotNull(type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public));
}
