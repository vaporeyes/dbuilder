// ABOUTME: Modal options dialog for UDB-style Select Similar property matching.
// ABOUTME: Builds mode-specific similarity option objects consumed by the map selection helpers.

using Avalonia.Controls;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class SelectSimilarDialog : PropertyDialog
{
    private readonly MapControl.EditMode _mode;
    private readonly CheckBox? _vertexZFloor;
    private readonly CheckBox? _vertexZCeiling;
    private readonly CheckBox? _vertexFields;
    private readonly CheckBox? _sectorFloorHeight;
    private readonly CheckBox? _sectorCeilingHeight;
    private readonly CheckBox? _sectorFloorTexture;
    private readonly CheckBox? _sectorCeilingTexture;
    private readonly CheckBox? _sectorBrightness;
    private readonly CheckBox? _sectorSpecial;
    private readonly CheckBox? _sectorTags;
    private readonly CheckBox? _sectorSlopes;
    private readonly CheckBox? _sectorFlags;
    private readonly CheckBox? _sectorFields;
    private readonly CheckBox? _linedefAction;
    private readonly CheckBox? _linedefArguments;
    private readonly CheckBox? _linedefActivation;
    private readonly CheckBox? _linedefTags;
    private readonly CheckBox? _linedefFlags;
    private readonly CheckBox? _linedefFields;
    private readonly CheckBox? _sidedefUpperTexture;
    private readonly CheckBox? _sidedefMiddleTexture;
    private readonly CheckBox? _sidedefLowerTexture;
    private readonly CheckBox? _sidedefOffsetX;
    private readonly CheckBox? _sidedefOffsetY;
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
                _vertexZFloor = AddCheckBox("Vertex floor height", true);
                _vertexZCeiling = AddCheckBox("Vertex ceiling height", true);
                _vertexFields = AddCheckBox("Custom fields", true);
                break;
            case MapControl.EditMode.Sectors:
                _sectorFloorHeight = AddCheckBox("Floor height", true);
                _sectorCeilingHeight = AddCheckBox("Ceiling height", true);
                _sectorFloorTexture = AddCheckBox("Floor texture", true);
                _sectorCeilingTexture = AddCheckBox("Ceiling texture", true);
                _sectorBrightness = AddCheckBox("Sector brightness", true);
                _sectorSpecial = AddCheckBox("Effect", true);
                _sectorTags = AddCheckBox("Tags", true);
                _sectorSlopes = AddCheckBox("Slopes", true);
                _sectorFlags = AddCheckBox("Flags", true);
                _sectorFields = AddCheckBox("Custom fields", true);
                break;
            case MapControl.EditMode.Linedefs:
                _linedefAction = AddCheckBox("Action", true);
                _linedefArguments = AddCheckBox("Action arguments", true);
                _linedefActivation = AddCheckBox("Activation", true);
                _linedefTags = AddCheckBox("Tags", true);
                _linedefFlags = AddCheckBox("Linedef flags", true);
                _linedefFields = AddCheckBox("Linedef custom fields", true);
                _sidedefUpperTexture = AddCheckBox("Upper texture", true);
                _sidedefMiddleTexture = AddCheckBox("Middle texture", true);
                _sidedefLowerTexture = AddCheckBox("Lower texture", true);
                _sidedefOffsetX = AddCheckBox("Texture offset X", true);
                _sidedefOffsetY = AddCheckBox("Texture offset Y", true);
                _sidedefFlags = AddCheckBox("Sidedef flags", true);
                _sidedefFields = AddCheckBox("Sidedef custom fields", true);
                break;
            case MapControl.EditMode.Things:
                _thingType = AddCheckBox("Type", true);
                _thingAngle = AddCheckBox("Angle", true);
                _thingHeight = AddCheckBox("Z-height", true);
                _thingPitch = AddCheckBox("Pitch", true);
                _thingRoll = AddCheckBox("Roll", true);
                _thingScale = AddCheckBox("Scale", true);
                _thingAction = AddCheckBox("Action", true);
                _thingArguments = AddCheckBox("Action arguments", true);
                _thingTag = AddCheckBox("Tag", true);
                _thingFlags = AddCheckBox("Flags", true);
                _thingFields = AddCheckBox("Custom fields", true);
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
                break;
            case MapControl.EditMode.Sectors:
                SectorOptions = new SectorSimilarityOptions
                {
                    FloorHeight = Checked(_sectorFloorHeight),
                    CeilingHeight = Checked(_sectorCeilingHeight),
                    FloorTexture = Checked(_sectorFloorTexture),
                    CeilingTexture = Checked(_sectorCeilingTexture),
                    Brightness = Checked(_sectorBrightness),
                    Special = Checked(_sectorSpecial),
                    Tags = Checked(_sectorTags),
                    Slopes = Checked(_sectorSlopes),
                    Flags = Checked(_sectorFlags),
                    Fields = Checked(_sectorFields),
                };
                break;
            case MapControl.EditMode.Linedefs:
                LinedefOptions = new LinedefSimilarityOptions
                {
                    Action = Checked(_linedefAction),
                    Arguments = Checked(_linedefArguments),
                    Activation = Checked(_linedefActivation),
                    Tags = Checked(_linedefTags),
                    Flags = Checked(_linedefFlags),
                    Fields = Checked(_linedefFields),
                };
                SidedefOptions = new SidedefSimilarityOptions
                {
                    UpperTexture = Checked(_sidedefUpperTexture),
                    MiddleTexture = Checked(_sidedefMiddleTexture),
                    LowerTexture = Checked(_sidedefLowerTexture),
                    OffsetX = Checked(_sidedefOffsetX),
                    OffsetY = Checked(_sidedefOffsetY),
                    Flags = Checked(_sidedefFlags),
                    Fields = Checked(_sidedefFields),
                };
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
                    Fields = Checked(_thingFields),
                };
                break;
        }
    }

    private static bool Checked(CheckBox? checkBox) => checkBox?.IsChecked == true;
}
