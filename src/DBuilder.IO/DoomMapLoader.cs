// ABOUTME: Purpose-built Doom-format binary map loader for the minimal Map skeleton.
// ABOUTME: Subset of UDB's DoomMapSetIO read path - parses the five core lumps and decodes well-known Doom flag bits to canonical UDMF flag names. Skips game-config validation, BLOCKMAP/REJECT/NODES (auxiliary lumps), and the entire write path.

/*
 * Doom (1993) map binary format:
 *   VERTEXES  4-byte records:  int16 x, int16 y
 *   LINEDEFS 14-byte records:  int16 v1, v2, flags, special, tag; int16 sidedef[2]
 *   SIDEDEFS 30-byte records:  int16 offsetx, offsety; char[8] upper, lower, middle; int16 sector
 *   SECTORS  26-byte records:  int16 floorheight, ceilingheight; char[8] floortex, ceiltex; int16 light, special, tag
 *   THINGS   10-byte records:  int16 x, y, angle, type, flags
 *
 * The map is identified by a marker lump (e.g. "MAP01", "E1M1") followed by these lumps
 * in that order, possibly with BLOCKMAP/REJECT/NODES/SEGS/SSECTORS interspersed.
 * We scan forward from the marker rather than relying on fixed positions.
 */

using System.Collections.Generic;
using System.IO;
using System.Text;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public static class DoomMapLoader
{
    // Doom linedef flag bits (matches DOOM.WAD format)
    [System.Flags]
    public enum DoomLinedefFlags
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
    }

    // Doom thing flag bits
    [System.Flags]
    public enum DoomThingFlags
    {
        Skill1And2 = 0x0001,
        Skill3     = 0x0002,
        Skill4And5 = 0x0004,
        Ambush     = 0x0008, // "deaf"
        MultiOnly  = 0x0010, // not in single player
    }

    /// <summary>Loads a Doom-format map identified by the given marker lump name (e.g. "MAP01", "E1M1").</summary>
    /// <returns>The populated MapSet, or null if the WAD doesn't contain the required lumps.</returns>
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
            return null; // THINGS is optional - some maps have no things at all

        var map = new MapSet();

        DoomMapLoaderInternals.ReadVertexes(vertexesLump, map);
        DoomMapLoaderInternals.ReadSectors(sectorsLump, map);
        DoomMapLoaderInternals.ReadSidedefs(sidedefsLump, map);
        ReadLinedefs(linedefsLump, map);
        DoomMapLoaderInternals.RemoveUnattachedSidedefs(map);
        if (thingsLump != null) ReadThings(thingsLump, map);

        map.BuildIndexes();
        return map;
    }

    // Scans forward from the marker for one of the named map sub-lumps. Stops at the next marker (a zero-length lump),
    // which mimics how UDB's MapManager isolates a single map's lump range.
    private static Lump? FindMapLump(WAD wad, int markerIdx, string name)
    {
        for (int i = markerIdx + 1; i < wad.Lumps.Count; i++)
        {
            var l = wad.Lumps[i];
            if (l.Name == name) return l;
            // Stop scanning at the next zero-length marker lump that isn't a known map sublump.
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
        int n = System.Math.Min(bytes.Length / 14, DoomMapLoaderInternals.BinaryFormatElementLimit);
        var used = new HashSet<Sidedef>();
        for (int i = 0; i < n; i++)
        {
            short v1 = r.ReadInt16();
            short v2 = r.ReadInt16();
            short flags = r.ReadInt16();
            short special = r.ReadInt16();
            short tag = r.ReadInt16();
            short sideRight = r.ReadInt16();
            short sideLeft = r.ReadInt16();

            if (!DoomMapLoaderInternals.TryGetValidLinedefVertices(map, v1, v2, out var start, out var end))
                continue;

            var line = new Linedef(start, end)
            {
                Flags = flags,
                Action = special,
                Tag = tag,
            };

            DoomMapLoaderInternals.AttachSidedef(map, sideRight, line, front: true, used);
            DoomMapLoaderInternals.AttachSidedef(map, sideLeft, line, front: false, used);

            // Decode the well-known Doom bits to canonical UDMF flag names so the viewer
            // (and any UDMF-vs-binary-agnostic code) treats both formats identically.
            var df = (DoomLinedefFlags)flags;
            if (df.HasFlag(DoomLinedefFlags.Blocking))      line.UdmfFlags.Add("blocking");
            if (df.HasFlag(DoomLinedefFlags.BlockMonsters)) line.UdmfFlags.Add("blockmonsters");
            if (df.HasFlag(DoomLinedefFlags.TwoSided))      line.UdmfFlags.Add("twosided");
            if (df.HasFlag(DoomLinedefFlags.DontPegTop))    line.UdmfFlags.Add("dontpegtop");
            if (df.HasFlag(DoomLinedefFlags.DontPegBottom)) line.UdmfFlags.Add("dontpegbottom");
            if (df.HasFlag(DoomLinedefFlags.Secret))        line.UdmfFlags.Add("secret");
            if (df.HasFlag(DoomLinedefFlags.BlockSound))    line.UdmfFlags.Add("blocksound");
            if (df.HasFlag(DoomLinedefFlags.DontDraw))      line.UdmfFlags.Add("dontdraw");
            if (df.HasFlag(DoomLinedefFlags.Mapped))        line.UdmfFlags.Add("mapped");

            map.Linedefs.Add(line);
        }
    }

    private static void ReadThings(Lump lump, MapSet map)
    {
        byte[] bytes = lump.Stream.ReadAllBytes();
        using var r = new BinaryReader(new MemoryStream(bytes));
        int n = bytes.Length / 10;
        for (int i = 0; i < n; i++)
        {
            short x = r.ReadInt16();
            short y = r.ReadInt16();
            short angle = r.ReadInt16();
            short type = r.ReadInt16();
            short flags = r.ReadInt16();

            var t = new Thing
            {
                Position = new Vector2D(x, y),
                Angle = angle,
                Type = type,
                Flags = flags,
            };

            var tf = (DoomThingFlags)flags;
            if (tf.HasFlag(DoomThingFlags.Skill1And2)) { t.UdmfFlags.Add("skill1"); t.UdmfFlags.Add("skill2"); }
            if (tf.HasFlag(DoomThingFlags.Skill3))       t.UdmfFlags.Add("skill3");
            if (tf.HasFlag(DoomThingFlags.Skill4And5)) { t.UdmfFlags.Add("skill4"); t.UdmfFlags.Add("skill5"); }
            if (tf.HasFlag(DoomThingFlags.Ambush))       t.UdmfFlags.Add("ambush");
            // Doom's MultiOnly bit is the inverse of "single" - set "single" only when the bit is clear.
            if (!tf.HasFlag(DoomThingFlags.MultiOnly)) { t.UdmfFlags.Add("single"); }
            t.UdmfFlags.Add("dm");          // Doom-format things always available in DM/coop
            t.UdmfFlags.Add("coop");

            map.Things.Add(t);
        }
    }

}
