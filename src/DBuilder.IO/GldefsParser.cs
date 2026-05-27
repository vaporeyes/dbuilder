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
    public float SecondarySize { get; set; }
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float OffsetZ { get; set; }
    public float Interval { get; set; }
    public float Chance { get; set; }
    public float Scale { get; set; }
    public bool Subtractive { get; set; }
    public bool Attenuate { get; set; }
    public bool DontLightSelf { get; set; }
}

/// <summary>An actor's light associations from an `object` block (the light names its frames reference).</summary>
public sealed class GldefsObject
{
    public string ClassName { get; init; } = "";
    public List<string> Lights { get; } = new();
}

public sealed record GldefsGlow(string Texture, float R, float G, float B, int Height = 128, bool Fullbright = false, bool CalculateTextureColor = false);

public sealed class GldefsSkybox
{
    public string Name { get; init; } = "";
    public bool FlipTop { get; set; }
    public List<string> Textures { get; } = new();
}

public sealed class Gldefs
{
    public Dictionary<string, GldefsLight> Lights { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<GldefsObject> Objects { get; } = new();
    public List<string> GlowFlats { get; } = new();
    public List<string> GlowTextures { get; } = new();
    public Dictionary<string, GldefsGlow> Glows { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, GldefsSkybox> Skyboxes { get; } = new(StringComparer.OrdinalIgnoreCase);

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
    { "pointlight", "pulselight", "flickerlight", "flickerlight2", "sectorlight" };

    public static Gldefs Parse(string text) => Parse(text, includeResolver: null);

    public static Gldefs Parse(string text, Func<string, string?>? includeResolver)
        => Parse(text, includeResolver, knownColors: null);

    public static Gldefs Parse(string text, Func<string, string?>? includeResolver, IReadOnlyDictionary<string, X11Color>? knownColors)
    {
        var g = new Gldefs();
        ParseInto(g, text, includeResolver, knownColors, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return g;
    }

    private static void ParseInto(Gldefs g, string text, Func<string, string?>? includeResolver, IReadOnlyDictionary<string, X11Color>? knownColors, HashSet<string> parsedIncludes)
    {
        var t = Tokenize(text);
        int i = 0;
        while (i < t.Count)
        {
            string kw = t[i].ToLowerInvariant();
            if (kw == "$gzdb_skip") break;
            else if (LightTypes.Contains(kw)) ParseLight(g, kw, t, ref i);
            else if (kw == "object") ParseObject(g, t, ref i);
            else if (kw == "glow") ParseGlow(g, t, ref i, knownColors);
            else if (kw == "skybox") ParseSkybox(g, t, ref i);
            else if (kw == "#include") ParseInclude(g, t, ref i, includeResolver, knownColors, parsedIncludes);
            else if (t[i] == "{") SkipBlock(t, ref i); // stray block
            else i++; // unknown keyword (skybox/brightmap/material/... handled by skipping its block next)
        }
    }

    private static void ParseInclude(Gldefs g, List<string> t, ref int i, Func<string, string?>? includeResolver, IReadOnlyDictionary<string, X11Color>? knownColors, HashSet<string> parsedIncludes)
    {
        i++; // #include
        if (includeResolver == null || i >= t.Count) return;
        string include = t[i++];
        if (!parsedIncludes.Add(include)) return;
        string? text = includeResolver(include);
        if (text != null) ParseInto(g, text, includeResolver, knownColors, parsedIncludes);
    }

    private static void ParseLight(Gldefs g, string type, List<string> t, ref int i)
    {
        i++; // type
        if (i >= t.Count) return;
        var light = new GldefsLight { Name = t[i++], Type = type };
        bool invalid = false;
        if (i < t.Count && t[i] == "{")
        {
            i++;
            while (i < t.Count && t[i] != "}")
            {
                string p = t[i++].ToLowerInvariant();
                if (p == "color") { light.R = ClampColor(ReadFloat(t, ref i)); light.G = ClampColor(ReadFloat(t, ref i)); light.B = ClampColor(ReadFloat(t, ref i)); }
                else if (p == "size")
                {
                    float size = ReadFloat(t, ref i);
                    if (type.Equals("sectorlight", StringComparison.OrdinalIgnoreCase) || size < 0.0f) invalid = true;
                    else light.Size = size * 2.0f;
                }
                else if (p == "secondarysize")
                {
                    float secondarySize = ReadFloat(t, ref i);
                    if (!CanHaveSecondarySize(type) || secondarySize < 0.0f) invalid = true;
                    else light.SecondarySize = secondarySize * 2.0f;
                }
                else if (p == "offset") { light.OffsetX = ReadFloat(t, ref i); light.OffsetZ = ReadFloat(t, ref i); light.OffsetY = ReadFloat(t, ref i); }
                else if (p == "interval")
                {
                    float interval = ReadFloat(t, ref i);
                    if (!CanHaveInterval(type) || interval <= 0.0f) invalid = true;
                    else light.Interval = NormalizeInterval(type, interval);
                }
                else if (p == "chance")
                {
                    light.Chance = ReadFloat(t, ref i);
                    if (!type.Equals("flickerlight", StringComparison.OrdinalIgnoreCase) || light.Chance is < 0.0f or > 1.0f) invalid = true;
                    else light.Interval = (int)(light.Chance * 359.0f);
                }
                else if (p == "scale")
                {
                    light.Scale = ReadFloat(t, ref i);
                    if (!type.Equals("sectorlight", StringComparison.OrdinalIgnoreCase) || light.Scale is < 0.0f or > 1.0f) invalid = true;
                    else light.Interval = (int)(light.Scale * 10.0f);
                }
                else if (p == "subtractive")
                {
                    if (TryReadIntFlag(t, ref i, out bool subtractive)) light.Subtractive = subtractive;
                    else invalid = true;
                }
                else if (p == "attenuate")
                {
                    if (TryReadIntFlag(t, ref i, out bool attenuate)) light.Attenuate = attenuate;
                    else invalid = true;
                }
                else if (p == "dontlightself")
                {
                    if (TryReadIntFlag(t, ref i, out bool dontLightSelf)) light.DontLightSelf = dontLightSelf;
                    else invalid = true;
                }
            }
            if (i < t.Count) i++; // }
        }
        if (!invalid && light.Name.Length > 0 && ShouldKeepLight(light)) g.Lights[light.Name] = light;
    }

    private static bool CanHaveSecondarySize(string type)
        => type.Equals("pulselight", StringComparison.OrdinalIgnoreCase)
        || type.Equals("flickerlight", StringComparison.OrdinalIgnoreCase)
        || type.Equals("flickerlight2", StringComparison.OrdinalIgnoreCase);

    private static bool CanHaveInterval(string type)
        => type.Equals("pulselight", StringComparison.OrdinalIgnoreCase)
        || type.Equals("flickerlight2", StringComparison.OrdinalIgnoreCase);

    private static float NormalizeInterval(string type, float interval)
    {
        if (type.Equals("pulselight", StringComparison.OrdinalIgnoreCase)) return (int)(interval * 35.0f);
        if (type.Equals("flickerlight2", StringComparison.OrdinalIgnoreCase)) return (int)(interval * 350.0f);
        return interval;
    }

    private static float ClampColor(float value) => Math.Clamp(value, 0.0f, 1.0f);

    private static bool IsBlack(GldefsLight light) => light.R == 0.0f && light.G == 0.0f && light.B == 0.0f;

    private static bool ShouldKeepLight(GldefsLight light)
    {
        if (IsBlack(light)) return false;
        if (light.Type.Equals("pointlight", StringComparison.OrdinalIgnoreCase))
            return light.Size != 0.0f;
        if (light.Type.Equals("pulselight", StringComparison.OrdinalIgnoreCase)
            || light.Type.Equals("flickerlight", StringComparison.OrdinalIgnoreCase)
            || light.Type.Equals("flickerlight2", StringComparison.OrdinalIgnoreCase))
            return light.Size != 0.0f || light.SecondarySize != 0.0f;
        return true;
    }

    private static void ParseObject(Gldefs g, List<string> t, ref int i)
    {
        i++; // object
        if (i >= t.Count) return;
        var obj = new GldefsObject { ClassName = t[i++] };
        bool foundLight = false;
        if (i < t.Count && t[i] == "{")
        {
            i++;
            while (i < t.Count && t[i] != "}")
            {
                if (t[i].Equals("frame", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    string frameName = i < t.Count ? t[i++] : "";
                    bool useFrame = !foundLight && (frameName.Length == 4 || (frameName.Length > 4 && char.ToLowerInvariant(frameName[4]) == 'a'));
                    if (i < t.Count && t[i] == "{")
                    {
                        i++;
                        while (i < t.Count && t[i] != "}")
                        {
                            if (t[i].Equals("light", StringComparison.OrdinalIgnoreCase) && i + 1 < t.Count)
                            {
                                if (useFrame)
                                {
                                    obj.Lights.Add(t[i + 1]);
                                    foundLight = true;
                                    useFrame = false;
                                }
                                i += 2;
                            }
                            else i++;
                        }
                        if (i < t.Count) i++; // }
                    }
                }
                else i++;
            }
            if (i < t.Count) i++; // }
        }
        if (obj.Lights.Count > 0) g.Objects.Add(obj);
    }

    private static void ParseGlow(Gldefs g, List<string> t, ref int i, IReadOnlyDictionary<string, X11Color>? knownColors)
    {
        i++; // glow
        if (i >= t.Count || t[i] != "{") return;
        i++;
        while (i < t.Count && t[i] != "}")
        {
            string p = t[i++].ToLowerInvariant();
            if ((p == "flats" || p == "walls") && i < t.Count && t[i] == "{")
            {
                i++;
                while (i < t.Count && t[i] != "}")
                {
                    string name = t[i++];
                    g.GlowFlats.Add(name);
                    g.Glows[name] = new GldefsGlow(name, 1.0f, 1.0f, 1.0f, Height: 128, Fullbright: true, CalculateTextureColor: true);
                }
                if (i < t.Count) i++; // }
            }
            else if (p == "texture" && i < t.Count)
            {
                ParseGlowTexture(g, t, ref i, knownColors);
            }
        }
        if (i < t.Count) i++; // }
    }

    private static void ParseGlowTexture(Gldefs g, List<string> t, ref int i, IReadOnlyDictionary<string, X11Color>? knownColors)
    {
        string texture = t[i++];
        float r = 1.0f, green = 1.0f, b = 1.0f;
        int height = 64;
        bool fullbright = false;

        if (i < t.Count && t[i] == ",") i++;
        if (i < t.Count && t[i].Equals("color", StringComparison.OrdinalIgnoreCase)) i++;

        if (i < t.Count && TryReadColor(t, ref i, knownColors, out var color))
        {
            r = color.R;
            green = color.G;
            b = color.B;
        }

        if (i < t.Count && t[i] == ",") i++;
        if (i < t.Count && int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedHeight))
        {
            height = parsedHeight;
            i++;
            if (i < t.Count && t[i] == ",") i++;
        }

        if (i < t.Count && t[i].Equals("fullbright", StringComparison.OrdinalIgnoreCase))
        {
            fullbright = true;
            i++;
        }

        g.GlowTextures.Add(texture);
        g.Glows[texture] = new GldefsGlow(texture, r, green, b, height * 2, fullbright);
    }

    private static void ParseSkybox(Gldefs g, List<string> t, ref int i)
    {
        i++; // skybox
        if (i >= t.Count) return;
        var skybox = new GldefsSkybox { Name = t[i++].ToUpperInvariant() };
        if (i < t.Count && t[i].Equals("fliptop", StringComparison.OrdinalIgnoreCase))
        {
            skybox.FlipTop = true;
            i++;
        }
        if (i < t.Count && t[i] == "{")
        {
            i++;
            while (i < t.Count && t[i] != "}") skybox.Textures.Add(t[i++]);
            if (i < t.Count) i++;
        }
        if (skybox.Name.Length > 0 && (skybox.Textures.Count == 3 || skybox.Textures.Count == 6)) g.Skyboxes[skybox.Name] = skybox;
    }

    private static float ReadFloat(List<string> t, ref int i)
    {
        if (i < t.Count && float.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) { i++; return v; }
        return 0;
    }

    private static bool TryReadIntFlag(List<string> t, ref int i, out bool value)
    {
        value = false;
        if (i >= t.Count) return false;
        if (int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric))
        {
            i++;
            value = numeric == 1;
            return true;
        }
        return false;
    }

    private static bool TryReadColor(List<string> t, ref int i, IReadOnlyDictionary<string, X11Color>? knownColors, out (float R, float G, float B) color)
    {
        color = default;
        if (i >= t.Count) return false;
        string value = t[i];
        if (i + 2 < t.Count
            && float.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float r)
            && float.TryParse(t[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g)
            && float.TryParse(t[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
        {
            i += 3;
            color = (r, g, b);
            return true;
        }
        if (ZDoomColorParser.TryParse(value, knownColors, out byte red, out byte green, out byte blue))
        {
            i++;
            color = (red / 255.0f, green / 255.0f, blue / 255.0f);
            return true;
        }
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
            if (c == '{' || c == '}' || c == ',') { toks.Add(c.ToString()); p++; continue; }
            int b = p;
            while (p < n && !char.IsWhiteSpace(s[p]) && s[p] != '{' && s[p] != '}' && s[p] != ',' && s[p] != '"') p++;
            toks.Add(s.Substring(b, p - b));
        }
        return toks;
    }
}
