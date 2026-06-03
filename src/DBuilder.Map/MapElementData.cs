// ABOUTME: Typed access helpers for map element custom fields and action arguments.
// ABOUTME: Keeps raw UDMF field dictionaries and five-slot argument arrays behind consistent APIs.

using System.Globalization;

namespace DBuilder.Map;

public static class MapElementData
{
    public static bool TryGetField<T>(this IFielded element, string key, out T value)
    {
        if (element.Fields.TryGetValue(key, out var raw) && TryConvert(raw, out value)) return true;
        value = default!;
        return false;
    }

    public static T GetField<T>(this IFielded element, string key, T defaultValue = default!)
        => element.TryGetField<T>(key, out var value) ? value : defaultValue;

    public static void SetField(this IFielded element, string key, object value)
        => element.Fields[key] = value;

    public static bool RemoveField(this IFielded element, string key)
        => element.Fields.Remove(key);

    public static double GetFloatField(this IFielded element, string key, double defaultValue = 0.0)
        => element.GetField(key, defaultValue);

    public static void SetFloatField(this IFielded element, string key, double value, double defaultValue = 0.0)
        => SetDefaultOmittingField(element, key, value, defaultValue);

    public static int GetIntegerField(this IFielded element, string key, int defaultValue = 0)
        => element.GetField(key, defaultValue);

    public static void SetIntegerField(this IFielded element, string key, int value, int defaultValue = 0)
        => SetDefaultOmittingField(element, key, value, defaultValue);

    public static string GetStringField(this IFielded element, string key, string defaultValue = "")
        => element.GetField(key, defaultValue);

    public static void SetStringField(this IFielded element, string key, string value, string defaultValue = "")
        => SetDefaultOmittingField(element, key, value, defaultValue);

    public static void RemoveFields(this IFielded element, IEnumerable<string> keys)
    {
        foreach (var key in keys) element.Fields.Remove(key);
    }

    public static bool FieldsMatch(this IFielded left, IFielded right)
    {
        if (left.Fields.Count != right.Fields.Count) return false;
        foreach (var (key, value) in left.Fields)
        {
            if (!right.Fields.TryGetValue(key, out var otherValue) || !FieldValuesMatch(value, otherValue)) return false;
        }

        return true;
    }

    public static bool FieldValueMatches(this IFielded left, IFielded right, string key)
    {
        var leftHasValue = left.Fields.TryGetValue(key, out var leftValue);
        var rightHasValue = right.Fields.TryGetValue(key, out var rightValue);
        if (!leftHasValue && !rightHasValue) return true;
        return leftHasValue == rightHasValue && FieldValuesMatch(leftValue!, rightValue!);
    }

    public static int GetArg(this IHasArguments element, int index)
        => element.Args[CheckedArgIndex(index)];

    public static void SetArg(this IHasArguments element, int index, int value)
        => element.Args[CheckedArgIndex(index)] = value;

    public static void ClearArgs(this IHasArguments element)
    {
        for (int i = 0; i < element.Args.Length; i++) element.Args[i] = 0;
    }

    private static int CheckedArgIndex(int index)
    {
        if ((uint)index >= 5) throw new ArgumentOutOfRangeException(nameof(index), "Map elements have five arguments indexed 0 through 4.");
        return index;
    }

    private static void SetDefaultOmittingField<T>(IFielded element, string key, T value, T defaultValue)
    {
        if (EqualityComparer<T>.Default.Equals(value, defaultValue))
        {
            element.Fields.Remove(key);
            return;
        }

        element.Fields[key] = value!;
    }

    private static bool FieldValuesMatch(object left, object right)
        => left.GetType() == right.GetType() && left.Equals(right);

    private static bool TryConvert<T>(object raw, out T value)
    {
        if (raw is T typed)
        {
            value = typed;
            return true;
        }

        var target = typeof(T);
        if (target == typeof(string))
        {
            value = (T)(object)Convert.ToString(raw, CultureInfo.InvariantCulture)!;
            return true;
        }

        if (target == typeof(int) && raw is double d && d >= int.MinValue && d <= int.MaxValue && Math.Truncate(d) == d)
        {
            value = (T)(object)(int)d;
            return true;
        }

        if (target == typeof(int) && raw is long l && l >= int.MinValue && l <= int.MaxValue)
        {
            value = (T)(object)(int)l;
            return true;
        }

        if (target == typeof(double) && raw is int i)
        {
            value = (T)(object)(double)i;
            return true;
        }

        if (target == typeof(double) && raw is long whole)
        {
            value = (T)(object)(double)whole;
            return true;
        }

        value = default!;
        return false;
    }
}
