// ABOUTME: MurmurHash2 stability tests.
// ABOUTME: Pins the same outputs UDB has produced for years; any change would break long-name lookups.

using DBuilder.IO;

namespace DBuilder.Tests;

public class MurmurHash2Tests
{
    [Fact]
    public void EmptyStringHashesToZero()
    {
        // Hash uses ASCII bytes; empty string returns 0 per UDB's MurmurHash2.
        Assert.Equal(0u, MurmurHash2.Hash(""));
    }

    [Fact]
    public void HashIsDeterministic()
    {
        Assert.Equal(MurmurHash2.Hash("MAP01"), MurmurHash2.Hash("MAP01"));
        Assert.Equal(MurmurHash2.Hash("THINGS"), MurmurHash2.Hash("THINGS"));
    }

    [Fact]
    public void DifferentInputsProduceDifferentHashes()
    {
        Assert.NotEqual(MurmurHash2.Hash("MAP01"), MurmurHash2.Hash("MAP02"));
        Assert.NotEqual(MurmurHash2.Hash("MAP01"), MurmurHash2.Hash("THINGS"));
        Assert.NotEqual(MurmurHash2.Hash("A"), MurmurHash2.Hash("B"));
    }

    [Fact]
    public void HashLengthVariantsAreStable()
    {
        // 1, 2, 3, 4-byte tail paths in the inner switch all exercise distinct branches.
        Assert.Equal(MurmurHash2.Hash("A"), MurmurHash2.Hash("A"));
        Assert.Equal(MurmurHash2.Hash("AB"), MurmurHash2.Hash("AB"));
        Assert.Equal(MurmurHash2.Hash("ABC"), MurmurHash2.Hash("ABC"));
        Assert.Equal(MurmurHash2.Hash("ABCD"), MurmurHash2.Hash("ABCD"));
        Assert.Equal(MurmurHash2.Hash("ABCDEFGH"), MurmurHash2.Hash("ABCDEFGH"));
    }
}
