// ABOUTME: SerializerStream ported from UDB Source/Core/IO/SerializerStream.cs.
// ABOUTME: Writes a string-tabled binary format; read-only ops throw to match UDB's General.Fail semantics.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;
using System.Collections.Generic;
using System.IO;
using DBuilder.Geometry;

namespace DBuilder.IO;

public sealed class SerializerStream : IReadWriteStream, IDisposable
{
    private readonly BinaryWriter writer;
    private readonly Dictionary<string, ushort> stringstable;
    private bool isdisposed; //mxd

    public bool IsWriting => true;

    public SerializerStream(Stream stream)
    {
        // leaveOpen: true — the caller owns the stream lifecycle, matching how UDB threads streams in from file save flows.
        this.writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        this.stringstable = new Dictionary<string, ushort>(StringComparer.Ordinal);
    }

    //mxd
    public void Dispose()
    {
        if (!isdisposed)
        {
            writer?.Close();
            isdisposed = true;
        }
    }

    public void Begin()
    {
        // First 4 bytes are reserved for the offset of the strings table
        const int offset = 0;
        writer.Write(offset);
    }

    public void End()
    {
        int offset = (int)writer.BaseStream.Length;
        writer.Seek(0, SeekOrigin.Begin);
        writer.Write(offset);

        writer.Seek(0, SeekOrigin.End);
        foreach (KeyValuePair<string, ushort> str in stringstable)
            writer.Write(str.Key);
    }

    public void rwInt(ref int v) => writer.Write(v);
    public void rwByte(ref byte v) => writer.Write(v);
    public void rwShort(ref short v) => writer.Write(v);

    public void rwString(ref string v)
    {
        ushort index;
        if (stringstable.ContainsKey(v))
            index = stringstable[v];
        else
            index = stringstable[v] = (ushort)stringstable.Count;
        writer.Write(index);
    }

    public void rwLong(ref long v) => writer.Write(v);
    public void rwUInt(ref uint v) => writer.Write(v);
    public void rwUShort(ref ushort v) => writer.Write(v);
    public void rwULong(ref ulong v) => writer.Write(v);
    public void rwFloat(ref float v) => writer.Write(v);
    public void rwDouble(ref double v) => writer.Write(v);
    public void rwBool(ref bool v) => writer.Write(v);

    public void rwVector2D(ref Vector2D v) { writer.Write(v.x); writer.Write(v.y); }
    public void rwVector3D(ref Vector3D v) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }

    public void wInt(int v) => writer.Write(v);
    public void wByte(byte v) => writer.Write(v);
    public void wShort(short v) => writer.Write(v);

    public void wString(string v)
    {
        ushort index;
        if (stringstable.ContainsKey(v))
            index = stringstable[v];
        else
            index = stringstable[v] = (ushort)stringstable.Count;
        writer.Write(index);
    }

    public void wLong(long v) => writer.Write(v);
    public void wUInt(uint v) => writer.Write(v);
    public void wUShort(ushort v) => writer.Write(v);
    public void wULong(ulong v) => writer.Write(v);
    public void wFloat(float v) => writer.Write(v);
    public void wDouble(double v) => writer.Write(v);
    public void wBool(bool v) => writer.Write(v);
    public void wVector2D(Vector2D v) { writer.Write(v.x); writer.Write(v.y); }
    public void wVector3D(Vector3D v) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }

    // Read-only is not supported (UDB calls General.Fail; we throw to keep parity).
    private static InvalidOperationException ReadNotSupported() =>
        new InvalidOperationException("Read-only is not supported on serialization stream. Consider passing the element by reference for bidirectional support.");

    public void rInt(out int v) => throw ReadNotSupported();
    public void rByte(out byte v) => throw ReadNotSupported();
    public void rShort(out short v) => throw ReadNotSupported();
    public void rString(out string v) => throw ReadNotSupported();
    public void rLong(out long v) => throw ReadNotSupported();
    public void rUInt(out uint v) => throw ReadNotSupported();
    public void rUShort(out ushort v) => throw ReadNotSupported();
    public void rULong(out ulong v) => throw ReadNotSupported();
    public void rFloat(out float v) => throw ReadNotSupported();
    public void rDouble(out double v) => throw ReadNotSupported();
    public void rBool(out bool v) => throw ReadNotSupported();
    public void rVector2D(out Vector2D v) => throw ReadNotSupported();
    public void rVector3D(out Vector3D v) => throw ReadNotSupported();
}
