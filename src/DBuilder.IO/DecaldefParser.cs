// ABOUTME: Parser for ZDoom DECALDEF decal and generator declarations.
// ABOUTME: Captures decal image metadata and actor-to-decal generator links for resource discovery.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DBuilder.IO;

public sealed class DecalDefs
{
    public Dictionary<string, DecalDefinition> Decals { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<DecalGenerator> Generators { get; } = new();
}

public sealed class DecalDefinition
{
    public string Name { get; init; } = "";
    public string? Pic { get; set; }
    public float? ScaleX { get; set; }
    public float? ScaleY { get; set; }
    public float? Alpha { get; set; }
    public bool Additive { get; set; }
}

public sealed record DecalGenerator(string ActorClass, string DecalName);

public static class DecaldefParser
{
    public static DecalDefs Parse(string text)
    {
        var defs = new DecalDefs();
        var t = ZDoomTokenScanner.Tokenize(text);
        for (int i = 0; i < t.Count;)
        {
            string keyword = t[i++].ToLowerInvariant();
            if (keyword == "decal" && i < t.Count) ParseDecal(defs, t, ref i);
            else if (keyword == "generator" && i < t.Count) ParseGenerator(defs, t, ref i);
            else if (i < t.Count && t[i] == "{") SkipBlock(t, ref i);
        }
        return defs;
    }

    private static void ParseDecal(DecalDefs defs, List<string> t, ref int i)
    {
        var decal = new DecalDefinition { Name = t[i++] };
        if (i < t.Count && t[i] == "{")
        {
            i++;
            while (i < t.Count && t[i] != "}")
            {
                string prop = t[i++].ToLowerInvariant();
                if (prop == "pic" && i < t.Count) decal.Pic = t[i++];
                else if (prop == "scale" && ReadFloat(t, ref i, out float sx))
                {
                    decal.ScaleX = sx;
                    decal.ScaleY = ReadFloat(t, ref i, out float sy) ? sy : sx;
                }
                else if ((prop == "x-scale" || prop == "xscale") && ReadFloat(t, ref i, out float xscale)) decal.ScaleX = xscale;
                else if ((prop == "y-scale" || prop == "yscale") && ReadFloat(t, ref i, out float yscale)) decal.ScaleY = yscale;
                else if (prop == "translucent" && ReadFloat(t, ref i, out float alpha)) decal.Alpha = alpha;
                else if (prop == "add") decal.Additive = true;
                else if (i < t.Count && t[i] == "{") SkipBlock(t, ref i);
            }
            if (i < t.Count) i++;
        }
        if (decal.Name.Length > 0) defs.Decals[decal.Name] = decal;
    }

    private static void ParseGenerator(DecalDefs defs, List<string> t, ref int i)
    {
        string actorClass = t[i++];
        string? decalName = null;
        if (i < t.Count && t[i] == "{")
        {
            i++;
            while (i < t.Count && t[i] != "}")
            {
                if (t[i++].Equals("decal", StringComparison.OrdinalIgnoreCase) && i < t.Count) decalName = t[i++];
            }
            if (i < t.Count) i++;
        }
        else if (i < t.Count)
        {
            decalName = t[i++];
        }
        if (!string.IsNullOrEmpty(actorClass) && !string.IsNullOrEmpty(decalName)) defs.Generators.Add(new DecalGenerator(actorClass, decalName));
    }

    private static bool ReadFloat(List<string> t, ref int i, out float value)
    {
        value = 0;
        if (i < t.Count && float.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out value)) { i++; return true; }
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
}
