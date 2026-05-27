// ABOUTME: Config-driven Boom generalized linedef/sector types parsed from gen_linedeftypes / gen_sectortypes blocks.
// ABOUTME: A category (offset+length) holds options; each option is a bit field of value->title choices that decode a packed number.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DBuilder.IO;

/// <summary>One selectable value within a generalized option: the packed value and its title.</summary>
public sealed class GeneralizedBit : IComparable<GeneralizedBit>
{
    public int Value { get; }
    public int Index => Value;
    public string Title { get; }
    public GeneralizedBit(int value, string title) { Value = value; Title = title; }

    public override string ToString() => Title;

    public int CompareTo(GeneralizedBit? other)
        => other == null ? 1 : Value.CompareTo(other.Value);
}

/// <summary>A bit field within a generalized category (e.g. "Speed" -> {Slow, Normal, Fast, Turbo}).</summary>
public sealed class GeneralizedOption
{
    public string Name { get; }
    public IReadOnlyList<GeneralizedBit> Bits { get; }
    public int BitsStep { get; }
    /// <summary>Combined mask of all this option's bit values; used to extract the option's selection from a packed number.</summary>
    public int Mask { get; }

    private GeneralizedOption(string name, List<GeneralizedBit> bits)
    {
        Name = name;
        Bits = bits;
        BitsStep = bits.Count > 1 ? bits[1].Value : 0;
        int mask = 0;
        foreach (var b in bits) mask |= b.Value;
        Mask = mask;
    }

    // Parses an option sub-dict: numeric keys are value->title bits; a "name" key overrides the title-cased option name.
    internal static GeneralizedOption Parse(string key, IDictionary dict)
    {
        string name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key);
        var bits = new List<GeneralizedBit>();
        foreach (DictionaryEntry e in dict)
        {
            string k = e.Key.ToString() ?? "";
            if (int.TryParse(k, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                bits.Add(new GeneralizedBit(v, e.Value?.ToString() ?? k));
            else if (k == "name" && e.Value is string n)
                name = n;
        }
        bits.Sort((a, b) => a.Value.CompareTo(b.Value));
        return new GeneralizedOption(name, bits);
    }

    internal static List<GeneralizedOption> ParseOptionsBlock(IDictionary block)
    {
        var options = new List<GeneralizedOption>();
        foreach (DictionaryEntry entry in block)
            if (entry.Value is IDictionary dict)
                options.Add(Parse(entry.Key.ToString() ?? "", dict));
        return options;
    }

    public override string ToString() => Name;
}

/// <summary>
/// A generalized type category occupying the action range [Offset, Offset+Length). The number minus Offset
/// packs each option's selected bits; decoding splits it back into per-option choices.
/// </summary>
public sealed class GeneralizedCategory
{
    public string Title { get; }
    public int Offset { get; }
    public int Length { get; }
    public IReadOnlyList<GeneralizedOption> Options { get; }

    private GeneralizedCategory(string title, int offset, int length, List<GeneralizedOption> options)
    {
        Title = title;
        Offset = offset;
        Length = length;
        Options = options;
    }

    /// <summary>True when an action number falls inside this category's range (placeholder categories with length 0 never match).</summary>
    public bool Contains(int action) => Length > 0 && action >= Offset && action < Offset + Length;

    /// <summary>Decodes a packed action into "Title (Option: Choice, ...)".</summary>
    public string Describe(int action)
    {
        int local = action - Offset;
        var parts = new List<string>();
        foreach (var opt in Options)
        {
            int sel = local & opt.Mask;
            var bit = opt.Bits.FirstOrDefault(b => b.Value == sel);
            if (bit != null && bit.Title.Length > 0) parts.Add($"{opt.Name}: {bit.Title}");
        }
        var sb = new StringBuilder(Title);
        if (parts.Count > 0) sb.Append(" (").Append(string.Join(", ", parts)).Append(')');
        return sb.ToString();
    }

    // Parses a gen_linedeftypes/gen_sectortypes block into categories (scalar title/offset/length plus option sub-dicts).
    internal static List<GeneralizedCategory> ParseBlock(IDictionary block)
    {
        var cats = new List<GeneralizedCategory>();
        foreach (DictionaryEntry catEntry in block)
        {
            if (catEntry.Value is not IDictionary cat) continue;
            string title = cat["title"] as string ?? catEntry.Key.ToString() ?? "";
            int offset = ReadInt(cat["offset"]);
            int length = ReadInt(cat["length"]);
            var options = new List<GeneralizedOption>();
            foreach (DictionaryEntry e in cat)
            {
                if (e.Value is IDictionary od)
                    options.Add(GeneralizedOption.Parse(e.Key.ToString() ?? "", od));
            }
            SortOptionsByBitsStep(options);
            cats.Add(new GeneralizedCategory(title, offset, length, options));
        }
        return cats;
    }

    private static void SortOptionsByBitsStep(List<GeneralizedOption> options)
        => options.Sort((a, b) => a.BitsStep.CompareTo(b.BitsStep));

    private static int ReadInt(object? v) => v switch
    {
        int i => i,
        long l => (int)l,
        double d => (int)d,
        string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) => p,
        _ => 0,
    };
}
