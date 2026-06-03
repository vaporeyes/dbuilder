// ABOUTME: Parser for the ZDoom TEXTURES lump - composite Texture/Sprite/Graphic/WallTexture/Flat definitions.
// ABOUTME: Extracts name/size/offset/scale and the patch list (name, x, y, flip) so resources can be composed.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DBuilder.IO;

public enum TexturesType { Texture, Sprite, WallTexture, Flat }

public enum TexturesPatchBlendStyle { None, Blend, Tint }

public enum TexturesPatchRenderStyle
{
    Copy,
    Translucent,
    Add,
    Subtract,
    ReverseSubtract,
    Modulate,
    CopyAlpha,
    CopyNewAlpha,
    Overlay,
}

/// <summary>A single patch placed within a TEXTURES definition.</summary>
public sealed class TexturesPatch
{
    public string Name { get; init; } = "";
    public int X { get; init; }
    public int Y { get; init; }
    public bool FlipX { get; set; }
    public bool FlipY { get; set; }
    public double Alpha { get; set; } = 1.0;
    public int Rotation { get; set; }
    public string? Style { get; set; }
    public TexturesPatchRenderStyle RenderStyle { get; set; }
    public TexturesPatchBlendStyle BlendStyle { get; set; }
    public byte BlendRed { get; set; }
    public byte BlendGreen { get; set; }
    public byte BlendBlue { get; set; }
    public byte BlendAlpha { get; set; }
    public bool Skip { get; set; }
}

/// <summary>A composite definition from the TEXTURES lump.</summary>
public sealed class TexturesDef
{
    public TexturesType Type { get; init; }
    public string Name { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public double ScaleX { get; set; }
    public double ScaleY { get; set; }
    public bool Optional { get; set; }
    public bool NullTexture { get; set; }
    public bool WorldPanning { get; set; }
    public List<TexturesPatch> Patches { get; } = new();
    internal int ResourceIndex { get; set; }
}

public static class TexturesParser
{
    private const int ClassicImageNameLength = 8;

    /// <summary>Parses a TEXTURES lump into its definitions (malformed entries are skipped).</summary>
    public static List<TexturesDef> Parse(string text)
        => Parse(text, knownColors: null);

    public static List<TexturesDef> Parse(string text, int maxTextureNameLength)
        => Parse(text, knownColors: null, maxTextureNameLength);

    public static List<TexturesDef> Parse(string text, IReadOnlyDictionary<string, X11Color>? knownColors)
        => Parse(text, knownColors, int.MaxValue);

    public static List<TexturesDef> Parse(string text, IReadOnlyDictionary<string, X11Color>? knownColors, int maxTextureNameLength)
    {
        var defs = new List<TexturesDef>();
        var t = Tokenize(text);
        int i = 0;
        while (i < t.Count)
        {
            string word = t[i];
            if (word.Equals("$gzdb_skip", StringComparison.OrdinalIgnoreCase)) break;
            if (word.Equals("optional", StringComparison.OrdinalIgnoreCase))
            {
                SkipUnknownTopLevelStructure(t, ref i);
                continue;
            }

            var type = word.ToLowerInvariant() switch
            {
                "texture" => TexturesType.Texture,
                "sprite" => TexturesType.Sprite,
                "walltexture" => TexturesType.WallTexture,
                "flat" => TexturesType.Flat,
                _ => (TexturesType?)null,
            };
            if (type == null)
            {
                i++;
                TrySkipUnknownTopLevelBlock(t, ref i);
                continue;
            }

            i++; // type
            var def = ParseDefinition(type.Value, optional: false, t, ref i, knownColors, maxTextureNameLength, out bool stopParsing);
            if (stopParsing) return defs;
            if (def != null) defs.Add(def);
        }
        return defs;
    }

    private static void TrySkipUnknownTopLevelBlock(List<Tok> t, ref int i)
    {
        while (i < t.Count && t[i] != "{")
        {
            i++;
        }
        if (i >= t.Count) return;

        int depth = 0;
        while (i < t.Count)
        {
            if (t[i] == "{") depth++;
            else if (t[i] == "}")
            {
                depth--;
                if (depth == 0) { i++; return; }
            }
            i++;
        }
    }

    private static void SkipUnknownTopLevelStructure(List<Tok> t, ref int i)
    {
        while (i < t.Count && t[i] != "{") i++;
        if (i >= t.Count) return;

        int depth = 0;
        while (i < t.Count)
        {
            if (t[i] == "{") depth++;
            else if (t[i] == "}")
            {
                depth--;
                if (depth == 0) { i++; return; }
            }
            i++;
        }
    }

    private static TexturesDef? ParseDefinition(TexturesType type, bool optional, List<Tok> t, ref int i, IReadOnlyDictionary<string, X11Color>? knownColors, int maxTextureNameLength, out bool stopParsing)
    {
        stopParsing = false;
        if (i >= t.Count) return null;
        Tok nameToken = t[i++];
        string name = nameToken.Text;
        if (name.Equals("optional", StringComparison.OrdinalIgnoreCase))
        {
            optional = true;
            if (i >= t.Count) return null;
            nameToken = t[i++];
            name = nameToken.Text;
        }
        if (string.IsNullOrEmpty(name))
        {
            stopParsing = true;
            return null;
        }
        if (IsInvalidLongTextureName(nameToken))
        {
            stopParsing = true;
            return null;
        }
        if (type != TexturesType.Sprite && name.Length > maxTextureNameLength)
        {
            stopParsing = true;
            return null;
        }
        if (type == TexturesType.Sprite && name.Length is not (6 or 8))
        {
            stopParsing = true;
            return null;
        }
        if (!ReadComma(t, ref i) || !ReadInt(t, ref i, out int width) || !ReadComma(t, ref i) || !ReadInt(t, ref i, out int height))
        {
            stopParsing = true;
            return null;
        }

        var def = new TexturesDef { Type = type, Name = name, Width = width, Height = height, Optional = optional };
        if (i >= t.Count || t[i] != "{")
        {
            stopParsing = true;
            return null;
        }
        bool invalid = false;

        if (i < t.Count && t[i] == "{")
        {
            i++; // {
            while (i < t.Count && t[i] != "}")
            {
                string kw = t[i++].Text.ToLowerInvariant();
                switch (kw)
                {
                    case "xscale":
                        if (ReadDouble(t, ref i, out double sx)) def.ScaleX = NormalizeScale(sx);
                        else invalid = true;
                        break;
                    case "yscale":
                        if (ReadDouble(t, ref i, out double sy)) def.ScaleY = NormalizeScale(sy);
                        else invalid = true;
                        break;
                    case "worldpanning": def.WorldPanning = true; break;
                    case "nulltexture": def.NullTexture = true; break;
                    case "offset":
                        if (ReadInt(t, ref i, out int ox) && ReadComma(t, ref i) && ReadInt(t, ref i, out int oy))
                        {
                            def.OffsetX = ox;
                            def.OffsetY = oy;
                        }
                        else invalid = true;
                        break;
                    case "patch":
                        if (!ParsePatch(def, t, ref i, knownColors)) invalid = true;
                        break;
                    default: break; // unknown single-token flag/value; skip
                }
            }
            if (i < t.Count) i++; // }
        }
        if (invalid)
        {
            stopParsing = true;
            return null;
        }
        return def;
    }

    private static double NormalizeScale(double value) => value == 0.0 ? 0.0 : 1.0 / value;

    private static bool ParsePatch(TexturesDef def, List<Tok> t, ref int i, IReadOnlyDictionary<string, X11Color>? knownColors)
    {
        if (i >= t.Count) return false;
        Tok nameToken = t[i++];
        string name = nameToken.Text;
        if (string.IsNullOrEmpty(name)) return false;
        if (IsInvalidLongTextureName(nameToken)) return false;
        if (!ReadComma(t, ref i) || !ReadInt(t, ref i, out int x) || !ReadComma(t, ref i) || !ReadInt(t, ref i, out int y)) return false;
        var patch = new TexturesPatch
        {
            Name = name.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar),
            X = x,
            Y = y,
            Skip = name.Equals("TNT1A0", StringComparison.OrdinalIgnoreCase),
        };

        if (i < t.Count && t[i] == "{")
        {
            i++; // {
            bool invalid = false;
            while (i < t.Count && t[i] != "}")
            {
                Tok modifier = t[i++];
                switch (modifier.Text.ToLowerInvariant())
                {
                    case "flipx": patch.FlipX = true; break;
                    case "flipy": patch.FlipY = true; break;
                    case "alpha":
                        if (ReadDouble(t, ref i, out double alpha)) patch.Alpha = Math.Clamp(alpha, 0.0, 1.0);
                        else invalid = true;
                        break;
                    case "rotate":
                        if (ReadInt(t, ref i, out int rotation)) patch.Rotation = NormalizeRotation(rotation);
                        else invalid = true;
                        break;
                    case "style":
                        if (i < t.Count)
                        {
                            patch.Style = t[i++].Text;
                            patch.RenderStyle = ParseRenderStyle(patch.Style);
                        }
                        break;
                    case "blend":
                        if (!ParseBlend(patch, t, ref i, knownColors, modifier.Line)) invalid = true;
                        break;
                    default: break; // translation and other patch modifiers are skipped token by token
                }
            }
            if (i < t.Count) i++; // }
            if (invalid) return false;
        }
        def.Patches.Add(patch);
        return true;
    }

    private static bool IsInvalidLongTextureName(Tok token)
        => token.Text.Length > ClassicImageNameLength && !token.Quoted;

    private static bool ParseBlend(TexturesPatch patch, List<Tok> t, ref int i, IReadOnlyDictionary<string, X11Color>? knownColors, int modifierLine)
    {
        if (i >= t.Count) return false;

        byte red;
        byte green;
        byte blue;
        Tok colorToken = t[i++];
        if (colorToken.Line != modifierLine) return false;
        int line = colorToken.Line;
        string token = colorToken.Text;
        if (byte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out red))
        {
            if (!ReadComma(t, ref i, line)) return false;
            if (!ReadByte(t, ref i, line, out green)) return false;
            if (!ReadComma(t, ref i, line)) return false;
            if (!ReadByte(t, ref i, line, out blue)) return false;
        }
        else if (!ZDoomColorParser.TryParse(token, knownColors, out red, out green, out blue))
        {
            return false;
        }

        double blendAlpha = -1.0;
        if (i < t.Count && t[i].Line == line && t[i] == ",")
        {
            i++;
            if (!ReadDouble(t, ref i, line, out blendAlpha)) return false;
        }

        if (blendAlpha > 0.0)
        {
            patch.BlendAlpha = (byte)Math.Clamp((int)(blendAlpha * 255.0), 1, 254);
            patch.BlendStyle = TexturesPatchBlendStyle.Tint;
        }
        else if (blendAlpha < 0.0)
        {
            patch.BlendAlpha = 255;
            patch.BlendStyle = TexturesPatchBlendStyle.Blend;
        }
        else
        {
            return true;
        }

        patch.BlendRed = red;
        patch.BlendGreen = green;
        patch.BlendBlue = blue;
        return true;
    }

    private static int NormalizeRotation(int rotation)
    {
        rotation %= 360;
        if (rotation < 0) rotation += 360;
        return rotation is 90 or 180 or 270 ? rotation : 0;
    }

    private static TexturesPatchRenderStyle ParseRenderStyle(string value)
    {
        switch (value.ToLowerInvariant())
        {
            case "translucent": return TexturesPatchRenderStyle.Translucent;
            case "add": return TexturesPatchRenderStyle.Add;
            case "subtract": return TexturesPatchRenderStyle.Subtract;
            case "reversesubtract": return TexturesPatchRenderStyle.ReverseSubtract;
            case "modulate": return TexturesPatchRenderStyle.Modulate;
            case "copyalpha": return TexturesPatchRenderStyle.CopyAlpha;
            case "copynewalpha": return TexturesPatchRenderStyle.CopyNewAlpha;
            case "overlay": return TexturesPatchRenderStyle.Overlay;
            default: return TexturesPatchRenderStyle.Copy;
        }
    }

    private static bool ReadComma(List<Tok> t, ref int i)
    {
        if (i >= t.Count || t[i] != ",") return false;
        i++;
        return true;
    }

    private static bool ReadComma(List<Tok> t, ref int i, int line)
    {
        if (i >= t.Count || t[i].Line != line || t[i] != ",") return false;
        i++;
        return true;
    }

    private static bool ReadInt(List<Tok> t, ref int i, out int v)
    {
        v = 0;
        if (i >= t.Count) return false;
        if (int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) { i++; return true; }
        return false;
    }

    private static bool ReadDouble(List<Tok> t, ref int i, out double v)
    {
        v = 0;
        if (i < t.Count && double.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) { i++; return true; }
        return false;
    }

    private static bool ReadDouble(List<Tok> t, ref int i, int line, out double v)
    {
        v = 0;
        if (i < t.Count && t[i].Line == line && double.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) { i++; return true; }
        return false;
    }

    private static bool ReadByte(List<Tok> t, ref int i, out byte value)
    {
        value = 0;
        if (i >= t.Count) return false;
        if (byte.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) { i++; return true; }
        return false;
    }

    private static bool ReadByte(List<Tok> t, ref int i, int line, out byte value)
    {
        value = 0;
        if (i >= t.Count || t[i].Line != line) return false;
        if (byte.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) { i++; return true; }
        return false;
    }

    // Tokenizes into words / quoted strings / single-char symbols ({ } ,), skipping // and /* */ comments.
    private static List<Tok> Tokenize(string s)
    {
        var toks = new List<Tok>();
        int n = s.Length;
        int line = 1;
        for (int p = 0; p < n;)
        {
            char c = s[p];
            if (char.IsWhiteSpace(c))
            {
                if (c == '\n') line++;
                p++;
                continue;
            }
            if (c == '#' && IsRegionDirectiveLine(s, p))
            {
                while (p < n && s[p] != '\n') p++;
                continue;
            }
            if (c == '/' && p + 1 < n && s[p + 1] == '/')
            {
                if (p + 2 < n && s[p + 2] == '$')
                {
                    int tokenStart = p + 2;
                    p = tokenStart;
                    while (p < n && s[p] != '\n') p++;
                    toks.Add(new Tok(s.Substring(tokenStart, p - tokenStart), false, line));
                }
                else
                {
                    p += 2;
                    while (p < n && s[p] != '\n') p++;
                }
                continue;
            }
            if (c == '/' && p + 1 < n && s[p + 1] == '*')
            {
                p += 2;
                while (p + 1 < n && !(s[p] == '*' && s[p + 1] == '/'))
                {
                    if (s[p] == '\n') line++;
                    p++;
                }
                p += 2;
                continue;
            }

            if (c == '"')
            {
                var sb = new StringBuilder();
                int tokenLine = line;
                p++;
                while (p < n && s[p] != '"')
                {
                    if (s[p] == '\n') line++;
                    sb.Append(s[p++]);
                }
                p++;
                toks.Add(new Tok(sb.ToString(), true, tokenLine));
                continue;
            }
            if (c == '{' || c == '}' || c == ',') { toks.Add(new Tok(c.ToString(), false, line)); p++; continue; }

            int b = p;
            while (p < n && !char.IsWhiteSpace(s[p]) && s[p] != '{' && s[p] != '}' && s[p] != ',' && s[p] != '"') p++;
            toks.Add(new Tok(s.Substring(b, p - b), false, line));
        }
        return toks;
    }

    private static bool IsRegionDirectiveLine(string text, int position)
    {
        int p = position + 1;
        while (p < text.Length && (text[p] == ' ' || text[p] == '\t')) p++;

        return StartsDirective(text, p, "region") || StartsDirective(text, p, "endregion");
    }

    private static bool StartsDirective(string text, int position, string directive)
    {
        if (position + directive.Length > text.Length) return false;
        if (!text.AsSpan(position, directive.Length).Equals(directive, StringComparison.OrdinalIgnoreCase)) return false;

        int end = position + directive.Length;
        return end == text.Length || char.IsWhiteSpace(text[end]);
    }

    private readonly record struct Tok(string Text, bool Quoted, int Line)
    {
        public static implicit operator string(Tok token) => token.Text;
    }
}
