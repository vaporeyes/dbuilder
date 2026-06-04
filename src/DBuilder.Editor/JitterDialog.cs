// ABOUTME: Dialog for applying BuilderEffects random jitter to selected map geometry and things.
// ABOUTME: Keeps compact jitter amount parsing in the editor layer while BuilderEffects owns transforms.

using System.Globalization;
using Avalonia.Controls;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class JitterDialog : PropertyDialog
{
    private readonly TextBox _positionAmount;
    private readonly TextBox _floorAmount;
    private readonly ComboBox _floorOffsetMode;
    private readonly TextBox _ceilingAmount;
    private readonly ComboBox _ceilingOffsetMode;
    private readonly TextBox _thingRotationAmount;
    private readonly TextBox _thingHeightAmount;

    public int ResultPositionAmount { get; private set; } = 16;
    public int ResultFloorAmount { get; private set; } = 16;
    public JitterOffsetMode ResultFloorOffsetMode { get; private set; } = JitterOffsetMode.RaiseAndLower;
    public int ResultCeilingAmount { get; private set; } = 16;
    public JitterOffsetMode ResultCeilingOffsetMode { get; private set; } = JitterOffsetMode.RaiseAndLower;
    public int ResultThingRotationAmount { get; private set; } = 45;
    public int ResultThingHeightAmount { get; private set; } = 16;

    public JitterDialog(string title)
        : base(title)
    {
        _positionAmount = AddField("Position amount", ResultPositionAmount.ToString(CultureInfo.InvariantCulture));
        _floorAmount = AddField("Floor amount", ResultFloorAmount.ToString(CultureInfo.InvariantCulture));
        _floorOffsetMode = AddCombo("Floor offset mode", FloorOffsetModeItems(), (int)ResultFloorOffsetMode);
        _ceilingAmount = AddField("Ceiling amount", ResultCeilingAmount.ToString(CultureInfo.InvariantCulture));
        _ceilingOffsetMode = AddCombo("Ceiling offset mode", CeilingOffsetModeItems(), (int)ResultCeilingOffsetMode);
        _thingRotationAmount = AddField("Thing angle amount", ResultThingRotationAmount.ToString(CultureInfo.InvariantCulture));
        _thingHeightAmount = AddField("Thing height amount", ResultThingHeightAmount.ToString(CultureInfo.InvariantCulture));
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
