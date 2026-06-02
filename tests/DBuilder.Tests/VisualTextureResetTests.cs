// ABOUTME: Verifies UDB-style visual reset behavior for texture fields and thing transforms.
// ABOUTME: Covers model-level reset helpers used by the 3D editor command dispatcher.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VisualTextureResetTests
{
    [Fact]
    public void ResetSidedefOffsetsClearsClassicOffsets()
    {
        var side = new Sidedef(new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(64, 0))), isFront: true)
        {
            OffsetX = 8,
            OffsetY = -16,
        };

        bool changed = VisualTextureReset.ResetSidedefOffsets(side);

        Assert.True(changed);
        Assert.Equal(0, side.OffsetX);
        Assert.Equal(0, side.OffsetY);
    }

    [Fact]
    public void ResetLocalSidedefClearsPartScaleOffsetAndBrightnessFields()
    {
        var side = new Sidedef(new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(64, 0))), isFront: true);
        side.Fields["offsetx_top"] = 4.0;
        side.Fields["offsety_top"] = -2.0;
        side.Fields["scalex_top"] = 0.5;
        side.Fields["scaley_top"] = 2.0;
        side.Fields["light"] = 32;
        side.Fields["lightabsolute"] = true;
        side.Fields["offsetx_mid"] = 8.0;

        bool changed = VisualTextureReset.ResetLocalSidedef(side, SidedefPart.Upper);

        Assert.True(changed);
        Assert.DoesNotContain("offsetx_top", side.Fields.Keys);
        Assert.DoesNotContain("offsety_top", side.Fields.Keys);
        Assert.DoesNotContain("scalex_top", side.Fields.Keys);
        Assert.DoesNotContain("scaley_top", side.Fields.Keys);
        Assert.DoesNotContain("light", side.Fields.Keys);
        Assert.DoesNotContain("lightabsolute", side.Fields.Keys);
        Assert.Contains("offsetx_mid", side.Fields.Keys);
    }

    [Fact]
    public void ResetSectorFlatClearsBasicAndLocalFloorFields()
    {
        var sector = new Sector();
        sector.Fields["xpanningfloor"] = 8.0;
        sector.Fields["ypanningfloor"] = -16.0;
        sector.Fields["xscalefloor"] = 0.5;
        sector.Fields["rotationfloor"] = 90.0;

        bool changed = VisualTextureReset.ResetSectorFlat(sector, ceiling: false, local: false);

        Assert.True(changed);
        Assert.DoesNotContain("xpanningfloor", sector.Fields.Keys);
        Assert.DoesNotContain("ypanningfloor", sector.Fields.Keys);
        Assert.Contains("xscalefloor", sector.Fields.Keys);

        changed = VisualTextureReset.ResetSectorFlat(sector, ceiling: false, local: true);

        Assert.True(changed);
        Assert.DoesNotContain("xscalefloor", sector.Fields.Keys);
        Assert.DoesNotContain("rotationfloor", sector.Fields.Keys);
    }

    [Fact]
    public void ResetThingClearsScaleAndOptionallyPitchRoll()
    {
        var thing = new Thing(new Vector2D(0, 0), 3001)
        {
            ScaleX = 2.0,
            ScaleY = 0.5,
            Pitch = 15,
            Roll = 30,
        };

        bool changed = VisualTextureReset.ResetThing(thing, local: false);

        Assert.True(changed);
        Assert.Equal(1.0, thing.ScaleX, 1e-9);
        Assert.Equal(1.0, thing.ScaleY, 1e-9);
        Assert.Equal(15, thing.Pitch);
        Assert.Equal(30, thing.Roll);

        changed = VisualTextureReset.ResetThing(thing, local: true);

        Assert.True(changed);
        Assert.Equal(0, thing.Pitch);
        Assert.Equal(0, thing.Roll);
    }
}
