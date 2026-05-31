// ABOUTME: Tests for NodeBuilder - external node-builder invocation (arg substitution, failure handling, run+readback).
// ABOUTME: The full pipeline test uses /bin/cp as a stand-in builder on unix; it is skipped where cp is unavailable.

using System.IO;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class NodeBuilderTests
{
    private const string NodeBuildConfig = @"
maplumpnames
{
    ~MAP { required = true; blindcopy = true; }
    THINGS { required = true; }
    LINEDEFS { required = true; }
    SIDEDEFS { required = true; }
    VERTEXES { required = true; }
    SECTORS { required = true; }
    NODES { required = true; nodebuild = true; allowempty = false; }
}";

    private static byte[] SampleWadBytes(bool includeStaleNodes = false)
    {
        var map = new MapSet();
        var s = map.AddSector();
        var v = new[]
        {
            map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(0, 64)),
            map.AddVertex(new Vector2D(64, 64)), map.AddVertex(new Vector2D(64, 0)),
        };
        for (int i = 0; i < 4; i++) map.AddSidedef(map.AddLinedef(v[i], v[(i + 1) % 4]), true, s);
        map.BuildIndexes();

        var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            DoomMapWriter.WriteMap(map, wad, "MAP01", 0);
            if (includeStaleNodes)
                wad.Insert("NODES", wad.Lumps.Count, 0, false);
            wad.WriteHeaders();
            return ms.ToArray();
        }
    }

    [Fact]
    public void BuildArgumentsSubstitutesPlaceholders()
    {
        Assert.Equal("-o \"out.wad\" \"in.wad\"",
            NodeBuilder.BuildArguments("-o \"%FO\" \"%FI\"", "in.wad", "out.wad"));
        Assert.Equal("\"in.wad\"", NodeBuilder.BuildArguments("\"%FI\"", "in.wad", "out.wad"));
    }

    [Fact]
    public void HasSeparateOutputDetectsFOPlaceholder()
    {
        Assert.True(NodeBuilder.HasSeparateOutput("-o \"%FO\" \"%FI\""));
        Assert.False(NodeBuilder.HasSeparateOutput("\"%FI\""));
    }

    [Fact]
    public void CreateStartInfoMatchesUdbNodesCompilerSettings()
    {
        var startInfo = NodeBuilder.CreateStartInfo(
            new NodebuilderConfig("/tools/zdbsp", "-o \"%FO\" \"%FI\""),
            "/tmp/input.wad",
            "/tmp/output.wad",
            "/tmp/dbuilder_nodes");

        Assert.Equal("/tools/zdbsp", startInfo.FileName);
        Assert.Equal("-o \"/tmp/output.wad\" \"/tmp/input.wad\"", startInfo.Arguments);
        Assert.Equal("/tmp/dbuilder_nodes", startInfo.WorkingDirectory);
        Assert.True(startInfo.CreateNoWindow);
        Assert.False(startInfo.ErrorDialog);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal(System.Diagnostics.ProcessWindowStyle.Hidden, startInfo.WindowStyle);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
    }

    [Fact]
    public void BuildsRequiredFileCopyPlanLikeUdbCompiler()
    {
        string root = Path.Combine(Path.GetTempPath(), "dbuilder_nodebuilder_files_" + Guid.NewGuid().ToString("N"));
        string working = Path.Combine(root, "work");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "zdbsp.cfg"), "support");
            var config = new NodebuilderConfig(
                "/tools/zdbsp",
                "%FI",
                root,
                new[] { "zdbsp.cfg", "missing.dat" });

            var plan = NodeBuilder.BuildRequiredFileCopyPlan(config, working);

            Assert.Collection(
                plan,
                copy =>
                {
                    Assert.Equal("zdbsp.cfg", copy.Name);
                    Assert.Equal(Path.Combine(root, "zdbsp.cfg"), copy.SourcePath);
                    Assert.Equal(Path.Combine(working, "zdbsp.cfg"), copy.TargetPath);
                    Assert.True(copy.SourceExists);
                },
                copy =>
                {
                    Assert.Equal("missing.dat", copy.Name);
                    Assert.Equal(Path.Combine(root, "missing.dat"), copy.SourcePath);
                    Assert.Equal(Path.Combine(working, "missing.dat"), copy.TargetPath);
                    Assert.False(copy.SourceExists);
                });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CopiesRequiredFilesAndReportsMissingLikeUdbCompiler()
    {
        string root = Path.Combine(Path.GetTempPath(), "dbuilder_nodebuilder_copy_" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source");
        string working = Path.Combine(root, "work");
        Directory.CreateDirectory(source);
        try
        {
            File.WriteAllText(Path.Combine(source, "zdbsp.cfg"), "support");
            var config = new NodebuilderConfig(
                "/tools/zdbsp",
                "%FI",
                source,
                new[] { "zdbsp.cfg", "missing.dat" });

            var missing = NodeBuilder.CopyRequiredFiles(NodeBuilder.BuildRequiredFileCopyPlan(config, working));

            Assert.Equal("support", File.ReadAllText(Path.Combine(working, "zdbsp.cfg")));
            var missingCopy = Assert.Single(missing);
            Assert.Equal("missing.dat", missingCopy.Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AnalyzeProcessResultFailsWhenOutputContainsErrorLikeUdb()
    {
        var normalOutput = NodeBuilder.AnalyzeProcessResult(0, "build error: bad subsector", "");
        var errorOutput = NodeBuilder.AnalyzeProcessResult(0, "", "fatal ERROR: bad blockmap");

        Assert.False(normalOutput.Success);
        Assert.Equal("build error: bad subsector", normalOutput.Message);
        Assert.False(errorOutput.Success);
        Assert.Equal("fatal ERROR: bad blockmap", errorOutput.Message);
    }

    [Fact]
    public void AnalyzeProcessResultStripsBackspaceCharactersLikeUdb()
    {
        var result = NodeBuilder.AnalyzeProcessResult(0, "building\b nodes", "");

        Assert.True(result.Success);
        Assert.Equal("building nodes", result.Message);
    }

    [Fact]
    public void AnalyzeProcessResultReportsExitCodeWhenNoErrorTextExistsLikeUdb()
    {
        var result = NodeBuilder.AnalyzeProcessResult(2, "finished", "details");

        Assert.False(result.Success);
        Assert.Equal("Node builder exited with code 2.", result.Message);
    }

    [Fact]
    public void MissingExecutableFailsGracefully()
    {
        var result = NodeBuilder.Build(SampleWadBytes(),
            new NodebuilderConfig("/nonexistent/zdbsp_xyz", "-o \"%FO\" \"%FI\""));
        Assert.False(result.Success);
        Assert.Null(result.Output);
        Assert.Contains("not found", result.Message);
    }

    [Fact]
    public void RunsExternalToolAndReadsOutputBack()
    {
        const string cp = "/bin/cp";
        if (!File.Exists(cp)) return; // platform without /bin/cp; pipeline covered elsewhere

        var input = SampleWadBytes();
        // cp copies input -> output, standing in for a builder that writes a separate output WAD.
        var result = NodeBuilder.Build(input, new NodebuilderConfig(cp, "\"%FI\" \"%FO\""));

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Output);
        Assert.Equal(input.Length, result.Output!.Length);

        // The returned bytes are a valid WAD with the map intact.
        using var wad = new WAD(new MemoryStream(result.Output), openreadonly: true);
        var maps = WadMaps.Find(wad);
        Assert.Single(maps);
        Assert.Equal("MAP01", maps[0].Name);
    }

    [Fact]
    public void ValidatedBuildRejectsOutputWithoutRequiredNodeLumps()
    {
        const string cp = "/bin/cp";
        if (!File.Exists(cp)) return; // platform without /bin/cp; pipeline covered elsewhere

        var config = GameConfiguration.FromText(NodeBuildConfig);
        var input = SampleWadBytes(includeStaleNodes: true);

        var result = NodeBuilder.Build(
            input,
            new NodebuilderConfig(cp, "\"%FI\" \"%FO\""),
            mapMarker: "MAP01",
            config: config);

        Assert.False(result.Success);
        Assert.Null(result.Output);
        Assert.Contains("expected data structures", result.Message);
    }
}
