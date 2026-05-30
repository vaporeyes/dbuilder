// ABOUTME: Parser for ZDoom DECALDEF decal and generator declarations.
// ABOUTME: Captures decal image metadata and actor-to-decal generator links for resource discovery.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DBuilder.IO;

public sealed class DecalDefs
{
    private readonly List<DecalIdDefinition> idDefinitions = new();

    public Dictionary<string, DecalDefinition> Decals { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, DecalGroupDefinition> Groups { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<DecalGenerator> Generators { get; } = new();

    public Dictionary<int, DecalIdDefinition> GetDecalDefsById()
    {
        var result = new Dictionary<int, DecalIdDefinition>();
        foreach (var definition in idDefinitions)
            result[definition.Id] = definition;
        return result;
    }

    internal void SetDecal(DecalDefinition decal)
    {
        Decals[decal.Name] = decal;
        Groups.Remove(decal.Name);
        SetIdDefinition(decal.Name, decal.Id, isGroup: false);
    }

    internal void SetGroup(DecalGroupDefinition group)
    {
        Groups[group.Name] = group;
        Decals.Remove(group.Name);
        SetIdDefinition(group.Name, group.Id, isGroup: true);
    }

    private void SetIdDefinition(string name, int? id, bool isGroup)
    {
        idDefinitions.RemoveAll(definition => definition.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (id is int value)
            idDefinitions.Add(new DecalIdDefinition(value, name, isGroup));
    }
}

public sealed record DecalIdDefinition(int Id, string Name, bool IsGroup)
{
    public string Description => Id.ToString(CultureInfo.InvariantCulture) + ": " + Name;
}

public sealed class DecalDefinition
{
    public string Name { get; init; } = "";
    public int? Id { get; set; }
    public string? Pic { get; set; }
    public float? ScaleX { get; set; }
    public float? ScaleY { get; set; }
    public float? Alpha { get; set; }
    public bool Additive { get; set; }
}

public sealed class DecalGroupDefinition
{
    public string Name { get; init; } = "";
    public int? Id { get; set; }
    public List<DecalGroupEntry> Entries { get; } = new();
}

public sealed record DecalGroupEntry(string DecalName, int Weight);

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
            else if (keyword == "decalgroup" && i < t.Count) ParseDecalGroup(defs, t, ref i);
            else if (keyword == "generator" && i < t.Count) ParseGenerator(defs, t, ref i);
            else if (i < t.Count && t[i] == "{") SkipBlock(t, ref i);
        }
        return defs;
    }

    private static void ParseDecal(DecalDefs defs, List<string> t, ref int i)
    {
        var decal = new DecalDefinition { Name = t[i++] };
        if (ReadInt(t, ref i, out int id)) decal.Id = id;
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
        if (decal.Name.Length > 0) defs.SetDecal(decal);
    }

    private static void ParseDecalGroup(DecalDefs defs, List<string> t, ref int i)
    {
        var group = new DecalGroupDefinition { Name = t[i++] };
        if (ReadInt(t, ref i, out int id)) group.Id = id;
        if (i < t.Count && t[i] == "{")
        {
            i++;
            while (i < t.Count && t[i] != "}")
            {
                string decalName = t[i++];
                if (decalName is "{" or "}") continue;
                if (ReadInt(t, ref i, out int weight)) group.Entries.Add(new DecalGroupEntry(decalName, weight));
            }
            if (i < t.Count) i++;
        }
        if (group.Name.Length > 0) defs.SetGroup(group);
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

    private static bool ReadInt(List<string> t, ref int i, out int value)
    {
        value = 0;
        if (i < t.Count && int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) { i++; return true; }
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
