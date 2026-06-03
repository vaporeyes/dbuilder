// ABOUTME: Tests the bounded status history used by editor status and notification UI.
// ABOUTME: Covers trimming, blank-message handling, and newest-first ordering.

using System.Linq;
using DBuilder.IO;

namespace DBuilder.Tests;

public class StatusHistoryTests
{
    [Fact]
    public void StatusBarFormatsConfigLabels()
    {
        Assert.Equal("Config: Doom_Doom2Doom", StatusBarModel.ConfigText("Doom_Doom2Doom", null));
        Assert.Equal("Config: Doom 2 (Doom_Doom2Doom)", StatusBarModel.ConfigText("Doom_Doom2Doom", "Doom 2"));
    }

    [Fact]
    public void StatusBarFormatsModePriority()
    {
        Assert.Equal("Mode: 3D", StatusBarModel.ModeText("Linedefs", in3DMode: true, automapMode: true));
        Assert.Equal("Mode: Automap", StatusBarModel.ModeText("Linedefs", automapMode: true));
        Assert.Equal("Mode: WadAuthor", StatusBarModel.ModeText("Linedefs", wadAuthorMode: true));
        Assert.Equal("Mode: Image Example", StatusBarModel.ModeText("Linedefs", imageExampleMode: true));
        Assert.Equal("Mode: Linedefs (draw)", StatusBarModel.ModeText("Linedefs", drawMode: true));
        Assert.Equal("Mode: Things", StatusBarModel.ModeText("Things"));
    }

    [Fact]
    public void StatusBarFormatsSnapGridLabel()
    {
        Assert.Equal("Snap: 32", StatusBarModel.GridText(snapToGrid: true, 32.0));
        Assert.Equal("Free: 0.125", StatusBarModel.GridText(snapToGrid: false, 0.125));
    }

    [Fact]
    public void StatusBarFormatsCoordinatesAsWholeMapUnits()
    {
        Assert.Equal("13 , -8", StatusBarModel.CoordinateText(12.6, -7.5));
    }

    [Fact]
    public void AddStoresMessagesNewestFirst()
    {
        var timestamp = new DateTimeOffset(2026, 5, 29, 12, 0, 0, TimeSpan.Zero);
        var history = new StatusHistory(clock: () => timestamp);

        Assert.Equal("No status messages yet.", history.HeaderText);

        history.Add("first");
        Assert.Equal("1 recent status message.", history.HeaderText);

        history.Add("second");

        Assert.Equal("second", history.Entries[0].Message);
        Assert.Equal("first", history.Entries[1].Message);
        Assert.Equal(timestamp, history.Entries[0].Timestamp);
        Assert.Equal("2 recent status messages.", history.HeaderText);
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

    [Fact]
    public void ClearRemovesAllEntries()
    {
        var history = new StatusHistory();
        history.Add("one");
        history.Add("two");

        history.Clear();

        Assert.Empty(history.Entries);
    }
}
