// ABOUTME: Parser for ZDoom LOCKDEFS lock declarations.
// ABOUTME: Captures lock ids, messages, map colors, and key group references for editor metadata.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DBuilder.IO;

public sealed class LockDefs
{
    public bool ClearLocks { get; set; }
    public List<LockDefinition> Locks { get; } = new();
}

public sealed class LockDefinition
{
    public string Id { get; init; } = "";
    public string? Game { get; set; }
    public string? Title { get; set; }
    public string? Message { get; set; }
    public string? RemoteMessage { get; set; }
    public (int R, int G, int B)? MapColor { get; set; }
    public List<LockKeyGroup> KeyGroups { get; } = new();
}

public sealed record LockKeyGroup(string Mode, IReadOnlyList<string> Keys);

public static class LockdefsParser
{
    public static LockDefs Parse(string text)
    {
        var defs = new LockDefs();
        var t = ZDoomTokenScanner.Tokenize(text);
        for (int i = 0; i < t.Count;)
        {
            string keyword = t[i++].ToLowerInvariant();
            if (keyword == "clearlocks") { defs.ClearLocks = true; defs.Locks.Clear(); }
            else if (keyword == "lock" && i < t.Count)
            {
                if (!ParseLock(defs, t, ref i)) return defs;
            }
            else if (i < t.Count && t[i] == "{") SkipBlock(t, ref i);
        }
        return defs;
    }

    private static bool ParseLock(LockDefs defs, List<string> t, ref int i)
    {
        if (!ReadPositiveLockId(t, ref i, out string lockId))
        {
            return false;
        }

        var lockDef = new LockDefinition { Id = lockId };
        if (i < t.Count && t[i] != "{") lockDef.Game = t[i++];
        if (i < t.Count && t[i] == "{")
        {
            i++;
            while (i < t.Count && t[i] != "}")
            {
                string prop = t[i++].ToLowerInvariant();
                if (prop == "$title" && i < t.Count) lockDef.Title = t[i++];
                else if (prop == "message" && i < t.Count) lockDef.Message = t[i++];
                else if (prop == "remotemessage" && i < t.Count) lockDef.RemoteMessage = t[i++];
                else if (prop == "mapcolor")
                {
                    if (!ReadMapColor(t, ref i, out var mapColor)) return false;
                    lockDef.MapColor = mapColor;
                }
                else if ((prop == "any" || prop == "all") && i < t.Count && t[i] == "{") lockDef.KeyGroups.Add(ReadKeyGroup(prop, t, ref i));
                else if (i < t.Count && t[i] == "{") SkipBlock(t, ref i);
            }
            if (i < t.Count) i++;
        }
        if (lockDef.Id.Length > 0) defs.Locks.Add(lockDef);
        return true;
    }

    private static bool ReadPositiveLockId(List<string> t, ref int i, out string lockId)
    {
        lockId = "";
        if (i >= t.Count) return false;
        string token = t[i++];
        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) || id < 1) return false;
        lockId = token;
        return true;
    }

    private static bool ReadMapColor(List<string> t, ref int i, out (int R, int G, int B)? mapColor)
    {
        mapColor = null;
        if (ReadByteRangeInt(t, ref i, out int r)
            && ReadByteRangeInt(t, ref i, out int g)
            && ReadByteRangeInt(t, ref i, out int b))
        {
            mapColor = (r, g, b);
            return true;
        }
        return false;
    }

    private static bool ReadByteRangeInt(List<string> t, ref int i, out int value)
    {
        value = 0;
        if (i >= t.Count || t[i] is "{" or "}") return false;
        string token = t[i++];
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value is >= 0 and <= 255;
    }

    private static LockKeyGroup ReadKeyGroup(string mode, List<string> t, ref int i)
    {
        var keys = new List<string>();
        i++;
        while (i < t.Count && t[i] != "}")
        {
            if (t[i] is not "," and not ";") keys.Add(t[i]);
            i++;
        }
        if (i < t.Count) i++;
        return new LockKeyGroup(mode, keys);
    }

    private static void SkipBlock(List<string> t, ref int i)
    {
        int depth = 0;
        for (; i < t.Count; i++)
        {
            if (t[i] == "{") depth++;
            else if (t[i] == "}") { depth--; if (depth == 0) { i++; return; } }
        }
    }
}
