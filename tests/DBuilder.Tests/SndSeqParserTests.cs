// ABOUTME: Tests ZDoom SNDSEQ parsing for named ambient sound sequences.
// ABOUTME: Uses synthetic sequence text to verify command order and basic argument capture.

using System.Linq;
using DBuilder.IO;

namespace DBuilder.Tests;

public class SndSeqParserTests
{
    [Fact]
    public void ParsesSequenceCommands()
    {
        const string text = @"
:DoorOpen
    play doors/open
    delay 8
    playuntildone doors/stop
end";

        var seq = SndSeqParser.Parse(text).Sequences.Single();

        Assert.Equal("DoorOpen", seq.Name);
        Assert.Equal(new SndSeqCommand("play", "doors/open"), seq.Commands[0]);
        Assert.Equal(new SndSeqCommand("delay", Value: 8), seq.Commands[1]);
        Assert.Equal(new SndSeqCommand("playuntildone", "doors/stop"), seq.Commands[2]);
        Assert.Equal(new SndSeqCommand("end"), seq.Commands[3]);
    }

    [Fact]
    public void ParsesMultipleSequencesAndComments()
    {
        const string text = @"
# elevator sequence
:Lift
play ""world/lift""
end
// ambient loop
:Loop
delayrand 12 24
stopsound world/lift
end";

        var parsed = SndSeqParser.Parse(text);

        Assert.Equal(new[] { "Lift", "Loop" }, parsed.Sequences.Select(s => s.Name).ToArray());
        Assert.Equal(new SndSeqCommand("play", "world/lift"), parsed.Sequences[0].Commands[0]);
        Assert.Equal(new SndSeqCommand("delayrand", Value: 12), parsed.Sequences[1].Commands[0]);
        Assert.Equal(new SndSeqCommand("stopsound", "world/lift"), parsed.Sequences[1].Commands[1]);
    }

    [Fact]
    public void ParsesGroupsAndUdbSortedSequenceNames()
    {
        const string text = @"
:Zeta
end
[Doors
[doors
:Alpha
end
[Platforms";

        var parsed = SndSeqParser.Parse(text);

        Assert.Equal(new[] { "Doors", "Platforms" }, parsed.SequenceGroups.ToArray());
        Assert.Equal(new[] { "Zeta", "Alpha" }, parsed.Sequences.Select(s => s.Name).ToArray());
        Assert.Equal(new[] { "Doors", "Platforms", "Alpha", "Zeta" }, parsed.GetSoundSequences());
    }
}
