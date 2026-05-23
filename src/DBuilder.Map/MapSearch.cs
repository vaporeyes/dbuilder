// ABOUTME: Find & replace over a MapSet by category (thing type, linedef action, sector effect, tag, texture, flat).
// ABOUTME: Find selects matches and reports a focus point; Replace mutates matched attributes. Pure, no UI.

using System;
using System.Collections.Generic;
using System.Globalization;
using DBuilder.Geometry;

namespace DBuilder.Map;

/// <summary>What a find/replace operation matches against.</summary>
public enum FindCategory { ThingType, LinedefAction, SectorEffect, Tag, Texture, Flat }

/// <summary>Outcome of a find: how many matched and a representative location to center on.</summary>
public readonly record struct SearchResult(int Count, Vector2D? Focus);

public static class MapSearch
{
    /// <summary>True for categories whose value is a string (texture/flat names); the rest are integers.</summary>
    public static bool IsTextual(FindCategory cat) => cat == FindCategory.Texture || cat == FindCategory.Flat;

    /// <summary>
    /// Selects every element matching <paramref name="value"/> in <paramref name="cat"/> (clearing prior selection)
    /// and returns the match count plus a focus point (the first match's location).
    /// </summary>
    public static SearchResult Find(MapSet map, FindCategory cat, string value)
    {
        map.ClearAllSelected();
        int count = 0;
        Vector2D? focus = null;
        bool numOk = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int num);

        switch (cat)
        {
            case FindCategory.ThingType:
                if (numOk) foreach (var t in map.Things) if (t.Type == num) { t.Selected = true; count++; focus ??= t.Position; }
                break;
            case FindCategory.LinedefAction:
                if (numOk) foreach (var l in map.Linedefs) if (l.Action == num) { l.Selected = true; count++; focus ??= Mid(l); }
                break;
            case FindCategory.SectorEffect:
                if (numOk) foreach (var s in map.Sectors) if (s.Special == num) { s.Selected = true; count++; }
                break;
            case FindCategory.Tag:
                if (numOk)
                {
                    foreach (var l in map.Linedefs) if (l.Tag == num) { l.Selected = true; count++; focus ??= Mid(l); }
                    foreach (var s in map.Sectors) if (s.Tag == num) { s.Selected = true; count++; }
                    foreach (var t in map.Things) if (t.Tag == num) { t.Selected = true; count++; focus ??= t.Position; }
                }
                break;
            case FindCategory.Texture:
                foreach (var sd in map.Sidedefs)
                    if (sd.Line != null && (Eq(sd.HighTexture, value) || Eq(sd.MidTexture, value) || Eq(sd.LowTexture, value)))
                    { sd.Line.Selected = true; count++; focus ??= Mid(sd.Line); }
                break;
            case FindCategory.Flat:
                foreach (var s in map.Sectors)
                    if (Eq(s.FloorTexture, value) || Eq(s.CeilTexture, value)) { s.Selected = true; count++; }
                break;
        }
        return new SearchResult(count, focus);
    }

    /// <summary>
    /// Replaces <paramref name="find"/> with <paramref name="replace"/> across <paramref name="cat"/> and returns
    /// the number of elements changed. Numeric categories parse both values; textual ones compare case-insensitively.
    /// </summary>
    public static int Replace(MapSet map, FindCategory cat, string find, string replace)
    {
        int changed = 0;
        if (IsTextual(cat))
        {
            switch (cat)
            {
                case FindCategory.Texture:
                    foreach (var sd in map.Sidedefs)
                    {
                        bool hit = false;
                        if (Eq(sd.HighTexture, find)) { sd.HighTexture = replace; hit = true; }
                        if (Eq(sd.MidTexture, find)) { sd.MidTexture = replace; hit = true; }
                        if (Eq(sd.LowTexture, find)) { sd.LowTexture = replace; hit = true; }
                        if (hit) changed++;
                    }
                    break;
                case FindCategory.Flat:
                    foreach (var s in map.Sectors)
                    {
                        bool hit = false;
                        if (Eq(s.FloorTexture, find)) { s.FloorTexture = replace; hit = true; }
                        if (Eq(s.CeilTexture, find)) { s.CeilTexture = replace; hit = true; }
                        if (hit) changed++;
                    }
                    break;
            }
            return changed;
        }

        if (!int.TryParse(find, NumberStyles.Integer, CultureInfo.InvariantCulture, out int from)) return 0;
        if (!int.TryParse(replace, NumberStyles.Integer, CultureInfo.InvariantCulture, out int to)) return 0;
        switch (cat)
        {
            case FindCategory.ThingType:
                foreach (var t in map.Things) if (t.Type == from) { t.Type = to; changed++; }
                break;
            case FindCategory.LinedefAction:
                foreach (var l in map.Linedefs) if (l.Action == from) { l.Action = to; changed++; }
                break;
            case FindCategory.SectorEffect:
                foreach (var s in map.Sectors) if (s.Special == from) { s.Special = to; changed++; }
                break;
            case FindCategory.Tag:
                foreach (var l in map.Linedefs) if (l.Tag == from) { l.Tag = to; changed++; }
                foreach (var s in map.Sectors) if (s.Tag == from) { s.Tag = to; changed++; }
                foreach (var t in map.Things) if (t.Tag == from) { t.Tag = to; changed++; }
                break;
        }
        return changed;
    }

    /// <summary>The lowest positive tag not used by any linedef, sector or thing.</summary>
    public static int NextFreeTag(MapSet map)
    {
        var used = new HashSet<int>();
        foreach (var l in map.Linedefs) if (l.Tag > 0) used.Add(l.Tag);
        foreach (var s in map.Sectors) if (s.Tag > 0) used.Add(s.Tag);
        foreach (var t in map.Things) if (t.Tag > 0) used.Add(t.Tag);
        int tag = 1;
        while (used.Contains(tag)) tag++;
        return tag;
    }

    private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static Vector2D Mid(Linedef l)
        => new Vector2D((l.Start.Position.x + l.End.Position.x) * 0.5, (l.Start.Position.y + l.End.Position.y) * 0.5);
}
