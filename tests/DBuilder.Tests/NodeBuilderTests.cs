// ABOUTME: Tests for NodeBuilder - external node-builder invocation (arg substitution, failure handling, run+readback).
// ABOUTME: The full pipeline test uses /bin/cp as a stand-in builder on unix; it is skipped where cp is unavailable.

using System.IO;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class NodeBuilderTests
{
    private static byte[] SampleWadBytes()
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
}
