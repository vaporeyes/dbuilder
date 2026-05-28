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

    private void Register(UniversalType type, string name, bool customUsable)
    {
        var info = new UniversalTypeInfo((int)type, type, name, customUsable);
        byIndex[info.Index] = info;
        byName[name] = info;
    }
}
