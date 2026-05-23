// ABOUTME: Parser for the ZDoom GLDEFS lump - dynamic light definitions, actor light associations, and glow lists.
// ABOUTME: Captures the editor-relevant data (light names/colors/sizes, object->light links, glowing flats/textures).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DBuilder.IO;

/// <summary>A dynamic light definition (pointlight/pulselight/flickerlight/...) with its color and size.</summary>
public sealed class GldefsLight
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public float R { get; set; }
    public float G { get; set; }
    public float B { get; set; }
    public float Size { get; set; }
}

/// <summary>An actor's light associations from an `object` block (the light names its frames reference).</summary>
public sealed class GldefsObject
{
    public string ClassName { get; init; } = "";
    public List<string> Lights { get; } = new();
}

public sealed class Gldefs
{
    public Dictionary<string, GldefsLight> Lights { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<GldefsObject> Objects { get; } = new();
    public List<string> GlowFlats { get; } = new();
    public List<string> GlowTextures { get; } = new();

    /// <summary>The color of the first light an actor references, or null.</summary>
    public (float R, float G, float B)? ActorLightColor(string className)
    {
        foreach (var o in Objects)
            if (string.Equals(o.ClassName, className, StringComparison.OrdinalIgnoreCase))
                foreach (var ln in o.Lights)
                    if (Lights.TryGetValue(ln, out var l)) return (l.R, l.G, l.B);
        return null;
    }
}

public static class GldefsParser
{
    private static readonly HashSet<string> LightTypes = new(StringComparer.OrdinalIgnoreCase)
    { "pointlight", "pulselight", "flickerlight", "flickerlight2", "sectorlight", "spotlight" };

    public static Gldefs Parse(string text)
    {
        var g = new Gldefs();
        var t = Tokenize(text);
        int i = 0;
        while (i < t.Count)
        {
            string kw = t[i].ToLowerInvariant();
            if (LightTypes.Contains(kw)) ParseLight(g, kw, t, ref i);
            else if (kw == "object") ParseObject(g, t, ref i);
            else if (kw == "glow") ParseGlow(g, t, ref i);
            else if (t[i] == "{") SkipBlock(t, ref i); // stray block
            else i++; // unknown keyword (skybox/brightmap/material/... handled by skipping its block next)
        }
        return g;
    }

    private static void ParseLight(Gldefs g, string type, List<string> t, ref int i)
    {
        i++; // type
        if (i >= t.Count) return;
        var light = new GldefsLight { Name = t[i++], Type = type };
        if (i < t.Count && t[i] == "{")
        {
            i++;
            while (i < t.Count && t[i] != "}")
            {
                string p = t[i++].ToLowerInvariant();
                if (p == "color") { light.R = ReadFloat(t, ref i); light.G = ReadFloat(t, ref i); light.B = ReadFloat(t, ref i); }
                else if (p == "size" || p == "secondarysize") light.Size = ReadFloat(t, ref i);
                // other props (offset/interval/chance/subtractive/...) skipped
            }
            if (i < t.Count) i++; // }
        }
        if (light.Name.Length > 0) g.Lights[light.Name] = light;
    }

    private static void ParseObject(Gldefs g, List<string> t, ref int i)
    {
        i++; // object
        if (i >= t.Count) return;
        var obj = new GldefsObject { ClassName = t[i++] };
        if (i < t.Count && t[i] == "{")
        {
            i++;
            while (i < t.Count && t[i] != "}")
            {
                if (t[i].Equals("frame", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    if (i < t.Count) i++; // frame sprite name
                    if (i < t.Count && t[i] == "{")
                    {
                        i++;
                        while (i < t.Count && t[i] != "}")
                        {
                            if (t[i].Equals("light", StringComparison.OrdinalIgnoreCase) && i + 1 < t.Count) { obj.Lights.Add(t[i + 1]); i += 2; }
                            else i++;
                        }
                        if (i < t.Count) i++; // }
                    }
                }
                else i++;
            }
            if (i < t.Count) i++; // }
        }
        g.Objects.Add(obj);
    }

    private static void ParseGlow(Gldefs g, List<string> t, ref int i)
    {
        i++; // glow
        if (i >= t.Count || t[i] != "{") return;
        i++;
        while (i < t.Count && t[i] != "}")
        {
            string p = t[i++].ToLowerInvariant();
            if (p == "flats" && i < t.Count && t[i] == "{")
            {
                i++;
                while (i < t.Count && t[i] != "}") g.GlowFlats.Add(t[i++]);
                if (i < t.Count) i++; // }
            }
            else if (p == "texture" && i < t.Count)
            {
                g.GlowTextures.Add(t[i++]);
                // skip a trailing "color r g b" / fullbright if present
                if (i < t.Count && t[i].Equals("color", StringComparison.OrdinalIgnoreCase)) { i++; ReadFloat(t, ref i); ReadFloat(t, ref i); ReadFloat(t, ref i); }
            }
        }
        if (i < t.Count) i++; // }
    }

    private static float ReadFloat(List<string> t, ref int i)
    {
        if (i < t.Count && float.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) { i++; return v; }
        return 0;
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

    private static List<string> Tokenize(string s)
    {
        var toks = new List<string>();
        int n = s.Length;
        for (int p = 0; p < n;)
        {
            char c = s[p];
            if (char.IsWhiteSpace(c)) { p++; continue; }
            if (c == '/' && p + 1 < n && s[p + 1] == '/') { p += 2; while (p < n && s[p] != '\n') p++; continue; }
            if (c == '/' && p + 1 < n && s[p + 1] == '*') { p += 2; while (p + 1 < n && !(s[p] == '*' && s[p + 1] == '/')) p++; p += 2; continue; }
            if (c == '"') { var sb = new StringBuilder(); p++; while (p < n && s[p] != '"') sb.Append(s[p++]); p++; toks.Add(sb.ToString()); continue; }
            if (c == '{' || c == '}') { toks.Add(c.ToString()); p++; continue; }
            int b = p;
            while (p < n && !char.IsWhiteSpace(s[p]) && s[p] != '{' && s[p] != '}' && s[p] != '"') p++;
            toks.Add(s.Substring(b, p - b));
        }
        return toks;
    }
}
