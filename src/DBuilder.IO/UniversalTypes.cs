// ABOUTME: Defines UDB universal field and argument type ids with their editor-facing metadata.
// ABOUTME: Provides a small registry equivalent to UDB's TypesManager attribute table.

namespace DBuilder.IO;

public enum UniversalType
{
    Integer = 0,
    Float = 1,
    String = 2,
    Boolean = 3,
    LinedefType = 4,
    SectorEffect = 5,
    Texture = 6,
    Flat = 7,
    AngleDegrees = 8,
    AngleRadians = 9,
    Color = 10,
    EnumOption = 11,
    EnumBits = 12,
    SectorTag = 13,
    ThingTag = 14,
    LinedefTag = 15,
    EnumStrings = 16,
    AngleDegreesFloat = 17,
    ThingType = 18,
    ThingClass = 19,
    RandomInteger = 20,
    RandomFloat = 21,
    AngleByte = 22,
    ThingRadius = 23,
    ThingHeight = 24,
    PolyobjectNumber = 25,
    EnumOptionAndBits = 26,
}

public sealed record UniversalTypeInfo(int Index, UniversalType Type, string Name, bool IsCustomUsable);

public sealed class UniversalTypeRegistry
{
    private static readonly UniversalTypeInfo UnknownType = new(-1, UniversalType.Integer, "Unknown", false);
    private readonly Dictionary<int, UniversalTypeInfo> byIndex = new();
    private readonly Dictionary<string, UniversalTypeInfo> byName = new(StringComparer.OrdinalIgnoreCase);

    public UniversalTypeRegistry()
    {
        Register(UniversalType.Integer, "Integer", true);
        Register(UniversalType.Float, "Decimal", true);
        Register(UniversalType.String, "Text", true);
        Register(UniversalType.Boolean, "Boolean", true);
        Register(UniversalType.LinedefType, "Linedef Action", false);
        Register(UniversalType.SectorEffect, "Sector Effect", false);
        Register(UniversalType.Texture, "Texture", false);
        Register(UniversalType.Flat, "Flat", false);
        Register(UniversalType.AngleDegrees, "Degrees (Integer)", false);
        Register(UniversalType.AngleRadians, "Radians", false);
        Register(UniversalType.Color, "Color", false);
        Register(UniversalType.EnumOption, "Setting", false);
        Register(UniversalType.EnumBits, "Options", false);
        Register(UniversalType.SectorTag, "Sector Tag", false);
        Register(UniversalType.ThingTag, "Thing Tag", false);
        Register(UniversalType.LinedefTag, "Linedef Tag", false);
        Register(UniversalType.EnumStrings, "Setting", false);
        Register(UniversalType.AngleDegreesFloat, "Degrees (Decimal)", false);
        Register(UniversalType.ThingType, "Thing Type", false);
        Register(UniversalType.ThingClass, "Thing Class", false);
        Register(UniversalType.RandomInteger, "Integer (Random)", false);
        Register(UniversalType.RandomFloat, "Decimal (Random)", false);
        Register(UniversalType.AngleByte, "Byte Angle", false);
        Register(UniversalType.ThingRadius, "Thing Radius", false);
        Register(UniversalType.ThingHeight, "Thing Height", false);
        Register(UniversalType.PolyobjectNumber, "Polyobject Number", false);
        Register(UniversalType.EnumOptionAndBits, "Options and Bits", false);
    }

    public IReadOnlyDictionary<int, UniversalTypeInfo> TypesByIndex => byIndex;

    public IReadOnlyList<UniversalTypeInfo> CustomUsableTypes
        => byIndex.Values.Where(t => t.IsCustomUsable).OrderBy(t => t.Index).ToArray();

    public UniversalTypeInfo Get(int index)
        => byIndex.TryGetValue(index, out var info) ? info : UnknownType;

    public UniversalTypeInfo Get(UniversalType type) => Get((int)type);

    public UniversalTypeInfo? GetByName(string name)
        => byName.TryGetValue(name, out var info) ? info : null;

    public bool IsKnown(int index) => byIndex.ContainsKey(index);

    public bool IsKnown(UniversalType type) => IsKnown((int)type);

    public UniversalTypeHandler CreateHandler(
        int index,
        object? defaultValue = null,
        bool isForArgument = false,
        EnumListInfo? enumList = null)
    {
        var info = Get(index);
        return info.Type switch
        {
            UniversalType.Integer when info.Index >= 0 => new IntegerTypeHandler(info, defaultValue, isForArgument),
            UniversalType.Float when info.Index >= 0 => new FloatTypeHandler(info, defaultValue, isForArgument),
            UniversalType.String when info.Index >= 0 => new StringTypeHandler(info, defaultValue, isForArgument),
            UniversalType.Boolean when info.Index >= 0 => new BooleanTypeHandler(info, defaultValue, isForArgument),
            UniversalType.LinedefType when info.Index >= 0 => new LinedefTypeHandler(info, defaultValue, isForArgument),
            UniversalType.SectorEffect when info.Index >= 0 => new SectorEffectTypeHandler(info, defaultValue, isForArgument),
            UniversalType.Texture when info.Index >= 0 => new TextureTypeHandler(info, defaultValue, isForArgument),
            UniversalType.Flat when info.Index >= 0 => new FlatTypeHandler(info, defaultValue, isForArgument),
            UniversalType.AngleDegrees when info.Index >= 0 => new AngleDegreesTypeHandler(info, defaultValue, isForArgument),
            UniversalType.AngleRadians when info.Index >= 0 => new AngleRadiansTypeHandler(info, defaultValue, isForArgument),
            UniversalType.Color when info.Index >= 0 => new ColorTypeHandler(info, defaultValue, isForArgument),
            UniversalType.EnumOption when info.Index >= 0 => new EnumOptionTypeHandler(info, defaultValue, isForArgument, enumList),
            UniversalType.EnumBits when info.Index >= 0 => new EnumBitsTypeHandler(info, defaultValue, isForArgument, enumList),
            UniversalType.SectorTag when info.Index >= 0 => new SectorTagTypeHandler(info, defaultValue, isForArgument, enumList),
            UniversalType.ThingTag when info.Index >= 0 => new ThingTagTypeHandler(info, defaultValue, isForArgument, enumList),
            UniversalType.LinedefTag when info.Index >= 0 => new LinedefTagTypeHandler(info, defaultValue, isForArgument, enumList),
            UniversalType.EnumStrings when info.Index >= 0 => new EnumStringsTypeHandler(info, defaultValue, isForArgument, enumList),
            UniversalType.AngleDegreesFloat when info.Index >= 0 => new AngleDegreesFloatTypeHandler(info, defaultValue, isForArgument),
            UniversalType.ThingType when info.Index >= 0 => new ThingTypeHandler(info, defaultValue, isForArgument),
            UniversalType.ThingClass when info.Index >= 0 => new ThingClassTypeHandler(info, defaultValue, isForArgument),
            UniversalType.RandomInteger when info.Index >= 0 => new RandomIntegerTypeHandler(info, defaultValue, isForArgument),
            UniversalType.RandomFloat when info.Index >= 0 => new RandomFloatTypeHandler(info, defaultValue, isForArgument),
            UniversalType.AngleByte when info.Index >= 0 => new AngleByteTypeHandler(info, defaultValue, isForArgument),
            UniversalType.ThingRadius when info.Index >= 0 => new ThingRadiusTypeHandler(info, defaultValue, isForArgument),
            UniversalType.PolyobjectNumber when info.Index >= 0 => new PolyobjectNumberTypeHandler(info, defaultValue, isForArgument, enumList),
            _ => new NullTypeHandler(info, defaultValue, isForArgument),
        };
    }

    public UniversalTypeHandler CreateHandler(
        UniversalType type,
        object? defaultValue = null,
        bool isForArgument = false,
        EnumListInfo? enumList = null)
        => CreateHandler((int)type, defaultValue, isForArgument, enumList);

    public UniversalTypeHandler CreateArgumentHandler(ArgInfo arg)
        => CreateHandler(arg.Type, arg.DefaultValue, isForArgument: true);

    public UniversalTypeHandler CreateFieldHandler(UniversalFieldInfo field)
        => CreateHandler(field.Type, field.DefaultValue, isForArgument: false);

    private void Register(UniversalType type, string name, bool customUsable)
    {
        var info = new UniversalTypeInfo((int)type, type, name, customUsable);
        byIndex[info.Index] = info;
        byName[name] = info;
    }
}
