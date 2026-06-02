// ABOUTME: Verifies UDB-style visual scale commands for wall texture parts and things.
// ABOUTME: Uses direct model helpers so scale math stays covered without a live renderer.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VisualScaleAdjustmentTests
{
    [Fact]
    public void ThingScaleUsesSpritePixelIncrementLikeUdb()
    {
        var thing = new Thing(new Vector2D(0, 0), 3001)
        {
            ScaleX = 1.0,
            ScaleY = 1.0,
        };

        bool changed = VisualScaleAdjustment.AdjustThing(thing, incrementX: 1, incrementY: -1, spriteWidth: 32, spriteHeight: 64);

        Assert.True(changed);
        Assert.Equal(1.031, thing.ScaleX, 1e-9);
        Assert.Equal(0.984, thing.ScaleY, 1e-9);
    }

    [Fact]
    public void WallScaleUsesInverseTexturePixelIncrementLikeUdb()
    {
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(64, 0)));
        var side = new Sidedef(line, isFront: true);

        bool changed = VisualScaleAdjustment.AdjustWall(side, SidedefPart.Middle, incrementX: 1, incrementY: -1, textureWidth: 32, textureHeight: 64);

        Assert.True(changed);
        Assert.Equal(0.969, side.GetFloatField("scalex_mid", 1.0), 1e-9);
        Assert.Equal(1.016, side.GetFloatField("scaley_mid", 1.0), 1e-9);
    }

    [Fact]
    public void DefaultScaleFieldsAreRemovedWhenWallScaleReturnsToOne()
    {
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(64, 0)));
        var side = new Sidedef(line, isFront: true);
        side.SetFloatField("scalex_top", 0.5, 1.0);

        bool changed = VisualScaleAdjustment.AdjustWall(side, SidedefPart.Upper, incrementX: -32, incrementY: 0, textureWidth: 64, textureHeight: 64);

        Assert.True(changed);
        Assert.False(side.Fields.ContainsKey("scalex_top"));
    }

    [Fact]
    public void ZeroScaleStepFlipsSignLikeUdb()
    {
        var thing = new Thing(new Vector2D(0, 0), 3001)
        {
            ScaleX = 0.031,
            ScaleY = 1.0,
        };

        bool changed = VisualScaleAdjustment.AdjustThing(thing, incrementX: -1, incrementY: 0, spriteWidth: 32, spriteHeight: 64);

        Assert.True(changed);
        Assert.Equal(-0.031, thing.ScaleX, 1e-9);
        Assert.Equal(1.0, thing.ScaleY, 1e-9);
    }
}
