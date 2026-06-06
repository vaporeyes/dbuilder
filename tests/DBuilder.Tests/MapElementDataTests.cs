// ABOUTME: Tests typed custom-field and action-argument access helpers on map elements.
// ABOUTME: Verifies conservative conversion, defaults, removal and five-slot argument bounds.

using DBuilder.Geometry;
using DBuilder.Map;
using System.Drawing;

namespace DBuilder.Tests;

public class MapElementDataTests
{
    [Fact]
    public void TypedFieldsReadStoredValuesAndDefaults()
    {
        var sector = new Sector();
        sector.SetField("lightcolor", 16711680);
        sector.SetField("gravity", 0.5);
        sector.SetField("hidden", true);
        sector.SetField("comment", "lava");

        Assert.Equal(16711680, sector.GetField<int>("lightcolor"));
        Assert.Equal(0.5, sector.GetField<double>("gravity"));
        Assert.True(sector.GetField<bool>("hidden"));
        Assert.Equal("lava", sector.GetField<string>("comment"));
        Assert.Equal("fallback", sector.GetField("missing", "fallback"));
    }

    [Fact]
    public void TypedFieldsSupportConservativeNumericConversion()
    {
        var vertex = new Vertex(new Vector2D(0, 0));
        vertex.SetField("whole_double", 8.0);
        vertex.SetField("fractional_double", 8.5);
        vertex.SetField("integer", 4);
        vertex.SetField("whole_long", 12L);
        vertex.SetField("large_long", 4294967295L);
        vertex.SetField("unsafe_double", 9007199254740994.0);

        Assert.True(vertex.TryGetField<int>("whole_double", out var whole));
        Assert.Equal(8, whole);
        Assert.False(vertex.TryGetField<int>("fractional_double", out _));
        Assert.Equal(4.0, vertex.GetField<double>("integer"));
        Assert.Equal(4L, vertex.GetField<long>("integer"));
        Assert.Equal(8L, vertex.GetField<long>("whole_double"));
        Assert.False(vertex.TryGetField<long>("fractional_double", out _));
        Assert.False(vertex.TryGetField<long>("unsafe_double", out _));
        Assert.True(vertex.TryGetField<int>("whole_long", out var wholeLong));
        Assert.Equal(12, wholeLong);
        Assert.False(vertex.TryGetField<int>("large_long", out _));
        Assert.Equal(4294967295.0, vertex.GetField<double>("large_long"));
    }

    [Fact]
    public void RemoveFieldReportsWhetherFieldExisted()
    {
        var sidedef = new Sidedef();
        sidedef.SetField("scalex_mid", 1.5);

        Assert.True(sidedef.RemoveField("scalex_mid"));
        Assert.False(sidedef.RemoveField("scalex_mid"));
        Assert.False(sidedef.TryGetField<double>("scalex_mid", out _));
    }

    [Fact]
    public void DefaultOmittingFieldSettersRemoveDefaultValues()
    {
        var sector = new Sector();

        sector.SetFloatField("gravity", 0.5);
        sector.SetIntegerField("lightcolor", 16711680);
        sector.SetStringField("comment", "lava", "");
        sector.SetBooleanField("lightabsolute", true);

        Assert.Equal(0.5, sector.GetFloatField("gravity"));
        Assert.Equal(16711680, sector.GetIntegerField("lightcolor"));
        Assert.Equal("lava", sector.GetStringField("comment"));
        Assert.True(sector.GetBooleanField("lightabsolute"));

        sector.SetFloatField("gravity", 1.0, 1.0);
        sector.SetIntegerField("lightcolor", 255, 255);
        sector.SetStringField("comment", "default", "default");
        sector.SetBooleanField("lightabsolute", false);

        Assert.Equal(1.0, sector.GetFloatField("gravity", 1.0));
        Assert.Equal(255, sector.GetIntegerField("lightcolor", 255));
        Assert.Equal("default", sector.GetStringField("comment", "default"));
        Assert.False(sector.GetBooleanField("lightabsolute"));
        Assert.Empty(sector.Fields);
    }

    [Fact]
    public void BooleanFieldSetterSupportsTrueDefault()
    {
        var side = new Sidedef();

        side.SetBooleanField("render", false, defaultValue: true);

        Assert.False(side.GetBooleanField("render", defaultValue: true));
        Assert.False(Assert.IsType<bool>(side.Fields["render"]));

        side.SetBooleanField("render", true, defaultValue: true);

        Assert.True(side.GetBooleanField("render", defaultValue: true));
        Assert.Empty(side.Fields);
    }

    [Fact]
    public void RemoveFieldsIgnoresMissingKeys()
    {
        var thing = new Thing(new Vector2D(0, 0), 3001);
        thing.SetField("alpha", 1);
        thing.SetField("beta", 2);

        thing.RemoveFields(["alpha", "missing"]);

        Assert.False(thing.Fields.ContainsKey("alpha"));
        Assert.True(thing.Fields.ContainsKey("beta"));
    }

    [Fact]
    public void FieldComparisonRequiresMatchingKeysTypesAndValues()
    {
        var left = new Vertex(new Vector2D(0, 0));
        var right = new Vertex(new Vector2D(1, 1));

        Assert.True(left.FieldsMatch(right));
        Assert.True(left.FieldValueMatches(right, "missing"));

        left.SetIntegerField("id", 1);
        right.SetIntegerField("id", 1);
        left.SetFloatField("gravity", 1.0);
        right.SetFloatField("gravity", 1.0);

        Assert.True(left.FieldsMatch(right));
        Assert.True(left.FieldValueMatches(right, "id"));

        right.SetFloatField("id", 1.0);

        Assert.False(left.FieldsMatch(right));
        Assert.False(left.FieldValueMatches(right, "id"));
    }

    [Fact]
    public void ArgumentHelpersReadSetAndClearLinedefArgs()
    {
        var line = new Linedef();

        Assert.Equal(5, Linedef.NUM_ARGS);
        Assert.Equal(Linedef.NUM_ARGS, line.Args.Length);
        Assert.Equal(0.01, Linedef.SIDE_POINT_DISTANCE);
        Assert.Equal(Linedef.SIDE_POINT_DISTANCE, Linedef.SidePointDistance);

        line.SetArg(0, 7);
        line.SetArg(Linedef.NUM_ARGS - 1, 99);

        Assert.Equal(7, line.GetArg(0));
        Assert.Equal(99, line.GetArg(Linedef.NUM_ARGS - 1));

        line.ClearArgs();

        Assert.All(line.Args, arg => Assert.Equal(0, arg));
    }

    [Fact]
    public void ArgumentHelpersEnforceFiveSlotRange()
    {
        var thing = new Thing(new Vector2D(0, 0), 3001);

        Assert.Equal(5, Thing.NUM_ARGS);
        Assert.Equal(Thing.NUM_ARGS, thing.Args.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => thing.GetArg(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => thing.SetArg(Thing.NUM_ARGS, 1));
    }

    [Fact]
    public void SidedefMiddleTextureAliasMatchesUdbSurface()
    {
        var side = new Sidedef();

        side.MidTexture = "STARTAN";
        Assert.Equal("STARTAN", side.MiddleTexture);

        side.MiddleTexture = "BRICK1";
        Assert.Equal("BRICK1", side.MidTexture);
    }

    [Fact]
    public void SidedefLongTextureNamesMatchUdbSurfaceAndCopy()
    {
        var source = new Sidedef
        {
            LongHighTexture = 11,
            LongMiddleTexture = 22,
            LongLowTexture = 33,
        };
        var target = new Sidedef();

        Assert.Equal(MapSet.EmptyLongName, target.LongHighTexture);
        Assert.Equal(MapSet.EmptyLongName, target.LongMiddleTexture);
        Assert.Equal(MapSet.EmptyLongName, target.LongLowTexture);

        source.CopyPropertiesTo(target);

        Assert.Equal(11, target.LongHighTexture);
        Assert.Equal(22, target.LongMiddleTexture);
        Assert.Equal(33, target.LongLowTexture);
    }

    [Fact]
    public void SectorEffectAliasMatchesUdbSurface()
    {
        var sector = new Sector();

        sector.Special = 9;
        Assert.Equal(9, sector.Effect);

        sector.Effect = 11;
        Assert.Equal(11, sector.Special);
    }

    [Fact]
    public void SectorLongTextureNamesMatchUdbSurfaceAndCopy()
    {
        var source = new Sector
        {
            LongFloorTexture = 44,
            LongCeilTexture = 55,
        };
        var target = new Sector();

        Assert.Equal(MapSet.EmptyLongName, target.LongFloorTexture);
        Assert.Equal(MapSet.EmptyLongName, target.LongCeilTexture);

        source.CopyPropertiesTo(target);

        Assert.Equal(44, target.LongFloorTexture);
        Assert.Equal(55, target.LongCeilTexture);
    }

    [Fact]
    public void SectorUpdateNeededSetterMatchesUdbStickyBehavior()
    {
        var sector = new Sector();

        Assert.False(sector.UpdateNeeded);

        sector.UpdateNeeded = false;
        Assert.False(sector.UpdateNeeded);

        sector.UpdateNeeded = true;
        sector.UpdateNeeded = false;

        Assert.True(sector.UpdateNeeded);
    }

    [Fact]
    public void SectorBrightnessSetterMarksUpdateNeededLikeUdb()
    {
        var sector = new Sector();

        Assert.Equal(192, sector.Brightness);
        Assert.False(sector.UpdateNeeded);

        sector.Brightness = 128;

        Assert.Equal(128, sector.Brightness);
        Assert.True(sector.UpdateNeeded);
    }

    [Fact]
    public void SectorSlopeSettersMarkUpdateNeededLikeUdb()
    {
        var floorSlope = new Vector3D(0, 1, -1);
        var ceilSlope = new Vector3D(0, 1, 1);

        var floorSlopeSector = new Sector();
        floorSlopeSector.FloorSlope = floorSlope;
        Assert.Equal(floorSlope, floorSlopeSector.FloorSlope);
        Assert.True(floorSlopeSector.UpdateNeeded);

        var floorOffsetSector = new Sector();
        floorOffsetSector.FloorSlopeOffset = 4.5;
        Assert.Equal(4.5, floorOffsetSector.FloorSlopeOffset);
        Assert.True(floorOffsetSector.UpdateNeeded);

        var ceilSlopeSector = new Sector();
        ceilSlopeSector.CeilSlope = ceilSlope;
        Assert.Equal(ceilSlope, ceilSlopeSector.CeilSlope);
        Assert.True(ceilSlopeSector.UpdateNeeded);

        var ceilOffsetSector = new Sector();
        ceilOffsetSector.CeilSlopeOffset = -2.25;
        Assert.Equal(-2.25, ceilOffsetSector.CeilSlopeOffset);
        Assert.True(ceilOffsetSector.UpdateNeeded);
    }

    [Fact]
    public void RawFlagsExposeUdbUnsignedBinaryFlagSurface()
    {
        var line = new Linedef { Flags = 0x12345 };
        var thing = new Thing(new Vector2D(0, 0), 3001) { Flags = 0x23456 };

        Assert.Equal((ushort)0x2345, line.RawFlags);
        Assert.Equal((ushort)0x3456, thing.RawFlags);
    }

    [Fact]
    public void ThingAngleAliasesMatchUdbSurface()
    {
        var thing = new Thing(new Vector2D(0, 0), 3001)
        {
            Angle = 90,
            Pitch = 45,
            Roll = 180,
        };

        Assert.Equal(90, thing.AngleDoom);
        Assert.Equal(Math.PI / 4.0, thing.PitchRad, 1e-9);
        Assert.Equal(Math.PI, thing.RollRad, 1e-9);

        thing.AngleDoom = 270;

        Assert.Equal(270, thing.Angle);
    }

    [Fact]
    public void ThingRenderSizeMatchesUdbSurfaceAndCopies()
    {
        var source = new Thing(new Vector2D(0, 0), 3001)
        {
            ActorScale = new SizeF(1.25f, 0.75f),
            Size = 16,
            RenderSize = 24,
            FixedSize = true,
        };
        var target = new Thing();

        source.CopyPropertiesTo(target);

        Assert.Equal(new SizeF(1.25f, 0.75f), target.ActorScale);
        Assert.Equal(16, target.Size);
        Assert.Equal(24, target.RenderSize);
        Assert.True(target.FixedSize);
    }

    [Fact]
    public void VertexRenderingConstantsMatchUdbSurface()
    {
        Assert.Equal(1, Vertex.BUFFERVERTICES);
        Assert.Equal(1, Vertex.RENDERPRIMITIVES);
    }

    [Fact]
    public void NamedFlagHelpersMatchUdbSurface()
    {
        var line = new Linedef();
        var side = new Sidedef();
        var sector = new Sector();
        var thing = new Thing(new Vector2D(0, 0), 3001);

        line.SetFlag("blocking", true);
        side.SetFlag("clipmidtex", true);
        sector.SetFlag("secret", true);
        thing.SetFlag("skill1", true);

        Dictionary<string, bool> lineFlags = line.GetFlags();
        Dictionary<string, bool> sideFlags = side.GetFlags();
        Dictionary<string, bool> sectorFlags = sector.GetFlags();
        Dictionary<string, bool> thingFlags = thing.GetFlags();
        HashSet<string> lineEnabled = line.GetEnabledFlags();
        HashSet<string> sideEnabled = side.GetEnabledFlags();
        HashSet<string> sectorEnabled = sector.GetEnabledFlags();
        HashSet<string> thingEnabled = thing.GetEnabledFlags();

        Assert.True(line.IsFlagSet("blocking"));
        Assert.True(side.IsFlagSet("clipmidtex"));
        Assert.True(sector.IsFlagSet("secret"));
        Assert.True(thing.IsFlagSet("skill1"));
        Assert.True(lineFlags["blocking"]);
        Assert.True(sideFlags["clipmidtex"]);
        Assert.True(sectorFlags["secret"]);
        Assert.True(thingFlags["skill1"]);
        Assert.Contains("blocking", lineEnabled);
        Assert.Contains("clipmidtex", sideEnabled);
        Assert.Contains("secret", sectorEnabled);
        Assert.Contains("skill1", thingEnabled);

        lineFlags["playeruse"] = true;
        sideFlags["wrapmidtex"] = true;
        sectorFlags["damagehazard"] = true;
        thingFlags["skill2"] = true;
        lineEnabled.Add("monsteruse");
        sideEnabled.Add("lightfog");
        sectorEnabled.Add("nofallingdamage");
        thingEnabled.Add("ambush");

        Assert.False(line.IsFlagSet("playeruse"));
        Assert.False(side.IsFlagSet("wrapmidtex"));
        Assert.False(sector.IsFlagSet("damagehazard"));
        Assert.False(thing.IsFlagSet("skill2"));
        Assert.False(line.IsFlagSet("monsteruse"));
        Assert.False(side.IsFlagSet("lightfog"));
        Assert.False(sector.IsFlagSet("nofallingdamage"));
        Assert.False(thing.IsFlagSet("ambush"));

        line.SetFlag("blocking", false);
        side.SetFlag("clipmidtex", false);
        sector.SetFlag("secret", false);
        thing.SetFlag("skill1", false);

        Assert.False(line.IsFlagSet("blocking"));
        Assert.False(side.IsFlagSet("clipmidtex"));
        Assert.False(sector.IsFlagSet("secret"));
        Assert.False(thing.IsFlagSet("skill1"));
        Assert.Empty(line.UdmfFlags);
        Assert.Empty(side.UdmfFlags);
        Assert.Empty(sector.UdmfFlags);
        Assert.Empty(thing.UdmfFlags);

        line.SetFlag("blocking", true);
        side.SetFlag("clipmidtex", true);
        sector.SetFlag("secret", true);
        thing.SetFlag("skill1", true);

        line.ClearFlags();
        side.ClearFlags();
        sector.ClearFlags();
        thing.ClearFlags();

        Assert.Empty(line.GetFlags());
        Assert.Empty(side.GetFlags());
        Assert.Empty(sector.GetFlags());
        Assert.Empty(thing.GetFlags());
        Assert.Empty(line.GetEnabledFlags());
        Assert.Empty(side.GetEnabledFlags());
        Assert.Empty(sector.GetEnabledFlags());
        Assert.Empty(thing.GetEnabledFlags());
    }
}
