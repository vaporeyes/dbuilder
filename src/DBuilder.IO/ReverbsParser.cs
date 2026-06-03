// ABOUTME: Parser for ZDoom REVERBS sound environment declarations.
// ABOUTME: Captures environment names and thing arguments while skipping detailed EAX properties.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DBuilder.IO;

public sealed class Reverbs
{
    public Dictionary<string, ReverbDefinition> Environments { get; } = new(StringComparer.Ordinal);
}

public sealed record ReverbDefinition(string Name, int Arg0, int Arg1);

public static class ReverbsParser
{
    public static Reverbs Parse(string text)
    {
        var result = new Reverbs();
        var usedIds = new HashSet<(int Arg0, int Arg1)>();
        var t = ZDoomTokenScanner.Tokenize(text);
        for (int i = 0; i < t.Count;)
        {
            if (t[i] == "{")
            {
                SkipBlock(t, ref i);
                continue;
            }

            string name = t[i++];
            if (name.Length == 0) return result;
            if (!ReadInt(t, ref i, out int arg0) || !ReadInt(t, ref i, out int arg1))
                return result;
            if (usedIds.Add((arg0, arg1))) result.Environments[name] = new ReverbDefinition(name, arg0, arg1);
            if (i < t.Count && t[i] == "{") SkipBlock(t, ref i);
        }
        SortEnvironments(result.Environments);
        return result;
    }

    private static void SortEnvironments(Dictionary<string, ReverbDefinition> environments)
    {
        var sorted = new List<ReverbDefinition>(environments.Values);
        sorted.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
        environments.Clear();
        foreach (var environment in sorted) environments[environment.Name] = environment;
    }

    private static bool ReadInt(List<string> t, ref int i, out int value)
    {
        value = 0;
        if (i < t.Count && int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) { i++; return true; }
        return false;
    }

    private static void SkipBlock(List<string> t, ref int i)
    {
        int depth = 0;
        for (; i < t.Count; i++)
        {
            if (t[i] == "{") depth++;
            else if (t[i] == "}") { depth--; if (depth == 0) { i++; return; } }
        }
    }
}
