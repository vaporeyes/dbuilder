// ABOUTME: Tests USDF DIALOGUE parsing over UDMF-style conversation, page, condition, and choice blocks.
// ABOUTME: Covers plugin availability gating, includes, repeated blocks, typed fields, and parse errors.

using DBuilder.IO;

namespace DBuilder.Tests;

public class UsdfDialogueParserTests
{
    [Fact]
    public void CanEditDialogueMatchesConfiguredDialogueMapLump()
    {
        var config = GameConfiguration.FromText("""
        maplumpnames
        {
            ~MAP { required = true; blindcopy = true; }
            THINGS { required = true; }
            DIALOGUE { required = false; script = "ZDoom_USDF.cfg"; }
        }
        """);
        var missing = GameConfiguration.FromText("""
        maplumpnames
        {
            ~MAP { required = true; blindcopy = true; }
            THINGS { required = true; }
        }
        """);

        Assert.True(UsdfDialogueParser.CanEditDialogue(config));
        Assert.False(UsdfDialogueParser.CanEditDialogue(missing));
        Assert.False(UsdfDialogueParser.CanEditDialogue(null));
    }

    [Fact]
    public void ParseReadsConversationsPagesConditionsAndChoices()
    {
        const string text = """
        include = "COMMON";

        conversation
        {
            id = 7;
            actor = 3004;

            page
            {
                name = "Oracle";
                panel = "PANEL01";
                voice = "VOX001";
                dialog = "Bring the chalice.";
                drop = 92;
                link = 2;

                ifitem
                {
                    item = 112;
                    amount = 1;
                }

                choice
                {
                    text = "I have it.";
                    cost
                    {
                        item = 112;
                        amount = 1;
                    }
                    displaycost = false;
                    yesmessage = "Accepted";
                    nomessage = "Denied";
                    log = "LOG001";
                    giveitem = 200;
                    special = 80;
                    arg0 = 1;
                    arg1 = 2;
                    arg2 = 3;
                    arg3 = 4;
                    arg4 = 5;
                    nextpage = 3;
                    closedialog = true;
                }
            }
        }
        """;

        var result = UsdfDialogueParser.Parse(text);

        Assert.True(result.Success);
        Assert.Equal("COMMON", Assert.Single(result.Document.Includes));
        var conversation = Assert.Single(result.Document.Conversations);
        Assert.Equal(0, conversation.Index);
        Assert.Equal(7, conversation.Id);
        Assert.Equal("3004", conversation.Actor);

        var page = Assert.Single(conversation.Pages);
        Assert.Equal(0, page.Index);
        Assert.Equal("Oracle", page.Name);
        Assert.Equal("PANEL01", page.Panel);
        Assert.Equal("VOX001", page.Voice);
        Assert.Equal("Bring the chalice.", page.Dialog);
        Assert.Equal("92", page.Drop);
        Assert.Equal(2, page.Link);
        Assert.Equal(new UsdfInventoryCondition("112", 1, null), Assert.Single(page.IfItems));

        var choice = Assert.Single(page.Choices);
        Assert.Equal(0, choice.Index);
        Assert.Equal("I have it.", choice.Text);
        Assert.Equal(new UsdfInventoryCondition("112", 1, null), Assert.Single(choice.Costs));
        Assert.False(choice.DisplayCost);
        Assert.Equal("Accepted", choice.YesMessage);
        Assert.Equal("Denied", choice.NoMessage);
        Assert.Equal("LOG001", choice.Log);
        Assert.Equal("200", choice.GiveItem);
        Assert.Equal(80, choice.Special);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, choice.Args);
        Assert.Equal(3, choice.NextPage);
        Assert.True(choice.CloseDialog);
    }

    [Fact]
    public void ParseKeepsRepeatedConversationsPagesAndChoicesInOrder()
    {
        const string text = """
        conversation
        {
            actor = "Vendor";
            page
            {
                dialog = "First";
                choice { text = "One"; }
                choice { text = "Two"; }
            }
            page { dialog = "Second"; }
        }
        conversation { actor = "Guard"; }
        """;

        var result = UsdfDialogueParser.Parse(text);

        Assert.True(result.Success);
        Assert.Equal(2, result.Document.Conversations.Count);
        Assert.Equal("Vendor", result.Document.Conversations[0].Actor);
        Assert.Equal("Guard", result.Document.Conversations[1].Actor);
        Assert.Equal(2, result.Document.Conversations[0].Pages.Count);
        Assert.Equal(0, result.Document.Conversations[0].Pages[0].Index);
        Assert.Equal(1, result.Document.Conversations[0].Pages[1].Index);
        Assert.Equal("One", result.Document.Conversations[0].Pages[0].Choices[0].Text);
        Assert.Equal("Two", result.Document.Conversations[0].Pages[0].Choices[1].Text);
    }

    [Fact]
    public void ParseReportsUniversalParserErrors()
    {
        var result = UsdfDialogueParser.Parse("conversation { actor-name = 1; }");

        Assert.False(result.Success);
        Assert.True(result.ErrorLine > 0);
        Assert.Contains("Invalid characters in key name", result.ErrorDescription);
        Assert.Empty(result.Document.Conversations);
    }
}
