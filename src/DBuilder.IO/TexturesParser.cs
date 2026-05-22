// ABOUTME: Parser for the ZDoom TEXTURES lump - composite Texture/Sprite/Graphic/WallTexture/Flat definitions.
// ABOUTME: Extracts name/size/offset/scale and the patch list (name, x, y, flip) so resources can be composed.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DBuilder.IO;

public enum TexturesType { Texture, Sprite, Graphic, WallTexture, Flat }

/// <summary>A single patch placed within a TEXTURES definition.</summary>
public sealed class TexturesPatch
{
    public string Name { get; init; } = "";
    public int X { get; init; }
    public int Y { get; init; }
    public bool FlipX { get; set; }
    public bool FlipY { get; set; }
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
            if (word == "optional") { i++; if (i >= t.Count) break; word = t[i]; }

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
            var def = ParseDefinition(type.Value, t, ref i);
            if (def != null) defs.Add(def);
        }
        return defs;
    }

    private static TexturesDef? ParseDefinition(TexturesType type, List<string> t, ref int i)
    {
        if (i >= t.Count) return null;
        string name = t[i++];
        SkipCommas(t, ref i);
        if (!ReadInt(t, ref i, out int width)) return null;
        SkipCommas(t, ref i);
        if (!ReadInt(t, ref i, out int height)) return null;

        var def = new TexturesDef { Type = type, Name = name, Width = width, Height = height };

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
        var patch = new TexturesPatch { Name = name, X = x, Y = y };

        if (i < t.Count && t[i] == "{")
        {
            i++; // {
            while (i < t.Count && t[i] != "}")
            {
                switch (t[i++].ToLowerInvariant())
                {
                    case "flipx": patch.FlipX = true; break;
                    case "flipy": patch.FlipY = true; break;
                    default: break; // rotate/alpha/style/translation/... skipped token by token
                }
            }
            if (i < t.Count) i++; // }
        }
        def.Patches.Add(patch);
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
