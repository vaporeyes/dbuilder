// ABOUTME: Parses USDF DIALOGUE text into typed conversation, page, condition, and choice models.
// ABOUTME: Uses the UniversalParser grammar because USDF is a UDMF-style key-value format.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DBuilder.IO;

public sealed record UsdfParseResult(UsdfDocument Document, int ErrorLine, string ErrorDescription)
{
    public bool Success => ErrorLine == 0 && string.IsNullOrEmpty(ErrorDescription);
}

public sealed record UsdfDocument(
    IReadOnlyList<string> Includes,
    IReadOnlyList<UsdfConversation> Conversations);

public sealed record UsdfConversation(
    int Index,
    int? Id,
    string? Actor,
    IReadOnlyList<UsdfPage> Pages);

public sealed record UsdfPage(
    int Index,
    string? Name,
    string? Panel,
    string? Voice,
    string? Dialog,
    string? Drop,
    int? Link,
    IReadOnlyList<UsdfInventoryCondition> IfItems,
    IReadOnlyList<UsdfChoice> Choices);

public sealed record UsdfInventoryCondition(string? Item, int Amount, int? Page);

public sealed record UsdfChoice(
    int Index,
    string? Text,
    IReadOnlyList<UsdfInventoryCondition> Costs,
    bool? DisplayCost,
    string? YesMessage,
    string? NoMessage,
    string? Log,
    string? GiveItem,
    int? Special,
    IReadOnlyList<int> Args,
    int? NextPage,
    bool CloseDialog);

public sealed record UsdfDialogEditorAction(
    string Id,
    string Title,
    string Category,
    string Description,
    bool AllowKeys,
    bool AllowMouse,
    bool AllowScroll);

public sealed record UsdfDialogEditorToolItem(
    string Text,
    string ActionId,
    string ImageResource);

public sealed record UsdfDialogEditorWindowState(
    int PositionX,
    int PositionY,
    int SizeWidth,
    int SizeHeight,
    int WindowState);

public sealed record UsdfDialogEditorTreeMetadata(
    string PathSeparator,
    int Indent,
    int ItemHeight,
    IReadOnlyList<string> ImageKeys);

public static class UsdfDialogueParser
{
    public static bool CanEditDialogue(GameConfiguration? config)
        => config?.MapLumpNames.ContainsKey("DIALOGUE") == true;

    public static UsdfParseResult Parse(string text)
    {
        var parser = new UniversalParser { StrictChecking = true };
        if (!parser.InputConfiguration(text))
            return new UsdfParseResult(new UsdfDocument(Array.Empty<string>(), Array.Empty<UsdfConversation>()), parser.ErrorLine, parser.ErrorDescription);

        var includes = new List<string>();
        var conversations = new List<UsdfConversation>();

        foreach (UniversalEntry entry in parser.Root)
        {
            if (entry.Key.Equals("include", StringComparison.OrdinalIgnoreCase))
            {
                if (StringValue(entry.Value) is { } include) includes.Add(include);
            }
            else if (entry.Key.Equals("conversation", StringComparison.OrdinalIgnoreCase) && entry.Value is UniversalCollection conversationBlock)
            {
                conversations.Add(ReadConversation(conversations.Count, conversationBlock));
            }
        }

        return new UsdfParseResult(new UsdfDocument(includes, conversations), 0, "");
    }

    private static UsdfConversation ReadConversation(int index, UniversalCollection block)
    {
        var pages = new List<UsdfPage>();
        foreach (UniversalEntry entry in block)
        {
            if (entry.Key.Equals("page", StringComparison.OrdinalIgnoreCase) && entry.Value is UniversalCollection pageBlock)
                pages.Add(ReadPage(pages.Count, pageBlock));
        }

        return new UsdfConversation(
            index,
            IntValue(FirstValue(block, "id")),
            StringValue(FirstValue(block, "actor")),
            pages);
    }

    private static UsdfPage ReadPage(int index, UniversalCollection block)
    {
        var ifItems = new List<UsdfInventoryCondition>();
        var choices = new List<UsdfChoice>();

        foreach (UniversalEntry entry in block)
        {
            if (entry.Key.Equals("ifitem", StringComparison.OrdinalIgnoreCase) && entry.Value is UniversalCollection conditionBlock)
                ifItems.Add(ReadCondition(conditionBlock));
            else if (entry.Key.Equals("choice", StringComparison.OrdinalIgnoreCase) && entry.Value is UniversalCollection choiceBlock)
                choices.Add(ReadChoice(choices.Count, choiceBlock));
        }

        return new UsdfPage(
            index,
            StringValue(FirstValue(block, "name")),
            StringValue(FirstValue(block, "panel")),
            StringValue(FirstValue(block, "voice")),
            StringValue(FirstValue(block, "dialog")),
            StringValue(FirstValue(block, "drop")),
            IntValue(FirstValue(block, "link")),
            ifItems,
            choices);
    }

    private static UsdfChoice ReadChoice(int index, UniversalCollection block)
    {
        var costs = new List<UsdfInventoryCondition>();
        foreach (UniversalEntry entry in block)
        {
            if (entry.Key.Equals("cost", StringComparison.OrdinalIgnoreCase) && entry.Value is UniversalCollection costBlock)
                costs.Add(ReadCondition(costBlock));
        }

        return new UsdfChoice(
            index,
            StringValue(FirstValue(block, "text")),
            costs,
            BoolValue(FirstValue(block, "displaycost")),
            StringValue(FirstValue(block, "yesmessage")),
            StringValue(FirstValue(block, "nomessage")),
            StringValue(FirstValue(block, "log")),
            StringValue(FirstValue(block, "giveitem")),
            IntValue(FirstValue(block, "special")),
            ReadArgs(block),
            IntValue(FirstValue(block, "nextpage")),
            BoolValue(FirstValue(block, "closedialog")) == true);
    }

    private static UsdfInventoryCondition ReadCondition(UniversalCollection block)
        => new(
            StringValue(FirstValue(block, "item")),
            IntValue(FirstValue(block, "amount")) ?? 0,
            IntValue(FirstValue(block, "page")));

    private static IReadOnlyList<int> ReadArgs(UniversalCollection block)
    {
        var args = new int[5];
        for (int i = 0; i < args.Length; i++) args[i] = IntValue(FirstValue(block, "arg" + i.ToString(CultureInfo.InvariantCulture))) ?? 0;
        return args;
    }

    private static object? FirstValue(UniversalCollection block, string key)
    {
        foreach (UniversalEntry entry in block)
            if (entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                return entry.Value;
        return null;
    }

    private static string? StringValue(object? value)
        => value switch
        {
            null => null,
            string s => s,
            bool b => b ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };

    private static int? IntValue(object? value)
        => value switch
        {
            int i => i,
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            double d when d >= int.MinValue && d <= int.MaxValue => (int)d,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) => i,
            _ => null,
        };

    private static bool? BoolValue(object? value)
        => value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out bool b) => b,
            _ => null,
        };
}

public static class UsdfDialogEditorModel
{
    public const string ActionId = "opendialogeditor";
    public const string MainFormTitle = "Dialog Editor";
    public const string PositionXKey = "mainwindow.positionx";
    public const string PositionYKey = "mainwindow.positiony";
    public const string SizeWidthKey = "mainwindow.sizewidth";
    public const string SizeHeightKey = "mainwindow.sizeheight";
    public const string WindowStateKey = "mainwindow.windowstate";
    public const int NormalWindowState = 0;
    public const int MinimizedWindowState = 1;
    public const int DefaultClientWidth = 942;
    public const int DefaultClientHeight = 612;
    public const int TreeWidth = 257;

    public static UsdfDialogEditorAction Action { get; } = new(
        ActionId,
        MainFormTitle,
        "view",
        "This opens the dialog editor that allows you to edit DIALOGUE conversations in your map.",
        AllowKeys: true,
        AllowMouse: true,
        AllowScroll: true);

    public static UsdfDialogEditorToolItem ToolbarButton { get; } = new(
        "Open Dialog Editor",
        ActionId,
        "Dialog.png");

    public static UsdfDialogEditorToolItem MenuItem { get; } = new(
        "Dialog Editor...",
        ActionId,
        "Dialog.png");

    public static UsdfDialogEditorTreeMetadata TreeMetadata { get; } = new(
        ".",
        22,
        18,
        ["Dialog2.png", "book_closed.png", "book_open.png", "page_user.png"]);

    public static UsdfDialogEditorWindowState ReadWindowState(
        IReadOnlyDictionary<string, object?> settings,
        UsdfDialogEditorWindowState fallback)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new UsdfDialogEditorWindowState(
            ReadInt(settings, PositionXKey, fallback.PositionX),
            ReadInt(settings, PositionYKey, fallback.PositionY),
            ReadInt(settings, SizeWidthKey, fallback.SizeWidth),
            ReadInt(settings, SizeHeightKey, fallback.SizeHeight),
            ReadInt(settings, WindowStateKey, fallback.WindowState));
    }

    public static Dictionary<string, object> WriteWindowState(UsdfDialogEditorWindowState state)
    {
        int persistedWindowState = state.WindowState == MinimizedWindowState ? NormalWindowState : state.WindowState;
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [PositionXKey] = state.PositionX,
            [PositionYKey] = state.PositionY,
            [SizeWidthKey] = state.SizeWidth,
            [SizeHeightKey] = state.SizeHeight,
            [WindowStateKey] = persistedWindowState,
        };
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> settings, string key, int fallback)
    {
        if (!settings.TryGetValue(key, out object? value)) return fallback;
        return value switch
        {
            int i => i,
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) => i,
            _ => fallback,
        };
    }
}
