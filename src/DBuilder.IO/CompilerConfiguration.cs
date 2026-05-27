// ABOUTME: Parses UDB compiler and nodebuilder configuration files into metadata records.
// ABOUTME: Keeps executable, interface, include files and nodebuilder parameter templates available to editor code.

using System;
using System.Collections;
using System.Collections.Generic;

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
