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
    public ModeldefVector Scale { get; set; } = new(1.0f, 1.0f, 1.0f);
    public ModeldefVector Offset { get; set; } = new(0.0f, 0.0f, 0.0f);
    public ModeldefVector RotationCenter { get; set; } = new(0.0f, 0.0f, 0.0f);
    public float AngleOffset { get; set; }
    public float PitchOffset { get; set; }
    public float RollOffset { get; set; }
    public bool InheritActorPitch { get; set; }
    public bool UseActorPitch { get; set; }
    public bool UseActorRoll { get; set; }
    public bool UseRotationCenter { get; set; }
    public List<ModeldefModel> Models { get; } = new();
    public List<ModeldefSkin> Skins { get; } = new();
    public List<ModeldefSurfaceSkin> SurfaceSkins { get; } = new();
    public List<ModeldefFrame> Frames { get; } = new();
}

public sealed record ModeldefVector(float X, float Y, float Z);
public sealed record ModeldefModel(int Index, string File);
public sealed record ModeldefSkin(int Index, string File);
public sealed record ModeldefSurfaceSkin(int ModelIndex, int SurfaceIndex, string File);
public sealed record ModeldefFrame(string Sprite, string Frame, int ModelIndex, int FrameIndex, string? ModelFrame = null);

public static class ModeldefParser
{
    private static readonly HashSet<string> SupportedModelExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".3d",
        ".iqm",
        ".md2",
        ".md3",
        ".obj",
    };

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
            if (i >= t.Count || t[i] != "{") continue;
            i++;
            bool valid = ParseBlock(def, t, ref i);
            if (valid && def.Models.Count > 0) result.Add(def);
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

    private static bool ParseBlock(Modeldef def, List<string> t, ref int i)
    {
        bool valid = true;
        while (i < t.Count && t[i] != "}")
        {
            string kw = t[i++].ToLowerInvariant();
            switch (kw)
            {
                case "path":
                    if (i < t.Count) def.Path = t[i++].TrimEnd('/', '\\');
                    break;
                case "model":
                    if (!ParseModel(def, t, ref i)) valid = false;
                    break;
                case "skin":
                    if (!ParseSkin(def, t, ref i)) valid = false;
                    break;
                case "surfaceskin":
                    if (!ParseSurfaceSkin(def, t, ref i)) valid = false;
                    break;
                case "scale":
                    if (!ParseVector(t, ref i, out ModeldefVector scale)) valid = false;
                    else def.Scale = new ModeldefVector(scale.Y, scale.X, scale.Z);
                    break;
                case "offset":
                    if (!ParseVector(t, ref i, out ModeldefVector offset)) valid = false;
                    else def.Offset = offset;
                    break;
                case "zoffset":
                    if (!ReadFloat(t, ref i, out float zOffset)) valid = false;
                    else def.Offset = def.Offset with { Z = zOffset };
                    break;
                case "angleoffset":
                    if (!ReadFloat(t, ref i, out float angleOffset)) valid = false;
                    else def.AngleOffset = angleOffset;
                    break;
                case "pitchoffset":
                    if (!ReadFloat(t, ref i, out float pitchOffset)) valid = false;
                    else def.PitchOffset = pitchOffset;
                    break;
                case "rolloffset":
                    if (!ReadFloat(t, ref i, out float rollOffset)) valid = false;
                    else def.RollOffset = rollOffset;
                    break;
                case "rotation-center":
                    if (!ParseVector(t, ref i, out ModeldefVector rotationCenter)) valid = false;
                    else def.RotationCenter = rotationCenter;
                    break;
                case "useactorpitch":
                    def.InheritActorPitch = false;
                    def.UseActorPitch = true;
                    break;
                case "useactorroll":
                case "inheritactorroll":
                    def.UseActorRoll = true;
                    break;
                case "rotating":
                case "userotationcenter":
                    def.UseRotationCenter = true;
                    break;
                case "inheritactorpitch":
                    def.InheritActorPitch = true;
                    def.UseActorPitch = false;
                    break;
                case "frameindex":
                    if (!ParseFrameIndex(def, t, ref i)) valid = false;
                    break;
                case "frame":
                    if (!ParseFrame(def, t, ref i)) valid = false;
                    break;
                default:
                    SkipValue(t, ref i);
                    break;
            }
        }
        if (i < t.Count) i++;
        return valid;
    }

    private static bool ParseModel(Modeldef def, List<string> t, ref int i)
    {
        if (!ReadInt(t, ref i, out int modelIndex) || modelIndex < 0) return false;
        if (i >= t.Count) return false;
        string file = t[i++];
        if (!IsValidModelResourcePath(file)) return false;
        if (string.IsNullOrWhiteSpace(Path.GetExtension(file))) return false;
        if (!SupportedModelExtensions.Contains(Path.GetExtension(file))) return false;
        SetModel(def.Models, new ModeldefModel(modelIndex, file));
        return true;
    }

    private static bool ParseSkin(Modeldef def, List<string> t, ref int i)
    {
        if (!ReadInt(t, ref i, out int skinIndex) || skinIndex < 0) return false;
        if (i >= t.Count) return false;
        string file = t[i++];
        if (!IsValidModelResourcePath(file)) return false;
        SetSkin(def.Skins, new ModeldefSkin(skinIndex, file));
        return true;
    }

    private static bool ParseSurfaceSkin(Modeldef def, List<string> t, ref int i)
    {
        if (!ReadInt(t, ref i, out int modelIndex) || modelIndex < 0) return false;
        if (!ReadInt(t, ref i, out int surfaceIndex) || surfaceIndex < 0) return false;
        if (i >= t.Count) return false;
        string file = t[i++];
        if (!IsValidModelResourcePath(file)) return false;
        SetSurfaceSkin(def.SurfaceSkins, new ModeldefSurfaceSkin(modelIndex, surfaceIndex, file));
        return true;
    }

    private static bool IsValidModelResourcePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        foreach (char c in path)
            if (c < 32 || c == '"' || c == '<' || c == '>' || c == '|')
                return false;
        return true;
    }

    private static void SetModel(List<ModeldefModel> models, ModeldefModel model)
    {
        for (int i = 0; i < models.Count; i++)
        {
            if (models[i].Index == model.Index)
            {
                models[i] = model;
                return;
            }
        }
        models.Add(model);
    }

    private static void SetSkin(List<ModeldefSkin> skins, ModeldefSkin skin)
    {
        for (int i = 0; i < skins.Count; i++)
        {
            if (skins[i].Index == skin.Index)
            {
                skins[i] = skin;
                return;
            }
        }
        skins.Add(skin);
    }

    private static void SetSurfaceSkin(List<ModeldefSurfaceSkin> skins, ModeldefSurfaceSkin skin)
    {
        for (int i = 0; i < skins.Count; i++)
        {
            if (skins[i].ModelIndex == skin.ModelIndex && skins[i].SurfaceIndex == skin.SurfaceIndex)
            {
                skins[i] = skin;
                return;
            }
        }
        skins.Add(skin);
    }

    private static bool ParseFrameIndex(Modeldef def, List<string> t, ref int i)
    {
        if (i + 1 >= t.Count) return false;
        string sprite = t[i++];
        string frame = t[i++];
        if (sprite.Length != 4 || frame.Length != 1) return false;
        if (!ReadInt(t, ref i, out int modelIndex) || modelIndex < 0) return false;
        if (!ReadInt(t, ref i, out int frameIndex)) return false;
        AddFrame(def.Frames, new ModeldefFrame(sprite, frame, modelIndex, frameIndex));
        return true;
    }

    private static bool ParseFrame(Modeldef def, List<string> t, ref int i)
    {
        if (i + 1 >= t.Count) return false;
        string sprite = t[i++];
        string frame = t[i++];
        if (sprite.Length != 4 || frame.Length != 1) return false;
        if (!ReadInt(t, ref i, out int modelIndex) || modelIndex < 0) return false;
        if (i >= t.Count) return false;
        string modelFrame = t[i++];
        if (string.IsNullOrWhiteSpace(modelFrame)) return false;
        AddFrame(def.Frames, new ModeldefFrame(sprite, frame, modelIndex, 0, modelFrame));
        return true;
    }

    private static void AddFrame(List<ModeldefFrame> frames, ModeldefFrame frame)
    {
        foreach (ModeldefFrame existing in frames)
            if (existing == frame)
                return;

        frames.Add(frame);
    }

    private static bool ParseVector(List<string> t, ref int i, out ModeldefVector vector)
    {
        vector = new ModeldefVector(0.0f, 0.0f, 0.0f);
        if (!ReadFloat(t, ref i, out float x)) return false;
        if (!ReadFloat(t, ref i, out float y)) return false;
        if (!ReadFloat(t, ref i, out float z)) return false;
        vector = new ModeldefVector(x, y, z);
        return true;
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
            || value.Equals("frame", StringComparison.OrdinalIgnoreCase)
            || value.Equals("scale", StringComparison.OrdinalIgnoreCase)
            || value.Equals("offset", StringComparison.OrdinalIgnoreCase)
            || value.Equals("zoffset", StringComparison.OrdinalIgnoreCase)
            || value.Equals("angleoffset", StringComparison.OrdinalIgnoreCase)
            || value.Equals("pitchoffset", StringComparison.OrdinalIgnoreCase)
            || value.Equals("rolloffset", StringComparison.OrdinalIgnoreCase)
            || value.Equals("rotation-center", StringComparison.OrdinalIgnoreCase)
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

    private static bool ReadFloat(List<string> t, ref int i, out float value)
    {
        value = 0.0f;
        if (i < t.Count && float.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
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
