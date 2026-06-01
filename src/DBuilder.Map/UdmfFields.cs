// ABOUTME: Formats and parses an element's custom UDMF fields as editable "key = value" text (for the property dialogs).
// ABOUTME: Round-trips bool/int/double/string with UDMF-style type inference; covers comment, lightcolor, gravity, etc.

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DBuilder.Map;

public static class UdmfFields
{
    private const string NameChars = "abcdefghijklmnopqrstuvwxyz0123456789_";
    private const string StartChars = "abcdefghijklmnopqrstuvwxyz_";

    /// <summary>Renders a fields dictionary as one "key = value" line per entry (sorted by key).</summary>
    public static string Format(IReadOnlyDictionary<string, object> fields)
    {
        var keys = new List<string>(fields.Keys);
        keys.Sort(System.StringComparer.Ordinal);
        var sb = new StringBuilder();
        foreach (var k in keys) sb.Append(k).Append(" = ").Append(ValueToString(fields[k])).Append('\n');
        return sb.ToString();
    }

    /// <summary>
    /// Parses "key = value" lines into a typed dictionary. Values are inferred: true/false -&gt; bool,
    /// integers -&gt; int, decimals -&gt; double, everything else (optionally quoted) -&gt; string. Blank lines ignored.
    /// </summary>
    public static Dictionary<string, object> Parse(string? text)
    {
        var result = new Dictionary<string, object>(System.StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = ValidateName(line[..eq]);
            string val = line[(eq + 1)..].Trim();
            if (key.Length == 0) continue;
            result[key] = InferValue(val);
        }
        return result;
    }

    public static string ValidateName(string name)
    {
        string fieldName = name.Trim().ToLowerInvariant();
        var validName = new StringBuilder();
        foreach (char c in fieldName)
        {
            string validChars = validName.Length > 0 ? NameChars : StartChars;
            if (validChars.IndexOf(c) > -1) validName.Append(c);
        }

        return validName.ToString();
    }

    private static object InferValue(string v)
    {
        if (v.Length >= 2 && v[0] == '"' && v[^1] == '"') return v[1..^1]; // explicit string
        if (string.Equals(v, "true", System.StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(v, "false", System.StringComparison.OrdinalIgnoreCase)) return false;
        if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return i;
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
        return v;
    }

    private static string ValueToString(object value) => value switch
    {
        bool b => b ? "true" : "false",
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        double db => db.ToString("0.######", CultureInfo.InvariantCulture),
        float f => ((double)f).ToString("0.######", CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? "",
    };
}
