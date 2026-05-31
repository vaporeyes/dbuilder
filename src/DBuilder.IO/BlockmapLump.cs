// ABOUTME: Parses Doom BLOCKMAP lump data for the BlockmapExplorer parity model.
// ABOUTME: Preserves raw block-list offsets, shared lists, and diagnostics for malformed lumps.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Buffers.Binary;
using DBuilder.Geometry;

namespace DBuilder.IO;

public enum BlockmapLumpStatus
{
    Ok,
    Empty,
    TooShortHeader,
    InvalidDimensions,
    OffsetTableTruncated,
    BlockListOffsetOutOfRange,
    BlockListReadPastEnd
}

public readonly record struct BlockmapLumpDiagnostic(
    BlockmapLumpStatus Status,
    int? Column,
    int? Row,
    int? Offset,
    string Message);

public sealed record BlockmapBlockList(int Offset, IReadOnlyList<int> LinedefIndexes, bool IsSublist);

public sealed class BlockmapLumpData
{
    private readonly int[,] blockPointers;
    private readonly IReadOnlyDictionary<int, BlockmapBlockList> blocks;
    private readonly IReadOnlyList<BlockmapLumpDiagnostic> diagnostics;

    public BlockmapLumpData(
        BlockmapLumpStatus status,
        int originX,
        int originY,
        int columns,
        int rows,
        int[,] blockPointers,
        IReadOnlyDictionary<int, BlockmapBlockList> blocks,
        int lumpSize,
        IReadOnlyList<BlockmapLumpDiagnostic> diagnostics)
    {
        Status = status;
        OriginX = originX;
        OriginY = originY;
        Columns = columns;
        Rows = rows;
        this.blockPointers = blockPointers;
        this.blocks = blocks;
        LumpSize = lumpSize;
        this.diagnostics = diagnostics;
    }

    public BlockmapLumpStatus Status { get; }
    public int OriginX { get; }
    public int OriginY { get; }
    public int Columns { get; }
    public int Rows { get; }
    public int LumpSize { get; }
    public Vector2D Origin => new(OriginX, OriginY);
    public IReadOnlyDictionary<int, BlockmapBlockList> Blocks => blocks;
    public IReadOnlyList<BlockmapLumpDiagnostic> Diagnostics => diagnostics;
    public bool IsUsable => Columns > 0 && Rows > 0 && Status != BlockmapLumpStatus.Empty && Status != BlockmapLumpStatus.TooShortHeader && Status != BlockmapLumpStatus.InvalidDimensions && Status != BlockmapLumpStatus.OffsetTableTruncated && Status != BlockmapLumpStatus.BlockListReadPastEnd;

    public int GetBlockListOffset(int column, int row)
    {
        if (!IsCellInRange(column, row)) return -1;
        return blockPointers[column, row];
    }

    public IReadOnlyList<int> GetLinesInBlock(int column, int row)
    {
        int offset = GetBlockListOffset(column, row);
        return offset >= 0 && blocks.TryGetValue(offset, out BlockmapBlockList? block)
            ? block.LinedefIndexes
            : Array.Empty<int>();
    }

    public IReadOnlyList<(int Column, int Row)> GetSharedBlocks(int column, int row)
    {
        int offset = GetBlockListOffset(column, row);
        if (offset < 0) return Array.Empty<(int, int)>();

        var shared = new List<(int Column, int Row)>();
        for (int rowIndex = 0; rowIndex < Rows; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < Columns; columnIndex++)
            {
                if (blockPointers[columnIndex, rowIndex] == offset)
                    shared.Add((columnIndex, rowIndex));
            }
        }

        return shared;
    }

    public IReadOnlyList<(int Column, int Row)> GetHighlightedBlocks(int column, int row, bool includeSharedBlocks)
    {
        if (!IsCellInRange(column, row)) return Array.Empty<(int, int)>();
        return includeSharedBlocks
            ? GetSharedBlocks(column, row)
            : new[] { (column, row) };
    }

    public (int Column, int Row) GetColumnAndRowByPosition(Vector2D position)
    {
        if (position.x < OriginX || position.y < OriginY || position.x > OriginX + Columns * BlockmapLump.BlockSize || position.y > OriginY + Rows * BlockmapLump.BlockSize)
            return (-1, -1);

        int column = (int)(position.x - OriginX) / BlockmapLump.BlockSize;
        int row = (int)(position.y - OriginY) / BlockmapLump.BlockSize;
        return (column, row);
    }

    public IReadOnlyList<(int Column, int Row)> GetQuestionableBlocks()
    {
        int listStart = BlockmapLump.HeaderSize + sizeof(ushort) * Columns * Rows;
        var questionable = new List<(int Column, int Row)>();

        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                int offset = blockPointers[column, row];
                if (offset >= 0 && offset < listStart) questionable.Add((column, row));
            }
        }

        return questionable;
    }

    public int GetQuestionableOffsetCount() => GetQuestionableBlocks().Count;

    public int CountLinesNotInBlocks(int linedefCount)
    {
        if (linedefCount <= 0) return 0;

        var present = new HashSet<int>();
        foreach (BlockmapBlockList block in blocks.Values)
        {
            foreach (int index in block.LinedefIndexes)
            {
                if (index >= 0 && index < linedefCount) present.Add(index);
            }
        }

        return linedefCount - present.Count;
    }

    private bool IsCellInRange(int column, int row) => column >= 0 && row >= 0 && column < Columns && row < Rows;
}

public static class BlockmapLump
{
    public const int HeaderSize = 8;
    public const int BlockSize = 128;

    public static BlockmapLumpData Parse(byte[]? data)
    {
        if (data == null || data.Length == 0)
            return Failure(BlockmapLumpStatus.Empty, data?.Length ?? 0, "BLOCKMAP lump is empty.");

        if (data.Length < HeaderSize)
            return Failure(BlockmapLumpStatus.TooShortHeader, data.Length, "BLOCKMAP lump is shorter than the 8-byte header.");

        int originX = ReadInt16(data, 0);
        int originY = ReadInt16(data, 2);
        int columns = ReadInt16(data, 4);
        int rows = ReadInt16(data, 6);

        if (columns <= 0 || rows <= 0)
        {
            return Failure(
                BlockmapLumpStatus.InvalidDimensions,
                data.Length,
                "BLOCKMAP lump has non-positive dimensions.",
                originX,
                originY,
                Math.Max(0, columns),
                Math.Max(0, rows));
        }

        long cellCount = (long)columns * rows;
        long offsetTableEnd = HeaderSize + cellCount * sizeof(ushort);
        if (offsetTableEnd > data.Length || cellCount > int.MaxValue)
        {
            return Failure(
                BlockmapLumpStatus.OffsetTableTruncated,
                data.Length,
                "BLOCKMAP lump ends before the block-list offset table is complete.",
                originX,
                originY,
                columns,
                rows);
        }

        var blockPointers = new int[columns, rows];
        var blocks = new Dictionary<int, BlockmapBlockList>();
        var diagnostics = new List<BlockmapLumpDiagnostic>();
        var offsets = new int[cellCount];

        for (int i = 0; i < offsets.Length; i++)
            offsets[i] = ReadUInt16(data, HeaderSize + i * sizeof(ushort)) * sizeof(ushort);

        for (int column = 0; column < columns; column++)
        {
            for (int row = rows - 1; row >= 0; row--)
            {
                int offset = offsets[row * columns + column];

                if (offset > data.Length)
                {
                    blockPointers[column, row] = -1;
                    diagnostics.Add(new BlockmapLumpDiagnostic(
                        BlockmapLumpStatus.BlockListOffsetOutOfRange,
                        column,
                        row,
                        offset,
                        "Block list offset points beyond the end of the BLOCKMAP lump."));
                    continue;
                }

                if (!blocks.ContainsKey(offset))
                {
                    if (!TryReadBlockList(data, offset, HeaderSize + offsets.Length * sizeof(ushort), out BlockmapBlockList? block, out BlockmapLumpDiagnostic? diagnostic))
                    {
                        blockPointers[column, row] = -1;
                        diagnostics.Add(diagnostic!.Value with { Column = column, Row = row });
                        continue;
                    }

                    blocks.Add(offset, block!);
                }

                blockPointers[column, row] = offset;
            }
        }

        BlockmapLumpStatus status = diagnostics.Count == 0
            ? BlockmapLumpStatus.Ok
            : WorstStatus(diagnostics);

        return new BlockmapLumpData(
            status,
            originX,
            originY,
            columns,
            rows,
            blockPointers,
            new ReadOnlyDictionary<int, BlockmapBlockList>(blocks),
            data.Length,
            diagnostics);
    }

    private static bool TryReadBlockList(
        byte[] data,
        int offset,
        int listStart,
        out BlockmapBlockList? block,
        out BlockmapLumpDiagnostic? diagnostic)
    {
        block = null;
        diagnostic = null;

        var indices = new HashSet<int>();
        int pos = offset;
        while (true)
        {
            if (pos + sizeof(ushort) > data.Length)
            {
                diagnostic = new BlockmapLumpDiagnostic(
                    BlockmapLumpStatus.BlockListReadPastEnd,
                    null,
                    null,
                    offset,
                    "Block list has no terminating 0xFFFF entry before the end of the BLOCKMAP lump.");
                return false;
            }

            int index = ReadUInt16(data, pos);
            pos += sizeof(ushort);
            if (index == ushort.MaxValue) break;

            indices.Add(index);
        }

        bool isSublist = offset > sizeof(ushort) && offset != listStart && ReadUInt16(data, offset - sizeof(ushort)) != ushort.MaxValue;
        block = new BlockmapBlockList(offset, Array.AsReadOnly(new List<int>(indices).ToArray()), isSublist);
        return true;
    }

    private static BlockmapLumpStatus WorstStatus(IEnumerable<BlockmapLumpDiagnostic> diagnostics)
    {
        BlockmapLumpStatus status = BlockmapLumpStatus.Ok;
        foreach (BlockmapLumpDiagnostic diagnostic in diagnostics)
        {
            if (diagnostic.Status > status) status = diagnostic.Status;
        }

        return status;
    }

    private static BlockmapLumpData Failure(
        BlockmapLumpStatus status,
        int lumpSize,
        string message,
        int originX = 0,
        int originY = 0,
        int columns = 0,
        int rows = 0)
    {
        return new BlockmapLumpData(
            status,
            originX,
            originY,
            columns,
            rows,
            new int[Math.Max(0, columns), Math.Max(0, rows)],
            new ReadOnlyDictionary<int, BlockmapBlockList>(new Dictionary<int, BlockmapBlockList>()),
            lumpSize,
            new[] { new BlockmapLumpDiagnostic(status, null, null, null, message) });
    }

    private static short ReadInt16(byte[] data, int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, sizeof(short)));
    private static ushort ReadUInt16(byte[] data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, sizeof(ushort)));
}
