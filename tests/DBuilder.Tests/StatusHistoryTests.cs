// ABOUTME: Tests the bounded status history used by editor status and notification UI.
// ABOUTME: Covers trimming, blank-message handling, and newest-first ordering.

using System.Linq;
using DBuilder.IO;

namespace DBuilder.Tests;

public class StatusHistoryTests
{
    [Fact]
    public void AddStoresMessagesNewestFirst()
    {
        var timestamp = new DateTimeOffset(2026, 5, 29, 12, 0, 0, TimeSpan.Zero);
        var history = new StatusHistory(clock: () => timestamp);

        history.Add("first");
        history.Add("second");

        Assert.Equal("second", history.Entries[0].Message);
        Assert.Equal("first", history.Entries[1].Message);
        Assert.Equal(timestamp, history.Entries[0].Timestamp);
    }

    [Fact]
    public void AddTrimsBlankMessagesAndKeepsCapacity()
    {
        var history = new StatusHistory(capacity: 2);

        history.Add(" first ");
        history.Add("");
        history.Add("second");
        history.Add("third");

        Assert.Equal(2, history.Entries.Count);
        Assert.Equal("third", history.Entries[0].Message);
        Assert.Equal("second", history.Entries[1].Message);
    }

    [Fact]
    public void SetCapacityTrimsOldestEntries()
    {
        var history = new StatusHistory(capacity: 4);
        history.Add("one");
        history.Add("two");
        history.Add("three");

        history.SetCapacity(2);

        Assert.Equal(2, history.Capacity);
        Assert.Equal(new[] { "three", "two" }, history.Entries.Select(e => e.Message));
    }
}
