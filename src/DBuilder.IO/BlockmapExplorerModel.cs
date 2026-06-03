// ABOUTME: Builds Blockmap Explorer rows and UDB-style summary text from parsed BLOCKMAP lump data.
// ABOUTME: Keeps blockmap explorer formatting testable outside the Avalonia window.

namespace DBuilder.IO;

public sealed record BlockmapExplorerRow(
    int Column,
    int Row,
    int Offset,
    int LineCount,
    bool IsSublist,
    int SharedCount,
    bool Questionable);

public static class BlockmapExplorerModel
{
    public static IReadOnlyList<BlockmapExplorerRow> BuildRows(BlockmapLumpData blockmap, bool questionableOnly = false)
    {
        var rows = new List<BlockmapExplorerRow>();
        for (int row = 0; row < blockmap.Rows; row++)
        {
            for (int column = 0; column < blockmap.Columns; column++)
            {
                int offset = blockmap.GetBlockListOffset(column, row);
                bool questionable = IsQuestionableOffset(blockmap, offset);
                if (questionableOnly && !questionable) continue;
                IReadOnlyList<int> lines = blockmap.GetLinesInBlock(column, row);
                bool isSublist = offset >= 0
                    && blockmap.Blocks.TryGetValue(offset, out BlockmapBlockList? block)
                    && block.IsSublist;
                int sharedCount = blockmap.GetSharedBlocks(column, row).Count;
                rows.Add(new BlockmapExplorerRow(column, row, offset, lines.Count, isSublist, sharedCount, questionable));
            }
        }

        return rows;
    }

    public static string FormatStatus(BlockmapLumpData blockmap)
        => $"BLOCKMAP: {blockmap.Status} ({CountLabel(blockmap.LumpSize, "byte")})";

    public static string FormatInfo(BlockmapLumpData blockmap, int linedefCount)
    {
        int totalBlocks = blockmap.Columns * blockmap.Rows;
        int offsetListEnd = BlockmapLump.HeaderSize + totalBlocks * sizeof(ushort);
        return $"Origin ({blockmap.OriginX}, {blockmap.OriginY}), {blockmap.Columns} columns x {blockmap.Rows} rows, " +
            $"{CountLabel(totalBlocks, "total block")}, {CountLabel(blockmap.Blocks.Count, "unique block list")}, " +
            $"offset list ends at byte {offsetListEnd}, {CountLabel(blockmap.GetQuestionableOffsetCount(), "questionable offset")}, " +
            $"{CountLabel(blockmap.CountLinesNotInBlocks(linedefCount), "linedef")} not in blocks.";
    }

    public static string FormatHeader(int shown, int rowCount)
        => rowCount == 0
            ? "No block rows to display."
            : $"Showing {shown} of {CountLabel(rowCount, "block row")}. Double-click a row to center that block.";

    public static string FormatHiddenDiagnostics(int hiddenCount)
        => $"{CountLabel(hiddenCount, "more diagnostic")}.";

    public static string FormatRow(BlockmapExplorerRow row)
    {
        string questionable = row.Questionable ? " questionable" : "";
        string sublist = row.IsSublist ? " sublist" : "";
        return $"({row.Column}, {row.Row}) offset {row.Offset}: {CountLabel(row.LineCount, "line")}, " +
            $"shared by {CountLabel(row.SharedCount, "block")}{sublist}{questionable}";
    }

    private static bool IsQuestionableOffset(BlockmapLumpData blockmap, int offset)
        => offset >= 0 && offset < BlockmapLump.HeaderSize + blockmap.Columns * blockmap.Rows * sizeof(ushort);

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}
