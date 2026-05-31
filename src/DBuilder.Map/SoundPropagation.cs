// ABOUTME: Doom-style sound propagation - which sectors a noise in a start sector reaches through two-sided lines.
// ABOUTME: Mirrors P_RecursiveSound: sound flows freely, but crosses at most one sound-blocking line (level 2).

using System;
using System.Collections.Generic;

namespace DBuilder.Map;

public sealed record SoundPropagationDomain(
    IReadOnlySet<Sector> Sectors,
    IReadOnlySet<Sector> AdjacentSectors,
    IReadOnlySet<Linedef> BlockingLinedefs);

public sealed class SoundPropagationModeModel
{
    private readonly Dictionary<Sector, SoundPropagationDomain> sectorDomains;
    private readonly IReadOnlyList<SoundPropagationDomain> domains;
    private readonly IReadOnlySet<Linedef> blockingLinedefs;

    internal SoundPropagationModeModel(
        Dictionary<Sector, SoundPropagationDomain> sectorDomains,
        IReadOnlyList<SoundPropagationDomain> domains,
        IReadOnlySet<Linedef> blockingLinedefs)
    {
        this.sectorDomains = sectorDomains;
        this.domains = domains;
        this.blockingLinedefs = blockingLinedefs;
    }

    public IReadOnlyList<SoundPropagationDomain> Domains => domains;
    public IReadOnlyDictionary<Sector, SoundPropagationDomain> SectorDomains => sectorDomains;
    public IReadOnlySet<Linedef> BlockingLinedefs => blockingLinedefs;

    public SoundPropagationDomain? GetDomain(Sector sector)
        => sectorDomains.TryGetValue(sector, out SoundPropagationDomain? domain) ? domain : null;

    public IReadOnlySet<Sector> GetAffectedSectors(Sector highlighted)
    {
        if (!sectorDomains.TryGetValue(highlighted, out SoundPropagationDomain? domain))
            return new HashSet<Sector>(ReferenceEqualityComparer.Instance);

        var affected = new HashSet<Sector>(domain.Sectors, ReferenceEqualityComparer.Instance);
        foreach (Sector adjacent in domain.AdjacentSectors)
        {
            if (!sectorDomains.TryGetValue(adjacent, out SoundPropagationDomain? adjacentDomain)) continue;
            affected.UnionWith(adjacentDomain.Sectors);
        }

        return affected;
    }

    public IReadOnlyList<Thing> GetHuntingThings(Sector highlighted, IEnumerable<Thing>? visibleThings = null, bool udmf = false)
    {
        IReadOnlySet<Sector> affected = GetAffectedSectors(highlighted);
        var hunting = new List<Thing>();

        foreach (Thing thing in visibleThings ?? Array.Empty<Thing>())
        {
            if (IsAmbushThing(thing, udmf)) continue;
            if (thing.Sector != null && affected.Contains(thing.Sector)) hunting.Add(thing);
        }

        return hunting;
    }

    private static bool IsAmbushThing(Thing thing, bool udmf)
        => udmf ? thing.IsFlagSet("ambush") : (thing.Flags & SoundPropagation.DefaultAmbushBit) != 0;
}

public static class SoundPropagation
{
    /// <summary>The Doom ML_SOUNDBLOCK linedef flag bit (block sound).</summary>
    public const int DefaultSoundBlockBit = 64;

    /// <summary>The Doom MTF_AMBUSH thing flag bit, used by UDB to exclude deaf monsters from hunting lists.</summary>
    public const int DefaultAmbushBit = 8;

    public const string DefaultUdmfSoundBlockFlag = "blocksound";

    public static SoundPropagationModeModel BuildModeModel(
        MapSet map,
        int soundBlockBit = DefaultSoundBlockBit,
        string soundBlockFlag = DefaultUdmfSoundBlockFlag,
        bool udmf = false)
    {
        var sectorDomains = new Dictionary<Sector, SoundPropagationDomain>(ReferenceEqualityComparer.Instance);
        var domains = new List<SoundPropagationDomain>();
        var blockingLinedefs = new HashSet<Linedef>(ReferenceEqualityComparer.Instance);

        foreach (Linedef linedef in map.Linedefs)
        {
            if (IsSoundBlocking(linedef, soundBlockBit, soundBlockFlag, udmf))
                blockingLinedefs.Add(linedef);
        }

        foreach (Sector sector in map.Sectors)
        {
            if (sectorDomains.ContainsKey(sector)) continue;

            SoundPropagationDomain domain = CreateDomain(sector, soundBlockBit, soundBlockFlag, udmf);
            foreach (Sector domainSector in domain.Sectors) sectorDomains[domainSector] = domain;
            domains.Add(domain);
        }

        return new SoundPropagationModeModel(
            sectorDomains,
            domains.AsReadOnly(),
            blockingLinedefs);
    }

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

    public static bool IsSoundBlocking(Linedef line, int soundBlockBit = DefaultSoundBlockBit, string soundBlockFlag = DefaultUdmfSoundBlockFlag, bool udmf = false)
        => udmf ? line.IsFlagSet(soundBlockFlag) : (line.Flags & soundBlockBit) != 0;

    private static SoundPropagationDomain CreateDomain(Sector source, int soundBlockBit, string soundBlockFlag, bool udmf)
    {
        var sectors = new HashSet<Sector>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<Sector>();
        var queued = new HashSet<Sector>(ReferenceEqualityComparer.Instance);
        var blockingLines = new HashSet<Linedef>(ReferenceEqualityComparer.Instance);

        queue.Enqueue(source);
        queued.Add(source);

        while (queue.Count > 0)
        {
            Sector sector = queue.Dequeue();

            foreach (Sidedef side in sector.Sidedefs)
            {
                Linedef? line = side.Line;
                if (line?.Front == null || line.Back == null) continue;

                bool blocksSound = IsSoundBlocking(line, soundBlockBit, soundBlockFlag, udmf);
                if (blocksSound && side.Other != null) blockingLines.Add(line);
                if (side.Other == null || blocksSound || IsBlockedByHeight(line)) continue;

                Sector? opposite = side.Other.Sector;
                if (opposite == null || sectors.Contains(opposite) || queued.Contains(opposite)) continue;

                queue.Enqueue(opposite);
                queued.Add(opposite);
            }

            sectors.Add(sector);
        }

        var adjacentSectors = new HashSet<Sector>(ReferenceEqualityComparer.Instance);
        foreach (Linedef line in blockingLines)
        {
            if (IsBlockedByHeight(line)) continue;
            if (line.Front?.Sector != null && !sectors.Contains(line.Front.Sector)) adjacentSectors.Add(line.Front.Sector);
            if (line.Back?.Sector != null && !sectors.Contains(line.Back.Sector)) adjacentSectors.Add(line.Back.Sector);
        }

        return new SoundPropagationDomain(
            sectors,
            adjacentSectors,
            blockingLines);
    }
}
