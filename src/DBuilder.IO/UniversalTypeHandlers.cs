// ABOUTME: Provides UI-independent value handlers for UDB universal field and argument types.
// ABOUTME: Mirrors UDB TypeHandler conversion behavior so map fields can be validated before editor UI integration.

using System.Globalization;

namespace DBuilder.IO;

public abstract class UniversalTypeHandler
{
    protected UniversalTypeHandler(UniversalTypeInfo typeInfo, object? defaultValue, bool isForArgument)
    {
        TypeInfo = typeInfo;
        IsForArgument = isForArgument;
        DefaultValue = CoerceDefault(defaultValue);
        SetValue(DefaultValue);
    }

    public UniversalTypeInfo TypeInfo { get; }
    public int Index => TypeInfo.Index;
    public string TypeName => TypeInfo.Name;
    public bool IsCustomUsable => TypeInfo.IsCustomUsable;
    public bool IsForArgument { get; }
    public object DefaultValue { get; protected set; }

    public abstract void SetValue(object? value);
    public abstract object GetValue();
    public abstract int GetIntValue();
    public abstract string GetStringValue();

    public virtual void ApplyDefaultValue() => SetValue(DefaultValue);

    protected virtual object CoerceDefault(object? value) => value ?? 0;
}

public sealed class IntegerTypeHandler : UniversalTypeHandler
{
    private int value;

    public IntegerTypeHandler(UniversalTypeInfo typeInfo, object? defaultValue = null, bool isForArgument = false)
        : base(typeInfo, defaultValue, isForArgument)
    {
    }

    public override void SetValue(object? value) => this.value = ToInt(value);

    public override object GetValue() => value;

    public override int GetIntValue() => value;

    public override string GetStringValue() => value.ToString(CultureInfo.CurrentCulture);

    protected override object CoerceDefault(object? value) => ToInt(value);

    private static int ToInt(object? value)
    {
        if (value == null) return 0;
        if (value is int or float or double or bool) return Convert.ToInt32(value, CultureInfo.CurrentCulture);
        return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.CurrentCulture, out int parsed) ? parsed : 0;
    }
}

public sealed class FloatTypeHandler : UniversalTypeHandler
{
    private double value;

    public FloatTypeHandler(UniversalTypeInfo typeInfo, object? defaultValue = null, bool isForArgument = false)
        : base(typeInfo, defaultValue, isForArgument)
    {
    }

    public override void SetValue(object? value) => this.value = ToDouble(value);

    public override object GetValue() => value;

    public override int GetIntValue() => (int)value;

    public override string GetStringValue() => value.ToString(CultureInfo.CurrentCulture);

    protected override object CoerceDefault(object? value) => ToDouble(value);

    private static double ToDouble(object? value)
    {
        if (value == null) return 0.0;
        if (value is int or float or double or bool) return Convert.ToDouble(value, CultureInfo.CurrentCulture);
        return double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.CurrentCulture, out double parsed) ? parsed : 0.0;
    }
}

public sealed class BooleanTypeHandler : UniversalTypeHandler
{
    private bool value;
    private static readonly EnumListInfo values = CreateValues();

    public BooleanTypeHandler(UniversalTypeInfo typeInfo, object? defaultValue = null, bool isForArgument = false)
        : base(typeInfo, defaultValue, isForArgument)
    {
    }

    public bool IsEnumerable => true;
    public bool IsLimitedToEnums => true;
    public EnumListInfo Values => values;

    public override void SetValue(object? value) => this.value = ToBool(value);

    public override object GetValue() => value;

    public override int GetIntValue() => value ? 1 : 0;

    public override string GetStringValue() => value.ToString(CultureInfo.CurrentCulture);

    protected override object CoerceDefault(object? value) => ToBool(value);

    private static bool ToBool(object? value)
    {
        if (value == null) return false;
        if (value is int or float or double or bool) return Convert.ToBoolean(value, CultureInfo.CurrentCulture);
        return value.ToString()?.StartsWith("t", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static EnumListInfo CreateValues()
    {
        var list = new EnumListInfo("boolean");
        list.Add(new EnumItemInfo("true", "True"));
        list.Add(new EnumItemInfo("false", "False"));
        return list;
    }
}

public sealed class NullTypeHandler : UniversalTypeHandler
{
    private object value = 0;

    public NullTypeHandler(UniversalTypeInfo typeInfo, object? defaultValue = null, bool isForArgument = false)
        : base(typeInfo, defaultValue, isForArgument)
    {
    }

    public override void SetValue(object? value) => this.value = value ?? 0;

    public override object GetValue() => value.ToString() ?? "";

    public override int GetIntValue()
        => int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.CurrentCulture, out int parsed) ? parsed : 0;

    public override string GetStringValue() => value.ToString() ?? "";
}
