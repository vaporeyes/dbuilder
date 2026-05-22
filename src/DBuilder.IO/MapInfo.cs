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
    public string? TitlePatch { get; set; }
    public int? Cluster { get; set; }
    public int? LevelNum { get; set; }
    public int? Par { get; set; }

    /// <summary>Any property not surfaced as a strong field, value tokens joined by spaces.</summary>
    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Parsed MAPINFO: the ordered list of map entries plus a by-lump lookup.</summary>
public sealed class MapInfo
{
    private readonly List<MapInfoEntry> maps = new();
    public IReadOnlyList<MapInfoEntry> Maps => maps;

    /// <summary>Finds a map entry by lump name (case-insensitive), or null.</summary>
    public MapInfoEntry? GetMap(string lump)
    {
        foreach (var m in maps)
            if (string.Equals(m.MapLump, lump, StringComparison.OrdinalIgnoreCase)) return m;
        return null;
    }

    public static MapInfo Parse(string text)
    {
        var mi = new MapInfo();
        var toks = Tokenize(text);
        int i = 0;
        while (i < toks.Count)
        {
            var t = toks[i];
            if (!t.IsString && t.Text.Equals("map", StringComparison.OrdinalIgnoreCase))
            {
                mi.maps.Add(ParseMap(toks, ref i));
                continue;
            }
            // Any other directive's brace block (gameinfo, cluster, ...) is skipped wholesale; non-map
            // tokens outside braces are simply ignored.
            if (!t.IsString && t.Text == "{") SkipBlock(toks, ref i);
            else i++;
        }
        return mi;
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

    private static MapInfoEntry ParseMap(List<Tok> toks, ref int i)
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

        var entry = new MapInfoEntry { MapLump = lump, Title = title, TitleIsLookup = lookup };

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

        switch (key.ToLowerInvariant())
        {
            case "next": e.Next = First(); break;
            case "secretnext": e.SecretNext = First(); break;
            case "music": e.Music = First(); break;
            case "sky1": e.Sky1 = First(); break;
            case "sky2": e.Sky2 = First(); break;
            case "titlepatch": e.TitlePatch = First(); break;
            case "cluster": e.Cluster = Int(); break;
            case "levelnum": e.LevelNum = Int(); break;
            case "par": e.Par = Int(); break;
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
