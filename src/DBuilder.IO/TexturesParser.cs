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
}

public static class TexturesParser
{
    /// <summary>Parses a TEXTURES lump into its definitions (malformed entries are skipped).</summary>
    public static List<TexturesDef> Parse(string text)
    {
        var defs = new List<TexturesDef>();
        var t = Tokenize(text);
        int i = 0;
        while (i < t.Count)
        {
            string word = t[i];
            bool optional = false;
            if (word.Equals("optional", StringComparison.OrdinalIgnoreCase)) { optional = true; i++; if (i >= t.Count) break; word = t[i]; }

            var type = word.ToLowerInvariant() switch
            {
                "texture" => TexturesType.Texture,
                "sprite" => TexturesType.Sprite,
                "graphic" => TexturesType.Graphic,
                "walltexture" => TexturesType.WallTexture,
                "flat" => TexturesType.Flat,
                _ => (TexturesType?)null,
            };
            if (type == null) { i++; continue; }

            i++; // type
            var def = ParseDefinition(type.Value, optional, t, ref i);
            if (def != null) defs.Add(def);
        }
        return defs;
    }

    private static TexturesDef? ParseDefinition(TexturesType type, bool optional, List<string> t, ref int i)
    {
        if (i >= t.Count) return null;
        string name = t[i++];
        if (name.Equals("optional", StringComparison.OrdinalIgnoreCase))
        {
            optional = true;
            if (i >= t.Count) return null;
            name = t[i++];
        }
        SkipCommas(t, ref i);
        if (!ReadInt(t, ref i, out int width)) return null;
        SkipCommas(t, ref i);
        if (!ReadInt(t, ref i, out int height)) return null;

        var def = new TexturesDef { Type = type, Name = name, Width = width, Height = height, Optional = optional };

        if (i < t.Count && t[i] == "{")
        {
            i++; // {
            while (i < t.Count && t[i] != "}")
            {
                string kw = t[i++].ToLowerInvariant();
                switch (kw)
                {
                    case "xscale": SkipCommas(t, ref i); if (ReadDouble(t, ref i, out double sx)) def.ScaleX = sx; break;
                    case "yscale": SkipCommas(t, ref i); if (ReadDouble(t, ref i, out double sy)) def.ScaleY = sy; break;
                    case "worldpanning": def.WorldPanning = true; break;
                    case "nulltexture": def.NullTexture = true; break;
                    case "offset":
                    case "offsets":
                        SkipCommas(t, ref i); if (ReadInt(t, ref i, out int ox)) def.OffsetX = ox;
                        SkipCommas(t, ref i); if (ReadInt(t, ref i, out int oy)) def.OffsetY = oy;
                        break;
                    case "patch": ParsePatch(def, t, ref i); break;
                    default: break; // unknown single-token flag/value; skip
                }
            }
            if (i < t.Count) i++; // }
        }
        return def;
    }

    private static void ParsePatch(TexturesDef def, List<string> t, ref int i)
    {
        if (i >= t.Count) return;
        string name = t[i++];
        SkipCommas(t, ref i);
        ReadInt(t, ref i, out int x);
        SkipCommas(t, ref i);
        ReadInt(t, ref i, out int y);
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
            while (i < t.Count && t[i] != "}")
            {
                switch (t[i++].ToLowerInvariant())
                {
                    case "flipx": patch.FlipX = true; break;
                    case "flipy": patch.FlipY = true; break;
                    case "alpha": if (ReadDouble(t, ref i, out double alpha)) patch.Alpha = Math.Clamp(alpha, 0.0, 1.0); break;
                    case "rotate": if (ReadInt(t, ref i, out int rotation)) patch.Rotation = NormalizeRotation(rotation); break;
                    case "style":
                        if (i < t.Count)
                        {
                            patch.Style = t[i++];
                            patch.RenderStyle = ParseRenderStyle(patch.Style);
                        }
                        break;
                    case "blend": ParseBlend(patch, t, ref i); break;
                    default: break; // translation and other patch modifiers are skipped token by token
                }
            }
            if (i < t.Count) i++; // }
        }
        def.Patches.Add(patch);
    }

    private static void ParseBlend(TexturesPatch patch, List<string> t, ref int i)
    {
        if (i >= t.Count) return;

        byte red;
        byte green;
        byte blue;
        string token = t[i++];
        if (byte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out red))
        {
            SkipCommas(t, ref i);
            if (!ReadByte(t, ref i, out green)) return;
            SkipCommas(t, ref i);
            if (!ReadByte(t, ref i, out blue)) return;
        }
        else if (!TryParseColor(token, out red, out green, out blue))
        {
            return;
        }

        double blendAlpha = -1.0;
        if (i < t.Count && t[i] == ",")
        {
            i++;
            ReadDouble(t, ref i, out blendAlpha);
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

    private static void SkipCommas(List<string> t, ref int i) { while (i < t.Count && t[i] == ",") i++; }

    private static bool ReadInt(List<string> t, ref int i, out int v)
    {
        v = 0;
        if (i >= t.Count) return false;
        // Some files use float offsets; accept and truncate.
        if (int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) { i++; return true; }
        if (double.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) { v = (int)d; i++; return true; }
        return false;
    }

    private static bool ReadDouble(List<string> t, ref int i, out double v)
    {
        v = 0;
        if (i < t.Count && double.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) { i++; return true; }
        return false;
    }

    private static bool ReadByte(List<string> t, ref int i, out byte value)
    {
        value = 0;
        if (i >= t.Count) return false;
        if (byte.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) { i++; return true; }
        return false;
    }

    private static bool TryParseColor(string token, out byte red, out byte green, out byte blue)
    {
        red = green = blue = 0;
        string value = token.Trim();
        if (value.StartsWith("#", StringComparison.Ordinal)) value = value.Substring(1);
        if (value.Length == 6 && int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
        {
            red = (byte)((rgb >> 16) & 0xFF);
            green = (byte)((rgb >> 8) & 0xFF);
            blue = (byte)(rgb & 0xFF);
            return true;
        }

        switch (value.ToLowerInvariant())
        {
            case "black": red = 0; green = 0; blue = 0; return true;
            case "white": red = 255; green = 255; blue = 255; return true;
            case "red": red = 255; green = 0; blue = 0; return true;
            case "green": red = 0; green = 255; blue = 0; return true;
            case "blue": red = 0; green = 0; blue = 255; return true;
            default: return false;
        }
    }

    // Tokenizes into words / quoted strings / single-char symbols ({ } ,), skipping // and /* */ comments.
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

            if (c == '"')
            {
                var sb = new StringBuilder();
                p++;
                while (p < n && s[p] != '"') { if (s[p] == '\\' && p + 1 < n) { sb.Append(s[p + 1]); p += 2; } else sb.Append(s[p++]); }
                p++;
                toks.Add(sb.ToString());
                continue;
            }
            if (c == '{' || c == '}' || c == ',') { toks.Add(c.ToString()); p++; continue; }

            int b = p;
            while (p < n && !char.IsWhiteSpace(s[p]) && s[p] != '{' && s[p] != '}' && s[p] != ',' && s[p] != '"') p++;
            toks.Add(s.Substring(b, p - b));
        }
        return toks;
    }
}
