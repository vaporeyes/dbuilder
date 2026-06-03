// ABOUTME: UDMF text-format map writer - inverse of UdmfMapLoader. Emits namespace + vertex/sector/sidedef/linedef/thing blocks.
// ABOUTME: Vertex/sidedef/sector indices are resolved via reference-equality lookup against the MapSet's list ordering.

/*
 * Inspired by UDB Source/Core/IO/UniversalStreamWriter.cs but written from scratch against
 * the minimal Map skeleton. Default values are omitted on write so the textual output stays
 * tight and matches the convention real UDMF tools use.  Round-trip is structural (not byte-
 * exact); the loader normalizes defaults so a load->write->load reproduces the same MapSet.
 */

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using DBuilder.Map;

namespace DBuilder.IO;

public static class UdmfMapWriter
{
    /// <summary>Serializes <paramref name="map"/> to a UDMF text string suitable for the TEXTMAP lump body.</summary>
    public static string Write(MapSet map)
        => Write(map, string.IsNullOrEmpty(map.Namespace) ? "Doom" : map.Namespace);

    /// <summary>Serializes <paramref name="map"/> to UDMF text, optionally omitting the namespace assignment.</summary>
    public static string Write(MapSet map, string? writeNamespace)
    {
        var sb = new StringBuilder();
        if (writeNamespace != null) WriteAssignment(sb, "namespace", writeNamespace);
        WriteCustomFields(sb, map.Fields, indent: false);
        WriteUnknownUdmfData(sb, map.UnknownUdmfData, indentLevel: 0);
        sb.AppendLine();

        var vertexIndex  = BuildIndex(map.Vertices);
        var sidedefIndex = BuildIndex(map.Sidedefs);
        var sectorIndex  = BuildIndex(map.Sectors);

        for (int i = 0; i < map.Vertices.Count; i++)
        {
            WriteVertex(sb, map.Vertices[i], i);
        }
        for (int i = 0; i < map.Linedefs.Count; i++)
        {
            WriteLinedef(sb, map.Linedefs[i], i, vertexIndex, sidedefIndex);
        }
        for (int i = 0; i < map.Sidedefs.Count; i++)
        {
            WriteSidedef(sb, map.Sidedefs[i], i, sectorIndex);
        }
        for (int i = 0; i < map.Sectors.Count; i++)
        {
            WriteSector(sb, map.Sectors[i], i);
        }
        for (int i = 0; i < map.Things.Count; i++)
        {
            WriteThing(sb, map.Things[i], i);
        }

        return NormalizeLineEndings(sb.ToString());
    }

    /// <summary>Writes <paramref name="map"/> as a TEXTMAP lump block (marker + TEXTMAP + ENDMAP) into <paramref name="wad"/>.</summary>
    public static void WriteMap(MapSet map, WAD wad, string markerName, int insertPos)
    {
        if (wad.IsReadOnly) throw new IOException("WAD is read-only");

        byte[] textmap = Encoding.ASCII.GetBytes(Write(map));

        int pos = insertPos;
        wad.Insert(markerName, pos++, 0);
        var lump = wad.Insert("TEXTMAP", pos++, textmap.Length)!;
        lump.Stream.Write(textmap, 0, textmap.Length);
        wad.Insert("ENDMAP", pos++, 0);
        wad.WriteHeaders();
    }

    // ============================================================
    // Block writers
    // ============================================================

    private static void WriteVertex(StringBuilder sb, Vertex v, int index)
    {
        sb.Append("vertex // ").Append(index).AppendLine();
        sb.AppendLine("{");
        WriteAssignment(sb, "x", v.Position.x, indent: true);
        WriteAssignment(sb, "y", v.Position.y, indent: true);
        if (!double.IsNaN(v.ZCeiling)) WriteAssignment(sb, "zceiling", v.ZCeiling, indent: true);
        if (!double.IsNaN(v.ZFloor))   WriteAssignment(sb, "zfloor",   v.ZFloor,   indent: true);
        WriteCustomFields(sb, v.Fields);
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void WriteSector(StringBuilder sb, Sector s, int index)
    {
        sb.Append("sector // ").Append(index).AppendLine();
        sb.AppendLine("{");
        WriteAssignment(sb, "heightfloor", s.FloorHeight, indent: true);
        WriteAssignment(sb, "heightceiling", s.CeilHeight, indent: true);
        WriteAssignment(sb, "texturefloor", s.FloorTexture, indent: true);
        WriteAssignment(sb, "textureceiling", s.CeilTexture, indent: true);
        WriteAssignment(sb, "lightlevel", s.Brightness, indent: true);
        WriteIfNonzero(sb, "special", s.Special);
        WriteIfNonzero(sb, "id",      s.Tag);
        WriteMoreIds(sb, s.Tags);

        // Slope planes are emitted only when an actual slope normal is set (matches UDB).
        if (s.FloorSlope.GetLengthSq() > 0)
        {
            WriteAssignment(sb, "floorplane_a", s.FloorSlope.x, indent: true);
            WriteAssignment(sb, "floorplane_b", s.FloorSlope.y, indent: true);
            WriteAssignment(sb, "floorplane_c", s.FloorSlope.z, indent: true);
            WriteAssignment(sb, "floorplane_d", double.IsNaN(s.FloorSlopeOffset) ? 0.0 : s.FloorSlopeOffset, indent: true);
        }
        if (s.CeilSlope.GetLengthSq() > 0)
        {
            WriteAssignment(sb, "ceilingplane_a", s.CeilSlope.x, indent: true);
            WriteAssignment(sb, "ceilingplane_b", s.CeilSlope.y, indent: true);
            WriteAssignment(sb, "ceilingplane_c", s.CeilSlope.z, indent: true);
            WriteAssignment(sb, "ceilingplane_d", double.IsNaN(s.CeilSlopeOffset) ? 0.0 : s.CeilSlopeOffset, indent: true);
        }

        WriteUdmfFlags(sb, s.UdmfFlags);
        WriteCustomFields(sb, s.Fields, excludeKeys: s.UdmfFlags);
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void WriteSidedef(StringBuilder sb, Sidedef sd, int index, Dictionary<Sector, int> sectorIndex)
    {
        sb.Append("sidedef // ").Append(index).AppendLine();
        sb.AppendLine("{");
        WriteIfNonzero(sb, "offsetx", sd.OffsetX);
        WriteIfNonzero(sb, "offsety", sd.OffsetY);
        WriteIfNotDefault(sb, "texturetop",    sd.HighTexture, "-");
        WriteIfNotDefault(sb, "texturebottom", sd.LowTexture,  "-");
        WriteIfNotDefault(sb, "texturemiddle", sd.MidTexture,  "-");
        int secIdx = sd.Sector != null && sectorIndex.TryGetValue(sd.Sector, out int si) ? si : 0;
        WriteAssignment(sb, "sector", secIdx, indent: true);
        WriteUdmfFlags(sb, sd.UdmfFlags);
        WriteCustomFields(sb, sd.Fields, excludeKeys: sd.UdmfFlags);
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void WriteLinedef(StringBuilder sb, Linedef l, int index, Dictionary<Vertex, int> vertexIndex, Dictionary<Sidedef, int> sidedefIndex)
    {
        sb.Append("linedef // ").Append(index).AppendLine();
        sb.AppendLine("{");
        WriteIfNonzero(sb, "id", l.Tag);
        int v1 = vertexIndex.TryGetValue(l.Start, out int v1i) ? v1i : 0;
        int v2 = vertexIndex.TryGetValue(l.End,   out int v2i) ? v2i : 0;
        WriteAssignment(sb, "v1", v1, indent: true);
        WriteAssignment(sb, "v2", v2, indent: true);

        int sideFront = l.Front != null && sidedefIndex.TryGetValue(l.Front, out int srI) ? srI : -1;
        int sideBack  = l.Back  != null && sidedefIndex.TryGetValue(l.Back,  out int slI) ? slI : -1;
        WriteAssignment(sb, "sidefront", sideFront, indent: true);
        WriteAssignment(sb, "sideback", sideBack, indent: true);

        WriteIfNonzero(sb, "special", l.Action);
        WriteMoreIds(sb, l.Tags);
        for (int i = 0; i < l.Args.Length; i++)
            if (l.Args[i] != 0) WriteAssignment(sb, $"arg{i}", l.Args[i], indent: true);

        WriteUdmfFlags(sb, l.UdmfFlags);
        WriteCustomFields(sb, l.Fields);
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void WriteThing(StringBuilder sb, Thing t, int index)
    {
        sb.Append("thing // ").Append(index).AppendLine();
        sb.AppendLine("{");
        WriteIfNonzero(sb, "id", t.Tag);
        WriteAssignment(sb, "x",     t.Position.x, indent: true);
        WriteAssignment(sb, "y",     t.Position.y, indent: true);
        if (t.Height != 0) WriteAssignment(sb, "height", t.Height, indent: true);
        WriteAssignment(sb, "angle", t.Angle, indent: true);
        WriteIfNonzero(sb, "pitch",   t.Pitch);
        WriteIfNonzero(sb, "roll",    t.Roll);
        if (t.ScaleX != 0 && t.ScaleX != 1.0) WriteAssignment(sb, "scalex", t.ScaleX, indent: true);
        if (t.ScaleY != 0 && t.ScaleY != 1.0) WriteAssignment(sb, "scaley", t.ScaleY, indent: true);
        WriteAssignment(sb, "type",   t.Type, indent: true);
        WriteIfNonzero(sb, "special", t.Action);
        for (int i = 0; i < t.Args.Length; i++)
            if (t.Args[i] != 0) WriteAssignment(sb, $"arg{i}", t.Args[i], indent: true);

        WriteUdmfFlags(sb, t.UdmfFlags);
        WriteCustomFields(sb, t.Fields);
        sb.AppendLine("}");
        sb.AppendLine();
    }

    // ============================================================
    // Primitive writers
    // ============================================================

    // Emits ZDoom "moreids" for the tags beyond the primary id (Tags[1..]). Tags[0] is written as "id".
    private static void WriteMoreIds(StringBuilder sb, List<int> tags)
    {
        if (tags.Count <= 1) return;
        var extra = new StringBuilder();
        for (int i = 1; i < tags.Count; i++)
        {
            if (i > 1) extra.Append(' ');
            extra.Append(tags[i].ToString(CultureInfo.InvariantCulture));
        }
        WriteAssignment(sb, "moreids", extra.ToString(), indent: true);
    }

    private static void WriteCustomFields(StringBuilder sb, Dictionary<string, object> fields, bool indent = true,
        ISet<string>? excludeKeys = null)
    {
        foreach (var kv in fields)
        {
            if (excludeKeys != null && excludeKeys.Contains(kv.Key)) continue;
            switch (kv.Value)
            {
                case bool b:   WriteAssignment(sb, kv.Key, b, indent); break;
                case int i:    WriteAssignment(sb, kv.Key, i, indent); break;
                case long l:   WriteAssignment(sb, kv.Key, l, indent); break;
                case double d: WriteAssignment(sb, kv.Key, d, indent); break;
                case float f:  WriteAssignment(sb, kv.Key, (double)f, indent); break;
                case string s: WriteAssignment(sb, kv.Key, s, indent); break;
            }
        }
    }

    private static void WriteUdmfFlags(StringBuilder sb, HashSet<string> flags)
    {
        // Skip stashed "argN=K" pseudo-flags - args are emitted from the typed Args[] field.
        foreach (var flag in flags)
        {
            if (flag.StartsWith("arg", System.StringComparison.OrdinalIgnoreCase) && flag.Contains('='))
                continue;
            WriteAssignment(sb, flag, true, indent: true);
        }
    }

    private static void WriteUnknownUdmfData(StringBuilder sb, IReadOnlyList<UnknownUdmfEntry> entries, int indentLevel)
    {
        foreach (var entry in entries)
        {
            if (entry.IsCollection)
            {
                AppendIndent(sb, indentLevel);
                sb.Append(entry.Key).AppendLine();
                AppendIndent(sb, indentLevel);
                sb.AppendLine("{");
                WriteUnknownUdmfData(sb, entry.Children, indentLevel + 1);
                AppendIndent(sb, indentLevel);
                sb.AppendLine("}");
            }
            else
            {
                WriteUnknownAssignment(sb, entry, indentLevel);
            }
        }
    }

    private static void WriteUnknownAssignment(StringBuilder sb, UnknownUdmfEntry entry, int indentLevel)
    {
        AppendIndent(sb, indentLevel);
        sb.Append(entry.Key).Append(" = ");
        switch (entry.Value)
        {
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case int i:
                sb.Append(i.ToString(CultureInfo.InvariantCulture));
                break;
            case long l:
                sb.Append(l.ToString(CultureInfo.InvariantCulture));
                break;
            case double d:
                sb.Append(FormatDouble(d));
                break;
            case float f:
                sb.Append(FormatDouble(f));
                break;
            case string s:
                sb.Append('"').Append(EscapeString(s)).Append('"');
                break;
            default:
                sb.Append('"').Append(EscapeString(entry.Value.ToString() ?? "")).Append('"');
                break;
        }
        sb.AppendLine(";");
    }

    private static void AppendIndent(StringBuilder sb, int indentLevel)
    {
        for (int i = 0; i < indentLevel; i++) sb.Append('\t');
    }

    private static string FormatDouble(double value)
        => value.ToString("0.0##############", CultureInfo.InvariantCulture);

    private static string NormalizeLineEndings(string text)
        => text.ReplaceLineEndings("\r\n");

    private static void WriteAssignment(StringBuilder sb, string key, string value, bool indent = false)
    {
        if (indent) sb.Append('\t');
        sb.Append(key).Append(" = \"").Append(EscapeString(value)).Append("\";");
        sb.AppendLine();
    }

    private static void WriteAssignment(StringBuilder sb, string key, int value, bool indent = false)
    {
        if (indent) sb.Append('\t');
        sb.Append(key).Append(" = ").Append(value.ToString(CultureInfo.InvariantCulture)).Append(';');
        sb.AppendLine();
    }

    private static void WriteAssignment(StringBuilder sb, string key, long value, bool indent = false)
    {
        if (indent) sb.Append('\t');
        sb.Append(key).Append(" = ").Append(value.ToString(CultureInfo.InvariantCulture)).Append(';');
        sb.AppendLine();
    }

    private static void WriteAssignment(StringBuilder sb, string key, double value, bool indent = false)
    {
        if (indent) sb.Append('\t');
        string formatted = FormatDouble(value);
        sb.Append(key).Append(" = ").Append(formatted).Append(';');
        sb.AppendLine();
    }

    private static void WriteAssignment(StringBuilder sb, string key, bool value, bool indent = false)
    {
        if (indent) sb.Append('\t');
        sb.Append(key).Append(" = ").Append(value ? "true" : "false").Append(';');
        sb.AppendLine();
    }

    private static void WriteIfNonzero(StringBuilder sb, string key, int value)
    {
        if (value != 0) WriteAssignment(sb, key, value, indent: true);
    }

    private static void WriteIfNotDefault(StringBuilder sb, string key, string value, string defaultValue)
    {
        if (!string.Equals(value, defaultValue, System.StringComparison.Ordinal))
            WriteAssignment(sb, key, value, indent: true);
    }

    private static string EscapeString(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:   sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private static Dictionary<T, int> BuildIndex<T>(IReadOnlyList<T> list) where T : class
    {
        var dict = new Dictionary<T, int>(list.Count, ReferenceEqualityComparer.Instance);
        for (int i = 0; i < list.Count; i++) dict[list[i]] = i;
        return dict;
    }
}
