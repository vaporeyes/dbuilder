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
using DBuilder.Map;

namespace DBuilder.IO;

public sealed class UndoManager
{
    private readonly MapSet map;
    private readonly int maxLevels;
    private readonly LinkedList<Snapshot> undos = new();
    private readonly LinkedList<Snapshot> redos = new();

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

    /// <summary>
    /// Snapshots the current map state under <paramref name="description"/> and pushes it onto the undo stack.
    /// Clears the redo stack (a new edit invalidates the redo history). Call this BEFORE mutating the map.
    /// </summary>
    public void CreateUndo(string description)
    {
        undos.AddFirst(Capture(description));
        while (undos.Count > maxLevels) undos.RemoveLast();
        redos.Clear();
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
        return true;
    }

    /// <summary>Re-applies the most recently undone edit. Returns false when there is nothing to redo.</summary>
    public bool Redo()
    {
        if (redos.Count == 0) return false;
        var snap = redos.First!.Value;
        redos.RemoveFirst();
        undos.AddFirst(Capture(snap.Description));
        Restore(snap);
        return true;
    }

    /// <summary>Discards all undo and redo history.</summary>
    public void Clear()
    {
        undos.Clear();
        redos.Clear();
    }

    private Snapshot Capture(string description)
    {
        using var ms = new MemoryStream();
        ClipboardStreamWriter.Write(map, ms);
        return new Snapshot(description, ms.ToArray()) { Namespace = map.Namespace };
    }

    private void Restore(Snapshot snap)
    {
        map.Vertices.Clear();
        map.Linedefs.Clear();
        map.Sidedefs.Clear();
        map.Sectors.Clear();
        map.Things.Clear();
        map.Namespace = snap.Namespace;
        using var ms = new MemoryStream(snap.Data);
        ClipboardStreamReader.Read(map, ms); // appends into the now-empty map and rebuilds indexes
    }

    private sealed class Snapshot
    {
        public string Description { get; }
        public byte[] Data { get; }
        public string Namespace { get; init; } = "";

        public Snapshot(string description, byte[] data)
        {
            Description = description;
            Data = data;
        }
    }
}
