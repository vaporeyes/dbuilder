// ABOUTME: Tests for LockdefsParser over LOCKDEFS lock declarations.
// ABOUTME: Covers clearlocks, messages, map colors, game labels, and key groups.

using DBuilder.IO;

namespace DBuilder.Tests;

public class LockdefsParserTests
{
    [Fact]
    public void ParsesLockMessagesColorAndKeys()
    {
        const string text = @"
clearlocks
lock 1 Doom
{
    $title ""Blue Lock""
    message ""You need a blue key.""
    remotemessage ""Remote blue lock.""
    mapcolor 0 0 255
    any { BlueCard BlueSkull }
    all { SwitchA, SwitchB }
}";

        var defs = LockdefsParser.Parse(text);

        Assert.True(defs.ClearLocks);
        var lockDef = Assert.Single(defs.Locks);
        Assert.Equal("1", lockDef.Id);
        Assert.Equal("Doom", lockDef.Game);
        Assert.Equal("Blue Lock", lockDef.Title);
        Assert.Equal("You need a blue key.", lockDef.Message);
        Assert.Equal("Remote blue lock.", lockDef.RemoteMessage);
        Assert.Equal((0, 0, 255), lockDef.MapColor);
        Assert.Equal("any", lockDef.KeyGroups[0].Mode);
        Assert.Contains("BlueSkull", lockDef.KeyGroups[0].Keys);
        Assert.Equal("all", lockDef.KeyGroups[1].Mode);
        Assert.DoesNotContain(",", lockDef.KeyGroups[1].Keys);
    }

    [Fact]
    public void SkipsUnknownLockBlocks()
    {
        const string text = @"lock Red { unknown { nested { value } } message ""red"" }";

        var defs = LockdefsParser.Parse(text);

        Assert.Equal("red", Assert.Single(defs.Locks).Message);
    }

    [Fact]
    public void ClearLocksRemovesEarlierDefinitions()
    {
        const string text = @"
lock 1 { $title ""Old Lock"" }
clearlocks
lock 2 { $title ""Replacement Lock"" }";

        var defs = LockdefsParser.Parse(text);

        var lockDef = Assert.Single(defs.Locks);
        Assert.True(defs.ClearLocks);
        Assert.Equal("2", lockDef.Id);
        Assert.Equal("Replacement Lock", lockDef.Title);
    }
}
