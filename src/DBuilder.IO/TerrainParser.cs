// ABOUTME: Parser for ZDoom TERRAIN lump declarations.
// ABOUTME: Captures flat terrain links, splash declarations, and editor-relevant movement properties.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DBuilder.IO;

public sealed class TerrainData
{
    public Dictionary<string, TerrainDefinition> Terrains { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, TerrainSplash> Splashes { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TerrainDefinition
{
    public string FlatName { get; init; } = "";
    public string? Splash { get; set; }
    public int? FootClip { get; set; }
    public float? Friction { get; set; }
    public bool Liquid { get; set; }
    public int? DamageAmount { get; set; }
    public string? DamageType { get; set; }
}

public sealed class TerrainSplash
{
    public string Name { get; init; } = "";
    public string? SmallClass { get; set; }
    public string? BaseClass { get; set; }
    public string? ChunkClass { get; set; }
    public string? Sound { get; set; }
}

public enum TerrainBaseGame
{
    Doom,
    Heretic,
    Hexen,
    Strife
}

public static class TerrainParser
{
    public static TerrainData Parse(string text) => Parse(text, baseGame: null);

    public static TerrainData Parse(string text, TerrainBaseGame? baseGame)
    {
        var data = new TerrainData();
        var t = ZDoomTokenScanner.Tokenize(text);
        bool skipDefinitions = false;
        for (int i = 0; i < t.Count;)
        {
            string keyword = t[i++].ToLowerInvariant();
            if (skipDefinitions)
            {
                if (keyword == "endif") skipDefinitions = false;
                continue;
            }

            if (keyword == "ifdoom") skipDefinitions = ShouldSkip(baseGame, TerrainBaseGame.Doom);
            else if (keyword == "ifheretic") skipDefinitions = ShouldSkip(baseGame, TerrainBaseGame.Heretic);
            else if (keyword == "ifhexen") skipDefinitions = ShouldSkip(baseGame, TerrainBaseGame.Hexen);
            else if (keyword == "ifstrife") skipDefinitions = ShouldSkip(baseGame, TerrainBaseGame.Strife);
            else if (keyword == "terrain" && i < t.Count) ParseTerrain(data, t, ref i);
            else if (keyword == "splash" && i < t.Count) ParseSplash(data, t, ref i);
            else if (i < t.Count && t[i] == "{") SkipBlock(t, ref i);
        }
        return data;
    }

    private static bool ShouldSkip(TerrainBaseGame? baseGame, TerrainBaseGame expected)
        => baseGame.HasValue && baseGame.Value != expected;

    private static void ParseTerrain(TerrainData data, List<string> t, ref int i)
    {
        var terrain = new TerrainDefinition { FlatName = t[i++] };
        if (i < t.Count && t[i] == "{")
        {
            i++;
            while (i < t.Count && t[i] != "}")
            {
                string prop = t[i++].ToLowerInvariant();
                if (prop == "splash" && i < t.Count) terrain.Splash = t[i++];
                else if (prop == "footclip" && ReadInt(t, ref i, out int footClip)) terrain.FootClip = footClip;
                else if (prop == "friction" && ReadFloat(t, ref i, out float friction)) terrain.Friction = friction;
                else if (prop == "liquid") terrain.Liquid = true;
                else if (prop == "damageamount" && ReadInt(t, ref i, out int damage)) terrain.DamageAmount = damage;
                else if (prop == "damagetype" && i < t.Count) terrain.DamageType = t[i++];
                else if (i < t.Count && t[i] == "{") SkipBlock(t, ref i);
            }
            if (i < t.Count) i++;
        }
        if (terrain.FlatName.Length > 0) data.Terrains[terrain.FlatName] = terrain;
    }

    private static void ParseSplash(TerrainData data, List<string> t, ref int i)
    {
        var splash = new TerrainSplash { Name = t[i++] };
        if (i < t.Count && t[i] == "{")
        {
            i++;
            while (i < t.Count && t[i] != "}")
            {
                string prop = t[i++].ToLowerInvariant();
                if (prop == "smallclass" && i < t.Count) splash.SmallClass = t[i++];
                else if (prop == "baseclass" && i < t.Count) splash.BaseClass = t[i++];
                else if (prop == "chunkclass" && i < t.Count) splash.ChunkClass = t[i++];
                else if (prop == "sound" && i < t.Count) splash.Sound = t[i++];
                else if (i < t.Count && t[i] == "{") SkipBlock(t, ref i);
            }
            if (i < t.Count) i++;
        }
        if (splash.Name.Length > 0) data.Splashes[splash.Name] = splash;
    }

    private static bool ReadInt(List<string> t, ref int i, out int value)
    {
        value = 0;
        if (i < t.Count && int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) { i++; return true; }
        return false;
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
