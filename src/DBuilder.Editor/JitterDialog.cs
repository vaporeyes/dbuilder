// ABOUTME: Dialog for applying BuilderEffects random jitter to selected map geometry and things.
// ABOUTME: Keeps compact jitter amount parsing in the editor layer while BuilderEffects owns transforms.

using System.Globalization;
using Avalonia.Controls;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class JitterDialog : PropertyDialog
{
    private static JitterOffsetMode s_floorOffsetMode = JitterOffsetMode.RaiseAndLower;
    private static JitterOffsetMode s_ceilingOffsetMode = JitterOffsetMode.RaiseAndLower;
    private static bool s_relativeThingPitch;
    private static bool s_relativeThingRoll;
    private static bool s_allowNegativeThingPitch;
    private static bool s_allowNegativeThingRoll;
    private static bool s_relativeThingScale;
    private static bool s_uniformThingScale;
    private static bool s_allowNegativeThingScaleX;
    private static bool s_allowNegativeThingScaleY;

    private readonly TextBox _positionAmount;
    private readonly TextBox _floorAmount;
    private readonly ComboBox _floorOffsetMode;
    private readonly TextBox _ceilingAmount;
    private readonly ComboBox _ceilingOffsetMode;
    private readonly TextBox _thingRotationAmount;
    private readonly TextBox _thingHeightAmount;
    private readonly TextBox _thingPitchAmount;
    private readonly TextBox _thingRollAmount;
    private readonly CheckBox _relativeThingPitch;
    private readonly CheckBox _relativeThingRoll;
    private readonly CheckBox _allowNegativeThingPitch;
    private readonly CheckBox _allowNegativeThingRoll;
    private readonly TextBox _thingScaleMinX;
    private readonly TextBox _thingScaleMaxX;
    private readonly TextBox _thingScaleMinY;
    private readonly TextBox _thingScaleMaxY;
    private readonly CheckBox _relativeThingScale;
    private readonly CheckBox _uniformThingScale;
    private readonly CheckBox _allowNegativeThingScaleX;
    private readonly CheckBox _allowNegativeThingScaleY;

    public int ResultPositionAmount { get; private set; } = 16;
    public int ResultFloorAmount { get; private set; } = 16;
    public JitterOffsetMode ResultFloorOffsetMode { get; private set; } = JitterOffsetMode.RaiseAndLower;
    public int ResultCeilingAmount { get; private set; } = 16;
    public JitterOffsetMode ResultCeilingOffsetMode { get; private set; } = JitterOffsetMode.RaiseAndLower;
    public int ResultThingRotationAmount { get; private set; } = 45;
    public int ResultThingHeightAmount { get; private set; } = 16;
    public int ResultThingPitchAmount { get; private set; }
    public int ResultThingRollAmount { get; private set; }
    public bool ResultRelativeThingPitch { get; private set; }
    public bool ResultRelativeThingRoll { get; private set; }
    public bool ResultAllowNegativeThingPitch { get; private set; }
    public bool ResultAllowNegativeThingRoll { get; private set; }
    public double ResultThingScaleMinX { get; private set; } = 1.0;
    public double ResultThingScaleMaxX { get; private set; } = 1.0;
    public double ResultThingScaleMinY { get; private set; } = 1.0;
    public double ResultThingScaleMaxY { get; private set; } = 1.0;
    public bool ResultRelativeThingScale { get; private set; }
    public bool ResultUniformThingScale { get; private set; }
    public bool ResultAllowNegativeThingScaleX { get; private set; }
    public bool ResultAllowNegativeThingScaleY { get; private set; }

    public JitterDialog(string title)
        : base(title)
    {
        ResultFloorOffsetMode = s_floorOffsetMode;
        ResultCeilingOffsetMode = s_ceilingOffsetMode;
        ResultRelativeThingPitch = s_relativeThingPitch;
        ResultRelativeThingRoll = s_relativeThingRoll;
        ResultAllowNegativeThingPitch = s_allowNegativeThingPitch;
        ResultAllowNegativeThingRoll = s_allowNegativeThingRoll;
        ResultRelativeThingScale = s_relativeThingScale;
        ResultUniformThingScale = s_uniformThingScale;
        ResultAllowNegativeThingScaleX = s_allowNegativeThingScaleX;
        ResultAllowNegativeThingScaleY = s_allowNegativeThingScaleY;

        _positionAmount = AddField("Position amount", ResultPositionAmount.ToString(CultureInfo.InvariantCulture));
        _floorAmount = AddField("Floor amount", ResultFloorAmount.ToString(CultureInfo.InvariantCulture));
        _floorOffsetMode = AddCombo("Floor offset mode", FloorOffsetModeItems(), (int)ResultFloorOffsetMode);
        _ceilingAmount = AddField("Ceiling amount", ResultCeilingAmount.ToString(CultureInfo.InvariantCulture));
        _ceilingOffsetMode = AddCombo("Ceiling offset mode", CeilingOffsetModeItems(), (int)ResultCeilingOffsetMode);
        _thingRotationAmount = AddField("Thing angle amount", ResultThingRotationAmount.ToString(CultureInfo.InvariantCulture));
        _thingHeightAmount = AddField("Thing height amount", ResultThingHeightAmount.ToString(CultureInfo.InvariantCulture));
        _thingPitchAmount = AddField("Thing pitch amount", ResultThingPitchAmount.ToString(CultureInfo.InvariantCulture));
        _thingRollAmount = AddField("Thing roll amount", ResultThingRollAmount.ToString(CultureInfo.InvariantCulture));
        _relativeThingPitch = AddCheckBox("Relative thing pitch", ResultRelativeThingPitch);
        _relativeThingRoll = AddCheckBox("Relative thing roll", ResultRelativeThingRoll);
        _allowNegativeThingPitch = AddCheckBox("Use negative pitch", ResultAllowNegativeThingPitch);
        _allowNegativeThingRoll = AddCheckBox("Use negative roll", ResultAllowNegativeThingRoll);
        _thingScaleMinX = AddField("Thing scale X min", ResultThingScaleMinX.ToString(CultureInfo.InvariantCulture));
        _thingScaleMaxX = AddField("Thing scale X max", ResultThingScaleMaxX.ToString(CultureInfo.InvariantCulture));
        _thingScaleMinY = AddField("Thing scale Y min", ResultThingScaleMinY.ToString(CultureInfo.InvariantCulture));
        _thingScaleMaxY = AddField("Thing scale Y max", ResultThingScaleMaxY.ToString(CultureInfo.InvariantCulture));
        _relativeThingScale = AddCheckBox("Relative thing scale", ResultRelativeThingScale);
        _uniformThingScale = AddCheckBox("Uniform thing scale", ResultUniformThingScale);
        _allowNegativeThingScaleX = AddCheckBox("Use negative width scale", ResultAllowNegativeThingScaleX);
        _allowNegativeThingScaleY = AddCheckBox("Use negative height scale", ResultAllowNegativeThingScaleY);
    }

    protected override void OnConfirm()
    {
        ResultPositionAmount = Math.Max(0, ParseInt(_positionAmount, ResultPositionAmount));
        ResultFloorAmount = Math.Max(0, ParseInt(_floorAmount, ResultFloorAmount));
        ResultFloorOffsetMode = (JitterOffsetMode)ComboNumber(_floorOffsetMode, (int)ResultFloorOffsetMode);
        ResultCeilingAmount = Math.Max(0, ParseInt(_ceilingAmount, ResultCeilingAmount));
        ResultCeilingOffsetMode = (JitterOffsetMode)ComboNumber(_ceilingOffsetMode, (int)ResultCeilingOffsetMode);
        ResultThingRotationAmount = Math.Max(0, ParseInt(_thingRotationAmount, ResultThingRotationAmount));
        ResultThingHeightAmount = Math.Max(0, ParseInt(_thingHeightAmount, ResultThingHeightAmount));
        ResultThingPitchAmount = Math.Max(0, ParseInt(_thingPitchAmount, ResultThingPitchAmount));
        ResultThingRollAmount = Math.Max(0, ParseInt(_thingRollAmount, ResultThingRollAmount));
        ResultRelativeThingPitch = _relativeThingPitch.IsChecked == true;
        ResultRelativeThingRoll = _relativeThingRoll.IsChecked == true;
        ResultAllowNegativeThingPitch = _allowNegativeThingPitch.IsChecked == true;
        ResultAllowNegativeThingRoll = _allowNegativeThingRoll.IsChecked == true;
        ResultThingScaleMinX = ParseDouble(_thingScaleMinX, ResultThingScaleMinX);
        ResultThingScaleMaxX = ParseDouble(_thingScaleMaxX, ResultThingScaleMaxX);
        ResultThingScaleMinY = ParseDouble(_thingScaleMinY, ResultThingScaleMinY);
        ResultThingScaleMaxY = ParseDouble(_thingScaleMaxY, ResultThingScaleMaxY);
        ResultRelativeThingScale = _relativeThingScale.IsChecked == true;
        ResultUniformThingScale = _uniformThingScale.IsChecked == true;
        ResultAllowNegativeThingScaleX = _allowNegativeThingScaleX.IsChecked == true;
        ResultAllowNegativeThingScaleY = _allowNegativeThingScaleY.IsChecked == true;

        s_floorOffsetMode = ResultFloorOffsetMode;
        s_ceilingOffsetMode = ResultCeilingOffsetMode;
        s_relativeThingPitch = ResultRelativeThingPitch;
        s_relativeThingRoll = ResultRelativeThingRoll;
        s_allowNegativeThingPitch = ResultAllowNegativeThingPitch;
        s_allowNegativeThingRoll = ResultAllowNegativeThingRoll;
        s_relativeThingScale = ResultRelativeThingScale;
        s_uniformThingScale = ResultUniformThingScale;
        s_allowNegativeThingScaleX = ResultAllowNegativeThingScaleX;
        s_allowNegativeThingScaleY = ResultAllowNegativeThingScaleY;
    }

    private static IEnumerable<CatalogItem> FloorOffsetModeItems()
    {
        yield return new CatalogItem((int)JitterOffsetMode.RaiseAndLower, "Raise and lower");
        yield return new CatalogItem((int)JitterOffsetMode.RaiseOnly, "Raise only");
        yield return new CatalogItem((int)JitterOffsetMode.LowerOnly, "Lower only");
    }

    private static IEnumerable<CatalogItem> CeilingOffsetModeItems()
    {
        yield return new CatalogItem((int)JitterOffsetMode.RaiseAndLower, "Raise and lower");
        yield return new CatalogItem((int)JitterOffsetMode.RaiseOnly, "Lower only");
        yield return new CatalogItem((int)JitterOffsetMode.LowerOnly, "Raise only");
    }
}
