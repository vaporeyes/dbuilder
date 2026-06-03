// ABOUTME: Doom-style sound propagation - which sectors a noise in a start sector reaches through two-sided lines.
// ABOUTME: Mirrors P_RecursiveSound: sound flows freely, but crosses at most one sound-blocking line (level 2).

using System;
using System.Collections.Generic;
using System.Linq;
using DBuilder.Geometry;

namespace DBuilder.Map;

public sealed record SoundPropagationDomain(
    IReadOnlySet<Sector> Sectors,
    IReadOnlySet<Sector> AdjacentSectors,
    IReadOnlySet<Linedef> BlockingLinedefs);

public sealed record SoundLeakPath(
    IReadOnlyList<Vector2D> Points,
    IReadOnlyList<Linedef> Linedefs,
    IReadOnlyList<Linedef> BlockingLinedefs);

public sealed record SoundPropagationActionDescriptor(
    string Id,
    string Title,
    string Category,
    string Description,
    bool AllowKeys,
    bool AllowMouse,
    bool AllowScroll);

public sealed record SoundPropagationColorField(
    string Key,
    string Label,
    uint DefaultColor);

public sealed record SoundPropagationColorSettings(
    uint HighlightColor,
    uint Level1Color,
    uint Level2Color,
    uint NoSoundColor,
    uint BlockSoundColor,
    IReadOnlyList<uint> DistinctDomainColors)
{
    public const string HighlightColorKey = "highlightcolor";
    public const string Level1ColorKey = "level1color";
    public const string Level2ColorKey = "level2color";
    public const string NoSoundColorKey = "nosoundcolor";
    public const string BlockSoundColorKey = "blocksoundcolor";
    public const string ColorConfigurationTitle = "Color Configuration";
    public const string ResetColorsText = "Reset colors";

    public static SoundPropagationColorSettings Default { get; } = new(
        0xFF00C000,
        0xFF00FF00,
        0xFFFFFF00,
        0xFFA0A0A0,
        0xFFFF0000,
        new[]
        {
            0xFF84D5A4u,
            0xFFC059CBu,
            0xFFD0533Du,
            0xFF415354u,
            0xFFCEA953u,
            0xFF91D44Bu,
            0xFFCD5B89u,
            0xFFA8B6C0u,
            0xFF797ECBu,
            0xFF567539u,
            0xFF72422Fu,
            0xFF5D3762u,
            0xFFFFED6Fu,
            0xFFCCEBC5u,
            0xFFBC80BDu,
            0xFFD9D9D9u,
            0xFFFCCDE5u,
            0xFF80B1D3u,
            0xFFFDB462u,
            0xFFB3DE69u,
            0xFFFB8072u,
            0xFFBEBADAu,
            0xFFFFFFB3u,
            0xFF8DD3C7u,
        });

    public static SoundPropagationActionDescriptor ColorConfigurationAction { get; } = new(
        "soundpropagationcolorconfiguration",
        "Configure colors",
        "soundpropagationmode",
        "Configure colors for sound propagation mode",
        AllowKeys: true,
        AllowMouse: true,
        AllowScroll: true);

    public static IReadOnlyList<SoundPropagationColorField> ColorConfigurationFields { get; } =
    [
        new(HighlightColorKey, "Highlight color:", Default.HighlightColor),
        new(Level1ColorKey, "Level 1 color:", Default.Level1Color),
        new(Level2ColorKey, "Level 2 color:", Default.Level2Color),
        new(NoSoundColorKey, "No sound color:", Default.NoSoundColor),
        new(BlockSoundColorKey, "Block sound color:", Default.BlockSoundColor),
    ];

    public static SoundPropagationColorSettings FromSettings(IReadOnlyDictionary<string, object?> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        SoundPropagationColorSettings fallback = Default;
        return fallback with
        {
            HighlightColor = ReadColor(settings, HighlightColorKey, fallback.HighlightColor),
            Level1Color = ReadColor(settings, Level1ColorKey, fallback.Level1Color),
            Level2Color = ReadColor(settings, Level2ColorKey, fallback.Level2Color),
            NoSoundColor = ReadColor(settings, NoSoundColorKey, fallback.NoSoundColor),
            BlockSoundColor = ReadColor(settings, BlockSoundColorKey, fallback.BlockSoundColor),
        };
    }

    public IReadOnlyDictionary<string, object> ToSettings()
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [HighlightColorKey] = unchecked((int)HighlightColor),
            [Level1ColorKey] = unchecked((int)Level1Color),
            [Level2ColorKey] = unchecked((int)Level2Color),
            [NoSoundColorKey] = unchecked((int)NoSoundColor),
            [BlockSoundColorKey] = unchecked((int)BlockSoundColor),
        };

    public uint DomainColorForIndex(int index)
    {
        if (DistinctDomainColors.Count == 0) return Level1Color;
        int wrapped = index % DistinctDomainColors.Count;
        if (wrapped < 0) wrapped += DistinctDomainColors.Count;
        return DistinctDomainColors[wrapped];
    }

    private static uint ReadColor(IReadOnlyDictionary<string, object?> settings, string key, uint fallback)
    {
        if (!settings.TryGetValue(key, out object? value)) return fallback;
        return value switch
        {
            uint color => color,
            int color => unchecked((uint)color),
            long color when color is >= int.MinValue and <= uint.MaxValue => unchecked((uint)color),
            string text when uint.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out uint color) => color,
            _ => fallback,
        };
    }
}

public readonly record struct SoundEnvironmentReverb(int Arg0, int Arg1);

public sealed record SoundEnvironmentInfo(
    IReadOnlySet<Sector> Sectors,
    IReadOnlyList<Thing> Things,
    IReadOnlyList<Linedef> BoundaryLinedefs,
    uint Color,
    int Id,
    string Name);

public sealed record SoundEnvironmentRow(
    string Text,
    int Depth,
    bool Warning,
    uint Color,
    SoundEnvironmentInfo? Environment,
    Thing? Thing,
    Linedef? Linedef);

public sealed record SoundEnvironmentModeModel(
    IReadOnlyList<SoundEnvironmentInfo> Environments,
    IReadOnlySet<Sector> UnassignedSectors,
    IReadOnlySet<Linedef> BoundaryLinedefs)
{
    public static SoundPropagationActionDescriptor ModeAction { get; } = new(
        "soundenvironmentmode",
        "Sound environment mode",
        "modes",
        "Shows ZDoom sound environments",
        AllowKeys: true,
        AllowMouse: true,
        AllowScroll: true);

    public const string DockerId = "soundenvironments";
    public const string DockerTitle = "Sound Environments";
    public const string ShowWarningsOnlyText = "Show nodes with warnings only";

    public IReadOnlyList<SoundEnvironmentRow> Rows(bool udmf = false, bool warningsOnly = false)
    {
        var rows = new List<SoundEnvironmentRow>();
        foreach (SoundEnvironmentInfo environment in Environments.OrderBy(e => e.Id))
        {
            bool activeThingWarning = ActiveThingCount(environment, udmf) > 1;
            List<Linedef> warningLines = environment.BoundaryLinedefs.Where(line => LineHasWarning(environment, line)).ToList();
            int rowWarnings = (activeThingWarning ? environment.Things.Count(thing => !SoundPropagation.ThingDormant(thing, udmf)) : 0)
                + warningLines.Count;
            if (warningsOnly && rowWarnings == 0) continue;

            rows.Add(new SoundEnvironmentRow(
                Text: $"{environment.Name}  Sectors: {environment.Sectors.Count}, things: {environment.Things.Count}, boundary lines: {environment.BoundaryLinedefs.Count}",
                Depth: 0,
                Warning: rowWarnings > 0,
                Color: environment.Color,
                Environment: environment,
                Thing: null,
                Linedef: null));

            foreach (Thing thing in environment.Things)
            {
                bool warning = activeThingWarning && !SoundPropagation.ThingDormant(thing, udmf);
                if (warningsOnly && !warning) continue;
                string dormant = SoundPropagation.ThingDormant(thing, udmf) ? " (dormant)" : "";
                rows.Add(new SoundEnvironmentRow(
                    Text: $"Thing type {thing.Type}{dormant}",
                    Depth: 1,
                    Warning: warning,
                    Color: environment.Color,
                    Environment: environment,
                    Thing: thing,
                    Linedef: null));
            }

            foreach (Linedef line in environment.BoundaryLinedefs)
            {
                bool warning = LineHasWarning(environment, line);
                if (warningsOnly && !warning) continue;
                rows.Add(new SoundEnvironmentRow(
                    Text: "Boundary linedef",
                    Depth: 1,
                    Warning: warning,
                    Color: environment.Color,
                    Environment: environment,
                    Thing: null,
                    Linedef: line));
            }
        }

        return rows;
    }

    public int WarningCount(bool udmf = false)
    {
        int warnings = 0;
        foreach (SoundEnvironmentInfo environment in Environments)
        {
            bool activeThingWarning = ActiveThingCount(environment, udmf) > 1;
            if (activeThingWarning)
                warnings += environment.Things.Count(thing => !SoundPropagation.ThingDormant(thing, udmf));

            warnings += environment.BoundaryLinedefs.Count(line => LineHasWarning(environment, line));
        }

        return warnings;
    }

    public uint[] SectorOverlayColors(
        IReadOnlyList<Sector> sectors,
        SoundPropagationColorSettings? colors = null,
        SoundEnvironmentInfo? highlighted = null)
    {
        ArgumentNullException.ThrowIfNull(sectors);
        colors ??= SoundPropagationColorSettings.Default;

        var result = new uint[sectors.Count];
        var indexes = new Dictionary<Sector, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < sectors.Count; i++)
        {
            indexes[sectors[i]] = i;
            if (UnassignedSectors.Contains(sectors[i])) result[i] = colors.NoSoundColor;
        }

        foreach (SoundEnvironmentInfo environment in Environments)
        {
            uint color = ReferenceEquals(environment, highlighted) ? colors.HighlightColor : environment.Color;
            foreach (Sector sector in environment.Sectors)
            {
                if (indexes.TryGetValue(sector, out int index)) result[index] = color;
            }
        }

        return result;
    }

    private static int ActiveThingCount(SoundEnvironmentInfo environment, bool udmf)
        => environment.Things.Count(thing => !SoundPropagation.ThingDormant(thing, udmf));

    private static bool LineHasWarning(SoundEnvironmentInfo environment, Linedef line)
    {
        if (line.Front?.Sector == null || line.Back?.Sector == null) return true;
        return environment.Sectors.Contains(line.Front.Sector) && environment.Sectors.Contains(line.Back.Sector);
    }
}

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

    public uint[] SectorOverlayColors(
        IReadOnlyList<Sector> sectors,
        Sector? highlightedSector,
        SoundPropagationColorSettings? colors = null)
    {
        colors ??= SoundPropagationColorSettings.Default;
        var result = new uint[sectors.Count];

        if (highlightedSector == null || !sectorDomains.TryGetValue(highlightedSector, out SoundPropagationDomain? highlightedDomain))
        {
            for (int i = 0; i < sectors.Count; i++)
            {
                if (!sectorDomains.TryGetValue(sectors[i], out SoundPropagationDomain? domain)) continue;

                int domainIndex = 0;
                for (; domainIndex < domains.Count; domainIndex++)
                    if (ReferenceEquals(domains[domainIndex], domain)) break;

                result[i] = colors.DomainColorForIndex(domainIndex);
            }

            return result;
        }

        Array.Fill(result, colors.NoSoundColor);

        for (int i = 0; i < sectors.Count; i++)
        {
            Sector sector = sectors[i];
            if (highlightedDomain.Sectors.Contains(sector))
                result[i] = colors.Level1Color;
        }

        foreach (Sector adjacent in highlightedDomain.AdjacentSectors)
        {
            if (!sectorDomains.TryGetValue(adjacent, out SoundPropagationDomain? adjacentDomain)) continue;
            for (int i = 0; i < sectors.Count; i++)
                if (adjacentDomain.Sectors.Contains(sectors[i]))
                    result[i] = colors.Level2Color;
        }

        for (int i = 0; i < sectors.Count; i++)
            if (ReferenceEquals(sectors[i], highlightedSector))
                result[i] = colors.HighlightColor;

        return result;
    }

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

    public IReadOnlySet<Sector> GetLeakSearchSectors(Sector source)
    {
        if (!sectorDomains.TryGetValue(source, out SoundPropagationDomain? domain))
            return new HashSet<Sector>(ReferenceEqualityComparer.Instance);

        var sectors = new HashSet<Sector>(domain.Sectors, ReferenceEqualityComparer.Instance);
        foreach (Sector adjacent in domain.AdjacentSectors)
        {
            if (!sectorDomains.TryGetValue(adjacent, out SoundPropagationDomain? adjacentDomain)) continue;
            sectors.UnionWith(adjacentDomain.Sectors);
        }

        return sectors;
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
    public const string DefaultUdmfZoneBoundaryFlag = "zoneboundary";
    public const string DefaultUdmfDormantFlag = "dormant";
    public const string DefaultClassicDormantFlag = "14";
    public const int SoundEnvironmentThingType = 9048;
    public const string UnknownSoundEnvironmentName = "Unknown sound environment";

    public static SoundEnvironmentModeModel BuildSoundEnvironmentModel(
        MapSet map,
        IReadOnlyDictionary<string, SoundEnvironmentReverb>? reverbs = null,
        SoundPropagationColorSettings? colors = null,
        bool udmf = false)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));

        colors ??= SoundPropagationColorSettings.Default;
        var allSectors = new HashSet<Sector>(map.Sectors, ReferenceEqualityComparer.Instance);
        var checkedSectors = new HashSet<Sector>(ReferenceEqualityComparer.Instance);
        var remainingThings = GetSoundEnvironmentThings(map, allSectors).ToList();
        var environments = new List<SoundEnvironmentInfo>();
        var boundaryLinedefs = new HashSet<Linedef>(ReferenceEqualityComparer.Instance);
        var colorsByThing = new Dictionary<Thing, uint>(ReferenceEqualityComparer.Instance);
        var numbersByThing = new Dictionary<Thing, int>(ReferenceEqualityComparer.Instance);

        for (int i = 0; i < remainingThings.Count; i++)
        {
            Thing thing = remainingThings[i];
            int colorId = ((Math.Abs(thing.Args[0]) % 256) << 8) + Math.Abs(thing.Args[1]) % 256;
            colorsByThing[thing] = colors.DomainColorForIndex(colorId);
            numbersByThing[thing] = i + 1;
        }

        while (remainingThings.Count > 0)
        {
            Thing thing = remainingThings[0];
            if (thing.Sector == null) thing.DetermineSector(map);
            if (thing.Sector == null || !allSectors.Contains(thing.Sector))
            {
                remainingThings.RemoveAt(0);
                continue;
            }

            var sectorsToCheck = new Queue<Sector>();
            var environmentSectors = new HashSet<Sector>(ReferenceEqualityComparer.Instance);
            var environmentLines = new HashSet<Linedef>(ReferenceEqualityComparer.Instance);
            sectorsToCheck.Enqueue(thing.Sector);

            while (sectorsToCheck.Count > 0)
            {
                Sector sector = sectorsToCheck.Dequeue();
                if (checkedSectors.Contains(sector)) continue;

                environmentSectors.Add(sector);
                checkedSectors.Add(sector);
                allSectors.Remove(sector);

                foreach (Sidedef sidedef in sector.Sidedefs)
                {
                    Linedef line = sidedef.Line;
                    if (LinedefBlocksSoundEnvironment(line, udmf))
                    {
                        environmentLines.Add(line);
                        boundaryLinedefs.Add(line);
                        continue;
                    }

                    Sector? other = ReferenceEquals(line.Front?.Sector, sector) ? line.Back?.Sector : line.Front?.Sector;
                    if (other != null && !checkedSectors.Contains(other)) sectorsToCheck.Enqueue(other);
                }
            }

            List<Thing> environmentThings = GetSoundEnvironmentThings(map, environmentSectors)
                .OrderBy(map.Things.IndexOf)
                .ToList();
            foreach (Thing environmentThing in environmentThings)
                remainingThings.Remove(environmentThing);

            Thing colorThing = environmentThings.Count > 0 ? environmentThings[0] : thing;
            int id = numbersByThing.TryGetValue(colorThing, out int number) ? number : environments.Count + 1;
            environments.Add(new SoundEnvironmentInfo(
                environmentSectors,
                environmentThings,
                environmentLines.OrderBy(map.Linedefs.IndexOf).ToArray(),
                colorsByThing.TryGetValue(colorThing, out uint color) ? color : colors.DomainColorForIndex(0),
                id,
                SoundEnvironmentName(environmentThings, reverbs, udmf, id)));
        }

        return new SoundEnvironmentModeModel(environments, allSectors, boundaryLinedefs);
    }

    public static IReadOnlyList<Thing> GetSoundEnvironmentThings(MapSet map, IEnumerable<Sector> sectors)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (sectors == null) throw new ArgumentNullException(nameof(sectors));

        var sectorSet = new HashSet<Sector>(sectors, ReferenceEqualityComparer.Instance);
        var things = new List<Thing>();
        foreach (Thing thing in map.Things)
        {
            if (thing.Type != SoundEnvironmentThingType) continue;
            if (thing.Sector == null) thing.DetermineSector(map);
            if (thing.Sector != null && sectorSet.Contains(thing.Sector)) things.Add(thing);
        }

        return things;
    }

    public static bool LinedefBlocksSoundEnvironment(Linedef linedef, bool udmf = false)
    {
        if (linedef == null) throw new ArgumentNullException(nameof(linedef));
        if (udmf) return linedef.IsFlagSet(DefaultUdmfZoneBoundaryFlag);
        return linedef.Action == 121 && (linedef.Args[1] & 1) == 1;
    }

    public static bool ThingDormant(Thing thing, bool udmf = false)
    {
        if (thing == null) throw new ArgumentNullException(nameof(thing));
        return thing.IsFlagSet(udmf ? DefaultUdmfDormantFlag : DefaultClassicDormantFlag);
    }

    public static void SetThingDormant(Thing thing, bool dormant, bool udmf = false)
    {
        if (thing == null) throw new ArgumentNullException(nameof(thing));
        thing.SetFlag(udmf ? DefaultUdmfDormantFlag : DefaultClassicDormantFlag, dormant);
    }

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

    private static string SoundEnvironmentName(
        IReadOnlyList<Thing> things,
        IReadOnlyDictionary<string, SoundEnvironmentReverb>? reverbs,
        bool udmf,
        int fallbackId)
    {
        Thing? active = things.FirstOrDefault(thing => !ThingDormant(thing, udmf));
        if (active == null) return $"{UnknownSoundEnvironmentName} {fallbackId}";

        if (reverbs != null)
        {
            foreach ((string name, SoundEnvironmentReverb reverb) in reverbs)
            {
                if (reverb.Arg0 == active.Args[0] && reverb.Arg1 == active.Args[1])
                    return $"{name} ({active.Args[0]} {active.Args[1]})";
            }
        }

        return $"{UnknownSoundEnvironmentName} ({active.Args[0]} {active.Args[1]})";
    }

    /// <summary>
    /// Returns every sector reachable by sound from <paramref name="start"/>, mapped to its level: 1 = heard
    /// directly, 2 = heard only after crossing one sound-blocking line. Requires <see cref="MapSet.BuildIndexes"/>.
    /// </summary>
    public static Dictionary<Sector, int> Reachable(
        MapSet map,
        Sector start,
        int soundBlockBit = DefaultSoundBlockBit,
        string soundBlockFlag = DefaultUdmfSoundBlockFlag,
        bool udmf = false)
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

                if (IsSoundBlocking(line, soundBlockBit, soundBlockFlag, udmf))
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

    public static bool ToggleSoundBlocking(Linedef line, int soundBlockBit = DefaultSoundBlockBit, string soundBlockFlag = DefaultUdmfSoundBlockFlag, bool udmf = false)
    {
        ArgumentNullException.ThrowIfNull(line);
        bool next = !IsSoundBlocking(line, soundBlockBit, soundBlockFlag, udmf);
        if (udmf) line.SetFlag(soundBlockFlag, next);
        else if (next) line.Flags |= soundBlockBit;
        else line.Flags &= ~soundBlockBit;
        return next;
    }

    public static SoundLeakPath? FindLeakPath(
        Sector source,
        Vector2D sourcePosition,
        Sector destination,
        Vector2D destinationPosition,
        IReadOnlySet<Sector> sectors,
        int soundBlockBit = DefaultSoundBlockBit,
        string soundBlockFlag = DefaultUdmfSoundBlockFlag,
        bool udmf = false)
    {
        if (!sectors.Contains(source) || !sectors.Contains(destination))
            throw new ArgumentException("Sound propagation domain does not contain both the start and end sectors");

        var start = SoundLeakNode.ForPoint(sourcePosition);
        var end = SoundLeakNode.ForPoint(destinationPosition);
        var nodesByLine = new Dictionary<Linedef, SoundLeakNode>(ReferenceEqualityComparer.Instance);
        var nodes = new List<SoundLeakNode> { start, end };

        foreach (Sector sector in sectors)
        {
            foreach (Sidedef side in sector.Sidedefs)
            {
                Linedef? line = side.Line;
                if (line == null || !CanLeakPassThrough(line, sectors)) continue;
                if (nodesByLine.ContainsKey(line)) continue;

                SoundLeakNode node = SoundLeakNode.ForLine(
                    line,
                    end.Position,
                    IsSoundBlocking(line, soundBlockBit, soundBlockFlag, udmf));
                nodesByLine.Add(line, node);
                nodes.Add(node);
            }
        }

        foreach ((Linedef line, SoundLeakNode node) in nodesByLine)
        {
            AddLeakNeighbors(line.Front?.Sector, line, node, nodesByLine, sectors);
            AddLeakNeighbors(line.Back?.Sector, line, node, nodesByLine, sectors);
        }

        AddEndpointNeighbors(source, start, nodesByLine, sectors);
        AddEndpointNeighbors(destination, end, nodesByLine, sectors);

        start.G = 0.0;
        start.F = start.H;

        int blockingNodeCount = nodesByLine.Values.Count(node => node.IsBlocking);
        while (true)
        {
            var openSet = new HashSet<SoundLeakNode>(ReferenceEqualityComparer.Instance) { start };

            while (openSet.Count > 0)
            {
                SoundLeakNode current = LowestCostNode(openSet);
                if (ReferenceEquals(current, end))
                    return CreateLeakPath(start, end);

                openSet.Remove(current);
                ProcessLeakNeighbors(current, start, openSet);
            }

            int skippedBlockingNodes = 0;
            foreach (SoundLeakNode node in nodes)
            {
                if (node.IsBlocking && node.G != double.MaxValue)
                {
                    node.IsSkip = true;
                    skippedBlockingNodes++;
                }

                node.Reset();
            }

            if (skippedBlockingNodes == 0 || skippedBlockingNodes == blockingNodeCount) return null;

            start.G = 0.0;
            start.F = start.H;
        }
    }

    public static Vector2D SectorCenter(Sector sector)
    {
        var vertices = new HashSet<Vertex>(ReferenceEqualityComparer.Instance);
        foreach (Sidedef side in sector.Sidedefs)
        {
            if (side.Line == null) continue;
            vertices.Add(side.Line.Start);
            vertices.Add(side.Line.End);
        }

        if (vertices.Count == 0) return new Vector2D();

        double x = 0.0;
        double y = 0.0;
        foreach (Vertex vertex in vertices)
        {
            x += vertex.Position.x;
            y += vertex.Position.y;
        }

        return new Vector2D(x / vertices.Count, y / vertices.Count);
    }

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

    private static bool CanLeakPassThrough(Linedef line, IReadOnlySet<Sector> sectors)
    {
        if (line.Front?.Sector == null || line.Back?.Sector == null) return false;
        if (ReferenceEquals(line.Front.Sector, line.Back.Sector)) return false;
        if (IsBlockedByHeight(line)) return false;
        return sectors.Contains(line.Front.Sector) && sectors.Contains(line.Back.Sector);
    }

    private static void AddLeakNeighbors(
        Sector? sector,
        Linedef line,
        SoundLeakNode node,
        Dictionary<Linedef, SoundLeakNode> nodesByLine,
        IReadOnlySet<Sector> sectors)
    {
        if (sector == null) return;

        foreach (Sidedef side in sector.Sidedefs)
        {
            Linedef? neighborLine = side.Line;
            if (neighborLine == null || ReferenceEquals(neighborLine, line)) continue;
            if (!CanLeakPassThrough(neighborLine, sectors)) continue;
            if (nodesByLine.TryGetValue(neighborLine, out SoundLeakNode? neighbor)) node.Neighbors.Add(neighbor);
        }
    }

    private static void AddEndpointNeighbors(
        Sector sector,
        SoundLeakNode endpoint,
        Dictionary<Linedef, SoundLeakNode> nodesByLine,
        IReadOnlySet<Sector> sectors)
    {
        foreach (Sidedef side in sector.Sidedefs)
        {
            Linedef? line = side.Line;
            if (line == null || !CanLeakPassThrough(line, sectors)) continue;
            if (!nodesByLine.TryGetValue(line, out SoundLeakNode? neighbor)) continue;

            endpoint.Neighbors.Add(neighbor);
            neighbor.Neighbors.Add(endpoint);
        }
    }

    private static SoundLeakNode LowestCostNode(HashSet<SoundLeakNode> openSet)
    {
        SoundLeakNode current = openSet.First();
        foreach (SoundLeakNode node in openSet)
            if (node.F < current.F) current = node;

        return current;
    }

    private static void ProcessLeakNeighbors(SoundLeakNode current, SoundLeakNode start, HashSet<SoundLeakNode> openSet)
    {
        bool blockingInPath = HasBlockingInPath(current, start);

        foreach (SoundLeakNode neighbor in current.Neighbors)
        {
            if ((neighbor.IsBlocking && blockingInPath) || neighbor.IsSkip) continue;

            double newG = current.G + Vector2D.Distance(current.Position, neighbor.Position);
            if (newG >= neighbor.G) continue;

            neighbor.From = current;
            neighbor.G = newG;
            neighbor.F = neighbor.G + neighbor.H;
            openSet.Add(neighbor);
        }
    }

    private static bool HasBlockingInPath(SoundLeakNode node, SoundLeakNode start)
    {
        SoundLeakNode? current = node;
        while (current != null && !ReferenceEquals(current, start))
        {
            if (current.IsBlocking) return true;
            current = current.From;
        }

        return false;
    }

    private static SoundLeakPath CreateLeakPath(SoundLeakNode start, SoundLeakNode end)
    {
        var points = new List<Vector2D>();
        var linedefs = new List<Linedef>();
        var blockingLinedefs = new List<Linedef>();

        for (SoundLeakNode? current = end; current != null; current = current.From)
        {
            points.Add(current.Position);
            if (current.Line != null)
            {
                linedefs.Add(current.Line);
                if (current.IsBlocking) blockingLinedefs.Add(current.Line);
            }
        }

        points.Reverse();
        linedefs.Reverse();
        blockingLinedefs.Reverse();

        return new SoundLeakPath(points, linedefs, blockingLinedefs);
    }

    private sealed class SoundLeakNode
    {
        private SoundLeakNode(Vector2D position, Linedef? line, Vector2D destination, bool isBlocking)
        {
            Position = position;
            Line = line;
            H = Vector2D.Distance(Position, destination);
            IsBlocking = isBlocking;
        }

        public Vector2D Position { get; }
        public Linedef? Line { get; }
        public List<SoundLeakNode> Neighbors { get; } = new();
        public SoundLeakNode? From { get; set; }
        public double G { get; set; } = double.MaxValue;
        public double H { get; }
        public double F { get; set; } = double.MaxValue;
        public bool IsBlocking { get; }
        public bool IsSkip { get; set; }

        public static SoundLeakNode ForPoint(Vector2D position)
            => new(position, null, position, isBlocking: false);

        public static SoundLeakNode ForLine(Linedef line, Vector2D destination, bool isBlocking)
            => new(Midpoint(line), line, destination, isBlocking);

        public void Reset()
        {
            From = null;
            G = double.MaxValue;
            F = double.MaxValue;
        }

        private static Vector2D Midpoint(Linedef line)
            => new((line.Start.Position.x + line.End.Position.x) * 0.5, (line.Start.Position.y + line.End.Position.y) * 0.5);
    }
}
