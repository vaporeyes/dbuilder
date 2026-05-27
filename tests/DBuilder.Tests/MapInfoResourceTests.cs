// ABOUTME: Tests ResourceManager aggregation of MAPINFO and ZMAPINFO data from resources.
// ABOUTME: Uses synthetic PK3 fixtures to verify include resolution and priority-style merging.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class MapInfoResourceTests
{
    [Fact]
    public void ResourceManagerParsesMapinfoIncludesFromPk3()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("MAPINFO.txt", Encoding.ASCII.GetBytes("""
include "mapinfo/maps.txt"
DoomEdNums { 9001 = RootActor }
""")),
            ("mapinfo/maps.txt", Encoding.ASCII.GetBytes("""
map MAP01 "Entryway" { next = MAP02 }
DoomEdNums { 9000 = IncludedActor }
""")));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            var mapInfo = resources.GetMapInfo();
            Assert.Equal("MAP02", mapInfo.GetMap("MAP01")!.Next);
            Assert.Equal("IncludedActor", mapInfo.DoomEdNums[9000]);
            Assert.Equal("RootActor", mapInfo.DoomEdNums[9001]);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void ResourceManagerPrefersZmapinfoOverMapinfoInSameResource()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("MAPINFO.txt", Encoding.ASCII.GetBytes("map MAP01 \"Entryway\" { next = MAP02 }\nDoomEdNums { 9000 = MapinfoActor }")),
            ("ZMAPINFO.txt", Encoding.ASCII.GetBytes("map E1M1 \"Hangar\" { next = E1M2 }\nDoomEdNums { 9001 = ZMapinfoActor }")));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            var mapInfo = resources.GetMapInfo();
            Assert.Null(mapInfo.GetMap("MAP01"));
            Assert.Equal("E1M2", mapInfo.GetMap("E1M1")!.Next);
            Assert.False(mapInfo.DoomEdNums.ContainsKey(9000));
            Assert.Equal("ZMapinfoActor", mapInfo.DoomEdNums[9001]);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void ResourceManagerAggregatesMapinfoAcrossResources()
    {
        string lower = TestArtifacts.BuildPk3(("MAPINFO.txt", Encoding.ASCII.GetBytes("map MAP01 \"Entryway\" { next = MAP02 }\nDoomEdNums { 9000 = MapinfoActor }")));
        string higher = TestArtifacts.BuildPk3(("ZMAPINFO.txt", Encoding.ASCII.GetBytes("map E1M1 \"Hangar\" { next = E1M2 }\nDoomEdNums { 9001 = ZMapinfoActor }")));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(lower);
            resources.AddResource(higher);

            var mapInfo = resources.GetMapInfo();
            Assert.Equal("MAP02", mapInfo.GetMap("MAP01")!.Next);
            Assert.Equal("E1M2", mapInfo.GetMap("E1M1")!.Next);
            Assert.Equal("MapinfoActor", mapInfo.DoomEdNums[9000]);
            Assert.Equal("ZMapinfoActor", mapInfo.DoomEdNums[9001]);
        }
        finally
        {
            File.Delete(lower);
            File.Delete(higher);
        }
    }
}
