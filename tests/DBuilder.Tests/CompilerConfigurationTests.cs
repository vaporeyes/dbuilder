// ABOUTME: Tests UDB compiler and nodebuilder cfg parsing into DBuilder metadata records.
// ABOUTME: Covers include-file normalization, nodebuilder profile defaults and nodebuilder config conversion.

using DBuilder.IO;
using System.IO;

namespace DBuilder.Tests;

public class CompilerConfigurationTests
{
    [Fact]
    public void ParsesCompilerDefinitionsAndIncludeFiles()
    {
        const string cfg = """
            compilers
            {
                zdoom_acc
                {
                    interface = "AccCompiler";
                    program = "acc";
                    zcommon = "zcommon.acs";
                    zdefs = "include\\zdefs.acs";
                }
            }
            """;

        var parsed = CompilerConfiguration.FromText(cfg, "acc.cfg", "/compilers/ZDoom");

        var compiler = parsed.Compilers["zdoom_acc"];
        Assert.Equal("acc.cfg", compiler.FileName);
        Assert.Equal("zdoom_acc", compiler.Name);
        Assert.Equal("/compilers/ZDoom", compiler.Path);
        Assert.Equal("acc", compiler.ProgramFile);
        Assert.Equal("AccCompiler", compiler.ProgramInterface);
        Assert.Contains("zcommon.acs", compiler.Files);
        Assert.Contains("include/zdefs.acs", compiler.Files);
        Assert.DoesNotContain("acc", compiler.Files);
    }

    [Fact]
    public void ParsesNodebuilderDefinitionsAndParameterMetadata()
    {
        const string cfg = """
            nodebuilders
            {
                zdbsp_normal
                {
                    title = "ZDBSP - Normal";
                    compiler = "zdbsp";
                    parameters = "-o%FO %FI";
                }

                zdbsp_inplace
                {
                    compiler = "zdbsp";
                    parameters = "%FI";
                }
            }
            """;

        var parsed = CompilerConfiguration.FromText(cfg, "zdbsp.cfg");

        var normal = parsed.Nodebuilders["zdbsp_normal"];
        Assert.Equal("zdbsp.cfg", normal.FileName);
        Assert.Equal("ZDBSP - Normal", normal.Title);
        Assert.Equal("zdbsp", normal.CompilerName);
        Assert.Equal("-o%FO %FI", normal.Parameters);
        Assert.True(normal.HasSpecialOutputFile);
        Assert.Equal(new NodebuilderConfig("/usr/bin/zdbsp", "-o%FO %FI"), normal.ToConfig("/usr/bin/zdbsp"));

        var inplace = parsed.Nodebuilders["zdbsp_inplace"];
        Assert.Equal("<untitled configuration>", inplace.Title);
        Assert.False(inplace.HasSpecialOutputFile);
    }

    [Fact]
    public void LoadsDirectoryAndResolvesNodebuilderExecutable()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_compilers_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "zdbsp.cfg"), """
                compilers
                {
                    zdbsp
                    {
                        interface = "NodesCompiler";
                        program = "zdbsp";
                    }
                }

                nodebuilders
                {
                    zdbsp_fast
                    {
                        compiler = "zdbsp";
                        parameters = "-R -o%FO %FI";
                    }
                }
                """);

            var parsed = CompilerConfiguration.FromDirectory(dir);
            var config = parsed.ResolveNodebuilderConfig("zdbsp_fast");

            Assert.Equal(new NodebuilderConfig(Path.Combine(dir, "zdbsp"), "-R -o%FO %FI"), config);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadsDirectoryRecursivelyLikeUdbCompilerPath()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_compilers_" + Guid.NewGuid().ToString("N"));
        string nested = Path.Combine(dir, "Nodebuilders");
        Directory.CreateDirectory(nested);
        try
        {
            File.WriteAllText(Path.Combine(nested, "zdbsp.cfg"), """
                compilers
                {
                    zdbsp
                    {
                        interface = "NodesCompiler";
                        program = "zdbsp";
                    }
                }

                nodebuilders
                {
                    zdbsp_normal
                    {
                        compiler = "zdbsp";
                        parameters = "-o%FO %FI";
                    }
                }
                """);

            var parsed = CompilerConfiguration.FromDirectory(dir);
            var config = parsed.ResolveNodebuilderConfig("zdbsp_normal");

            Assert.True(parsed.Compilers.ContainsKey("zdbsp"));
            Assert.True(parsed.Nodebuilders.ContainsKey("zdbsp_normal"));
            Assert.Equal(new NodebuilderConfig(Path.Combine(nested, "zdbsp"), "-o%FO %FI"), config);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadsDirectoryKeepsFirstDuplicateCompilerDefinition()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_compilers_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.cfg"), """
                compilers
                {
                    acc
                    {
                        interface = "AccCompiler";
                        program = "first-acc";
                    }
                }
                """);
            File.WriteAllText(Path.Combine(dir, "b.cfg"), """
                compilers
                {
                    acc
                    {
                        interface = "AccCompiler";
                        program = "second-acc";
                    }
                }
                """);

            var parsed = CompilerConfiguration.FromDirectory(dir);

            Assert.Equal("a.cfg", parsed.Compilers["acc"].FileName);
            Assert.Equal("first-acc", parsed.Compilers["acc"].ProgramFile);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ResolveNodebuilderUsesExecutableOverride()
    {
        const string cfg = """
            nodebuilders
            {
                zdbsp_fast
                {
                    compiler = "zdbsp";
                    parameters = "-R -o%FO %FI";
                }
            }
            """;

        var parsed = CompilerConfiguration.FromText(cfg);
        var config = parsed.ResolveNodebuilderConfig("zdbsp_fast", "/usr/local/bin/zdbsp");

        Assert.Equal(new NodebuilderConfig("/usr/local/bin/zdbsp", "-R -o%FO %FI"), config);
    }

    [Fact]
    public void ScriptCompilerArgumentsSubstituteUdbAccTokens()
    {
        var paths = new ScriptCompilerPaths(
            "input.wad",
            "behavior.o",
            "scripts.acs",
            "/tmp/dbuilder_compile",
            "/maps/project");

        string args = ScriptCompilerArguments.Build("-i %FI -o %FO -s %FS -temp %PT -I %PS", paths);

        Assert.Equal("-i input.wad -o behavior.o -s scripts.acs -temp /tmp/dbuilder_compile -I /maps/project", args);
    }

    [Fact]
    public void ScriptCompilerProcessBuildsAccStartInfoLikeUdb()
    {
        var compiler = new CompilerInfo(
            "acc.cfg",
            "zdoom_acc",
            "/compilers/ZDoom",
            "acc",
            "AccCompiler",
            new HashSet<string>());

        var startInfo = ScriptCompilerProcess.CreateAccStartInfo(
            compiler,
            "-i scripts.acs -o behavior.o",
            "/tmp/dbuilder_compile");

        Assert.Equal(Path.Combine("/compilers/ZDoom", "acc"), startInfo.FileName);
        Assert.Equal("-i scripts.acs -o behavior.o", startInfo.Arguments);
        Assert.Equal("/tmp/dbuilder_compile", startInfo.WorkingDirectory);
        Assert.False(startInfo.CreateNoWindow);
        Assert.False(startInfo.RedirectStandardError);
        Assert.False(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.UseShellExecute);
        Assert.Equal(System.Diagnostics.ProcessWindowStyle.Hidden, startInfo.WindowStyle);
    }

    [Fact]
    public void ScriptCompilerProcessBuildsBccStartInfoLikeUdb()
    {
        var compiler = new CompilerInfo(
            "bcc.cfg",
            "bcc",
            "/compilers/BCC",
            "bcc",
            "BccCompiler",
            new HashSet<string>());

        var startInfo = ScriptCompilerProcess.CreateBccStartInfo(
            compiler,
            "-acc-err-file -i scripts.bcs -o behavior.o",
            "/tmp/dbuilder_compile");

        Assert.Equal(Path.Combine("/compilers/BCC", "bcc"), startInfo.FileName);
        Assert.Equal("-acc-err-file -i scripts.bcs -o behavior.o", startInfo.Arguments);
        Assert.Equal("/tmp/dbuilder_compile", startInfo.WorkingDirectory);
        Assert.True(startInfo.CreateNoWindow);
        Assert.True(startInfo.RedirectStandardError);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal(System.Diagnostics.ProcessWindowStyle.Hidden, startInfo.WindowStyle);
    }

    [Fact]
    public void ScriptCompileFlowBuildsDirectoryPlanLikeUdb()
    {
        var plan = ScriptCompileFlow.BuildDirectoryPlan(
            "/maps/project/scripts/library.acs",
            "/tmp/dbuilder_compile",
            "/tmp/dbuilder_compile/tmp123");

        Assert.Equal("/tmp/dbuilder_compile/library.acs", plan.InputCopyPath);
        Assert.Equal("/tmp/dbuilder_compile/tmp123", plan.OutputPath);
        Assert.Equal("/tmp/dbuilder_compile", plan.WorkingDirectory);
        Assert.Equal("/tmp/dbuilder_compile/library.acs", plan.Paths.InputFile);
        Assert.Equal("tmp123", plan.Paths.OutputFile);
        Assert.Equal("/maps/project/scripts/library.acs", plan.Paths.SourceFile);
        Assert.Equal("/tmp/dbuilder_compile", plan.Paths.TempPath);
        Assert.Equal("/maps/project/scripts", plan.Paths.SourcePath);
    }

    [Fact]
    public void ScriptCompileFlowBuildsArchivePlanLikeUdb()
    {
        var plan = ScriptCompileFlow.BuildArchivePlan(
            "acs/library.acs",
            "/tmp/dbuilder_compile",
            "/tmp/dbuilder_compile/tmp123");

        Assert.Equal("/tmp/dbuilder_compile/library.acs", plan.InputCopyPath);
        Assert.Equal("/tmp/dbuilder_compile/tmp123", plan.OutputPath);
        Assert.Equal("/tmp/dbuilder_compile", plan.WorkingDirectory);
        Assert.Equal("/tmp/dbuilder_compile/library.acs", plan.Paths.InputFile);
        Assert.Equal("tmp123", plan.Paths.OutputFile);
        Assert.Equal("/tmp/dbuilder_compile/library.acs", plan.Paths.SourceFile);
        Assert.Equal("/tmp/dbuilder_compile", plan.Paths.TempPath);
        Assert.Equal("acs", plan.Paths.SourcePath);
    }

    [Fact]
    public void ScriptCompileFlowBuildsIncludeCopyPlanLikeUdb()
    {
        var plan = ScriptCompileFlow.BuildIncludeCopyPlan(
            new[] { "libs\\shared.acs", "acs/mapsupport.acs" },
            "/tmp/dbuilder_compile");

        Assert.Collection(
            plan,
            copy =>
            {
                Assert.Equal(Path.Combine("libs", "shared.acs"), copy.IncludeName);
                Assert.Equal(Path.Combine("/tmp/dbuilder_compile", "libs", "shared.acs"), copy.TargetPath);
                Assert.True(copy.ShouldCopy);
            },
            copy =>
            {
                Assert.Equal(Path.Combine("acs", "mapsupport.acs"), copy.IncludeName);
                Assert.Equal(Path.Combine("/tmp/dbuilder_compile", "acs", "mapsupport.acs"), copy.TargetPath);
                Assert.True(copy.ShouldCopy);
            });
    }

    [Fact]
    public void ScriptCompileFlowSkipsExistingIncludeCopyTargetsLikeUdb()
    {
        string existing = Path.Combine("/tmp/dbuilder_compile", "libs", "shared.acs");

        var plan = ScriptCompileFlow.BuildIncludeCopyPlan(
            new[] { "libs/shared.acs" },
            "/tmp/dbuilder_compile",
            new[] { existing });

        var copy = Assert.Single(plan);
        Assert.Equal(Path.Combine("libs", "shared.acs"), copy.IncludeName);
        Assert.Equal(existing, copy.TargetPath);
        Assert.False(copy.ShouldCopy);
    }

    [Fact]
    public void ScriptCompileFlowCopiesIncludesIntoTempDirectoryLikeUdb()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_compile_" + Guid.NewGuid().ToString("N"));
        try
        {
            var plan = ScriptCompileFlow.BuildIncludeCopyPlan(
                new[] { "libs/shared.acs", "missing.acs" },
                dir);

            var copied = ScriptCompileFlow.CopyIncludes(
                plan,
                include => include == Path.Combine("libs", "shared.acs")
                    ? "script 1 OPEN { }"u8.ToArray()
                    : null);

            string target = Path.Combine(dir, "libs", "shared.acs");
            Assert.Equal(target, Assert.Single(copied));
            Assert.Equal("script 1 OPEN { }", File.ReadAllText(target));
            Assert.False(File.Exists(Path.Combine(dir, "missing.acs")));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ScriptCompileFlowDoesNotOverwriteExistingIncludeCopiesLikeUdb()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_compile_" + Guid.NewGuid().ToString("N"));
        string target = Path.Combine(dir, "libs", "shared.acs");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "original");

        try
        {
            var plan = ScriptCompileFlow.BuildIncludeCopyPlan(
                new[] { "libs/shared.acs" },
                dir,
                new[] { target });

            var copied = ScriptCompileFlow.CopyIncludes(
                plan,
                _ => "replacement"u8.ToArray());

            Assert.Empty(copied);
            Assert.Equal("original", File.ReadAllText(target));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ScriptCompileFlowResolvesAccLibraryTargetsLikeUdb()
    {
        var target = ScriptCompileFlow.ResolveFileTarget(
            "/maps/project/scripts/library.acs",
            resultLump: "",
            isAccCompiler: true,
            libraryName: "helpers");

        Assert.True(target.Success);
        Assert.Equal("/maps/project/scripts/helpers.o", target.TargetPath);
    }

    [Fact]
    public void ScriptCompileFlowResolvesResultLumpTargetsLikeUdb()
    {
        var target = ScriptCompileFlow.ResolveArchiveTarget(
            "acs/library.bcs",
            resultLump: "library.o",
            isAccCompiler: false,
            libraryName: "");

        Assert.True(target.Success);
        Assert.Equal(Path.Combine("acs", "library.o"), target.TargetPath);
    }

    [Fact]
    public void ScriptCompileFlowReportsMissingResultLumpLikeUdb()
    {
        var target = ScriptCompileFlow.ResolveFileTarget(
            "/maps/project/scripts/library.bcs",
            resultLump: "",
            isAccCompiler: false,
            libraryName: "",
            scriptConfigurationName: "BCC");

        Assert.False(target.Success);
        Assert.Equal("", target.TargetPath);
        Assert.Equal("Unable to create target file: unable to determine target filename. Make sure \"ResultLump\" property is set in the \"BCC\" script configuration.", target.ErrorMessage);
    }

    [Fact]
    public void ScriptCompileFlowReportsMissingOutputFileLikeUdb()
    {
        var error = ScriptCompileFlow.MissingOutputFileError("/tmp/dbuilder_compile/tmp123");

        Assert.Equal("Output file \"/tmp/dbuilder_compile/tmp123\" doesn't exist.", error.Description);
        Assert.Equal("", error.FileName);
        Assert.Equal(-1, error.LineNumber);
    }

    [Fact]
    public void ScriptCompileFlowRemapsDirectoryTempInputErrorsLikeUdb()
    {
        var error = new ScriptCompilerError("Unknown function", "/tmp/dbuilder_compile/library.acs", 4);

        var remapped = ScriptCompileFlow.RemapDirectoryError(
            error,
            "/tmp/dbuilder_compile/library.acs",
            "/maps/project/scripts/library.acs");

        Assert.Equal("/maps/project/scripts/library.acs", remapped.FileName);
        Assert.Equal("Unknown function", remapped.Description);
        Assert.Equal(4, remapped.LineNumber);
    }

    [Fact]
    public void ScriptCompileFlowRemapsArchiveTempInputErrorsLikeUdb()
    {
        var error = new ScriptCompilerError("Expected semicolon", "/tmp/dbuilder_compile/library.acs", 7);

        var remapped = ScriptCompileFlow.RemapArchiveError(
            error,
            "/tmp/dbuilder_compile/library.acs",
            "acs/library.acs");

        Assert.Equal("acs/library.acs", remapped.FileName);
        Assert.Equal("Expected semicolon", remapped.Description);
        Assert.Equal(7, remapped.LineNumber);
    }

    [Fact]
    public void ScriptCompileFlowRemapsWadLumpTempInputErrorsLikeUdb()
    {
        var error = new ScriptCompilerError("Bad syntax", "/tmp/dbuilder_compile/tmp123", 0);

        var remapped = ScriptCompileFlow.RemapWadLumpError(
            error,
            "/tmp/dbuilder_compile/tmp123",
            "SCRIPTS");

        Assert.Equal("?SCRIPTS", remapped.FileName);
        Assert.Equal("Bad syntax", remapped.Description);
        Assert.Equal(0, remapped.LineNumber);
    }

    [Fact]
    public void ScriptCompileFlowKeepsNonTempInputErrorsLikeUdb()
    {
        var error = new ScriptCompilerError("Bad include", "/maps/project/libs/common.acs", 3);

        var remapped = ScriptCompileFlow.RemapDirectoryError(
            error,
            "/tmp/dbuilder_compile/library.acs",
            "/maps/project/scripts/library.acs");

        Assert.Equal(error, remapped);
    }

    [Fact]
    public void AcsCompilePreflightSkipsEmptyMapScriptsLumpLikeUdb()
    {
        var result = AcsCompilePreflight.Analyze("", "SCRIPTS", sourceIsMapScriptsLump: true);

        Assert.False(result.ShouldCompile);
        Assert.Null(result.Error);
        Assert.Empty(result.Includes);
        Assert.Equal("", result.LibraryName);
    }

    [Fact]
    public void AcsCompilePreflightRequiresExternalAcsLibrariesLikeUdb()
    {
        var result = AcsCompilePreflight.Analyze(
            "script 1 OPEN { }\n",
            "/maps/project/scripts/main.acs",
            sourceIsMapScriptsLump: false);

        Assert.True(result.ShouldCompile);
        var error = Assert.IsType<ScriptCompilerError>(result.Error);
        Assert.Equal("External ACS files can only be compiled as libraries.", error.Description);
        Assert.Equal("/maps/project/scripts/main.acs", error.FileName);
        Assert.Equal(-1, error.LineNumber);
    }

    [Fact]
    public void AcsCompilePreflightCollectsIncludesAndSkipsCompilerFilesLikeUdb()
    {
        const string text = """
            #library "helpers"
            #include "zcommon.acs"
            #include "libs\shared.acs"
            #import "acs/mapsupport.acs"
            """;

        var result = AcsCompilePreflight.Analyze(
            text,
            "/maps/project/scripts/helpers.acs",
            sourceIsMapScriptsLump: false,
            compilerFiles: new[] { "zcommon.acs" });

        Assert.True(result.ShouldCompile);
        Assert.Null(result.Error);
        Assert.Equal("helpers", result.LibraryName);
        Assert.DoesNotContain("zcommon.acs", result.Includes);
        Assert.Contains(Path.Combine("libs", "shared.acs"), result.Includes);
        Assert.Contains(Path.Combine("acs", "mapsupport.acs"), result.Includes);
    }

    [Fact]
    public void AcsCompilePreflightReportsDuplicateIncludesLikeUdb()
    {
        const string text = """
            #library "helpers"
            #include "libs/shared.acs"
            #include "libs/shared.acs"
            """;

        var result = AcsCompilePreflight.Analyze(
            text,
            "/maps/project/scripts/helpers.acs",
            sourceIsMapScriptsLump: false);

        string include = Path.Combine("libs", "shared.acs");
        var error = Assert.IsType<ScriptCompilerError>(result.Error);
        Assert.Equal("Already parsed \"" + include + "\". Check your #include directives.", error.Description);
        Assert.Equal("/maps/project/scripts/helpers.acs", error.FileName);
        Assert.Equal(2, error.LineNumber);
    }

    [Fact]
    public void AcsCompilePreflightRequiresQuotedIncludesLikeUdb()
    {
        const string text = """
            #library "helpers"
            #import libs/shared.acs
            """;

        var result = AcsCompilePreflight.Analyze(
            text,
            "/maps/project/scripts/helpers.acs",
            sourceIsMapScriptsLump: false);

        var error = Assert.IsType<ScriptCompilerError>(result.Error);
        Assert.Equal("#import filename should be quoted.", error.Description);
        Assert.Equal("/maps/project/scripts/helpers.acs", error.FileName);
        Assert.Equal(1, error.LineNumber);
    }

    [Fact]
    public void AcsCompilePreflightReportsMissingIncludesLikeUdb()
    {
        const string text = """
            #library "helpers"
            #include "libs/missing.acs"
            """;

        var result = AcsCompilePreflight.Analyze(
            text,
            "/maps/project/scripts/helpers.acs",
            sourceIsMapScriptsLump: false,
            includeExists: include => include != Path.Combine("libs", "missing.acs"));

        var error = Assert.IsType<ScriptCompilerError>(result.Error);
        Assert.Equal("Unable to find include file \"" + Path.Combine("libs", "missing.acs") + "\".", error.Description);
        Assert.Equal("/maps/project/scripts/helpers.acs", error.FileName);
        Assert.Equal(1, error.LineNumber);
    }

    [Fact]
    public void AcsCompilePreflightSkipsCompilerIncludeResolutionLikeUdb()
    {
        const string text = """
            #library "helpers"
            #include "zcommon.acs"
            """;

        var result = AcsCompilePreflight.Analyze(
            text,
            "/maps/project/scripts/helpers.acs",
            sourceIsMapScriptsLump: false,
            compilerFiles: new[] { "zcommon.acs" },
            includeExists: _ => false);

        Assert.True(result.ShouldCompile);
        Assert.Null(result.Error);
        Assert.Empty(result.Includes);
        Assert.Equal("helpers", result.LibraryName);
    }

    [Fact]
    public void ScriptCompilerErrorsParseAccErrorLines()
    {
        var errors = ScriptCompilerErrors.ParseAcc(
            new[] { "/tmp/dbuilder_compile/scripts.acs:12: Unknown function" },
            "/tmp/dbuilder_compile",
            "/maps/project");

        var error = Assert.Single(errors);
        Assert.Equal("Unknown function", error.Description);
        Assert.Equal(Path.Combine("/maps/project", "scripts.acs"), error.FileName);
        Assert.Equal(11, error.LineNumber);
    }

    [Fact]
    public void ScriptCompilerErrorsResolveAccIncludeErrorsLikeUdb()
    {
        var errors = ScriptCompilerErrors.ParseAcc(
            new[] { "libs/common.acs:12: Unknown function" },
            "/tmp/dbuilder_compile",
            "/maps/project",
            include => include == Path.Combine("libs", "common.acs")
                ? Path.Combine("/resources/pk3", include)
                : null);

        var error = Assert.Single(errors);
        Assert.Equal("Unknown function", error.Description);
        Assert.Equal(Path.Combine("/resources/pk3", "libs", "common.acs"), error.FileName);
        Assert.Equal(11, error.LineNumber);
    }

    [Fact]
    public void ScriptCompilerErrorsParseAccWindowsPathErrorLines()
    {
        var errors = ScriptCompilerErrors.ParseAcc(
            new[] { @"C:\maps\scripts.acs:12: Unknown function" },
            @"C:\tmp\dbuilder_compile",
            @"C:\maps");

        var error = Assert.Single(errors);
        Assert.Equal("Unknown function", error.Description);
        Assert.Equal(@"C:\maps\scripts.acs", error.FileName);
        Assert.Equal(11, error.LineNumber);
    }

    [Fact]
    public void ScriptCompilerErrorsStripWindowsTempPathFromAccErrors()
    {
        var errors = ScriptCompilerErrors.ParseAcc(
            new[] { @"C:\tmp\dbuilder_compile\scripts.acs:12: Unknown function" },
            @"C:\tmp\dbuilder_compile",
            @"C:\maps");

        var error = Assert.Single(errors);
        Assert.Equal("Unknown function", error.Description);
        Assert.Equal(Path.Combine(@"C:\maps", "scripts.acs"), error.FileName);
        Assert.Equal(11, error.LineNumber);
    }

    [Fact]
    public void ScriptCompilerErrorsParseBccErrorLines()
    {
        var errors = ScriptCompilerErrors.ParseBcc(
            new[] { "libs/common.acs:8:4: Expected semicolon" },
            "/tmp/dbuilder_compile",
            "/maps/project");

        var error = Assert.Single(errors);
        Assert.Equal("Expected semicolon", error.Description);
        Assert.Equal(Path.Combine("/maps/project", "libs/common.acs"), error.FileName);
        Assert.Equal(7, error.LineNumber);
    }

    [Fact]
    public void ScriptCompilerErrorsResolveBccIncludeErrorsLikeUdb()
    {
        var errors = ScriptCompilerErrors.ParseBcc(
            new[] { "libs/common.acs:8:4: Expected semicolon" },
            "/tmp/dbuilder_compile",
            "/maps/project",
            include => include == Path.Combine("libs", "common.acs")
                ? Path.Combine("/resources/pk3", include)
                : null);

        var error = Assert.Single(errors);
        Assert.Equal("Expected semicolon", error.Description);
        Assert.Equal(Path.Combine("/resources/pk3", "libs", "common.acs"), error.FileName);
        Assert.Equal(7, error.LineNumber);
    }

    [Fact]
    public void ScriptCompilerErrorsParseZtBccStderrLines()
    {
        var errors = ScriptCompilerErrors.ParseZtBcc(
            new[] { "scripts/main.acs:10:2: Unknown identifier" },
            "/tmp/dbuilder_compile",
            "/maps/project");

        var error = Assert.Single(errors);
        Assert.Equal("Unknown identifier", error.Description);
        Assert.Equal(Path.Combine("/maps/project", "scripts/main.acs"), error.FileName);
        Assert.Equal(9, error.LineNumber);
    }

    [Fact]
    public void ScriptCompilerErrorsFallbackForUnformattedZtBccStderr()
    {
        var errors = ScriptCompilerErrors.ParseZtBcc(
            new[] { "fatal: compiler crashed", "details unavailable" },
            "/tmp/dbuilder_compile",
            "/maps/project");

        var error = Assert.Single(errors);
        Assert.Equal($"fatal: compiler crashed{Environment.NewLine}details unavailable", error.Description);
        Assert.Equal("", error.FileName);
        Assert.Equal(-1, error.LineNumber);
    }
}
