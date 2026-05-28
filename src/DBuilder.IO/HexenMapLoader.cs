// ABOUTME: Hexen/ZDoom binary map loader - 16-byte linedef records with args[5] and 20-byte thing records with tid/z/args.
// ABOUTME: SIDEDEFS/SECTORS/VERTEXES lumps reuse the Doom format. The BEHAVIOR lump (compiled ACS) is detected but not parsed.

/*
 * Hexen/ZDoom binary format:
 *   VERTEXES  (Doom)  4-byte:  int16 x, int16 y
 *   SECTORS   (Doom) 26-byte:  same as Doom
 *   SIDEDEFS  (Doom) 30-byte:  same as Doom
 *   LINEDEFS  (Hexen) 16-byte: uint16 v1, v2, flags; byte action, args[5]; uint16 s1, s2
 *   THINGS    (Hexen) 20-byte: uint16 tid; int16 x, y, z, angle, type; uint16 flags;
 *                              byte action, args[5]
 *
 * A map is Hexen-format when its marker lump is followed by a BEHAVIOR lump (compiled ACS bytecode).
 */

using System.Collections.Generic;
using System.IO;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public static class HexenMapLoader
{
    // Hexen linedef flag bits.  Shares the Doom low byte; high byte adds activation/passthru/translucent.
    [System.Flags]
    public enum HexenLinedefFlags
    {
        Blocking      = 0x0001,
        BlockMonsters = 0x0002,
        TwoSided      = 0x0004,
        DontPegTop    = 0x0008,
        DontPegBottom = 0x0010,
        Secret        = 0x0020,
        BlockSound    = 0x0040,
        DontDraw      = 0x0080,
        Mapped        = 0x0100,
        Repeats       = 0x0200,
        // 0x1C00 = activation type (SPAC, 3 bits encoding cross/use/impact/push/etc.)
        ActivationMask = 0x1C00,
        BlockAll      = 0x8000,
    }

    // Hexen thing flag bits.  Skill bits are Doom-compatible; class/single/dm/coop are explicit (unlike Doom's inverted MultiOnly).
    [System.Flags]
    public enum HexenThingFlags
    {
        Skill1And2 = 0x0001,
        Skill3     = 0x0002,
        Skill4And5 = 0x0004,
        Ambush     = 0x0008,
        Dormant    = 0x0010,
        Class1     = 0x0020, // Fighter
        Class2     = 0x0040, // Cleric
        Class3     = 0x0080, // Mage
        Single     = 0x0100,
        Coop       = 0x0200,
        Dm         = 0x0400,
    }

    /// <summary>True when the WAD's map at <paramref name="markerLumpName"/> looks Hexen-format (has BEHAVIOR lump).</summary>
    public static bool IsHexenFormat(WAD wad, string markerLumpName)
    {
        int markerIdx = wad.FindLumpIndex(markerLumpName);
        if (markerIdx < 0) return false;
        for (int i = markerIdx + 1; i < wad.Lumps.Count; i++)
        {
            string nm = wad.Lumps[i].Name;
            if (nm == "BEHAVIOR") return true;
            if (nm == "TEXTMAP") return false; // UDMF
            // Bail out of the scan after the typical map block size.
            if (i - markerIdx > 12) break;
        }
        return false;
    }

    /// <summary>Loads a Hexen-format map identified by the given marker lump name.</summary>
    public static MapSet? Load(WAD wad, string markerLumpName)
    {
        int markerIdx = wad.FindLumpIndex(markerLumpName);
        if (markerIdx < 0) return null;

        Lump? vertexesLump = FindMapLump(wad, markerIdx, "VERTEXES");
        Lump? linedefsLump = FindMapLump(wad, markerIdx, "LINEDEFS");
        Lump? sidedefsLump = FindMapLump(wad, markerIdx, "SIDEDEFS");
        Lump? sectorsLump  = FindMapLump(wad, markerIdx, "SECTORS");
        Lump? thingsLump   = FindMapLump(wad, markerIdx, "THINGS");

        if (vertexesLump == null || linedefsLump == null || sidedefsLump == null || sectorsLump == null)
            return null;

        var map = new MapSet();

        // VERTEXES, SECTORS, SIDEDEFS are identical to Doom format.
        DoomMapLoaderInternals.ReadVertexes(vertexesLump, map);
        DoomMapLoaderInternals.ReadSectors(sectorsLump, map);
        DoomMapLoaderInternals.ReadSidedefs(sidedefsLump, map);

        ReadLinedefs(linedefsLump, map);
        DoomMapLoaderInternals.RemoveUnattachedSidedefs(map);
        if (thingsLump != null) ReadThings(thingsLump, map);

        map.BuildIndexes();
        return map;
    }

    private static Lump? FindMapLump(WAD wad, int markerIdx, string name)
    {
        for (int i = markerIdx + 1; i < wad.Lumps.Count; i++)
        {
            var l = wad.Lumps[i];
            if (l.Name == name) return l;
            // Stop at the next zero-length marker that isn't a known map sub-lump
            if (l.Length == 0 && !IsKnownMapSubLump(l.Name)) break;
        }
        return null;
    }

    private static bool IsKnownMapSubLump(string name) => name switch
    {
        "VERTEXES" or "LINEDEFS" or "SIDEDEFS" or "SECTORS" or "THINGS"
            or "BLOCKMAP" or "REJECT" or "NODES" or "SEGS" or "SSECTORS"
            or "BEHAVIOR" or "SCRIPTS" or "TEXTMAP" or "ENDMAP" or "DIALOGUE" => true,
        _ => false,
    };

    private static void ReadLinedefs(Lump lump, MapSet map)
    {
        byte[] bytes = lump.Stream.ReadAllBytes();
        using var r = new BinaryReader(new MemoryStream(bytes));
        int n = bytes.Length / 16;
        var used = new HashSet<Sidedef>();
        for (int i = 0; i < n; i++)
        {
            int v1 = r.ReadUInt16();
            int v2 = r.ReadUInt16();
            ushort flags = r.ReadUInt16();
            byte action = r.ReadByte();
            byte a0 = r.ReadByte(), a1 = r.ReadByte(), a2 = r.ReadByte(), a3 = r.ReadByte(), a4 = r.ReadByte();
            int s1 = r.ReadUInt16();
            int s2 = r.ReadUInt16();

            if (!DoomMapLoaderInternals.TryGetValidLinedefVertices(map, v1, v2, out var start, out var end))
                continue;

            var line = new Linedef(start, end)
            {
                Flags = flags,
                Action = action,
                Tag = 0, // Hexen has no tag field; tag lives in args
            };
            line.Args[0] = a0; line.Args[1] = a1; line.Args[2] = a2; line.Args[3] = a3; line.Args[4] = a4;

            // Also stashed as named UdmfFlags so existing consumers that depend on this
            // representation keep working until they migrate to Linedef.Args.
            if (a0 != 0) line.UdmfFlags.Add($"arg0={a0}");
            if (a1 != 0) line.UdmfFlags.Add($"arg1={a1}");
            if (a2 != 0) line.UdmfFlags.Add($"arg2={a2}");
            if (a3 != 0) line.UdmfFlags.Add($"arg3={a3}");
            if (a4 != 0) line.UdmfFlags.Add($"arg4={a4}");

            // Hexen reuses ushort.MaxValue (-1 as unsigned) for "no sidedef". Shared sidedefs are unpacked.
            if (s1 != 0xFFFF) DoomMapLoaderInternals.AttachSidedef(map, s1, line, front: true, used);
            if (s2 != 0xFFFF) DoomMapLoaderInternals.AttachSidedef(map, s2, line, front: false, used);

            var hf = (HexenLinedefFlags)flags;
            if (hf.HasFlag(HexenLinedefFlags.Blocking))      line.UdmfFlags.Add("blocking");
            if (hf.HasFlag(HexenLinedefFlags.BlockMonsters)) line.UdmfFlags.Add("blockmonsters");
            if (hf.HasFlag(HexenLinedefFlags.TwoSided))      line.UdmfFlags.Add("twosided");
            if (hf.HasFlag(HexenLinedefFlags.DontPegTop))    line.UdmfFlags.Add("dontpegtop");
            if (hf.HasFlag(HexenLinedefFlags.DontPegBottom)) line.UdmfFlags.Add("dontpegbottom");
            if (hf.HasFlag(HexenLinedefFlags.Secret))        line.UdmfFlags.Add("secret");
            if (hf.HasFlag(HexenLinedefFlags.BlockSound))    line.UdmfFlags.Add("blocksound");
            if (hf.HasFlag(HexenLinedefFlags.DontDraw))      line.UdmfFlags.Add("dontdraw");
            if (hf.HasFlag(HexenLinedefFlags.Mapped))        line.UdmfFlags.Add("mapped");
            if (hf.HasFlag(HexenLinedefFlags.Repeats))       line.UdmfFlags.Add("repeatspecial");
            if (hf.HasFlag(HexenLinedefFlags.BlockAll))      line.UdmfFlags.Add("blockall");

            // SPAC activation kind (3 bits)
            int spac = (flags & (int)HexenLinedefFlags.ActivationMask) >> 10;
            line.UdmfFlags.Add(spac switch
            {
                0 => "playercross",
                1 => "playeruse",
                2 => "monstercross",
                3 => "impact",
                4 => "playerpush",
                5 => "missilecross",
                6 => "anycross",
                _ => "useract",
            });

            map.Linedefs.Add(line);
        }
    }

    private static void ReadThings(Lump lump, MapSet map)
    {
        byte[] bytes = lump.Stream.ReadAllBytes();
        using var r = new BinaryReader(new MemoryStream(bytes));
        int n = bytes.Length / 20;
        for (int i = 0; i < n; i++)
        {
            ushort tid = r.ReadUInt16();
            short x = r.ReadInt16();
            short y = r.ReadInt16();
            short z = r.ReadInt16();
            short angle = r.ReadInt16();
            short type = r.ReadInt16();
            ushort flags = r.ReadUInt16();
            byte action = r.ReadByte();
            byte ta0 = r.ReadByte(), ta1 = r.ReadByte(), ta2 = r.ReadByte(), ta3 = r.ReadByte(), ta4 = r.ReadByte();

            var t = new Thing
            {
                Position = new Vector2D(x, y),
                Height = z,
                Angle = angle,
                Type = type,
                Flags = flags,
                Tag = tid,
                Action = action,
            };
            t.Args[0] = ta0; t.Args[1] = ta1; t.Args[2] = ta2; t.Args[3] = ta3; t.Args[4] = ta4;

            var tf = (HexenThingFlags)flags;
            if (tf.HasFlag(HexenThingFlags.Skill1And2)) { t.UdmfFlags.Add("skill1"); t.UdmfFlags.Add("skill2"); }
            if (tf.HasFlag(HexenThingFlags.Skill3))       t.UdmfFlags.Add("skill3");
            if (tf.HasFlag(HexenThingFlags.Skill4And5)) { t.UdmfFlags.Add("skill4"); t.UdmfFlags.Add("skill5"); }
            if (tf.HasFlag(HexenThingFlags.Ambush))       t.UdmfFlags.Add("ambush");
            if (tf.HasFlag(HexenThingFlags.Dormant))      t.UdmfFlags.Add("dormant");
            if (tf.HasFlag(HexenThingFlags.Class1))       t.UdmfFlags.Add("class1");
            if (tf.HasFlag(HexenThingFlags.Class2))       t.UdmfFlags.Add("class2");
            if (tf.HasFlag(HexenThingFlags.Class3))       t.UdmfFlags.Add("class3");
            if (tf.HasFlag(HexenThingFlags.Single))       t.UdmfFlags.Add("single");
            if (tf.HasFlag(HexenThingFlags.Coop))         t.UdmfFlags.Add("coop");
            if (tf.HasFlag(HexenThingFlags.Dm))           t.UdmfFlags.Add("dm");

            map.Things.Add(t);
        }
    }
}
