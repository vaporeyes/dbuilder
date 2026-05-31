// ABOUTME: Tests VisplaneExplorer WAD preflight checks before VPO analysis.
// ABOUTME: Covers UDB-compatible NODES presence, empty-lump, and ZDBSP-header rejection.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class VisplaneExplorerPreflightTests
{
    [Fact]
    public void CheckReportsMissingNodesLump()
    {
        using var ms = new MemoryStream();
        using var wad = new WAD(ms);
        wad.Insert("MAP01", 0, 0);

        VisplaneExplorerPreflightResult result = VisplaneExplorerPreflight.Check(wad);

        Assert.False(result.Success);
        Assert.Equal(VisplaneExplorerPreflightStatus.MissingNodes, result.Status);
        Assert.Equal("NODES lump not found", result.Message);
    }

    [Fact]
    public void CheckReportsEmptyNodesLump()
    {
        using var ms = new MemoryStream();
        using var wad = new WAD(ms);
        wad.Insert("NODES", 0, 0);

        VisplaneExplorerPreflightResult result = VisplaneExplorerPreflight.Check(wad);

        Assert.False(result.Success);
        Assert.Equal(VisplaneExplorerPreflightStatus.EmptyNodes, result.Status);
        Assert.Equal("NODES lump is empty", result.Message);
    }

    [Theory]
    [InlineData("ZNOD")]
    [InlineData("XNOD")]
    public void CheckRejectsZdbspNodesHeaders(string header)
    {
        VisplaneExplorerPreflightResult result = VisplaneExplorerPreflight.CheckNodesLump(Encoding.ASCII.GetBytes(header));

        Assert.False(result.Success);
        Assert.Equal(VisplaneExplorerPreflightStatus.UnsupportedZdbspNodes, result.Status);
        Assert.Equal("ZDBSP nodes detected. This format is not supported by the Visplane Explorer Mode", result.Message);
    }

    [Fact]
    public void CheckAllowsClassicNodesData()
    {
        byte[] nodes = new byte[28];
        nodes[0] = 1;

        VisplaneExplorerPreflightResult result = VisplaneExplorerPreflight.CheckNodesLump(nodes);

        Assert.True(result.Success);
        Assert.Equal(VisplaneExplorerPreflightStatus.Ok, result.Status);
        Assert.Equal("", result.Message);
    }
}
