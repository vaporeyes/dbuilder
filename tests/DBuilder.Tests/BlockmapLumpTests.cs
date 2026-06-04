// ABOUTME: Tests Doom BLOCKMAP lump parsing for BlockmapExplorer parity data.
// ABOUTME: Covers shared lists, malformed lump diagnostics, and explorer query helpers.

using System;
using System.Collections.Generic;
using DBuilder.Geometry;
using DBuilder.IO;

namespace DBuilder.Tests;

public class BlockmapLumpTests
{
    [Fact]
    public void ParsesHeaderOffsetsAndSharedBlockLists()
    {
        byte[] data = BuildBlockmap(
            originX: -128,
            originY: 64,
            columns: 2,
            rows: 2,
            offsetsInShorts: new ushort[] { 8, 12, 8, 14 },
            lists: new[]
            {
                List(0, 1, 1, ushort.MaxValue),
                List(5, ushort.MaxValue),
                List(7, 8, ushort.MaxValue)
            });

        BlockmapLumpData blockmap = BlockmapLump.Parse(data);

        Assert.Equal(BlockmapLumpStatus.Ok, blockmap.Status);
        Assert.True(blockmap.IsUsable);
        Assert.Equal(-128, blockmap.OriginX);
        Assert.Equal(64, blockmap.OriginY);
        Assert.Equal(2, blockmap.Columns);
        Assert.Equal(2, blockmap.Rows);
        Assert.Equal(3, blockmap.Blocks.Count);
        Assert.Equal(new[] { 0, 1 }, blockmap.GetLinesInBlock(0, 0));
        Assert.Equal(new[] { 0, 1 }, blockmap.GetLinesInBlock(0, 1));
        Assert.Equal(new[] { 5 }, blockmap.GetLinesInBlock(1, 0));
        Assert.Equal(new[] { 7, 8 }, blockmap.GetLinesInBlock(1, 1));
        Assert.False(blockmap.Blocks[16].IsSublist);
        Assert.False(blockmap.Blocks[24].IsSublist);
        Assert.False(blockmap.Blocks[28].IsSublist);
        Assert.Equal(new[] { (0, 0), (0, 1) }, blockmap.GetSharedBlocks(0, 0));
        Assert.Equal(new[] { (0, 0) }, blockmap.GetHighlightedBlocks(0, 0, includeSharedBlocks: false));
        Assert.Equal(new[] { (0, 0), (0, 1) }, blockmap.GetHighlightedBlocks(0, 0, includeSharedBlocks: true));
        Assert.Empty(blockmap.GetHighlightedBlocks(5, 5, includeSharedBlocks: true));
        Assert.Equal(4, blockmap.CountLinesNotInBlocks(9));
    }

    [Fact]
    public void LocatesCellsByMapPosition()
    {
        byte[] data = BuildBlockmap(
            100,
            200,
            2,
            1,
            new ushort[] { 6, 6 },
            new[] { List(ushort.MaxValue) });

        BlockmapLumpData blockmap = BlockmapLump.Parse(data);

        Assert.Equal((0, 0), blockmap.GetColumnAndRowByPosition(new Vector2D(100, 200)));
        Assert.Equal((1, 0), blockmap.GetColumnAndRowByPosition(new Vector2D(229, 250)));
        Assert.Equal((-1, -1), blockmap.GetColumnAndRowByPosition(new Vector2D(99, 250)));
        Assert.Equal((-1, -1), blockmap.GetColumnAndRowByPosition(new Vector2D(356, 250)));
        Assert.Equal((-1, -1), blockmap.GetColumnAndRowByPosition(new Vector2D(120, 328)));
    }

    [Fact]
    public void ReportsOffsetPastLumpButKeepsOtherBlocks()
    {
        byte[] data = BuildBlockmap(
            0,
            0,
            2,
            1,
            new ushort[] { 6, 99 },
            new[] { List(3, ushort.MaxValue) });

        BlockmapLumpData blockmap = BlockmapLump.Parse(data);

        Assert.Equal(BlockmapLumpStatus.BlockListOffsetOutOfRange, blockmap.Status);
        Assert.Equal(new[] { 3 }, blockmap.GetLinesInBlock(0, 0));
        Assert.Empty(blockmap.GetLinesInBlock(1, 0));
        var diagnostic = Assert.Single(blockmap.Diagnostics);
        Assert.Equal(1, diagnostic.Column);
        Assert.Equal(0, diagnostic.Row);
        Assert.Equal(198, diagnostic.Offset);
    }

    [Fact]
    public void ReportsBlockListWithoutTerminator()
    {
        byte[] data = BuildBlockmap(
            0,
            0,
            1,
            1,
            new ushort[] { 5 },
            new[] { List(2, 3) });

        BlockmapLumpData blockmap = BlockmapLump.Parse(data);

        Assert.Equal(BlockmapLumpStatus.BlockListReadPastEnd, blockmap.Status);
        Assert.False(blockmap.IsUsable);
        Assert.Empty(blockmap.GetLinesInBlock(0, 0));
        var diagnostic = Assert.Single(blockmap.Diagnostics);
        Assert.Equal(0, diagnostic.Column);
        Assert.Equal(0, diagnostic.Row);
        Assert.Equal(10, diagnostic.Offset);
    }

    [Fact]
    public void RejectsEmptyShortAndTruncatedOffsetTable()
    {
        Assert.Equal(BlockmapLumpStatus.Empty, BlockmapLump.Parse(Array.Empty<byte>()).Status);
        Assert.Equal(BlockmapLumpStatus.TooShortHeader, BlockmapLump.Parse(new byte[4]).Status);

        byte[] truncated = new byte[9];
        WriteInt16(truncated, 4, 1);
        WriteInt16(truncated, 6, 1);

        Assert.Equal(BlockmapLumpStatus.OffsetTableTruncated, BlockmapLump.Parse(truncated).Status);
    }

    [Fact]
    public void ExposesQuestionableOffsetsAndMissingLineCount()
    {
        byte[] data = BuildBlockmap(
            0,
            0,
            2,
            1,
            new ushort[] { 2, 6 },
            new[] { List(0, 2, ushort.MaxValue) });

        BlockmapLumpData blockmap = BlockmapLump.Parse(data);

        Assert.Equal(new[] { (0, 0) }, blockmap.GetQuestionableBlocks());
        Assert.Equal(1, blockmap.GetQuestionableOffsetCount());
    }

    [Fact]
    public void ExplorerModelFormatsSummariesRowsAndCounts()
    {
        byte[] data = BuildBlockmap(
            originX: -128,
            originY: 64,
            columns: 2,
            rows: 2,
            offsetsInShorts: new ushort[] { 8, 12, 8, 14 },
            lists: new[]
            {
                List(0, 1, 1, ushort.MaxValue),
                List(5, ushort.MaxValue),
                List(7, 8, ushort.MaxValue)
            });

        BlockmapLumpData blockmap = BlockmapLump.Parse(data);
        IReadOnlyList<BlockmapExplorerRow> rows = BlockmapExplorerModel.BuildRows(blockmap);

        Assert.Equal("BLOCKMAP: Ok (34 bytes)", BlockmapExplorerModel.FormatStatus(blockmap));
        Assert.Equal(
            "Origin (-128, 64), 2 columns x 2 rows, 4 total blocks, 3 unique block lists, offset list ends at byte 16, 0 questionable offsets, 4 linedefs not in blocks.",
            BlockmapExplorerModel.FormatInfo(blockmap, linedefCount: 9));
        Assert.Equal("Showing 1 of 1 block row. Double-click a row to center that block.", BlockmapExplorerModel.FormatHeader(1, 1));
        Assert.Equal("Showing 4 of 4 block rows. Double-click a row to center that block.", BlockmapExplorerModel.FormatHeader(4, rows.Count));
        Assert.Equal("No block rows to display.", BlockmapExplorerModel.FormatHeader(0, 0));
        Assert.Equal("1 more diagnostic.", BlockmapExplorerModel.FormatHiddenDiagnostics(1));
        Assert.Equal("2 more diagnostics.", BlockmapExplorerModel.FormatHiddenDiagnostics(2));
        Assert.Equal("(0, 0) offset 16: 2 lines, shared by 2 blocks", BlockmapExplorerModel.FormatRow(rows[0]));
        Assert.Equal("(1, 0) offset 24: 1 line, shared by 1 block", BlockmapExplorerModel.FormatRow(rows[1]));
    }

    [Fact]
    public void ExplorerModeDescriptorMatchesUdbEditModeMetadata()
    {
        BlockmapExplorerModeDescriptor mode = BlockmapExplorerModel.ModeDescriptor;

        Assert.Equal("Blockmap Explorer Mode", mode.DisplayName);
        Assert.Equal("blockmapexplorermode", mode.SwitchAction);
        Assert.Equal("blockmap.png", mode.ButtonImage);
        Assert.Equal(int.MinValue + 504, mode.ButtonOrder);
        Assert.Equal("000_editing", mode.ButtonGroup);
        Assert.Equal([BlockmapExplorerModel.DoomMapSetIo, BlockmapExplorerModel.HexenMapSetIo], mode.SupportedMapFormats);
        Assert.True(mode.UseByDefault);
        Assert.True(mode.Volatile);
        Assert.Equal("/gzdb/features/classic_modes/mode_blockmapexplorer.html", mode.HelpPath);
    }

    private static byte[] BuildBlockmap(int originX, int originY, short columns, short rows, ushort[] offsetsInShorts, byte[][] lists)
    {
        int size = BlockmapLump.HeaderSize + offsetsInShorts.Length * sizeof(ushort);
        foreach (byte[] list in lists) size += list.Length;

        var data = new byte[size];
        WriteInt16(data, 0, originX);
        WriteInt16(data, 2, originY);
        WriteInt16(data, 4, columns);
        WriteInt16(data, 6, rows);

        for (int i = 0; i < offsetsInShorts.Length; i++)
            WriteUInt16(data, BlockmapLump.HeaderSize + i * sizeof(ushort), offsetsInShorts[i]);

        int position = BlockmapLump.HeaderSize + offsetsInShorts.Length * sizeof(ushort);
        foreach (byte[] list in lists)
        {
            Buffer.BlockCopy(list, 0, data, position, list.Length);
            position += list.Length;
        }

        return data;
    }

    private static byte[] List(params ushort[] values)
    {
        var data = new byte[values.Length * sizeof(ushort)];
        for (int i = 0; i < values.Length; i++) WriteUInt16(data, i * sizeof(ushort), values[i]);
        return data;
    }

    private static void WriteInt16(byte[] data, int offset, int value) => BitConverter.GetBytes((short)value).CopyTo(data, offset);
    private static void WriteUInt16(byte[] data, int offset, ushort value) => BitConverter.GetBytes(value).CopyTo(data, offset);
}
