// ABOUTME: Tests UDB universal type handlers for field and argument value conversion.
// ABOUTME: Protects primitive, random, and text handler behavior before editor UI integration.

using DBuilder.IO;

namespace DBuilder.Tests;

public class UniversalTypeHandlersTests
{
    [Fact]
    public void IntegerHandlerCoercesValuesLikeUdb()
    {
        var handler = new UniversalTypeRegistry().CreateHandler(UniversalType.Integer, defaultValue: 7);

        Assert.IsType<IntegerTypeHandler>(handler);
        Assert.Equal(7, handler.DefaultValue);
        Assert.Equal(7, handler.GetIntValue());

        handler.SetValue(true);
        Assert.Equal(1, handler.GetValue());

        handler.SetValue("42");
        Assert.Equal(42, handler.GetIntValue());
        Assert.Equal("42", handler.GetStringValue());

        handler.SetValue("not an integer");
        Assert.Equal(0, handler.GetIntValue());

        handler.ApplyDefaultValue();
        Assert.Equal(7, handler.GetIntValue());
    }

    [Fact]
    public void FloatHandlerCoercesValuesLikeUdb()
    {
        var handler = new UniversalTypeRegistry().CreateHandler(UniversalType.Float, defaultValue: 1);

        Assert.IsType<FloatTypeHandler>(handler);
        Assert.Equal(1.0, handler.DefaultValue);
        Assert.Equal(1.0, handler.GetValue());

        handler.SetValue("2.5");
        Assert.Equal(2.5, handler.GetValue());
        Assert.Equal(2, handler.GetIntValue());

        handler.SetValue(false);
        Assert.Equal(0.0, handler.GetValue());

        handler.SetValue("not a decimal");
        Assert.Equal(0.0, handler.GetValue());
    }

    [Fact]
    public void BooleanHandlerCoercesValuesAndExposesEnumValuesLikeUdb()
    {
        var handler = (BooleanTypeHandler)new UniversalTypeRegistry().CreateHandler(UniversalType.Boolean);

        Assert.True(handler.IsEnumerable);
        Assert.True(handler.IsLimitedToEnums);
        Assert.Equal("True", handler.Values.GetByEnumIndex("true")!.Title);
        Assert.Equal("False", handler.Values.GetByEnumIndex("false")!.Title);

        handler.SetValue("true");
        Assert.True((bool)handler.GetValue());
        Assert.Equal(1, handler.GetIntValue());
        Assert.Equal("True", handler.GetStringValue());

        handler.SetValue("1");
        Assert.False((bool)handler.GetValue());

        handler.SetValue(2);
        Assert.True((bool)handler.GetValue());
    }

    [Fact]
    public void StringHandlerStoresTextWithoutQuotesLikeUdb()
    {
        var handler = new UniversalTypeRegistry().CreateHandler(UniversalType.String, defaultValue: "\"quoted\"");

        Assert.IsType<StringTypeHandler>(handler);
        Assert.True(handler.IsBrowseable);
        Assert.Equal("quoted", handler.DefaultValue);
        Assert.Equal("quoted", handler.GetValue());

        handler.SetValue("12");
        Assert.Equal("12", handler.GetValue());
        Assert.Equal(12, handler.GetIntValue());
        Assert.Equal("12", handler.GetStringValue());

        handler.SetValue("a \"quoted\" value");
        Assert.Equal("a quoted value", handler.GetValue());
        Assert.Equal(0, handler.GetIntValue());

        handler.SetValue(null);
        Assert.Equal("", handler.GetValue());
    }

    [Fact]
    public void TextureAndFlatHandlersStoreImageNamesLikeUdb()
    {
        var texture = (TextureTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.Texture, defaultValue: "STARTAN3");
        var flat = (FlatTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.Flat, defaultValue: "FLOOR0_1");

        Assert.True(texture.IsBrowseable);
        Assert.False(texture.BrowseFlats);
        Assert.Equal("STARTAN3", texture.GetValue());
        Assert.Equal("STARTAN3", texture.GetStringValue());

        texture.SetValue("\"QUOTED\"");
        Assert.Equal("\"QUOTED\"", texture.GetValue());

        texture.SetValue(null);
        Assert.Equal("", texture.GetValue());

        Assert.True(flat.IsBrowseable);
        Assert.True(flat.BrowseFlats);
        Assert.Equal("FLOOR0_1", flat.GetValue());

        flat.SetValue(123);
        Assert.Equal("123", flat.GetValue());
        Assert.Equal(123, flat.GetIntValue());
    }

    [Fact]
    public void ColorHandlerParsesAndFormatsColorsLikeUdb()
    {
        var handler = new UniversalTypeRegistry().CreateHandler(UniversalType.Color, defaultValue: "00ff80");

        Assert.IsType<ColorTypeHandler>(handler);
        Assert.True(handler.IsBrowseable);
        Assert.Equal(0x00FF80, handler.GetValue());
        Assert.Equal("00FF80", handler.GetStringValue());

        handler.SetValue("ff0000");
        Assert.Equal(0xFF0000, handler.GetIntValue());
        Assert.Equal("FF0000", handler.GetStringValue());

        handler.SetValue(255);
        Assert.Equal(255, handler.GetValue());
        Assert.Equal("0000FF", handler.GetStringValue());

        handler.SetValue(true);
        Assert.Equal(1, handler.GetIntValue());
        Assert.Equal("000001", handler.GetStringValue());

        handler.SetValue("not hex");
        Assert.Equal(0, handler.GetValue());
        Assert.Equal("000000", handler.GetStringValue());

        handler.SetValue(null);
        Assert.Equal(0, handler.GetValue());
    }

    [Fact]
    public void EnumOptionHandlerMatchesValueTitleAndNumericFallbackLikeUdb()
    {
        var values = GameConfiguration.FromText("""
            enums
            {
                speeds
                {
                    0 = "Slow";
                    16 = "Normal";
                    32 = "Fast";
                }
            }
            """).GetEnumList("speeds")!;
        var handler = (EnumOptionTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.EnumOption, defaultValue: 16, enumList: values);

        Assert.True(handler.IsBrowseable);
        Assert.True(handler.IsEnumerable);
        Assert.Equal((int)UniversalType.EnumOption, handler.Index);
        Assert.Equal(16, handler.GetValue());
        Assert.Equal("Normal", handler.GetStringValue());
        Assert.Same(values, handler.Values);

        handler.SetValue(32.0);
        Assert.Equal(32, handler.GetIntValue());
        Assert.Equal("Fast", handler.GetStringValue());

        handler.SetValue("0");
        Assert.Equal(0, handler.GetValue());
        Assert.Equal("Slow", handler.GetStringValue());

        handler.SetValue("normal");
        Assert.Equal(16, handler.GetValue());
        Assert.Equal("Normal", handler.GetStringValue());

        handler.SetValue("not listed");
        Assert.Equal(0, handler.GetValue());
        Assert.Equal("not listed", handler.GetStringValue());

        handler.SetValue(null);
        Assert.Equal(0, handler.GetValue());
        Assert.Equal("NULL", handler.GetStringValue());
    }

    [Fact]
    public void EnumBitsHandlerCoercesIntegerValuesLikeUdb()
    {
        var values = GameConfiguration.FromText("""
            enums
            {
                flags
                {
                    1 = "One";
                    2 = "Two";
                    4 = "Four";
                }
            }
            """).GetEnumList("flags")!;
        var handler = (EnumBitsTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.EnumBits, defaultValue: 3, enumList: values);

        Assert.True(handler.IsBrowseable);
        Assert.False(handler.IsEnumerable);
        Assert.Equal((int)UniversalType.EnumBits, handler.Index);
        Assert.Equal(3, handler.DefaultValue);
        Assert.Equal(3, handler.GetValue());
        Assert.Same(values, handler.Values);

        handler.SetValue(true);
        Assert.Equal(1, handler.GetIntValue());

        handler.SetValue("5");
        Assert.Equal(5, handler.GetValue());
        Assert.Equal("5", handler.GetStringValue());

        handler.SetValue("not bits");
        Assert.Equal(0, handler.GetValue());

        handler.ApplyDefaultValue();
        Assert.Equal(3, handler.GetValue());
    }

    [Fact]
    public void EnumStringsHandlerMatchesStringValuesAndTitlesLikeUdb()
    {
        var values = GameConfiguration.FromText("""
            enums
            {
                renderstyles
                {
                    Normal = "Normal";
                    Translucent = "Translucent";
                    Add = "Additive";
                    5 = "Numeric";
                }
            }
            """).GetEnumList("renderstyles")!;
        var handler = (EnumStringsTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.EnumStrings, defaultValue: "Add", enumList: values);

        Assert.True(handler.IsBrowseable);
        Assert.True(handler.IsEnumerable);
        Assert.Equal((int)UniversalType.EnumStrings, handler.Index);
        Assert.Equal("Add", handler.GetValue());
        Assert.Equal("Additive", handler.GetStringValue());
        Assert.Same(values, handler.Values);

        handler.SetValue("Translucent");
        Assert.Equal("Translucent", handler.GetValue());
        Assert.Equal("Translucent", handler.GetStringValue());

        handler.SetValue("normal");
        Assert.Equal("Normal", handler.GetValue());
        Assert.Equal("Normal", handler.GetStringValue());

        handler.SetValue("5");
        Assert.Equal("5", handler.GetValue());
        Assert.Equal(5, handler.GetIntValue());

        handler.SetValue("Unknown");
        Assert.Equal("Unknown", handler.GetValue());
        Assert.Equal("Unknown", handler.GetStringValue());

        handler.SetValue(null);
        Assert.Equal("", handler.GetValue());
        Assert.Equal("", handler.GetStringValue());
    }

    [Fact]
    public void RegistryCreatesPrimitiveHandlersForArgsAndUniversalFields()
    {
        var registry = new UniversalTypeRegistry();
        var arg = new ArgInfo { Type = (int)UniversalType.Integer, DefaultValue = 9 };
        var argHandler = registry.CreateArgumentHandler(arg);

        Assert.True(argHandler.IsForArgument);
        Assert.Equal(9, argHandler.GetIntValue());

        var field = new UniversalFieldInfo(
            "thing",
            "health",
            (int)UniversalType.Boolean,
            true,
            false,
            true,
            null,
            Array.Empty<EnumItemInfo>(),
            new Dictionary<string, UniversalFieldAssociationInfo>());
        var fieldHandler = registry.CreateFieldHandler(field);

        Assert.False(fieldHandler.IsForArgument);
        Assert.IsType<BooleanTypeHandler>(fieldHandler);
        Assert.True((bool)fieldHandler.GetValue());
    }

    [Fact]
    public void GameConfigurationCreatesEnumHandlersFromArgAndFieldMetadata()
    {
        const string cfg = """
            enums
            {
                speeds
                {
                    0 = "Slow";
                    16 = "Normal";
                }
                flags
                {
                    1 = "Silent";
                    2 = "Fog";
                }
                renderstyles
                {
                    Normal = "Normal";
                    Add = "Additive";
                }
            }
            linedeftypes
            {
                doors
                {
                    1
                    {
                        title = "Door";
                        arg0
                        {
                            title = "Speed";
                            type = 11;
                            enum = "speeds";
                            default = 16;
                        }
                        arg1
                        {
                            title = "Flags";
                            type = 12;
                            enum = "flags";
                            default = 3;
                        }
                        arg2
                        {
                            title = "Render style";
                            type = 16;
                            enum = "renderstyles";
                            default = "Add";
                        }
                    }
                }
            }
            universalfields
            {
                thing
                {
                    attitude
                    {
                        type = 11;
                        default = 1;
                        enum
                        {
                            0 = "Hostile";
                            1 = "Friendly";
                        }
                    }
                }
            }
            """;
        var config = GameConfiguration.FromText(cfg);

        var argHandler = (EnumOptionTypeHandler)config.CreateArgumentHandler(config.GetLinedefAction(1)!.Args[0]);
        Assert.Equal(16, argHandler.GetValue());
        Assert.Equal("Normal", argHandler.GetStringValue());

        var bitsHandler = (EnumBitsTypeHandler)config.CreateArgumentHandler(config.GetLinedefAction(1)!.Args[1]);
        Assert.Equal(3, bitsHandler.GetValue());
        Assert.Equal("Fog", bitsHandler.Values.GetByEnumIndex("2")!.Title);

        var stringsHandler = (EnumStringsTypeHandler)config.CreateArgumentHandler(config.GetLinedefAction(1)!.Args[2]);
        Assert.Equal("Add", stringsHandler.GetValue());
        Assert.Equal("Additive", stringsHandler.GetStringValue());

        var field = config.UniversalFields["thing"]["attitude"];
        var fieldHandler = (EnumOptionTypeHandler)config.CreateFieldHandler(field);
        Assert.Equal(1, fieldHandler.GetValue());
        Assert.Equal("Friendly", fieldHandler.GetStringValue());
        Assert.Equal("Hostile", config.GetFieldEnumList(field)!.GetByEnumIndex("0")!.Title);
    }

    [Fact]
    public void UnknownHandlerPreservesDisplayValueButReturnsStringValue()
    {
        var handler = new UniversalTypeRegistry().CreateHandler(99, defaultValue: "abc");

        Assert.IsType<NullTypeHandler>(handler);
        Assert.Equal("abc", handler.GetValue());
        Assert.Equal("abc", handler.GetStringValue());
        Assert.Equal(0, handler.GetIntValue());
    }

    [Fact]
    public void RandomIntegerHandlerParsesRangesAndStoresAsIntegerType()
    {
        var handler = (RandomIntegerTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.RandomInteger, defaultValue: 4);

        Assert.Equal((int)UniversalType.Integer, handler.Index);
        Assert.Equal("Integer (Random)", handler.TypeName);
        Assert.Equal(4, handler.DefaultValue);

        handler.SetValue("7 5");
        Assert.True(handler.HasRandomRange);
        Assert.Equal(5, handler.Min);
        Assert.Equal(7, handler.Max);

        for (int i = 0; i < 20; i++)
        {
            int value = handler.GetIntValue();
            Assert.InRange(value, 5, 7);
        }

        handler.SetValue("6 6");
        Assert.False(handler.HasRandomRange);
        Assert.Equal(6, handler.GetIntValue());
        Assert.Equal("6", handler.GetStringValue());
    }

    [Fact]
    public void RandomFloatHandlerParsesRangesAndStoresAsFloatType()
    {
        var handler = (RandomFloatTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.RandomFloat, defaultValue: 1);

        Assert.Equal((int)UniversalType.Float, handler.Index);
        Assert.Equal("Decimal (Random)", handler.TypeName);
        Assert.Equal(1.0, handler.DefaultValue);

        handler.SetValue("3.5 2.5");
        Assert.True(handler.HasRandomRange);
        Assert.Equal(2.5, handler.Min);
        Assert.Equal(3.5, handler.Max);

        for (int i = 0; i < 20; i++)
        {
            double value = (double)handler.GetValue();
            Assert.InRange(value, 2.5, 3.5);
            Assert.Equal(value, Math.Round(value, 2));
        }

        handler.SetValue("6.25 6.25");
        Assert.False(handler.HasRandomRange);
        Assert.Equal(6.25, handler.GetValue());
        Assert.Equal(6, handler.GetIntValue());
    }
}
