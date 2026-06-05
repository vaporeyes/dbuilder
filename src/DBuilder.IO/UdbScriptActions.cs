// ABOUTME: Models UDBScript action metadata that mirrors the upstream plugin action surface.
// ABOUTME: Keeps script execution command ids and slot action metadata stable for shortcut binding.

namespace DBuilder.IO;

public sealed record UdbScriptActionDescriptor(
    string Id,
    string Title,
    string Category,
    string Description,
    bool AllowKeys,
    bool AllowMouse,
    bool AllowScroll);

public sealed record UdbScriptExecutionPlan(
    bool ShouldRun,
    UdbScriptInfo? Script,
    int Slot);

public static class UdbScriptActions
{
    public const string CategoryId = "udbscript";
    public const string CategoryTitle = "Scripting";
    public const int ScriptSlotCount = 30;

    public static UdbScriptActionDescriptor Scripts { get; } = new(
        "udbscripts",
        "Scripts",
        CategoryId,
        "Opens the script browser",
        AllowKeys: true,
        AllowMouse: true,
        AllowScroll: true);

    public static UdbScriptActionDescriptor Execute { get; } = new(
        "udbscriptexecute",
        "Execute Script",
        CategoryId,
        "Executes a script",
        AllowKeys: true,
        AllowMouse: true,
        AllowScroll: true);

    public static IReadOnlyList<UdbScriptActionDescriptor> Slots { get; } = Enumerable
        .Range(1, ScriptSlotCount)
        .Select(slot => new UdbScriptActionDescriptor(
            $"udbscriptexecuteslot{slot}",
            $"Execute Script Slot {slot}",
            CategoryId,
            $"execute script in slot {slot}",
            AllowKeys: true,
            AllowMouse: true,
            AllowScroll: true))
        .ToArray();

    public static IReadOnlyList<UdbScriptActionDescriptor> All { get; } = new[] { Execute }
        .Concat(Slots)
        .ToArray();

    public static IReadOnlyList<EditorCommandDescriptor> CommandDescriptors { get; } = new[] { Scripts }
        .Concat(All)
        .Select(action => new EditorCommandDescriptor(
            $"window.{action.Id}",
            action.Title,
            "Menu",
            EditorCommandScope.Window,
            AllowKeys: action.AllowKeys,
            AllowMouse: action.AllowMouse,
            AllowScroll: action.AllowScroll,
            Category: CategoryTitle))
        .ToArray();

    public static UdbScriptExecutionPlan ExecuteCurrentPlan(UdbScriptInfo? currentScript)
        => currentScript is null
            ? new UdbScriptExecutionPlan(false, null, 0)
            : new UdbScriptExecutionPlan(true, currentScript, 0);

    public static UdbScriptExecutionPlan ExecuteSlotPlan(
        string actionName,
        IReadOnlyDictionary<int, UdbScriptInfo?> slotAssignments)
    {
        int slot = SlotFromActionName(actionName);
        if (slot == 0)
            return new UdbScriptExecutionPlan(false, null, 0);

        if (!slotAssignments.TryGetValue(slot, out UdbScriptInfo? script) || script is null)
            return new UdbScriptExecutionPlan(false, null, slot);

        return new UdbScriptExecutionPlan(true, script, slot);
    }

    public static int SlotFromActionName(string actionName)
    {
        int index = actionName.Length - 1;
        while (index >= 0 && char.IsDigit(actionName[index]))
            index--;

        if (index == actionName.Length - 1)
            return 0;

        string slotText = actionName[(index + 1)..];
        return int.TryParse(slotText, out int slot) ? slot : 0;
    }
}
