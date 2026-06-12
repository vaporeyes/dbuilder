// ABOUTME: Tests ZDoom SNDINFO parsing for logical sound mappings and aliases.
// ABOUTME: Uses synthetic text with comments, quoted paths, and unsupported directives.

using DBuilder.IO;

namespace DBuilder.Tests;

public class SndInfoParserTests
{
    [Fact]
    public void ParsesSoundMappingsAndAliases()
    {
        const string text = @"
// logical name to lump path
weapons/pistol ""sounds/pistol.wav""
misc/secret DSSECRET
$alias player/death misc/death
# comment
misc/death ""sounds/death.ogg""";

        var info = SndInfoParser.Parse(text);

        Assert.Equal("sounds/pistol.wav", info.Sounds["weapons/pistol"]);
        Assert.Equal("DSSECRET", info.Sounds["misc/secret"]);
        Assert.Equal("sounds/death.ogg", info.Sounds["misc/death"]);
        Assert.Equal("misc/death", info.Aliases["player/death"]);
    }

    [Fact]
    public void MixedSoundAssignmentFormatsStopParsingLikeUdb()
    {
        const string oldThenNew = @"
world/old DSOLD
world/new = DSNEW
world/later DSLATE";
        const string newThenOld = @"
world/new = DSNEW
world/old DSOLD
world/later = DSLATE";

        var oldFirst = SndInfoParser.Parse(oldThenNew);
        var newFirst = SndInfoParser.Parse(newThenOld);

        Assert.Equal("DSOLD", oldFirst.Sounds["world/old"]);
        Assert.False(oldFirst.Sounds.ContainsKey("world/new"));
        Assert.False(oldFirst.Sounds.ContainsKey("world/later"));
        Assert.Equal("DSNEW", newFirst.Sounds["world/new"]);
        Assert.False(newFirst.Sounds.ContainsKey("world/old"));
        Assert.False(newFirst.Sounds.ContainsKey("world/later"));
    }

    [Fact]
    public void SkipsUnsupportedDollarDirectives()
    {
        const string text = @"
$random misc/random { sound/a sound/b }
world/drip drip1
$volume world/drip 0.5";

        var info = SndInfoParser.Parse(text);

        Assert.Equal("drip1", info.Sounds["world/drip"]);
        Assert.Empty(info.Aliases);
    }

    [Fact]
    public void ParsesRandomGroups()
    {
        const string text = @"$random misc/random { sound/a sound/b }";

        var info = SndInfoParser.Parse(text);

        Assert.Equal(new[] { "sound/a", "sound/b" }, info.RandomGroups["misc/random"]);
    }

    [Fact]
    public void InvalidRandomGroupsStopParsingLikeUdb()
    {
        const string text = @"
world/before DSBEFORE
$random misc/random { sound/a misc/random }
world/after DSAFTER
$random misc/valid { sound/a sound/b }";

        var info = SndInfoParser.Parse(text);

        Assert.Equal("DSBEFORE", info.Sounds["world/before"]);
        Assert.False(info.RandomGroups.ContainsKey("misc/random"));
        Assert.False(info.Sounds.ContainsKey("world/after"));
        Assert.False(info.RandomGroups.ContainsKey("misc/valid"));
    }

    [Fact]
    public void ParsesMultilineRandomGroups()
    {
        const string text = """
$random misc/random
{
    sound/a
    sound/b // inline comment
}
world/drip drip1
""";

        var info = SndInfoParser.Parse(text);

        Assert.Equal(new[] { "sound/a", "sound/b" }, info.RandomGroups["misc/random"]);
        Assert.Equal("drip1", info.Sounds["world/drip"]);
    }

    [Fact]
    public void ParsesRandomGroupBracesWithoutWhitespace()
    {
        const string text = "$random misc/random {sound/a sound/b}";

        var info = SndInfoParser.Parse(text);

        Assert.Equal(new[] { "sound/a", "sound/b" }, info.RandomGroups["misc/random"]);
    }

    [Fact]
    public void AppliesBaseGameConditionals()
    {
        const string text = @"
$ifdoom
world/secret DSSECRET
$endif
$ifhexen
world/secret HEXSECRET
$endif";

        var doom = SndInfoParser.Parse(text, TerrainBaseGame.Doom);
        var hexen = SndInfoParser.Parse(text, TerrainBaseGame.Hexen);
        var all = SndInfoParser.Parse(text);

        Assert.Equal("DSSECRET", doom.Sounds["world/secret"]);
        Assert.Equal("HEXSECRET", hexen.Sounds["world/secret"]);
        Assert.Equal("HEXSECRET", all.Sounds["world/secret"]);
    }

    [Fact]
    public void ParsesAmbientSoundsWithDefaultEditorRadii()
    {
        const string text = """
world/wind DSWIND
$ambient 4 world/wind point 0.5 continuous 0.75
""";

        var info = SndInfoParser.Parse(text);
        AmbientSoundInfo ambient = info.AmbientSounds[4];

        Assert.Equal("world/wind", ambient.SoundName);
        Assert.Equal(AmbientSoundType.Point, ambient.Type);
        Assert.Equal(AmbientSoundMode.Continuous, ambient.Mode);
        Assert.Equal(0.75, ambient.Volume);
        Assert.Equal(0.5, ambient.Attenuation);
        Assert.Equal(200.0, ambient.MinimumRadius);
        Assert.Equal(1200.0, ambient.MaximumRadius);
    }

    [Fact]
    public void AmbientSoundRadiiUseAttenuationAndLinearRolloff()
    {
        const string text = """
world/drip DSDRIP
$attenuation world/drip 2
$rolloff world/drip linear 100 500
$ambient 9 world/drip random 1 3 0.25
""";

        var info = SndInfoParser.Parse(text);
        AmbientSoundInfo ambient = info.AmbientSounds[9];

        Assert.Equal(AmbientSoundMode.Random, ambient.Mode);
        Assert.Equal(1.0, ambient.SecondsMin);
        Assert.Equal(3.0, ambient.SecondsMax);
        Assert.Equal(50.0, ambient.MinimumRadius);
        Assert.Equal(250.0, ambient.MaximumRadius);
    }

    [Fact]
    public void AmbientSoundRadiiUseLogRolloffFactor()
    {
        const string text = """
world/hum DSHUM
$rolloff world/hum log 80 3
$ambient 12 world/hum periodic 7 1
""";

        var info = SndInfoParser.Parse(text);
        AmbientSoundInfo ambient = info.AmbientSounds[12];

        Assert.Equal(AmbientSoundMode.Periodic, ambient.Mode);
        Assert.Equal(7.0, ambient.Seconds);
        Assert.Equal(80.0, ambient.MinimumRadius);
        Assert.Equal(320.0, ambient.MaximumRadius);
    }

    [Fact]
    public void AmbientSoundRadiiResolveAliasesAndRandomGroups()
    {
        const string text = """
world/source DSSRC
$rolloff world/source 64 256
$alias world/alias world/source
$random world/random { world/alias world/other }
$ambient 14 world/random surround continuous 1
""";

        var info = SndInfoParser.Parse(text);
        AmbientSoundInfo ambient = info.AmbientSounds[14];

        Assert.Equal(AmbientSoundType.Surround, ambient.Type);
        Assert.Equal(64.0, ambient.MinimumRadius);
        Assert.Equal(256.0, ambient.MaximumRadius);
    }

    [Fact]
    public void AmbientSoundsHonorBaseGameIndexLimits()
    {
        const string text = """
world/wind DSWIND
$ambient 65 world/wind continuous 1
""";

        var doom = SndInfoParser.Parse(text, TerrainBaseGame.Doom);
        var hexen = SndInfoParser.Parse(text, TerrainBaseGame.Hexen);

        Assert.False(doom.AmbientSounds.ContainsKey(65));
        Assert.True(hexen.AmbientSounds.ContainsKey(65));
    }

    [Fact]
    public void AmbientSoundsSkipUndefinedSounds()
    {
        const string text = """
$rolloff world/missing 64 256
$ambient 7 world/missing continuous 1
""";

        var info = SndInfoParser.Parse(text);

        Assert.False(info.AmbientSounds.ContainsKey(7));
    }
}
