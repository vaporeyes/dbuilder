// ABOUTME: Parses UDB compiler and nodebuilder configuration files into metadata records.
// ABOUTME: Keeps executable, interface, include files and nodebuilder parameter templates available to editor code.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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

public sealed record ScriptCompilePlan(
    ScriptCompilerPaths Paths,
    string WorkingDirectory,
    string InputCopyPath,
    string OutputPath);

public sealed record ScriptCompileIncludeCopy(string IncludeName, string TargetPath, bool ShouldCopy);

public sealed record ScriptCompileTarget(string TargetPath, string ErrorMessage = "")
{
    public bool Success => ErrorMessage.Length == 0;
}

public sealed record AcsCompilePreflightResult(
    bool ShouldCompile,
    string LibraryName,
    IReadOnlySet<string> Includes,
    ScriptCompilerError? Error);

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

public static class ScriptCompilerProcess
{
    public static ProcessStartInfo CreateStartInfo(CompilerInfo compiler, string arguments, string workingDirectory)
        => compiler.ProgramInterface switch
        {
            "BccCompiler" => CreateBccStartInfo(compiler, arguments, workingDirectory),
            "ZtBccCompiler" => CreateZtBccStartInfo(compiler, arguments, workingDirectory),
            _ => CreateAccStartInfo(compiler, arguments, workingDirectory),
        };

    public static ProcessStartInfo CreateAccStartInfo(CompilerInfo compiler, string arguments, string workingDirectory)
    {
        return new ProcessStartInfo
        {
            Arguments = arguments,
            FileName = Path.Combine(compiler.Path, compiler.ProgramFile),
            CreateNoWindow = false,
            ErrorDialog = false,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = workingDirectory
        };
    }

    public static ProcessStartInfo CreateBccStartInfo(CompilerInfo compiler, string arguments, string workingDirectory)
    {
        ProcessStartInfo startInfo = CreateAccStartInfo(compiler, arguments, workingDirectory);
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardOutput = true;
        return startInfo;
    }

    public static ProcessStartInfo CreateZtBccStartInfo(CompilerInfo compiler, string arguments, string workingDirectory)
        => CreateBccStartInfo(compiler, arguments, workingDirectory);
}

public static class ScriptCompileFlow
{
    public static ScriptCompilePlan BuildDirectoryPlan(string sourceFile, string compilerTempDirectory, string outputPath)
    {
        string inputCopyPath = Path.Combine(compilerTempDirectory, Path.GetFileName(sourceFile));
        return new ScriptCompilePlan(
            new ScriptCompilerPaths(
                inputCopyPath,
                Path.GetFileName(outputPath),
                sourceFile,
                compilerTempDirectory,
                Path.GetDirectoryName(sourceFile) ?? ""),
            compilerTempDirectory,
            inputCopyPath,
            outputPath);
    }

    public static ScriptCompilePlan BuildArchivePlan(string archiveEntryName, string compilerTempDirectory, string outputPath)
    {
        string inputCopyPath = Path.Combine(compilerTempDirectory, Path.GetFileName(archiveEntryName));
        return new ScriptCompilePlan(
            new ScriptCompilerPaths(
                inputCopyPath,
                Path.GetFileName(outputPath),
                inputCopyPath,
                compilerTempDirectory,
                Path.GetDirectoryName(archiveEntryName) ?? ""),
            compilerTempDirectory,
            inputCopyPath,
            outputPath);
    }

    public static ScriptCompileTarget ResolveFileTarget(
        string sourceFile,
        string resultLump,
        CompilerInfo compiler,
        string libraryName,
        string scriptConfigurationName = "")
        => ResolveFileTarget(sourceFile, resultLump, IsAccCompiler(compiler), libraryName, scriptConfigurationName);

    public static ScriptCompileTarget ResolveFileTarget(
        string sourceFile,
        string resultLump,
        bool isAccCompiler,
        string libraryName,
        string scriptConfigurationName = "")
    {
        string sourceDirectory = Path.GetDirectoryName(sourceFile) ?? "";
        if (isAccCompiler) return new ScriptCompileTarget(Path.Combine(sourceDirectory, libraryName + ".o"));
        if (string.IsNullOrEmpty(resultLump)) return MissingResultLumpTarget(scriptConfigurationName);
        return new ScriptCompileTarget(Path.Combine(sourceDirectory, resultLump));
    }

    public static ScriptCompileTarget ResolveArchiveTarget(
        string archiveEntryName,
        string resultLump,
        CompilerInfo compiler,
        string libraryName,
        string scriptConfigurationName = "")
        => ResolveArchiveTarget(archiveEntryName, resultLump, IsAccCompiler(compiler), libraryName, scriptConfigurationName);

    public static ScriptCompileTarget ResolveArchiveTarget(
        string archiveEntryName,
        string resultLump,
        bool isAccCompiler,
        string libraryName,
        string scriptConfigurationName = "")
    {
        string entryDirectory = Path.GetDirectoryName(archiveEntryName) ?? "";
        if (isAccCompiler) return new ScriptCompileTarget(Path.Combine(entryDirectory, libraryName + ".o"));
        if (string.IsNullOrEmpty(resultLump)) return MissingResultLumpTarget(scriptConfigurationName);
        return new ScriptCompileTarget(Path.Combine(entryDirectory, resultLump));
    }

    public static ScriptCompilerError MissingOutputFileError(string outputPath)
        => new("Output file \"" + outputPath + "\" doesn't exist.");

    public static IReadOnlyList<ScriptCompileIncludeCopy> BuildIncludeCopyPlan(
        IEnumerable<string> includes,
        string compilerTempDirectory,
        IEnumerable<string>? existingTargetPaths = null)
    {
        var existing = new HashSet<string>(
            existingTargetPaths ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        return includes
            .Select(include =>
            {
                string normalizedInclude = NormalizeIncludePath(include);
                string target = Path.Combine(compilerTempDirectory, normalizedInclude);
                return new ScriptCompileIncludeCopy(normalizedInclude, target, !existing.Contains(target));
            })
            .ToList();
    }

    public static IReadOnlyList<string> CopyIncludes(
        IEnumerable<ScriptCompileIncludeCopy> copyPlan,
        Func<string, byte[]?> readInclude)
    {
        var copied = new List<string>();
        foreach (ScriptCompileIncludeCopy copy in copyPlan)
        {
            if (!copy.ShouldCopy) continue;
            byte[]? data = readInclude(copy.IncludeName);
            if (data is null) continue;

            string? directory = Path.GetDirectoryName(copy.TargetPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(copy.TargetPath, data);
            copied.Add(copy.TargetPath);
        }

        return copied;
    }

    public static ScriptCompilerError RemapDirectoryError(ScriptCompilerError error, string inputCopyPath, string sourceFile)
        => SamePath(error.FileName, inputCopyPath)
            ? error with { FileName = sourceFile }
            : error;

    public static ScriptCompilerError RemapArchiveError(ScriptCompilerError error, string inputCopyPath, string archiveEntryName)
        => SamePath(error.FileName, inputCopyPath)
            ? error with { FileName = archiveEntryName }
            : error;

    public static ScriptCompilerError RemapWadLumpError(ScriptCompilerError error, string inputFile, string lumpName)
        => SamePath(error.FileName, inputFile)
            ? error with { FileName = "?" + lumpName }
            : error;

    private static ScriptCompileTarget MissingResultLumpTarget(string scriptConfigurationName)
    {
        string name = string.IsNullOrEmpty(scriptConfigurationName) ? "" : "\"" + scriptConfigurationName + "\" ";
        return new ScriptCompileTarget("", "Unable to create target file: unable to determine target filename. Make sure \"ResultLump\" property is set in the " + name + "script configuration.");
    }

    private static bool SamePath(string left, string right)
        => string.Equals(
            left.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar),
            right.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizeIncludePath(string value)
        => value
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

    private static bool IsAccCompiler(CompilerInfo compiler)
        => string.Equals(compiler.ProgramInterface, "AccCompiler", StringComparison.Ordinal);
}

public sealed record ScriptCompilerError(string Description, string FileName = "", int LineNumber = -1);

public sealed record ScriptCompilerErrorDisplayItem(int Index, string Description, string Source);

public static class ScriptCompilerErrorDisplay
{
    public static IReadOnlyList<ScriptCompilerError> Combine(
        IEnumerable<ScriptCompilerError> existing,
        IEnumerable<ScriptCompilerError>? incoming)
    {
        var result = new List<ScriptCompilerError>(existing);
        if (incoming is null) return result;
        foreach (ScriptCompilerError error in incoming)
        {
            if (!result.Contains(error)) result.Add(error);
        }

        return result;
    }

    public static IReadOnlyList<ScriptCompilerErrorDisplayItem> BuildItems(IEnumerable<ScriptCompilerError> errors)
    {
        var items = new List<ScriptCompilerErrorDisplayItem>();
        int index = 1;
        foreach (ScriptCompilerError error in errors)
        {
            items.Add(new ScriptCompilerErrorDisplayItem(
                index,
                error.Description,
                SourceText(error)));
            index++;
        }

        return items;
    }

    private static string SourceText(ScriptCompilerError error)
    {
        string fileName = error.FileName.StartsWith("?", StringComparison.Ordinal)
            ? error.FileName.Replace("?", "")
            : ErrorFileName(error.FileName);
        string lineNumber = error.LineNumber != -1 ? " (line " + (error.LineNumber + 1) + ")" : "";
        return fileName + lineNumber;
    }

    private static string ErrorFileName(string fileName)
    {
        fileName = UnquoteFileName(fileName.Trim());
        string normalized = fileName.Replace('\\', Path.DirectorySeparatorChar);
        return Path.GetFileName(normalized);
    }

    private static string UnquoteFileName(string fileName)
    {
        if (fileName.Length >= 2
            && ((fileName[0] == '"' && fileName[^1] == '"')
                || (fileName[0] == '\'' && fileName[^1] == '\'')))
            return fileName[1..^1];
        return fileName;
    }
}

public static class AcsCompilePreflight
{
    public static AcsCompilePreflightResult Analyze(
        string text,
        string sourceFile,
        bool sourceIsMapScriptsLump,
        IEnumerable<string>? compilerFiles = null,
        Func<string, bool>? includeExists = null)
    {
        var includes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var compilerIncludes = new HashSet<string>(
            (compilerFiles ?? Array.Empty<string>()).Select(NormalizeIncludePath),
            StringComparer.OrdinalIgnoreCase);
        string libraryName = "";

        if (sourceIsMapScriptsLump && text.Length == 0)
            return new AcsCompilePreflightResult(false, "", includes, null);

        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimStart();
            if (line.StartsWith("#library", StringComparison.Ordinal))
            {
                if (TryReadQuotedArgument(line["#library".Length..].Trim(), out string name)) libraryName = name;
                continue;
            }

            string token = line.StartsWith("#include", StringComparison.Ordinal)
                ? "#include"
                : line.StartsWith("#import", StringComparison.Ordinal)
                    ? "#import"
                    : "";
            if (token.Length == 0) continue;

            string value = line[token.Length..].Trim();
            if (!TryReadQuotedArgument(value, out string include))
                return Error(token + " filename should be quoted.", sourceFile, i);

            include = NormalizeIncludePath(include);
            if (include.Length == 0)
                return Error("Expected file name to " + token + ".", sourceFile, i);

            bool isCompilerInclude = compilerIncludes.Contains(include);
            if (!includes.Add(include))
                return Error("Already parsed \"" + include + "\". Check your " + token + " directives.", sourceFile, i);
            if (!isCompilerInclude && includeExists is not null && !includeExists(include))
                return Error("Unable to find include file \"" + include + "\".", sourceFile, i);
        }

        if (!sourceIsMapScriptsLump && libraryName.Length == 0)
            return Error("External ACS files can only be compiled as libraries.", sourceFile);

        includes.ExceptWith(compilerIncludes);
        return new AcsCompilePreflightResult(true, libraryName, includes, null);
    }

    private static AcsCompilePreflightResult Error(string description, string sourceFile, int lineNumber = -1)
        => new(true, "", new HashSet<string>(StringComparer.OrdinalIgnoreCase), new ScriptCompilerError(description, sourceFile, lineNumber));

    private static bool TryReadQuotedArgument(string value, out string argument)
    {
        argument = "";
        if (value.Length < 2 || value[0] != '"') return false;
        int end = value.IndexOf('"', 1);
        if (end < 0) return false;
        argument = value[1..end];
        return true;
    }

    private static string NormalizeIncludePath(string value)
        => value
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
}

public static class ScriptCompilerErrors
{
    public static IReadOnlyList<ScriptCompilerError> Parse(
        CompilerInfo compiler,
        IEnumerable<string> lines,
        string tempPath,
        string workingDirectory,
        Func<string, string?>? resolveIncludeFile = null)
        => compiler.ProgramInterface switch
        {
            "BccCompiler" => ParseBcc(lines, tempPath, workingDirectory, resolveIncludeFile),
            "ZtBccCompiler" => ParseZtBcc(lines, tempPath, workingDirectory, resolveIncludeFile),
            _ => ParseAcc(lines, tempPath, workingDirectory, resolveIncludeFile),
        };

    public static IReadOnlyList<ScriptCompilerError> ParseAcc(
        IEnumerable<string> lines,
        string tempPath,
        string workingDirectory,
        Func<string, string?>? resolveIncludeFile = null)
    {
        var errors = new List<ScriptCompilerError>();
        foreach (string line in lines)
        {
            if (!TryFindAccErrorLocation(line, out int firstColon, out int secondColon, out int lineNumber))
                continue;

            errors.Add(new ScriptCompilerError(
                line[(secondColon + 2)..].Trim(),
                NormalizeCompilerErrorFile(line[..firstColon], tempPath, workingDirectory, resolveIncludeFile),
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
        string workingDirectory,
        Func<string, string?>? resolveIncludeFile = null)
        => ParseBccFormat(lines, tempPath, workingDirectory, resolveIncludeFile);

    public static IReadOnlyList<ScriptCompilerError> ParseZtBcc(
        IEnumerable<string> stderrLines,
        string tempPath,
        string workingDirectory,
        Func<string, string?>? resolveIncludeFile = null)
        => ParseBccFormat(stderrLines, tempPath, workingDirectory, resolveIncludeFile);

    private static IReadOnlyList<ScriptCompilerError> ParseBccFormat(
        IEnumerable<string> lines,
        string tempPath,
        string workingDirectory,
        Func<string, string?>? resolveIncludeFile)
    {
        var errors = new List<ScriptCompilerError>();
        foreach (string line in lines)
        {
            if (!TryFindBccErrorLocation(line, out string fileName, out int lineNumber, out string description))
                continue;
            errors.Add(new ScriptCompilerError(
                description,
                NormalizeCompilerErrorFile(fileName, tempPath, workingDirectory, resolveIncludeFile),
                lineNumber - 1));
        }

        if (errors.Count == 0)
        {
            var fallback = string.Join(Environment.NewLine, lines).Trim();
            if (fallback.Length > 0) errors.Add(new ScriptCompilerError(fallback));
        }

        return errors;
    }

    private static bool TryFindBccErrorLocation(string line, out string fileName, out int lineNumber, out string description)
    {
        fileName = "";
        lineNumber = -1;
        description = "";

        for (int firstColon = 1; firstColon < line.Length; firstColon++)
        {
            if (line[firstColon] != ':') continue;
            int secondColon = line.IndexOf(':', firstColon + 1);
            if (secondColon <= firstColon + 1) continue;
            int thirdColon = line.IndexOf(':', secondColon + 1);
            if (thirdColon <= secondColon + 1) continue;
            if (!int.TryParse(line[(firstColon + 1)..secondColon], out lineNumber)) continue;
            if (!int.TryParse(line[(secondColon + 1)..thirdColon], out _)) continue;

            fileName = line[..firstColon];
            description = line[(thirdColon + 1)..].Trim();
            return true;
        }

        return false;
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

    private static string NormalizeCompilerErrorFile(
        string fileName,
        string tempPath,
        string workingDirectory,
        Func<string, string?>? resolveIncludeFile)
    {
        fileName = UnquoteCompilerFileName(fileName.Trim());
        string tempNormalized = NormalizePathSeparators(tempPath);
        string tempPrefix = tempNormalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string alternateTempPrefix = tempPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + '\\';
        string normalized = NormalizePathSeparators(fileName);
        if (normalized.StartsWith(tempPrefix, StringComparison.Ordinal))
            normalized = normalized[tempPrefix.Length..];
        else if (fileName.StartsWith(alternateTempPrefix, StringComparison.Ordinal))
            normalized = NormalizePathSeparators(fileName[alternateTempPrefix.Length..]);
        else if (IsRootedCompilerPath(fileName))
            return fileName;
        if (!IsRootedCompilerPath(normalized) && resolveIncludeFile is not null)
        {
            string? includeFile = resolveIncludeFile(normalized);
            if (!string.IsNullOrEmpty(includeFile)) return includeFile;
        }

        return IsRootedCompilerPath(normalized) ? normalized : Path.Combine(workingDirectory, normalized);
    }

    private static string UnquoteCompilerFileName(string fileName)
    {
        if (fileName.Length >= 2
            && ((fileName[0] == '"' && fileName[^1] == '"')
                || (fileName[0] == '\'' && fileName[^1] == '\'')))
            return fileName[1..^1];
        return fileName;
    }

    private static string NormalizePathSeparators(string path)
        => path
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

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

        foreach (string file in Directory.EnumerateFiles(path, "*.cfg", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
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
            compilers.TryAdd(compiler.Key, compiler.Value);
        foreach (var nodebuilder in other.nodebuilders)
            nodebuilders[nodebuilder.Key] = nodebuilder.Value;
    }

    public NodebuilderConfig? ResolveNodebuilderConfig(string nodebuilderName, string? executableOverride = null)
    {
        if (!nodebuilders.TryGetValue(nodebuilderName, out var nodebuilder)) return null;

        string executable = executableOverride ?? "";
        CompilerInfo? compiler = null;
        if (string.IsNullOrWhiteSpace(executable)
            && compilers.TryGetValue(nodebuilder.CompilerName, out compiler)
            && !string.IsNullOrWhiteSpace(compiler.ProgramFile))
        {
            executable = Path.Combine(compiler.Path, compiler.ProgramFile);
        }
        else if (compiler is null)
        {
            compilers.TryGetValue(nodebuilder.CompilerName, out compiler);
        }

        if (string.IsNullOrWhiteSpace(executable)) return null;
        if (compiler != null && compiler.Files.Count > 0)
            return new NodebuilderConfig(executable, nodebuilder.Parameters, compiler.Path, compiler.Files.ToArray());

        return nodebuilder.ToConfig(executable);
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

            compilers.TryAdd(name, new CompilerInfo(
                fileName,
                name,
                path,
                GetString(compiler, "program", ""),
                GetString(compiler, "interface", ""),
                files));
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
