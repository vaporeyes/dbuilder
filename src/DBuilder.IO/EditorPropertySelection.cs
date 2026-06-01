// ABOUTME: Encapsulates property-dialog selection rules shared by editor UI and tests.
// ABOUTME: Keeps command availability aligned with the single-element edit surfaces.

namespace DBuilder.IO;

public static class EditorPropertySelection
{
    public static bool CanEdit(int vertices, int linedefs, int sidedefs, int sectors, int things)
        => vertices + linedefs + sidedefs + sectors + things == 1;
}
