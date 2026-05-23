// ABOUTME: Parser for ZScript actor classes - reuses the DECORATE engine keyed on "class" (no header editor number).
// ABOUTME: ZScript editor numbers come from MAPINFO DoomEdNums, assigned separately via GameConfiguration.MergeActors.

using System.Collections.Generic;

namespace DBuilder.IO;

public static class ZScriptParser
{
    /// <summary>
    /// Parses ZScript class definitions into actor info (class/parent/replaces, Default Radius/Height, //$ keys,
    /// spawn-state sprite). DoomEdNum is left unset; assign it from a MAPINFO DoomEdNums map when merging.
    /// </summary>
    public static List<ActorInfo> Parse(string text) => DecorateParser.ParseActors(text, "class", headerNum: false);
}
