// ABOUTME: Parser for the ZDoom ANIMDEFS lump - animated texture/flat sequences and switch definitions.
// ABOUTME: Captures range form ("flat A range B tics N"), block form ("texture X { pic ... }") and switches.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DBuilder.IO;

public enum AnimKind { Texture, Flat }

/// <summary>One frame of an explicit animation block.</summary>
public sealed record AnimFrame(string Texture, int Tics);

/// <summary>An animated texture/flat: either a range (first..RangeLast) or an explicit frame list.</summary>
public sealed class AnimationDef
{
    public AnimKind Kind { get; init; }
    public string FirstName { get; init; } = "";
    public string? RangeLast { get; set; }
    public int RangeTics { get; set; }
    public List<AnimFrame> Frames { get; } = new();
    public bool IsRange => RangeLast != null;
}

/// <summary>A wall switch: its default (off) texture and its pressed (on) texture.</summary>
public sealed record SwitchDef(string OffTexture, string OnTexture);

/// <summary>A named camera texture surface declared by ANIMDEFS.</summary>
public sealed record CameraTextureDef(
    string Name,
    int Width,
    int Height,
    float ScaleX = 1.0f,
    float ScaleY = 1.0f,
    bool WorldPanning = false,
    bool FitTexture = false);

public sealed class Animdefs
{
    public List<AnimationDef> Animations { get; } = new();
    public List<SwitchDef> Switches { get; } = new();
    public List<CameraTextureDef> CameraTextures { get; } = new();
}

public static class AnimdefsParser
{
    public static Animdefs Parse(string text)
    {
        var result = new Animdefs();
        var t = Tokenize(text);
        int i = 0;
        while (i < t.Count)
        {
            string kw = t[i].ToLowerInvariant();
            if (kw == "flat" || kw == "texture") ParseAnimation(result, kw == "flat" ? AnimKind.Flat : AnimKind.Texture, t, ref i);
            else if (kw == "switch") ParseSwitch(result, t, ref i);
            else if (kw == "cameratexture") ParseCameraTexture(result, t, ref i);
            else if (t[i] == "{") SkipBlock(t, ref i); // unknown directive's block (warp/cameratexture/...)
            else i++;
        }
        return result;
    }

    private static void ParseAnimation(Animdefs result, AnimKind kind, List<string> t, ref int i)
    {
        i++; // flat/texture
        if (i >= t.Count) return;
        string first = t[i++];
        var def = new AnimationDef { Kind = kind, FirstName = first };

        if (i < t.Count && t[i].Equals("range", StringComparison.OrdinalIgnoreCase))
        {
            i++;
            if (i < t.Count) def.RangeLast = t[i++];
            if (i < t.Count && t[i].Equals("tics", StringComparison.OrdinalIgnoreCase)) { i++; def.RangeTics = ReadInt(t, ref i); }
            result.Animations.Add(def);
        }
        else if (i < t.Count && t[i] == "{")
        {
            i++; // {
            while (i < t.Count && t[i] != "}")
            {
                if (t[i].Equals("pic", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    if (i >= t.Count) break;
                    string name = t[i++];
                    int tics = 1;
                    if (i < t.Count && t[i].Equals("tics", StringComparison.OrdinalIgnoreCase)) { i++; tics = ReadInt(t, ref i); }
                    else if (i < t.Count && t[i].Equals("rand", StringComparison.OrdinalIgnoreCase)) { i++; tics = ReadInt(t, ref i); ReadInt(t, ref i); }
                    def.Frames.Add(new AnimFrame(name, tics));
                }
                else i++; // skip other block tokens (allowfullbright, ...)
            }
            if (i < t.Count) i++; // }
            result.Animations.Add(def);
        }
        // else: a bare "flat NAME" with nothing useful - ignore.
    }

    private static void ParseSwitch(Animdefs result, List<string> t, ref int i)
    {
        i++; // switch
        // An optional game qualifier ("switch doom SW1... on ...") precedes the off texture.
        if (i < t.Count && IsGameQualifier(t[i])) i++;
        if (i >= t.Count) return;
        string off = t[i++];

        string? on = null;
        while (i < t.Count)
        {
            string w = t[i].ToLowerInvariant();
            if (w == "flat" || w == "texture" || w == "switch") break; // next top-level directive
            if (w == "on" && i + 2 < t.Count && t[i + 1].Equals("pic", StringComparison.OrdinalIgnoreCase))
            {
                on = t[i + 2];
                i += 3;
                continue;
            }
            i++;
        }
        if (on != null) result.Switches.Add(new SwitchDef(off, on));
    }

    private static void ParseCameraTexture(Animdefs result, List<string> t, ref int i)
    {
        i++; // cameratexture
        if (i >= t.Count) return;
        string name = t[i++];
        int width = ReadInt(t, ref i);
        int height = ReadInt(t, ref i);
        float scaleX = 1.0f;
        float scaleY = 1.0f;
        bool fitTexture = false;
        bool worldPanning = false;
        if (i < t.Count && t[i].Equals("fit", StringComparison.OrdinalIgnoreCase))
        {
            i++;
            int fitWidth = ReadInt(t, ref i);
            int fitHeight = ReadInt(t, ref i);
            if (width > 0 && height > 0 && fitWidth > 0 && fitHeight > 0)
            {
                fitTexture = true;
                scaleX = (float)fitWidth / width;
                scaleY = (float)fitHeight / height;
            }
            if (i < t.Count && t[i].Equals("worldpanning", StringComparison.OrdinalIgnoreCase)) { worldPanning = true; i++; }
        }
        else if (i < t.Count && t[i].Equals("worldpanning", StringComparison.OrdinalIgnoreCase))
        {
            worldPanning = true;
            i++;
        }
        if (width > 0 && height > 0) result.CameraTextures.Add(new CameraTextureDef(name, width, height, scaleX, scaleY, worldPanning, fitTexture));
        if (i < t.Count && t[i] == "{") SkipBlock(t, ref i);
    }

    private static bool IsGameQualifier(string s) =>
        s.Equals("doom", StringComparison.OrdinalIgnoreCase) || s.Equals("heretic", StringComparison.OrdinalIgnoreCase) ||
        s.Equals("hexen", StringComparison.OrdinalIgnoreCase) || s.Equals("strife", StringComparison.OrdinalIgnoreCase) ||
        s.Equals("any", StringComparison.OrdinalIgnoreCase) || s.Equals("commercial", StringComparison.OrdinalIgnoreCase);

    private static int ReadInt(List<string> t, ref int i)
    {
        if (i < t.Count && int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) { i++; return v; }
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
            if (c == '"')
            {
                var sb = new StringBuilder(); p++;
                while (p < n && s[p] != '"') sb.Append(s[p++]);
                p++;
                toks.Add(sb.ToString());
                continue;
            }
            if (c == '{' || c == '}') { toks.Add(c.ToString()); p++; continue; }
            int b = p;
            while (p < n && !char.IsWhiteSpace(s[p]) && s[p] != '{' && s[p] != '}' && s[p] != '"') p++;
            toks.Add(s.Substring(b, p - b));
        }
        return toks;
    }
}
