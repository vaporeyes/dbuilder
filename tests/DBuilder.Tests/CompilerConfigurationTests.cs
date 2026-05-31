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
