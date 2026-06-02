// ABOUTME: Discovers UDBScript script files and leading metadata in UDB-compatible directory trees.
// ABOUTME: Provides script and folder records for future docker UI, slot assignment, and script execution.

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DBuilder.IO;

public sealed record UdbScriptInfo(
    string Name,
    string Description,
    uint Version,
    string ScriptFile,
    string PathHash,
    string? RawOptions);

public sealed record UdbScriptDirectory(
    string Path,
    string Name,
    string Hash,
    IReadOnlyList<UdbScriptDirectory> Directories,
    IReadOnlyList<UdbScriptInfo> Scripts);

public static class UdbScriptDiscovery
{
    public const string ScriptFolder = "UDBScript";
    public const string ScriptsSubfolder = "Scripts";
    public const uint DefaultVersion = 1;
    public const string DefaultDescription = "No description.";

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
            .Select(ParseScript)
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
                    break;
            }
        }

        return new UdbScriptInfo(name, description, version, scriptFile, HashPath(scriptFile), rawOptions);
    }

    public static string HashPath(string path)
        => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(path))).ToLowerInvariant();

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
