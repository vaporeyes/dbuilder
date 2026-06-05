// ABOUTME: Modal options dialog for UDB-style Select Similar property matching.
// ABOUTME: Builds mode-specific similarity option objects consumed by the map selection helpers.

using Avalonia.Controls;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class SelectSimilarDialog : PropertyDialog
{
    private static VertexSimilarityOptions SavedVertexOptions { get; set; } = new();
    private static SectorSimilarityOptions SavedSectorOptions { get; set; } = new();
    private static LinedefSimilarityOptions SavedLinedefOptions { get; set; } = new();
    private static SidedefSimilarityOptions SavedSidedefOptions { get; set; } = new();
    private static ThingSimilarityOptions SavedThingOptions { get; set; } = new();

    private readonly MapControl.EditMode _mode;
    private readonly CheckBox? _vertexZFloor;
    private readonly CheckBox? _vertexZCeiling;
    private readonly CheckBox? _vertexFields;
    private readonly CheckBox? _sectorFloorHeight;
    private readonly CheckBox? _sectorCeilingHeight;
    private readonly CheckBox? _sectorFloorTexture;
    private readonly CheckBox? _sectorCeilingTexture;
    private readonly CheckBox? _sectorFloorTextureOffsets;
    private readonly CheckBox? _sectorCeilingTextureOffsets;
    private readonly CheckBox? _sectorFloorTextureScale;
    private readonly CheckBox? _sectorCeilingTextureScale;
    private readonly CheckBox? _sectorFloorTextureRotation;
    private readonly CheckBox? _sectorCeilingTextureRotation;
    private readonly CheckBox? _sectorFloorAlpha;
    private readonly CheckBox? _sectorCeilingAlpha;
    private readonly CheckBox? _sectorFloorPortalAlpha;
    private readonly CheckBox? _sectorCeilingPortalAlpha;
    private readonly CheckBox? _sectorBrightness;
    private readonly CheckBox? _sectorFloorBrightness;
    private readonly CheckBox? _sectorCeilingBrightness;
    private readonly CheckBox? _sectorFloorRenderStyle;
    private readonly CheckBox? _sectorCeilingRenderStyle;
    private readonly CheckBox? _sectorFloorPortalRenderStyle;
    private readonly CheckBox? _sectorCeilingPortalRenderStyle;
    private readonly CheckBox? _sectorSpecial;
    private readonly CheckBox? _sectorTags;
    private readonly CheckBox? _sectorSlopes;
    private readonly CheckBox? _sectorFlags;
    private readonly CheckBox? _sectorFloorTerrain;
    private readonly CheckBox? _sectorCeilingTerrain;
    private readonly CheckBox? _sectorLightColor;
    private readonly CheckBox? _sectorFadeColor;
    private readonly CheckBox? _sectorFloorColor;
    private readonly CheckBox? _sectorCeilingColor;
    private readonly CheckBox? _sectorTopWallColor;
    private readonly CheckBox? _sectorBottomWallColor;
    private readonly CheckBox? _sectorSpritesColor;
    private readonly CheckBox? _sectorFloorGlow;
    private readonly CheckBox? _sectorCeilingGlow;
    private readonly CheckBox? _sectorFogDensity;
    private readonly CheckBox? _sectorDesaturation;
    private readonly CheckBox? _sectorDamageType;
    private readonly CheckBox? _sectorDamageAmount;
    private readonly CheckBox? _sectorDamageInterval;
    private readonly CheckBox? _sectorDamageLeakiness;
    private readonly CheckBox? _sectorSoundSequence;
    private readonly CheckBox? _sectorGravity;
    private readonly CheckBox? _sectorComment;
    private readonly CheckBox? _sectorFields;
    private readonly CheckBox? _linedefAction;
    private readonly CheckBox? _linedefArguments;
    private readonly CheckBox? _linedefActivation;
    private readonly CheckBox? _linedefTags;
    private readonly CheckBox? _linedefFlags;
    private readonly CheckBox? _linedefAlpha;
    private readonly CheckBox? _linedefRenderStyle;
    private readonly CheckBox? _linedefLockNumber;
    private readonly CheckBox? _linedefComment;
    private readonly CheckBox? _linedefFields;
    private readonly CheckBox? _sidedefUpperTexture;
    private readonly CheckBox? _sidedefMiddleTexture;
    private readonly CheckBox? _sidedefLowerTexture;
    private readonly CheckBox? _sidedefOffsetX;
    private readonly CheckBox? _sidedefOffsetY;
    private readonly CheckBox? _sidedefUpperTextureOffsets;
    private readonly CheckBox? _sidedefMiddleTextureOffsets;
    private readonly CheckBox? _sidedefLowerTextureOffsets;
    private readonly CheckBox? _sidedefUpperTextureScale;
    private readonly CheckBox? _sidedefMiddleTextureScale;
    private readonly CheckBox? _sidedefLowerTextureScale;
    private readonly CheckBox? _sidedefBrightness;
    private readonly CheckBox? _sidedefFlags;
    private readonly CheckBox? _sidedefFields;
    private readonly CheckBox? _thingType;
    private readonly CheckBox? _thingAngle;
    private readonly CheckBox? _thingHeight;
    private readonly CheckBox? _thingPitch;
    private readonly CheckBox? _thingRoll;
    private readonly CheckBox? _thingScale;
    private readonly CheckBox? _thingAction;
    private readonly CheckBox? _thingArguments;
    private readonly CheckBox? _thingTag;
    private readonly CheckBox? _thingFlags;
    private readonly CheckBox? _thingConversation;
    private readonly CheckBox? _thingGravity;
    private readonly CheckBox? _thingHealth;
    private readonly CheckBox? _thingScore;
    private readonly CheckBox? _thingFloatBobPhase;
    private readonly CheckBox? _thingAlpha;
    private readonly CheckBox? _thingFillColor;
    private readonly CheckBox? _thingRenderStyle;
    private readonly CheckBox? _thingComment;
    private readonly CheckBox? _thingFields;

    public VertexSimilarityOptions VertexOptions { get; private set; } = new();
    public SectorSimilarityOptions SectorOptions { get; private set; } = new();
    public LinedefSimilarityOptions LinedefOptions { get; private set; } = new();
    public SidedefSimilarityOptions SidedefOptions { get; private set; } = new();
    public ThingSimilarityOptions ThingOptions { get; private set; } = new();

    public SelectSimilarDialog(MapControl.EditMode mode)
        : base("Select Similar", "Choose which properties must match the current selection.")
    {
        _mode = mode;
        switch (mode)
        {
            case MapControl.EditMode.Vertices:
                _vertexZFloor = AddCheckBox("Vertex floor height", SavedVertexOptions.ZFloor);
                _vertexZCeiling = AddCheckBox("Vertex ceiling height", SavedVertexOptions.ZCeiling);
                _vertexFields = AddCheckBox("Custom fields", SavedVertexOptions.Fields);
                break;
            case MapControl.EditMode.Sectors:
                _sectorFloorHeight = AddCheckBox("Floor height", SavedSectorOptions.FloorHeight);
                _sectorCeilingHeight = AddCheckBox("Ceiling height", SavedSectorOptions.CeilingHeight);
                _sectorFloorTexture = AddCheckBox("Floor texture", SavedSectorOptions.FloorTexture);
                _sectorCeilingTexture = AddCheckBox("Ceiling texture", SavedSectorOptions.CeilingTexture);
                _sectorFloorTextureOffsets = AddCheckBox("Floor texture offsets", SavedSectorOptions.FloorTextureOffsets);
                _sectorCeilingTextureOffsets = AddCheckBox("Ceiling texture offsets", SavedSectorOptions.CeilingTextureOffsets);
                _sectorFloorTextureScale = AddCheckBox("Floor texture scale", SavedSectorOptions.FloorTextureScale);
                _sectorCeilingTextureScale = AddCheckBox("Ceiling texture scale", SavedSectorOptions.CeilingTextureScale);
                _sectorFloorTextureRotation = AddCheckBox("Floor texture rotation", SavedSectorOptions.FloorTextureRotation);
                _sectorCeilingTextureRotation = AddCheckBox("Ceiling texture rotation", SavedSectorOptions.CeilingTextureRotation);
                _sectorFloorAlpha = AddCheckBox("Floor alpha", SavedSectorOptions.FloorAlpha);
                _sectorCeilingAlpha = AddCheckBox("Ceiling alpha", SavedSectorOptions.CeilingAlpha);
                _sectorFloorPortalAlpha = AddCheckBox("Floor portal alpha", SavedSectorOptions.FloorPortalAlpha);
                _sectorCeilingPortalAlpha = AddCheckBox("Ceiling portal alpha", SavedSectorOptions.CeilingPortalAlpha);
                _sectorBrightness = AddCheckBox("Sector brightness", SavedSectorOptions.Brightness);
                _sectorFloorBrightness = AddCheckBox("Floor brightness", SavedSectorOptions.FloorBrightness);
                _sectorCeilingBrightness = AddCheckBox("Ceiling brightness", SavedSectorOptions.CeilingBrightness);
                _sectorFloorRenderStyle = AddCheckBox("Floor render style", SavedSectorOptions.FloorRenderStyle);
                _sectorCeilingRenderStyle = AddCheckBox("Ceiling render style", SavedSectorOptions.CeilingRenderStyle);
                _sectorFloorPortalRenderStyle = AddCheckBox("Floor portal render style", SavedSectorOptions.FloorPortalRenderStyle);
                _sectorCeilingPortalRenderStyle = AddCheckBox("Ceiling portal render style", SavedSectorOptions.CeilingPortalRenderStyle);
                _sectorSpecial = AddCheckBox("Effect", SavedSectorOptions.Special);
                _sectorTags = AddCheckBox("Tags", SavedSectorOptions.Tags);
                _sectorSlopes = AddCheckBox("Slopes", SavedSectorOptions.Slopes);
                _sectorFlags = AddCheckBox("Flags", SavedSectorOptions.Flags);
                _sectorFloorTerrain = AddCheckBox("Floor terrain", SavedSectorOptions.FloorTerrain);
                _sectorCeilingTerrain = AddCheckBox("Ceiling terrain", SavedSectorOptions.CeilingTerrain);
                _sectorLightColor = AddCheckBox("Light color", SavedSectorOptions.LightColor);
                _sectorFadeColor = AddCheckBox("Fade color", SavedSectorOptions.FadeColor);
                _sectorFloorColor = AddCheckBox("Floor color", SavedSectorOptions.FloorColor);
                _sectorCeilingColor = AddCheckBox("Ceiling color", SavedSectorOptions.CeilingColor);
                _sectorTopWallColor = AddCheckBox("Top wall color", SavedSectorOptions.TopWallColor);
                _sectorBottomWallColor = AddCheckBox("Bottom wall color", SavedSectorOptions.BottomWallColor);
                _sectorSpritesColor = AddCheckBox("Sprites color", SavedSectorOptions.SpritesColor);
                _sectorFloorGlow = AddCheckBox("Floor glow", SavedSectorOptions.FloorGlow);
                _sectorCeilingGlow = AddCheckBox("Ceiling glow", SavedSectorOptions.CeilingGlow);
                _sectorFogDensity = AddCheckBox("Fog density", SavedSectorOptions.FogDensity);
                _sectorDesaturation = AddCheckBox("Desaturation", SavedSectorOptions.Desaturation);
                _sectorDamageType = AddCheckBox("Damage type", SavedSectorOptions.DamageType);
                _sectorDamageAmount = AddCheckBox("Damage amount", SavedSectorOptions.DamageAmount);
                _sectorDamageInterval = AddCheckBox("Damage interval", SavedSectorOptions.DamageInterval);
                _sectorDamageLeakiness = AddCheckBox("Damage leakiness", SavedSectorOptions.DamageLeakiness);
                _sectorSoundSequence = AddCheckBox("Sound sequence", SavedSectorOptions.SoundSequence);
                _sectorGravity = AddCheckBox("Gravity", SavedSectorOptions.Gravity);
                _sectorFields = AddCheckBox("Custom fields", SavedSectorOptions.Fields);
                _sectorComment = AddCheckBox("Comment", SavedSectorOptions.Comment);
                break;
            case MapControl.EditMode.Linedefs:
                _linedefAction = AddCheckBox("Action", SavedLinedefOptions.Action);
                _linedefArguments = AddCheckBox("Action arguments", SavedLinedefOptions.Arguments);
                _linedefActivation = AddCheckBox("Activation", SavedLinedefOptions.Activation);
                _linedefTags = AddCheckBox("Tags", SavedLinedefOptions.Tags);
                _linedefFlags = AddCheckBox("Linedef flags", SavedLinedefOptions.Flags);
                _linedefAlpha = AddCheckBox("Alpha", SavedLinedefOptions.Alpha);
                _linedefRenderStyle = AddCheckBox("Render style", SavedLinedefOptions.RenderStyle);
                _linedefLockNumber = AddCheckBox("Lock number", SavedLinedefOptions.LockNumber);
                _linedefFields = AddCheckBox("Linedef custom fields", SavedLinedefOptions.Fields);
                _linedefComment = AddCheckBox("Comment", SavedLinedefOptions.Comment);
                _sidedefUpperTexture = AddCheckBox("Upper texture", SavedSidedefOptions.UpperTexture);
                _sidedefMiddleTexture = AddCheckBox("Middle texture", SavedSidedefOptions.MiddleTexture);
                _sidedefLowerTexture = AddCheckBox("Lower texture", SavedSidedefOptions.LowerTexture);
                _sidedefOffsetX = AddCheckBox("Texture offset X", SavedSidedefOptions.OffsetX);
                _sidedefOffsetY = AddCheckBox("Texture offset Y", SavedSidedefOptions.OffsetY);
                _sidedefUpperTextureOffsets = AddCheckBox("Upper texture offsets", SavedSidedefOptions.UpperTextureOffsets);
                _sidedefMiddleTextureOffsets = AddCheckBox("Middle texture offsets", SavedSidedefOptions.MiddleTextureOffsets);
                _sidedefLowerTextureOffsets = AddCheckBox("Lower texture offsets", SavedSidedefOptions.LowerTextureOffsets);
                _sidedefUpperTextureScale = AddCheckBox("Upper texture scale", SavedSidedefOptions.UpperTextureScale);
                _sidedefMiddleTextureScale = AddCheckBox("Middle texture scale", SavedSidedefOptions.MiddleTextureScale);
                _sidedefLowerTextureScale = AddCheckBox("Lower texture scale", SavedSidedefOptions.LowerTextureScale);
                _sidedefBrightness = AddCheckBox("Brightness", SavedSidedefOptions.Brightness);
                _sidedefFlags = AddCheckBox("Sidedef flags", SavedSidedefOptions.Flags);
                _sidedefFields = AddCheckBox("Sidedef custom fields", SavedSidedefOptions.Fields);
                break;
            case MapControl.EditMode.Things:
                _thingType = AddCheckBox("Type", SavedThingOptions.Type);
                _thingAngle = AddCheckBox("Angle", SavedThingOptions.Angle);
                _thingHeight = AddCheckBox("Z-height", SavedThingOptions.Height);
                _thingPitch = AddCheckBox("Pitch", SavedThingOptions.Pitch);
                _thingRoll = AddCheckBox("Roll", SavedThingOptions.Roll);
                _thingScale = AddCheckBox("Scale", SavedThingOptions.Scale);
                _thingAction = AddCheckBox("Action", SavedThingOptions.Action);
                _thingArguments = AddCheckBox("Action arguments", SavedThingOptions.Arguments);
                _thingTag = AddCheckBox("Tag", SavedThingOptions.Tag);
                _thingFlags = AddCheckBox("Flags", SavedThingOptions.Flags);
                _thingConversation = AddCheckBox("Conversation ID", SavedThingOptions.Conversation);
                _thingGravity = AddCheckBox("Gravity", SavedThingOptions.Gravity);
                _thingHealth = AddCheckBox("Health multiplier", SavedThingOptions.Health);
                _thingScore = AddCheckBox("Score", SavedThingOptions.Score);
                _thingFloatBobPhase = AddCheckBox("Float bob phase", SavedThingOptions.FloatBobPhase);
                _thingAlpha = AddCheckBox("Alpha", SavedThingOptions.Alpha);
                _thingFillColor = AddCheckBox("Fill color", SavedThingOptions.FillColor);
                _thingRenderStyle = AddCheckBox("Render style", SavedThingOptions.RenderStyle);
                _thingFields = AddCheckBox("Custom fields", SavedThingOptions.Fields);
                _thingComment = AddCheckBox("Comment", SavedThingOptions.Comment);
                break;
        }
    }

    protected override void OnConfirm()
    {
        switch (_mode)
        {
            case MapControl.EditMode.Vertices:
                VertexOptions = new VertexSimilarityOptions
                {
                    ZFloor = Checked(_vertexZFloor),
                    ZCeiling = Checked(_vertexZCeiling),
                    Fields = Checked(_vertexFields),
                };
                SavedVertexOptions = VertexOptions;
                break;
            case MapControl.EditMode.Sectors:
                SectorOptions = new SectorSimilarityOptions
                {
                    FloorHeight = Checked(_sectorFloorHeight),
                    CeilingHeight = Checked(_sectorCeilingHeight),
                    FloorTexture = Checked(_sectorFloorTexture),
                    CeilingTexture = Checked(_sectorCeilingTexture),
                    FloorTextureOffsets = Checked(_sectorFloorTextureOffsets),
                    CeilingTextureOffsets = Checked(_sectorCeilingTextureOffsets),
                    FloorTextureScale = Checked(_sectorFloorTextureScale),
                    CeilingTextureScale = Checked(_sectorCeilingTextureScale),
                    FloorTextureRotation = Checked(_sectorFloorTextureRotation),
                    CeilingTextureRotation = Checked(_sectorCeilingTextureRotation),
                    FloorAlpha = Checked(_sectorFloorAlpha),
                    CeilingAlpha = Checked(_sectorCeilingAlpha),
                    FloorPortalAlpha = Checked(_sectorFloorPortalAlpha),
                    CeilingPortalAlpha = Checked(_sectorCeilingPortalAlpha),
                    Brightness = Checked(_sectorBrightness),
                    FloorBrightness = Checked(_sectorFloorBrightness),
                    CeilingBrightness = Checked(_sectorCeilingBrightness),
                    FloorRenderStyle = Checked(_sectorFloorRenderStyle),
                    CeilingRenderStyle = Checked(_sectorCeilingRenderStyle),
                    FloorPortalRenderStyle = Checked(_sectorFloorPortalRenderStyle),
                    CeilingPortalRenderStyle = Checked(_sectorCeilingPortalRenderStyle),
                    Special = Checked(_sectorSpecial),
                    Tags = Checked(_sectorTags),
                    Slopes = Checked(_sectorSlopes),
                    Flags = Checked(_sectorFlags),
                    FloorTerrain = Checked(_sectorFloorTerrain),
                    CeilingTerrain = Checked(_sectorCeilingTerrain),
                    LightColor = Checked(_sectorLightColor),
                    FadeColor = Checked(_sectorFadeColor),
                    FloorColor = Checked(_sectorFloorColor),
                    CeilingColor = Checked(_sectorCeilingColor),
                    TopWallColor = Checked(_sectorTopWallColor),
                    BottomWallColor = Checked(_sectorBottomWallColor),
                    SpritesColor = Checked(_sectorSpritesColor),
                    FloorGlow = Checked(_sectorFloorGlow),
                    CeilingGlow = Checked(_sectorCeilingGlow),
                    FogDensity = Checked(_sectorFogDensity),
                    Desaturation = Checked(_sectorDesaturation),
                    DamageType = Checked(_sectorDamageType),
                    DamageAmount = Checked(_sectorDamageAmount),
                    DamageInterval = Checked(_sectorDamageInterval),
                    DamageLeakiness = Checked(_sectorDamageLeakiness),
                    SoundSequence = Checked(_sectorSoundSequence),
                    Gravity = Checked(_sectorGravity),
                    Comment = Checked(_sectorComment),
                    Fields = Checked(_sectorFields),
                };
                SavedSectorOptions = SectorOptions;
                break;
            case MapControl.EditMode.Linedefs:
                LinedefOptions = new LinedefSimilarityOptions
                {
                    Action = Checked(_linedefAction),
                    Arguments = Checked(_linedefArguments),
                    Activation = Checked(_linedefActivation),
                    Tags = Checked(_linedefTags),
                    Flags = Checked(_linedefFlags),
                    Alpha = Checked(_linedefAlpha),
                    RenderStyle = Checked(_linedefRenderStyle),
                    LockNumber = Checked(_linedefLockNumber),
                    Comment = Checked(_linedefComment),
                    Fields = Checked(_linedefFields),
                };
                SidedefOptions = new SidedefSimilarityOptions
                {
                    UpperTexture = Checked(_sidedefUpperTexture),
                    MiddleTexture = Checked(_sidedefMiddleTexture),
                    LowerTexture = Checked(_sidedefLowerTexture),
                    OffsetX = Checked(_sidedefOffsetX),
                    OffsetY = Checked(_sidedefOffsetY),
                    UpperTextureOffsets = Checked(_sidedefUpperTextureOffsets),
                    MiddleTextureOffsets = Checked(_sidedefMiddleTextureOffsets),
                    LowerTextureOffsets = Checked(_sidedefLowerTextureOffsets),
                    UpperTextureScale = Checked(_sidedefUpperTextureScale),
                    MiddleTextureScale = Checked(_sidedefMiddleTextureScale),
                    LowerTextureScale = Checked(_sidedefLowerTextureScale),
                    Brightness = Checked(_sidedefBrightness),
                    Flags = Checked(_sidedefFlags),
                    Fields = Checked(_sidedefFields),
                };
                SavedLinedefOptions = LinedefOptions;
                SavedSidedefOptions = SidedefOptions;
                break;
            case MapControl.EditMode.Things:
                ThingOptions = new ThingSimilarityOptions
                {
                    Type = Checked(_thingType),
                    Angle = Checked(_thingAngle),
                    Height = Checked(_thingHeight),
                    Pitch = Checked(_thingPitch),
                    Roll = Checked(_thingRoll),
                    Scale = Checked(_thingScale),
                    Action = Checked(_thingAction),
                    Arguments = Checked(_thingArguments),
                    Tag = Checked(_thingTag),
                    Flags = Checked(_thingFlags),
                    Conversation = Checked(_thingConversation),
                    Gravity = Checked(_thingGravity),
                    Health = Checked(_thingHealth),
                    Score = Checked(_thingScore),
                    FloatBobPhase = Checked(_thingFloatBobPhase),
                    Alpha = Checked(_thingAlpha),
                    FillColor = Checked(_thingFillColor),
                    RenderStyle = Checked(_thingRenderStyle),
                    Fields = Checked(_thingFields),
                    Comment = Checked(_thingComment),
                };
                SavedThingOptions = ThingOptions;
                break;
        }
    }

    private static bool Checked(CheckBox? checkBox) => checkBox?.IsChecked == true;
}
