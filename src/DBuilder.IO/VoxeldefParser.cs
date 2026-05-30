// ABOUTME: Parser for ZDoom VOXELDEF sprite-to-voxel declarations.
// ABOUTME: Captures voxel model names and preview settings without loading voxel image data.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DBuilder.IO;

public sealed class VoxelDef
{
    public Dictionary<string, VoxelDefinition> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class VoxelDefinition
{
    public string ModelName { get; init; } = "";
    public float Scale { get; set; } = 1.0f;
    public float AngleOffset { get; set; }
    public bool OverridePalette { get; set; }
    public string? Spin { get; set; }
}

public static class VoxeldefParser
{
    public static VoxelDef Parse(string text)
    {
        var result = new VoxelDef();
        var t = ZDoomTokenScanner.Tokenize(text);
        var sprites = new List<string>();
        string previous = "";
        string modelName = "";
        for (int i = 0; i < t.Count;)
        {
            string token = t[i++];
            if (token == ",")
            {
                AddSprite(sprites, previous);
                previous = "";
            }
            else if (token == "=")
            {
                AddSprite(sprites, previous);
                previous = "";
                if (i < t.Count) modelName = t[i++].ToUpperInvariant();
            }
            else if (token == "{")
            {
                var definition = new VoxelDefinition { ModelName = modelName };
                bool valid = ParseSettings(definition, t, ref i);
                if (valid && definition.ModelName.Length > 0)
                {
                    foreach (string sprite in sprites) result.Entries[sprite] = definition;
                }
                sprites.Clear();
                previous = "";
                modelName = "";
            }
            else
            {
                previous = token.ToUpperInvariant();
            }
        }
        return result;
    }

    private static bool ParseSettings(VoxelDefinition definition, List<string> t, ref int i)
    {
        bool valid = true;
        while (i < t.Count && t[i] != "}")
        {
            string prop = t[i++].ToLowerInvariant();
            if (prop == "overridepalette") definition.OverridePalette = true;
            else if (prop == "angleoffset")
            {
                if (ReadEquals(t, ref i) && ReadFloat(t, ref i, out float angle)) definition.AngleOffset = angle;
                else valid = false;
            }
            else if (prop == "scale")
            {
                if (ReadEquals(t, ref i) && ReadFloat(t, ref i, out float scale)) definition.Scale = scale;
                else valid = false;
            }
            else if (prop == "spin" || prop == "droppedspin" || prop == "placedspin")
            {
                definition.Spin = prop;
                if (i < t.Count && t[i] != "}") i++;
            }
            else if (i < t.Count && t[i] == "{") SkipBlock(t, ref i);
        }
        if (i < t.Count) i++;
        return valid;
    }

    private static void AddSprite(List<string> sprites, string value)
    {
        if (value.Length > 0 && !sprites.Contains(value)) sprites.Add(value);
    }

    private static bool ReadFloat(List<string> t, ref int i, out float value)
    {
        value = 0;
        if (i < t.Count && float.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out value)) { i++; return true; }
        return false;
    }

    private static bool ReadEquals(List<string> t, ref int i)
    {
        if (i >= t.Count || t[i] != "=") return false;
        i++;
        return true;
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
}
