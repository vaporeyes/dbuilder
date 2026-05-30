// ABOUTME: Parser for the ZDoom TEXTURES lump - composite Texture/Sprite/Graphic/WallTexture/Flat definitions.
// ABOUTME: Extracts name/size/offset/scale and the patch list (name, x, y, flip) so resources can be composed.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DBuilder.IO;

public enum TexturesType { Texture, Sprite, Graphic, WallTexture, Flat }

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
    public double ScaleX { get; set; } = 1.0;
    public double ScaleY { get; set; } = 1.0;
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

    public static List<TexturesDef> Parse(string text, IReadOnlyDictionary<string, X11Color>? knownColors)
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
                "graphic" => TexturesType.Graphic,
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
            var def = ParseDefinition(type.Value, optional: false, t, ref i, knownColors);
            if (def != null) defs.Add(def);
        }
        return defs;
    }

    private static void TrySkipUnknownTopLevelBlock(List<Tok> t, ref int i)
    {
        while (i < t.Count && t[i] != "{")
        {
            if (IsTopLevelTypeToken(t[i].Text)) return;
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

    private static bool IsTopLevelTypeToken(string word)
    {
        return word.Equals("texture", StringComparison.OrdinalIgnoreCase)
            || word.Equals("sprite", StringComparison.OrdinalIgnoreCase)
            || word.Equals("graphic", StringComparison.OrdinalIgnoreCase)
            || word.Equals("walltexture", StringComparison.OrdinalIgnoreCase)
            || word.Equals("flat", StringComparison.OrdinalIgnoreCase)
            || word.Equals("$gzdb_skip", StringComparison.OrdinalIgnoreCase);
    }

    private static TexturesDef? ParseDefinition(TexturesType type, bool optional, List<Tok> t, ref int i, IReadOnlyDictionary<string, X11Color>? knownColors)
    {
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
        if (IsInvalidLongTextureName(nameToken)) return null;
        if (type == TexturesType.Sprite && name.Length is not (6 or 8)) return null;
        if (!ReadComma(t, ref i)) return null;
        if (!ReadInt(t, ref i, out int width)) return null;
        if (!ReadComma(t, ref i)) return null;
        if (!ReadInt(t, ref i, out int height)) return null;

        var def = new TexturesDef { Type = type, Name = name, Width = width, Height = height, Optional = optional };
        if (i >= t.Count || t[i] != "{") return null;
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
                        SkipCommas(t, ref i);
                        if (ReadDouble(t, ref i, out double sx)) def.ScaleX = NormalizeScale(sx);
                        else invalid = true;
                        break;
                    case "yscale":
                        SkipCommas(t, ref i);
                        if (ReadDouble(t, ref i, out double sy)) def.ScaleY = NormalizeScale(sy);
                        else invalid = true;
                        break;
                    case "worldpanning": def.WorldPanning = true; break;
                    case "nulltexture": def.NullTexture = true; break;
                    case "offset":
                        SkipCommas(t, ref i);
                        if (ReadInt(t, ref i, out int ox) && ReadComma(t, ref i) && ReadInt(t, ref i, out int oy))
                        {
                            def.OffsetX = ox;
                            def.OffsetY = oy;
                        }
                        else invalid = true;
                        break;
                    case "patch": ParsePatch(def, t, ref i, knownColors); break;
                    default: break; // unknown single-token flag/value; skip
                }
            }
            if (i < t.Count) i++; // }
        }
        return invalid ? null : def;
    }

    private static double NormalizeScale(double value) => value == 0.0 ? 1.0 : 1.0 / value;

    private static void ParsePatch(TexturesDef def, List<Tok> t, ref int i, IReadOnlyDictionary<string, X11Color>? knownColors)
    {
        if (i >= t.Count) return;
        Tok nameToken = t[i++];
        string name = nameToken.Text;
        if (IsInvalidLongTextureName(nameToken)) return;
        if (!ReadComma(t, ref i)) return;
        if (!ReadInt(t, ref i, out int x)) return;
        if (!ReadComma(t, ref i)) return;
        if (!ReadInt(t, ref i, out int y)) return;
        var patch = new TexturesPatch
        {
            Name = name.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).ToUpperInvariant(),
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
                switch (t[i++].Text.ToLowerInvariant())
                {
                    case "flipx": patch.FlipX = true; break;
                    case "flipy": patch.FlipY = true; break;
                    case "alpha":
                        if (ReadDouble(t, ref i, out double alpha)) patch.Alpha = Math.Clamp(alpha, 0.0, 1.0);
                        else invalid = true;
                        break;
                    case "rotate": if (ReadInt(t, ref i, out int rotation)) patch.Rotation = NormalizeRotation(rotation); break;
                    case "style":
                        if (i < t.Count)
                        {
                            patch.Style = t[i++].Text;
                            patch.RenderStyle = ParseRenderStyle(patch.Style);
                        }
                        break;
                    case "blend": ParseBlend(patch, t, ref i, knownColors); break;
                    default: break; // translation and other patch modifiers are skipped token by token
                }
            }
            if (i < t.Count) i++; // }
            if (invalid) return;
        }
        def.Patches.Add(patch);
    }

    private static bool IsInvalidLongTextureName(Tok token)
        => token.Text.Length > ClassicImageNameLength && !token.Quoted;

    private static void ParseBlend(TexturesPatch patch, List<Tok> t, ref int i, IReadOnlyDictionary<string, X11Color>? knownColors)
    {
        if (i >= t.Count) return;

        byte red;
        byte green;
        byte blue;
        string token = t[i++];
        if (byte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out red))
        {
            if (!ReadComma(t, ref i)) return;
            if (!ReadByte(t, ref i, out green)) return;
            if (!ReadComma(t, ref i)) return;
            if (!ReadByte(t, ref i, out blue)) return;
        }
        else if (!ZDoomColorParser.TryParse(token, knownColors, out red, out green, out blue))
        {
            return;
        }

        double blendAlpha = -1.0;
        if (i < t.Count && t[i] == ",")
        {
            i++;
            if (!ReadDouble(t, ref i, out blendAlpha)) return;
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
            return;
        }

        patch.BlendRed = red;
        patch.BlendGreen = green;
        patch.BlendBlue = blue;
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

    private static void SkipCommas(List<Tok> t, ref int i) { while (i < t.Count && t[i] == ",") i++; }

    private static bool ReadComma(List<Tok> t, ref int i)
    {
        if (i >= t.Count || t[i] != ",") return false;
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

    private static bool ReadByte(List<Tok> t, ref int i, out byte value)
    {
        value = 0;
        if (i >= t.Count) return false;
        if (byte.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) { i++; return true; }
        return false;
    }

    // Tokenizes into words / quoted strings / single-char symbols ({ } ,), skipping // and /* */ comments.
    private static List<Tok> Tokenize(string s)
    {
        var toks = new List<Tok>();
        int n = s.Length;
        for (int p = 0; p < n;)
        {
            char c = s[p];
            if (char.IsWhiteSpace(c)) { p++; continue; }
            if (c == '/' && p + 1 < n && s[p + 1] == '/') { p += 2; while (p < n && s[p] != '\n') p++; continue; }
            if (c == '/' && p + 1 < n && s[p + 1] == '*') { p += 2; while (p + 1 < n && !(s[p] == '*' && s[p + 1] == '/')) p++; p += 2; continue; }

            if (c == '"')
            {
                var sb = new StringBuilder();
                p++;
                while (p < n && s[p] != '"') { if (s[p] == '\\' && p + 1 < n) { sb.Append(s[p + 1]); p += 2; } else sb.Append(s[p++]); }
                p++;
                toks.Add(new Tok(sb.ToString(), true));
                continue;
            }
            if (c == '{' || c == '}' || c == ',') { toks.Add(new Tok(c.ToString(), false)); p++; continue; }

            int b = p;
            while (p < n && !char.IsWhiteSpace(s[p]) && s[p] != '{' && s[p] != '}' && s[p] != ',' && s[p] != '"') p++;
            toks.Add(new Tok(s.Substring(b, p - b), false));
        }
        return toks;
    }

    private readonly record struct Tok(string Text, bool Quoted)
    {
        public static implicit operator string(Tok token) => token.Text;
    }
}
