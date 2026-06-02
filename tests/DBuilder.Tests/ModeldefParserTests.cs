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
    SurfaceSkin 0 2 ""zombie_alt.png""
    Scale 1.0 1.0 1.0
    FrameIndex POSS A 0 12
    Frame POSS B 0 ""Run01""
}";

        var def = ModeldefParser.Parse(text).Single();

        Assert.Equal("ZombieMan", def.ActorName);
        Assert.Equal("models/monsters", def.Path);
        Assert.Equal(new ModeldefModel(0, "zombie.md3"), def.Models.Single());
        Assert.Equal(new ModeldefSkin(0, "zombie.png"), def.Skins.Single());
        Assert.Equal(new ModeldefSurfaceSkin(0, 2, "zombie_alt.png"), def.SurfaceSkins.Single());
        Assert.Equal(new ModeldefFrame("POSS", "A", 0, 12), def.Frames[0]);
        Assert.Equal(new ModeldefFrame("POSS", "B", 0, 0, "Run01"), def.Frames[1]);
    }

    [Fact]
    public void ParsesTransformAndActorOrientationMetadata()
    {
        const string text = @"
model TransformThing
{
    Model 0 ""thing.md3""
    Scale 1.5 2.5 3.5
    Offset -1 2 -3
    ZOffset 9
    AngleOffset 45
    PitchOffset -10
    RollOffset 25
    Rotation-Center 4 5 6
    UseActorPitch
    UseActorRoll
    Userotationcenter
}";

        var def = ModeldefParser.Parse(text).Single();

        Assert.Equal(new ModeldefVector(2.5f, 1.5f, 3.5f), def.Scale);
        Assert.Equal(new ModeldefVector(-1.0f, 2.0f, 9.0f), def.Offset);
        Assert.Equal(new ModeldefVector(4.0f, 5.0f, 6.0f), def.RotationCenter);
        Assert.Equal(45.0f, def.AngleOffset);
        Assert.Equal(-10.0f, def.PitchOffset);
        Assert.Equal(25.0f, def.RollOffset);
        Assert.True(def.UseActorPitch);
        Assert.False(def.InheritActorPitch);
        Assert.True(def.UseActorRoll);
        Assert.True(def.UseRotationCenter);
    }

    [Fact]
    public void InheritActorPitchClearsUseActorPitchLikeUdb()
    {
        const string text = @"
model PitchThing
{
    Model 0 ""thing.md3""
    UseActorPitch
    InheritActorPitch
    InheritActorRoll
}";

        var def = ModeldefParser.Parse(text).Single();

        Assert.True(def.InheritActorPitch);
        Assert.False(def.UseActorPitch);
        Assert.True(def.UseActorRoll);
    }

    [Fact]
    public void UnknownDirectivesDoNotConsumeOrientationFlags()
    {
        const string text = @"
model FlagThing
{
    Model 0 ""thing.md3""
    UnknownDirective 1
    UseActorPitch
    UnknownDirective 2
    UseActorRoll
    UnknownDirective 3
    Userotationcenter
}";

        var def = ModeldefParser.Parse(text).Single();

        Assert.True(def.UseActorPitch);
        Assert.True(def.UseActorRoll);
        Assert.True(def.UseRotationCenter);
    }

    [Fact]
    public void InvalidTransformValuesSkipModelBlockLikeUdb()
    {
        const string text = @"
model Bad
{
    Model 0 ""bad.md3""
    Scale 1 bogus 3
}
model Good
{
    Model 0 ""good.md3""
    Offset 1 2 3
}";

        var def = Assert.Single(ModeldefParser.Parse(text));

        Assert.Equal("Good", def.ActorName);
        Assert.Equal(new ModeldefVector(1.0f, 2.0f, 3.0f), def.Offset);
    }

    [Fact]
    public void EmptyPathSkipsModelBlockLikeUdb()
    {
        const string text = @"
model Bad
{
    Path """"
    Model 0 ""bad.md3""
}
model Good
{
    Path ""models""
    Model 0 ""good.md3""
}";

        var def = Assert.Single(ModeldefParser.Parse(text));

        Assert.Equal("Good", def.ActorName);
        Assert.Equal("models", def.Path);
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

    [Fact]
    public void LaterModelAndSkinIndexesReplaceEarlierOnes()
    {
        const string text = @"
model Repeated
{
    Model 0 ""first.md3""
    Model 0 ""second.md3""
    Skin 0 ""first.png""
    Skin 0 ""second.png""
    SurfaceSkin 0 2 ""first_alt.png""
    SurfaceSkin 0 2 ""second_alt.png""
}";

        var def = ModeldefParser.Parse(text).Single();

        Assert.Equal(new ModeldefModel(0, "second.md3"), def.Models.Single());
        Assert.Equal(new ModeldefSkin(0, "second.png"), def.Skins.Single());
        Assert.Equal(new ModeldefSurfaceSkin(0, 2, "second_alt.png"), def.SurfaceSkins.Single());
    }

    [Fact]
    public void DuplicateFramesAreSkippedLikeUdb()
    {
        const string text = @"
model RepeatedFrames
{
    Model 0 ""thing.md3""
    Model 1 ""other.md3""
    FrameIndex POSS A 0 12
    FrameIndex POSS A 0 12
    FrameIndex POSS A 1 12
    Frame POSS B 0 ""Run01""
    Frame POSS B 0 ""Run01""
    Frame POSS B 0 ""Run02""
}";

        var def = ModeldefParser.Parse(text).Single();

        Assert.Equal(
            new[]
            {
                new ModeldefFrame("POSS", "A", 0, 12),
                new ModeldefFrame("POSS", "A", 1, 12),
                new ModeldefFrame("POSS", "B", 0, 0, "Run01"),
                new ModeldefFrame("POSS", "B", 0, 0, "Run02"),
            },
            def.Frames);
    }

    [Theory]
    [InlineData("bad.txt")]
    [InlineData("bad")]
    public void SkipsModelsWithUnsupportedModelFilesLikeUdb(string file)
    {
        string text = $$"""
            model Bad
            {
                Model 0 "{{file}}"
            }
            model Good
            {
                Model 0 "good.obj"
            }
            """;

        var def = Assert.Single(ModeldefParser.Parse(text));

        Assert.Equal("Good", def.ActorName);
        Assert.Equal(new ModeldefModel(0, "good.obj"), def.Models.Single());
    }

    [Fact]
    public void SkipsModelsWithNegativeIndexesLikeUdb()
    {
        const string text = @"
model BadModel
{
    Model -1 ""bad.md3""
}
model BadSkin
{
    Model 0 ""bad.md3""
    Skin -1 ""bad.png""
}
model BadSurfaceSkin
{
    Model 0 ""bad.md3""
    SurfaceSkin 0 -1 ""bad.png""
}
model BadFrame
{
    Model 0 ""bad.md3""
    FrameIndex POSS A -1 0
}
model Good
{
    Model 0 ""good.iqm""
    Skin 0 ""good.png""
    SurfaceSkin 0 1 ""good_alt.png""
    FrameIndex POSS A 0 -1
}";

        var def = Assert.Single(ModeldefParser.Parse(text));

        Assert.Equal("Good", def.ActorName);
        Assert.Equal(new ModeldefModel(0, "good.iqm"), def.Models.Single());
        Assert.Equal(new ModeldefSkin(0, "good.png"), def.Skins.Single());
        Assert.Equal(new ModeldefSurfaceSkin(0, 1, "good_alt.png"), def.SurfaceSkins.Single());
        Assert.Equal(new ModeldefFrame("POSS", "A", 0, -1), def.Frames.Single());
    }

    [Theory]
    [InlineData("Model 0 \"bad<model.md3\"")]
    [InlineData("Skin 0 \"bad|skin.png\"")]
    [InlineData("SurfaceSkin 0 1 \"bad>skin.png\"")]
    public void SkipsModelsWithInvalidResourcePathCharactersLikeUdb(string invalidLine)
    {
        string text = $$"""
            model Bad
            {
                Model 0 "bad.md3"
                {{invalidLine}}
            }
            model Good
            {
                Model 0 "good.md3"
            }
            """;

        var def = Assert.Single(ModeldefParser.Parse(text));

        Assert.Equal("Good", def.ActorName);
        Assert.Equal(new ModeldefModel(0, "good.md3"), def.Models.Single());
    }

    [Fact]
    public void SkipsModelsWithoutModelEntriesLikeUdb()
    {
        const string text = @"
model Empty
{
    Skin 0 ""empty.png""
}
model Good
{
    Model 0 ""good.md2""
}";

        var def = Assert.Single(ModeldefParser.Parse(text));

        Assert.Equal("Good", def.ActorName);
    }

    [Fact]
    public void ParsesIncludesOnce()
    {
        const string text = @"
#include ""models/defs.txt""
#include ""models/defs.txt""
model Local { Model 0 ""local.md3"" }";

        var defs = ModeldefParser.Parse(text, include => include == "models/defs.txt"
            ? "model Included { Model 0 \"included.md3\" }"
            : null);

        Assert.Equal(new[] { "Included", "Local" }, defs.Select(d => d.ActorName).ToArray());
    }

    [Theory]
    [InlineData("../models/defs.txt")]
    [InlineData("./models/defs.txt")]
    [InlineData("models\\defs.txt")]
    [InlineData("/models/defs.txt")]
    public void RejectsInvalidIncludePaths(string includePath)
    {
        string text = includePath.Contains('\\') ? "#include " + includePath : "#include \"" + includePath + "\"";

        var defs = ModeldefParser.Parse(text, _ => "model Bad { Model 0 \"bad.md3\" }");

        Assert.Empty(defs);
    }
}
