// ABOUTME: IReadWriteStream interface ported from UDB Source/Core/IO/IReadWriteStream.cs.
// ABOUTME: Bidirectional binary serialization protocol used by SerializerStream/DeserializerStream.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using DBuilder.Geometry;

namespace DBuilder.IO;

public interface IReadWriteStream
{
    bool IsWriting { get; }

    void Begin();
    void End();

    // Bidirectional
    void rwInt(ref int v);
    void rwByte(ref byte v);
    void rwShort(ref short v);
    void rwString(ref string v);
    void rwLong(ref long v);
    void rwUInt(ref uint v);
    void rwUShort(ref ushort v);
    void rwULong(ref ulong v);
    void rwFloat(ref float v);
    void rwDouble(ref double v);
    void rwVector2D(ref Vector2D v);
    void rwVector3D(ref Vector3D v);
    void rwBool(ref bool v);

    // Write-only
    void wInt(int v);
    void wByte(byte v);
    void wShort(short v);
    void wString(string v);
    void wLong(long v);
    void wUInt(uint v);
    void wUShort(ushort v);
    void wULong(ulong v);
    void wFloat(float v);
    void wDouble(double v);
    void wVector2D(Vector2D v);
    void wVector3D(Vector3D v);
    void wBool(bool v);

    // Read-only
    void rInt(out int v);
    void rByte(out byte v);
    void rShort(out short v);
    void rString(out string v);
    void rLong(out long v);
    void rUInt(out uint v);
    void rUShort(out ushort v);
    void rULong(out ulong v);
    void rFloat(out float v);
    void rDouble(out double v);
    void rVector2D(out Vector2D v);
    void rVector3D(out Vector3D v);
    void rBool(out bool v);
}
