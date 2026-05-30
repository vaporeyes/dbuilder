// ABOUTME: Parser for ZDoom CVARINFO variable declarations.
// ABOUTME: Captures scope, type, name, default value, and archive flag for editor configuration metadata.

using System;
using System.Collections.Generic;
using System.Globalization;

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
    public string? HandlerClass { get; set; }
    public List<string> Flags { get; } = new();
}

public static class CvarInfoParser
{
    private static readonly HashSet<string> Scopes = new(StringComparer.OrdinalIgnoreCase) { "server", "user", "nosave", "local" };
    private static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase) { "server", "user", "nosave", "local", "archive", "noarchive", "cheat", "latch" };
    private static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase) { "bool", "int", "float", "string", "color" };

    public static CvarInfo Parse(string text)
    {
        var info = new CvarInfo();
        var t = ZDoomTokenScanner.Tokenize(text);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < t.Count;)
        {
            string scope = "";
            bool archive = false;
            string? handlerClass = null;
            bool invalidDeclaration = false;
            var flags = new List<string>();

            while (i < t.Count && !Types.Contains(t[i]))
            {
                string flag = t[i++];
                string lower = flag.ToLowerInvariant();
                if (Scopes.Contains(lower) && scope.Length == 0) scope = lower;
                if (lower == "archive") archive = true;
                if (Flags.Contains(lower)) flags.Add(lower);
                else if (lower.StartsWith("handlerclass", StringComparison.OrdinalIgnoreCase))
                {
                    flags.Add("handlerclass");
                    handlerClass = ReadHandlerClass(t, ref i, flag);
                }
                else
                {
                    invalidDeclaration = true;
                    SkipDeclaration(t, ref i);
                    break;
                }
            }

            if (invalidDeclaration) continue;

            if (i >= t.Count || !Types.Contains(t[i]))
            {
                SkipDeclaration(t, ref i);
                continue;
            }

            string type = t[i++];
            if (i >= t.Count)
            {
                continue;
            }

            var variable = new CvarDefinition
            {
                Scope = scope,
                Type = type.ToLowerInvariant(),
                Name = t[i++],
                Archive = archive,
                HandlerClass = handlerClass
            };
            foreach (string flag in flags)
            {
                variable.Flags.Add(flag);
            }

            if (i < t.Count && t[i] == "=")
            {
                i++;
                if (i < t.Count && t[i] != ";") variable.DefaultValue = t[i++];
            }
            while (i < t.Count && t[i] != ";") i++;
            if (i < t.Count) i++;
            if (!IsValidDefaultValue(variable.Type, variable.DefaultValue)) continue;
            if (!names.Add(variable.Name)) continue;
            info.Variables.Add(variable);
        }
        return info;
    }

    private static void SkipDeclaration(List<string> t, ref int i)
    {
        while (i < t.Count && t[i] != ";") i++;
        if (i < t.Count) i++;
    }

    private static string? ReadHandlerClass(List<string> t, ref int i, string token)
    {
        int open = token.IndexOf('(');
        if (open >= 0 && token.EndsWith(')') && token.Length > open + 2) return token.Substring(open + 1, token.Length - open - 2);
        if (open >= 0)
        {
            if (i < t.Count)
            {
                string handlerClass = t[i++];
                if (i < t.Count && t[i] == ")") i++;
                return handlerClass;
            }
            return null;
        }
        if (i < t.Count && t[i] == "(") i++;
        if (i >= t.Count) return null;
        string value = t[i++];
        if (i < t.Count && t[i] == ")") i++;
        return value;
    }

    private static bool IsValidDefaultValue(string type, string? value)
    {
        if (string.IsNullOrEmpty(value)) return true;
        return type.ToLowerInvariant() switch
        {
            "int" => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "float" => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            "bool" => value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase),
            "color" => ZDoomColorParser.TryParse(value, knownColors: null, out _, out _, out _),
            "string" => true,
            _ => false,
        };
    }
}
