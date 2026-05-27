// ABOUTME: Tests GZDoom MODELDEF parsing for actor model resource discovery.
// ABOUTME: Uses synthetic MODELDEF text to verify model, skin, path, and frameindex extraction.

using System.Linq;
using DBuilder.IO;

namespace DBuilder.Tests;

public class ModeldefParserTests
{
    [Fact]
    public void ParsesModelBlockResourcesAndFrames()
    {
        const string text = @"
model ZombieMan
{
    Path ""models/monsters""
    Model 0 ""zombie.md3""
    Skin 0 ""zombie.png""
    Scale 1.0 1.0 1.0
    FrameIndex POSS A 0 12
}";

        var def = ModeldefParser.Parse(text).Single();

        Assert.Equal("ZombieMan", def.ActorName);
        Assert.Equal("models/monsters", def.Path);
        Assert.Equal(new ModeldefModel(0, "zombie.md3"), def.Models.Single());
        Assert.Equal(new ModeldefSkin(0, "zombie.png"), def.Skins.Single());
        Assert.Equal(new ModeldefFrame("POSS", "A", 0, 12), def.Frames.Single());
    }

    [Fact]
    public void ParsesMultipleModelsAndSkipsUnknownDirectives()
    {
        const string text = @"
model A
{
    Model 0 ""a.md3""
    Skin 0 ""a.png""
    Offset 1 2 3
}
model B
{
    Path ""models/""
    Model 1 ""b.md2""
    FrameIndex BOSS B 1 3
}";

        var defs = ModeldefParser.Parse(text);

        Assert.Equal(new[] { "A", "B" }, defs.Select(d => d.ActorName).ToArray());
        Assert.Equal("models", defs[1].Path);
        Assert.Equal(new ModeldefModel(1, "b.md2"), defs[1].Models.Single());
        Assert.Equal(new ModeldefFrame("BOSS", "B", 1, 3), defs[1].Frames.Single());
    }
}
