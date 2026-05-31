// ABOUTME: Validates WAD node data before VisplaneExplorer analysis starts.
// ABOUTME: Mirrors UDB's VPO preflight rejection for missing, empty, and ZDBSP NODES lumps.

using System.Text;

namespace DBuilder.IO;

public enum VisplaneExplorerPreflightStatus
{
    Ok,
    MissingNodes,
    EmptyNodes,
    UnsupportedZdbspNodes,
}

public sealed record VisplaneExplorerPreflightResult(VisplaneExplorerPreflightStatus Status, string Message)
{
    public bool Success => Status == VisplaneExplorerPreflightStatus.Ok;
}

public static class VisplaneExplorerPreflight
{
    public static VisplaneExplorerPreflightResult Check(WAD wad)
    {
        ArgumentNullException.ThrowIfNull(wad);

        Lump? nodes = wad.FindLump("NODES");
        if (nodes == null) return MissingNodes();

        byte[] bytes = nodes.Stream.ReadAllBytes();
        return CheckNodesLump(bytes);
    }

    public static VisplaneExplorerPreflightResult CheckNodesLump(byte[]? nodesData)
    {
        if (nodesData == null) return MissingNodes();
        if (nodesData.Length == 0) return new VisplaneExplorerPreflightResult(
            VisplaneExplorerPreflightStatus.EmptyNodes,
            "NODES lump is empty");

        if (nodesData.Length >= 4)
        {
            string header = Encoding.ASCII.GetString(nodesData, 0, 4);
            if (header is "ZNOD" or "XNOD")
                return new VisplaneExplorerPreflightResult(
                    VisplaneExplorerPreflightStatus.UnsupportedZdbspNodes,
                    "ZDBSP nodes detected. This format is not supported by the Visplane Explorer Mode");
        }

        return new VisplaneExplorerPreflightResult(VisplaneExplorerPreflightStatus.Ok, "");
    }

    private static VisplaneExplorerPreflightResult MissingNodes()
        => new(VisplaneExplorerPreflightStatus.MissingNodes, "NODES lump not found");
}
