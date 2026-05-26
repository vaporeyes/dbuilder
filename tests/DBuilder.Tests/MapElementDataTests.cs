// ABOUTME: Tests typed custom-field and action-argument access helpers on map elements.
// ABOUTME: Verifies conservative conversion, defaults, removal and five-slot argument bounds.

using DBuilder.Geometry;
using DBuilder.Map;

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

        Assert.True(vertex.TryGetField<int>("whole_double", out var whole));
        Assert.Equal(8, whole);
        Assert.False(vertex.TryGetField<int>("fractional_double", out _));
        Assert.Equal(4.0, vertex.GetField<double>("integer"));
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

        Assert.Equal(0.5, sector.GetFloatField("gravity"));
        Assert.Equal(16711680, sector.GetIntegerField("lightcolor"));
        Assert.Equal("lava", sector.GetStringField("comment"));

        sector.SetFloatField("gravity", 1.0, 1.0);
        sector.SetIntegerField("lightcolor", 255, 255);
        sector.SetStringField("comment", "default", "default");

        Assert.Equal(1.0, sector.GetFloatField("gravity", 1.0));
        Assert.Equal(255, sector.GetIntegerField("lightcolor", 255));
        Assert.Equal("default", sector.GetStringField("comment", "default"));
        Assert.Empty(sector.Fields);
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
        line.SetArg(0, 7);
        line.SetArg(4, 99);

        Assert.Equal(7, line.GetArg(0));
        Assert.Equal(99, line.GetArg(4));

        line.ClearArgs();

        Assert.All(line.Args, arg => Assert.Equal(0, arg));
    }

    [Fact]
    public void ArgumentHelpersEnforceFiveSlotRange()
    {
        var thing = new Thing(new Vector2D(0, 0), 3001);

        Assert.Throws<ArgumentOutOfRangeException>(() => thing.GetArg(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => thing.SetArg(5, 1));
    }
}
