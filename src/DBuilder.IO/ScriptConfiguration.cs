// ABOUTME: Parses UDB script editor configuration files into compiler, lexer and autocomplete metadata.
// ABOUTME: Provides keyword, constant and property lookup behavior without depending on editor UI controls.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DBuilder.IO;

public sealed class ScriptConfigurationCatalog
{
    private readonly Dictionary<string, ScriptConfigurationInfo> configurations = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, ScriptConfigurationInfo> Configurations => configurations;

    public static ScriptConfigurationCatalog FromDirectory(string path)
    {
        var catalog = new ScriptConfigurationCatalog();
        if (!Directory.Exists(path)) return catalog;

        foreach (string file in Directory.EnumerateFiles(path, "*.cfg", SearchOption.TopDirectoryOnly)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            catalog.configurations[Path.GetFileName(file).ToLowerInvariant()] = ScriptConfigurationInfo.FromFile(file);
        }

        return catalog;
    }

    public void Add(string fileName, ScriptConfigurationInfo configuration)
        => configurations[fileName.ToLowerInvariant()] = configuration;

    public ScriptConfigurationInfo? GetScriptConfiguration(
        ScriptType type,
        string mapScriptCompiler = "",
        string defaultScriptCompiler = "")
    {
        if (type == ScriptType.Acs)
        {
            string compiler = !string.IsNullOrEmpty(mapScriptCompiler) ? mapScriptCompiler : defaultScriptCompiler;
            return configurations.TryGetValue(compiler, out var configuration) ? configuration : null;
        }

        foreach (var configuration in configurations.Values)
        {
            if (configuration.ScriptType == type) return configuration;
        }

        return null;
    }
}

public sealed class ScriptConfigurationInfo : IComparable<ScriptConfigurationInfo>
{
    private readonly Dictionary<string, string> keywords = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> lowerKeywords = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> lowerConstants = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> lowerProperties = new(StringComparer.Ordinal);
    private readonly List<string> keywordKeysSorted = new();
    private readonly List<string> constants = new();
    private readonly List<string> properties = new();
    private readonly List<string> snippetKeysSorted = new();
    private readonly Dictionary<string, string[]> snippets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<char> braces = new();

    public string CompilerName { get; private init; } = "";
    public string Parameters { get; private init; } = "";
    public string ResultLump { get; private init; } = "";
    public string Description { get; private init; } = "Plain text";
    public int CodePage { get; private init; } = 65001;
    public string ExtraWordCharacters { get; private init; } = "";
    public IReadOnlyList<string> Extensions { get; private init; } = new[] { "txt" };
    public bool CaseSensitive { get; private init; }
    public int InsertCase { get; private init; }
    public int Lexer { get; private init; }
    public string KeywordHelp { get; private init; } = "";
    public string FunctionOpen { get; private init; } = "";
    public string FunctionClose { get; private init; } = "";
    public string CodeBlockOpen { get; private init; } = "";
    public string CodeBlockClose { get; private init; } = "";
    public string ArrayOpen { get; private init; } = "";
    public string ArrayClose { get; private init; } = "";
    public string ArgumentDelimiter { get; private init; } = "";
    public string Terminator { get; private init; } = "";
    public ScriptType ScriptType { get; private init; } = ScriptType.Unknown;

    public IReadOnlyList<string> Keywords => keywordKeysSorted;
    public IReadOnlyList<string> Constants => constants;
    public IReadOnlyList<string> Properties => properties;
    public IReadOnlyList<string> Snippets => snippetKeysSorted;
    public IReadOnlySet<char> BraceChars => braces;

    public static ScriptConfigurationInfo PlainText { get; } = new();

    public static ScriptConfigurationInfo FromFile(string path, string snippetsPath = "")
    {
        var cfg = new Configuration(path);
        return FromConfiguration(cfg, snippetsPath);
    }

    public static ScriptConfigurationInfo FromText(string text, string snippetsPath = "")
    {
        var cfg = new Configuration();
        cfg.InputConfiguration(text);
        return FromConfiguration(cfg, snippetsPath);
    }

    public static ScriptConfigurationInfo FromConfiguration(Configuration cfg, string snippetsPath = "")
    {
        string extensions = cfg.ReadSetting("extensions", "") ?? "";
        var info = new ScriptConfigurationInfo
        {
            Description = cfg.ReadSetting("description", "Untitled script") ?? "Untitled script",
            CodePage = cfg.ReadSetting("codepage", 0),
            Extensions = SplitExtensions(extensions),
            CompilerName = cfg.ReadSetting("compiler", "") ?? "",
            Parameters = cfg.ReadSetting("parameters", "") ?? "",
            ResultLump = cfg.ReadSetting("resultlump", "") ?? "",
            CaseSensitive = cfg.ReadSetting("casesensitive", true),
            InsertCase = cfg.ReadSetting("insertcase", 0),
            Lexer = cfg.ReadSetting("lexer", 0),
            KeywordHelp = cfg.ReadSetting("keywordhelp", "") ?? "",
            FunctionOpen = cfg.ReadSetting("functionopen", "") ?? "",
            FunctionClose = cfg.ReadSetting("functionclose", "") ?? "",
            CodeBlockOpen = cfg.ReadSetting("codeblockopen", "") ?? "",
            CodeBlockClose = cfg.ReadSetting("codeblockclose", "") ?? "",
            ArrayOpen = cfg.ReadSetting("arrayopen", "") ?? "",
            ArrayClose = cfg.ReadSetting("arrayclose", "") ?? "",
            ArgumentDelimiter = cfg.ReadSetting("argumentdelimiter", "") ?? "",
            Terminator = cfg.ReadSetting("terminator", "") ?? "",
            ExtraWordCharacters = cfg.ReadSetting("extrawordchars", "") ?? "",
            ScriptType = ParseScriptType(cfg.ReadSetting("scripttype", "") ?? ""),
        };

        info.LoadBraces();
        info.LoadKeywords(cfg.ReadSetting("keywords", new Hashtable()) ?? new Hashtable());
        info.LoadProperties(cfg.ReadSetting("properties", new Hashtable()) ?? new Hashtable());
        info.LoadConstants(cfg.ReadSetting("constants", new Hashtable()) ?? new Hashtable());
        info.LoadSnippets(cfg.ReadSetting("snippetsdir", "") ?? "", snippetsPath);
        return info;
    }

    public string GetKeywordCase(string keyword)
        => lowerKeywords.TryGetValue(keyword.ToLowerInvariant(), out string? value) ? value : keyword;

    public string GetConstantCase(string constant)
        => lowerConstants.TryGetValue(constant.ToLowerInvariant(), out string? value) ? value : constant;

    public string GetPropertyCase(string property)
        => lowerProperties.TryGetValue(property.ToLowerInvariant(), out string? value) ? value : property;

    public bool IsKeyword(string keyword) => lowerKeywords.ContainsKey(keyword.ToLowerInvariant());

    public bool IsConstant(string constant) => lowerConstants.ContainsKey(constant.ToLowerInvariant());

    public bool IsProperty(string property) => lowerProperties.ContainsKey(property.ToLowerInvariant());

    public string? GetFunctionDefinition(string keyword)
        => keywords.TryGetValue(keyword, out string? definition) ? definition : null;

    public string[]? GetSnippet(string name)
        => snippets.TryGetValue(name, out string[]? lines) ? lines : null;

    public int CompareTo(ScriptConfigurationInfo? other)
        => string.Compare(Description, other?.Description, ignoreCase: true, CultureInfo.InvariantCulture);

    public override string ToString() => Description;

    private void LoadBraces()
    {
        AddBrace(FunctionOpen);
        AddBrace(FunctionClose);
        AddBrace(CodeBlockOpen);
        AddBrace(CodeBlockClose);
        AddBrace(ArrayOpen);
        AddBrace(ArrayClose);
    }

    private void LoadKeywords(IDictionary dictionary)
    {
        foreach (DictionaryEntry entry in dictionary)
        {
            string keyword = entry.Key.ToString() ?? "";
            if (keyword.Length == 0 || keywords.ContainsKey(keyword)) continue;
            keywords[keyword] = entry.Value?.ToString() ?? "";
            lowerKeywords[keyword.ToLowerInvariant()] = keyword;
            keywordKeysSorted.Add(keyword);
        }
        keywordKeysSorted.Sort(StringComparer.Ordinal);
    }

    private void LoadProperties(IDictionary dictionary)
    {
        foreach (DictionaryEntry entry in dictionary)
        {
            string property = entry.Key.ToString() ?? "";
            if (property.Length == 0 || lowerProperties.ContainsKey(property.ToLowerInvariant())) continue;
            properties.Add(property);
            lowerProperties[property.ToLowerInvariant()] = property;
        }
        properties.Sort(StringComparer.Ordinal);
    }

    private void LoadConstants(IDictionary dictionary)
    {
        foreach (DictionaryEntry entry in dictionary)
        {
            string constant = entry.Key.ToString() ?? "";
            if (constant.Length == 0 || lowerConstants.ContainsKey(constant.ToLowerInvariant())) continue;
            constants.Add(constant);
            lowerConstants[constant.ToLowerInvariant()] = constant;
        }
        constants.Sort(StringComparer.Ordinal);
    }

    private void LoadSnippets(string snippetsDirectory, string snippetsPath)
    {
        if (string.IsNullOrEmpty(snippetsDirectory) || string.IsNullOrEmpty(snippetsPath)) return;

        string path = Path.Combine(snippetsPath, snippetsDirectory);
        if (!Directory.Exists(path)) return;

        foreach (string file in Directory.EnumerateFiles(path, "*.txt", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrEmpty(name)) continue;
            if (name.Contains(' ', StringComparison.Ordinal)) name = name.Replace(' ', '_');

            string[] lines = File.ReadAllLines(file);
            if (lines.Length == 0) continue;

            snippets[name] = lines;
            snippetKeysSorted.Add(name);
        }

        snippetKeysSorted.Sort(StringComparer.Ordinal);
    }

    private void AddBrace(string value)
    {
        if (!string.IsNullOrEmpty(value)) braces.Add(value[0]);
    }

    private static IReadOnlyList<string> SplitExtensions(string extensions)
    {
        if (extensions.Length == 0) return Array.Empty<string>();
        string[] parts = extensions.Split(',');
        for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
        return parts;
    }

    private static ScriptType ParseScriptType(string value)
    {
        string normalized = value.Replace("_", "", StringComparison.Ordinal).ToUpperInvariant();
        return normalized switch
        {
            "ACS" => ScriptType.Acs,
            "MODELDEF" => ScriptType.ModelDef,
            "DECORATE" => ScriptType.Decorate,
            "GLDEFS" => ScriptType.Gldefs,
            "SNDSEQ" => ScriptType.SndSeq,
            "MAPINFO" => ScriptType.MapInfo,
            "VOXELDEF" => ScriptType.VoxelDef,
            "TEXTURES" => ScriptType.Textures,
            "ANIMDEFS" => ScriptType.Animdefs,
            "REVERBS" => ScriptType.Reverbs,
            "TERRAIN" => ScriptType.Terrain,
            "X11R6RGB" => ScriptType.X11R6Rgb,
            "CVARINFO" => ScriptType.CvarInfo,
            "SNDINFO" => ScriptType.SndInfo,
            "LOCKDEFS" => ScriptType.LockDefs,
            "MENUDEF" => ScriptType.MenuDef,
            "SBARINFO" => ScriptType.SbarInfo,
            "USDF" => ScriptType.Usdf,
            "GAMEINFO" => ScriptType.GameInfo,
            "KEYCONF" => ScriptType.KeyConf,
            "FONTDEFS" => ScriptType.FontDefs,
            "ZSCRIPT" => ScriptType.ZScript,
            "DECALDEF" => ScriptType.DecalDef,
            _ => ScriptType.Unknown,
        };
    }
}
