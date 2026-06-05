// ABOUTME: Stores a bounded newest-first history of editor status messages.
// ABOUTME: Provides a small model for status and notification UI without depending on Avalonia.

using System;
using System.Collections.Generic;

namespace DBuilder.IO;

public enum StatusHistoryKind
{
    Ready,
    Selection,
    Action,
    Info,
    Busy,
    Warning,
}

public sealed record StatusHistoryEntry(string Message, DateTimeOffset Timestamp, StatusHistoryKind Kind = StatusHistoryKind.Info);

public sealed class StatusHistory
{
    private readonly List<StatusHistoryEntry> _entries = new();
    private readonly Func<DateTimeOffset> _clock;

    public int Capacity { get; private set; }

    public IReadOnlyList<StatusHistoryEntry> Entries => _entries;

    public string HeaderText => HeaderTextFor(Entries);

    public static string HeaderTextFor(IReadOnlyList<StatusHistoryEntry> entries)
        => entries.Count == 0
            ? "No status messages yet."
            : $"{CountLabel(entries.Count, "recent status message")}.";

    public StatusHistory(int capacity = 100, Func<DateTimeOffset>? clock = null)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public void SetCapacity(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        if (_entries.Count > Capacity) _entries.RemoveRange(Capacity, _entries.Count - Capacity);
    }

    public void Add(string message, StatusHistoryKind kind = StatusHistoryKind.Info)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _entries.Insert(0, new StatusHistoryEntry(message.Trim(), _clock(), kind));
        if (_entries.Count > Capacity) _entries.RemoveRange(Capacity, _entries.Count - Capacity);
    }

    public void Clear() => _entries.Clear();

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}
