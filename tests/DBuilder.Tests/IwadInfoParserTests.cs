// ABOUTME: Tests for IwadInfoParser over GZDoom IWADINFO declarations.
// ABOUTME: Covers assignment parsing, quoted values, semicolons, and unknown block skipping.

using DBuilder.IO;

namespace DBuilder.Tests;

public class IwadInfoParserTests
{
    [Fact]
    public void ParsesIwadMetadataFields()
    {
        const string text = @"
unknown { ignored true }
IWad
{
    Name = ""Doom II"";
    AutoName = ""doom2.wad"";
    Game = ""Doom"";
    Config = ""Doom_Doom2"";
    MapInfo = ""mapinfo/doom2.txt"";
}";

        var info = IwadInfoParser.Parse(text);

        var iwad = Assert.Single(info.Iwads);
        Assert.Equal("Doom II", iwad.Name);
        Assert.Equal("doom2.wad", iwad.AutoName);
        Assert.Equal("Doom", iwad.Game);
        Assert.Equal("Doom_Doom2", iwad.Config);
        Assert.Equal("mapinfo/doom2.txt", iwad.MapInfo);
    }

    [Fact]
    public void PreservesUnknownFields()
    {
        const string text = @"IWad { LoadLights = ""lights.pk3""; }";

        var iwad = Assert.Single(IwadInfoParser.Parse(text).Iwads);

        Assert.Equal("lights.pk3", iwad.Fields["loadlights"]);
    }

    [Fact]
    public void MissingIwadBodyStopsParsingLikeUdb()
    {
        const string text = @"
IWad { Name = ""Before""; }
IWad
IWad { Name = ""After""; }";

        var iwad = Assert.Single(IwadInfoParser.Parse(text).Iwads);

        Assert.Equal("Before", iwad.Name);
    }
}
