// ABOUTME: Parser for the ZDoom/Hexen MAPINFO lump, extracting per-map entries (lump, title, next, music, sky, ...).
// ABOUTME: Handles both the new brace-block format and the old line-oriented format; non-map directives are skipped.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DBuilder.IO;

/// <summary>A single map definition from MAPINFO. Common fields are surfaced; everything else lands in Properties.</summary>
public sealed class MapInfoEntry
{
    public string MapLump { get; init; } = "";
    public string Title { get; set; } = "";
    /// <summary>True when the title is a language-lookup tag (ZDoom "lookup") rather than literal text.</summary>
    public bool TitleIsLookup { get; set; }
    public string? Next { get; set; }
    public string? SecretNext { get; set; }
    public string? Music { get; set; }
    public string? Sky1 { get; set; }
    public string? Sky2 { get; set; }
    public float? Sky1ScrollSpeed { get; set; }
    public float? Sky2ScrollSpeed { get; set; }
    public string? TitlePatch { get; set; }
    public int? Cluster { get; set; }
    public int? LevelNum { get; set; }
    public int? Par { get; set; }
    public bool DoubleSky { get; set; }
    public bool EvenLighting { get; set; }
    public bool SmoothLighting { get; set; }
    public bool ForceWorldPanning { get; set; }
    public string? Fade { get; set; }
    public string? OutsideFog { get; set; }
    public int? FogDensity { get; set; }
    public int? OutsideFogDensity { get; set; }
    public int? HorizWallShade { get; set; }
    public int? VertWallShade { get; set; }
    public string? LightMode { get; set; }
    public string? LightAttenuationMode { get; set; }
    public double? PixelRatio { get; set; }

    /// <summary>Any property not surfaced as a strong field, value tokens joined by spaces.</summary>
    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Parsed MAPINFO: the ordered list of map entries plus a by-lump lookup.</summary>
public sealed class MapInfo
{
    private readonly List<MapInfoEntry> maps = new();
    public IReadOnlyList<MapInfoEntry> Maps => maps;

    private readonly Dictionary<int, string> doomEdNums = new();
    /// <summary>Editor number -> actor class name, from the MAPINFO DoomEdNums block (ZScript thing placement).</summary>
    public IReadOnlyDictionary<int, string> DoomEdNums => doomEdNums;

    private readonly Dictionary<int, string> spawnNums = new();
    /// <summary>Spawn number -> actor class name, from the MAPINFO SpawnNums block.</summary>
    public IReadOnlyDictionary<int, string> SpawnNums => spawnNums;

    /// <summary>Finds a map entry by lump name (case-insensitive), or null.</summary>
    public MapInfoEntry? GetMap(string lump)
    {
        foreach (var m in maps)
            if (string.Equals(m.MapLump, lump, StringComparison.OrdinalIgnoreCase)) return m;
        return null;
    }

    public static MapInfo Parse(string text) => Parse(text, includeResolver: null);

    public static MapInfo Parse(string text, Func<string, string?>? includeResolver)
    {
        var mi = new MapInfo();
        ParseInto(mi, text, includeResolver, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return mi;
    }

    private static void ParseInto(MapInfo mi, string text, Func<string, string?>? includeResolver, HashSet<string> parsedIncludes)
    {
        var toks = Tokenize(text);
        var defaults = new MapInfoEntry();
        int i = 0;
        while (i < toks.Count)
        {
            var t = toks[i];
            if (!t.IsString && t.Text.Equals("map", StringComparison.OrdinalIgnoreCase))
            {
                mi.maps.Add(ParseMap(toks, ref i, defaults));
                continue;
            }
            if (!t.IsString && t.Text.Equals("defaultmap", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                defaults = new MapInfoEntry();
                ParseDefaultMap(toks, ref i, defaults);
                continue;
            }
            if (!t.IsString && t.Text.Equals("adddefaultmap", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                ParseDefaultMap(toks, ref i, defaults);
                continue;
            }
            if (!t.IsString && t.Text.Equals("doomednums", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                ParseNumberedActors(toks, ref i, mi.doomEdNums);
                continue;
            }
            if (!t.IsString && t.Text.Equals("spawnnums", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                ParseNumberedActors(toks, ref i, mi.spawnNums);
                continue;
            }
            if (!t.IsString && t.Text.Equals("include", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                ParseInclude(mi, toks, ref i, includeResolver, parsedIncludes);
                continue;
            }
            // Any other directive's brace block (gameinfo, cluster, ...) is skipped wholesale; non-map
            // tokens outside braces are simply ignored.
            if (!t.IsString && t.Text == "{") SkipBlock(toks, ref i);
            else i++;
        }
    }

    public void MergeFrom(MapInfo other)
    {
        maps.AddRange(other.maps);
        foreach (var entry in other.doomEdNums) doomEdNums[entry.Key] = entry.Value;
        foreach (var entry in other.spawnNums) spawnNums[entry.Key] = entry.Value;
    }

    private static void ParseInclude(MapInfo mi, List<Tok> toks, ref int i, Func<string, string?>? includeResolver, HashSet<string> parsedIncludes)
    {
        if (includeResolver == null || i >= toks.Count) return;
        string include = toks[i++].Text;
        if (!parsedIncludes.Add(include)) return;
        string? text = includeResolver(include);
        if (text != null) ParseInto(mi, text, includeResolver, parsedIncludes);
    }

    // Top-level directives. "cluster"/"clusterdef" are intentionally excluded because old-format maps use
    // "cluster N" as a property; they only terminate an old-format body when followed by a brace block.
    private static readonly HashSet<string> TopLevel = new(StringComparer.OrdinalIgnoreCase)
    {
        "map", "defaultmap", "adddefaultmap", "gamedefaults", "episode",
        "clearepisodes", "skill", "clearskills", "gameinfo", "intermission", "automap", "automap_overlay",
        "doomednums", "spawnnums", "conversationids", "damagetype", "include",
    };

    // Whether the token at i ends an old-format map body (a structural directive on its own line).
    private static bool OldFormatTerminates(List<Tok> toks, int i)
    {
        var t = toks[i];
        if (!t.NewLine || t.IsString) return false;
        if (TopLevel.Contains(t.Text)) return true;
        bool clusterDef = t.Text.Equals("cluster", StringComparison.OrdinalIgnoreCase)
                       || t.Text.Equals("clusterdef", StringComparison.OrdinalIgnoreCase);
        return clusterDef && i + 1 < toks.Count && !toks[i + 1].IsString && toks[i + 1].Text == "{";
    }

    private static MapInfoEntry ParseMap(List<Tok> toks, ref int i, MapInfoEntry defaults)
    {
        i++; // consume 'map'
        string lump = i < toks.Count ? toks[i++].Text : "";

        string title = "";
        bool lookup = false;
        if (i < toks.Count && !toks[i].NewLine)
        {
            if (!toks[i].IsString && toks[i].Text.Equals("lookup", StringComparison.OrdinalIgnoreCase))
            {
                lookup = true;
                i++;
                if (i < toks.Count && !toks[i].NewLine) title = toks[i++].Text;
            }
            else title = toks[i++].Text;
        }

        var entry = CloneDefaults(defaults, lump, title, lookup);

        if (i < toks.Count && !toks[i].IsString && toks[i].Text == "{")
        {
            i++; // consume '{'
            while (i < toks.Count && !(!toks[i].IsString && toks[i].Text == "}"))
                ReadProperty(toks, ref i, entry, stopAtBrace: true);
            if (i < toks.Count) i++; // consume '}'
        }
        else
        {
            // Old format: properties run until the next structural directive at a line start.
            while (i < toks.Count && !OldFormatTerminates(toks, i))
                ReadProperty(toks, ref i, entry, stopAtBrace: false);
        }

        return entry;
    }

    private static void ParseDefaultMap(List<Tok> toks, ref int i, MapInfoEntry defaults)
    {
        if (i < toks.Count && !toks[i].IsString && toks[i].Text == "{")
        {
            i++;
            while (i < toks.Count && !(!toks[i].IsString && toks[i].Text == "}"))
                ReadProperty(toks, ref i, defaults, stopAtBrace: true);
            if (i < toks.Count) i++;
        }
        else
        {
            while (i < toks.Count && !OldFormatTerminates(toks, i))
                ReadProperty(toks, ref i, defaults, stopAtBrace: false);
        }
    }

    private static MapInfoEntry CloneDefaults(MapInfoEntry defaults, string lump, string title, bool lookup)
    {
        var entry = new MapInfoEntry
        {
            MapLump = lump,
            Title = title,
            TitleIsLookup = lookup,
            Next = defaults.Next,
            SecretNext = defaults.SecretNext,
            Music = defaults.Music,
            Sky1 = defaults.Sky1,
            Sky2 = defaults.Sky2,
            Sky1ScrollSpeed = defaults.Sky1ScrollSpeed,
            Sky2ScrollSpeed = defaults.Sky2ScrollSpeed,
            TitlePatch = defaults.TitlePatch,
            Cluster = defaults.Cluster,
            LevelNum = defaults.LevelNum,
            Par = defaults.Par,
            DoubleSky = defaults.DoubleSky,
            EvenLighting = defaults.EvenLighting,
            SmoothLighting = defaults.SmoothLighting,
            ForceWorldPanning = defaults.ForceWorldPanning,
            Fade = defaults.Fade,
            OutsideFog = defaults.OutsideFog,
            FogDensity = defaults.FogDensity,
            OutsideFogDensity = defaults.OutsideFogDensity,
            HorizWallShade = defaults.HorizWallShade,
            VertWallShade = defaults.VertWallShade,
            LightMode = defaults.LightMode,
            LightAttenuationMode = defaults.LightAttenuationMode,
            PixelRatio = defaults.PixelRatio,
        };
        foreach (var property in defaults.Properties) entry.Properties[property.Key] = property.Value;
        return entry;
    }

    // Reads one "key [=] value..." property (values run to end of line / closing brace) into the entry.
    private static void ReadProperty(List<Tok> toks, ref int i, MapInfoEntry e, bool stopAtBrace)
    {
        string key = toks[i++].Text;
        if (i < toks.Count && !toks[i].IsString && toks[i].Text == "=") i++;

        var values = new List<string>();
        while (i < toks.Count && !toks[i].NewLine)
        {
            if (stopAtBrace && !toks[i].IsString && toks[i].Text == "}") break;
            if (!toks[i].IsString && toks[i].Text == ",") { i++; continue; }
            values.Add(toks[i++].Text);
        }
        Apply(e, key, values);
    }

    private static void Apply(MapInfoEntry e, string key, List<string> values)
    {
        string First() => values.Count > 0 ? values[0] : "";
        int? Int() => values.Count > 0 && int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : null;
        float? FloatAt(int index) => values.Count > index && float.TryParse(values[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : null;
        double? Double() => values.Count > 0 && double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : null;

        switch (key.ToLowerInvariant())
        {
            case "next": e.Next = First(); break;
            case "secretnext": e.SecretNext = First(); break;
            case "music": e.Music = First(); break;
            case "sky1": e.Sky1 = First(); e.Sky1ScrollSpeed = FloatAt(1); break;
            case "sky2": e.Sky2 = First(); e.Sky2ScrollSpeed = FloatAt(1); break;
            case "skybox": e.Sky1 = First(); e.Sky1ScrollSpeed = 0; break;
            case "titlepatch": e.TitlePatch = First(); break;
            case "cluster": e.Cluster = Int(); break;
            case "levelnum": e.LevelNum = Int(); break;
            case "par": e.Par = Int(); break;
            case "doublesky": e.DoubleSky = true; break;
            case "evenlighting": e.EvenLighting = true; break;
            case "smoothlighting": e.SmoothLighting = true; break;
            case "forceworldpanning": e.ForceWorldPanning = true; break;
            case "fade": e.Fade = First(); break;
            case "outsidefog": e.OutsideFog = First(); break;
            case "fogdensity": e.FogDensity = Int(); break;
            case "outsidefogdensity": e.OutsideFogDensity = Int(); break;
            case "horizwallshade": e.HorizWallShade = Int(); break;
            case "vertwallshade": e.VertWallShade = Int(); break;
            case "lightmode": e.LightMode = First(); break;
            case "lightattenuationmode": e.LightAttenuationMode = First(); break;
            case "pixelratio": e.PixelRatio = Double(); break;
            default: e.Properties[key] = string.Join(" ", values); break;
        }
    }

    private static void SkipBlock(List<Tok> toks, ref int i)
    {
        int depth = 0;
        for (; i < toks.Count; i++)
        {
            if (toks[i].IsString) continue;
            if (toks[i].Text == "{") depth++;
            else if (toks[i].Text == "}") { depth--; if (depth == 0) { i++; return; } }
        }
    }

    // Parses a "DoomEdNums" or "SpawnNums" block: <num> = <ClassName> [, args...].
    private static void ParseNumberedActors(List<Tok> toks, ref int i, Dictionary<int, string> map)
    {
        if (i >= toks.Count || toks[i].IsString || toks[i].Text != "{") return;
        i++; // {
        while (i < toks.Count && !(!toks[i].IsString && toks[i].Text == "}"))
        {
            if (!toks[i].IsString && int.TryParse(toks[i].Text, out int num)
                && i + 2 < toks.Count && !toks[i + 1].IsString && toks[i + 1].Text == "=")
            {
                map[num] = toks[i + 2].Text; // ClassName (extra args after a comma are ignored)
                i += 3;
            }
            else i++;
        }
        if (i < toks.Count) i++; // }
    }

    // ---- tokenizer ----

    private readonly struct Tok
    {
        public Tok(string text, bool isString, bool newLine) { Text = text; IsString = isString; NewLine = newLine; }
        public string Text { get; }
        public bool IsString { get; }
        public bool NewLine { get; } // a line break preceded this token (delimits old-format properties)
    }

    private static List<Tok> Tokenize(string s)
    {
        var toks = new List<Tok>();
        bool pendingNewline = false;
        int n = s.Length;
        for (int p = 0; p < n;)
        {
            char c = s[p];
            if (c == '\n') { pendingNewline = true; p++; continue; }
            if (char.IsWhiteSpace(c)) { p++; continue; }

            // Comments: // to end of line, /* ... */ block.
            if (c == '/' && p + 1 < n && s[p + 1] == '/')
            {
                p += 2;
                while (p < n && s[p] != '\n') p++;
                continue;
            }
            if (c == '/' && p + 1 < n && s[p + 1] == '*')
            {
                p += 2;
                while (p + 1 < n && !(s[p] == '*' && s[p + 1] == '/')) { if (s[p] == '\n') pendingNewline = true; p++; }
                p += 2;
                continue;
            }

            if (c == '"')
            {
                int start = ++p;
                var sb = new StringBuilder();
                while (p < n && s[p] != '"')
                {
                    if (s[p] == '\\' && p + 1 < n) { sb.Append(s[p + 1]); p += 2; continue; }
                    sb.Append(s[p++]);
                }
                p++; // closing quote
                toks.Add(new Tok(sb.ToString(), true, pendingNewline));
                pendingNewline = false;
                continue;
            }

            if (c == '{' || c == '}' || c == '=' || c == ',')
            {
                toks.Add(new Tok(c.ToString(), false, pendingNewline));
                pendingNewline = false;
                p++;
                continue;
            }

            // Bare identifier / number: runs until whitespace or a delimiter.
            int b = p;
            while (p < n && !char.IsWhiteSpace(s[p]) && s[p] != '{' && s[p] != '}' && s[p] != '=' && s[p] != ',' && s[p] != '"')
                p++;
            toks.Add(new Tok(s.Substring(b, p - b), false, pendingNewline));
            pendingNewline = false;
        }
        return toks;
    }
}
