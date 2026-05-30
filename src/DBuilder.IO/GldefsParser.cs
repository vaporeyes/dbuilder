// ABOUTME: Parser for the ZDoom GLDEFS lump - dynamic light definitions, actor light associations, and glow lists.
// ABOUTME: Captures the editor-relevant data (light names/colors/sizes, object->light links, glowing flats/textures).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DBuilder.IO;

/// <summary>A dynamic light definition (pointlight/pulselight/flickerlight/...) with its color and size.</summary>
public enum GldefsLightRenderStyle
{
    Normal,
    Subtractive,
    Attenuated,
}

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
    public GldefsLightRenderStyle RenderStyle { get; set; } = GldefsLightRenderStyle.Normal;
    public bool Subtractive
    {
        get => RenderStyle == GldefsLightRenderStyle.Subtractive;
        set => RenderStyle = value
            ? GldefsLightRenderStyle.Subtractive
            : GldefsLightRenderStyle.Normal;
    }
    public bool Attenuate
    {
        get => RenderStyle == GldefsLightRenderStyle.Attenuated;
        set => RenderStyle = value
            ? GldefsLightRenderStyle.Attenuated
            : GldefsLightRenderStyle.Normal;
    }
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

    private static bool ParseInto(Gldefs g, string text, Func<string, string?>? includeResolver, IReadOnlyDictionary<string, X11Color>? knownColors, HashSet<string> parsedIncludes)
    {
        var tokens = Tokenize(text);
        var t = tokens.Text;
        int i = 0;
        while (i < t.Count)
        {
            string kw = t[i].ToLowerInvariant();
            if (kw == "$gzdb_skip") break;
            else if (LightTypes.Contains(kw))
            {
                if (!ParseLight(g, kw, t, ref i)) return false;
            }
            else if (kw == "object") ParseObject(g, t, ref i);
            else if (kw == "glow")
            {
                if (!ParseGlow(g, tokens, ref i, knownColors)) return false;
            }
            else if (kw == "skybox")
            {
                if (!ParseSkybox(g, tokens, ref i)) return false;
            }
            else if (kw == "#include")
            {
                if (!ParseInclude(g, t, ref i, includeResolver, knownColors, parsedIncludes)) return false;
            }
            else if (t[i] == "{") SkipBlock(t, ref i); // stray block
            else TrySkipUnknownTopLevelBlock(t, ref i);
        }
        return true;
    }

    private static void TrySkipUnknownTopLevelBlock(List<string> t, ref int i)
    {
        i++;
        while (i < t.Count && t[i] != "{") i++;

        if (i < t.Count && t[i] == "{") SkipBlock(t, ref i);
    }

    private static bool ParseInclude(Gldefs g, List<string> t, ref int i, Func<string, string?>? includeResolver, IReadOnlyDictionary<string, X11Color>? knownColors, HashSet<string> parsedIncludes)
    {
        i++; // #include
        if (i >= t.Count) return false;
        string include = t[i++];
        if (!IsValidIncludePath(include)) return false;
        if (!parsedIncludes.Add(include)) return false;
        if (includeResolver == null) return true;
        string? text = includeResolver(include);
        return text == null || ParseInto(g, text, includeResolver, knownColors, parsedIncludes);
    }

    private static bool IsValidIncludePath(string include)
    {
        if (string.IsNullOrWhiteSpace(include)) return false;
        if (Path.IsPathRooted(include)) return false;
        if (include.Contains('\\')) return false;
        return !include.StartsWith("../", StringComparison.Ordinal)
            && !include.StartsWith("./", StringComparison.Ordinal)
            && !include.Equals("..", StringComparison.Ordinal)
            && !include.Equals(".", StringComparison.Ordinal);
    }

    private static bool ParseLight(Gldefs g, string type, List<string> t, ref int i)
    {
        i++; // type
        if (i >= t.Count) return false;
        var light = new GldefsLight { Name = t[i++], Type = type };
        if (light.Name.Length == 0) return false;
        if (i >= t.Count || t[i] != "{") return false;

        i++;
        while (i < t.Count && t[i] != "}")
        {
            string p = t[i++].ToLowerInvariant();
            if (p == "color")
            {
                if (!TryReadFloat(t, ref i, out float red)
                    || !TryReadFloat(t, ref i, out float green)
                    || !TryReadFloat(t, ref i, out float blue))
                {
                    return false;
                }
                light.R = ClampColor(red);
                light.G = ClampColor(green);
                light.B = ClampColor(blue);
            }
            else if (p == "size")
            {
                if (!TryReadInt(t, ref i, out int size)) return false;
                if (type.Equals("sectorlight", StringComparison.OrdinalIgnoreCase) || size < 0.0f) return false;
                light.Size = size * 2.0f;
            }
            else if (p == "secondarysize")
            {
                if (!TryReadSignedInt(t, ref i, out int secondarySize)) return false;
                if (!CanHaveSecondarySize(type) || secondarySize < 0.0f) return false;
                light.SecondarySize = secondarySize * 2.0f;
            }
            else if (p == "offset")
            {
                if (!TryReadSignedFloat(t, ref i, out float offsetX)
                    || !TryReadSignedFloat(t, ref i, out float offsetZ)
                    || !TryReadSignedFloat(t, ref i, out float offsetY))
                {
                    return false;
                }
                light.OffsetX = offsetX;
                light.OffsetZ = offsetZ;
                light.OffsetY = offsetY;
            }
            else if (p == "interval")
            {
                if (!TryReadSignedFloat(t, ref i, out float interval)) return false;
                if (!CanHaveInterval(type) || interval <= 0.0f) return false;
                light.Interval = NormalizeInterval(type, interval);
            }
            else if (p == "chance")
            {
                if (!TryReadSignedFloat(t, ref i, out float chance)) return false;
                light.Chance = chance;
                if (!type.Equals("flickerlight", StringComparison.OrdinalIgnoreCase) || light.Chance is < 0.0f or > 1.0f) return false;
                light.Interval = (int)(light.Chance * 359.0f);
            }
            else if (p == "scale")
            {
                if (!TryReadSignedFloat(t, ref i, out float scale)) return false;
                light.Scale = scale;
                if (!type.Equals("sectorlight", StringComparison.OrdinalIgnoreCase) || light.Scale is < 0.0f or > 1.0f) return false;
                light.Interval = (int)(light.Scale * 10.0f);
            }
            else if (p == "subtractive")
            {
                if (!TryReadIntFlag(t, ref i, out bool subtractive)) return false;
                light.Subtractive = subtractive;
            }
            else if (p == "attenuate")
            {
                if (!TryReadIntFlag(t, ref i, out bool attenuate)) return false;
                light.Attenuate = attenuate;
            }
            else if (p == "dontlightself")
            {
                if (!TryReadIntFlag(t, ref i, out bool dontLightSelf)) return false;
                light.DontLightSelf = dontLightSelf;
            }
            else
            {
                // UDB ignores unknown GLDEFS light properties and keeps scanning the light block.
            }
        }
        if (i < t.Count) i++; // }
        if (ShouldKeepLight(light)) g.Lights[light.Name] = light;
        return true;
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
        if (string.IsNullOrEmpty(obj.ClassName))
        {
            if (i < t.Count && t[i] == "{") SkipBlock(t, ref i);
            return;
        }
        bool foundLight = false;
        if (i < t.Count && t[i] == "{")
        {
            i++;
            int bracesCount = 1;
            bool foundFrame = false;
            while (i < t.Count)
            {
                string token = t[i++].ToLowerInvariant();
                if (!foundLight && !foundFrame && token == "frame")
                {
                    string frameName = i < t.Count ? t[i++].ToLowerInvariant() : "";
                    foundFrame = frameName.Length == 4 || (frameName.Length > 4 && frameName[4] == 'a');
                }
                else if (!foundLight && foundFrame && token == "light")
                {
                    string lightName = i < t.Count ? t[i++] : "";
                    if (!string.IsNullOrEmpty(lightName))
                    {
                        obj.Lights.Add(lightName);
                        foundLight = true;
                    }
                }
                else if (token == "{")
                {
                    bracesCount++;
                }
                else if (token == "}" && --bracesCount < 1)
                {
                    break;
                }
            }
        }
        if (obj.Lights.Count > 0) SetObject(g.Objects, obj);
    }

    private static void SetObject(List<GldefsObject> objects, GldefsObject obj)
    {
        for (int i = 0; i < objects.Count; i++)
        {
            if (string.Equals(objects[i].ClassName, obj.ClassName, StringComparison.OrdinalIgnoreCase))
            {
                objects[i] = obj;
                return;
            }
        }
        objects.Add(obj);
    }

    private static bool ParseGlow(Gldefs g, TokenStream tokens, ref int i, IReadOnlyDictionary<string, X11Color>? knownColors)
    {
        var t = tokens.Text;
        i++; // glow
        if (i >= t.Count || t[i] != "{") return false;
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
                    if (string.IsNullOrEmpty(name)) continue;
                    if (p == "flats") g.GlowFlats.Add(name);
                    else g.GlowTextures.Add(name);
                    g.Glows[name] = new GldefsGlow(name, 1.0f, 1.0f, 1.0f, Height: 128, Fullbright: true, CalculateTextureColor: true);
                }
                if (i < t.Count) i++; // }
            }
            else if (p == "texture" && i < t.Count)
            {
                if (!ParseGlowTexture(g, tokens, ref i, knownColors)) return false;
            }
            else if (p == "texture")
            {
                return false;
            }
            else if (p is "flats" or "walls")
            {
                return false;
            }
        }
        if (i < t.Count) i++; // }
        return true;
    }

    private static bool ParseGlowTexture(Gldefs g, TokenStream tokens, ref int i, IReadOnlyDictionary<string, X11Color>? knownColors)
    {
        var t = tokens.Text;
        if (IsInvalidLongTextureName(tokens, i)) return false;
        string texture = t[i++];
        if (string.IsNullOrEmpty(texture)) return false;
        float r = 1.0f, green = 1.0f, b = 1.0f;
        int height = 64;
        bool fullbright = false;

        if (i >= t.Count || t[i] != ",") return false;
        i++;
        if (i >= t.Count || !TryReadColorToken(t[i++], knownColors, out var color)) return false;
        r = color.R;
        green = color.G;
        b = color.B;

        if (i < t.Count && t[i] == ",")
        {
            i++;
            if (i >= t.Count) return false;

            string token = t[i];
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedHeight))
            {
                height = parsedHeight;
                i++;
                if (i < t.Count && t[i] == ",")
                {
                    i++;
                    if (i >= t.Count) return false;
                    token = t[i];
                }
                else
                {
                    g.GlowTextures.Add(texture);
                    g.Glows[texture] = new GldefsGlow(texture, r, green, b, height * 2, fullbright);
                    return true;
                }
            }

            if (!token.Equals("fullbright", StringComparison.OrdinalIgnoreCase)) return false;
            fullbright = true;
            i++;
        }

        g.GlowTextures.Add(texture);
        g.Glows[texture] = new GldefsGlow(texture, r, green, b, height * 2, fullbright);
        return true;
    }

    private static bool ParseSkybox(Gldefs g, TokenStream t, ref int i)
    {
        i++; // skybox
        if (i >= t.Text.Count || IsInvalidLongTextureName(t, i)) return false;
        var skybox = new GldefsSkybox { Name = t.Text[i++].ToUpperInvariant() };
        if (skybox.Name.Length == 0) return false;
        if (i < t.Text.Count && t.Text[i].Equals("fliptop", StringComparison.OrdinalIgnoreCase))
        {
            skybox.FlipTop = true;
            i++;
        }
        if (i >= t.Text.Count || t.Text[i] != "{") return false;

        i++;
        while (i < t.Text.Count && t.Text[i] != "}") skybox.Textures.Add(t.Text[i++]);
        if (i < t.Text.Count) i++;

        if (skybox.Textures.Count != 3 && skybox.Textures.Count != 6) return false;
        g.Skyboxes[skybox.Name] = skybox;
        return true;
    }

    private static bool IsInvalidLongTextureName(TokenStream t, int i)
        => t.Text[i].Length > 8 && !t.Quoted[i];

    private static float ReadFloat(List<string> t, ref int i)
    {
        if (i < t.Count && float.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) { i++; return v; }
        return 0;
    }

    private static bool TryReadFloat(List<string> t, ref int i, out float value)
    {
        value = 0;
        if (i >= t.Count) return false;
        if (float.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            i++;
            return true;
        }
        i++;
        return false;
    }

    private static bool TryReadInt(List<string> t, ref int i, out int value)
    {
        value = 0;
        if (i >= t.Count) return false;
        if (int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            i++;
            return true;
        }
        i++;
        return false;
    }

    private static bool TryReadSignedFloat(List<string> t, ref int i, out float value)
    {
        value = 0;
        int sign = 1;
        if (i < t.Count && t[i] == "-")
        {
            sign = -1;
            i++;
        }

        if (i >= t.Count) return false;
        if (!float.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
        {
            i++;
            return false;
        }

        i++;
        value = parsed * sign;
        return true;
    }

    private static bool TryReadSignedInt(List<string> t, ref int i, out int value)
    {
        value = 0;
        int sign = 1;
        if (i < t.Count && t[i] == "-")
        {
            sign = -1;
            i++;
        }

        if (i >= t.Count) return false;
        if (!int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            i++;
            return false;
        }

        i++;
        value = parsed * sign;
        return true;
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

    private static bool TryReadColorToken(string value, IReadOnlyDictionary<string, X11Color>? knownColors, out (float R, float G, float B) color)
    {
        color = default;
        if (ZDoomColorParser.TryParse(value, knownColors, out byte red, out byte green, out byte blue))
        {
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

    private static TokenStream Tokenize(string s)
    {
        var toks = new List<string>();
        var quoted = new List<bool>();
        int n = s.Length;
        for (int p = 0; p < n;)
        {
            char c = s[p];
            if (char.IsWhiteSpace(c)) { p++; continue; }
            if (c == '/' && p + 1 < n && s[p + 1] == '/') { p += 2; while (p < n && s[p] != '\n') p++; continue; }
            if (c == '/' && p + 1 < n && s[p + 1] == '*') { p += 2; while (p + 1 < n && !(s[p] == '*' && s[p + 1] == '/')) p++; p += 2; continue; }
            if (c == '"') { var sb = new StringBuilder(); p++; while (p < n && s[p] != '"') sb.Append(s[p++]); p++; toks.Add(sb.ToString()); quoted.Add(true); continue; }
            if (c == '{' || c == '}' || c == ',') { toks.Add(c.ToString()); quoted.Add(false); p++; continue; }
            int b = p;
            while (p < n && !char.IsWhiteSpace(s[p]) && s[p] != '{' && s[p] != '}' && s[p] != ',' && s[p] != '"') p++;
            toks.Add(s.Substring(b, p - b));
            quoted.Add(false);
        }
        return new TokenStream(toks, quoted);
    }

    private sealed record TokenStream(List<string> Text, List<bool> Quoted);
}
