// ABOUTME: Tests UDB-style script type inference used by script editor resources.
// ABOUTME: Covers MODELDEF, ACS, DECORATE, ZScript, and unknown script text detection.

using DBuilder.IO;

namespace DBuilder.Tests;

public class ScriptTypeDetectorTests
{
    [Fact]
    public void DetectsModeldefModelBlocks()
    {
        const string text = """
            // model definitions may start after comments
            model Zombie
            {
                model 0 "zombie.md3"
            }
            """;

        Assert.Equal(ScriptType.ModelDef, ScriptTypeDetector.Detect(text));
    }

    [Fact]
    public void DetectsAcsScriptBlocks()
    {
        const string text = """
            #library "TEST"
            script 1 OPEN
            {
                terminate;
            }
            """;

        Assert.Equal(ScriptType.Acs, ScriptTypeDetector.Detect(text));
    }

    [Theory]
    [InlineData("script 1 (void) { }")]
    [InlineData("script \"OpenDoor\" (int tid, int speed) { }")]
    [InlineData("script 2 OPEN (void) { }")]
    public void DetectsAcsScriptHeadersWithArguments(string text)
    {
        Assert.Equal(ScriptType.Acs, ScriptTypeDetector.Detect(text));
    }

    [Fact]
    public void AcsDetectionStopsAtStatementTerminators()
    {
        const string text = "script = \"not a script\"; {";

        Assert.Equal(ScriptType.Unknown, ScriptTypeDetector.Detect(text));
    }

    [Theory]
    [InlineData("actor Imp { }")]
    [InlineData("actor FastImp : DoomImp { }")]
    [InlineData("actor NewImp replaces DoomImp { }")]
    [InlineData("actor NumberedImp : DoomImp 12345 { }")]
    public void DetectsDecorateActorHeaders(string text)
    {
        Assert.Equal(ScriptType.Decorate, ScriptTypeDetector.Detect(text));
    }

    [Theory]
    [InlineData("class CustomActor : Actor { }")]
    [InlineData("class CustomActor replaces DoomImp { }")]
    [InlineData("class Forward;")]
    [InlineData("struct Vec2 { double x; }")]
    [InlineData("enum DamageFlags { A, B }")]
    [InlineData("extend class DoomImp { }")]
    public void DetectsZScriptHeaders(string text)
    {
        Assert.Equal(ScriptType.ZScript, ScriptTypeDetector.Detect(text));
    }

    [Fact]
    public void ReturnsUnknownForPlainText()
    {
        const string text = """
            This is a plain text note.
            It has braces { } but no script header.
            """;

        Assert.Equal(ScriptType.Unknown, ScriptTypeDetector.Detect(text));
    }
}
