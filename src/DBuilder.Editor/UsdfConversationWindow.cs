// ABOUTME: Non-modal USDF conversation viewer for parsed DIALOGUE map lumps.
// ABOUTME: Presents includes, conversations, pages, inventory conditions, and choices without save-back editing.

using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class UsdfConversationWindow : Window
{
    private readonly ListBox _list = new();

    public UsdfConversationWindow(UsdfParseResult result)
    {
        Title = UsdfDialogEditorModel.MainFormTitle;
        Width = 760;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel { Margin = new Thickness(10) };
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock { Text = FormatStatus(result), FontWeight = FontWeight.Bold });
        if (result.Success)
            header.Children.Add(new TextBlock { Text = FormatCounts(result.Document) });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        foreach (Control row in BuildRows(result))
            _list.Items.Add(row);

        root.Children.Add(new ScrollViewer { Content = _list });
        Content = root;
    }

    private static string FormatStatus(UsdfParseResult result)
        => result.Success ? "DIALOGUE: OK" : $"DIALOGUE parse error on line {result.ErrorLine}: {result.ErrorDescription}";

    private static string FormatCounts(UsdfDocument document)
    {
        int pages = document.Conversations.Sum(conversation => conversation.Pages.Count);
        int choices = document.Conversations.Sum(conversation => conversation.Pages.Sum(page => page.Choices.Count));
        return $"{document.Includes.Count} include(s), {document.Conversations.Count} conversation(s), {pages} page(s), {choices} choice(s).";
    }

    private static IEnumerable<Control> BuildRows(UsdfParseResult result)
    {
        if (!result.Success) yield break;

        foreach (string include in result.Document.Includes)
            yield return Row($"include: {include}", Brushes.LightSkyBlue);

        foreach (UsdfConversation conversation in result.Document.Conversations)
        {
            yield return Row(FormatConversation(conversation), Brushes.White);
            foreach (UsdfPage page in conversation.Pages)
            {
                yield return Row("  " + FormatPage(page), Brushes.Gainsboro);
                foreach (UsdfInventoryCondition condition in page.IfItems)
                    yield return Row("    if item: " + FormatCondition(condition), Brushes.LightGray);
                foreach (UsdfChoice choice in page.Choices)
                    yield return Row("    " + FormatChoice(choice), Brushes.LightGray);
            }
        }
    }

    private static Control Row(string text, IBrush brush)
        => new TextBlock
        {
            Text = text,
            Foreground = brush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2),
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
