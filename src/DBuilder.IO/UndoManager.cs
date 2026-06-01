// ABOUTME: Snapshot-based undo/redo for a MapSet, built on the ClipboardStream serializer.
// ABOUTME: Each CreateUndo captures the full pre-edit map state; Undo/Redo swap whole-map snapshots in and out.

/*
 * UDB's UndoManager serializes map elements into compressed memory streams and restores them on undo.
 * This port follows the same whole-map-snapshot strategy but reuses the already-tested ClipboardStream
 * serializer for the heavy lifting. Snapshots are O(map size); for a foundation that is acceptable and
 * keeps undo correctness independent of the per-edit mutation operations.
 *
 * Usage pattern:
 *   undo.CreateUndo("Move vertex");   // snapshot the state BEFORE the edit
 *   vertex.Position = newPos;          // mutate
 *   ...
 *   undo.Undo();                       // restores the pre-edit state
 *   undo.Redo();                       // re-applies the edit
 */

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed class UndoManager
{
    private readonly MapSet map;
    private readonly int maxLevels;
    private readonly LinkedList<Snapshot> undos = new();
    private readonly LinkedList<Snapshot> redos = new();
    private int ticketId;
    private Assembly? lastGroupAssembly;
    private int lastGroupId;
    private int lastGroupTag;

    public UndoManager(MapSet map, int maxLevels = 50)
    {
        this.map = map;
        this.maxLevels = maxLevels < 1 ? 1 : maxLevels;
    }

    public bool CanUndo => undos.Count > 0;
    public bool CanRedo => redos.Count > 0;
    public int UndoCount => undos.Count;
    public int RedoCount => redos.Count;

    /// <summary>Description of the edit that the next Undo would reverse, or null when nothing to undo.</summary>
    public string? NextUndoDescription => undos.First?.Value.Description;

    /// <summary>Description of the edit that the next Redo would re-apply, or null when nothing to redo.</summary>
    public string? NextRedoDescription => redos.First?.Value.Description;

    public IReadOnlyList<string> GetUndoDescriptions()
        => undos.Select(snapshot => snapshot.Description).ToArray();

    public IReadOnlyList<string> GetRedoDescriptions()
        => redos.Select(snapshot => snapshot.Description).ToArray();

    /// <summary>
    /// Snapshots the current map state under <paramref name="description"/> and pushes it onto the undo stack.
    /// Clears the redo stack (a new edit invalidates the redo history). Call this BEFORE mutating the map.
    /// </summary>
    public void CreateUndo(string description)
        => CreateUndo(description, null, 0, 0);

    public int CreateUndo(string description, object? groupSource, int groupId, int groupTag)
    {
        Assembly? groupAssembly = groupSource?.GetType().Assembly;
        if (groupAssembly != null
            && lastGroupAssembly != null
            && groupAssembly == lastGroupAssembly
            && groupId != 0
            && lastGroupId != 0
            && groupId == lastGroupId
            && groupTag == lastGroupTag)
        {
            return -1;
        }

        undos.AddFirst(Capture(description));
        while (undos.Count > maxLevels) undos.RemoveLast();
        redos.Clear();
        lastGroupAssembly = groupAssembly;
        lastGroupId = groupId;
        lastGroupTag = groupTag;
        if (++ticketId == int.MaxValue) ticketId = 1;
        return ticketId;
    }

    /// <summary>Reverts to the most recent snapshot. Returns false when there is nothing to undo.</summary>
    public bool Undo()
    {
        if (undos.Count == 0) return false;
        var snap = undos.First!.Value;
        undos.RemoveFirst();
        // Push the current (post-edit) state onto the redo stack so Redo can return to it.
        redos.AddFirst(Capture(snap.Description));
        Restore(snap);
        map.ClearAllSelected();
        ClearGrouping();
        return true;
    }

    public int PerformUndo(int levels)
    {
        int performed = 0;
        for (int i = 0; i < levels && Undo(); i++)
            performed++;
        return performed;
    }

    /// <summary>Re-applies the most recently undone edit. Returns false when there is nothing to redo.</summary>
    public bool Redo()
    {
        if (redos.Count == 0) return false;
        var snap = redos.First!.Value;
        redos.RemoveFirst();
        undos.AddFirst(Capture(snap.Description));
        Restore(snap);
        map.ClearAllSelected();
        ClearGrouping();
        return true;
    }

    public int PerformRedo(int levels)
    {
        int performed = 0;
        for (int i = 0; i < levels && Redo(); i++)
            performed++;
        return performed;
    }

    /// <summary>Restores and removes the latest undo snapshot, then discards the redo it created.</summary>
    public bool WithdrawUndo()
    {
        if (!Undo()) return false;
        redos.Clear();
        ClearGrouping();
        return true;
    }

    /// <summary>Discards all undo and redo history.</summary>
    public void Clear()
    {
        undos.Clear();
        redos.Clear();
        ClearGrouping();
    }

    private void ClearGrouping()
    {
        lastGroupAssembly = null;
        lastGroupId = 0;
        lastGroupTag = 0;
    }

    private Snapshot Capture(string description)
    {
        using var ms = new MemoryStream();
        ClipboardStreamWriter.Write(map, ms);
        return new Snapshot(description, ms.ToArray())
        {
            Namespace = map.Namespace,
            Fields = CopyFields(map.Fields),
            UnknownUdmfData = CopyUnknownUdmfData(map.UnknownUdmfData),
        };
    }

    private void Restore(Snapshot snap)
    {
        map.Vertices.Clear();
        map.Linedefs.Clear();
        map.Sidedefs.Clear();
        map.Sectors.Clear();
        map.Things.Clear();
        map.Fields.Clear();
        map.UnknownUdmfData.Clear();
        map.Namespace = snap.Namespace;
        foreach (var kv in snap.Fields) map.Fields[kv.Key] = kv.Value;
        foreach (var entry in snap.UnknownUdmfData) map.UnknownUdmfData.Add(entry.Clone());
        using var ms = new MemoryStream(snap.Data);
        ClipboardStreamReader.Read(map, ms); // appends into the now-empty map and rebuilds indexes
    }

    private static Dictionary<string, object> CopyFields(Dictionary<string, object> fields)
    {
        var copy = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var kv in fields) copy[kv.Key] = kv.Value;
        return copy;
    }

    private static List<UnknownUdmfEntry> CopyUnknownUdmfData(List<UnknownUdmfEntry> entries)
        => entries.Select(entry => entry.Clone()).ToList();

    private sealed class Snapshot
    {
        public string Description { get; }
        public byte[] Data { get; }
        public string Namespace { get; init; } = "";
        public Dictionary<string, object> Fields { get; init; } = new(StringComparer.Ordinal);
        public List<UnknownUdmfEntry> UnknownUdmfData { get; init; } = new();

        public Snapshot(string description, byte[] data)
        {
            Description = description;
            Data = data;
        }
    }
}
