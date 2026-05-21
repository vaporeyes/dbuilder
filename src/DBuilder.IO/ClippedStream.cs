// ABOUTME: ClippedStream ported from UDB Source/Core/IO/ClippedStream.cs.
// ABOUTME: Stream view onto a [offset, offset+length) window of a seekable base stream. Foundation for Lump and PK3 readers.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;
using System.IO;

namespace DBuilder.IO;

public class ClippedStream : Stream
{
    private Stream? basestream;

    private int offset;
    private int length;
    private long position;

    private bool isdisposed;

    public Stream? BaseStream => basestream;
    public override long Length => length;
    public override long Position { get => position; set => this.Seek(value, SeekOrigin.Begin); }
    public override bool CanRead => basestream!.CanRead;
    public override bool CanSeek => basestream!.CanSeek;
    public override bool CanWrite => basestream!.CanWrite;
    public bool IsDisposed => isdisposed;

    public ClippedStream(Stream basestream, int offset, int length)
    {
        if (!basestream.CanSeek) throw new ArgumentException("ClippedStream can only be created with a Stream that allows Seeking.");

        this.basestream = basestream;
        this.position = 0;
        this.offset = offset;
        this.length = length;

        GC.SuppressFinalize(this);
    }

    public new void Dispose()
    {
        if (!isdisposed)
        {
            isdisposed = true;
            basestream = null;
            base.Dispose();
        }
    }

    public override void Flush() => basestream!.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        // Clip read to stream length
        if ((this.position + count) > (this.length + 1))
            count = this.length - (int)this.position;

        if (count > 0)
        {
            if (basestream!.Position != (this.offset + this.position))
                basestream.Seek(this.offset + this.position, SeekOrigin.Begin);

            position += count;
            return basestream.Read(buffer, offset, count);
        }
        return 0;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if ((this.position + count) > (this.length + 1))
            throw new ArgumentException("Attempted to write outside the range of the stream.");

        if (basestream!.Position != (this.offset + this.position))
            basestream.Seek(this.offset + this.position, SeekOrigin.Begin);

        position += count;
        basestream.Write(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (origin == SeekOrigin.Begin)
        {
            if ((offset > this.length) || (offset < 0))
                throw new ArgumentException("Attempted to seek outside the range of the stream.");

            position = basestream!.Seek(this.offset + offset, SeekOrigin.Begin) - this.offset;
        }
        else if (origin == SeekOrigin.Current)
        {
            if ((this.position + offset > this.length) || (this.position + offset < 0))
                throw new ArgumentException("Attempted to seek outside the range of the stream.");

            position = basestream!.Seek(this.offset + this.position + offset, SeekOrigin.Begin) - this.offset;
        }
        else
        {
            if ((offset > 0) || (this.length + offset < 0))
                throw new ArgumentException("Attempted to seek outside the range of the stream.");

            position = basestream!.Seek(this.offset + this.length + offset, SeekOrigin.Begin) - this.offset;
        }

        return position;
    }

    public override void SetLength(long value)
        => throw new NotSupportedException("This operation is not supported.");

    public override int ReadByte()
    {
        if ((this.position + 1) > (this.length + 1))
            throw new ArgumentException("Attempted to read outside the range of the stream.");

        if (basestream!.Position != (this.offset + this.position))
            basestream.Seek(this.offset + this.position, SeekOrigin.Begin);

        position++;
        return basestream.ReadByte();
    }

    public override void WriteByte(byte value)
    {
        if ((this.position + 1) > (this.length + 1))
            throw new ArgumentException("Attempted to write outside the range of the stream.");

        if (basestream!.Position != (this.offset + this.position))
            basestream.Seek(this.offset + this.position, SeekOrigin.Begin);

        position++;
        basestream.WriteByte(value);
    }

    public override void Close()
    {
        basestream = null;
        base.Close();
    }

    // Returns all bytes in the clipped window.
    public byte[] ReadAllBytes()
    {
        byte[] bytes = new byte[length];
        Seek(0, SeekOrigin.Begin);
        Read(bytes, 0, length);
        return bytes;
    }
}
