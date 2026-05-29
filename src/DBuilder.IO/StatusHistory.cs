// ABOUTME: Stores a bounded newest-first history of editor status messages.
// ABOUTME: Provides a small model for status and notification UI without depending on Avalonia.

using System;
using System.Collections.Generic;

namespace DBuilder.IO;

public sealed record StatusHistoryEntry(string Message, DateTimeOffset Timestamp);

public sealed class StatusHistory
{
    private readonly List<StatusHistoryEntry> _entries = new();
    private readonly Func<DateTimeOffset> _clock;

    public int Capacity { get; }

    public IReadOnlyList<StatusHistoryEntry> Entries => _entries;

    public StatusHistory(int capacity = 100, Func<DateTimeOffset>? clock = null)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public void Add(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _entries.Insert(0, new StatusHistoryEntry(message.Trim(), _clock()));
        if (_entries.Count > Capacity) _entries.RemoveRange(Capacity, _entries.Count - Capacity);
    }
}
