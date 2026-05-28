// ABOUTME: Parser for GZDoom MODELDEF resources used to discover model files, skins, and frame mappings.
// ABOUTME: Captures model blocks without loading model formats or integrating them into rendering.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DBuilder.IO;

public sealed class Modeldef
{
    public string ActorName { get; init; } = "";
    public string Path { get; set; } = "";
    public List<ModeldefModel> Models { get; } = new();
    public List<ModeldefSkin> Skins { get; } = new();
    public List<ModeldefSurfaceSkin> SurfaceSkins { get; } = new();
    public List<ModeldefFrame> Frames { get; } = new();
}

public sealed record ModeldefModel(int Index, string File);
public sealed record ModeldefSkin(int Index, string File);
public sealed record ModeldefSurfaceSkin(int ModelIndex, int SurfaceIndex, string File);
public sealed record ModeldefFrame(string Sprite, string Frame, int ModelIndex, int FrameIndex);

public static class ModeldefParser
{
    public static List<Modeldef> Parse(string text) => Parse(text, includeResolver: null);

    public static List<Modeldef> Parse(string text, Func<string, string?>? includeResolver)
    {
        var result = new List<Modeldef>();
        ParseInto(result, text, includeResolver, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return result;
    }

    private static void ParseInto(List<Modeldef> result, string text, Func<string, string?>? includeResolver, HashSet<string> parsedIncludes)
    {
        var t = Tokenize(text);
        int i = 0;
        while (i < t.Count)
        {
            if (t[i].Equals("#include", StringComparison.OrdinalIgnoreCase))
            {
                ParseInclude(result, t, ref i, includeResolver, parsedIncludes);
                continue;
            }
            if (!t[i].Equals("model", StringComparison.OrdinalIgnoreCase)) { i++; continue; }
            i++;
            if (i >= t.Count) break;
            var def = new Modeldef { ActorName = t[i++] };
            if (i >= t.Count || t[i] != "{") { result.Add(def); continue; }
            i++;
            ParseBlock(def, t, ref i);
            result.Add(def);
        }
    }

    private static void ParseInclude(List<Modeldef> result, List<string> t, ref int i, Func<string, string?>? includeResolver, HashSet<string> parsedIncludes)
    {
        i++; // #include
        if (includeResolver == null || i >= t.Count) return;
        string include = t[i++];
        if (!IsValidIncludePath(include)) return;
        if (!parsedIncludes.Add(include)) return;
        string? text = includeResolver(include);
        if (text != null) ParseInto(result, text, includeResolver, parsedIncludes);
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

    private static void ParseBlock(Modeldef def, List<string> t, ref int i)
    {
        while (i < t.Count && t[i] != "}")
        {
            string kw = t[i++].ToLowerInvariant();
            switch (kw)
            {
                case "path":
                    if (i < t.Count) def.Path = t[i++].TrimEnd('/', '\\');
                    break;
                case "model":
                    if (ReadInt(t, ref i, out int modelIndex) && i < t.Count)
                        def.Models.Add(new ModeldefModel(modelIndex, t[i++]));
                    break;
                case "skin":
                    if (ReadInt(t, ref i, out int skinIndex) && i < t.Count)
                        def.Skins.Add(new ModeldefSkin(skinIndex, t[i++]));
                    break;
                case "surfaceskin":
                    ParseSurfaceSkin(def, t, ref i);
                    break;
                case "frameindex":
                    ParseFrameIndex(def, t, ref i);
                    break;
                default:
                    SkipValue(t, ref i);
                    break;
            }
        }
        if (i < t.Count) i++;
    }

    private static void ParseSurfaceSkin(Modeldef def, List<string> t, ref int i)
    {
        if (!ReadInt(t, ref i, out int modelIndex)) return;
        if (!ReadInt(t, ref i, out int surfaceIndex)) return;
        if (i >= t.Count) return;
        def.SurfaceSkins.Add(new ModeldefSurfaceSkin(modelIndex, surfaceIndex, t[i++]));
    }

    private static void ParseFrameIndex(Modeldef def, List<string> t, ref int i)
    {
        if (i + 1 >= t.Count) return;
        string sprite = t[i++];
        string frame = t[i++];
        if (!ReadInt(t, ref i, out int modelIndex)) return;
        if (!ReadInt(t, ref i, out int frameIndex)) return;
        def.Frames.Add(new ModeldefFrame(sprite, frame, modelIndex, frameIndex));
    }

    private static void SkipValue(List<string> t, ref int i)
    {
        while (i < t.Count && t[i] != "}" && !IsKeyword(t[i])) i++;
    }

    private static bool IsKeyword(string value)
        => value.Equals("path", StringComparison.OrdinalIgnoreCase)
            || value.Equals("model", StringComparison.OrdinalIgnoreCase)
            || value.Equals("skin", StringComparison.OrdinalIgnoreCase)
            || value.Equals("surfaceskin", StringComparison.OrdinalIgnoreCase)
            || value.Equals("frameindex", StringComparison.OrdinalIgnoreCase)
            || value.Equals("scale", StringComparison.OrdinalIgnoreCase)
            || value.Equals("offset", StringComparison.OrdinalIgnoreCase)
            || value.Equals("angleoffset", StringComparison.OrdinalIgnoreCase)
            || value.Equals("pitchfrommomentum", StringComparison.OrdinalIgnoreCase);

    private static bool ReadInt(List<string> t, ref int i, out int value)
    {
        value = 0;
        if (i < t.Count && int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            i++;
            return true;
        }
        return false;
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
                var sb = new StringBuilder();
                p++;
                while (p < n && s[p] != '"')
                {
                    if (s[p] == '\\' && p + 1 < n) { sb.Append(s[p + 1]); p += 2; }
                    else sb.Append(s[p++]);
                }
                if (p < n) p++;
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
