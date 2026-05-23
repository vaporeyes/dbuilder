// ABOUTME: Tests Boom SWITCHES parsing, ResourceManager switch-pair resolution (explicit + SW1/SW2 convention),
// ABOUTME: and the built-in Heretic animation table.

using System;
using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class SwitchesAndHereticAnimTests
{
    private static void WriteName(byte[] buf, int off, string name)
        => Array.Copy(Encoding.ASCII.GetBytes(name), 0, buf, off, Math.Min(name.Length, 9));

    [Fact]
    public void ParsesSwitchesLump()
    {
        var data = new byte[20 * 2];
        WriteName(data, 0, "SW1CUSTOM");
        WriteName(data, 9, "SW2CUSTOM");
        BitConverter.GetBytes((short)1).CopyTo(data, 18); // game = 1 (doom)
        // second record game=0 -> terminator
        var list = BoomSwitches.Parse(data);
        Assert.Single(list);
        Assert.Equal("SW1CUSTOM", list[0].OffTexture);
        Assert.Equal("SW2CUSTOM", list[0].OnTexture);
    }

    [Fact]
    public void SwitchPairByConvention()
    {
        // Two textures named with the SW1/SW2 convention, no SWITCHES lump.
        byte[] Px() => TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, 1, 1, 1, 255));
        string pk3 = TestArtifacts.BuildPk3(("textures/SW1COMP.png", Px()), ("textures/SW2COMP.png", Px()));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);
            Assert.Equal("SW2COMP", rm.GetSwitchPair("SW1COMP"));
            Assert.Equal("SW1COMP", rm.GetSwitchPair("SW2COMP"));
            Assert.Null(rm.GetSwitchPair("SW1MISSING")); // partner not present
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void HereticBuiltinAnimationApplies()
    {
        byte[] Px(byte v) => TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, v, v, v, 255));
        string pk3 = TestArtifacts.BuildPk3(
            ("flats/FLTWAWA1.png", Px(1)),
            ("flats/FLTWAWA2.png", Px(2)),
            ("flats/FLTWAWA3.png", Px(3)));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);
            Assert.True(rm.HasAnimations);
            Assert.Equal("FLTWAWA1", rm.CurrentFlatFrame("FLTWAWA1", 0.0));
            Assert.Equal("FLTWAWA2", rm.CurrentFlatFrame("FLTWAWA1", 0.25));
        }
        finally { File.Delete(pk3); }
    }
}
