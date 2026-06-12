// ABOUTME: Tests UDB editing-manager transition ordering and volatile-mode decisions.
// ABOUTME: Covers previous mode tracking without requiring the Avalonia editor shell.

using DBuilder.Map;

namespace DBuilder.Tests;

public class EditModeLifecycleTests
{
    [Fact]
    public void SwitchModeTracksPreviousStableAndClassicModes()
    {
        EditModeLifecycleMode current = new("LinedefsMode", Volatile: false, Classic: true);
        EditModeLifecycleMode next = new("ThingsMode", Volatile: false, Classic: true);

        EditModeSwitchPlan plan = EditModeLifecycle.SwitchMode(
            current,
            next,
            previousStableMode: "VerticesMode",
            previousClassicMode: "VerticesMode");

        Assert.Equal("LinedefsMode", plan.PreviousMode);
        Assert.Equal("LinedefsMode", plan.PreviousStableMode);
        Assert.Equal("LinedefsMode", plan.PreviousClassicMode);
        Assert.Equal(
            new[]
            {
                EditModeLifecycleStep.ConstructNextMode,
                EditModeLifecycleStep.NotifyPluginsModeChanging,
                EditModeLifecycleStep.DisengageOldMode,
                EditModeLifecycleStep.ResetCursor,
                EditModeLifecycleStep.ApplyNextMode,
                EditModeLifecycleStep.EngageNextMode,
                EditModeLifecycleStep.RebindSwitchActions,
                EditModeLifecycleStep.UpdateInterface,
                EditModeLifecycleStep.DisposeOldMode,
                EditModeLifecycleStep.RedrawDisplay
            },
            plan.Steps);
    }

    [Fact]
    public void SwitchModeKeepsPreviousStableAndClassicModesWhenLeavingVolatileMode()
    {
        EditModeLifecycleMode current = new("FindMode", Volatile: true, Classic: false);
        EditModeLifecycleMode next = new("SectorsMode", Volatile: false, Classic: true);

        EditModeSwitchPlan plan = EditModeLifecycle.SwitchMode(
            current,
            next,
            previousStableMode: "ThingsMode",
            previousClassicMode: "ThingsMode");

        Assert.Equal("FindMode", plan.PreviousMode);
        Assert.Equal("ThingsMode", plan.PreviousStableMode);
        Assert.Equal("ThingsMode", plan.PreviousClassicMode);
    }

    [Fact]
    public void SwitchModeOmitsOldAndNextModeHooksWhenStoppingFromNoMode()
    {
        EditModeSwitchPlan plan = EditModeLifecycle.SwitchMode(
            currentMode: null,
            nextMode: null,
            previousStableMode: "ThingsMode",
            previousClassicMode: "ThingsMode");

        Assert.Null(plan.PreviousMode);
        Assert.Equal("ThingsMode", plan.PreviousStableMode);
        Assert.Equal("ThingsMode", plan.PreviousClassicMode);
        Assert.Equal(
            new[]
            {
                EditModeLifecycleStep.NotifyPluginsModeChanging,
                EditModeLifecycleStep.ResetCursor,
                EditModeLifecycleStep.ApplyNextMode,
                EditModeLifecycleStep.RebindSwitchActions,
                EditModeLifecycleStep.UpdateInterface,
                EditModeLifecycleStep.RedrawDisplay
            },
            plan.Steps);
    }

    [Fact]
    public void VolatileCancelCallsPluginCancelAndModeCancel()
    {
        EditModeLifecycleMode current = new("DrawGeometryMode", Volatile: true, Classic: false);

        EditModeVolatilePlan plan = EditModeLifecycle.CancelVolatileMode(current, alreadyDisengaging: false);

        Assert.True(plan.Handled);
        Assert.True(plan.NotifyPlugins);
        Assert.True(plan.CallCancel);
        Assert.Null(plan.ReturnMode);
    }

    [Fact]
    public void VolatileDisengageReturnsToPreviousStableModeWithoutCancel()
    {
        EditModeLifecycleMode current = new("DrawGeometryMode", Volatile: true, Classic: false);

        EditModeVolatilePlan plan = EditModeLifecycle.DisengageVolatileMode(
            current,
            alreadyDisengaging: false,
            previousStableMode: "LinedefsMode");

        Assert.True(plan.Handled);
        Assert.False(plan.NotifyPlugins);
        Assert.False(plan.CallCancel);
        Assert.Equal("LinedefsMode", plan.ReturnMode);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void VolatileHelpersIgnoreStableOrAlreadyDisengagingModes(bool volatileMode, bool alreadyDisengaging)
    {
        EditModeLifecycleMode current = new("LinedefsMode", volatileMode, Classic: true);

        EditModeVolatilePlan cancel = EditModeLifecycle.CancelVolatileMode(current, alreadyDisengaging);
        EditModeVolatilePlan disengage = EditModeLifecycle.DisengageVolatileMode(
            current,
            alreadyDisengaging,
            previousStableMode: "VerticesMode");

        Assert.False(cancel.Handled);
        Assert.False(disengage.Handled);
    }
}
