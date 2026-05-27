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
    public (byte R, byte G, byte B)? FadeColor { get; set; }
    public bool HasFadeColor => FogDensity.GetValueOrDefault() > 0 && FadeColor is { } color && (color.R != 0 || color.G != 0 || color.B != 0);
    public string? OutsideFog { get; set; }
    public (byte R, byte G, byte B)? OutsideFogColor { get; set; }
    public bool HasOutsideFogColor => OutsideFogDensity.GetValueOrDefault() > 0 && OutsideFogColor is { } color && (color.R != 0 || color.G != 0 || color.B != 0);
    public int? FogDensity { get; set; } = 255;
    public int? OutsideFogDensity { get; set; } = 255;
    public int? HorizWallShade { get; set; } = -16;
    public int? VertWallShade { get; set; } = 16;
    public string? LightMode { get; set; }
    public string? LightAttenuationMode { get; set; }
    public double? PixelRatio { get; set; } = 1.2;

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

    /// <summary>Sky flat marker name from gameinfo.SkyFlatName, or null when not declared.</summary>
    public string? SkyFlatName { get; private set; }

    /// <summary>Finds a map entry by lump name (case-insensitive), or null.</summary>
    public MapInfoEntry? GetMap(string lump)
    {
        foreach (var m in maps)
            if (string.Equals(m.MapLump, lump, StringComparison.OrdinalIgnoreCase)) return m;
        return null;
    }

    public static MapInfo Parse(string text) => Parse(text, includeResolver: null);

    public static MapInfo Parse(string text, Func<string, string?>? includeResolver)
        => Parse(text, includeResolver, knownColors: null);

    public static MapInfo Parse(string text, Func<string, string?>? includeResolver, IReadOnlyDictionary<string, X11Color>? knownColors)
    {
        var mi = new MapInfo();
        ParseInto(mi, text, includeResolver, knownColors, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return mi;
    }

    private static void ParseInto(MapInfo mi, string text, Func<string, string?>? includeResolver, IReadOnlyDictionary<string, X11Color>? knownColors, HashSet<string> parsedIncludes)
    {
        var toks = Tokenize(text);
        var defaults = new MapInfoEntry();
        int i = 0;
        while (i < toks.Count)
        {
            var t = toks[i];
            if (!t.IsString && t.Text.Equals("$gzdb_skip", StringComparison.OrdinalIgnoreCase)) break;
            if (!t.IsString && t.Text.Equals("map", StringComparison.OrdinalIgnoreCase))
            {
                mi.maps.Add(ParseMap(toks, ref i, defaults, knownColors));
                continue;
            }
            if (!t.IsString && t.Text.Equals("defaultmap", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                defaults = new MapInfoEntry();
                ParseDefaultMap(toks, ref i, defaults, knownColors);
                continue;
            }
            if (!t.IsString && t.Text.Equals("adddefaultmap", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                ParseDefaultMap(toks, ref i, defaults, knownColors);
                continue;
            }
            if (!t.IsString && t.Text.Equals("doomednums", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                ParseNumberedActors(toks, ref i, mi.doomEdNums, skipZero: true);
                continue;
            }
            if (!t.IsString && t.Text.Equals("spawnnums", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                ParseNumberedActors(toks, ref i, mi.spawnNums, skipZero: false);
                continue;
            }
            if (!t.IsString && t.Text.Equals("gameinfo", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                ParseGameInfo(toks, ref i, mi);
                continue;
            }
            if (!t.IsString && t.Text.Equals("include", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                ParseInclude(mi, toks, ref i, includeResolver, knownColors, parsedIncludes);
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
        if (other.SkyFlatName != null) SkyFlatName = other.SkyFlatName;
    }

    private static void ParseInclude(MapInfo mi, List<Tok> toks, ref int i, Func<string, string?>? includeResolver, IReadOnlyDictionary<string, X11Color>? knownColors, HashSet<string> parsedIncludes)
    {
        if (includeResolver == null || i >= toks.Count) return;
        string include = toks[i++].Text;
        if (!parsedIncludes.Add(include)) return;
        string? text = includeResolver(include);
        if (text != null) ParseInto(mi, text, includeResolver, knownColors, parsedIncludes);
    }

    private static void ParseGameInfo(List<Tok> toks, ref int i, MapInfo mi)
    {
        if (i >= toks.Count || toks[i].IsString || toks[i].Text != "{") return;
        i++;
        while (i < toks.Count && !(!toks[i].IsString && toks[i].Text == "}"))
        {
            string key = toks[i++].Text;
            if (i < toks.Count && !toks[i].IsString && toks[i].Text == "=") i++;

            var values = new List<string>();
            while (i < toks.Count && !toks[i].NewLine)
            {
                if (!toks[i].IsString && toks[i].Text == "}") break;
                if (!toks[i].IsString && toks[i].Text == ",") { i++; continue; }
                values.Add(toks[i++].Text);
            }

            if (key.Equals("skyflatname", StringComparison.OrdinalIgnoreCase) && values.Count > 0)
                mi.SkyFlatName = values[0].ToUpperInvariant();
        }
        if (i < toks.Count) i++;
    }

    // Top-level directives. "cluster"/"clusterdef" are intentionally excluded because old-format maps use
    // "cluster N" as a property; they only terminate an old-format body when followed by a brace block.
    private static readonly HashSet<string> TopLevel = new(StringComparer.OrdinalIgnoreCase)
    {
        "map", "defaultmap", "adddefaultmap", "gamedefaults", "episode",
        "clearepisodes", "skill", "clearskills", "gameinfo", "intermission", "automap", "automap_overlay",
        "doomednums", "spawnnums", "conversationids", "damagetype", "include",
    };

    private static readonly HashSet<string> KnownProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "next", "secretnext", "music", "sky1", "sky2", "skybox", "titlepatch", "cluster", "levelnum", "par",
        "doublesky", "evenlighting", "smoothlighting", "forceworldpanning", "fade", "outsidefog",
        "fogdensity", "outsidefogdensity", "horizwallshade", "vertwallshade", "lightmode",
        "lightattenuationmode", "pixelratio",
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

    private static MapInfoEntry ParseMap(List<Tok> toks, ref int i, MapInfoEntry defaults, IReadOnlyDictionary<string, X11Color>? knownColors)
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
                ReadProperty(toks, ref i, entry, stopAtBrace: true, knownColors);
            if (i < toks.Count) i++; // consume '}'
        }
        else
        {
            // Old format: properties run until the next structural directive at a line start.
            while (i < toks.Count && !OldFormatTerminates(toks, i))
                ReadProperty(toks, ref i, entry, stopAtBrace: false, knownColors);
        }

        NormalizeEntry(entry);
        return entry;
    }

    private static void NormalizeEntry(MapInfoEntry entry)
    {
        if (entry.DoubleSky && string.IsNullOrEmpty(entry.Sky2))
            entry.DoubleSky = false;
    }

    private static void ParseDefaultMap(List<Tok> toks, ref int i, MapInfoEntry defaults, IReadOnlyDictionary<string, X11Color>? knownColors)
    {
        if (i < toks.Count && !toks[i].IsString && toks[i].Text == "{")
        {
            i++;
            while (i < toks.Count && !(!toks[i].IsString && toks[i].Text == "}"))
                ReadProperty(toks, ref i, defaults, stopAtBrace: true, knownColors);
            if (i < toks.Count) i++;
        }
        else
        {
            while (i < toks.Count && !OldFormatTerminates(toks, i))
                ReadProperty(toks, ref i, defaults, stopAtBrace: false, knownColors);
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
            FadeColor = defaults.FadeColor,
            OutsideFog = defaults.OutsideFog,
            OutsideFogColor = defaults.OutsideFogColor,
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
    private static void ReadProperty(List<Tok> toks, ref int i, MapInfoEntry e, bool stopAtBrace, IReadOnlyDictionary<string, X11Color>? knownColors)
    {
        string key = toks[i++].Text;
        bool hasEquals = i < toks.Count && !toks[i].IsString && toks[i].Text == "=";
        if (hasEquals) i++;

        var values = new List<string>();
        while (i < toks.Count && !toks[i].NewLine)
        {
            if (stopAtBrace && !toks[i].IsString && toks[i].Text == "}") break;
            if (stopAtBrace && values.Count > 0 && IsInlinePropertyStart(toks, i)) break;
            if (!toks[i].IsString && toks[i].Text == ",") { i++; continue; }
            values.Add(toks[i++].Text);
        }
        Apply(e, key, values, hasEquals, knownColors);
    }

    private static bool IsInlinePropertyStart(List<Tok> toks, int i)
    {
        return i < toks.Count && !toks[i].IsString && KnownProperties.Contains(toks[i].Text);
    }

    private static void Apply(MapInfoEntry e, string key, List<string> values, bool hasEquals, IReadOnlyDictionary<string, X11Color>? knownColors)
    {
        string First() => values.Count > 0 ? values[0] : "";
        int? Int() => values.Count > 0 && int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : null;
        float? FloatAt(int index) => values.Count > index && float.TryParse(values[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : null;
        double? Double() => values.Count > 0 && double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : null;
        bool RequiresEquals() => key.Equals("skybox", StringComparison.OrdinalIgnoreCase)
                              || key.Equals("fogdensity", StringComparison.OrdinalIgnoreCase)
                              || key.Equals("outsidefogdensity", StringComparison.OrdinalIgnoreCase)
                              || key.Equals("lightmode", StringComparison.OrdinalIgnoreCase)
                              || key.Equals("lightattenuationmode", StringComparison.OrdinalIgnoreCase)
                              || key.Equals("pixelratio", StringComparison.OrdinalIgnoreCase);

        if (RequiresEquals() && !hasEquals) return;

        switch (key.ToLowerInvariant())
        {
            case "next": e.Next = First(); break;
            case "secretnext": e.SecretNext = First(); break;
            case "music": e.Music = First(); break;
            case "sky1": e.Sky1 = NormalizeSkyToken(First()); e.Sky1ScrollSpeed = FloatAt(1); break;
            case "sky2": e.Sky2 = NormalizeSkyToken(First()); e.Sky2ScrollSpeed = FloatAt(1); break;
            case "skybox": e.Sky1 = First().ToUpperInvariant(); e.Sky1ScrollSpeed = 0; break;
            case "titlepatch": e.TitlePatch = First(); break;
            case "cluster": e.Cluster = Int(); break;
            case "levelnum": e.LevelNum = Int(); break;
            case "par": e.Par = Int(); break;
            case "doublesky": e.DoubleSky = true; break;
            case "evenlighting": e.EvenLighting = true; break;
            case "smoothlighting": e.SmoothLighting = true; break;
            case "forceworldpanning": e.ForceWorldPanning = true; break;
            case "fade":
                e.Fade = NormalizeColorToken(First());
                if (ZDoomColorParser.TryParse(e.Fade, knownColors, out byte fadeRed, out byte fadeGreen, out byte fadeBlue))
                    e.FadeColor = (fadeRed, fadeGreen, fadeBlue);
                break;
            case "outsidefog":
                e.OutsideFog = NormalizeColorToken(First());
                if (ZDoomColorParser.TryParse(e.OutsideFog, knownColors, out byte fogRed, out byte fogGreen, out byte fogBlue))
                    e.OutsideFogColor = (fogRed, fogGreen, fogBlue);
                break;
            case "fogdensity": e.FogDensity = Int(); break;
            case "outsidefogdensity": e.OutsideFogDensity = Int(); break;
            case "horizwallshade": e.HorizWallShade = ClampWallShade(Int()); break;
            case "vertwallshade": e.VertWallShade = ClampWallShade(Int()); break;
            case "lightmode":
                if (Int().HasValue) e.LightMode = First();
                break;
            case "lightattenuationmode": e.LightAttenuationMode = First(); break;
            case "pixelratio":
                if (Double().HasValue) e.PixelRatio = Double();
                break;
            default: e.Properties[key] = string.Join(" ", values); break;
        }
    }

    private static string NormalizeSkyToken(string value) => value.Replace(",", "").ToUpperInvariant();

    private static string NormalizeColorToken(string value) => value.ToLowerInvariant().Replace(" ", "");

    private static int? ClampWallShade(int? value) => value.HasValue ? Math.Clamp(value.Value, -255, 255) : null;

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
    private static void ParseNumberedActors(List<Tok> toks, ref int i, Dictionary<int, string> map, bool skipZero)
    {
        if (i >= toks.Count || toks[i].IsString || toks[i].Text != "{") return;
        i++; // {
        while (i < toks.Count && !(!toks[i].IsString && toks[i].Text == "}"))
        {
            if (!toks[i].IsString && int.TryParse(toks[i].Text, out int num)
                && i + 2 < toks.Count && !toks[i + 1].IsString && toks[i + 1].Text == "=")
            {
                if (!skipZero || num != 0) map[num] = toks[i + 2].Text.ToLowerInvariant(); // ClassName (extra args after a comma are ignored)
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
