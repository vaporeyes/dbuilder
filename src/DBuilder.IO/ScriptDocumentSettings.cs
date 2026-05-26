// ABOUTME: Persisted script editor document state from UDB map options.
// ABOUTME: Stores tab identity, view state and fold levels without depending on editor UI types.

namespace DBuilder.IO;

public enum ScriptDocumentTabType
{
    Lump,
    Resource,
    File,
}

public enum ScriptType
{
    Unknown,
    Acs,
    ModelDef,
    Decorate,
    Gldefs,
    SndSeq,
    MapInfo,
    VoxelDef,
    Textures,
    Animdefs,
    Reverbs,
    Terrain,
    X11R6Rgb,
    CvarInfo,
    SndInfo,
    LockDefs,
    MenuDef,
    SbarInfo,
    Usdf,
    GameInfo,
    KeyConf,
    FontDefs,
    ZScript,
    DecalDef,
}

public sealed class ScriptDocumentSettings
{
    public Dictionary<int, HashSet<int>> FoldLevels { get; } = new();
    public int CaretPosition { get; set; }
    public int FirstVisibleLine { get; set; }
    public string Filename { get; set; } = "";
    public string ResourceLocation { get; set; } = "";
    public ScriptType ScriptType { get; set; }
    public ScriptDocumentTabType TabType { get; set; }
    public bool IsActiveTab { get; set; }
    public long Hash { get; set; }
}
