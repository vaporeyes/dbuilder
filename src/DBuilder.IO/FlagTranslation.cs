// ABOUTME: Maps a binary flag bit to one or more UDMF flag field names (compound "a,b" and negated "!a" supported).
// ABOUTME: Drives lossless binary<->UDMF flag conversion, mirroring UDB's FlagTranslation semantics.

using System.Collections.Generic;

namespace DBuilder.IO;

/// <summary>
/// One binary flag bit translated to UDMF fields. A spec like "skill1,skill2" sets both fields true when the bit
/// is set; "!single" sets the field false when the bit is set (and true when it is clear).
/// </summary>
public sealed class FlagTranslation
{
    public int Flag { get; }
    public IReadOnlyList<string> Fields { get; }
    public IReadOnlyList<bool> Values { get; }

    private FlagTranslation(int flag, List<string> fields, List<bool> values)
    {
        Flag = flag;
        Fields = fields;
        Values = values;
    }

    // Parses a "field1,!field2" spec for a given flag bit; returns null if the spec is empty.
    internal static FlagTranslation? Parse(int flag, string spec)
    {
        var fields = new List<string>();
        var values = new List<bool>();
        foreach (var raw in spec.Split(','))
        {
            string f = raw.Trim();
            if (f.Length == 0) continue;
            if (f[0] == '!') { fields.Add(f.Substring(1).Trim()); values.Add(false); }
            else { fields.Add(f); values.Add(true); }
        }
        return fields.Count == 0 ? null : new FlagTranslation(flag, fields, values);
    }
}
