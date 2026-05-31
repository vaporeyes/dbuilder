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
}
