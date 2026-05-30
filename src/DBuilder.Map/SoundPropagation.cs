// ABOUTME: Doom-style sound propagation - which sectors a noise in a start sector reaches through two-sided lines.
// ABOUTME: Mirrors P_RecursiveSound: sound flows freely, but crosses at most one sound-blocking line (level 2).

using System.Collections.Generic;

namespace DBuilder.Map;

public static class SoundPropagation
{
    /// <summary>The Doom ML_SOUNDBLOCK linedef flag bit (block sound).</summary>
    public const int DefaultSoundBlockBit = 64;

    /// <summary>
    /// Returns every sector reachable by sound from <paramref name="start"/>, mapped to its level: 1 = heard
    /// directly, 2 = heard only after crossing one sound-blocking line. Requires <see cref="MapSet.BuildIndexes"/>.
    /// </summary>
    public static Dictionary<Sector, int> Reachable(MapSet map, Sector start, int soundBlockBit = DefaultSoundBlockBit)
    {
        var traversed = new Dictionary<Sector, int>(ReferenceEqualityComparer.Instance);
        Recurse(start, 0);
        return traversed;

        void Recurse(Sector sec, int soundblocks)
        {
            int level = soundblocks + 1; // soundtraversed
            if (traversed.TryGetValue(sec, out int prev) && prev <= level) return;
            traversed[sec] = level;

            foreach (var sd in sec.Sidedefs)
            {
                var line = sd.Line;
                if (line?.Front == null || line.Back == null) continue; // single-sided lines block sound entirely
                if (IsBlockedByHeight(line)) continue;

                var other = ReferenceEquals(line.Front.Sector, sec) ? line.Back.Sector : line.Front.Sector;
                if (other == null || ReferenceEquals(other, sec)) continue;

                if ((line.Flags & soundBlockBit) != 0)
                {
                    if (soundblocks == 0) Recurse(other, 1); // sound passes one block line, then stops at the next
                }
                else
                {
                    Recurse(other, soundblocks);
                }
            }
        }
    }

    /// <summary>True when a two-sided linedef has no vertical sound opening between its sectors.</summary>
    public static bool IsBlockedByHeight(Linedef line)
    {
        if (line.Front?.Sector == null || line.Back?.Sector == null) return false;

        Sector front = line.Front.Sector;
        Sector back = line.Back.Sector;
        return front.CeilHeight <= back.FloorHeight
            || front.FloorHeight >= back.CeilHeight
            || back.CeilHeight <= back.FloorHeight
            || front.CeilHeight <= front.FloorHeight;
    }
}
