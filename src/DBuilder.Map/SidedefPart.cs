// ABOUTME: Identifies one of the three texture-bearing wall parts on a sidedef.
// ABOUTME: Matches UDB's SidedefPart values so editing and visual picking share the same vocabulary.

namespace DBuilder.Map;

public enum SidedefPart
{
    None = 0,
    Upper = 1,
    Middle = 2,
    Lower = 3,
}
