// ABOUTME: Parser for GZDoom IWADINFO game archive declarations.
// ABOUTME: Captures IWAD metadata fields used for game and resource selection.

using System;
using System.Collections.Generic;

namespace DBuilder.IO;

public sealed class IwadInfo
{
    public List<IwadDefinition> Iwads { get; } = new();
}

public sealed class IwadDefinition
{
    public Dictionary<string, string> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Name => Fields.TryGetValue("name", out var value) ? value : null;
    public string? AutoName => Fields.TryGetValue("autoname", out var value) ? value : null;
    public string? Game => Fields.TryGetValue("game", out var value) ? value : null;
    public string? Config => Fields.TryGetValue("config", out var value) ? value : null;
    public string? MapInfo => Fields.TryGetValue("mapinfo", out var value) ? value : null;
}

public static class IwadInfoParser
{
    public static IwadInfo Parse(string text)
    {
        var info = new IwadInfo();
        var t = ZDoomTokenScanner.Tokenize(text);
        for (int i = 0; i < t.Count;)
        {
            string keyword = t[i++];
            if (!keyword.Equals("iwad", StringComparison.OrdinalIgnoreCase))
            {
                if (i < t.Count && t[i] == "{") SkipBlock(t, ref i);
                continue;
            }

            var iwad = new IwadDefinition();
            if (i < t.Count && t[i] == "{")
            {
                i++;
                while (i < t.Count && t[i] != "}")
                {
                    string key = t[i++].ToLowerInvariant();
                    if (i < t.Count && t[i] == "=") i++;
                    if (i < t.Count && t[i] != ";" && t[i] != "}") iwad.Fields[key] = t[i++];
                    if (i < t.Count && t[i] == ";") i++;
                }
                if (i < t.Count) i++;
            }
            info.Iwads.Add(iwad);
        }
        return info;
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
