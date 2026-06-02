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

public static class UdbScriptActions
{
    public const string CategoryId = "udbscript";
    public const string CategoryTitle = "Scripting";
    public const int ScriptSlotCount = 30;

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

    public static IReadOnlyList<EditorCommandDescriptor> CommandDescriptors { get; } = All
        .Select(action => new EditorCommandDescriptor(
            $"window.{action.Id}",
            action.Title,
            "Menu",
            EditorCommandScope.Window,
            AllowKeys: action.AllowKeys,
            AllowMouse: action.AllowMouse,
            AllowScroll: action.AllowScroll))
        .ToArray();
}
