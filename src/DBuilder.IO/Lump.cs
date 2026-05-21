// ABOUTME: WAD lump record ported from UDB Source/Core/IO/Lump.cs.
// ABOUTME: Global-state-dependent MakeLongName(name) overload dropped; callers pass useLongNames explicitly.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;
using System.IO;
using System.Text;

namespace DBuilder.IO;

public class Lump : IDisposable
{
    // Classic 8-character WAD lump name limit (UDB calls this DataManager.CLASIC_IMAGE_NAME_LENGTH).
    public const int CLASSIC_NAME_LENGTH = 8;

    // Allowed characters in a map lump name
    internal const string MAP_LUMP_NAME_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890_";

    private WAD? owner;

    // Data stream
    private readonly ClippedStream stream;

    // Data info
    private string name;
    private long longname;
    private byte[] fixedname;
    private readonly int offset;
    private readonly int length;

    private bool isdisposed;

    public WAD? Owner => owner;
    public string Name => name;
    public long LongName => longname;
    public byte[] FixedName => fixedname;
    public int Offset => offset;
    public int Length => length;
    public ClippedStream Stream => stream;
    public bool IsDisposed => isdisposed;

    internal Lump(Stream data, WAD owner, byte[] fixedname, int offset, int length)
    {
        this.stream = new ClippedStream(data, offset, length);
        this.owner = owner;
        this.fixedname = fixedname;
        this.offset = offset;
        this.length = length;

        // Make name. Owner's UseLongTextureNames determines which longname variant is used.
        this.name = MakeNormalName(fixedname, WAD.ENCODING).ToUpperInvariant();
        this.fixedname = MakeFixedName(name, WAD.ENCODING);
        this.longname = MakeLongName(name, owner.UseLongTextureNames);

        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (!isdisposed)
        {
            stream.Dispose();
            owner = null;
            isdisposed = true;
        }
    }

    //mxd. Stable (hopefully unique) hash value for a texture name of any length.
    // The flag controls whether the name is first truncated to the classic 8-char limit.
    public static long MakeLongName(string name, bool useLongNames)
    {
        // biwa. ToUpper can produce clashes between names that differ only in case; matches UDB behavior.
        name = name.ToUpper();
        if (!useLongNames && name.Length > CLASSIC_NAME_LENGTH)
        {
            name = name.Substring(0, CLASSIC_NAME_LENGTH);
        }
        return MurmurHash2.Hash(name);
    }

    // Trim trailing nulls and convert to upper-case ASCII.
    public static string MakeNormalName(byte[] fixedname, Encoding encoding)
    {
        int length = 0;
        while ((length < fixedname.Length) && (fixedname[length] != 0)) length++;
        return encoding.GetString(fixedname, 0, length).Trim().ToUpper();
    }

    // Encode to the 8-byte (zero-padded) on-disk lump name.
    public static byte[] MakeFixedName(string name, Encoding encoding)
    {
        string uppername = name.Trim().ToUpper();
        int bytes = encoding.GetByteCount(uppername);
        if (bytes < CLASSIC_NAME_LENGTH) bytes = CLASSIC_NAME_LENGTH;

        byte[] fixedname = new byte[bytes];
        encoding.GetBytes(uppername, 0, uppername.Length, fixedname, 0);
        return fixedname;
    }

    internal void CopyTo(Lump dest)
    {
        BinaryReader reader = new BinaryReader(stream);
        stream.Seek(0, SeekOrigin.Begin);
        dest.Stream.Write(reader.ReadBytes((int)stream.Length), 0, (int)stream.Length);
    }

    public override string ToString() => name;

    internal void Rename(string newname)
    {
        this.fixedname = MakeFixedName(newname, WAD.ENCODING);
        this.name = MakeNormalName(this.fixedname, WAD.ENCODING).ToUpperInvariant();
        this.longname = MakeLongName(newname, owner!.UseLongTextureNames);
        owner.WriteHeaders();
    }

    // [ZZ] thread-safe: returns a MemoryStream with a copy of the lump bytes.
    public Stream? GetSafeStream()
    {
        if (stream == null || stream.BaseStream == null)
            return null;

        byte[] data;
        lock (stream.BaseStream)
        {
            stream.Position = 0;
            data = stream.ReadAllBytes();
        }

        MemoryStream ms = new MemoryStream(data);
        ms.Position = 0;
        return ms;
    }
}
