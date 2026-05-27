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
}
