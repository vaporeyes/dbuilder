// ABOUTME: Parses UDB compiler and nodebuilder configuration files into metadata records.
// ABOUTME: Keeps executable, interface, include files and nodebuilder parameter templates available to editor code.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DBuilder.IO;

public sealed record CompilerInfo(
    string FileName,
    string Name,
    string Path,
    string ProgramFile,
    string ProgramInterface,
    IReadOnlySet<string> Files);

public sealed record ScriptCompilerPaths(
    string InputFile,
    string OutputFile,
    string SourceFile,
    string TempPath,
    string SourcePath);

public static class ScriptCompilerArguments
{
    public static string Build(string parameters, ScriptCompilerPaths paths)
        => parameters
            .Replace("%FI", paths.InputFile)
            .Replace("%FO", paths.OutputFile)
            .Replace("%FS", paths.SourceFile)
            .Replace("%PT", paths.TempPath)
            .Replace("%PS", paths.SourcePath)
            .Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}

public sealed record ScriptCompilerError(string Description, string FileName = "", int LineNumber = -1);

public static class ScriptCompilerErrors
{
    public static IReadOnlyList<ScriptCompilerError> ParseAcc(
        IEnumerable<string> lines,
        string tempPath,
        string workingDirectory)
    {
        var errors = new List<ScriptCompilerError>();
        foreach (string line in lines)
        {
            if (!TryFindAccErrorLocation(line, out int firstColon, out int secondColon, out int lineNumber))
                continue;

            errors.Add(new ScriptCompilerError(
                line[(secondColon + 2)..].Trim(),
                NormalizeCompilerErrorFile(line[..firstColon], tempPath, workingDirectory),
                lineNumber - 1));
        }

        if (errors.Count == 0)
        {
            var fallback = string.Join(Environment.NewLine, lines).Trim();
            if (fallback.Length > 0) errors.Add(new ScriptCompilerError(fallback));
        }

        return errors;
    }

    public static IReadOnlyList<ScriptCompilerError> ParseBcc(
        IEnumerable<string> lines,
        string tempPath,
        string workingDirectory)
    {
        var errors = new List<ScriptCompilerError>();
        foreach (string line in lines)
        {
            string[] parts = line.Split(new[] { ':' }, 4);
            if (parts.Length != 4 || !int.TryParse(parts[1], out int lineNumber)) continue;
            errors.Add(new ScriptCompilerError(
                parts[3].Trim(),
                NormalizeCompilerErrorFile(parts[0], tempPath, workingDirectory),
                lineNumber - 1));
        }

        if (errors.Count == 0)
        {
            var fallback = string.Join(Environment.NewLine, lines).Trim();
            if (fallback.Length > 0) errors.Add(new ScriptCompilerError(fallback));
        }

        return errors;
    }

    private static bool TryFindAccErrorLocation(string line, out int firstColon, out int secondColon, out int lineNumber)
    {
        for (firstColon = 1; firstColon < line.Length; firstColon++)
        {
            if (line[firstColon] != ':') continue;
            secondColon = line.IndexOf(':', firstColon + 1);
            if (secondColon <= firstColon + 1) continue;
            if (secondColon + 1 >= line.Length || line[secondColon + 1] != ' ') continue;
            if (int.TryParse(line[(firstColon + 1)..secondColon], out lineNumber))
                return true;
        }

        secondColon = -1;
        lineNumber = -1;
        return false;
    }

    private static string NormalizeCompilerErrorFile(string fileName, string tempPath, string workingDirectory)
    {
        string normalized = fileName.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        string tempPrefix = tempPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (normalized.StartsWith(tempPrefix, StringComparison.Ordinal))
            normalized = normalized[tempPrefix.Length..];
        return IsRootedCompilerPath(normalized) ? normalized : Path.Combine(workingDirectory, normalized);
    }

    private static bool IsRootedCompilerPath(string fileName)
    {
        if (Path.IsPathRooted(fileName)) return true;
        return fileName.Length >= 3
            && char.IsLetter(fileName[0])
            && fileName[1] == ':'
            && (fileName[2] == '\\' || fileName[2] == '/');
    }
}

public sealed record NodebuilderInfo(
    string FileName,
    string Name,
    string Title,
    string CompilerName,
    string Parameters)
{
    public bool HasSpecialOutputFile => Parameters.Contains("%FO", StringComparison.Ordinal);

    public NodebuilderConfig ToConfig(string executable)
        => new(executable, Parameters);
}

public sealed class CompilerConfiguration
{
    private readonly Dictionary<string, CompilerInfo> compilers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NodebuilderInfo> nodebuilders = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, CompilerInfo> Compilers => compilers;
    public IReadOnlyDictionary<string, NodebuilderInfo> Nodebuilders => nodebuilders;

    public static CompilerConfiguration FromDirectory(string path)
    {
        var result = new CompilerConfiguration();
        if (!Directory.Exists(path)) return result;

        foreach (string file in Directory.EnumerateFiles(path, "*.cfg").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            result.MergeFrom(FromFile(file));

        return result;
    }

    public static CompilerConfiguration FromFile(string path)
    {
        var cfg = new Configuration(path);
        return FromConfiguration(cfg, System.IO.Path.GetFileName(path), System.IO.Path.GetDirectoryName(path) ?? "");
    }

    public static CompilerConfiguration FromText(string cfgText, string fileName = "", string path = "")
    {
        var cfg = new Configuration();
        cfg.InputConfiguration(cfgText);
        return FromConfiguration(cfg, fileName, path);
    }

    public static CompilerConfiguration FromConfiguration(Configuration cfg, string fileName = "", string path = "")
    {
        var result = new CompilerConfiguration();
        if (cfg.Root is IDictionary root)
        {
            if (root["compilers"] is IDictionary compilerBlock) result.ParseCompilers(compilerBlock, fileName, path);
            if (root["nodebuilders"] is IDictionary nodebuilderBlock) result.ParseNodebuilders(nodebuilderBlock, fileName);
        }
        return result;
    }

    public void MergeFrom(CompilerConfiguration other)
    {
        foreach (var compiler in other.compilers)
            compilers[compiler.Key] = compiler.Value;
        foreach (var nodebuilder in other.nodebuilders)
            nodebuilders[nodebuilder.Key] = nodebuilder.Value;
    }

    public NodebuilderConfig? ResolveNodebuilderConfig(string nodebuilderName, string? executableOverride = null)
    {
        if (!nodebuilders.TryGetValue(nodebuilderName, out var nodebuilder)) return null;

        string executable = executableOverride ?? "";
        if (string.IsNullOrWhiteSpace(executable)
            && compilers.TryGetValue(nodebuilder.CompilerName, out var compiler)
            && !string.IsNullOrWhiteSpace(compiler.ProgramFile))
            executable = Path.Combine(compiler.Path, compiler.ProgramFile);

        return string.IsNullOrWhiteSpace(executable) ? null : nodebuilder.ToConfig(executable);
    }

    private void ParseCompilers(IDictionary block, string fileName, string path)
    {
        foreach (DictionaryEntry e in block)
        {
            string name = e.Key.ToString() ?? "";
            if (name.Length == 0 || e.Value is not IDictionary compiler) continue;

            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry child in compiler)
            {
                string key = child.Key.ToString() ?? "";
                if (string.Equals(key, "interface", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(key, "program", StringComparison.OrdinalIgnoreCase)) continue;
                string include = (child.Value?.ToString() ?? "").Replace('\\', '/');
                if (include.Length > 0) files.Add(include);
            }

            compilers[name] = new CompilerInfo(
                fileName,
                name,
                path,
                GetString(compiler, "program", ""),
                GetString(compiler, "interface", ""),
                files);
        }
    }

    private void ParseNodebuilders(IDictionary block, string fileName)
    {
        foreach (DictionaryEntry e in block)
        {
            string name = e.Key.ToString() ?? "";
            if (name.Length == 0 || e.Value is not IDictionary nodebuilder) continue;
            nodebuilders[name] = new NodebuilderInfo(
                fileName,
                name,
                GetString(nodebuilder, "title", "<untitled configuration>"),
                GetString(nodebuilder, "compiler", ""),
                GetString(nodebuilder, "parameters", ""));
        }
    }

    private static string GetString(IDictionary d, string key, string fallback)
        => d[key] is string s ? s : fallback;
}
