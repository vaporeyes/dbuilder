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
        Assert.NotNull(type.GetMethod("RememberUdbScriptIgnoreVersion", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(type.GetMethod("HandleUdbScriptRunnerExceptionAsync", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(type.GetMethod("StatusKindFromUdbScript", BindingFlags.Static | BindingFlags.NonPublic));
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

    [Fact]
    public void MainWindowRemembersAcceptedUdbScriptFeatureVersionWarningsForSession()
    {
        string body = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("script.IgnoreVersion", body, StringComparison.Ordinal);
        Assert.Contains("script = RememberUdbScriptIgnoreVersion(script);", body, StringComparison.Ordinal);
        Assert.Contains("script with { IgnoreVersion = true }", body, StringComparison.Ordinal);
        Assert.Contains("_udbScriptDocker?.ApplyCurrentScript(remembered);", body, StringComparison.Ordinal);
        Assert.Contains("_udbScriptSlotAssignments = _udbScriptSlotAssignments.ToDictionary(", body, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowRecordsUdbScriptOutcomeStatusKinds()
    {
        string body = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(plan.Outcome.StatusText, StatusKindFromUdbScript(plan.Outcome.StatusKind));", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"UDBScript execution failed: {script.Name}\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("UdbScriptRunnerStatusKind.Ready => StatusHistoryKind.Ready", body, StringComparison.Ordinal);
        Assert.Contains("UdbScriptRunnerStatusKind.Warning => StatusHistoryKind.Warning", body, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowRecordsUdbScriptUtilityGuardsAsWarnings()
    {
        string body = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus($\"UDBScript is not assigned to a slot: {script.Name}\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"UDBScript folder not found: {folderPath}\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"UDBScript has no options: {script.Name}\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowRecordsUdbScriptPreflightAbortsAsWarnings()
    {
        string body = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus($\"UDBScript feature version rejected: {script.Name}\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"UDBScript source file not found: {loadedSources.MissingPath}\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"UDBScript runtime constraint aborted: {script.Name}\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }
}
