// ABOUTME: Parser for ZDoom CVARINFO variable declarations.
// ABOUTME: Captures scope, type, name, default value, and archive flag for editor configuration metadata.

using System;
using System.Collections.Generic;

namespace DBuilder.IO;

public sealed class CvarInfo
{
    public List<CvarDefinition> Variables { get; } = new();
}

public sealed class CvarDefinition
{
    public string Scope { get; init; } = "";
    public string Type { get; init; } = "";
    public string Name { get; init; } = "";
    public string? DefaultValue { get; set; }
    public bool Archive { get; set; }
}

public static class CvarInfoParser
{
    private static readonly HashSet<string> Scopes = new(StringComparer.OrdinalIgnoreCase) { "server", "user", "nosave" };
    private static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase) { "bool", "int", "float", "string", "color" };

    public static CvarInfo Parse(string text)
    {
        var info = new CvarInfo();
        var t = ZDoomTokenScanner.Tokenize(text);
        for (int i = 0; i < t.Count;)
        {
            string scope = t[i++];
            if (!Scopes.Contains(scope) || i >= t.Count) continue;

            bool archive = false;
            if (t[i].Equals("archive", StringComparison.OrdinalIgnoreCase))
            {
                archive = true;
                i++;
            }
            if (i >= t.Count || !Types.Contains(t[i])) continue;
            string type = t[i++];
            if (i >= t.Count) continue;
            var variable = new CvarDefinition { Scope = scope.ToLowerInvariant(), Type = type.ToLowerInvariant(), Name = t[i++], Archive = archive };
            if (i < t.Count && t[i] == "=")
            {
                i++;
                if (i < t.Count && t[i] != ";") variable.DefaultValue = t[i++];
            }
            while (i < t.Count && t[i] != ";") i++;
            if (i < t.Count) i++;
            info.Variables.Add(variable);
        }
        return info;
    }
}
