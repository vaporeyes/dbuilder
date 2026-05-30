// ABOUTME: Paste option model matching UDB's copy and paste preferences for pasted map geometry.
// ABOUTME: Controls whether pasted tags are kept, renumbered, or removed, and whether pasted actions are cleared.

namespace DBuilder.IO;

public enum PasteTagMode
{
    Keep = 0,
    Renumber = 1,
    Remove = 2,
}

public sealed class PasteOptions
{
    public PasteTagMode ChangeTags { get; init; } = PasteTagMode.Keep;
    public bool RemoveActions { get; init; }

    public PasteOptions Copy()
        => new()
        {
            ChangeTags = ChangeTags,
            RemoveActions = RemoveActions,
        };
}
