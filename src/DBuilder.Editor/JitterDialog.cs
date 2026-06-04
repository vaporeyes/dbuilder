// ABOUTME: Dialog for applying BuilderEffects random jitter to selected map geometry and things.
// ABOUTME: Keeps compact jitter amount parsing in the editor layer while BuilderEffects owns transforms.

using System.Globalization;
using Avalonia.Controls;

namespace DBuilder.Editor;

public sealed class JitterDialog : PropertyDialog
{
    private readonly TextBox _positionAmount;
    private readonly TextBox _floorAmount;
    private readonly TextBox _ceilingAmount;
    private readonly TextBox _thingRotationAmount;

    public int ResultPositionAmount { get; private set; } = 16;
    public int ResultFloorAmount { get; private set; } = 16;
    public int ResultCeilingAmount { get; private set; } = 16;
    public int ResultThingRotationAmount { get; private set; } = 45;

    public JitterDialog(string title)
        : base(title)
    {
        _positionAmount = AddField("Position amount", ResultPositionAmount.ToString(CultureInfo.InvariantCulture));
        _floorAmount = AddField("Floor amount", ResultFloorAmount.ToString(CultureInfo.InvariantCulture));
        _ceilingAmount = AddField("Ceiling amount", ResultCeilingAmount.ToString(CultureInfo.InvariantCulture));
        _thingRotationAmount = AddField("Thing angle amount", ResultThingRotationAmount.ToString(CultureInfo.InvariantCulture));
    }

    protected override void OnConfirm()
    {
        ResultPositionAmount = Math.Max(0, ParseInt(_positionAmount, ResultPositionAmount));
        ResultFloorAmount = Math.Max(0, ParseInt(_floorAmount, ResultFloorAmount));
        ResultCeilingAmount = Math.Max(0, ParseInt(_ceilingAmount, ResultCeilingAmount));
        ResultThingRotationAmount = Math.Max(0, ParseInt(_thingRotationAmount, ResultThingRotationAmount));
    }
}
