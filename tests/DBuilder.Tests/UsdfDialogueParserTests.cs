// ABOUTME: Tests USDF DIALOGUE parsing over UDMF-style conversation, page, condition, and choice blocks.
// ABOUTME: Covers plugin availability gating, includes, repeated blocks, typed fields, and parse errors.

using DBuilder.IO;

namespace DBuilder.Tests;

public class UsdfDialogueParserTests
{
    [Fact]
    public void DialogEditorActionMatchesUdbActionsCfg()
    {
        UsdfDialogEditorAction action = UsdfDialogEditorModel.Action;

        Assert.Equal("opendialogeditor", action.Id);
        Assert.Equal("Dialog Editor", action.Title);
        Assert.Equal("view", action.Category);
        Assert.Equal("This opens the dialog editor that allows you to edit DIALOGUE conversations in your map.", action.Description);
        Assert.True(action.AllowKeys);
        Assert.True(action.AllowMouse);
        Assert.True(action.AllowScroll);
    }

    [Fact]
    public void DialogEditorToolItemsMatchUdbToolsForm()
    {
        Assert.Equal(new UsdfDialogEditorToolItem("Open Dialog Editor", "opendialogeditor", "Dialog.png"), UsdfDialogEditorModel.ToolbarButton);
        Assert.Equal(new UsdfDialogEditorToolItem("Dialog Editor...", "opendialogeditor", "Dialog.png"), UsdfDialogEditorModel.MenuItem);
    }

    [Fact]
    public void DialogEditorMainFormMetadataMatchesUdbDesigner()
    {
        Assert.Equal("Dialog Editor", UsdfDialogEditorModel.MainFormTitle);
        Assert.Equal(942, UsdfDialogEditorModel.DefaultClientWidth);
        Assert.Equal(612, UsdfDialogEditorModel.DefaultClientHeight);
        Assert.Equal(
            new UsdfDialogEditorWindowState(
                0,
                0,
                UsdfDialogEditorModel.DefaultClientWidth,
                UsdfDialogEditorModel.DefaultClientHeight,
                UsdfDialogEditorModel.NormalWindowState),
            UsdfDialogEditorModel.DefaultWindowState);
        Assert.Equal(257, UsdfDialogEditorModel.TreeWidth);
        Assert.Equal(".", UsdfDialogEditorModel.TreeMetadata.PathSeparator);
        Assert.Equal(22, UsdfDialogEditorModel.TreeMetadata.Indent);
        Assert.Equal(18, UsdfDialogEditorModel.TreeMetadata.ItemHeight);
        Assert.Equal(
            new[] { "Dialog2.png", "book_closed.png", "book_open.png", "page_user.png" },
            UsdfDialogEditorModel.TreeMetadata.ImageKeys);
    }

    [Fact]
    public void DialogEditorTreeBuildsRootConversationAndPageNodes()
    {
        const string text = """
        conversation
        {
            id = 7;
            actor = "Guard";
            page
            {
                name = "Greeting";
                dialog = "Hello";
            }
        }
        conversation
        {
            actor = "Vendor";
        }
        """;

        UsdfParseResult result = UsdfDialogueParser.Parse(text);

        IReadOnlyList<UsdfDialogEditorTreeNode> nodes = UsdfDialogEditorModel.BuildTree(result);

        Assert.Equal(
            new[]
            {
                new UsdfDialogEditorTreeNode("Dialog Editor", 0, "Dialog2.png"),
                new UsdfDialogEditorTreeNode("Conversation 0, id 7, actor Guard", 1, "book_open.png"),
                new UsdfDialogEditorTreeNode("Page 0, Greeting", 2, "page_user.png"),
                new UsdfDialogEditorTreeNode("Conversation 1, actor Vendor", 1, "book_closed.png"),
            },
            nodes);
    }

    [Fact]
    public void DialogEditorTreeIsEmptyForParseErrors()
    {
        UsdfParseResult result = UsdfDialogueParser.Parse("conversation { actor-name = 1; }");

        Assert.Empty(UsdfDialogEditorModel.BuildTree(result));
    }

    [Fact]
    public void DialogEditorWindowStateReadsUdbPluginSettings()
    {
        var fallback = new UsdfDialogEditorWindowState(1, 2, 300, 200, UsdfDialogEditorModel.NormalWindowState);
        var settings = new Dictionary<string, object?>
        {
            [UsdfDialogEditorModel.PositionXKey] = "10",
            [UsdfDialogEditorModel.PositionYKey] = 20,
            [UsdfDialogEditorModel.SizeWidthKey] = 640,
            [UsdfDialogEditorModel.SizeHeightKey] = "480",
            [UsdfDialogEditorModel.WindowStateKey] = 2,
        };

        UsdfDialogEditorWindowState state = UsdfDialogEditorModel.ReadWindowState(settings, fallback);

        Assert.Equal(new UsdfDialogEditorWindowState(10, 20, 640, 480, 2), state);
    }

    [Fact]
    public void DialogEditorWindowStateUsesFallbacksForMissingSettings()
    {
        var fallback = new UsdfDialogEditorWindowState(1, 2, 300, 200, UsdfDialogEditorModel.NormalWindowState);

        UsdfDialogEditorWindowState state = UsdfDialogEditorModel.ReadWindowState(new Dictionary<string, object?>(), fallback);

        Assert.Equal(fallback, state);
    }

    [Fact]
    public void DialogEditorWindowStateWritesUdbPluginSettingsAndNormalizesMinimized()
    {
        var state = new UsdfDialogEditorWindowState(10, 20, 640, 480, UsdfDialogEditorModel.MinimizedWindowState);

        Dictionary<string, object> settings = UsdfDialogEditorModel.WriteWindowState(state);

        Assert.Equal(10, settings[UsdfDialogEditorModel.PositionXKey]);
        Assert.Equal(20, settings[UsdfDialogEditorModel.PositionYKey]);
        Assert.Equal(640, settings[UsdfDialogEditorModel.SizeWidthKey]);
        Assert.Equal(480, settings[UsdfDialogEditorModel.SizeHeightKey]);
        Assert.Equal(UsdfDialogEditorModel.NormalWindowState, settings[UsdfDialogEditorModel.WindowStateKey]);
    }

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
    public void CanEditDialogueTrimsConfiguredLumpNamesLikeUdb()
    {
        var config = new GameConfiguration();
        var field = typeof(GameConfiguration).GetField(
            "mapLumpNames",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var mapLumps = Assert.IsType<Dictionary<string, MapLumpInfo>>(field?.GetValue(config));
        mapLumps[" dialogue "] = new MapLumpInfo { Name = " dialogue " };

        Assert.True(UsdfDialogueParser.CanEditDialogue(config));
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
    public void ParseWithIncludesMergesResolvedConversationsInDirectiveOrder()
    {
        const string text = """
        conversation { actor = "Root before"; }
        include = "COMMON";
        conversation { actor = "Root after"; }
        """;
        var includes = new Dictionary<string, string>
        {
            ["COMMON"] = """
            conversation { actor = "Included"; }
            """,
        };

        var result = UsdfDialogueParser.ParseWithIncludes(text, include => includes.GetValueOrDefault(include));

        Assert.True(result.Success);
        Assert.Equal(new[] { "COMMON" }, result.Document.Includes);
        Assert.Equal(new[] { "Root before", "Included", "Root after" }, result.Document.Conversations.Select(c => c.Actor));
        Assert.Equal(new[] { 0, 1, 2 }, result.Document.Conversations.Select(c => c.Index));
    }

    [Fact]
    public void ParseWithIncludesKeepsMissingIncludesWithoutFailing()
    {
        const string text = """
        include = "MISSING";
        conversation { actor = "Root"; }
        """;

        var result = UsdfDialogueParser.ParseWithIncludes(text, _ => null);

        Assert.True(result.Success);
        Assert.Equal(new[] { "MISSING" }, result.Document.Includes);
        Assert.Equal("Root", Assert.Single(result.Document.Conversations).Actor);
    }

    [Fact]
    public void ParseWithIncludesSkipsAlreadyParsedIncludes()
    {
        const string text = """
        include = "A";
        include = "A";
        conversation { actor = "Root"; }
        """;
        var includes = new Dictionary<string, string>
        {
            ["A"] = """
            include = "B";
            conversation { actor = "A"; }
            """,
            ["B"] = """
            include = "A";
            conversation { actor = "B"; }
            """,
        };

        var result = UsdfDialogueParser.ParseWithIncludes(text, include => includes.GetValueOrDefault(include));

        Assert.True(result.Success);
        Assert.Equal(new[] { "A", "B", "A", "A" }, result.Document.Includes);
        Assert.Equal(new[] { "B", "A", "Root" }, result.Document.Conversations.Select(c => c.Actor));
        Assert.Equal(new[] { 0, 1, 2 }, result.Document.Conversations.Select(c => c.Index));
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

    [Fact]
    public void ViewerModelFormatsStatusSummaryAndRows()
    {
        const string text = """
            include = "COMMON";
            conversation
            {
                id = 7;
                actor = "Guard";
                page
                {
                    name = "Greeting";
                    dialog = "Hello";
                    ifitem { item = "BlueCard"; amount = 1; page = 2; }
                    choice
                    {
                        text = "Open";
                        special = 80;
                        arg0 = 1;
                        nextpage = 2;
                        cost { item = "Coin"; amount = 3; }
                    }
                }
            }
            """;

        UsdfParseResult result = UsdfDialogueParser.Parse(text);

        Assert.Equal("DIALOGUE: OK", UsdfDialogueParser.ViewerStatus(result));
        Assert.Equal("1 include, 1 conversation, 1 page, 1 choice.", UsdfDialogueParser.ViewerSummary(result.Document));

        IReadOnlyList<UsdfConversationRow> rows = UsdfDialogueParser.ViewerRows(result);
        Assert.Equal(
            new[]
            {
                UsdfConversationRowKind.Include,
                UsdfConversationRowKind.Conversation,
                UsdfConversationRowKind.Page,
                UsdfConversationRowKind.Condition,
                UsdfConversationRowKind.Choice,
            },
            rows.Select(row => row.Kind));
        Assert.Equal(new[] { 0, 0, 1, 2, 2 }, rows.Select(row => row.Depth));
        Assert.Equal("include: COMMON", rows[0].Text);
        Assert.Equal("conversation 0: id 7 actor Guard", rows[1].Text);
        Assert.Contains("dialog \"Hello\"", rows[2].Text);
        Assert.Equal("if item: BlueCard x1, page 2", rows[3].Text);
        Assert.Contains("costs Coin x3", rows[4].Text);
    }

    [Fact]
    public void ViewerSummaryFormatsPluralCounts()
    {
        var document = new UsdfDocument(
            new[] { "a", "b" },
            new[]
            {
                new UsdfConversation(0, null, null, new[]
                {
                    new UsdfPage(0, null, null, null, null, null, null, Array.Empty<UsdfInventoryCondition>(), new[]
                    {
                        new UsdfChoice(0, null, Array.Empty<UsdfInventoryCondition>(), null, null, null, null, null, null, Array.Empty<int>(), null, false),
                        new UsdfChoice(1, null, Array.Empty<UsdfInventoryCondition>(), null, null, null, null, null, null, Array.Empty<int>(), null, false),
                    }),
                }),
                new UsdfConversation(1, null, null, new[]
                {
                    new UsdfPage(0, null, null, null, null, null, null, Array.Empty<UsdfInventoryCondition>(), Array.Empty<UsdfChoice>()),
                }),
            });

        Assert.Equal("2 includes, 2 conversations, 2 pages, 2 choices.", UsdfDialogueParser.ViewerSummary(document));
    }

    [Fact]
    public void ViewerRowsAreEmptyForParseErrors()
    {
        UsdfParseResult result = UsdfDialogueParser.Parse("conversation { actor-name = 1; }");

        Assert.Contains("DIALOGUE parse error on line", UsdfDialogueParser.ViewerStatus(result));
        Assert.Empty(UsdfDialogueParser.ViewerRows(result));
    }

    [Theory]
    [InlineData(1, "USDF: 1 conversation.")]
    [InlineData(2, "USDF: 2 conversations.")]
    public void EditorStatusFormatsSingularAndPluralConversationCounts(int conversationCount, string expected)
    {
        var conversations = Enumerable.Range(0, conversationCount)
            .Select(index => new UsdfConversation(index, null, null, Array.Empty<UsdfPage>()))
            .ToArray();
        var result = new UsdfParseResult(new UsdfDocument(Array.Empty<string>(), conversations), 0, "");

        Assert.Equal(expected, UsdfDialogueParser.EditorStatus(result));
    }

    [Fact]
    public void EditorStatusFormatsParseErrors()
    {
        var result = new UsdfParseResult(new UsdfDocument(Array.Empty<string>(), Array.Empty<UsdfConversation>()), 7, "bad token");

        Assert.Equal("USDF parse error on line 7.", UsdfDialogueParser.EditorStatus(result));
    }
}
