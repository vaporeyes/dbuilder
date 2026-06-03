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
        Assert.NotNull(type.GetProperty("IsRunnerRunning"));
        AssertPublicInstanceMethod(type, "Start");
        AssertPublicInstanceMethod(type, "MarkRunning");
        AssertPublicInstanceMethod(type, "Finish");
        AssertPublicInstanceMethod(type, "ApplyProgress");
        AssertPublicInstanceMethod(type, "ApplyStatus");
        AssertPublicInstanceMethod(type, "ApplyLog");
        AssertPublicInstanceMethod(type, "ApplyTimerTick");
        AssertPublicInstanceMethod(type, "ApplyState");
    }

    private static void AssertPublicInstanceMethod(Type type, string name)
        => Assert.NotNull(type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public));
}
