// ABOUTME: Tests UDB-style ambient sound radius lookup for 2D thing helper rings.
// ABOUTME: Covers AmbientSound classes, numbered ambient classes, and custom thing arg radii.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class SoundRadiusDisplayTests
{
    [Fact]
    public void AmbientSoundClassUsesArg0SoundIndex()
    {
        var thing = new Thing(new Vector2D(0, 0), 14001);
        thing.Args[0] = 4;
        ThingTypeInfo info = ThingInfo("AmbientSound");
        SndInfo sndInfo = SndInfoWithAmbient(index: 4, minimum: 200, maximum: 1200);

        AmbientSoundRadii? radii = SoundRadiusDisplay.ThingRadii(thing, info, sndInfo, doomMap: false);

        Assert.Equal(new AmbientSoundRadii(200, 1200), radii);
    }

    [Fact]
    public void NumberedAmbientSoundClassUsesClassIndex()
    {
        var thing = new Thing(new Vector2D(0, 0), 14002);
        ThingTypeInfo info = ThingInfo("$AmbientSound7");
        SndInfo sndInfo = SndInfoWithAmbient(index: 7, minimum: 64, maximum: 512);

        AmbientSoundRadii? radii = SoundRadiusDisplay.ThingRadii(thing, info, sndInfo, doomMap: true);

        Assert.Equal(new AmbientSoundRadii(64, 512), radii);
    }

    [Fact]
    public void AmbientSoundClassUsesCustomMinMaxArgsWhenPresent()
    {
        var thing = new Thing(new Vector2D(0, 0), 14001);
        thing.Args[0] = 4;
        thing.Args[2] = 32;
        thing.Args[3] = 96;
        thing.Args[4] = 2;
        ThingTypeInfo info = ThingInfo("AmbientSoundNoGravity");
        SndInfo sndInfo = SndInfoWithAmbient(index: 4, minimum: 200, maximum: 1200);

        AmbientSoundRadii? radii = SoundRadiusDisplay.ThingRadii(thing, info, sndInfo, doomMap: false);

        Assert.Equal(new AmbientSoundRadii(64, 192), radii);
    }

    [Fact]
    public void NonAmbientThingReturnsNull()
    {
        var thing = new Thing(new Vector2D(0, 0), 3001);

        Assert.Null(SoundRadiusDisplay.ThingRadii(thing, ThingInfo("DoomImp"), new SndInfo(), doomMap: false));
    }

    private static ThingTypeInfo ThingInfo(string className)
        => new() { ClassName = className, Color = 3 };

    private static SndInfo SndInfoWithAmbient(int index, double minimum, double maximum)
    {
        var sndInfo = new SndInfo();
        sndInfo.AmbientSounds[index] = new AmbientSoundInfo(
            index,
            "world/sound",
            AmbientSoundType.Point,
            AmbientSoundMode.Continuous,
            1.0,
            1.0,
            0.0,
            0.0,
            0.0,
            minimum,
            maximum);
        return sndInfo;
    }
}
