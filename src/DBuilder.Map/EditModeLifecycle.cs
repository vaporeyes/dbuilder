// ABOUTME: Models UDB editing-manager mode transition and volatile-mode decisions.
// ABOUTME: Keeps mode switch ordering testable before full plugin and UI wiring is ported.

namespace DBuilder.Map;

public enum EditModeLifecycleStep
{
    ConstructNextMode,
    NotifyPluginsModeChanging,
    DisengageOldMode,
    ResetCursor,
    ApplyNextMode,
    EngageNextMode,
    RebindSwitchActions,
    UpdateInterface,
    DisposeOldMode,
    RedrawDisplay
}

public readonly record struct EditModeLifecycleMode(
    string Name,
    bool Volatile,
    bool Classic);

public sealed record EditModeSwitchPlan(
    IReadOnlyList<EditModeLifecycleStep> Steps,
    string? PreviousMode,
    string? PreviousStableMode,
    string? PreviousClassicMode);

public sealed record EditModeVolatilePlan(
    bool Handled,
    bool NotifyPlugins,
    bool CallCancel,
    string? ReturnMode);

public static class EditModeLifecycle
{
    public static EditModeSwitchPlan SwitchMode(
        EditModeLifecycleMode? currentMode,
        EditModeLifecycleMode? nextMode,
        string? previousStableMode,
        string? previousClassicMode)
    {
        string? updatedPreviousMode = currentMode?.Name;
        string? updatedPreviousStableMode = previousStableMode;
        string? updatedPreviousClassicMode = previousClassicMode;

        if (currentMode is { Volatile: false } stableMode)
        {
            updatedPreviousStableMode = stableMode.Name;
            if (stableMode.Classic) updatedPreviousClassicMode = stableMode.Name;
        }

        return new EditModeSwitchPlan(
            BuildSwitchSteps(currentMode, nextMode),
            updatedPreviousMode,
            updatedPreviousStableMode,
            updatedPreviousClassicMode);
    }

    public static EditModeVolatilePlan CancelVolatileMode(EditModeLifecycleMode? currentMode, bool alreadyDisengaging)
    {
        if (currentMode is not { Volatile: true } || alreadyDisengaging)
        {
            return new EditModeVolatilePlan(false, false, false, null);
        }

        return new EditModeVolatilePlan(true, true, true, null);
    }

    public static EditModeVolatilePlan DisengageVolatileMode(
        EditModeLifecycleMode? currentMode,
        bool alreadyDisengaging,
        string? previousStableMode)
    {
        if (currentMode is not { Volatile: true } || alreadyDisengaging)
        {
            return new EditModeVolatilePlan(false, false, false, null);
        }

        return new EditModeVolatilePlan(true, false, false, previousStableMode);
    }

    private static IReadOnlyList<EditModeLifecycleStep> BuildSwitchSteps(
        EditModeLifecycleMode? currentMode,
        EditModeLifecycleMode? nextMode)
    {
        List<EditModeLifecycleStep> steps = new();
        if (nextMode != null) steps.Add(EditModeLifecycleStep.ConstructNextMode);

        steps.Add(EditModeLifecycleStep.NotifyPluginsModeChanging);
        if (currentMode != null) steps.Add(EditModeLifecycleStep.DisengageOldMode);

        steps.Add(EditModeLifecycleStep.ResetCursor);
        steps.Add(EditModeLifecycleStep.ApplyNextMode);

        if (nextMode != null) steps.Add(EditModeLifecycleStep.EngageNextMode);

        steps.Add(EditModeLifecycleStep.RebindSwitchActions);
        steps.Add(EditModeLifecycleStep.UpdateInterface);

        if (currentMode != null) steps.Add(EditModeLifecycleStep.DisposeOldMode);

        steps.Add(EditModeLifecycleStep.RedrawDisplay);
        return steps;
    }
}
