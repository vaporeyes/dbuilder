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
    public virtual int Index => TypeInfo.Index;
    public string TypeName => TypeInfo.Name;
    public bool IsCustomUsable => TypeInfo.IsCustomUsable;
    public bool IsForArgument { get; }
    public object DefaultValue { get; protected set; }
    public virtual bool IsBrowseable => false;
    public virtual bool IsEnumerable => false;
    public virtual bool IsLimitedToEnums => false;

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

    public override bool IsEnumerable => true;
    public override bool IsLimitedToEnums => true;
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

public sealed class StringTypeHandler : UniversalTypeHandler
{
    private string value = "";

    public StringTypeHandler(UniversalTypeInfo typeInfo, object? defaultValue = null, bool isForArgument = false)
        : base(typeInfo, defaultValue, isForArgument)
    {
    }

    public override bool IsBrowseable => true;

    public override void SetValue(object? value)
        => this.value = value?.ToString()?.Replace("\"", "") ?? "";

    public override object GetValue() => value;

    public override int GetIntValue()
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out int parsed) ? parsed : 0;

    public override string GetStringValue() => value;

    protected override object CoerceDefault(object? value)
        => value?.ToString()?.Replace("\"", "") ?? "";
}

public sealed class EnumOptionTypeHandler : UniversalTypeHandler
{
    private EnumListInfo values = new("");
    private EnumItemInfo value = new("0", "NULL");

    public EnumOptionTypeHandler(
        UniversalTypeInfo typeInfo,
        object? defaultValue = null,
        bool isForArgument = false,
        EnumListInfo? values = null)
        : base(typeInfo, defaultValue, isForArgument)
    {
        this.values = values ?? new EnumListInfo("");
        SetValue(DefaultValue);
    }

    public override bool IsBrowseable => true;
    public override bool IsEnumerable => true;
    public EnumListInfo Values => values;

    public override void SetValue(object? value)
    {
        if (value == null)
        {
            this.value = new EnumItemInfo("0", "NULL");
            return;
        }

        if (value is int or float or double or bool)
        {
            int intValue = Convert.ToInt32(value, CultureInfo.CurrentCulture);
            foreach (EnumItemInfo item in values.Items)
            {
                if (item.GetIntValue() == intValue)
                {
                    this.value = item;
                    return;
                }
            }
        }

        string text = value.ToString() ?? "";
        foreach (EnumItemInfo item in values.Items)
        {
            if (item.Value == text)
            {
                this.value = item;
                return;
            }
        }

        foreach (EnumItemInfo item in values.Items)
        {
            if (string.Equals(item.Title, text, StringComparison.OrdinalIgnoreCase))
            {
                this.value = item;
                return;
            }
        }

        var dummy = new EnumItemInfo(text, text);
        this.value = new EnumItemInfo(dummy.GetIntValue().ToString(CultureInfo.InvariantCulture), text);
    }

    public override object GetValue() => GetIntValue();

    public override int GetIntValue() => value.GetIntValue();

    public override string GetStringValue() => value.Title;

    protected override object CoerceDefault(object? value)
        => value?.ToString() ?? "0";
}

public sealed class EnumBitsTypeHandler : UniversalTypeHandler
{
    private readonly EnumListInfo values;
    private int value;

    public EnumBitsTypeHandler(
        UniversalTypeInfo typeInfo,
        object? defaultValue = null,
        bool isForArgument = false,
        EnumListInfo? values = null)
        : base(typeInfo, defaultValue, isForArgument)
    {
        this.values = values ?? new EnumListInfo("");
        SetValue(DefaultValue);
    }

    public override bool IsBrowseable => true;
    public EnumListInfo Values => values;

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

public sealed class EnumStringsTypeHandler : UniversalTypeHandler
{
    private EnumListInfo values = new("");
    private EnumItemInfo value = new("", "");

    public EnumStringsTypeHandler(
        UniversalTypeInfo typeInfo,
        object? defaultValue = null,
        bool isForArgument = false,
        EnumListInfo? values = null)
        : base(typeInfo, defaultValue, isForArgument)
    {
        this.values = values ?? new EnumListInfo("");
        SetValue(DefaultValue);
    }

    public override bool IsBrowseable => true;
    public override bool IsEnumerable => true;
    public EnumListInfo Values => values;

    public override void SetValue(object? value)
    {
        if (value == null)
        {
            this.value = new EnumItemInfo("", "");
            return;
        }

        string text = value.ToString() ?? "";
        foreach (EnumItemInfo item in values.Items)
        {
            if (item.Value == text)
            {
                this.value = item;
                return;
            }
        }

        foreach (EnumItemInfo item in values.Items)
        {
            if (string.Equals(item.Title, text, StringComparison.OrdinalIgnoreCase))
            {
                this.value = item;
                return;
            }
        }

        this.value = new EnumItemInfo(text, text);
    }

    public override object GetValue() => value.Value;

    public override int GetIntValue() => value.GetIntValue();

    public override string GetStringValue() => value.Title;

    protected override object CoerceDefault(object? value) => value?.ToString() ?? "0";
}

public sealed class RandomIntegerTypeHandler : UniversalTypeHandler
{
    private int value;
    private bool hasRandomRange;
    private int min;
    private int max;

    public RandomIntegerTypeHandler(UniversalTypeInfo typeInfo, object? defaultValue = null, bool isForArgument = false)
        : base(typeInfo, defaultValue, isForArgument)
    {
    }

    public override int Index => (int)UniversalType.Integer;

    public bool HasRandomRange => hasRandomRange;
    public int Min => min;
    public int Max => max;

    public override void SetValue(object? value)
    {
        hasRandomRange = false;
        if (TryDirectInt(value, out int parsed))
        {
            this.value = parsed;
            return;
        }

        if (TryParseRange(value, out int parsedMin, out int parsedMax))
        {
            min = Math.Min(parsedMin, parsedMax);
            max = Math.Max(parsedMin, parsedMax);
            if (min == max)
            {
                this.value = min;
                return;
            }

            hasRandomRange = true;
        }

        this.value = 0;
    }

    public override object GetValue() => GetIntValue();

    public override int GetIntValue() => hasRandomRange ? Random.Shared.Next(min, max + 1) : value;

    public override string GetStringValue() => GetIntValue().ToString(CultureInfo.InvariantCulture);

    protected override object CoerceDefault(object? value)
        => TryDirectInt(value, out int parsed) ? parsed : 0;

    private static bool TryDirectInt(object? value, out int parsed)
    {
        if (value == null)
        {
            parsed = 0;
            return true;
        }
        if (value is int or float or double or bool)
        {
            parsed = Convert.ToInt32(value, CultureInfo.CurrentCulture);
            return true;
        }
        return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed);
    }

    private static bool TryParseRange(object? value, out int min, out int max)
    {
        min = 0;
        max = 0;
        string[] parts = (value?.ToString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.CurrentCulture, out min)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.CurrentCulture, out max);
    }
}

public sealed class RandomFloatTypeHandler : UniversalTypeHandler
{
    private double value;
    private bool hasRandomRange;
    private double min;
    private double max;

    public RandomFloatTypeHandler(UniversalTypeInfo typeInfo, object? defaultValue = null, bool isForArgument = false)
        : base(typeInfo, defaultValue, isForArgument)
    {
    }

    public override int Index => (int)UniversalType.Float;

    public bool HasRandomRange => hasRandomRange;
    public double Min => min;
    public double Max => max;

    public override void SetValue(object? value)
    {
        hasRandomRange = false;
        if (TryDirectDouble(value, out double parsed))
        {
            this.value = parsed;
            return;
        }

        if (TryParseRange(value, out double parsedMin, out double parsedMax))
        {
            min = Math.Min(parsedMin, parsedMax);
            max = Math.Max(parsedMin, parsedMax);
            if (Math.Abs(min - max) < double.Epsilon)
            {
                this.value = min;
                return;
            }

            hasRandomRange = true;
        }

        this.value = 0.0;
    }

    public override object GetValue() => RandomValue();

    public override int GetIntValue() => (int)RandomValue();

    public override string GetStringValue() => RandomValue().ToString(CultureInfo.InvariantCulture);

    protected override object CoerceDefault(object? value)
        => TryDirectDouble(value, out double parsed) ? parsed : 0.0;

    private double RandomValue()
        => hasRandomRange ? Math.Round(min + ((max - min) * Random.Shared.NextDouble()), 2) : value;

    private static bool TryDirectDouble(object? value, out double parsed)
    {
        if (value == null)
        {
            parsed = 0.0;
            return true;
        }
        if (value is int or float or double or bool)
        {
            parsed = Convert.ToDouble(value, CultureInfo.CurrentCulture);
            return true;
        }
        return double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.CurrentCulture, out parsed);
    }

    private static bool TryParseRange(object? value, out double min, out double max)
    {
        min = 0.0;
        max = 0.0;
        string[] parts = (value?.ToString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.CurrentCulture, out min)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.CurrentCulture, out max);
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
