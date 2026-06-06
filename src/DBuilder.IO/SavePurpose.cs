// ABOUTME: Defines UDB-compatible map save purpose values for plugin callback dispatch.
// ABOUTME: Keeps map save callback context explicit without depending on editor UI code.

namespace DBuilder.IO;

public enum SavePurpose
{
    Normal = 0,
    AsNewFile = 1,
    IntoFile = 2,
    Testing = 3,
    Autosave = 4,
}
