// ABOUTME: Modal options dialog for UDB-style Select Similar property matching.
// ABOUTME: Builds mode-specific similarity option objects consumed by the map selection helpers.

using Avalonia.Controls;
using Avalonia.Layout;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class SelectSimilarDialog : PropertyDialog
{
    private static VertexSimilarityOptions SavedVertexOptions { get; set; } = new();
    private static SectorSimilarityOptions SavedSectorOptions { get; set; } = new();
    private static LinedefSimilarityOptions SavedLinedefOptions { get; set; } = new();
    private static SidedefSimilarityOptions SavedSidedefOptions { get; set; } = new();
    private static ThingSimilarityOptions SavedThingOptions { get; set; } = new();

    private readonly List<CheckBox> _visibleChecks = new();
    private readonly MapControl.EditMode _mode;
    private readonly MapFormat _mapFormat;
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

    public SelectSimilarDialog(MapControl.EditMode mode, MapFormat mapFormat)
        : base("Select Similar", "Choose which properties must match the current selection.")
    {
        _mode = mode;
        _mapFormat = mapFormat;
        switch (mode)
        {
            case MapControl.EditMode.Vertices:
                _vertexZFloor = AddUdmfCheckBox("Vertex floor height", SavedVertexOptions.ZFloor);
                _vertexZCeiling = AddUdmfCheckBox("Vertex ceiling height", SavedVertexOptions.ZCeiling);
                _vertexFields = AddUdmfCheckBox("Custom fields", SavedVertexOptions.Fields);
                break;
            case MapControl.EditMode.Sectors:
                _sectorFloorHeight = AddCheckBox("Floor height", SavedSectorOptions.FloorHeight);
                _sectorCeilingHeight = AddCheckBox("Ceiling height", SavedSectorOptions.CeilingHeight);
                _sectorFloorTexture = AddCheckBox("Floor texture", SavedSectorOptions.FloorTexture);
                _sectorCeilingTexture = AddCheckBox("Ceiling texture", SavedSectorOptions.CeilingTexture);
                _sectorFloorTextureOffsets = AddUdmfCheckBox("Floor texture offsets", SavedSectorOptions.FloorTextureOffsets);
                _sectorCeilingTextureOffsets = AddUdmfCheckBox("Ceiling texture offsets", SavedSectorOptions.CeilingTextureOffsets);
                _sectorFloorTextureScale = AddUdmfCheckBox("Floor texture scale", SavedSectorOptions.FloorTextureScale);
                _sectorCeilingTextureScale = AddUdmfCheckBox("Ceiling texture scale", SavedSectorOptions.CeilingTextureScale);
                _sectorFloorTextureRotation = AddUdmfCheckBox("Floor texture rotation", SavedSectorOptions.FloorTextureRotation);
                _sectorCeilingTextureRotation = AddUdmfCheckBox("Ceiling texture rotation", SavedSectorOptions.CeilingTextureRotation);
                _sectorFloorAlpha = AddUdmfCheckBox("Floor alpha", SavedSectorOptions.FloorAlpha);
                _sectorCeilingAlpha = AddUdmfCheckBox("Ceiling alpha", SavedSectorOptions.CeilingAlpha);
                _sectorFloorPortalAlpha = AddUdmfCheckBox("Floor portal alpha", SavedSectorOptions.FloorPortalAlpha);
                _sectorCeilingPortalAlpha = AddUdmfCheckBox("Ceiling portal alpha", SavedSectorOptions.CeilingPortalAlpha);
                _sectorBrightness = AddCheckBox("Sector brightness", SavedSectorOptions.Brightness);
                _sectorFloorBrightness = AddUdmfCheckBox("Floor brightness", SavedSectorOptions.FloorBrightness);
                _sectorCeilingBrightness = AddUdmfCheckBox("Ceiling brightness", SavedSectorOptions.CeilingBrightness);
                _sectorFloorRenderStyle = AddUdmfCheckBox("Floor render style", SavedSectorOptions.FloorRenderStyle);
                _sectorCeilingRenderStyle = AddUdmfCheckBox("Ceiling render style", SavedSectorOptions.CeilingRenderStyle);
                _sectorFloorPortalRenderStyle = AddUdmfCheckBox("Floor portal render style", SavedSectorOptions.FloorPortalRenderStyle);
                _sectorCeilingPortalRenderStyle = AddUdmfCheckBox("Ceiling portal render style", SavedSectorOptions.CeilingPortalRenderStyle);
                _sectorSpecial = AddCheckBox("Effect", SavedSectorOptions.Special);
                _sectorTags = AddCheckBox("Tags", SavedSectorOptions.Tags);
                _sectorSlopes = AddUdmfCheckBox("Slopes", SavedSectorOptions.Slopes);
                _sectorFlags = AddUdmfCheckBox("Flags", SavedSectorOptions.Flags);
                _sectorFloorTerrain = AddUdmfCheckBox("Floor terrain", SavedSectorOptions.FloorTerrain);
                _sectorCeilingTerrain = AddUdmfCheckBox("Ceiling terrain", SavedSectorOptions.CeilingTerrain);
                _sectorLightColor = AddUdmfCheckBox("Light color", SavedSectorOptions.LightColor);
                _sectorFadeColor = AddUdmfCheckBox("Fade color", SavedSectorOptions.FadeColor);
                _sectorFloorColor = AddUdmfCheckBox("Floor color", SavedSectorOptions.FloorColor);
                _sectorCeilingColor = AddUdmfCheckBox("Ceiling color", SavedSectorOptions.CeilingColor);
                _sectorTopWallColor = AddUdmfCheckBox("Top wall color", SavedSectorOptions.TopWallColor);
                _sectorBottomWallColor = AddUdmfCheckBox("Bottom wall color", SavedSectorOptions.BottomWallColor);
                _sectorSpritesColor = AddUdmfCheckBox("Sprites color", SavedSectorOptions.SpritesColor);
                _sectorFloorGlow = AddUdmfCheckBox("Floor glow", SavedSectorOptions.FloorGlow);
                _sectorCeilingGlow = AddUdmfCheckBox("Ceiling glow", SavedSectorOptions.CeilingGlow);
                _sectorFogDensity = AddUdmfCheckBox("Fog density", SavedSectorOptions.FogDensity);
                _sectorDesaturation = AddUdmfCheckBox("Desaturation", SavedSectorOptions.Desaturation);
                _sectorDamageType = AddUdmfCheckBox("Damage type", SavedSectorOptions.DamageType);
                _sectorDamageAmount = AddUdmfCheckBox("Damage amount", SavedSectorOptions.DamageAmount);
                _sectorDamageInterval = AddUdmfCheckBox("Damage interval", SavedSectorOptions.DamageInterval);
                _sectorDamageLeakiness = AddUdmfCheckBox("Damage leakiness", SavedSectorOptions.DamageLeakiness);
                _sectorSoundSequence = AddUdmfCheckBox("Sound sequence", SavedSectorOptions.SoundSequence);
                _sectorGravity = AddUdmfCheckBox("Gravity", SavedSectorOptions.Gravity);
                _sectorFields = AddUdmfCheckBox("Custom fields", SavedSectorOptions.Fields);
                _sectorComment = AddUdmfCheckBox("Comment", SavedSectorOptions.Comment);
                break;
            case MapControl.EditMode.Linedefs:
                _linedefAction = AddCheckBox("Action", SavedLinedefOptions.Action);
                _linedefArguments = AddSupportedCheckBox("Action arguments", SavedLinedefOptions.Arguments, doom: false);
                _linedefActivation = AddSupportedCheckBox("Activation", SavedLinedefOptions.Activation, doom: false, udmf: false);
                _linedefTags = AddSupportedCheckBox("Tags", SavedLinedefOptions.Tags, hexen: false);
                _linedefFlags = AddCheckBox("Linedef flags", SavedLinedefOptions.Flags);
                _linedefAlpha = AddUdmfCheckBox("Alpha", SavedLinedefOptions.Alpha);
                _linedefRenderStyle = AddUdmfCheckBox("Render style", SavedLinedefOptions.RenderStyle);
                _linedefLockNumber = AddUdmfCheckBox("Lock number", SavedLinedefOptions.LockNumber);
                _linedefFields = AddUdmfCheckBox("Linedef custom fields", SavedLinedefOptions.Fields);
                _linedefComment = AddUdmfCheckBox("Comment", SavedLinedefOptions.Comment);
                _sidedefUpperTexture = AddCheckBox("Upper texture", SavedSidedefOptions.UpperTexture);
                _sidedefMiddleTexture = AddCheckBox("Middle texture", SavedSidedefOptions.MiddleTexture);
                _sidedefLowerTexture = AddCheckBox("Lower texture", SavedSidedefOptions.LowerTexture);
                _sidedefOffsetX = AddCheckBox("Texture offset X", SavedSidedefOptions.OffsetX);
                _sidedefOffsetY = AddCheckBox("Texture offset Y", SavedSidedefOptions.OffsetY);
                _sidedefUpperTextureOffsets = AddUdmfCheckBox("Upper texture offsets", SavedSidedefOptions.UpperTextureOffsets);
                _sidedefMiddleTextureOffsets = AddUdmfCheckBox("Middle texture offsets", SavedSidedefOptions.MiddleTextureOffsets);
                _sidedefLowerTextureOffsets = AddUdmfCheckBox("Lower texture offsets", SavedSidedefOptions.LowerTextureOffsets);
                _sidedefUpperTextureScale = AddUdmfCheckBox("Upper texture scale", SavedSidedefOptions.UpperTextureScale);
                _sidedefMiddleTextureScale = AddUdmfCheckBox("Middle texture scale", SavedSidedefOptions.MiddleTextureScale);
                _sidedefLowerTextureScale = AddUdmfCheckBox("Lower texture scale", SavedSidedefOptions.LowerTextureScale);
                _sidedefBrightness = AddUdmfCheckBox("Brightness", SavedSidedefOptions.Brightness);
                _sidedefFlags = AddUdmfCheckBox("Sidedef flags", SavedSidedefOptions.Flags);
                _sidedefFields = AddUdmfCheckBox("Sidedef custom fields", SavedSidedefOptions.Fields);
                break;
            case MapControl.EditMode.Things:
                _thingType = AddCheckBox("Type", SavedThingOptions.Type);
                _thingAngle = AddCheckBox("Angle", SavedThingOptions.Angle);
                _thingHeight = AddSupportedCheckBox("Z-height", SavedThingOptions.Height, doom: false);
                _thingPitch = AddUdmfCheckBox("Pitch", SavedThingOptions.Pitch);
                _thingRoll = AddUdmfCheckBox("Roll", SavedThingOptions.Roll);
                _thingScale = AddUdmfCheckBox("Scale", SavedThingOptions.Scale);
                _thingAction = AddSupportedCheckBox("Action", SavedThingOptions.Action, doom: false);
                _thingArguments = AddSupportedCheckBox("Action arguments", SavedThingOptions.Arguments, doom: false);
                _thingTag = AddSupportedCheckBox("Tag", SavedThingOptions.Tag, doom: false);
                _thingFlags = AddCheckBox("Flags", SavedThingOptions.Flags);
                _thingConversation = AddUdmfCheckBox("Conversation ID", SavedThingOptions.Conversation);
                _thingGravity = AddUdmfCheckBox("Gravity", SavedThingOptions.Gravity);
                _thingHealth = AddUdmfCheckBox("Health multiplier", SavedThingOptions.Health);
                _thingScore = AddUdmfCheckBox("Score", SavedThingOptions.Score);
                _thingFloatBobPhase = AddUdmfCheckBox("Float bob phase", SavedThingOptions.FloatBobPhase);
                _thingAlpha = AddUdmfCheckBox("Alpha", SavedThingOptions.Alpha);
                _thingFillColor = AddUdmfCheckBox("Fill color", SavedThingOptions.FillColor);
                _thingRenderStyle = AddUdmfCheckBox("Render style", SavedThingOptions.RenderStyle);
                _thingFields = AddUdmfCheckBox("Custom fields", SavedThingOptions.Fields);
                _thingComment = AddUdmfCheckBox("Comment", SavedThingOptions.Comment);
                break;
        }

        AddEnableAllButton();
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

    private CheckBox? AddUdmfCheckBox(string label, bool isChecked)
        => AddSupportedCheckBox(label, isChecked, doom: false, hexen: false);

    private CheckBox? AddSupportedCheckBox(string label, bool isChecked, bool doom = true, bool hexen = true, bool udmf = true)
        => SupportsCurrentMapFormat(doom, hexen, udmf) ? AddCheckBox(label, isChecked) : null;

    private new CheckBox AddCheckBox(string label, bool isChecked)
    {
        CheckBox check = base.AddCheckBox(label, isChecked);
        _visibleChecks.Add(check);
        return check;
    }

    private void AddEnableAllButton()
    {
        if (_visibleChecks.Count == 0) return;
        var enableAll = new Button { Content = "Enable All", MinWidth = 96 };
        enableAll.Click += (_, _) => ToggleVisibleChecks();
        AddCustomRow(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { enableAll },
        });
    }

    private void ToggleVisibleChecks()
    {
        bool enable = _visibleChecks[0].IsChecked != true;
        foreach (CheckBox check in _visibleChecks)
            check.IsChecked = enable;
    }

    private bool SupportsCurrentMapFormat(bool doom, bool hexen, bool udmf)
        => _mapFormat switch
        {
            MapFormat.Doom => doom,
            MapFormat.Hexen => hexen,
            MapFormat.Udmf => udmf,
            _ => false,
        };

    private static bool Checked(CheckBox? checkBox) => checkBox?.IsChecked == true;
}
