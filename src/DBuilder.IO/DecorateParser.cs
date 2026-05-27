// ABOUTME: Parser for the ZDoom DECORATE lump - extracts actor (thing-type) definitions for the editor.
// ABOUTME: Captures class/parent/replaces/DoomEdNum, //$ editor keys, Radius/Height, and the spawn-state sprite.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DBuilder.IO;

/// <summary>An actor definition parsed from DECORATE. DoomEdNum &lt; 0 means it has no editor (placeable) number.</summary>
public sealed class ActorInfo
{
    public string ClassName { get; init; } = "";
    public string? ParentName { get; set; }
    public string? Replaces { get; set; }
    public int DoomEdNum { get; set; } = -1;
    public int Radius { get; set; }   // 0 = unset (inherit)
    public int Height { get; set; }   // 0 = unset (inherit)
    public string? Sprite { get; set; }
    public Dictionary<string, string> EditorKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Display title: //$Title if given, else the class name.</summary>
    public string Title => EditorKeys.TryGetValue("$title", out var t) && t.Length > 0 ? t
        : TryFirstProperty("$title", out t) && t.Length > 0 ? t : ClassName;

    /// <summary>Editor category: //$Category if given, else null.</summary>
    public string? Category => EditorKeys.TryGetValue("$category", out var c) ? c
        : TryFirstProperty("$category", out c) ? c : null;

    /// <summary>Sprite name: //$Sprite if given, else the spawn-state derived sprite (may be null).</summary>
    public string? EditorSprite
    {
        get
        {
            if (EditorKeys.TryGetValue("$sprite", out var s) && s.Length > 0) return s;
            if (TryFirstProperty("$sprite", out s) && s.Length > 0) return s;
            return Sprite;
        }
    }

    private bool TryFirstProperty(string key, out string value)
    {
        value = "";
        if (!Properties.TryGetValue(key, out var values) || values.Count == 0) return false;
        value = values[0];
        return true;
    }
}

public static class DecorateParser
{
    private enum Kind { Word, Str, Sym, Editor }
    private readonly record struct Tok(Kind Kind, string Text);

    private static readonly HashSet<string> StateFlow = new(StringComparer.OrdinalIgnoreCase)
    { "goto", "loop", "stop", "wait", "fail", "hold" };

    /// <summary>Parses a DECORATE lump into actor definitions, with parent inheritance applied.</summary>
    public static List<ActorInfo> Parse(string text, Func<string, string?>? includeResolver = null)
        => ParseActors(text, "actor", headerNum: true, includeResolver);

    /// <summary>
    /// Shared engine for DECORATE ("actor", editor number in the header) and ZScript ("class", no header number).
    /// </summary>
    internal static List<ActorInfo> ParseActors(string text, string keyword, bool headerNum, Func<string, string?>? includeResolver = null)
    {
        text = ExpandIncludes(text, includeResolver, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var toks = Tokenize(text);
        var actors = new List<ActorInfo>();
        int i = 0;
        while (i < toks.Count)
        {
            if (toks[i].Kind == Kind.Word && toks[i].Text.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            {
                var a = ParseActor(toks, ref i, headerNum);
                if (a != null) actors.Add(a);
            }
            else i++;
        }
        ResolveInheritance(actors);
        return actors;
    }

    private static string ExpandIncludes(string text, Func<string, string?>? includeResolver, HashSet<string> seen)
    {
        if (includeResolver == null) return text;

        using var reader = new StringReader(text);
        var result = new System.Text.StringBuilder();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (TryReadInclude(line, out string includePath))
            {
                string? included = includeResolver(includePath);
                if (included != null && seen.Add(includePath))
                {
                    result.AppendLine(ExpandIncludes(included, includeResolver, seen));
                    continue;
                }
            }
            result.AppendLine(line);
        }
        return result.ToString();
    }

    private static bool TryReadInclude(string line, out string includePath)
    {
        includePath = "";
        string trimmed = line.TrimStart();
        int offset;
        if (trimmed.StartsWith("#include", StringComparison.OrdinalIgnoreCase))
            offset = "#include".Length;
        else if (trimmed.StartsWith("include", StringComparison.OrdinalIgnoreCase))
            offset = "include".Length;
        else
            return false;

        if (trimmed.Length > offset && !char.IsWhiteSpace(trimmed[offset])) return false;
        string rest = trimmed.Substring(offset).TrimStart();
        if (rest.Length == 0) return false;

        if (rest[0] == '"')
        {
            int end = rest.IndexOf('"', 1);
            if (end <= 1) return false;
            includePath = rest.Substring(1, end - 1);
            return true;
        }

        int length = 0;
        while (length < rest.Length && !char.IsWhiteSpace(rest[length])) length++;
        includePath = rest.Substring(0, length);
        return includePath.Length > 0;
    }

    private static ActorInfo? ParseActor(List<Tok> t, ref int i, bool headerNum)
    {
        i++; // keyword
        if (i >= t.Count || t[i].Kind != Kind.Word) return null;
        var actor = new ActorInfo { ClassName = t[i++].Text };

        // Header: [: Parent] [replaces Other] [DoomEdNum], until '{' (body) or ';' (forward declaration).
        while (i < t.Count && !(t[i].Kind == Kind.Sym && (t[i].Text == "{" || t[i].Text == ";")))
        {
            var tk = t[i];
            if (tk.Kind == Kind.Sym && tk.Text == ":")
            {
                i++;
                if (i < t.Count && t[i].Kind == Kind.Word) actor.ParentName = t[i++].Text;
            }
            else if (tk.Kind == Kind.Word && tk.Text.Equals("replaces", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                if (i < t.Count && t[i].Kind == Kind.Word) actor.Replaces = t[i++].Text;
            }
            else if (headerNum && tk.Kind == Kind.Word && int.TryParse(tk.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
            {
                actor.DoomEdNum = n; i++;
            }
            else i++; // 'native'/'abstract'/version "x"/stray tokens
        }

        if (i >= t.Count || t[i].Text != "{") return actor; // no body (e.g. forward declaration)
        i++; // '{'
        ParseBody(actor, t, ref i);
        return actor;
    }

    private static void ParseBody(ActorInfo actor, List<Tok> t, ref int i)
    {
        int depth = 1;
        bool pendingStates = false, inStates = false;
        int statesDepth = 0;

        while (i < t.Count && depth > 0)
        {
            var tk = t[i++];
            if (tk.Kind == Kind.Sym && tk.Text == "{")
            {
                depth++;
                if (pendingStates) { inStates = true; statesDepth = depth; pendingStates = false; }
                continue;
            }
            if (tk.Kind == Kind.Sym && tk.Text == "}")
            {
                if (inStates && depth == statesDepth) inStates = false;
                depth--;
                continue;
            }
            if (tk.Kind == Kind.Editor) { ParseEditorKey(tk.Text, actor.EditorKeys); continue; }
            if (tk.Kind != Kind.Word) continue;

            string lw = tk.Text.ToLowerInvariant();
            // DECORATE puts Radius/Height in the actor body (depth 1); ZScript puts them in Default {} (depth 2).
            if (depth == 1 && lw == "states") { pendingStates = true; }
            else if (!inStates && TryParseFlag(tk.Text, actor)) { }
            else if (!inStates && (tk.Text.Equals("$angled", StringComparison.OrdinalIgnoreCase)
                                || tk.Text.Equals("$notangled", StringComparison.OrdinalIgnoreCase))) actor.Properties[tk.Text] = new List<string>();
            else if (!inStates && lw == "radius" && PeekInt(t, ref i, out int r)) { actor.Radius = r; actor.Properties["radius"] = new List<string> { r.ToString(CultureInfo.InvariantCulture) }; }
            else if (!inStates && lw == "height" && PeekInt(t, ref i, out int h)) { actor.Height = h; actor.Properties["height"] = new List<string> { h.ToString(CultureInfo.InvariantCulture) }; }
            else if (!inStates && LooksLikeProperty(tk.Text, t, i))
            {
                actor.Properties[tk.Text] = ReadPropertyValues(tk.Text, t, ref i);
            }
            else if (inStates && actor.Sprite == null && LooksLikeSpriteFrame(tk.Text, t, i))
                actor.Sprite = tk.Text.ToUpperInvariant() + char.ToUpperInvariant(t[i].Text[0]) + "0";
        }
    }

    private static bool TryParseFlag(string word, ActorInfo actor)
    {
        if (word.Length <= 1 || word[0] is not ('+' or '-')) return false;
        actor.Flags[word.Substring(1)] = word[0] == '+';
        return true;
    }

    private static bool LooksLikeProperty(string word, List<Tok> t, int next)
    {
        if (word.StartsWith('+') || word.StartsWith('-')) return false;
        if (StateFlow.Contains(word)) return false;
        if (next >= t.Count) return false;
        if (t[next].Kind == Kind.Sym && t[next].Text == "=") return next + 1 < t.Count && t[next + 1].Kind is Kind.Word or Kind.Str;
        if (t[next].Kind == Kind.Sym && t[next].Text is "{" or "}" or ":" or ";") return false;
        return t[next].Kind is Kind.Word or Kind.Str;
    }

    private static List<string> ReadPropertyValues(string key, List<Tok> t, ref int i)
    {
        var values = new List<string>();
        int maxValues = key.Equals("scale", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        if (i < t.Count && t[i].Kind == Kind.Sym && t[i].Text == "=") i++;
        while (i < t.Count && values.Count < maxValues)
        {
            var tk = t[i];
            if (tk.Kind == Kind.Sym && tk.Text is "{" or "}" or ";") break;
            if (tk.Kind == Kind.Sym && tk.Text == ",") { i++; continue; }
            if (key.Equals("scale", StringComparison.OrdinalIgnoreCase)
                && values.Count > 0
                && !double.TryParse(tk.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                break;
            if (tk.Kind is Kind.Word or Kind.Str) values.Add(tk.Text);
            i++;
        }
        return values;
    }

    // A spawn-state frame begins with a 4-char sprite name followed by a frame-letters token.
    private static bool LooksLikeSpriteFrame(string word, List<Tok> t, int next)
    {
        if (word.Length != 4 || StateFlow.Contains(word)) return false;
        if (next >= t.Count || t[next].Kind != Kind.Word) return false;
        string frames = t[next].Text;
        foreach (char c in frames)
            if (!(char.IsLetter(c) || c is '#' or '-' or '_' or '[' or ']' or '\\' or '^')) return false;
        return frames.Length > 0;
    }

    private static bool PeekInt(List<Tok> t, ref int i, out int value)
    {
        value = 0;
        if (i < t.Count && t[i].Kind == Kind.Sym && t[i].Text == "=") i++;
        if (i < t.Count && t[i].Kind == Kind.Word &&
            int.TryParse(t[i].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) { i++; return true; }
        // Some properties use floats; accept and truncate.
        if (i < t.Count && t[i].Kind == Kind.Word &&
            double.TryParse(t[i].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) { value = (int)d; i++; return true; }
        return false;
    }

    // "$Title \"Imp\"" -> key "$title", value "Imp" (quotes stripped).
    private static void ParseEditorKey(string text, Dictionary<string, string> keys)
    {
        text = text.Trim();
        int sp = 0;
        while (sp < text.Length && !char.IsWhiteSpace(text[sp])) sp++;
        string key = text.Substring(0, sp);
        string value = text.Substring(sp).Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value.Substring(1, value.Length - 2);
        keys[key] = value;
    }

    // Fills missing Sprite/Category/Radius/Height from the nearest ancestor that defines them.
    private static void ResolveInheritance(List<ActorInfo> actors)
    {
        var byName = new Dictionary<string, ActorInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in actors) byName[a.ClassName] = a;

        foreach (var a in actors)
        {
            if (a.Sprite != null && a.Radius != 0 && a.Height != 0 && a.Category != null) continue;
            var p = a.ParentName;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { a.ClassName };
            while (p != null && byName.TryGetValue(p, out var parent) && seen.Add(p))
            {
                a.Sprite ??= parent.Sprite;
                if (a.Radius == 0) a.Radius = parent.Radius;
                if (a.Height == 0) a.Height = parent.Height;
                foreach (var kvp in parent.Flags)
                    if (!a.Flags.ContainsKey(kvp.Key)) a.Flags[kvp.Key] = kvp.Value;
                foreach (var kvp in parent.Properties)
                    if (!a.Properties.ContainsKey(kvp.Key)) a.Properties[kvp.Key] = kvp.Value;
                if (!a.EditorKeys.ContainsKey("$category") && parent.EditorKeys.TryGetValue("$category", out var cat))
                    a.EditorKeys["$category"] = cat;
                p = parent.ParentName;
            }
        }
    }

    private static List<Tok> Tokenize(string s)
    {
        var toks = new List<Tok>();
        int n = s.Length;
        for (int p = 0; p < n;)
        {
            char c = s[p];
            if (char.IsWhiteSpace(c)) { p++; continue; }

            if (c == '/' && p + 1 < n && s[p + 1] == '/')
            {
                // "//$..." carries editor metadata; a plain "//" is a comment.
                if (p + 2 < n && s[p + 2] == '$')
                {
                    int start = p + 2;
                    int e = start;
                    while (e < n && s[e] != '\n' && s[e] != '\r') e++;
                    toks.Add(new Tok(Kind.Editor, s.Substring(start, e - start)));
                    p = e;
                }
                else { p += 2; while (p < n && s[p] != '\n') p++; }
                continue;
            }
            if (c == '/' && p + 1 < n && s[p + 1] == '*')
            {
                p += 2; while (p + 1 < n && !(s[p] == '*' && s[p + 1] == '/')) p++; p += 2; continue;
            }
            if (c == '"')
            {
                int b = ++p; var sb = new System.Text.StringBuilder();
                while (p < n && s[p] != '"') { if (s[p] == '\\' && p + 1 < n) { sb.Append(s[p + 1]); p += 2; } else sb.Append(s[p++]); }
                p++;
                toks.Add(new Tok(Kind.Str, sb.ToString()));
                continue;
            }
            if (c is '{' or '}' or ':' or ',' or ';' or '=')
            {
                toks.Add(new Tok(Kind.Sym, c.ToString())); p++; continue;
            }
            int w = p;
            while (p < n && !char.IsWhiteSpace(s[p]) && s[p] is not ('{' or '}' or ':' or ',' or ';' or '=' or '"') &&
                   !(s[p] == '/' && p + 1 < n && (s[p + 1] == '/' || s[p + 1] == '*'))) p++;
            toks.Add(new Tok(Kind.Word, s.Substring(w, p - w)));
        }
        return toks;
    }
}
