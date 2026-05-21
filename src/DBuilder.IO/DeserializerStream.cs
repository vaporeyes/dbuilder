// ABOUTME: DeserializerStream ported from UDB Source/Core/IO/DeserializerStream.cs.
// ABOUTME: Reads the string-tabled binary format produced by SerializerStream.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;
using System.Collections.Generic;
using System.IO;
using DBuilder.Geometry;

namespace DBuilder.IO;

public sealed class DeserializerStream : IReadWriteStream, IDisposable
{
    private Stream? stream;
    private readonly BinaryReader reader;
    private string[] stringstable = Array.Empty<string>();
    private int stringtablepos;
    private bool isdisposed; //mxd

    public bool IsWriting => false;

    public int EndPosition => stringtablepos;

    public DeserializerStream(Stream stream)
    {
        this.stream = stream;
        this.reader = new BinaryReader(stream);
    }

    //mxd
    public void Dispose()
    {
        if (!isdisposed)
        {
            reader?.Close();
            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }
            isdisposed = true;
        }
    }

    public void Begin()
    {
        // First 4 bytes are reserved for the offset of the strings table
        stringtablepos = reader.ReadInt32();
        stream!.Seek(stringtablepos, SeekOrigin.Begin);

        List<string> strings = new List<string>();
        while (stream.Position < (int)stream.Length)
            strings.Add(reader.ReadString());
        stringstable = strings.ToArray();

        // Back to start
        stream.Seek(4, SeekOrigin.Begin);
    }

    public void End() { }

    public void rwInt(ref int v) => v = reader.ReadInt32();
    public void rwByte(ref byte v) => v = reader.ReadByte();
    public void rwShort(ref short v) => v = reader.ReadInt16();

    public void rwString(ref string v)
    {
        ushort index = reader.ReadUInt16();
        v = stringstable[index];
    }

    public void rwLong(ref long v) => v = reader.ReadInt64();
    public void rwUInt(ref uint v) => v = reader.ReadUInt32();
    public void rwUShort(ref ushort v) => v = reader.ReadUInt16();
    public void rwULong(ref ulong v) => v = reader.ReadUInt64();
    public void rwFloat(ref float v) => v = reader.ReadSingle();
    public void rwDouble(ref double v) => v = reader.ReadDouble();
    public void rwBool(ref bool v) => v = reader.ReadBoolean();

    public void rwVector2D(ref Vector2D v) { v.x = reader.ReadDouble(); v.y = reader.ReadDouble(); }
    public void rwVector3D(ref Vector3D v) { v.x = reader.ReadDouble(); v.y = reader.ReadDouble(); v.z = reader.ReadDouble(); }

    // Write-only is not supported
    private static InvalidOperationException WriteNotSupported() =>
        new InvalidOperationException("Write-only is not supported on deserialization stream. Consider passing the element by reference for bidirectional support.");

    public void wInt(int v) => throw WriteNotSupported();
    public void wByte(byte v) => throw WriteNotSupported();
    public void wShort(short v) => throw WriteNotSupported();
    public void wString(string v) => throw WriteNotSupported();
    public void wLong(long v) => throw WriteNotSupported();
    public void wUInt(uint v) => throw WriteNotSupported();
    public void wUShort(ushort v) => throw WriteNotSupported();
    public void wULong(ulong v) => throw WriteNotSupported();
    public void wFloat(float v) => throw WriteNotSupported();
    public void wDouble(double v) => throw WriteNotSupported();
    public void wBool(bool v) => throw WriteNotSupported();
    public void wVector2D(Vector2D v) => throw WriteNotSupported();
    public void wVector3D(Vector3D v) => throw WriteNotSupported();

    public void rInt(out int v) => v = reader.ReadInt32();
    public void rByte(out byte v) => v = reader.ReadByte();
    public void rShort(out short v) => v = reader.ReadInt16();

    public void rString(out string v)
    {
        ushort index = reader.ReadUInt16();
        v = stringstable[index];
    }

    public void rLong(out long v) => v = reader.ReadInt64();
    public void rUInt(out uint v) => v = reader.ReadUInt32();
    public void rUShort(out ushort v) => v = reader.ReadUInt16();
    public void rULong(out ulong v) => v = reader.ReadUInt64();
    public void rFloat(out float v) => v = reader.ReadSingle();
    public void rDouble(out double v) => v = reader.ReadDouble();
    public void rBool(out bool v) => v = reader.ReadBoolean();

    public void rVector2D(out Vector2D v)
    {
        v = new Vector2D();
        v.x = reader.ReadDouble();
        v.y = reader.ReadDouble();
    }

    public void rVector3D(out Vector3D v)
    {
        v = new Vector3D();
        v.x = reader.ReadDouble();
        v.y = reader.ReadDouble();
        v.z = reader.ReadDouble();
    }
}
