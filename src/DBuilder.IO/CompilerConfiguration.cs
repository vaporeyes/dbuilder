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
