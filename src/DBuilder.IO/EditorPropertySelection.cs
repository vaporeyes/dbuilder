// ABOUTME: Encapsulates property-dialog selection rules shared by editor UI and tests.
// ABOUTME: Keeps command availability aligned with the single-element edit surfaces.

namespace DBuilder.IO;

public static class EditorPropertySelection
{
    public static bool CanEdit(int vertices, int linedefs, int sidedefs, int sectors, int things)
        => vertices + linedefs + sidedefs + sectors + things == 1;

    public static bool CanEditFlags(int vertices, int linedefs, int sidedefs, int sectors, int things)
        => sidedefs == 0
           && vertices == 0
           && ((linedefs == 1 && sectors == 0 && things == 0)
               || (sectors == 1 && linedefs == 0 && things == 0)
               || (things == 1 && linedefs == 0 && sectors == 0));
}
