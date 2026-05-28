// ABOUTME: Tests the DBuilder universal type registry against UDB's type ids and names.
// ABOUTME: Keeps custom field type metadata stable for future property editing handlers.

using DBuilder.IO;

namespace DBuilder.Tests;

public class UniversalTypesTests
{
    [Fact]
    public void RegistersUdbUniversalTypeIdsAndNames()
    {
        var types = new UniversalTypeRegistry();

        Assert.Equal(0, types.Get(UniversalType.Integer).Index);
        Assert.Equal("Integer", types.Get(0).Name);
        Assert.Equal("Decimal", types.Get(1).Name);
        Assert.Equal("Text", types.Get(2).Name);
        Assert.Equal("Boolean", types.Get(3).Name);
        Assert.Equal("Linedef Action", types.Get(4).Name);
        Assert.Equal("Sector Effect", types.Get(5).Name);
        Assert.Equal("Texture", types.Get(6).Name);
        Assert.Equal("Flat", types.Get(7).Name);
        Assert.Equal("Degrees (Integer)", types.Get(8).Name);
        Assert.Equal("Radians", types.Get(9).Name);
        Assert.Equal("Color", types.Get(10).Name);
        Assert.Equal("Setting", types.Get(11).Name);
        Assert.Equal("Options", types.Get(12).Name);
        Assert.Equal("Sector Tag", types.Get(13).Name);
        Assert.Equal("Thing Tag", types.Get(14).Name);
        Assert.Equal("Linedef Tag", types.Get(15).Name);
        Assert.Equal("Setting", types.Get(16).Name);
        Assert.Equal("Degrees (Decimal)", types.Get(17).Name);
        Assert.Equal("Thing Type", types.Get(18).Name);
        Assert.Equal("Thing Class", types.Get(19).Name);
        Assert.Equal("Integer (Random)", types.Get(20).Name);
        Assert.Equal("Decimal (Random)", types.Get(21).Name);
        Assert.Equal("Byte Angle", types.Get(22).Name);
        Assert.Equal("Thing Radius", types.Get(23).Name);
        Assert.Equal("Thing Height", types.Get(24).Name);
        Assert.Equal("Polyobject Number", types.Get(25).Name);
        Assert.Equal("Options and Bits", types.Get(26).Name);
    }

    [Fact]
    public void ExposesOnlyUdbCustomUsableTypes()
    {
        var types = new UniversalTypeRegistry();

        Assert.Equal(
            new[] { 0, 1, 2, 3 },
            types.CustomUsableTypes.Select(t => t.Index).ToArray());
        Assert.True(types.Get(UniversalType.Integer).IsCustomUsable);
        Assert.True(types.Get(UniversalType.Float).IsCustomUsable);
        Assert.True(types.Get(UniversalType.String).IsCustomUsable);
        Assert.True(types.Get(UniversalType.Boolean).IsCustomUsable);
        Assert.False(types.Get(UniversalType.EnumOption).IsCustomUsable);
    }

    [Fact]
    public void ResolvesByNameAndFallsBackForUnknownTypes()
    {
        var types = new UniversalTypeRegistry();

        Assert.Equal(UniversalType.ThingType, types.GetByName("thing type")!.Type);
        Assert.True(types.IsKnown(UniversalType.AngleByte));
        Assert.False(types.IsKnown(99));

        var unknown = types.Get(99);
        Assert.Equal(-1, unknown.Index);
        Assert.Equal("Unknown", unknown.Name);
        Assert.False(unknown.IsCustomUsable);
    }

    [Fact]
    public void GameConfigurationExposesTypeRegistry()
    {
        var config = GameConfiguration.FromText("");

        Assert.Equal("Integer", config.Types.Get(0).Name);
        Assert.Equal(UniversalType.String, config.Types.GetByName("Text")!.Type);
    }
}
