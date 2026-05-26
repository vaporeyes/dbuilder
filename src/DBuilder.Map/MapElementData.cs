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

        if (target == typeof(double) && raw is int i)
        {
            value = (T)(object)(double)i;
            return true;
        }

        value = default!;
        return false;
    }
}
