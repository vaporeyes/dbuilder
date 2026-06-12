// ABOUTME: Verifies 2D display colors for UDB internal and GLDEFS-backed dynamic light things.
// ABOUTME: Covers internal light args, arg0str spot colors, lightmap alpha, and actor light associations.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class DynamicLightDisplayTests
{
    [Fact]
    public void InternalPointLightUsesRgbArguments()
    {
        var thing = new Thing(new Vector2D(0, 0), 9800);
        thing.Args[0] = 10;
        thing.Args[1] = 20;
        thing.Args[2] = 30;

        Assert.Equal(unchecked((int)0xff0a141e), DynamicLightDisplay.ThingColor(thing, null, null));
    }

    [Fact]
    public void SpotLightUsesPackedArgString()
    {
        var thing = new Thing(new Vector2D(0, 0), 9840);
        thing.Fields[ColorPickerModel.DynamicLightPackedColorField] = "112233";

        Assert.Equal(unchecked((int)0xff112233), DynamicLightDisplay.ThingColor(thing, null, null));
    }

    [Fact]
    public void LightmapThingAppliesAlphaIntensity()
    {
        var thing = new Thing(new Vector2D(0, 0), 9876);
        thing.Args[0] = 100;
        thing.Args[1] = 50;
        thing.Args[2] = 25;
        thing.Fields["alpha"] = 0.5;

        Assert.Equal(unchecked((int)0xff32190c), DynamicLightDisplay.ThingColor(thing, null, null));
    }

    [Fact]
    public void GldefsActorLightUsesConfiguredClassAssociation()
    {
        GameConfiguration config = GameConfiguration.FromText("""
            thingtypes
            {
                decorations
                {
                    color = 7;
                    31000
                    {
                        title = "Lamp";
                        class = "LampActor";
                    }
                }
            }
            """);
        Gldefs gldefs = GldefsParser.Parse("""
            pointlight LAMP { color 1.0 0.5 0.25 size 64 }
            object LampActor { frame LAMP { light LAMP } }
            """);
        var thing = new Thing(new Vector2D(0, 0), 31000);

        Assert.Equal(unchecked((int)0xffff7f3f), DynamicLightDisplay.ThingColor(thing, config, gldefs));
    }

    [Fact]
    public void InternalPointLightRadiusUsesPrimaryRadiusArgument()
    {
        var thing = new Thing(new Vector2D(0, 0), 9800);
        thing.Args[3] = 96;

        Assert.Equal(96.0, DynamicLightDisplay.ThingRadius(thing, null, null));
    }

    [Fact]
    public void VavoomLightRadiusUsesFirstArgument()
    {
        var thing = new Thing(new Vector2D(0, 0), 1502);
        thing.Args[0] = 128;

        Assert.Equal(128.0, DynamicLightDisplay.ThingRadius(thing, null, null));
    }

    [Fact]
    public void GldefsActorLightRadiusUsesConfiguredLightSize()
    {
        GameConfiguration config = GameConfiguration.FromText("""
            thingtypes
            {
                decorations
                {
                    color = 7;
                    31000
                    {
                        title = "Lamp";
                        class = "LampActor";
                    }
                }
            }
            """);
        Gldefs gldefs = GldefsParser.Parse("""
            pointlight LAMP { color 1.0 0.5 0.25 size 64 }
            object LampActor { frame LAMP { light LAMP } }
            """);
        var thing = new Thing(new Vector2D(0, 0), 31000);

        Assert.Equal(128.0, DynamicLightDisplay.ThingRadius(thing, config, gldefs));
    }

    [Fact]
    public void NonLightThingReturnsNull()
    {
        var thing = new Thing(new Vector2D(0, 0), 3001);

        Assert.Null(DynamicLightDisplay.ThingColor(thing, null, null));
        Assert.Null(DynamicLightDisplay.ThingRadius(thing, null, null));
    }

    [Fact]
    public void BillboardTintUsesDynamicLightColorWhenAvailable()
    {
        var thing = new Thing(new Vector2D(0, 0), 9800);
        thing.Args[0] = 10;
        thing.Args[1] = 20;
        thing.Args[2] = 30;

        Assert.Equal(unchecked((int)0xff0a141e), DynamicLightDisplay.BillboardTint(thing, null, null));
    }

    [Fact]
    public void BillboardTintUsesWhiteForNonLightThings()
    {
        var thing = new Thing(new Vector2D(0, 0), 3001);

        Assert.Equal(DynamicLightDisplay.DefaultBillboardTint, DynamicLightDisplay.BillboardTint(thing, null, null));
    }

    [Fact]
    public void BillboardTintKeepsSelectedThingsHighlighted()
    {
        var thing = new Thing(new Vector2D(0, 0), 9800) { Selected = true };
        thing.Args[0] = 10;
        thing.Args[1] = 20;
        thing.Args[2] = 30;

        Assert.Equal(DynamicLightDisplay.SelectedBillboardTint, DynamicLightDisplay.BillboardTint(thing, null, null));
    }
}
