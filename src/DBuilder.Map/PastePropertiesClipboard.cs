// ABOUTME: Stores copied map-element property snapshots and applies them to selected targets.
// ABOUTME: Mirrors UDB classic copy/paste-properties behavior without depending on editor highlight state.

namespace DBuilder.Map;

using DBuilder.Geometry;

public sealed record PastePropertiesCopyResult(bool Copied, PastePropertiesElementKind Kind, string StatusMessage);

public sealed record PastePropertiesApplyResult(bool Applied, PastePropertiesElementKind Kind, int TargetCount, string StatusMessage);

public sealed class PastePropertiesClipboard
{
    private Vertex? copiedVertex;
    private Linedef? copiedLinedef;
    private Sidedef? copiedSidedef;
    private Sector? copiedSector;
    private Thing? copiedThing;

    public PastePropertiesCopiedState CopiedState => new(
        Vertex: copiedVertex != null,
        Linedef: copiedLinedef != null,
        Sidedef: copiedSidedef != null,
        Sector: copiedSector != null,
        Thing: copiedThing != null);

    public PastePropertiesCopyResult CopySelected(MapSet map, PastePropertiesElementKind kind)
    {
        switch (kind)
        {
            case PastePropertiesElementKind.Vertex:
                if (map.GetSelectedVertices().FirstOrDefault() is not { } vertex)
                    return CopyFailed(kind);
                copiedVertex = Snapshot(vertex);
                return CopySucceeded(kind);

            case PastePropertiesElementKind.Linedef:
                if (map.GetSelectedLinedefs().FirstOrDefault() is not { } linedef)
                    return CopyFailed(kind);
                copiedLinedef = Snapshot(linedef);
                return CopySucceeded(kind);

            case PastePropertiesElementKind.Sidedef:
                if (map.GetSelectedSidedefs().FirstOrDefault() is not { } sidedef)
                    return CopyFailed(kind);
                copiedSidedef = Snapshot(sidedef);
                return CopySucceeded(kind);

            case PastePropertiesElementKind.Sector:
                if (map.GetSelectedSectors().FirstOrDefault() is not { } sector)
                    return CopyFailed(kind);
                copiedSector = Snapshot(sector);
                return CopySucceeded(kind);

            case PastePropertiesElementKind.Thing:
                if (map.GetSelectedThings().FirstOrDefault() is not { } thing)
                    return CopyFailed(kind);
                copiedThing = Snapshot(thing);
                return CopySucceeded(kind);

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown map element type.");
        }
    }

    public PastePropertiesOptionsResult BuildOptions(
        IEnumerable<PastePropertiesElementKind> targetKinds,
        bool supportsUdmf = true)
        => PastePropertiesOptionsModel.Build(
            CopiedState,
            targetKinds,
            PastePropertiesOptionsModel.CreateDefaultCatalog(supportsUdmf));

    public PastePropertiesApplyResult ApplySelected(
        MapSet map,
        PastePropertiesElementKind kind,
        bool supportsUdmf = true,
        ISet<string>? enabledKeys = null)
    {
        if (!HasSourceFor(kind)) return MissingSourceResult(kind);

        enabledKeys ??= EnabledKeysFor(kind, supportsUdmf);
        if (enabledKeys.Count == 0)
            return new PastePropertiesApplyResult(false, kind, 0, PastePropertiesOptionsModel.NoSupportedPropertiesMessage);

        return kind switch
        {
            PastePropertiesElementKind.Vertex => ApplyVertices(map, enabledKeys),
            PastePropertiesElementKind.Linedef => ApplyLinedefs(map, enabledKeys),
            PastePropertiesElementKind.Sidedef => ApplySidedefs(map, enabledKeys),
            PastePropertiesElementKind.Sector => ApplySectors(map, enabledKeys),
            PastePropertiesElementKind.Thing => ApplyThings(map, enabledKeys),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown map element type."),
        };
    }

    private ISet<string> EnabledKeysFor(PastePropertiesElementKind kind, bool supportsUdmf)
    {
        PastePropertiesOptionsResult options = BuildOptions([kind], supportsUdmf);
        return options.IsAvailable
            ? PastePropertiesApplier.EnabledKeys(options)
            : new HashSet<string>(StringComparer.Ordinal);
    }

    private bool HasSourceFor(PastePropertiesElementKind kind) => kind switch
    {
        PastePropertiesElementKind.Vertex => copiedVertex != null,
        PastePropertiesElementKind.Linedef => copiedLinedef != null,
        PastePropertiesElementKind.Sidedef => copiedSidedef != null || copiedLinedef?.Front != null || copiedLinedef?.Back != null,
        PastePropertiesElementKind.Sector => copiedSector != null,
        PastePropertiesElementKind.Thing => copiedThing != null,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown map element type."),
    };

    private static PastePropertiesApplyResult MissingSourceResult(PastePropertiesElementKind kind) => kind switch
    {
        PastePropertiesElementKind.Vertex => PasteFailed(kind, "Copy vertex properties first!"),
        PastePropertiesElementKind.Linedef => PasteFailed(kind, "Copy linedef properties first!"),
        PastePropertiesElementKind.Sidedef => PasteFailed(kind, "Copy sidedef properties first!"),
        PastePropertiesElementKind.Sector => PasteFailed(kind, "Copy sector properties first!"),
        PastePropertiesElementKind.Thing => PasteFailed(kind, "Copy thing properties first!"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown map element type."),
    };

    private PastePropertiesApplyResult ApplyVertices(MapSet map, ISet<string> enabledKeys)
    {
        var targets = map.GetSelectedVertices();
        if (targets.Count == 0) return ApplyFailed(PastePropertiesElementKind.Vertex);

        foreach (Vertex target in targets)
            PastePropertiesApplier.Apply(copiedVertex!, target, enabledKeys);
        return ApplySucceeded(PastePropertiesElementKind.Vertex, targets.Count);
    }

    private PastePropertiesApplyResult ApplyLinedefs(MapSet map, ISet<string> enabledKeys)
    {
        var targets = map.GetSelectedLinedefs();
        if (targets.Count == 0) return ApplyFailed(PastePropertiesElementKind.Linedef);

        foreach (Linedef target in targets)
            PastePropertiesApplier.Apply(copiedLinedef!, target, enabledKeys);
        return ApplySucceeded(PastePropertiesElementKind.Linedef, targets.Count);
    }

    private PastePropertiesApplyResult ApplySidedefs(MapSet map, ISet<string> enabledKeys)
    {
        Sidedef? source = copiedSidedef ?? copiedLinedef?.Front ?? copiedLinedef?.Back;

        var targets = map.GetSelectedSidedefs();
        if (targets.Count == 0) return ApplyFailed(PastePropertiesElementKind.Sidedef);

        foreach (Sidedef target in targets)
            PastePropertiesApplier.Apply(source!, target, enabledKeys);
        return ApplySucceeded(PastePropertiesElementKind.Sidedef, targets.Count);
    }

    private PastePropertiesApplyResult ApplySectors(MapSet map, ISet<string> enabledKeys)
    {
        var targets = map.GetSelectedSectors();
        if (targets.Count == 0) return ApplyFailed(PastePropertiesElementKind.Sector);

        foreach (Sector target in targets)
            PastePropertiesApplier.Apply(copiedSector!, target, enabledKeys);
        return ApplySucceeded(PastePropertiesElementKind.Sector, targets.Count);
    }

    private PastePropertiesApplyResult ApplyThings(MapSet map, ISet<string> enabledKeys)
    {
        var targets = map.GetSelectedThings();
        if (targets.Count == 0) return ApplyFailed(PastePropertiesElementKind.Thing);

        foreach (Thing target in targets)
            PastePropertiesApplier.Apply(copiedThing!, target, enabledKeys);
        return ApplySucceeded(PastePropertiesElementKind.Thing, targets.Count);
    }

    private static Vertex Snapshot(Vertex source)
    {
        Vertex snapshot = new();
        source.CopyPropertiesTo(snapshot);
        return snapshot;
    }

    private static Linedef Snapshot(Linedef source)
    {
        Linedef snapshot = new(new Vertex(source.Start.Position), new Vertex(source.End.Position));
        source.CopyPropertiesTo(snapshot);
        if (source.Front != null) snapshot.AttachFront(Snapshot(source.Front, snapshot, isFront: true));
        if (source.Back != null) snapshot.AttachBack(Snapshot(source.Back, snapshot, isFront: false));
        return snapshot;
    }

    private static Sidedef Snapshot(Sidedef source)
    {
        Linedef line = new(new Vertex(new Vector2D()), new Vertex(new Vector2D(64, 0)));
        return Snapshot(source, line, source.IsFront);
    }

    private static Sidedef Snapshot(Sidedef source, Linedef line, bool isFront)
    {
        Sidedef snapshot = new(line, isFront);
        source.CopyPropertiesTo(snapshot);
        return snapshot;
    }

    private static Sector Snapshot(Sector source)
    {
        Sector snapshot = new();
        source.CopyPropertiesTo(snapshot);
        return snapshot;
    }

    private static Thing Snapshot(Thing source)
    {
        Thing snapshot = new();
        source.CopyPropertiesTo(snapshot);
        return snapshot;
    }

    private static PastePropertiesCopyResult CopySucceeded(PastePropertiesElementKind kind)
        => new(true, kind, $"Copied {DisplayName(kind)} properties.");

    private static PastePropertiesCopyResult CopyFailed(PastePropertiesElementKind kind)
        => new(false, kind, "This action requires highlight or selection!");

    private static PastePropertiesApplyResult ApplySucceeded(PastePropertiesElementKind kind, int count)
        => new(true, kind, count, $"Pasted properties to {TargetText(kind, count)}.");

    private static PastePropertiesApplyResult ApplyFailed(PastePropertiesElementKind kind)
        => new(false, kind, 0, "This action requires highlight or selection!");

    private static PastePropertiesApplyResult PasteFailed(PastePropertiesElementKind kind, string message)
        => new(false, kind, 0, message);

    private static string DisplayName(PastePropertiesElementKind kind) => kind switch
    {
        PastePropertiesElementKind.Vertex => "vertex",
        PastePropertiesElementKind.Linedef => "linedef",
        PastePropertiesElementKind.Sidedef => "sidedef",
        PastePropertiesElementKind.Sector => "sector",
        PastePropertiesElementKind.Thing => "thing",
        _ => "element",
    };

    private static string TargetText(PastePropertiesElementKind kind, int count)
    {
        string name = DisplayName(kind);
        return count == 1 ? $"a single {name}" : $"{count} {name}s";
    }
}
