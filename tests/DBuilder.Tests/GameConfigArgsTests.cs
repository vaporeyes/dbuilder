// ABOUTME: Tests GameConfiguration parsing of enums and per-action/thing argument metadata (title/type/enum/default).
// ABOUTME: Drives the smarter property dialogs that label and enum-pick the 5 Hexen-style args.

using DBuilder.IO;

namespace DBuilder.Tests;

public class GameConfigArgsTests
{
    private const string Cfg = @"
enums
{
    speeds
    {
        0 = ""Slow"";
        16 = ""Normal"";
        32 = ""Fast"";
    }
}
linedeftypes
{
    polyobj
    {
        title = ""Polyobjects"";
        2
        {
            title = ""Rotate Left"";
            arg0 { title = ""Polyobject Number""; type = 25; }
            arg1 { title = ""Speed""; type = 11; enum = ""speeds""; default = 16; }
        }
    }
}
thingtypes
{
    scripted
    {
        9001
        {
            title = ""MapSpot"";
            arg0 { title = ""TID""; }
        }
    }
}";

    [Fact]
    public void ParsesEnums()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var e = gc.GetEnum("speeds");
        Assert.NotNull(e);
        Assert.Equal("Normal", e![16]);
        Assert.Equal("Fast", e[32]);
    }

    [Fact]
    public void ParsesLinedefActionArgs()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var a = gc.GetLinedefAction(2);
        Assert.NotNull(a);
        Assert.Equal(5, a!.Args.Length);
        Assert.Equal("Polyobject Number", a.Args[0].Title);
        Assert.True(a.Args[0].Used);
        Assert.Equal("Speed", a.Args[1].Title);
        Assert.Equal("speeds", a.Args[1].Enum);
        Assert.Equal(16, a.Args[1].Default);
        Assert.False(a.Args[2].Used); // unused slot
    }

    [Fact]
    public void ResolvesArgEnum()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var arg = gc.GetLinedefAction(2)!.Args[1];
        var e = gc.GetArgEnum(arg);
        Assert.NotNull(e);
        Assert.Equal("Slow", e![0]);
    }

    [Fact]
    public void ParsesThingArgs()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var t = gc.GetThing(9001);
        Assert.NotNull(t);
        Assert.Equal("TID", t!.Args[0].Title);
    }

    [Fact]
    public void ParsesSkills()
    {
        const string skillCfg = @"
skills
{
    1 = ""I'm too young to die"";
    3 = ""Hurt me plenty"";
    5 = ""Nightmare"";
}";
        var gc = GameConfiguration.FromText(skillCfg);
        Assert.Equal(3, gc.Skills.Count);
        Assert.Equal("Hurt me plenty", gc.Skills[3]);
        Assert.Equal("Nightmare", gc.Skills[5]);
    }
}
