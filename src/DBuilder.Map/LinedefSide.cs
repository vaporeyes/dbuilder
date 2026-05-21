// ABOUTME: LinedefSide ported from UDB Source/Core/Geometry/LinedefSide.cs.
// ABOUTME: Indicates a side of a line without needing a real Sidedef.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;

namespace DBuilder.Map;

public sealed class LinedefSide : IEquatable<LinedefSide>
{
    private Linedef line;
    private bool front;
    private bool ignore; //mxd

    public Linedef Line { get => line; set => line = value; }
    public bool Front { get => front; set => front = value; }
    public bool Ignore { get => ignore; set => ignore = value; } //mxd

    public LinedefSide(Linedef line, bool front)
    {
        this.line = line;
        this.front = front;
    }

    /// <summary>Copy ctor.</summary>
    public LinedefSide(LinedefSide original)
    {
        this.line = original.line;
        this.front = original.front;
    }

    public static bool operator ==(LinedefSide? a, LinedefSide? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return (a.line == b.line) && (a.front == b.front);
    }

    public static bool operator !=(LinedefSide? a, LinedefSide? b)
    {
        if (a is null && b is null) return false;
        if (a is null || b is null) return true;
        return (a.line != b.line) || (a.front != b.front);
    }

    public bool Equals(LinedefSide? other) => other is not null && line == other.line && front == other.front;
    public override bool Equals(object? obj) => obj is LinedefSide ls && Equals(ls);
    public override int GetHashCode() => HashCode.Combine(line, front);

#if DEBUG
    public override string ToString()
    {
        Sidedef? side = (front ? line.Front : line.Back);
        Sector? sector = side?.Sector;
        return line + " (" + (front ? "front" : "back") + ")" + (sector != null ? ", Sector " + sector.Index : ", no sector");
    }
#endif
}
