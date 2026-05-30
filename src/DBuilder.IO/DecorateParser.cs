// ABOUTME: Parser for the ZDoom DECORATE lump - extracts actor (thing-type) definitions for the editor.
// ABOUTME: Captures class/parent/replaces/DoomEdNum, //$ editor keys, Radius/Height, spawn sprite, and state metadata.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DBuilder.IO;

internal readonly record struct StateSpriteCandidate(string Name, bool IsEmpty, string? LightName, bool Bright);

internal readonly record struct StateGotoTarget(string? ClassName, string StateName, int SpriteOffset);

public sealed record ActorUserVariable(string Name, UniversalType Type, object? DefaultValue = null);

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
    public string? LightName { get; set; }
    public bool StateBright { get; set; }
    public string? RegionCategory { get; set; }
    public Dictionary<string, List<string>> RegionProperties { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> EditorKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ActorUserVariable> UserVariables { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Mixins { get; } = new();
    internal Dictionary<string, StateSpriteCandidate> StateSprites { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, List<StateSpriteCandidate>> StateFrames { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, StateGotoTarget> StateGotos { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Display title: //$Title if given, else the class name.</summary>
    public string Title => EditorKeys.TryGetValue("$title", out var t) && t.Length > 0 ? t
        : TryFirstProperty("$title", out t) && t.Length > 0 ? t : ClassName;

    /// <summary>Editor category: //$Category if given, else null.</summary>
    public string? Category => EditorKeys.TryGetValue("$category", out var c) ? c
        : TryFirstProperty("$category", out c) ? c : RegionCategory;

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

public sealed class DecorateParseResult
{
    public List<ActorInfo> Actors { get; } = new();
    public HashSet<string> DamageTypes { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class DecorateParser
{
    private enum Kind { Word, Str, Sym, Editor }
    private readonly record struct Tok(Kind Kind, string Text);

    private static readonly HashSet<string> StateFlow = new(StringComparer.OrdinalIgnoreCase)
    { "goto", "loop", "stop", "wait", "fail", "hold" };

    private static readonly HashSet<string> ZScriptFieldModifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "static", "native", "action", "internal", "readonly", "protected", "private", "virtual",
        "override", "meta", "transient", "deprecated", "final", "play", "ui", "clearscope",
        "virtualscope", "version", "const", "abstract", "norollback"
    };

    private static readonly string[] SpriteCheckStates = { "idle", "see", "inactive", "spawn" };

    /// <summary>Parses a DECORATE lump into actor definitions, with parent inheritance applied.</summary>
    public static List<ActorInfo> Parse(string text, Func<string, string?>? includeResolver = null)
        => ParseDocument(text, includeResolver).Actors;

    /// <summary>Parses a DECORATE lump into actors and editor-visible metadata.</summary>
    public static DecorateParseResult ParseDocument(string text, Func<string, string?>? includeResolver = null)
    {
        var result = new DecorateParseResult();
        result.Actors.AddRange(ParseActors(text, "actor", headerNum: true, includeResolver, allowRelativeIncludes: false, result.DamageTypes));
        return result;
    }

    /// <summary>
    /// Shared engine for DECORATE ("actor", editor number in the header) and ZScript ("class", no header number).
    /// </summary>
    internal static List<ActorInfo> ParseActors(
        string text,
        string keyword,
        bool headerNum,
        Func<string, string?>? includeResolver = null,
        bool allowRelativeIncludes = false,
        ISet<string>? damageTypes = null)
    {
        bool deferIncludes = keyword.Equals("class", StringComparison.OrdinalIgnoreCase);
        text = ExpandIncludes(text, includeResolver, new HashSet<string>(StringComparer.OrdinalIgnoreCase), allowRelativeIncludes, deferIncludes);
        var toks = Tokenize(text);
        var actors = new List<ActorInfo>();
        var mixins = new Dictionary<string, ActorInfo>(StringComparer.OrdinalIgnoreCase);
        var extensions = new Dictionary<string, List<ActorInfo>>(StringComparer.OrdinalIgnoreCase);
        var regions = new List<string>();
        var regionPartCounts = new List<int>();
        var regionProperties = new List<Dictionary<string, List<string>>>();
        int i = 0;
        while (i < toks.Count)
        {
            if ((toks[i].Kind == Kind.Word || toks[i].Kind == Kind.Editor)
                && toks[i].Text.Equals("$gzdb_skip", StringComparison.OrdinalIgnoreCase)) break;
            if (toks[i].Kind == Kind.Editor)
            {
                if (regionProperties.Count > 0)
                    ParseRegionEditorKey(toks[i].Text, regionProperties[^1]);
                i++;
                continue;
            }
            if (toks[i].Kind == Kind.Word && toks[i].Text.StartsWith("$", StringComparison.Ordinal))
            {
                if (regionProperties.Count > 0)
                {
                    string editorLine = ReadLineValue(toks, ref i);
                    ParseRegionEditorKey(editorLine, regionProperties[^1]);
                }
                else
                {
                    SkipLine(toks, ref i);
                }
                continue;
            }
            if (toks[i].Kind == Kind.Word && toks[i].Text.Equals("#region", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                string title = ReadLineValue(toks, ref i);
                int count = AddRegionParts(regions, title);
                regionPartCounts.Add(count);
                if (count > 0)
                {
                    var props = regionProperties.Count > 0
                        ? CopyProperties(regionProperties[^1])
                        : new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    regionProperties.Add(props);
                }
                continue;
            }
            else if (toks[i].Kind == Kind.Word && toks[i].Text.Equals("#endregion", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                if (regionPartCounts.Count > 0)
                {
                    int count = regionPartCounts[^1];
                    regionPartCounts.RemoveAt(regionPartCounts.Count - 1);
                    if (count <= regions.Count) regions.RemoveRange(regions.Count - count, count);
                    if (count > 0 && regionProperties.Count > 0)
                        regionProperties.RemoveAt(regionProperties.Count - 1);
                }
                SkipLine(toks, ref i);
                continue;
            }
            if (keyword.Equals("actor", StringComparison.OrdinalIgnoreCase)
                && toks[i].Kind == Kind.Word
                && toks[i].Text.Equals("damagetype", StringComparison.OrdinalIgnoreCase))
            {
                ParseDamageType(toks, ref i, damageTypes);
            }
            else if (keyword.Equals("actor", StringComparison.OrdinalIgnoreCase)
                && toks[i].Kind == Kind.Word
                && IsSkippedDecorateTopLevelDeclaration(toks[i].Text))
            {
                SkipUntilSemicolon(toks, ref i);
            }
            else if (toks[i].Kind == Kind.Word && toks[i].Text.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            {
                if (keyword.Equals("class", StringComparison.OrdinalIgnoreCase))
                {
                    var classKind = GetZScriptClassKind(toks, i);
                    var parsed = ParseActor(toks, ref i, headerNum, CurrentRegionCategory(regions), CurrentRegionProperties(regionProperties));
                    if (parsed == null) continue;
                    if (classKind == ZScriptClassKind.Extension)
                    {
                        if (!extensions.TryGetValue(parsed.ClassName, out var list))
                        {
                            list = new List<ActorInfo>();
                            extensions[parsed.ClassName] = list;
                        }
                        list.Add(parsed);
                    }
                    else if (classKind == ZScriptClassKind.Mixin) mixins[parsed.ClassName] = parsed;
                    else if (!ContainsActorClass(actors, parsed.ClassName)) actors.Add(parsed);
                }
                else
                {
                    var a = ParseActor(toks, ref i, headerNum, CurrentRegionCategory(regions), CurrentRegionProperties(regionProperties));
                    if (a != null && !ContainsActorClass(actors, a.ClassName)) actors.Add(a);
                }
            }
            else if (keyword.Equals("actor", StringComparison.OrdinalIgnoreCase)
                && toks[i].Kind == Kind.Word)
            {
                SkipDeclaration(toks, ref i);
            }
            else if (keyword.Equals("class", StringComparison.OrdinalIgnoreCase)
                && toks[i].Kind == Kind.Word
                && IsSkippedZScriptTopLevelDeclaration(toks[i].Text))
            {
                SkipDeclaration(toks, ref i);
            }
            else i++;
        }
        ApplyMixins(actors, mixins);
        ApplyExtensions(actors, extensions, mixins);
        ResolveInheritance(actors);
        if (keyword.Equals("class", StringComparison.OrdinalIgnoreCase))
            FilterZScriptActorClasses(actors);
        return actors;
    }

    private static bool ContainsActorClass(List<ActorInfo> actors, string className)
    {
        foreach (var actor in actors)
            if (actor.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static string? CurrentRegionCategory(List<string> regions)
        => regions.Count == 0 ? null : string.Join(".", regions);

    private static Dictionary<string, List<string>>? CurrentRegionProperties(List<Dictionary<string, List<string>>> regionProperties)
        => regionProperties.Count == 0 ? null : regionProperties[^1];

    private static Dictionary<string, List<string>> CopyProperties(Dictionary<string, List<string>> source)
    {
        var copy = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in source)
            copy[kvp.Key] = new List<string>(kvp.Value);
        return copy;
    }

    private static void ParseRegionEditorKey(string text, Dictionary<string, List<string>> properties)
    {
        text = text.Trim();
        int sp = 0;
        while (sp < text.Length && !char.IsWhiteSpace(text[sp])) sp++;
        string key = text.Substring(0, sp);
        string value = text.Substring(sp).Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value.Substring(1, value.Length - 2);
        properties[key] = value.Length == 0 ? new List<string>() : new List<string> { value };
    }

    private static int AddRegionParts(List<string> regions, string title)
    {
        int count = 0;
        foreach (string part in title.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                regions.Add(trimmed);
                count++;
            }
        }
        return count;
    }

    private static string ReadLineValue(List<Tok> t, ref int i)
    {
        var parts = new List<string>();
        while (i < t.Count)
        {
            var tk = t[i++];
            if (tk.Kind == Kind.Sym && tk.Text == "\n") break;
            parts.Add(tk.Text);
        }
        return JoinLineValue(parts).Trim();
    }

    private static void SkipLine(List<Tok> t, ref int i)
    {
        while (i < t.Count)
        {
            var tk = t[i++];
            if (tk.Kind == Kind.Sym && tk.Text == "\n") return;
        }
    }

    private enum ZScriptClassKind { Actor, Mixin, Extension }

    private static ZScriptClassKind GetZScriptClassKind(List<Tok> t, int classIndex)
    {
        int i = classIndex - 1;
        while (i >= 0 && t[i].Kind == Kind.Sym && t[i].Text == "\n") i--;
        if (i < 0 || t[i].Kind != Kind.Word) return ZScriptClassKind.Actor;
        if (t[i].Text.Equals("mixin", StringComparison.OrdinalIgnoreCase)) return ZScriptClassKind.Mixin;
        if (t[i].Text.Equals("extend", StringComparison.OrdinalIgnoreCase)) return ZScriptClassKind.Extension;
        return ZScriptClassKind.Actor;
    }

    private static void SkipDeclaration(List<Tok> t, ref int i)
    {
        while (i < t.Count)
        {
            if (t[i].Kind == Kind.Sym && t[i].Text == ";")
            {
                i++;
                return;
            }

            if (t[i].Kind == Kind.Sym && t[i].Text == "{")
            {
                int depth = 0;
                while (i < t.Count)
                {
                    if (t[i].Kind == Kind.Sym && t[i].Text == "{") depth++;
                    else if (t[i].Kind == Kind.Sym && t[i].Text == "}")
                    {
                        depth--;
                        if (depth == 0)
                        {
                            i++;
                            return;
                        }
                    }
                    i++;
                }
                return;
            }

            i++;
        }
    }

    private static bool IsSkippedZScriptTopLevelDeclaration(string word)
        => word.Equals("struct", StringComparison.OrdinalIgnoreCase)
        || word.Equals("enum", StringComparison.OrdinalIgnoreCase)
        || word.Equals("const", StringComparison.OrdinalIgnoreCase);

    private static bool IsSkippedDecorateTopLevelDeclaration(string word)
        => word.Equals("enum", StringComparison.OrdinalIgnoreCase)
        || word.Equals("native", StringComparison.OrdinalIgnoreCase)
        || word.Equals("const", StringComparison.OrdinalIgnoreCase);

    private static void ParseDamageType(List<Tok> t, ref int i, ISet<string>? damageTypes)
    {
        i++;
        if (i >= t.Count || !IsNameToken(t[i])) return;
        string name = t[i++].Text;
        SkipNewlines(t, ref i);
        if (i >= t.Count || t[i].Kind != Kind.Sym || t[i].Text != "{") return;
        SkipBlock(t, ref i);
        if (name.Length > 0) damageTypes?.Add(name);
    }

    private static void SkipNewlines(List<Tok> t, ref int i)
    {
        while (i < t.Count && t[i].Kind == Kind.Sym && t[i].Text == "\n") i++;
    }

    private static void SkipBlock(List<Tok> t, ref int i)
    {
        if (i >= t.Count || t[i].Kind != Kind.Sym || t[i].Text != "{") return;
        int depth = 0;
        while (i < t.Count)
        {
            if (t[i].Kind == Kind.Sym && t[i].Text == "{") depth++;
            else if (t[i].Kind == Kind.Sym && t[i].Text == "}")
            {
                depth--;
                if (depth == 0)
                {
                    i++;
                    return;
                }
            }
            i++;
        }
    }

    private static void ApplyMixins(List<ActorInfo> actors, Dictionary<string, ActorInfo> mixins)
    {
        foreach (var actor in actors)
            ApplyActorMixins(actor, mixins);
    }

    private static void ApplyActorMixins(ActorInfo actor, Dictionary<string, ActorInfo> mixins)
    {
        foreach (string mixinName in actor.Mixins)
        {
            if (!mixins.TryGetValue(mixinName, out var mixin)) continue;
            if (actor.StateSprites.Count == 0 && actor.StateGotos.Count == 0 && HasSpawnState(mixin))
            {
                actor.Sprite ??= mixin.Sprite;
                actor.LightName ??= mixin.LightName;
                actor.StateBright = actor.StateBright || mixin.StateBright;
            }
            if (actor.Radius == 0)
            {
                actor.Radius = mixin.Radius;
                if (mixin.Properties.TryGetValue("radius", out var radius)) actor.Properties["radius"] = new List<string>(radius);
            }
            if (actor.Height == 0)
            {
                actor.Height = mixin.Height;
                if (mixin.Properties.TryGetValue("height", out var height)) actor.Properties["height"] = new List<string>(height);
            }
            CopyUserVariables(actor, mixin);
            CopyMixinFlag(actor, mixin, "spawnceiling");
            CopyMixinFlag(actor, mixin, "solid");
        }
    }

    private static void ApplyExtensions(List<ActorInfo> actors, Dictionary<string, List<ActorInfo>> extensions, Dictionary<string, ActorInfo> mixins)
    {
        foreach (var actor in actors)
        {
            if (!extensions.TryGetValue(actor.ClassName, out var actorExtensions)) continue;
            foreach (var extension in actorExtensions)
            {
                ApplyActorMixins(extension, mixins);
                if (HasSpawnState(extension))
                {
                    actor.Sprite = extension.Sprite ?? actor.Sprite;
                    actor.LightName = extension.LightName ?? actor.LightName;
                    actor.StateBright = extension.StateBright;
                }
                if (extension.Radius > 0) actor.Radius = extension.Radius;
                if (extension.Height > 0) actor.Height = extension.Height;
                CopyUserVariables(actor, extension);
                CopyExtensionFlag(actor, extension, "spawnceiling");
                CopyExtensionFlag(actor, extension, "solid");
            }
        }
    }

    private static void CopyExtensionFlag(ActorInfo actor, ActorInfo extension, string flag)
    {
        if (extension.Flags.ContainsKey(flag)) actor.Flags[flag] = true;
    }

    private static void CopyMixinFlag(ActorInfo actor, ActorInfo mixin, string flag)
    {
        if (!actor.Flags.ContainsKey(flag) && mixin.Flags.TryGetValue(flag, out bool enabled))
            actor.Flags[flag] = enabled;
    }

    private static void CopyUserVariables(ActorInfo actor, ActorInfo source)
    {
        foreach (var variable in source.UserVariables.Values)
            actor.UserVariables[variable.Name] = variable;
    }

    private static bool HasSpawnState(ActorInfo actor)
        => actor.StateSprites.ContainsKey("spawn") || actor.StateGotos.ContainsKey("spawn");

    private static string ExpandIncludes(string text, Func<string, string?>? includeResolver, HashSet<string> seen, bool allowRelativeIncludes, bool deferIncludes)
    {
        if (includeResolver == null) return text;

        using var reader = new StringReader(text);
        var result = new System.Text.StringBuilder();
        var deferred = deferIncludes ? new System.Text.StringBuilder() : null;
        bool emittedDeferred = false;
        bool stopCollectingDeferred = false;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (deferred != null && !emittedDeferred && IsGzdbSkipLine(line))
            {
                result.Append(deferred);
                emittedDeferred = true;
                stopCollectingDeferred = true;
                result.AppendLine(line);
                continue;
            }

            if (!stopCollectingDeferred && TryReadInclude(line, out string includePath))
            {
                if (!IsValidIncludePath(includePath, allowRelativeIncludes))
                {
                    result.AppendLine(line);
                    continue;
                }
                string? included = includeResolver(includePath);
                if (included != null && seen.Add(includePath))
                {
                    string expanded = ExpandIncludes(included, includeResolver, seen, allowRelativeIncludes, deferIncludes);
                    if (deferred != null) deferred.AppendLine(expanded);
                    else result.AppendLine(expanded);
                    continue;
                }
            }
            result.AppendLine(line);
        }
        if (deferred != null && !emittedDeferred) result.Append(deferred);
        return result.ToString();
    }

    private static bool IsGzdbSkipLine(string line)
    {
        string trimmed = line.TrimStart();
        if (trimmed.StartsWith("//", StringComparison.Ordinal))
            trimmed = trimmed.Substring(2).TrimStart();
        return trimmed.Equals("$gzdb_skip", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidIncludePath(string includePath, bool allowRelativeIncludes)
    {
        if (string.IsNullOrWhiteSpace(includePath)) return false;
        if (Path.IsPathRooted(includePath)) return false;
        if (includePath.Contains('\\')) return false;
        if (allowRelativeIncludes) return true;
        return !includePath.StartsWith("../", StringComparison.Ordinal)
            && !includePath.StartsWith("./", StringComparison.Ordinal)
            && !includePath.Equals("..", StringComparison.Ordinal)
            && !includePath.Equals(".", StringComparison.Ordinal);
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

    private static ActorInfo? ParseActor(
        List<Tok> t,
        ref int i,
        bool headerNum,
        string? regionCategory,
        Dictionary<string, List<string>>? regionProperties)
    {
        i++; // keyword
        if (i >= t.Count || !IsNameToken(t[i])) return null;
        string className = t[i++].Text;
        if (className.Length == 0)
        {
            SkipDeclaration(t, ref i);
            return null;
        }
        var actor = new ActorInfo
        {
            ClassName = className,
            ParentName = headerNum && !className.Equals("Actor", StringComparison.OrdinalIgnoreCase) ? "Actor" : null,
            RegionCategory = regionCategory
        };
        if (regionProperties != null)
        {
            foreach (var kvp in regionProperties)
                actor.RegionProperties[kvp.Key] = new List<string>(kvp.Value);
        }

        // Header: [: Parent] [replaces Other] [DoomEdNum], until '{' (body) or ';' (forward declaration).
        bool hasParent = false;
        bool hasReplacement = false;
        bool hasNative = false;
        bool hasFinal = false;
        bool hasScope = false;
        bool hasVersion = false;
        while (i < t.Count && !(t[i].Kind == Kind.Sym && (t[i].Text == "{" || t[i].Text == ";")))
        {
            var tk = t[i];
            if (tk.Kind == Kind.Sym && tk.Text == "\n")
            {
                i++;
            }
            else if (tk.Kind == Kind.Sym && tk.Text == ":")
            {
                if (!headerNum && (hasParent || hasReplacement || hasNative)) return SkipInvalidActorDeclaration(t, ref i);
                i++;
                if (i < t.Count && IsNameToken(t[i]) && t[i].Text.Length > 0) actor.ParentName = t[i++].Text;
                else
                {
                    SkipDeclaration(t, ref i);
                    return null;
                }
                hasParent = true;
            }
            else if (tk.Kind == Kind.Word && tk.Text.Equals("replaces", StringComparison.OrdinalIgnoreCase))
            {
                if (!headerNum && (hasReplacement || hasNative)) return SkipInvalidActorDeclaration(t, ref i);
                i++;
                if (i < t.Count && IsNameToken(t[i]) && t[i].Text.Length > 0) actor.Replaces = t[i++].Text;
                else
                {
                    SkipDeclaration(t, ref i);
                    return null;
                }
                hasReplacement = true;
            }
            else if (tk.Kind == Kind.Word && tk.Text.StartsWith("$", StringComparison.Ordinal))
            {
                i++;
                actor.Properties[tk.Text] = ReadDollarPropertyValues(t, ref i);
            }
            else if (headerNum && tk.Kind == Kind.Word && tk.Text == "-")
            {
                i++;
                if (i < t.Count && t[i].Kind == Kind.Word) i++;
            }
            else if (headerNum && tk.Kind == Kind.Word && int.TryParse(tk.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
            {
                actor.DoomEdNum = n; i++;
            }
            else if (headerNum && tk.Kind == Kind.Word && tk.Text.Equals("native", StringComparison.OrdinalIgnoreCase))
            {
                i++;
            }
            else if (!headerNum && tk.Kind == Kind.Word && tk.Text.Equals("native", StringComparison.OrdinalIgnoreCase))
            {
                if (hasNative) return SkipInvalidActorDeclaration(t, ref i);
                hasNative = true;
                i++;
            }
            else if (!headerNum && tk.Kind == Kind.Word && tk.Text.Equals("abstract", StringComparison.OrdinalIgnoreCase))
            {
                i++;
            }
            else if (!headerNum && tk.Kind == Kind.Word && tk.Text.Equals("final", StringComparison.OrdinalIgnoreCase))
            {
                if (hasFinal) return SkipInvalidActorDeclaration(t, ref i);
                hasFinal = true;
                i++;
            }
            else if (!headerNum && tk.Kind == Kind.Word && IsZScriptHeaderScopeModifier(tk.Text))
            {
                if (hasScope) return SkipInvalidActorDeclaration(t, ref i);
                hasScope = true;
                i++;
            }
            else if (!headerNum && tk.Kind == Kind.Word && IsZScriptHeaderModifier(tk.Text, "version"))
            {
                if (hasVersion) return SkipInvalidActorDeclaration(t, ref i);
                hasVersion = true;
                if (!TryReadZScriptHeaderModifierArguments(t, ref i, out var arguments)
                    || !ValidateZScriptHeaderModifierArguments("version", arguments))
                    return SkipInvalidActorDeclaration(t, ref i);
            }
            else if (!headerNum && tk.Kind == Kind.Word && TryGetZScriptHeaderParameterizedModifier(tk.Text, out string modifier))
            {
                if (!TryReadZScriptHeaderModifierArguments(t, ref i, out var arguments)
                    || !ValidateZScriptHeaderModifierArguments(modifier, arguments))
                    return SkipInvalidActorDeclaration(t, ref i);
            }
            else if (headerNum)
            {
                SkipDeclaration(t, ref i);
                return null;
            }
            else return SkipInvalidActorDeclaration(t, ref i);
        }

        if (i >= t.Count || t[i].Text != "{") return actor; // no body (e.g. forward declaration)
        i++; // '{'
        ParseBody(actor, t, ref i, zscriptBody: !headerNum);
        return actor;
    }

    private static ActorInfo? SkipInvalidActorDeclaration(List<Tok> t, ref int i)
    {
        SkipDeclaration(t, ref i);
        return null;
    }

    private static bool IsZScriptHeaderScopeModifier(string token)
        => token.Equals("clearscope", StringComparison.OrdinalIgnoreCase)
        || token.Equals("ui", StringComparison.OrdinalIgnoreCase)
        || token.Equals("play", StringComparison.OrdinalIgnoreCase);

    private static bool IsZScriptHeaderModifier(string token, string modifier)
        => token.Equals(modifier, StringComparison.OrdinalIgnoreCase)
        || token.StartsWith(modifier + "(", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetZScriptHeaderParameterizedModifier(string token, out string modifier)
    {
        foreach (string candidate in new[] { "deprecated", "unsafe", "sealed" })
        {
            if (!IsZScriptHeaderModifier(token, candidate)) continue;
            modifier = candidate;
            return true;
        }

        modifier = "";
        return false;
    }

    private static bool TryReadZScriptHeaderModifierArguments(List<Tok> t, ref int i, out List<Tok> arguments)
    {
        arguments = new List<Tok>();
        bool hasOpen = false;
        while (i < t.Count)
        {
            var tk = t[i];
            if (tk.Kind == Kind.Sym && tk.Text is "{" or ";") return false;
            string text = tk.Text;
            if (!hasOpen)
            {
                int open = text.IndexOf('(');
                if (open < 0)
                {
                    i++;
                    continue;
                }

                hasOpen = true;
                text = text[(open + 1)..];
            }

            int close = text.IndexOf(')');
            string value = close >= 0 ? text[..close] : text;
            if (value.Length > 0) arguments.Add(new Tok(tk.Kind, value));
            i++;
            if (close >= 0) return true;
        }

        return false;
    }

    private static bool ValidateZScriptHeaderModifierArguments(string modifier, List<Tok> arguments)
    {
        arguments.RemoveAll(t => t.Kind == Kind.Sym && t.Text == "\n");
        if (modifier.Equals("version", StringComparison.OrdinalIgnoreCase))
            return arguments.Count == 1 && arguments[0].Kind == Kind.Str;
        if (modifier.Equals("unsafe", StringComparison.OrdinalIgnoreCase))
            return arguments.Count == 1 && arguments[0].Kind == Kind.Word && arguments[0].Text.Length > 0;
        if (modifier.Equals("deprecated", StringComparison.OrdinalIgnoreCase))
            return arguments.Count == 3
                && arguments[0].Kind == Kind.Str
                && arguments[1].Kind == Kind.Sym
                && arguments[1].Text == ","
                && arguments[2].Kind == Kind.Str;
        if (!modifier.Equals("sealed", StringComparison.OrdinalIgnoreCase)) return false;
        if (arguments.Count == 0 || arguments.Count % 2 == 0) return false;
        for (int j = 0; j < arguments.Count; j++)
        {
            if (j % 2 == 0)
            {
                if (arguments[j].Kind != Kind.Word || arguments[j].Text.Length == 0) return false;
            }
            else if (arguments[j].Kind != Kind.Sym || arguments[j].Text != ",") return false;
        }

        return true;
    }

    private static bool IsNameToken(Tok token) => token.Kind is Kind.Word or Kind.Str;

    private static void ParseBody(ActorInfo actor, List<Tok> t, ref int i, bool zscriptBody)
    {
        int depth = 1;
        bool pendingStates = false, inStates = false;
        int statesDepth = 0;
        string? currentState = null;
        var stateSprites = new Dictionary<string, StateSpriteCandidate>(StringComparer.OrdinalIgnoreCase);
        StateSpriteCandidate? firstSprite = null;
        StateSpriteCandidate? firstNonEmptySprite = null;
        var pendingUserVariableMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
                if (inStates && depth == statesDepth)
                {
                    inStates = false;
                    currentState = null;
                }
                depth--;
                continue;
            }
            if (tk.Kind == Kind.Editor)
            {
                if (zscriptBody && depth == 1 && TryParseUserVariableMetadata(tk.Text, pendingUserVariableMetadata))
                    continue;
                ParseEditorKey(tk.Text, actor.EditorKeys);
                continue;
            }
            if (tk.Kind != Kind.Word)
            {
                if (inStates && actor.Sprite == null && LooksLikeSpriteFrame(tk.Text, t, i))
                {
                    var sprite = BuildSpriteCandidate(tk.Text, t, i);
                    firstSprite ??= sprite;
                    if (!sprite.IsEmpty) firstNonEmptySprite ??= sprite;
                    if (currentState != null && !stateSprites.ContainsKey(currentState))
                        stateSprites[currentState] = sprite;
                    if (currentState != null)
                        AddStateFrame(actor, currentState, sprite);
                }
                continue;
            }

            string lw = tk.Text.ToLowerInvariant();
            // DECORATE puts Radius/Height in the actor body (depth 1); ZScript puts them in Default {} (depth 2).
            if (depth == 1 && (lw == "states" || lw.StartsWith("states(", StringComparison.Ordinal)))
            {
                pendingUserVariableMetadata.Clear();
                pendingStates = true;
            }
            else if (zscriptBody && depth == 1 && lw == "mixin")
            {
                if (i < t.Count && t[i].Kind == Kind.Word) actor.Mixins.Add(t[i++].Text);
                SkipUntilSemicolon(t, ref i);
                pendingUserVariableMetadata.Clear();
            }
            else if (zscriptBody && depth == 1 && lw == "default") pendingUserVariableMetadata.Clear();
            else if (zscriptBody && depth == 1 && lw != "default")
            {
                if (!TryParseZScriptUserVariables(actor, tk.Text, pendingUserVariableMetadata, t, ref i))
                    SkipZScriptMember(t, ref i);
                pendingUserVariableMetadata.Clear();
            }
            else if (!inStates && TryParseFlag(tk.Text, actor)) { }
            else if (!inStates && TryParseSeparatedFlag(tk.Text, t, ref i, actor)) { }
            else if (!inStates && (tk.Text.Equals("$angled", StringComparison.OrdinalIgnoreCase)
                                || tk.Text.Equals("$notangled", StringComparison.OrdinalIgnoreCase))) actor.Properties[tk.Text] = new List<string>();
            else if (!inStates && tk.Text.Equals("$clearargs", StringComparison.OrdinalIgnoreCase)) actor.Properties[tk.Text] = new List<string>();
            else if (!inStates && tk.Text.Equals("skip_super", StringComparison.OrdinalIgnoreCase)) actor.Properties[tk.Text] = new List<string>();
            else if (!inStates && tk.Text.Equals("defaultalpha", StringComparison.OrdinalIgnoreCase)) actor.Properties[tk.Text] = new List<string>();
            else if (!inStates && tk.Text.Equals("var", StringComparison.OrdinalIgnoreCase)) ParseDecorateUserVariable(actor, t, ref i);
            else if (!inStates && (tk.Text.Equals("action", StringComparison.OrdinalIgnoreCase)
                                || tk.Text.Equals("native", StringComparison.OrdinalIgnoreCase))) SkipUntilSemicolon(t, ref i);
            else if (!inStates && tk.Text.Equals("monster", StringComparison.OrdinalIgnoreCase)) ApplyMonsterFlags(actor);
            else if (!inStates && tk.Text.Equals("projectile", StringComparison.OrdinalIgnoreCase)) ApplyProjectileFlags(actor);
            else if (!inStates && tk.Text.Equals("clearflags", StringComparison.OrdinalIgnoreCase)) actor.Flags.Clear();
            else if (!inStates && lw == "radius" && PeekInt(t, ref i, out int r)) { actor.Radius = r; actor.Properties["radius"] = new List<string> { r.ToString(CultureInfo.InvariantCulture) }; }
            else if (!inStates && lw == "height" && PeekInt(t, ref i, out int h)) { actor.Height = h; actor.Properties["height"] = new List<string> { h.ToString(CultureInfo.InvariantCulture) }; }
            else if (!inStates && LooksLikeProperty(tk.Text, t, i))
            {
                var values = ReadPropertyValues(tk.Text, t, ref i);
                if (tk.Text.Equals("scale", StringComparison.OrdinalIgnoreCase))
                {
                    actor.Properties["xscale"] = values;
                    actor.Properties["yscale"] = values;
                }
                else
                {
                    actor.Properties[tk.Text] = values;
                }
            }
            else if (inStates && actor.Sprite == null && LooksLikeSpriteFrame(tk.Text, t, i))
            {
                var sprite = BuildSpriteCandidate(tk.Text, t, i);
                firstSprite ??= sprite;
                if (!sprite.IsEmpty) firstNonEmptySprite ??= sprite;
                if (currentState != null && !stateSprites.ContainsKey(currentState))
                    stateSprites[currentState] = sprite;
                if (currentState != null)
                    AddStateFrame(actor, currentState, sprite);
            }
            else if (inStates && currentState != null && lw == "goto")
            {
                if (TryReadStateGoto(t, ref i, allowSingleColonClassTarget: !zscriptBody, out var target))
                    actor.StateGotos[currentState] = target;
            }
            else if (inStates && tk.Kind == Kind.Word && IsStateLabel(t, i))
            {
                currentState = tk.Text;
                i++;
            }
        }

        TrimStateFrames(actor, stateSprites);

        foreach (var stateSprite in stateSprites)
            actor.StateSprites[stateSprite.Key] = stateSprite.Value;

        if (actor.Sprite == null)
        {
            var sprite = ChooseSprite(stateSprites, firstNonEmptySprite, firstSprite);
            actor.Sprite = sprite?.Name;
            actor.LightName = sprite?.LightName;
            actor.StateBright = sprite?.Bright == true;
        }
    }

    private static bool IsStateLabel(List<Tok> t, int colonIndex)
        => colonIndex < t.Count
        && t[colonIndex].Kind == Kind.Sym
        && t[colonIndex].Text == ":"
        && !(colonIndex + 1 < t.Count && t[colonIndex + 1].Kind == Kind.Sym && t[colonIndex + 1].Text == ":");

    private static StateSpriteCandidate BuildSpriteCandidate(string spriteName, List<Tok> t, int frameIndex)
    {
        string sprite = spriteName.ToUpperInvariant() + char.ToUpperInvariant(t[frameIndex].Text[0]) + "0";
        return new StateSpriteCandidate(
            sprite,
            IsEmptySprite(sprite) || IsZeroDurationFrame(t, frameIndex + 1),
            FindStateFrameLightName(t, frameIndex + 2),
            HasStateFrameBright(t, frameIndex + 2));
    }

    private static StateSpriteCandidate? ChooseSprite(Dictionary<string, StateSpriteCandidate> stateSprites, StateSpriteCandidate? firstNonEmptySprite, StateSpriteCandidate? firstSprite)
    {
        foreach (string state in SpriteCheckStates)
            if (stateSprites.TryGetValue(state, out var sprite) && !sprite.IsEmpty)
                return sprite;

        foreach (string state in SpriteCheckStates)
            if (stateSprites.TryGetValue(state, out var sprite))
                if (!IsInvalidPlaceholderSprite(sprite.Name))
                    return sprite;

        if (firstNonEmptySprite != null) return firstNonEmptySprite;
        return firstSprite is { } fallbackSprite && !IsInvalidPlaceholderSprite(fallbackSprite.Name) ? fallbackSprite : null;
    }

    private static void AddStateFrame(ActorInfo actor, string stateName, StateSpriteCandidate sprite)
    {
        if (!actor.StateFrames.TryGetValue(stateName, out var frames))
        {
            frames = new List<StateSpriteCandidate>();
            actor.StateFrames[stateName] = frames;
        }
        frames.Add(sprite);
    }

    private static void TrimStateFrames(ActorInfo actor, Dictionary<string, StateSpriteCandidate> stateSprites)
    {
        foreach (var state in actor.StateFrames)
        {
            int firstNonEmpty = state.Value.FindIndex(sprite => !sprite.IsEmpty);
            if (firstNonEmpty > 0) state.Value.RemoveRange(0, firstNonEmpty);
            if (state.Value.Count > 0) stateSprites[state.Key] = state.Value[0];
        }
    }

    private static bool TryReadStateGoto(List<Tok> t, ref int i, bool allowSingleColonClassTarget, out StateGotoTarget target)
    {
        target = default;
        if (i >= t.Count || t[i].Kind is not (Kind.Word or Kind.Str)) return false;

        string first = t[i++].Text;
        string? className = null;
        string stateName = first;
        int spriteOffset = 0;
        if (i + 1 < t.Count && t[i].Text == ":" && t[i + 1].Text == ":")
        {
            i += 2;
            if (i >= t.Count || t[i].Kind is not (Kind.Word or Kind.Str)) return false;
            className = first;
            stateName = t[i++].Text;
        }
        else if (allowSingleColonClassTarget && i + 1 < t.Count && t[i].Text == ":" && t[i + 1].Kind is Kind.Word or Kind.Str)
        {
            i++;
            className = first;
            stateName = t[i++].Text;
        }

        stateName = ReadStateNameAndOffset(stateName, ref spriteOffset);
        if (i + 1 < t.Count && t[i].Text == "+" && t[i + 1].Kind == Kind.Word)
        {
            if (int.TryParse(t[i + 1].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedOffset))
                spriteOffset = parsedOffset;
            i += 2;
        }
        if (stateName.Length == 0) return false;
        target = new StateGotoTarget(className, stateName, spriteOffset);
        return true;
    }

    private static string ReadStateNameAndOffset(string stateName, ref int spriteOffset)
    {
        int offsetIndex = stateName.IndexOf('+', StringComparison.Ordinal);
        if (offsetIndex < 0) return stateName;

        string offsetText = stateName[(offsetIndex + 1)..];
        if (int.TryParse(offsetText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedOffset))
            spriteOffset = parsedOffset;
        return stateName[..offsetIndex];
    }

    private static bool IsZeroDurationFrame(List<Tok> t, int durationIndex)
    {
        if (durationIndex >= t.Count || t[durationIndex].Kind != Kind.Word) return false;
        string durationText = t[durationIndex].Text;
        if (durationText == "-" && durationIndex + 1 < t.Count && t[durationIndex + 1].Kind == Kind.Word)
            durationText += t[durationIndex + 1].Text;
        return int.TryParse(durationText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int duration)
            && duration == 0;
    }

    private static string? FindStateFrameLightName(List<Tok> t, int start)
    {
        for (int i = start; i < t.Count; i++)
        {
            if (t[i].Kind == Kind.Sym && t[i].Text is "\n" or "{" or "}" or ";") return null;
            if (t[i].Kind != Kind.Word) continue;

            string token = t[i].Text;
            if (token.StartsWith("Light(", StringComparison.OrdinalIgnoreCase))
            {
                string? inlineLight = ReadInlineLightName(token);
                if (inlineLight != null) return inlineLight;
                if (i + 1 < t.Count && t[i + 1].Kind is Kind.Word or Kind.Str) return t[i + 1].Text;
            }
            if (!token.Equals("Light", StringComparison.OrdinalIgnoreCase)) continue;

            if (i + 1 < t.Count)
            {
                string next = t[i + 1].Text;
                if (next.StartsWith("(", StringComparison.Ordinal)) return ReadInlineLightName(next);
            }

            if (i + 2 < t.Count && t[i + 1].Text == "(" && t[i + 2].Kind is Kind.Word or Kind.Str)
                return t[i + 2].Text;
        }

        return null;
    }

    private static bool HasStateFrameBright(List<Tok> t, int start)
    {
        for (int i = start; i < t.Count; i++)
        {
            if (t[i].Kind == Kind.Sym && t[i].Text is "\n" or "{" or "}" or ";") return false;
            if (t[i].Kind == Kind.Word && t[i].Text.Equals("bright", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static string? ReadInlineLightName(string token)
    {
        int open = token.IndexOf('(');
        if (open < 0) return null;
        string value = token[(open + 1)..].Trim();
        int close = value.IndexOf(')');
        if (close >= 0) value = value[..close].Trim();
        if (value.Length >= 2 && value[0] is '"' or '\'' && value[^1] == value[0])
            value = value[1..^1];
        return value.Length == 0 ? null : value;
    }

    private static bool IsEmptySprite(string sprite)
        => sprite.StartsWith("TNT1", StringComparison.OrdinalIgnoreCase)
        || IsInvalidPlaceholderSprite(sprite);

    private static bool IsInvalidPlaceholderSprite(string sprite)
        => sprite.StartsWith("----", StringComparison.OrdinalIgnoreCase)
        || sprite.Contains('#', StringComparison.Ordinal);

    private static void SkipZScriptMember(List<Tok> t, ref int i)
    {
        int braceDepth = 0;
        while (i < t.Count)
        {
            var tk = t[i++];
            if (tk.Kind == Kind.Sym && tk.Text == "{")
            {
                braceDepth++;
                continue;
            }
            if (tk.Kind == Kind.Sym && tk.Text == "}")
            {
                if (braceDepth == 0) { i--; return; }
                braceDepth--;
                if (braceDepth == 0) return;
                continue;
            }
            if (braceDepth == 0 && tk.Kind == Kind.Sym && tk.Text == ";") return;
        }
    }

    private static void ApplyMonsterFlags(ActorInfo actor)
    {
        actor.Flags["shootable"] = true;
        actor.Flags["countkill"] = true;
        actor.Flags["solid"] = true;
        actor.Flags["canpushwalls"] = true;
        actor.Flags["canusewalls"] = true;
        actor.Flags["activatemcross"] = true;
        actor.Flags["canpass"] = true;
        actor.Flags["ismonster"] = true;
    }

    private static void ApplyProjectileFlags(ActorInfo actor)
    {
        actor.Flags["noblockmap"] = true;
        actor.Flags["nogravity"] = true;
        actor.Flags["dropoff"] = true;
        actor.Flags["missile"] = true;
        actor.Flags["activateimpact"] = true;
        actor.Flags["activatepcross"] = true;
        actor.Flags["noteleport"] = true;
    }

    private static void SkipUntilSemicolon(List<Tok> t, ref int i)
    {
        while (i < t.Count)
        {
            var tk = t[i++];
            if (tk.Kind == Kind.Sym && tk.Text == ";") return;
        }
    }

    private static bool TryParseFlag(string word, ActorInfo actor)
    {
        if (word.Length <= 1 || word[0] is not ('+' or '-')) return false;
        actor.Flags[word.Substring(1)] = word[0] == '+';
        return true;
    }

    private static bool TryParseSeparatedFlag(string word, List<Tok> t, ref int i, ActorInfo actor)
    {
        if (word is not ("+" or "-")) return false;
        if (i >= t.Count || t[i].Kind != Kind.Word) return false;
        actor.Flags[t[i++].Text] = word == "+";
        return true;
    }

    private static bool LooksLikeProperty(string word, List<Tok> t, int next)
    {
        if (word.StartsWith('+') || word.StartsWith('-')) return false;
        if (StateFlow.Contains(word)) return false;
        if (next >= t.Count) return false;
        if (t[next].Kind == Kind.Sym && t[next].Text == "=") return next + 1 < t.Count && t[next + 1].Kind is Kind.Word or Kind.Str;
        if (word.StartsWith("$", StringComparison.Ordinal) && t[next].Kind == Kind.Sym && t[next].Text == "{") return true;
        if (t[next].Kind == Kind.Sym && t[next].Text is "{" or "}" or ":" or ";") return false;
        return t[next].Kind is Kind.Word or Kind.Str;
    }

    private static List<string> ReadPropertyValues(string key, List<Tok> t, ref int i)
    {
        if (key.StartsWith("$", StringComparison.Ordinal))
            return ReadDollarPropertyValues(t, ref i);

        var values = new List<string>();
        bool isGameProperty = key.Equals("game", StringComparison.OrdinalIgnoreCase);
        if (i < t.Count && t[i].Kind == Kind.Sym && t[i].Text == "=") i++;
        if (HasSemicolonTerminator(t, i))
            return ReadSemicolonPropertyValues(key, t, ref i, isGameProperty);

        int maxValues = HasSemicolonTerminator(t, i) ? int.MaxValue
            : HasLineTerminator(t, i) ? int.MaxValue
            : key.Equals("scale", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        while (i < t.Count && values.Count < maxValues)
        {
            var tk = t[i];
            if (tk.Kind == Kind.Sym && tk.Text is "{" or "}" or ";" or "\n") break;
            if (tk.Kind == Kind.Sym && tk.Text == ",") { i++; continue; }
            if (key.Equals("scale", StringComparison.OrdinalIgnoreCase)
                && values.Count > 0
                && !double.TryParse(tk.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                break;
            if (tk.Kind is Kind.Word or Kind.Str)
                values.Add(isGameProperty ? tk.Text.ToLowerInvariant() : tk.Text);
            i++;
        }
        return values;
    }

    private static void ParseDecorateUserVariable(ActorInfo actor, List<Tok> t, ref int i)
    {
        if (i + 1 >= t.Count)
        {
            SkipUntilSemicolon(t, ref i);
            return;
        }

        if (!TryUserVariableType(t[i].Text, out var type))
        {
            SkipUntilSemicolon(t, ref i);
            return;
        }

        i++;
        if (i >= t.Count || t[i].Kind != Kind.Word)
        {
            SkipUntilSemicolon(t, ref i);
            return;
        }

        string name = t[i++].Text;
        bool isArray = i < t.Count && t[i].Kind == Kind.Sym && t[i].Text == "[";
        if (!isArray && IsUserVariableName(name) && !actor.UserVariables.ContainsKey(name))
            actor.UserVariables[name] = new ActorUserVariable(name, type);

        SkipUntilSemicolon(t, ref i);
    }

    private static bool TryParseZScriptUserVariables(
        ActorInfo actor,
        string typeName,
        Dictionary<string, string> metadata,
        List<Tok> t,
        ref int i)
    {
        if (!TryResolveZScriptFieldType(typeName, t, ref i, out var type, out bool typeArray)) return false;
        if (i >= t.Count || t[i].Kind != Kind.Word) return false;

        UniversalType effectiveType = ReinterpretUserVariableType(metadata, type);
        object? defaultValue = TryActorUserVariableDefault(metadata, type, effectiveType, out var parsedDefault) ? parsedDefault : null;
        var variables = new List<(string Name, bool IsArray)>();
        bool expectName = true;
        while (i < t.Count)
        {
            var tk = t[i];
            if (tk.Kind == Kind.Sym && tk.Text == ";")
            {
                i++;
                foreach (var variable in variables)
                    if (!typeArray && !variable.IsArray && IsUserVariableName(variable.Name) && !actor.UserVariables.ContainsKey(variable.Name))
                        actor.UserVariables[variable.Name] = new ActorUserVariable(variable.Name, effectiveType, defaultValue);
                return true;
            }

            if (tk.Kind == Kind.Sym && tk.Text == "(") return false;
            if (tk.Kind == Kind.Sym && tk.Text == ",")
            {
                expectName = true;
                i++;
                continue;
            }

            if (expectName && tk.Kind == Kind.Word)
            {
                string name = tk.Text;
                i++;
                bool isArray = false;
                if (i < t.Count && t[i].Kind == Kind.Sym && t[i].Text == "[")
                {
                    isArray = true;
                    SkipBracketedExpression(t, ref i);
                }
                variables.Add((name, isArray));
                expectName = false;
                continue;
            }

            i++;
        }

        return true;
    }

    private static void SkipBracketedExpression(List<Tok> t, ref int i)
    {
        int depth = 0;
        while (i < t.Count)
        {
            var tk = t[i++];
            if (tk.Kind == Kind.Sym && tk.Text == "[") depth++;
            else if (tk.Kind == Kind.Sym && tk.Text == "]" && --depth <= 0) return;
        }
    }

    private static bool TryUserVariableType(string typeName, out UniversalType type)
    {
        if (typeName.Equals("int", StringComparison.OrdinalIgnoreCase)
            || typeName.Equals("int8", StringComparison.OrdinalIgnoreCase)
            || typeName.Equals("int16", StringComparison.OrdinalIgnoreCase)
            || typeName.Equals("uint", StringComparison.OrdinalIgnoreCase)
            || typeName.Equals("uint8", StringComparison.OrdinalIgnoreCase)
            || typeName.Equals("uint16", StringComparison.OrdinalIgnoreCase))
        {
            type = UniversalType.Integer;
            return true;
        }

        if (typeName.Equals("float", StringComparison.OrdinalIgnoreCase)
            || typeName.Equals("double", StringComparison.OrdinalIgnoreCase))
        {
            type = UniversalType.Float;
            return true;
        }

        if (typeName.Equals("bool", StringComparison.OrdinalIgnoreCase))
        {
            type = UniversalType.Boolean;
            return true;
        }

        if (typeName.Equals("string", StringComparison.OrdinalIgnoreCase))
        {
            type = UniversalType.String;
            return true;
        }

        type = UniversalType.Integer;
        return false;
    }

    private static bool TryResolveZScriptFieldType(
        string firstWord,
        List<Tok> t,
        ref int i,
        out UniversalType type,
        out bool typeArray)
    {
        string typeName = firstWord;
        while (ZScriptFieldModifiers.Contains(typeName))
        {
            if (i >= t.Count || t[i].Kind != Kind.Word)
            {
                type = UniversalType.Integer;
                typeArray = false;
                return false;
            }
            typeName = t[i++].Text;
        }

        if (!TryUserVariableType(typeName, out type))
        {
            typeArray = false;
            return false;
        }

        typeArray = i < t.Count && t[i].Kind == Kind.Sym && t[i].Text == "[";
        if (typeArray) SkipBracketedExpression(t, ref i);
        return true;
    }

    private static bool IsUserVariableName(string name)
        => name.StartsWith("user_", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseUserVariableMetadata(string text, Dictionary<string, string> metadata)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ParseEditorKey(text, parsed);
        bool hasDefault = parsed.TryGetValue("$userdefaultvalue", out string? defaultValue);
        bool hasReinterpret = parsed.TryGetValue("$userreinterpret", out string? reinterpret);
        if (!hasDefault && !hasReinterpret)
            return false;

        if (hasDefault && defaultValue != null) metadata["$userdefaultvalue"] = defaultValue;
        if (hasReinterpret && reinterpret != null) metadata["$userreinterpret"] = reinterpret;
        return true;
    }

    private static bool TryActorUserVariableDefault(
        Dictionary<string, string> metadata,
        UniversalType declaredType,
        UniversalType effectiveType,
        out object? value)
    {
        value = null;
        if (!metadata.TryGetValue("$userdefaultvalue", out string? raw)) return false;

        switch (declaredType)
        {
            case UniversalType.Integer:
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
                {
                    value = integer;
                    return true;
                }
                if (effectiveType == UniversalType.Color
                    && ZDoomColorParser.TryParse(raw, knownColors: null, out byte red, out byte green, out byte blue))
                {
                    value = red << 16 | green << 8 | blue;
                    return true;
                }
                return false;
            case UniversalType.Float:
                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                {
                    value = number;
                    return true;
                }
                return false;
            case UniversalType.Boolean:
                if (bool.TryParse(raw, out bool boolean))
                {
                    value = boolean;
                    return true;
                }
                return false;
            case UniversalType.String:
                value = raw;
                return true;
            default:
                return false;
        }
    }

    private static UniversalType ReinterpretUserVariableType(
        Dictionary<string, string> metadata,
        UniversalType declaredType)
    {
        if (declaredType != UniversalType.Integer) return declaredType;
        return metadata.TryGetValue("$userreinterpret", out string? reinterpret)
            && reinterpret.Trim().Equals("color", StringComparison.OrdinalIgnoreCase)
            ? UniversalType.Color
            : declaredType;
    }

    private static List<string> ReadSemicolonPropertyValues(string key, List<Tok> t, ref int i, bool isGameProperty)
    {
        var values = new List<string>();
        var parts = new List<string>();
        bool scale = key.Equals("scale", StringComparison.OrdinalIgnoreCase);

        while (i < t.Count)
        {
            var tk = t[i];
            if (tk.Kind == Kind.Sym && tk.Text is "{" or "}" or "\n") break;
            if (tk.Kind == Kind.Sym && tk.Text is "," or ";")
            {
                AddExpressionValue(values, parts, isGameProperty);
                parts.Clear();
                i++;
                if (tk.Text == ";") break;
                continue;
            }

            parts.Add(tk.Text);
            i++;
        }

        AddExpressionValue(values, parts, isGameProperty);

        if (scale && values.Count == 1) values.Add(values[0]);
        return values;
    }

    private static void AddExpressionValue(List<string> values, List<string> parts, bool lower)
    {
        if (parts.Count == 0) return;
        string value = JoinLineValue(parts);
        values.Add(lower ? value.ToLowerInvariant() : value);
    }

    private static List<string> ReadDollarPropertyValues(List<Tok> t, ref int i)
    {
        if (i < t.Count && t[i].Kind == Kind.Sym && t[i].Text == "=") i++;
        if (i < t.Count && t[i].Kind == Kind.Sym && t[i].Text == "{")
            return new List<string> { ReadInlineBlockValue(t, ref i) };

        var parts = new List<string>();
        while (i < t.Count)
        {
            var tk = t[i];
            if (tk.Kind == Kind.Sym && tk.Text is "{" or "}" or ";" or "\n") break;
            parts.Add(tk.Text);
            i++;
        }

        return parts.Count == 0 ? parts : new List<string> { JoinLineValue(parts) };
    }

    private static string ReadInlineBlockValue(List<Tok> t, ref int i)
    {
        var parts = new List<string>();
        int depth = 0;
        while (i < t.Count)
        {
            var tk = t[i++];
            parts.Add(ConfigValueToken(tk));
            if (tk.Kind == Kind.Sym && tk.Text == "{") depth++;
            else if (tk.Kind == Kind.Sym && tk.Text == "}" && --depth <= 0) break;
        }

        return JoinLineValue(parts);
    }

    private static string ConfigValueToken(Tok token)
        => token.Kind == Kind.Str ? "\"" + token.Text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"" : token.Text;

    private static string JoinLineValue(List<string> parts)
    {
        var value = string.Join(" ", parts);
        return value.Replace(" ,", ",", StringComparison.Ordinal);
    }

    private static bool HasLineTerminator(List<Tok> t, int i)
    {
        while (i < t.Count)
        {
            var tk = t[i++];
            if (tk.Kind == Kind.Sym && tk.Text == "\n") return true;
            if (tk.Kind == Kind.Sym && tk.Text is "{" or "}" or ";") return false;
        }
        return false;
    }

    private static bool HasSemicolonTerminator(List<Tok> t, int i)
    {
        while (i < t.Count)
        {
            var tk = t[i++];
            if (tk.Kind == Kind.Sym && tk.Text == ";") return true;
            if (tk.Kind == Kind.Sym && tk.Text is "{" or "}" or "\n") return false;
        }
        return false;
    }

    // A spawn-state frame begins with a 4-char sprite name followed by a frame-letters token.
    private static bool LooksLikeSpriteFrame(string word, List<Tok> t, int next)
    {
        if (word.Length != 4 || word.Equals("goto", StringComparison.OrdinalIgnoreCase)) return false;
        if (next >= t.Count || t[next].Kind is not (Kind.Word or Kind.Str)) return false;
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

    // Fills missing Sprite/LightName/Category/Radius/Height from the nearest ancestor that defines them.
    private static void ResolveInheritance(List<ActorInfo> actors)
    {
        var byName = new Dictionary<string, ActorInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in actors) byName[a.ClassName] = a;

        foreach (var a in actors)
        {
            var sprite = ResolveRelevantGotoSprite(a, byName, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            sprite ??= ResolveInheritedRelevantStateSprite(a, byName, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            if (sprite != null)
            {
                a.Sprite = sprite.Value.Name;
                a.LightName = sprite.Value.LightName;
                a.StateBright = sprite.Value.Bright;
            }
        }

        foreach (var a in actors)
        {
            if (a.Sprite != null && a.Radius != 0 && a.Height != 0 && a.Category != null) continue;
            if (SkipsSuper(a)) continue;
            var p = a.ParentName;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { a.ClassName };
            while (p != null && byName.TryGetValue(p, out var parent) && seen.Add(p))
            {
                var parentSprite = RootActorStateSprite(parent)
                    ?? new StateSpriteCandidate(parent.Sprite ?? "", parent.Sprite == null || IsEmptySprite(parent.Sprite), parent.LightName, parent.StateBright);
                if (!parentSprite.IsEmpty)
                {
                    if (a.Sprite == null)
                    {
                        a.Sprite = parentSprite.Name;
                        a.StateBright = parentSprite.Bright;
                    }
                    a.LightName ??= parentSprite.LightName;
                }
                if (a.Radius == 0) a.Radius = parent.Radius;
                if (a.Height == 0) a.Height = parent.Height;
                foreach (var kvp in parent.Flags)
                    if (!a.Flags.ContainsKey(kvp.Key)) a.Flags[kvp.Key] = kvp.Value;
                foreach (var kvp in parent.Properties)
                {
                    if ((a.Properties.ContainsKey("$clearargs") || a.Properties.ContainsKey("skip_super"))
                        && kvp.Key.StartsWith("$arg", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!a.Properties.ContainsKey(kvp.Key)) a.Properties[kvp.Key] = kvp.Value;
                }
                CopyUserVariables(a, parent);
                if (!a.EditorKeys.ContainsKey("$category") && parent.EditorKeys.TryGetValue("$category", out var cat))
                    a.EditorKeys["$category"] = cat;
                p = parent.ParentName;
            }
        }
    }

    private static bool SkipsSuper(ActorInfo actor)
        => actor.Properties.ContainsKey("skip_super");

    private static StateSpriteCandidate? ResolveRelevantGotoSprite(
        ActorInfo actor,
        Dictionary<string, ActorInfo> byName,
        HashSet<string> seen)
    {
        if (HasNonEmptyRelevantStateSprite(actor)) return null;

        foreach (string state in SpriteCheckStates)
        {
            var sprite = ResolveStateGotoSprite(actor, state, byName, new HashSet<string>(seen, StringComparer.OrdinalIgnoreCase));
            if (sprite is { IsEmpty: false }) return sprite;
        }

        return null;
    }

    private static StateSpriteCandidate? RootActorStateSprite(ActorInfo actor)
    {
        if (!actor.ClassName.Equals("Actor", StringComparison.OrdinalIgnoreCase)) return null;
        return actor.StateSprites.TryGetValue("spawn", out var sprite) ? sprite : new StateSpriteCandidate("", true, null, false);
    }

    private static bool HasNonEmptyRelevantStateSprite(ActorInfo actor)
    {
        foreach (string state in SpriteCheckStates)
            if (actor.StateSprites.TryGetValue(state, out var sprite) && !sprite.IsEmpty)
                return true;
        return false;
    }

    private static StateSpriteCandidate? ResolveInheritedRelevantStateSprite(
        ActorInfo actor,
        Dictionary<string, ActorInfo> byName,
        HashSet<string> seen)
    {
        if (HasNonEmptyRelevantStateSprite(actor)) return null;

        foreach (string state in SpriteCheckStates)
        {
            var sprite = ResolveInheritedStateSprite(actor, state, byName, seen);
            if (sprite is { IsEmpty: false }) return sprite;
        }

        return null;
    }

    private static StateSpriteCandidate? ResolveInheritedStateSprite(
        ActorInfo actor,
        string stateName,
        Dictionary<string, ActorInfo> byName,
        HashSet<string> seen)
    {
        string key = actor.ClassName + "::" + stateName;
        if (!seen.Add(key)) return null;

        if (actor.ClassName.Equals("Actor", StringComparison.OrdinalIgnoreCase)
            && !stateName.Equals("spawn", StringComparison.OrdinalIgnoreCase))
            return null;

        if (actor.StateSprites.TryGetValue(stateName, out var sprite)) return sprite;
        if (actor.StateGotos.ContainsKey(stateName))
            return ResolveStateGotoSprite(actor, stateName, byName, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        if (SkipsSuper(actor) || actor.ParentName == null || !byName.TryGetValue(actor.ParentName, out var parent))
            return null;
        return ResolveInheritedStateSprite(parent, stateName, byName, seen);
    }

    private static StateSpriteCandidate? ResolveStateGotoSprite(
        ActorInfo actor,
        string stateName,
        Dictionary<string, ActorInfo> byName,
        HashSet<string> seen)
    {
        string key = actor.ClassName + "::" + stateName;
        if (!seen.Add(key)) return null;

        if (!actor.StateGotos.TryGetValue(stateName, out var target)) return null;

        ActorInfo? targetActor = actor;
        if (target.ClassName != null)
        {
            string? className = target.ClassName.Equals("super", StringComparison.OrdinalIgnoreCase)
                ? actor.ParentName
                : target.ClassName;
            if (className == null || !byName.TryGetValue(className, out targetActor)) return null;
        }

        if (targetActor.ClassName.Equals("Actor", StringComparison.OrdinalIgnoreCase)
            && !target.StateName.Equals("spawn", StringComparison.OrdinalIgnoreCase))
            return null;

        if (target.SpriteOffset >= 0
            && targetActor.StateFrames.TryGetValue(target.StateName, out var frames)
            && target.SpriteOffset < frames.Count)
            return frames[target.SpriteOffset];
        var chained = ResolveStateGotoSprite(targetActor, target.StateName, byName, seen);
        if (chained != null) return chained;
        if (targetActor.StateSprites.TryGetValue(target.StateName, out var sprite)) return sprite;
        return null;
    }

    private static void FilterZScriptActorClasses(List<ActorInfo> actors)
    {
        var byName = new Dictionary<string, ActorInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var actor in actors) byName[actor.ClassName] = actor;

        actors.RemoveAll(actor => !IsZScriptActorClass(actor, byName, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
    }

    private static bool IsZScriptActorClass(ActorInfo actor, Dictionary<string, ActorInfo> byName, HashSet<string> seen)
    {
        if (actor.ClassName.Equals("Actor", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.IsNullOrEmpty(actor.ParentName)) return false;
        if (actor.ParentName.Equals("Actor", StringComparison.OrdinalIgnoreCase)) return true;
        if (!seen.Add(actor.ClassName)) return false;
        return !byName.TryGetValue(actor.ParentName, out var parent)
            || IsZScriptActorClass(parent, byName, seen);
    }

    private static List<Tok> Tokenize(string s)
    {
        var toks = new List<Tok>();
        int n = s.Length;
        for (int p = 0; p < n;)
        {
            char c = s[p];
            if (c is '\r' or '\n')
            {
                if (c == '\r' && p + 1 < n && s[p + 1] == '\n') p++;
                p++;
                toks.Add(new Tok(Kind.Sym, "\n"));
                continue;
            }
            if (char.IsWhiteSpace(c)) { p++; continue; }

            if (c == '/' && p + 1 < n && s[p + 1] == '/')
            {
                // "//$..." carries editor metadata; a plain "//" is a comment.
                int start = p + 2;
                while (start < n && s[start] is ' ' or '\t' or '\u00A0') start++;
                if (start < n && s[start] == '$')
                {
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
            if (c is '"' or '\'')
            {
                char quote = c;
                int b = ++p; var sb = new System.Text.StringBuilder();
                while (p < n && s[p] != quote)
                {
                    if (s[p] == '\\' && p + 1 < n)
                    {
                        if (s[p + 1] == 'n') sb.Append("\\n");
                        else sb.Append(s[p + 1]);
                        p += 2;
                    }
                    else sb.Append(s[p++]);
                }
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
