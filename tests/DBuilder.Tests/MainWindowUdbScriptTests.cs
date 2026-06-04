// ABOUTME: Tests MainWindow UDBScript runner wiring without creating an Avalonia window.
// ABOUTME: Keeps command-to-runner integration discoverable while script execution is still incomplete.

using System.Reflection;
using DBuilder.Editor;

namespace DBuilder.Tests;

public class MainWindowUdbScriptTests
{
    [Fact]
    public void MainWindowExposesUdbScriptRunnerIntegrationSurface()
    {
        Type type = typeof(MainWindow);

        Assert.NotNull(type.GetField("_udbScriptRunner", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(type.GetField("_pendingUdbScript", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(type.GetMethod("RunUdbScriptPlan", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(type.GetMethod("OpenUdbScriptRunnerWindow", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(type.GetMethod("RunUdbScriptInRunner", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(type.GetMethod("HandleUdbScriptRunnerExceptionAsync", BindingFlags.Instance | BindingFlags.NonPublic));
    }

    [Fact]
    public void MainWindowPassesSlotHotkeysIntoUdbScriptDocker()
    {
        string body = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("UdbScriptDockerModel.SlotHotkeys(_shortcutBindings)", body, StringComparison.Ordinal);
        Assert.Contains("new UdbScriptDockerWindow(", body, StringComparison.Ordinal);
    }
}
