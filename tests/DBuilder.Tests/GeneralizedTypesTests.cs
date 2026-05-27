// ABOUTME: Tests config-driven Boom generalized linedef/sector type parsing and packed-number decoding.
// ABOUTME: Mirrors the gen_linedeftypes / gen_sectortypes structure from UDB's Boom_generalized.cfg.

using System.Linq;
using DBuilder.IO;

namespace DBuilder.Tests;

public class GeneralizedTypesTests
{
    private const string Cfg = @"
gen_linedeftypes
{
    normal
    {
        title = ""None"";
        offset = 0;
        length = 0;
    }
    floors
    {
        title = ""Floor"";
        offset = 24576;
        length = 8192;
        trigger
        {
            0 = ""Walk Over Once"";
            2 = ""Switch Once"";
            3 = ""Switch Repeatable"";
        }
        speed
        {
            0 = ""Slow"";
            8 = ""Normal"";
            16 = ""Fast"";
            24 = ""Turbo"";
        }
        direction
        {
            0 = ""Down"";
            64 = ""Up"";
        }
    }
}
gen_sectortypes
{
    damage
    {
        0 = ""None"";
        32 = ""5 per second"";
        64 = ""10 per second"";
        96 = ""20 per second"";
    }
}
sectortypes
{
    1 = ""Secret"";
}";

    [Fact]
    public void ParsesCategoriesSkippingPlaceholder()
    {
        var gc = GameConfiguration.FromText(Cfg);
        Assert.Equal(2, gc.GeneralizedLinedefs.Count);
        var floor = gc.GeneralizedLinedefs.First(c => c.Title == "Floor");
        Assert.Equal(24576, floor.Offset);
        Assert.Equal(8192, floor.Length);
        Assert.Equal(3, floor.Options.Count);
        Assert.False(gc.GeneralizedLinedefs.First(c => c.Title == "None").Contains(0)); // length 0 never matches
    }

    [Fact]
    public void ComputesOptionMaskFromBitValues()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var floor = gc.GeneralizedLinedefs.First(c => c.Title == "Floor");
        var speed = floor.Options.First(o => o.Name == "Speed");
        Assert.Equal(24, speed.Mask); // 0|8|16|24
        var trigger = floor.Options.First(o => o.Name == "Trigger");
        Assert.Equal(3, trigger.Mask); // 0|2|3
    }

    [Fact]
    public void DecodesPackedLinedefNumber()
    {
        var gc = GameConfiguration.FromText(Cfg);
        // Floor base + Switch Once (2) + Normal speed (8) + Up (64)
        int action = 24576 + 2 + 8 + 64;
        string desc = gc.DescribeGeneralizedLinedef(action)!;
        Assert.StartsWith("Floor (", desc);
        Assert.Contains("Trigger: Switch Once", desc);
        Assert.Contains("Speed: Normal", desc);
        Assert.Contains("Direction: Up", desc);
    }

    [Fact]
    public void LinedefActionTitlePrefersConfigGeneralized()
    {
        var gc = GameConfiguration.FromText(Cfg);
        int action = 24576 + 3; // Switch Repeatable, slow, down
        Assert.Contains("Trigger: Switch Repeatable", gc.LinedefActionTitle(action));
    }

    [Fact]
    public void NonGeneralizedReturnsNull()
    {
        var gc = GameConfiguration.FromText(Cfg);
        Assert.Null(gc.DescribeGeneralizedLinedef(11)); // a normal action number
    }

    [Fact]
    public void ParsesSectorGeneralizedOptions()
    {
        var gc = GameConfiguration.FromText(Cfg);
        Assert.Single(gc.GeneralizedSectorEffects);
        var damage = gc.GeneralizedSectorEffects[0];
        Assert.Equal("Damage", damage.Name);
        Assert.Equal(32, damage.BitsStep);
        Assert.Contains(damage.Bits, bit => bit.Value == 64 && bit.Title == "10 per second");
    }

    [Fact]
    public void DecodesGeneralizedSectorEffects()
    {
        var gc = GameConfiguration.FromText(Cfg);
        Assert.True(gc.IsGeneralizedSectorEffect(32));
        Assert.False(gc.IsGeneralizedSectorEffect(1));
        Assert.Equal("Damage: 5 per second", gc.DescribeGeneralizedSectorEffect(32));
        Assert.Equal("Secret + Damage: 5 per second", gc.SectorEffectTitle(33));

        var data = gc.GetSectorEffectData(33);
        Assert.Equal(1, data.Effect);
        Assert.Contains(32, data.GeneralizedBits);
    }
}
