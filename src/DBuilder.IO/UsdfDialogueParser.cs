// ABOUTME: Parses USDF DIALOGUE text into typed conversation, page, condition, and choice models.
// ABOUTME: Uses the UniversalParser grammar because USDF is a UDMF-style key-value format.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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

public enum UsdfConversationRowKind
{
    Include,
    Conversation,
    Page,
    Condition,
    Choice,
}

public sealed record UsdfConversationRow(
    string Text,
    int Depth,
    UsdfConversationRowKind Kind);

public static class UsdfDialogueParser
{
    public static bool CanEditDialogue(GameConfiguration? config)
        => config?.MapLumpNames.ContainsKey("DIALOGUE") == true;

    public static string ViewerStatus(UsdfParseResult result)
        => result.Success ? "DIALOGUE: OK" : $"DIALOGUE parse error on line {result.ErrorLine}: {result.ErrorDescription}";

    public static string ViewerSummary(UsdfDocument document)
    {
        int pages = document.Conversations.Sum(conversation => conversation.Pages.Count);
        int choices = document.Conversations.Sum(conversation => conversation.Pages.Sum(page => page.Choices.Count));
        return $"{document.Includes.Count} include(s), {document.Conversations.Count} conversation(s), {pages} page(s), {choices} choice(s).";
    }

    public static IReadOnlyList<UsdfConversationRow> ViewerRows(UsdfParseResult result)
    {
        if (!result.Success) return Array.Empty<UsdfConversationRow>();

        var rows = new List<UsdfConversationRow>();
        foreach (string include in result.Document.Includes)
            rows.Add(new UsdfConversationRow($"include: {include}", 0, UsdfConversationRowKind.Include));

        foreach (UsdfConversation conversation in result.Document.Conversations)
        {
            rows.Add(new UsdfConversationRow(FormatConversation(conversation), 0, UsdfConversationRowKind.Conversation));
            foreach (UsdfPage page in conversation.Pages)
            {
                rows.Add(new UsdfConversationRow(FormatPage(page), 1, UsdfConversationRowKind.Page));
                foreach (UsdfInventoryCondition condition in page.IfItems)
                    rows.Add(new UsdfConversationRow("if item: " + FormatCondition(condition), 2, UsdfConversationRowKind.Condition));
                foreach (UsdfChoice choice in page.Choices)
                    rows.Add(new UsdfConversationRow(FormatChoice(choice), 2, UsdfConversationRowKind.Choice));
            }
        }

        return rows;
    }

    public static UsdfParseResult Parse(string text)
        => ParseInternal(text, includeResolver: null);

    public static UsdfParseResult ParseWithIncludes(string text, Func<string, string?> includeResolver)
    {
        ArgumentNullException.ThrowIfNull(includeResolver);
        return ParseInternal(text, includeResolver);
    }

    private static UsdfParseResult ParseInternal(string text, Func<string, string?>? includeResolver)
        => ParseInternal(text, includeResolver, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private static UsdfParseResult ParseInternal(
        string text,
        Func<string, string?>? includeResolver,
        HashSet<string> parsedIncludes)
    {
        var parser = new UniversalParser { StrictChecking = true };
        if (!parser.InputConfiguration(text))
            return new UsdfParseResult(new UsdfDocument(Array.Empty<string>(), Array.Empty<UsdfConversation>()), parser.ErrorLine, parser.ErrorDescription);

        var includes = new List<string>();
        var conversations = new List<UsdfConversation>();

        UsdfParseResult? includeError = ReadEntries(
            parser.Root,
            includeResolver,
            parsedIncludes,
            includes,
            conversations);
        if (includeError is not null) return includeError;

        return new UsdfParseResult(new UsdfDocument(includes, conversations), 0, "");
    }

    private static UsdfParseResult? ReadEntries(
        UniversalCollection root,
        Func<string, string?>? includeResolver,
        HashSet<string> parsedIncludes,
        List<string> includes,
        List<UsdfConversation> conversations)
    {
        foreach (UniversalEntry entry in root)
        {
            if (entry.Key.Equals("include", StringComparison.OrdinalIgnoreCase))
            {
                if (StringValue(entry.Value) is { } include)
                {
                    includes.Add(include);
                    if (includeResolver is not null && parsedIncludes.Add(include))
                    {
                        string? includeText = includeResolver(include);
                        if (!string.IsNullOrEmpty(includeText))
                        {
                            UsdfParseResult parsed = ParseInternal(includeText, includeResolver, parsedIncludes);
                            if (!parsed.Success) return parsed;
                            includes.AddRange(parsed.Document.Includes);
                            foreach (UsdfConversation conversation in parsed.Document.Conversations)
                                conversations.Add(conversation with { Index = conversations.Count });
                        }
                    }
                }
            }
            else if (entry.Key.Equals("conversation", StringComparison.OrdinalIgnoreCase) && entry.Value is UniversalCollection conversationBlock)
            {
                conversations.Add(ReadConversation(conversations.Count, conversationBlock));
            }
        }

        return null;
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

    private static string FormatConversation(UsdfConversation conversation)
    {
        string id = conversation.Id is int value ? $" id {value}" : "";
        string actor = string.IsNullOrWhiteSpace(conversation.Actor) ? "" : $" actor {conversation.Actor}";
        return $"conversation {conversation.Index}:{id}{actor}";
    }

    private static string FormatPage(UsdfPage page)
    {
        var parts = new List<string> { $"page {page.Index}" };
        Add(parts, "name", page.Name);
        Add(parts, "panel", page.Panel);
        Add(parts, "voice", page.Voice);
        Add(parts, "dialog", page.Dialog);
        Add(parts, "drop", page.Drop);
        if (page.Link is int link) parts.Add($"link {link}");
        return string.Join(", ", parts);
    }

    private static string FormatChoice(UsdfChoice choice)
    {
        var parts = new List<string> { $"choice {choice.Index}" };
        Add(parts, "text", choice.Text);
        Add(parts, "yes", choice.YesMessage);
        Add(parts, "no", choice.NoMessage);
        Add(parts, "log", choice.Log);
        Add(parts, "give", choice.GiveItem);
        if (choice.Special is int special) parts.Add($"special {special}");
        if (choice.Args.Any(arg => arg != 0)) parts.Add("args " + string.Join(", ", choice.Args));
        if (choice.NextPage is int nextPage) parts.Add($"next page {nextPage}");
        if (choice.CloseDialog) parts.Add("close dialog");
        if (choice.Costs.Count > 0) parts.Add("costs " + string.Join("; ", choice.Costs.Select(FormatCondition)));
        return string.Join(", ", parts);
    }

    private static string FormatCondition(UsdfInventoryCondition condition)
    {
        string item = string.IsNullOrWhiteSpace(condition.Item) ? "(none)" : condition.Item;
        string page = condition.Page is int value ? $", page {value}" : "";
        return $"{item} x{condition.Amount}{page}";
    }

    private static void Add(List<string> parts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) parts.Add($"{label} \"{value}\"");
    }
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
