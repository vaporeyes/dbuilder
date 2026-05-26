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
