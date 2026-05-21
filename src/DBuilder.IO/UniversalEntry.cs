// ABOUTME: UDMF key-value entry ported from UDB Source/Core/IO/UniversalEntry.cs.
// ABOUTME: One row in a UniversalCollection; value is boxed and can be int/long/double/bool/string/UniversalCollection.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;

namespace DBuilder.IO;

public sealed class UniversalEntry
{
    private string key;
    private object value;

    public string Key => key;
    public object Value => value;

    public UniversalEntry(string key, object value)
    {
        this.key = key;
        this.value = value;
    }

    // Throws when the value is not of the expected type.
    public void ValidateType(Type t)
    {
        if (value.GetType() != t) throw new Exception("The value of entry \"" + key + "\" is of incompatible type (expected " + t.Name + ")");
    }

    //mxd
    public bool IsValidType(Type t) => value.GetType() == t;
}
