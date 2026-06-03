// ABOUTME: Discovers UDBScript script files and leading metadata in UDB-compatible directory trees.
// ABOUTME: Provides script and folder records for future docker UI, slot assignment, and script execution.

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;

namespace DBuilder.IO;

public sealed record UdbScriptInfo(
    string Name,
    string Description,
    uint Version,
    string ScriptFile,
    string PathHash,
    string? RawOptions,
    IReadOnlyList<UdbScriptOption> Options);

public sealed record UdbScriptOption(
    string Name,
    string Description,
    int Type,
    object DefaultValue,
    object Value,
    IReadOnlyList<UdbScriptEnumValue> EnumValues,
    string SettingKey);

public sealed record UdbScriptEnumValue(string Key, string? Label);

public enum UdbScriptSettingOperationKind
{
    Write,
    Delete,
}

public sealed record UdbScriptSettingOperation(
    UdbScriptSettingOperationKind Kind,
    string Key,
    object? Value = null);

public sealed record UdbScriptDirectory(
    string Path,
    string Name,
    string Hash,
    IReadOnlyList<UdbScriptDirectory> Directories,
    IReadOnlyList<UdbScriptInfo> Scripts);

public sealed record UdbScriptLoadRetryPolicy(
    int MaxAttempts,
    int DelayMilliseconds);

public sealed record UdbScriptLoadResult(
    UdbScriptInfo? Script,
    int Attempts,
    bool Succeeded,
    string ErrorMessage);

public static class UdbScriptDiscovery
{
    public const string ScriptFolder = "UDBScript";
    public const string ScriptsSubfolder = "Scripts";
    public const uint DefaultVersion = 1;
    public const string DefaultDescription = "No description.";
    public const string DefaultOptionDescription = "no description";
    public const int ScriptLoadRetryAttempts = 5;
    public const int ScriptLoadRetryDelayMilliseconds = 100;

    public static IReadOnlyList<UniversalType> ValidOptionTypes { get; } = new[]
    {
        UniversalType.Integer,
        UniversalType.Float,
        UniversalType.String,
        UniversalType.Boolean,
        UniversalType.LinedefType,
        UniversalType.SectorEffect,
        UniversalType.Texture,
        UniversalType.Flat,
        UniversalType.AngleDegrees,
        UniversalType.AngleRadians,
        UniversalType.Color,
        UniversalType.EnumOption,
        UniversalType.SectorTag,
        UniversalType.ThingTag,
        UniversalType.LinedefTag,
        UniversalType.AngleDegreesFloat,
        UniversalType.ThingType,
        UniversalType.ThingClass,
        UniversalType.AngleByte,
        UniversalType.PolyobjectNumber,
    };

    public static UdbScriptDirectory DiscoverFromAppPath(string appPath)
        => Discover(Path.Combine(appPath, ScriptFolder, ScriptsSubfolder));

    public static UdbScriptDirectory Discover(string scriptsPath)
    {
        string name = Path.GetFileName(scriptsPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(name)) name = scriptsPath;

        if (!Directory.Exists(scriptsPath))
            return new UdbScriptDirectory(scriptsPath, name, HashPath(scriptsPath), Array.Empty<UdbScriptDirectory>(), Array.Empty<UdbScriptInfo>());

        var directories = Directory.EnumerateDirectories(scriptsPath)
            .Where(path => !Path.GetFileName(path).StartsWith(".", StringComparison.Ordinal))
            .Select(Discover)
            .ToArray();

        var scripts = Directory.EnumerateFiles(scriptsPath, "*.js")
            .Select(LoadScriptWithRetry)
            .Where(result => result.Succeeded && result.Script is not null)
            .Select(result => result.Script!)
            .ToArray();

        return new UdbScriptDirectory(scriptsPath, name, HashPath(scriptsPath), directories, scripts);
    }

    public static UdbScriptInfo ParseScript(string scriptFile)
    {
        string text = File.ReadAllText(scriptFile);
        string name = Path.GetFileNameWithoutExtension(scriptFile);
        string description = DefaultDescription;
        uint version = DefaultVersion;
        string? rawOptions = null;
        IReadOnlyList<UdbScriptOption> options = Array.Empty<UdbScriptOption>();

        foreach ((string command, string payload) in ReadMetadata(text))
        {
            switch (command.ToLowerInvariant())
            {
                case "name":
                    name = NormalizeTemplatePayload(payload);
                    break;
                case "description":
                    description = NormalizeTemplatePayload(payload);
                    break;
                case "version":
                    if (!uint.TryParse(payload.Trim(), out version))
                        throw new ArgumentException("Invalid version value");
                    if (version == 0)
                        throw new ArgumentException("Version number has to be at least 1");
                    break;
                case "scriptoptions":
                    rawOptions = payload;
                    options = ParseOptions(payload, scriptFile);
                    break;
            }
        }

        return new UdbScriptInfo(name, description, version, scriptFile, HashPath(scriptFile), rawOptions, options);
    }

    public static string HashPath(string path)
        => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(path))).ToLowerInvariant();

    public static IReadOnlyList<UdbScriptOption> ParseOptions(string configText, string scriptFile)
    {
        var cfg = new Configuration();
        if (!cfg.InputConfiguration(configText, sorted: true))
            throw new ArgumentException("Error parsing script options: " + cfg.ErrorDescription);

        string scriptHash = HashPath(scriptFile);
        var options = new List<UdbScriptOption>();
        foreach (DictionaryEntry entry in cfg.Root)
        {
            if (entry.Value is not IDictionary)
                continue;

            string optionName = entry.Key.ToString() ?? "";
            string description = cfg.ReadSetting($"{optionName}.description", DefaultOptionDescription) ?? DefaultOptionDescription;
            int type = cfg.ReadSetting($"{optionName}.type", 0);
            object defaultValue = cfg.ReadSettingObject($"{optionName}.default", "") ?? "";
            IReadOnlyList<UdbScriptEnumValue> enumValues = ReadEnumValues(cfg.ReadSetting($"{optionName}.enumvalues", (IDictionary?)null));

            if (!IsValidOptionType(type))
                continue;

            object effectiveDefault = EffectiveDefault(defaultValue, enumValues);
            options.Add(new UdbScriptOption(
                optionName,
                description,
                type,
                effectiveDefault,
                effectiveDefault,
                enumValues,
                $"scripts.{scriptHash}.options.{optionName}"));
        }

        return options;
    }

    public static bool IsValidOptionType(int type)
        => ValidOptionTypes.Any(valid => (int)valid == type);

    public static bool ShouldReloadAfterWatcherEvent(
        WatcherChangeTypes changeType,
        string fullPath,
        bool fullPathIsDirectory)
        => changeType == WatcherChangeTypes.Deleted
            || (fullPathIsDirectory && changeType != WatcherChangeTypes.Changed)
            || string.Equals(Path.GetExtension(fullPath), ".js", StringComparison.OrdinalIgnoreCase);

    public static UdbScriptLoadRetryPolicy LoadRetryPolicy()
        => new(
            MaxAttempts: ScriptLoadRetryAttempts,
            DelayMilliseconds: ScriptLoadRetryDelayMilliseconds);

    public static UdbScriptLoadResult LoadScriptWithRetry(string scriptFile)
        => LoadScriptWithRetry(scriptFile, ParseScript, LoadRetryPolicy());

    public static UdbScriptLoadResult LoadScriptWithRetry(
        string scriptFile,
        Func<string, UdbScriptInfo> parser,
        UdbScriptLoadRetryPolicy policy)
    {
        int maxAttempts = Math.Max(1, policy.MaxAttempts);
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return new UdbScriptLoadResult(parser(scriptFile), attempt, true, "");
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                if (policy.DelayMilliseconds > 0)
                    Thread.Sleep(policy.DelayMilliseconds);
            }
            catch (Exception ex)
            {
                return new UdbScriptLoadResult(null, attempt, false, "Failed to process " + scriptFile + ": " + ex.Message);
            }
        }

        return new UdbScriptLoadResult(null, maxAttempts, false, "Failed to process " + scriptFile);
    }

    public static UdbScriptInfo ApplySavedOptionValues(
        UdbScriptInfo script,
        IReadOnlyDictionary<string, object?> settings)
    {
        var options = script.Options
            .Select(option => ApplySavedOptionValue(option, settings))
            .ToArray();

        return script with { Options = options };
    }

    public static UdbScriptDirectory ApplySavedOptionValues(
        UdbScriptDirectory directory,
        IReadOnlyDictionary<string, object?> settings)
    {
        UdbScriptDirectory[] directories = directory.Directories
            .Select(child => ApplySavedOptionValues(child, settings))
            .ToArray();
        UdbScriptInfo[] scripts = directory.Scripts
            .Select(script => ApplySavedOptionValues(script, settings))
            .ToArray();

        return directory with { Directories = directories, Scripts = scripts };
    }

    public static IReadOnlyList<UdbScriptSettingOperation> SaveOptionValueOperations(UdbScriptInfo script)
    {
        var operations = new List<UdbScriptSettingOperation>();
        int writtenOptions = 0;

        foreach (UdbScriptOption option in script.Options)
        {
            if (ValueText(option.Value) == ValueText(option.DefaultValue))
            {
                operations.Add(new UdbScriptSettingOperation(UdbScriptSettingOperationKind.Delete, option.SettingKey));
            }
            else
            {
                operations.Add(new UdbScriptSettingOperation(UdbScriptSettingOperationKind.Write, option.SettingKey, option.Value));
                writtenOptions++;
            }
        }

        if (script.Options.Count > 0 && writtenOptions == 0)
        {
            operations.Add(new UdbScriptSettingOperation(UdbScriptSettingOperationKind.Delete, $"scripts.{script.PathHash}.options"));
            operations.Add(new UdbScriptSettingOperation(UdbScriptSettingOperationKind.Delete, $"scripts.{script.PathHash}"));
        }

        return operations;
    }

    private static IReadOnlyList<UdbScriptEnumValue> ReadEnumValues(IDictionary? values)
    {
        if (values is null || values.Count == 0)
            return Array.Empty<UdbScriptEnumValue>();

        var result = new List<UdbScriptEnumValue>();
        foreach (DictionaryEntry entry in values)
            result.Add(new UdbScriptEnumValue(entry.Key.ToString() ?? "", entry.Value?.ToString()));

        return result;
    }

    internal static object EffectiveDefault(object defaultValue, IReadOnlyList<UdbScriptEnumValue> enumValues)
    {
        if (enumValues.Count == 0)
            return defaultValue;

        string defaultText = defaultValue.ToString() ?? "";
        foreach (UdbScriptEnumValue value in enumValues)
        {
            if (value.Key == defaultText)
                return value.Label ?? value.Key;
        }

        return defaultValue;
    }

    private static UdbScriptOption ApplySavedOptionValue(
        UdbScriptOption option,
        IReadOnlyDictionary<string, object?> settings)
    {
        if (!settings.TryGetValue(option.SettingKey, out object? savedValue))
            return option;

        string text = savedValue?.ToString() ?? "";
        return string.IsNullOrWhiteSpace(text)
            ? option with { Value = option.DefaultValue }
            : option with { Value = text };
    }

    private static string ValueText(object? value)
        => value?.ToString() ?? "";

    private static IEnumerable<(string Command, string Payload)> ReadMetadata(string text)
    {
        int index = 0;
        while (TryReadMetadataTemplate(text, ref index, out string template))
        {
            Match match = Regex.Match(template.Trim(), @"\s*#([^\s]+)\s+(.*)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
                yield return (match.Groups[1].Value, match.Groups[2].Value);

            SkipWhitespaceAndComments(text, ref index);
            if (index < text.Length && text[index] == ';')
                index++;
        }
    }

    private static bool TryReadMetadataTemplate(string text, ref int index, out string template)
    {
        template = "";
        SkipWhitespaceAndComments(text, ref index);
        if (index >= text.Length || text[index] != '`') return false;

        int start = ++index;
        bool escaped = false;
        while (index < text.Length)
        {
            char c = text[index];
            if (escaped)
            {
                escaped = false;
            }
            else if (c == '\\')
            {
                escaped = true;
            }
            else if (c == '`')
            {
                template = text[start..index];
                index++;
                return true;
            }

            index++;
        }

        return false;
    }

    private static void SkipWhitespaceAndComments(string text, ref int index)
    {
        while (index < text.Length)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                index++;
                continue;
            }

            if (index + 1 < text.Length && text[index] == '/' && text[index + 1] == '/')
            {
                index += 2;
                while (index < text.Length && text[index] != '\r' && text[index] != '\n')
                    index++;
                continue;
            }

            if (index + 1 < text.Length && text[index] == '/' && text[index + 1] == '*')
            {
                index += 2;
                while (index + 1 < text.Length && !(text[index] == '*' && text[index + 1] == '/'))
                    index++;
                if (index + 1 < text.Length) index += 2;
                continue;
            }

            break;
        }
    }

    private static string NormalizeTemplatePayload(string payload)
        => Regex.Replace(payload, @"(\r\n?|\n)+", " ", RegexOptions.Singleline);
}
