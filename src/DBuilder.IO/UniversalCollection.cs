// ABOUTME: UDMF collection ported from UDB Source/Core/IO/UniversalCollection.cs.
// ABOUTME: Ordered list of UniversalEntry with optional comment line. Used for root and nested structs in UDMF.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System.Collections.Generic;

namespace DBuilder.IO;

public sealed class UniversalCollection : List<UniversalEntry>
{
    private string comment = "";

    public string Comment { get => comment; set => comment = value; }

    public void Add(string key, object value)
    {
        base.Add(new UniversalEntry(key, value));
    }
}
