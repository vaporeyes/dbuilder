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
    public void AngleDegreesHandlerCoercesIntegerAnglesLikeUdb()
    {
        var handler = (AngleDegreesTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.AngleDegrees, defaultValue: 90);

        Assert.True(handler.IsBrowseable);
        Assert.True(handler.HasDynamicImage);
        Assert.Equal(90, handler.DefaultValue);
        Assert.Equal(90, handler.GetValue());
        Assert.Equal("90", handler.GetStringValue());
        Assert.Equal(2, handler.AnglePreviewIndex);

        handler.SetValue("180");
        Assert.Equal(180, handler.GetIntValue());
        Assert.Equal(4, handler.AnglePreviewIndex);

        handler.SetValue(true);
        Assert.Equal(1, handler.GetValue());
        Assert.Equal(0, handler.AnglePreviewIndex);

        handler.SetValue("not an angle");
        Assert.Equal(0, handler.GetValue());
    }

    [Fact]
    public void AngleDecimalHandlersCoerceValuesLikeUdb()
    {
        var degrees = (AngleDegreesFloatTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.AngleDegreesFloat, defaultValue: "45.5");
        var radians = (AngleRadiansTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.AngleRadians, defaultValue: Math.PI);

        Assert.True(degrees.IsBrowseable);
        Assert.True(degrees.HasDynamicImage);
        Assert.Equal(45.5f, degrees.DefaultValue);
        Assert.Equal(45.5f, degrees.GetValue());
        Assert.Equal(45, degrees.GetIntValue());
        Assert.Equal(1, degrees.AnglePreviewIndex);

        degrees.SetValue("90.25");
        Assert.Equal(90.25f, degrees.GetValue());
        Assert.Equal(90, degrees.GetIntValue());
        Assert.Equal(2, degrees.AnglePreviewIndex);

        Assert.True(radians.IsBrowseable);
        Assert.True(radians.HasDynamicImage);
        Assert.Equal(Convert.ToSingle(Math.PI), radians.DefaultValue);
        Assert.Equal(Convert.ToDouble(Convert.ToSingle(Math.PI)), radians.GetValue());
        Assert.Equal(3, radians.GetIntValue());
        Assert.Equal(2, radians.AnglePreviewIndex);

        radians.SetValue("not radians");
        Assert.Equal(0.0, radians.GetValue());
        Assert.Equal(6, radians.AnglePreviewIndex);
    }

    [Fact]
    public void AngleByteHandlerStoresByteAngleAndPreviewLikeUdb()
    {
        var handler = (AngleByteTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.AngleByte, defaultValue: 64);

        Assert.True(handler.IsBrowseable);
        Assert.True(handler.HasDynamicImage);
        Assert.Equal(64, handler.GetValue());
        Assert.Equal("64", handler.GetStringValue());
        Assert.Equal(2, handler.AnglePreviewIndex);

        handler.SetValue("128");
        Assert.Equal(128, handler.GetIntValue());
        Assert.Equal(4, handler.AnglePreviewIndex);

        handler.SetValue(false);
        Assert.Equal(0, handler.GetValue());
        Assert.Equal(0, handler.AnglePreviewIndex);
    }

    [Fact]
    public void ThingTypeHandlerCoercesIntegerValuesLikeUdb()
    {
        var handler = (ThingTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.ThingType, defaultValue: 3001);

        Assert.True(handler.IsBrowseable);
        Assert.Equal(3001, handler.DefaultValue);
        Assert.Equal(3001, handler.GetValue());
        Assert.Equal("3001", handler.GetStringValue());

        handler.SetValue("2001");
        Assert.Equal(2001, handler.GetIntValue());

        handler.SetValue(true);
        Assert.Equal(1, handler.GetValue());

        handler.SetValue("not a thing type");
        Assert.Equal(0, handler.GetValue());

        handler.ApplyDefaultValue();
        Assert.Equal(3001, handler.GetValue());
    }

    [Fact]
    public void ThingClassHandlerStoresClassNamesLikeUdb()
    {
        var handler = (ThingClassTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.ThingClass, defaultValue: "DoomImp");

        Assert.True(handler.IsBrowseable);
        Assert.Equal("DoomImp", handler.DefaultValue);
        Assert.Equal("DoomImp", handler.GetValue());
        Assert.Equal("DoomImp", handler.GetStringValue());

        handler.SetValue("\"QuotedClass\"");
        Assert.Equal("\"QuotedClass\"", handler.GetValue());

        handler.SetValue(3001);
        Assert.Equal("3001", handler.GetValue());

        handler.SetValue(null);
        Assert.Equal("", handler.GetValue());

        Assert.Throws<NotSupportedException>(() => handler.GetIntValue());
    }

    [Fact]
    public void ThingRadiusHandlerCoercesIntegerValuesLikeUdb()
    {
        var handler = (ThingRadiusTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.ThingRadius, defaultValue: 20);

        Assert.Equal((int)UniversalType.ThingRadius, handler.Index);
        Assert.Equal(20, handler.DefaultValue);
        Assert.Equal(20, handler.GetValue());
        Assert.Equal("20", handler.GetStringValue());

        handler.SetValue("32");
        Assert.Equal(32, handler.GetIntValue());

        handler.SetValue(false);
        Assert.Equal(0, handler.GetValue());

        handler.SetValue("not a radius");
        Assert.Equal(0, handler.GetValue());
    }

    [Fact]
    public void ThingTagHandlerMatchesEnumValuesAndTitlesLikeUdb()
    {
        var values = GameConfiguration.FromText("""
            enums
            {
                thingtags
                {
                    7 = "Teleport target";
                    11 = "Ambush group";
                }
            }
            """).GetEnumList("thingtags")!;
        var handler = (ThingTagTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.ThingTag, defaultValue: 7, enumList: values);

        Assert.True(handler.IsEnumerable);
        Assert.Equal(7, handler.DefaultValue);
        Assert.Equal(7, handler.GetValue());
        Assert.Equal("Teleport target", handler.GetStringValue());
        Assert.Same(values, handler.Values);

        handler.SetValue("11");
        Assert.Equal(11, handler.GetIntValue());
        Assert.Equal("Ambush group", handler.GetStringValue());

        handler.SetValue("teleport target");
        Assert.Equal(7, handler.GetValue());
        Assert.Equal("Teleport target", handler.GetStringValue());

        handler.SetValue("13");
        Assert.Equal(13, handler.GetValue());
        Assert.Equal("13", handler.GetStringValue());

        handler.SetValue("not a tag");
        Assert.Equal(0, handler.GetValue());
        Assert.Equal("not a tag", handler.GetStringValue());

        handler.SetValue(null);
        Assert.Equal(0, handler.GetValue());
        Assert.Equal("0", handler.GetStringValue());
    }

    [Fact]
    public void LinedefTypeHandlerCoercesIntegerValuesLikeUdb()
    {
        var handler = (LinedefTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.LinedefType, defaultValue: 11);

        Assert.True(handler.IsBrowseable);
        Assert.Equal(11, handler.DefaultValue);
        Assert.Equal(11, handler.GetValue());
        Assert.Equal("11", handler.GetStringValue());

        handler.SetValue("80");
        Assert.Equal(80, handler.GetIntValue());

        handler.SetValue(true);
        Assert.Equal(1, handler.GetValue());

        handler.SetValue("not an action");
        Assert.Equal(0, handler.GetValue());

        handler.ApplyDefaultValue();
        Assert.Equal(11, handler.GetValue());
    }

    [Fact]
    public void LinedefTagHandlerMatchesEnumValuesAndTitlesLikeUdb()
    {
        var values = GameConfiguration.FromText("""
            enums
            {
                linetags
                {
                    3 = "Door sector";
                    9 = "Lift sector";
                }
            }
            """).GetEnumList("linetags")!;
        var handler = (LinedefTagTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.LinedefTag, defaultValue: 3, enumList: values);

        Assert.True(handler.IsEnumerable);
        Assert.Equal(3, handler.DefaultValue);
        Assert.Equal(3, handler.GetValue());
        Assert.Equal("Door sector", handler.GetStringValue());
        Assert.Same(values, handler.Values);

        handler.SetValue("9");
        Assert.Equal(9, handler.GetIntValue());
        Assert.Equal("Lift sector", handler.GetStringValue());

        handler.SetValue("door sector");
        Assert.Equal(3, handler.GetValue());
        Assert.Equal("Door sector", handler.GetStringValue());

        handler.SetValue("15");
        Assert.Equal(15, handler.GetValue());
        Assert.Equal("15", handler.GetStringValue());

        handler.SetValue("not a tag");
        Assert.Equal(0, handler.GetValue());
        Assert.Equal("not a tag", handler.GetStringValue());
    }

    [Fact]
    public void SectorEffectHandlerCoercesIntegerValuesLikeUdb()
    {
        var handler = (SectorEffectTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.SectorEffect, defaultValue: 5);

        Assert.True(handler.IsBrowseable);
        Assert.Equal(5, handler.DefaultValue);
        Assert.Equal(5, handler.GetValue());
        Assert.Equal("5", handler.GetStringValue());

        handler.SetValue("16");
        Assert.Equal(16, handler.GetIntValue());

        handler.SetValue(true);
        Assert.Equal(1, handler.GetValue());

        handler.SetValue("not an effect");
        Assert.Equal(0, handler.GetValue());

        handler.ApplyDefaultValue();
        Assert.Equal(5, handler.GetValue());
    }

    [Fact]
    public void SectorTagHandlerMatchesEnumValuesAndTitlesLikeUdb()
    {
        var values = GameConfiguration.FromText("""
            enums
            {
                sectortags
                {
                    2 = "2: Secret room";
                    8 = "8: Exit platform";
                }
            }
            """).GetEnumList("sectortags")!;
        var handler = (SectorTagTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.SectorTag, defaultValue: 2, enumList: values);

        Assert.True(handler.IsEnumerable);
        Assert.Equal(2, handler.DefaultValue);
        Assert.Equal(2, handler.GetValue());
        Assert.Equal("2: Secret room", handler.GetStringValue());
        Assert.Same(values, handler.Values);

        handler.SetValue("8");
        Assert.Equal(8, handler.GetIntValue());
        Assert.Equal("8: Exit platform", handler.GetStringValue());

        handler.SetValue("2: secret room");
        Assert.Equal(2, handler.GetValue());
        Assert.Equal("2: Secret room", handler.GetStringValue());

        handler.SetValue("12");
        Assert.Equal(12, handler.GetValue());
        Assert.Equal("12", handler.GetStringValue());

        handler.SetValue("not a tag");
        Assert.Equal(0, handler.GetValue());
        Assert.Equal("not a tag", handler.GetStringValue());
    }

    [Fact]
    public void PolyobjectNumberHandlerMatchesEnumValuesAndNumericFallbackLikeUdb()
    {
        var values = GameConfiguration.FromText("""
            enums
            {
                polyobjects
                {
                    1 = "1";
                    16 = "16";
                }
            }
            """).GetEnumList("polyobjects")!;
        var handler = (PolyobjectNumberTypeHandler)new UniversalTypeRegistry()
            .CreateHandler(UniversalType.PolyobjectNumber, defaultValue: 16, enumList: values);

        Assert.True(handler.IsEnumerable);
        Assert.Equal(16, handler.DefaultValue);
        Assert.Equal(16, handler.GetValue());
        Assert.Equal("16", handler.GetStringValue());
        Assert.Same(values, handler.Values);

        handler.SetValue("1");
        Assert.Equal(1, handler.GetIntValue());
        Assert.Equal("1", handler.GetStringValue());

        handler.SetValue("24");
        Assert.Equal(24, handler.GetValue());
        Assert.Equal("24", handler.GetStringValue());

        handler.SetValue("not a polyobject");
        Assert.Equal(0, handler.GetValue());
        Assert.Equal("not a polyobject", handler.GetStringValue());

        handler.SetValue(null);
        Assert.Equal(0, handler.GetValue());
        Assert.Equal("0", handler.GetStringValue());
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
